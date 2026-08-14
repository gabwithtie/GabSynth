namespace GabSynth.Effects;

public class VelocityScalerEffect : IAudioEffect
{
    public string Name => "Velocity Dynamics";
    public bool IsEnabled { get; set; } = true;

    // Minimum output velocity for light key taps
    public EffectParameter MinVelocity { get; } = new("Min Velocity", 1.0f, 127.0f, 1.0f);

    // Maximum output velocity for hard key hits
    public EffectParameter MaxVelocity { get; } = new("Max Velocity", 1.0f, 127.0f, 127.0f);

    public IReadOnlyList<EffectParameter> Parameters => new[] { MinVelocity, MaxVelocity };

    public void Initialize(int sampleRate)
    {
        // No audio sample rate initialization needed for MIDI effects
    }

    /// <summary>
    /// Linearly interpolates input velocity [1..127] into [MinVelocity..MaxVelocity]
    /// </summary>
    public byte ProcessVelocity(byte inputVelocity)
    {
        if (!IsEnabled) return inputVelocity;

        float min = Math.Clamp(MinVelocity.Value, 1.0f, 127.0f);
        float max = Math.Clamp(MaxVelocity.Value, 1.0f, 127.0f);

        // Normalize input (1 to 127) to factor [0.0 .. 1.0]
        float t = Math.Clamp((inputVelocity - 1.0f) / 126.0f, 0.0f, 1.0f);

        // Linear interpolation
        float scaledVelocity = min + t * (max - min);

        return (byte)Math.Clamp((int)Math.Round(scaledVelocity), 1, 127);
    }

    public void Process(Span<float> buffer)
    {
        // Pass-through audio untouched (this is a MIDI effect)
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