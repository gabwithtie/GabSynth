namespace GabSynth.Effects;

public interface IMidiEffect
{
    string Name { get; }
    bool IsEnabled { get; set; }
    IReadOnlyList<EffectParameter> Parameters { get; }

    // Distinctly handles MIDI data before it reaches the synthesizer
    void ProcessMidi(ref byte status, ref byte note, ref byte velocity);
    void ResetToDefaults();
}