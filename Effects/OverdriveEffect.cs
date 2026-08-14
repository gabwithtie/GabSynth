namespace GabSynth.Effects;

public class OverdriveEffect : IAudioEffect
{
    public string Name => "Overdrive";
    public bool IsEnabled { get; set; } = false;

    public EffectParameter Drive { get; } = new("Drive", 1.0f, 10.0f, 2.0f);
    public EffectParameter Mix { get; } = new("Mix", 0.0f, 1.0f, 0.5f);

    public IReadOnlyList<EffectParameter> Parameters => new[] { Drive, Mix };

    public void Initialize(int sampleRate) { }

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled) return;

        float drive = Drive.Value;
        float mix = Mix.Value;

        for (int i = 0; i < buffer.Length; i++)
        {
            float input = buffer[i];
            float driven = Math.Clamp(input * drive, -1.5f, 1.5f);

            // Soft-clipping cubic curve
            float distorted = driven - (driven * driven * driven / 3.0f);

            buffer[i] = (input * (1.0f - mix)) + (distorted * mix);
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