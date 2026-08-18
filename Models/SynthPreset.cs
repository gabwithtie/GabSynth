namespace GabSynth.Models;

public class SynthPreset
{
    public int SlotNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ChannelPresetState> Channels { get; set; } = new();
    public List<EffectState> MasterMidiEffects { get; set; } = new();
    public List<EffectState> MasterAudioEffects { get; set; } = new();
}

public class ChannelPresetState
{
    public int ChannelId { get; set; }
    public bool IsEnabled { get; set; }
    public float Volume { get; set; }
    public string SoundFontPath { get; set; } = string.Empty;
    public string InstrumentName { get; set; } = string.Empty;
    public List<EffectState> MidiEffects { get; set; } = new();
    public List<EffectState> AudioEffects { get; set; } = new();
}