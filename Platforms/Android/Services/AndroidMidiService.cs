using System;
using System.Collections.Generic;
using System.Text;
using Android.Content;
using Android.Media.Midi;
using Android.OS;

namespace GabSynth.Platforms.Android.Services
{
    using Interfaces;
    using Models;
    using Platforms.Android.Native;

    public class AndroidMidiService : IMidiService
    {
        public event EventHandler<MidiEventArgs>? MidiMessageReceived;

        public void Initialize()
        {
            ScanAndConnect();
        }

        public void ScanAndConnect()
        {
            var context = Platform.CurrentActivity ?? Microsoft.Maui.ApplicationModel.Platform.AppContext;
            var midiManager = (MidiManager?)context.GetSystemService(Context.MidiService);

            if (midiManager == null) return;

            MidiDeviceInfo[] devices = midiManager.GetDevices();
            foreach (var device in devices)
            {
                midiManager.OpenDevice(device, new MidiDeviceOpenedListener(onOpenedDevice =>
                {
                    if (onOpenedDevice == null) return;

                    MidiOutputPort outputPort = onOpenedDevice.OpenOutputPort(0);
                    var receiver = new AndroidMidiReceiver();

                    receiver.OnRawMidiMessage += (status, note, velocity) =>
                    {
                        MidiMessageReceived?.Invoke(this, new MidiEventArgs(status, note, velocity));
                    };

                    outputPort.Connect(receiver);
                }), new Handler(Looper.MainLooper));
            }
        }

        private class MidiDeviceOpenedListener : Java.Lang.Object, MidiManager.IOnDeviceOpenedListener
        {
            private readonly Action<MidiDevice?> _onOpened;
            public MidiDeviceOpenedListener(Action<MidiDevice?> onOpened) => _onOpened = onOpened;
            public void OnDeviceOpened(MidiDevice? device) => _onOpened(device);
        }
    }
}
