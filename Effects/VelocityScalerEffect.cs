namespace GabSynth.Effects;

// Now distinctly implements IMidiEffect instead of IAudioEffect
public class VelocityScalerEffect : IMidiEffect
{
    public string Name => "Velocity Dynamics";
    public bool IsEnabled { get; set; } = true;

    public EffectParameter MinVelocity { get; } = new("Min Velocity", 1.0f, 127.0f, 1.0f);
    public EffectParameter MaxVelocity { get; } = new("Max Velocity", 1.0f, 127.0f, 127.0f);

    public IReadOnlyList<EffectParameter> Parameters => new[] { MinVelocity, MaxVelocity };

    public void ProcessMidi(ref byte status, ref byte note, ref byte velocity)
    {
        if (!IsEnabled || (status & 0xF0) != 0x90 || velocity == 0) return;

        float min = Math.Clamp(MinVelocity.Value, 1.0f, 127.0f);
        float max = Math.Clamp(MaxVelocity.Value, 1.0f, 127.0f);
        float t = Math.Clamp((velocity - 1.0f) / 126.0f, 0.0f, 1.0f);

        velocity = (byte)Math.Clamp((int)Math.Round(min + t * (max - min)), 1, 127);
    }

    public void ResetToDefaults()
    {
        IsEnabled = true;
        foreach (var param in Parameters) param.ResetToDefault();
    }
}