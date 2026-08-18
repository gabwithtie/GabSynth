using MeltySynth;
using GabSynth.Config;
using GabSynth.Effects;
using GabSynth.Interfaces;
using GabSynth.Models;
using GabSynth.Services;

namespace GabSynth.Audio;

public class MeltyAudioProcessor : IAudioProcessor
{
    public MixerChannel[] Channels { get; } = new MixerChannel[BuildSettings.MaxMixerChannels];
    public EffectsChain MasterEffects { get; } = new();

    private float[] _mixBufferLeft = Array.Empty<float>();
    private float[] _mixBufferRight = Array.Empty<float>();
    private float[] _channelBufferLeft = Array.Empty<float>();
    private float[] _channelBufferRight = Array.Empty<float>();
    private float[] _channelMonoBuffer = Array.Empty<float>();

    private readonly object _renderLock = new();
    private int _sampleRate = 48000;

    public MeltyAudioProcessor()
    {
        for (int i = 0; i < BuildSettings.MaxMixerChannels; i++)
        {
            Channels[i] = new MixerChannel(i + 1);
        }
    }

    public void Initialize(int sampleRate)
    {
        _sampleRate = sampleRate;
        MasterEffects.Initialize(sampleRate);
    }

    public void ProcessMidiMessage(byte status, byte note, byte velocity)
    {
        byte command = (byte)(status & 0xF0);
        byte midiChannel = (byte)(status & 0x0F);

        lock (_renderLock)
        {
            for (int i = 0; i < Channels.Length; i++)
            {
                // Route strictly based on matching MIDI channel index
                if (midiChannel == i && Channels[i].IsEnabled && Channels[i].Synth != null)
                {
                    Channels[i].Synth.ProcessMidiMessage(0, command, note, velocity);
                }
            }
        }
    }

    public void ProcessChannelMidi(int channelIndex, byte internalMidiChannel, byte command, byte note, byte velocity)
    {
        lock (_renderLock)
        {
            if (channelIndex >= 0 && channelIndex < Channels.Length)
            {
                var channel = Channels[channelIndex];
                if (channel.IsEnabled && channel.Synth != null)
                {
                    // Clean command byte to ensure correct MeltySynth status execution
                    byte cleanCommand = (byte)(command & 0xF0);
                    channel.Synth.ProcessMidiMessage(internalMidiChannel, cleanCommand, note, velocity);
                }
            }
        }
    }

    public void RenderAudio(Span<float> outputBuffer)
    {
        int bufferSize = outputBuffer.Length;
        EnsureBuffers(bufferSize);

        Span<float> mixLeft = _mixBufferLeft.AsSpan(0, bufferSize);
        Span<float> mixRight = _mixBufferRight.AsSpan(0, bufferSize);
        mixLeft.Clear();
        mixRight.Clear();

        lock (_renderLock)
        {
            foreach (var channel in Channels)
            {
                if (!channel.IsEnabled || channel.Synth == null || channel.Volume <= 0.001f) continue;

                Span<float> chLeft = _channelBufferLeft.AsSpan(0, bufferSize);
                Span<float> chRight = _channelBufferRight.AsSpan(0, bufferSize);

                // 1. Synthesize Full Stereo Audio
                channel.Synth.Render(chLeft, chRight);

                // 2. Process Channel Audio Effects (Dual-Mono/Stereo processing)
                Span<float> chMono = _channelMonoBuffer.AsSpan(0, bufferSize);
                for (int i = 0; i < bufferSize; i++)
                {
                    chMono[i] = (chLeft[i] + chRight[i]) * 0.5f;
                }

                channel.Effects.ProcessAudio(chMono);

                // 3. Sum processed signal back into Stereo Master Mix preserving SoundFont panning
                for (int i = 0; i < bufferSize; i++)
                {
                    float effectContribution = chMono[i] * channel.Volume;

                    // Blend processed effect with stereo synthesis signal
                    mixLeft[i] += (chLeft[i] * channel.Volume) + effectContribution;
                    mixRight[i] += (chRight[i] * channel.Volume) + effectContribution;
                }
            }
        }

        // Collapse Master Stereo Mix down to target mono output buffer
        for (int i = 0; i < bufferSize; i++)
        {
            outputBuffer[i] = (mixLeft[i] + mixRight[i]) * 0.5f;
        }

        // 4. Global Master Audio Effects & Soft Clipping
        MasterEffects.ProcessAudio(outputBuffer);
        ApplyMasterSoftClippingAndLimiting(outputBuffer);
    }

    public void LoadSoundFontToChannel(int channelIndex, string filePath)
    {
        if (channelIndex < 0 || channelIndex >= Channels.Length) return;
        if (!File.Exists(filePath)) return;

        lock (_renderLock)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var soundFont = new SoundFont(stream);
                var settings = new SynthesizerSettings(_sampleRate) { BlockSize = 64 };

                var channel = Channels[channelIndex];
                channel.Synth = new Synthesizer(soundFont, settings);

                // Initialize preset on both internal channels for top-note pitch bend routing
                channel.Synth.ProcessMidiMessage(0, 0xC0, 0, 0);
                channel.Synth.ProcessMidiMessage(1, 0xC0, 0, 0);

                channel.CurrentSoundFontPath = filePath;
                channel.IsEnabled = true;
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[MeltySynth] Error loading SoundFont into CH {channelIndex + 1}", ex);
            }
        }
    }

    private void EnsureBuffers(int bufferSize)
    {
        if (_mixBufferLeft.Length < bufferSize)
        {
            _mixBufferLeft = new float[bufferSize];
            _mixBufferRight = new float[bufferSize];
            _channelBufferLeft = new float[bufferSize];
            _channelBufferRight = new float[bufferSize];
            _channelMonoBuffer = new float[bufferSize];
        }
    }

    private void ApplyMasterSoftClippingAndLimiting(Span<float> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (float)Math.Tanh(buffer[i]);
        }
    }
}