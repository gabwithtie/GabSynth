using MeltySynth;
using GabSynth.Effects;
using GabSynth.Interfaces;
using GabSynth.Models;
using GabSynth.Services;

namespace GabSynth.Audio;

public class MeltyAudioProcessor : IAudioProcessor
{
    private Synthesizer? _synthesizer;
    private float[] _leftBuffer = Array.Empty<float>();
    private float[] _rightBuffer = Array.Empty<float>();
    private readonly object _renderLock = new();
    private int _sampleRate = 48000;

    public AudioEffectsChain EffectsChain { get; } = new();
    public PresetManager Presets { get; } = new();
    public string CurrentSoundFontPath { get; private set; } = string.Empty;

    /// <summary>
    /// Event fired when a MIDI CC preset load changes the SoundFont instrument.
    /// </summary>
    public event Action<string>? OnSoundFontChangedFromPreset;

    public void Initialize(int sampleRate)
    {
        _sampleRate = sampleRate;
        EffectsChain.Initialize(sampleRate);
    }

    public void LoadSoundFont(string filePath)
    {
        if (!File.Exists(filePath)) return;

        lock (_renderLock)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var soundFont = new SoundFont(stream);
                var settings = new SynthesizerSettings(_sampleRate) { BlockSize = 64 };

                _synthesizer = new Synthesizer(soundFont, settings);
                CurrentSoundFontPath = filePath;
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[MeltySynth] Error loading SoundFont", ex);
            }
        }
    }

    public void SavePreset(int slotNumber, string presetName = "")
    {
        Presets.SavePreset(slotNumber, CurrentSoundFontPath, EffectsChain, presetName);
    }

    public void LoadPreset(int slotNumber)
    {
        var preset = Presets.LoadPresetData(slotNumber);
        if (preset == null)
        {
            AppLogger.Log($"[Preset] Slot #{slotNumber} is empty!");
            return;
        }

        // 1. Restore Audio Effects parameters & CC mappings
        Presets.ApplyPresetToEffects(preset, EffectsChain);

        // 2. Reload SoundFont if different
        if (!string.IsNullOrEmpty(preset.SoundFontPath) && File.Exists(preset.SoundFontPath))
        {
            if (preset.SoundFontPath != CurrentSoundFontPath)
            {
                LoadSoundFont(preset.SoundFontPath);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnSoundFontChangedFromPreset?.Invoke(preset.SoundFontPath);
                });
            }
        }

        AppLogger.Log($"[Preset] Successfully Loaded Slot #{slotNumber}: '{preset.Name}'");
    }

    public void ProcessMidiMessage(byte status, byte note, byte velocity)
    {
        byte command = (byte)(status & 0xF0);
        byte channel = (byte)(status & 0x0F);

        if (command == 0xB0) // MIDI Control Change
        {
            byte ccNumber = note;
            byte ccValue = velocity;

            // 🎛️ CC #36 to CC #43: Hardware Button Preset Trigger
            if (ccNumber >= 36 && ccNumber <= 43)
            {
                // Only trigger on button press down (Value > 0)
                if (ccValue > 0)
                {
                    int targetSlot = ccNumber - 35; // CC#36 -> Slot 1, CC#43 -> Slot 8
                    LoadPreset(targetSlot);
                }
                return;
            }

            // Route all other CC messages to Effect parameter knobs
            EffectsChain.HandleControlChange(ccNumber, ccValue);
            return;
        }

        if (_synthesizer == null) return;

        lock (_renderLock)
        {
            if (command == 0x90 && velocity > 2)
            {
                _synthesizer.NoteOn(channel, note, velocity);
            }
            else if (command == 0x80 || (command == 0x90 && velocity <= 2))
            {
                _synthesizer.NoteOff(channel, note);
            }
            else
            {
                _synthesizer.ProcessMidiMessage(channel, command, note, velocity);
            }
        }
    }

    public void RenderAudio(Span<float> outputBuffer)
    {
        if (_synthesizer == null)
        {
            outputBuffer.Clear();
            return;
        }

        int bufferSize = outputBuffer.Length;

        if (_leftBuffer.Length < bufferSize)
        {
            _leftBuffer = new float[bufferSize];
            _rightBuffer = new float[bufferSize];
        }

        Span<float> leftSpan = _leftBuffer.AsSpan(0, bufferSize);
        Span<float> rightSpan = _rightBuffer.AsSpan(0, bufferSize);

        lock (_renderLock)
        {
            _synthesizer.Render(leftSpan, rightSpan);
        }

        for (int i = 0; i < bufferSize; i++)
        {
            outputBuffer[i] = (leftSpan[i] + rightSpan[i]) * 0.5f;
        }

        EffectsChain.Process(outputBuffer);
        ApplyMasterSoftClippingAndLimiting(outputBuffer);
    }

    private void ApplyMasterSoftClippingAndLimiting(Span<float> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (float)Math.Tanh(buffer[i]);
        }
    }
}