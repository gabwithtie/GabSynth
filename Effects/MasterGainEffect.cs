namespace GabSynth.Effects;

public class MasterGainEffect : IAudioEffect
{
    public string Name => "Master Gain";
    public bool IsEnabled { get; set; } = true;

    // dB scale from -24dB (very soft) to +12dB (boost)
    public EffectParameter GainDb { get; } = new("Gain (dB)", -24.0f, 12.0f, 0.0f);

    public IReadOnlyList<EffectParameter> Parameters => new[] { GainDb };

    public void Initialize(int sampleRate)
    {
        // No sample-rate dependent initialization required
    }

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled) return;

        // Convert dB to linear scaling factor
        float linearGain = MathF.Pow(10.0f, GainDb.Value / 20.0f);

        // Don't waste CPU cycles multiplying by 1.0
        if (MathF.Abs(linearGain - 1.0f) < 0.001f) return;

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] *= linearGain;
        }
    }
}