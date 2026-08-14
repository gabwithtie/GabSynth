namespace GabSynth.Effects;

public class LowPassFilterEffect : IAudioEffect
{
    public string Name => "Lowpass Filter";
    public bool IsEnabled { get; set; } = true;

    public EffectParameter Cutoff { get; } = new("Cutoff", 20f, 18000f, 8000f);
    public EffectParameter Resonance { get; } = new("Resonance", 0.1f, 5.0f, 0.707f);

    public IReadOnlyList<EffectParameter> Parameters => new[] { Cutoff, Resonance };

    // TPT State variables (internal feedback memory)
    private float _s1 = 0f;
    private float _s2 = 0f;
    private int _sampleRate = 48000;

    public void Initialize(int sampleRate)
    {
        _sampleRate = sampleRate;
        Reset();
    }

    public void Reset()
    {
        _s1 = 0f;
        _s2 = 0f;
    }

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled) return;

        // 1. Clamp cutoff safely below Nyquist frequency
        float clampedCutoff = Math.Clamp(Cutoff.Value, 20f, _sampleRate * 0.49f);

        // 2. Compute TPT SVF Coefficients (Cytomic / Simper topology)
        float g = (float)Math.Tan(Math.PI * clampedCutoff / _sampleRate);
        float k = 1.0f / Math.Max(Resonance.Value, 0.1f); // Resonance / Q factor

        float a1 = 1.0f / (1.0f + g * (g + k));
        float a2 = g * a1;
        float a3 = g * a2;

        // 3. Process Audio Buffer
        for (int i = 0; i < buffer.Length; i++)
        {
            float x = buffer[i];

            // Calculate internal node values
            float v1 = a1 * _s1 + a2 * (x - _s2);
            float v2 = _s2 + a3 * _s1 + a2 * (x - _s2);

            // Update filter state registers
            _s1 = 2.0f * v1 - _s1;
            _s2 = 2.0f * v2 - _s2;

            // Output Lowpass (v2)
            buffer[i] = v2;
        }
    }
    public void ResetToDefaults()
    {
        IsEnabled = false; // Or default state (e.g. true for EQ / MasterGain / VelocityScaler)
        foreach (var param in Parameters)
        {
            param.ResetToDefault();
        }
    }
}