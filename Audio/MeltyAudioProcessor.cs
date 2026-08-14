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

    // 🎯 Pitch Bend Auto-Reset & Deadband Defaults
    private const int PitchBendCenter = 8192;
    private const int PitchBendDeadband = 300;
    private bool _isWaitingForPitchBendCenter = false;
    private int _lastPhysicalPitchBend = PitchBendCenter;

    // 🎹 Dynamic Voice Allocation (Per-Note MIDI Channel Pool)
    // Channel 9 is skipped because it is reserved for GM Drums
    private readonly List<byte> _availableChannels = new() { 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15 };
    private readonly Dictionary<byte, byte> _noteToChannelMap = new();

    public AudioEffectsChain EffectsChain { get; } = new();
    public PresetManager Presets { get; } = new();
    public string CurrentSoundFontPath { get; private set; } = string.Empty;

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

                ResetChannelPool();
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

        Presets.ApplyPresetToEffects(preset, EffectsChain);

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

        // 1. MIDI Control Change (Presets & Knob Mappings)
        if (command == 0xB0)
        {
            byte ccNumber = note;
            byte ccValue = velocity;

            if (ccNumber >= 36 && ccNumber <= 43)
            {
                if (ccValue > 0)
                {
                    int targetSlot = ccNumber - 35;
                    LoadPreset(targetSlot);
                }
                return;
            }

            EffectsChain.HandleControlChange(ccNumber, ccValue);
            return;
        }

        // 2. MIDI Pitch Bend (0xE0) — Target Top Note Only
        if (command == 0xE0)
        {
            int rawPitchValue = (velocity << 7) | note; // 14-bit value (0 .. 16383)
            _lastPhysicalPitchBend = rawPitchValue;

            if (_isWaitingForPitchBendCenter)
            {
                if (Math.Abs(rawPitchValue - PitchBendCenter) <= PitchBendDeadband)
                {
                    _isWaitingForPitchBendCenter = false; // Unlocked!
                }
            }

            lock (_renderLock)
            {
                UpdatePitchBendsAcrossChannels();
            }
            return;
        }

        if (_synthesizer == null) return;

        // 3. Note On / Note Off Processing
        lock (_renderLock)
        {
            if (command == 0x90 && velocity > 2) // Note On
            {
                byte assignedChannel = AssignChannelForNote(note);

                // Arm pitch bend lock if wheel is held off-center
                _isWaitingForPitchBendCenter = Math.Abs(_lastPhysicalPitchBend - PitchBendCenter) > PitchBendDeadband;

                // Optional Velocity Scaling Processor
                byte processedVelocity = velocity;
                var velEffect = EffectsChain.Effects.OfType<VelocityScalerEffect>().FirstOrDefault();
                if (velEffect != null)
                {
                    processedVelocity = velEffect.ProcessVelocity(velocity);
                }

                _synthesizer.NoteOn(assignedChannel, note, processedVelocity);

                // Re-evaluate top note and route pitch bend
                UpdatePitchBendsAcrossChannels();
            }
            else if (command == 0x80 || (command == 0x90 && velocity <= 2)) // Note Off
            {
                if (_noteToChannelMap.TryGetValue(note, out byte assignedChannel))
                {
                    _synthesizer.NoteOff(assignedChannel, note);
                    ReleaseChannelForNote(note);

                    // Re-evaluate top note and route pitch bend to remaining held notes
                    UpdatePitchBendsAcrossChannels();
                }
            }
        }
    }

    /// <summary>
    /// Sends active pitch bend ONLY to the highest active note's MIDI channel.
    /// All other active notes are locked to PitchBendCenter (8192).
    /// </summary>
    private void UpdatePitchBendsAcrossChannels()
    {
        if (_synthesizer == null || _noteToChannelMap.Count == 0) return;

        // Identify the highest active note
        byte topNote = _noteToChannelMap.Keys.Max();
        byte topChannel = _noteToChannelMap[topNote];

        int bendForTopNote = _isWaitingForPitchBendCenter ? PitchBendCenter : _lastPhysicalPitchBend;

        byte topLsb = (byte)(bendForTopNote & 0x7F);
        byte topMsb = (byte)((bendForTopNote >> 7) & 0x7F);

        byte centerLsb = 0x00;
        byte centerMsb = 0x40; // 8192 Center

        foreach (var kvp in _noteToChannelMap)
        {
            byte channel = kvp.Value;

            if (channel == topChannel)
            {
                _synthesizer.ProcessMidiMessage(channel, 0xE0, topLsb, topMsb);
            }
            else
            {
                _synthesizer.ProcessMidiMessage(channel, 0xE0, centerLsb, centerMsb);
            }
        }
    }

    private byte AssignChannelForNote(byte note)
    {
        if (_noteToChannelMap.TryGetValue(note, out byte existingCh))
        {
            return existingCh;
        }

        if (_availableChannels.Count > 0)
        {
            byte channel = _availableChannels[0];
            _availableChannels.RemoveAt(0);
            _noteToChannelMap[note] = channel;
            return channel;
        }

        // Fallback if all channels are occupied
        _noteToChannelMap[note] = 0;
        return 0;
    }

    private void ReleaseChannelForNote(byte note)
    {
        if (_noteToChannelMap.TryGetValue(note, out byte channel))
        {
            _noteToChannelMap.Remove(note);
            if (!_availableChannels.Contains(channel) && channel != 9)
            {
                _availableChannels.Add(channel);
            }
        }
    }

    private void ResetChannelPool()
    {
        _availableChannels.Clear();
        _availableChannels.AddRange(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15 });
        _noteToChannelMap.Clear();
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