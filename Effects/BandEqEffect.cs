namespace GabSynth.Effects;

public class BandEqEffect : IAudioEffect
{
    public string Name => "3-Band Equalizer";
    public bool IsEnabled { get; set; } = true;

    public EffectParameter LowGainDb { get; } = new("Low (dB)", -12f, 12f, 0f);
    public EffectParameter MidGainDb { get; } = new("Mid (dB)", -12f, 12f, 0f);
    public EffectParameter HighGainDb { get; } = new("High (dB)", -12f, 12f, 0f);

    public IReadOnlyList<EffectParameter> Parameters => new[] { LowGainDb, MidGainDb, HighGainDb };

    private float _lp1State;
    private float _lp2State;
    private int _sampleRate = 48000;

    public void Initialize(int sampleRate)
    {
        _sampleRate = sampleRate;
        _lp1State = 0f;
        _lp2State = 0f;
    }

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled) return;

        // Convert dB values to linear scale multipliers
        float lowGain = MathF.Pow(10f, LowGainDb.Value / 20f);
        float midGain = MathF.Pow(10f, MidGainDb.Value / 20f);
        float highGain = MathF.Pow(10f, HighGainDb.Value / 20f);

        // Crossover Frequencies: Low/Mid = 250Hz, Mid/High = 4000Hz
        float g1 = MathF.Tan(MathF.PI * 250f / _sampleRate);
        float alpha1 = g1 / (1.0f + g1);

        float g2 = MathF.Tan(MathF.PI * 4000f / _sampleRate);
        float alpha2 = g2 / (1.0f + g2);

        for (int i = 0; i < buffer.Length; i++)
        {
            float input = buffer[i];

            // Single-Pole TPT Crossover Filters
            float lp1 = _lp1State + alpha1 * (input - _lp1State);
            _lp1State = lp1 + alpha1 * (input - _lp1State);

            float lp2 = _lp2State + alpha2 * (input - _lp2State);
            _lp2State = lp2 + alpha2 * (input - _lp2State);

            // Split into 3 non-overlapping frequency bands
            float lowBand = lp1;
            float midBand = lp2 - lp1;
            float highBand = input - lp2;

            // Recombine scaled bands
            buffer[i] = (lowBand * lowGain) + (midBand * midGain) + (highBand * highGain);
        }
    }
}