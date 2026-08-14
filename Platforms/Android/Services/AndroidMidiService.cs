using Android.Content;
using Android.Media.Midi;
using Android.OS;
using GabSynth.Interfaces;
using GabSynth.Models;
using GabSynth.Platforms.Android.Native;
using GabSynth.Services;

namespace GabSynth.Platforms.Android.Services;

public class AndroidMidiService : IMidiService
{
    public event EventHandler<MidiEventArgs>? MidiMessageReceived;

    private MidiManager? _midiManager;
    private MidiDeviceCallbackHandler? _deviceCallback;

    // 1. PINNING: Keep active connection handles alive
    private readonly Dictionary<int, ConnectedMidiDevice> _activeConnections = new();

    // 2. CRITICAL GC PIN: Holds C# reference to listener so C# GC doesn't destroy it mid-open
    private readonly HashSet<Java.Lang.Object> _pendingListeners = new();

    public void Initialize()
    {
        // FIX: Use process-level Application Context to avoid transient MauiContext / Activity scope disposal
        var context = global::Android.App.Application.Context;
        _midiManager = (MidiManager?)context.GetSystemService(Context.MidiService);

        if (_midiManager == null)
        {
            AppLogger.Log("MidiManager system service unavailable.");
            return;
        }

        _deviceCallback = new MidiDeviceCallbackHandler(
            onAdded: OnDeviceAdded,
            onRemoved: OnDeviceRemoved
        );

        _midiManager.RegisterDeviceCallback(_deviceCallback, new Handler(Looper.MainLooper));

        ScanAndConnect();
    }

    public void ScanAndConnect()
    {
        if (_midiManager == null) return;

        MidiDeviceInfo[] devices = _midiManager.GetDevices();
        foreach (var device in devices)
        {
            ConnectDevice(device);
        }
    }

    private void ConnectDevice(MidiDeviceInfo deviceInfo)
    {
        int deviceId = deviceInfo.Id;
        AppLogger.Log($"Attempting connection to Device ID: {deviceId}");

        try
        {
            if (deviceInfo.OutputPortCount == 0)
            {
                AppLogger.Log($"Device {deviceId} skipped: 0 output ports.");
                return;
            }

            SafeMidiDeviceOpenedListener? listener = null;

            listener = new SafeMidiDeviceOpenedListener(openedDevice =>
            {
                try
                {
                    if (openedDevice == null)
                    {
                        AppLogger.Log($"Failed: Android returned null opened device for ID {deviceId}");
                        return;
                    }

                    MidiOutputPort? outputPort = openedDevice.OpenOutputPort(0);
                    if (outputPort == null)
                    {
                        AppLogger.Log($"Failed: OutputPort(0) is null for device ID {deviceId}");
                        return;
                    }

                    var receiver = new AndroidMidiReceiver();
                    receiver.OnRawMidiMessage += (status, note, velocity) =>
                    {
                        MidiMessageReceived?.Invoke(this, new MidiEventArgs(status, note, velocity));
                    };

                    outputPort.Connect(receiver);

                    lock (_activeConnections)
                    {
                        _activeConnections[deviceId] = new ConnectedMidiDevice
                        {
                            Device = openedDevice,
                            OutputPort = outputPort,
                            Receiver = receiver
                        };
                    }

                    AppLogger.Log($"SUCCESS: Connected to MIDI device {deviceId}");
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"ERROR inside OnDeviceOpened callback for device {deviceId}", ex);
                }
            });

            lock (_pendingListeners)
            {
                _pendingListeners.Add(listener);
            }

            _midiManager?.OpenDevice(deviceInfo, listener, new Handler(Looper.MainLooper));
        }
        catch (Exception ex)
        {
            AppLogger.Log($"ERROR in ConnectDevice for device {deviceId}", ex);
        }
    }

    private void OnDeviceAdded(MidiDeviceInfo deviceInfo)
    {
        // Safe dispatch on main thread when hardware is hot-plugged
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectDevice(deviceInfo);
        });
    }

    private void OnDeviceRemoved(MidiDeviceInfo deviceInfo)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            int deviceId = deviceInfo.Id;

            lock (_activeConnections)
            {
                if (_activeConnections.TryGetValue(deviceId, out var connection))
                {
                    try
                    {
                        if (connection.OutputPort != null && connection.Receiver != null)
                        {
                            connection.OutputPort.Disconnect(connection.Receiver);
                            connection.OutputPort.Close();
                        }
                        connection.Device?.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GabSynth] Error disconnecting device {deviceId}: {ex.Message}");
                    }
                    finally
                    {
                        _activeConnections.Remove(deviceId);
                    }
                }
            }
        });
    }

    private class ConnectedMidiDevice
    {
        public MidiDevice? Device { get; set; }
        public MidiOutputPort? OutputPort { get; set; }
        public AndroidMidiReceiver? Receiver { get; set; }
    }

    private class MidiDeviceCallbackHandler : MidiManager.DeviceCallback
    {
        private readonly Action<MidiDeviceInfo> _onAdded;
        private readonly Action<MidiDeviceInfo> _onRemoved;

        public MidiDeviceCallbackHandler(Action<MidiDeviceInfo> onAdded, Action<MidiDeviceInfo> onRemoved)
        {
            _onAdded = onAdded;
            _onRemoved = onRemoved;
        }

        public override void OnDeviceAdded(MidiDeviceInfo? device)
        {
            if (device != null) _onAdded(device);
        }

        public override void OnDeviceRemoved(MidiDeviceInfo? device)
        {
            if (device != null) _onRemoved(device);
        }
    }

    private class SafeMidiDeviceOpenedListener : Java.Lang.Object, MidiManager.IOnDeviceOpenedListener
    {
        private readonly Action<MidiDevice?> _onOpened;

        public SafeMidiDeviceOpenedListener(Action<MidiDevice?> onOpened)
        {
            _onOpened = onOpened;
        }

        public void OnDeviceOpened(MidiDevice? device)
        {
            _onOpened?.Invoke(device);
        }
    }
}