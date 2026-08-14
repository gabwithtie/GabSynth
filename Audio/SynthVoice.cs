namespace GabSynth.Audio;

public enum EnvelopeStage { Off, Attack, Decay, Sustain, Release }

public class SynthVoice
{
    public bool IsActive { get; private set; }
    public byte CurrentNote { get; private set; }
    public long Age { get; set; } // Used for voice stealing (LRU)

    private float _phase;
    private float _frequency;
    private float _velocityGain;

    // Simple ADSR Envelope Settings
    private EnvelopeStage _stage = EnvelopeStage.Off;
    private float _envelopeLevel = 0.0f;

    private const float AttackRate = 0.005f;  // Fast Attack
    private const float DecayRate = 0.001f;   // Decay speed
    private const float SustainLevel = 0.6f;  // Sustain amplitude
    private const float ReleaseRate = 0.002f; // Smooth Release

    public void NoteOn(byte note, byte velocity, int sampleRate)
    {
        CurrentNote = note;
        _frequency = (float)(440.0 * Math.Pow(2.0, (note - 69) / 12.0));
        _velocityGain = velocity / 127.0f;
        _stage = EnvelopeStage.Attack;
        IsActive = true;
    }

    public void NoteOff()
    {
        _stage = EnvelopeStage.Release;
    }

    public float ProcessNextSample(int sampleRate)
    {
        if (!IsActive) return 0.0f;

        // 1. Process ADSR Envelope
        switch (_stage)
        {
            case EnvelopeStage.Attack:
                _envelopeLevel += AttackRate;
                if (_envelopeLevel >= 1.0f)
                {
                    _envelopeLevel = 1.0f;
                    _stage = EnvelopeStage.Decay;
                }
                break;

            case EnvelopeStage.Decay:
                _envelopeLevel -= DecayRate;
                if (_envelopeLevel <= SustainLevel)
                {
                    _envelopeLevel = SustainLevel;
                    _stage = EnvelopeStage.Sustain;
                }
                break;

            case EnvelopeStage.Sustain:
                _envelopeLevel = SustainLevel;
                break;

            case EnvelopeStage.Release:
                _envelopeLevel -= ReleaseRate;
                if (_envelopeLevel <= 0.001f)
                {
                    _envelopeLevel = 0.0f;
                    _stage = EnvelopeStage.Off;
                    IsActive = false;
                    return 0.0f;
                }
                break;
        }

        // 2. Waveform Generator (Band-limited Sawtooth Wave)
        float phaseIncrement = _frequency / sampleRate;
        _phase += phaseIncrement;
        if (_phase >= 1.0f) _phase -= 1.0f;

        // Sawtooth wave: 2 * phase - 1
        float rawWave = (2.0f * _phase) - 1.0f;

        return rawWave * _envelopeLevel * _velocityGain;
    }
}