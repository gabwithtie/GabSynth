namespace GabSynth.Models;

public class EffectParameterState
{
    public string Name { get; set; } = string.Empty;
    public float Value { get; set; }
    public int? MappedCc { get; set; }
}

public class EffectState
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<EffectParameterState> Parameters { get; set; } = new();
}

public class SynthPreset
{
    public int SlotNumber { get; set; } // 1 to 8
    public string Name { get; set; } = string.Empty;
    public string SoundFontPath { get; set; } = string.Empty;
    public List<EffectState> Effects { get; set; } = new();
}