namespace GabSynth.Models;

public class EffectState
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<EffectParameterState> Parameters { get; set; } = new();
}

public class EffectParameterState
{
    public string Name { get; set; } = string.Empty;
    public float Value { get; set; }
    public int? MappedCc { get; set; }
}