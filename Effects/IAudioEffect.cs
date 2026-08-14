namespace GabSynth.Effects;

public interface IAudioEffect
{
    string Name { get; }
    bool IsEnabled { get; set; }
    IReadOnlyList<EffectParameter> Parameters { get; }
    void Initialize(int sampleRate);
    void Process(Span<float> buffer);
    void ResetToDefaults(); // 👈 Reset method contract
}