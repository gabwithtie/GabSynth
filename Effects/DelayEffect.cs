namespace GabSynth.Effects;

public class DelayEffect : IAudioEffect
{
    public string Name => "Delay Echo";
    public bool IsEnabled { get; set; } = false;

    public EffectParameter DelayTimeMs { get; } = new("Time (ms)", 50f, 1000f, 300f);
    public EffectParameter Feedback { get; } = new("Feedback", 0.0f, 0.85f, 0.4f);
    public EffectParameter Mix { get; } = new("Mix", 0.0f, 1.0f, 0.3f);

    public IReadOnlyList<EffectParameter> Parameters => new[] { DelayTimeMs, Feedback, Mix };

    private float[] _delayBuffer = Array.Empty<float>();
    private int _writePos;
    private int _sampleRate = 48000;

    public void Initialize(int sampleRate)
    {
        _sampleRate = sampleRate;
        _delayBuffer = new float[sampleRate * 2]; // 2 Seconds Buffer Max
        _writePos = 0;
    }

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled || _delayBuffer.Length == 0) return;

        int delaySamples = (int)(_sampleRate * (DelayTimeMs.Value / 1000.0f));
        float feedback = Feedback.Value;
        float mix = Mix.Value;

        for (int i = 0; i < buffer.Length; i++)
        {
            int readPos = (_writePos - delaySamples + _delayBuffer.Length) % _delayBuffer.Length;
            float delayedSample = _delayBuffer[readPos];

            _delayBuffer[_writePos] = buffer[i] + (delayedSample * feedback);

            buffer[i] = (buffer[i] * (1.0f - mix)) + (delayedSample * mix);

            _writePos = (_writePos + 1) % _delayBuffer.Length;
        }
    }
}