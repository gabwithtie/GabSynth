namespace GabSynth.Effects;

public class ChorusEffect : IAudioEffect
{
    public string Name => "Chorus Modulator";
    public bool IsEnabled { get; set; } = false;

    public EffectParameter Rate { get; } = new("Rate (Hz)", 0.1f, 5.0f, 1.2f);
    public EffectParameter DepthMs { get; } = new("Depth (ms)", 0.5f, 10.0f, 3.0f);
    public EffectParameter Mix { get; } = new("Mix", 0.0f, 1.0f, 0.4f);

    public IReadOnlyList<EffectParameter> Parameters => new[] { Rate, DepthMs, Mix };

    private float[] _delayBuffer = Array.Empty<float>();
    private int _writePos;
    private float _lfoPhase;
    private int _sampleRate = 48000;

    public void Initialize(int sampleRate)
    {
        _sampleRate = sampleRate;
        // Allocate 50ms buffer
        _delayBuffer = new float[(int)(sampleRate * 0.05f)];
        _writePos = 0;
        _lfoPhase = 0f;
    }

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled || _delayBuffer.Length == 0) return;

        float mix = Mix.Value;
        float baseDelay = _sampleRate * 0.015f; // 15ms base delay offset
        float depthSamples = _sampleRate * (DepthMs.Value / 1000.0f);
        float phaseInc = MathF.Tau * Rate.Value / _sampleRate;

        int bufferLen = _delayBuffer.Length;

        for (int i = 0; i < buffer.Length; i++)
        {
            float input = buffer[i];
            _delayBuffer[_writePos] = input;

            // LFO calculates current delay position
            float lfo = MathF.Sin(_lfoPhase);
            _lfoPhase += phaseInc;
            if (_lfoPhase >= MathF.Tau) _lfoPhase -= MathF.Tau;

            float currentDelay = baseDelay + (lfo * depthSamples);
            float readPos = _writePos - currentDelay;
            while (readPos < 0) readPos += bufferLen;

            // Linear Interpolation between fractional samples
            int idxA = (int)readPos;
            int idxB = (idxA + 1) % bufferLen;
            float frac = readPos - idxA;

            float delayedSample = (_delayBuffer[idxA] * (1.0f - frac)) + (_delayBuffer[idxB] * frac);

            buffer[i] = (input * (1.0f - mix)) + (delayedSample * mix);

            _writePos = (_writePos + 1) % bufferLen;
        }
    }
}