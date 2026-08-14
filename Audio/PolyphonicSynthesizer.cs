using GabSynth.Interfaces;

namespace GabSynth.Audio;

public class PolyphonicSynthesizer : IAudioProcessor
{
    private const int MaxPolyphony = 16; // 16 simultaneous voices
    private readonly SynthVoice[] _voices = new SynthVoice[MaxPolyphony];
    private int _sampleRate = 48000;
    private long _ageCounter = 0;

    public PolyphonicSynthesizer()
    {
        for (int i = 0; i < MaxPolyphony; i++)
        {
            _voices[i] = new SynthVoice();
        }
    }

    public void Initialize(int sampleRate)
    {
        _sampleRate = sampleRate;
    }

    public void ProcessMidiMessage(byte status, byte note, byte velocity)
    {
        byte command = (byte)(status & 0xF0);

        if (command == 0x90 && velocity > 0) // Note On
        {
            _ageCounter++;

            // 1. Find an inactive voice
            SynthVoice? targetVoice = null;
            foreach (var voice in _voices)
            {
                if (!voice.IsActive)
                {
                    targetVoice = voice;
                    break;
                }
            }

            // 2. Voice Stealing: If all 16 voices are busy, steal the oldest voice
            if (targetVoice == null)
            {
                long oldestAge = long.MaxValue;
                foreach (var voice in _voices)
                {
                    if (voice.Age < oldestAge)
                    {
                        oldestAge = voice.Age;
                        targetVoice = voice;
                    }
                }
            }

            if (targetVoice != null)
            {
                targetVoice.Age = _ageCounter;
                targetVoice.NoteOn(note, velocity, _sampleRate);
            }
        }
        else if (command == 0x80 || (command == 0x90 && velocity == 0)) // Note Off
        {
            foreach (var voice in _voices)
            {
                if (voice.IsActive && voice.CurrentNote == note)
                {
                    voice.NoteOff();
                }
            }
        }
    }

    public void RenderAudio(Span<float> outputBuffer)
    {
        // Clear buffer before summing voice signals
        outputBuffer.Clear();

        for (int i = 0; i < outputBuffer.Length; i++)
        {
            float mixedSample = 0.0f;

            // Sum active voices together
            for (int v = 0; v < MaxPolyphony; v++)
            {
                if (_voices[v].IsActive)
                {
                    mixedSample += _voices[v].ProcessNextSample(_sampleRate);
                }
            }

            // Master Volume & Soft Clipping Protection (Prevents Harsh Digital Distortion)
            mixedSample *= 0.25f; // Master gain attenuation for polyphony
            outputBuffer[i] = Math.Clamp(mixedSample, -1.0f, 1.0f);
        }
    }
}