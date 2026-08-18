using System.Text.Json;
using GabSynth.Effects;
using GabSynth.Models;

namespace GabSynth.Services;

public class PresetManager
{
    private readonly string _presetsFolder;

    public PresetManager()
    {
        _presetsFolder = Path.Combine(FileSystem.Current.AppDataDirectory, "Presets");
        if (!Directory.Exists(_presetsFolder))
            Directory.CreateDirectory(_presetsFolder);
    }

    public void SaveFullSnapshot(int slot, MixerChannel[] channels, EffectsChain masterEffects, string name = "")
    {
        var preset = new SynthPreset
        {
            SlotNumber = slot,
            Name = string.IsNullOrWhiteSpace(name) ? $"Preset Slot {slot}" : name
        };

        foreach (var ch in channels)
        {
            var chState = new ChannelPresetState
            {
                ChannelId = ch.ChannelId,
                IsEnabled = ch.IsEnabled,
                Volume = ch.Volume,
                SoundFontPath = ch.CurrentSoundFontPath,
                InstrumentName = ch.CurrentInstrumentName
            };

            SerializeEffects(ch.Effects.MidiEffects, chState.MidiEffects);
            SerializeEffects(ch.Effects.AudioEffects, chState.AudioEffects);

            preset.Channels.Add(chState);
        }

        SerializeEffects(masterEffects.MidiEffects, preset.MasterMidiEffects);
        SerializeEffects(masterEffects.AudioEffects, preset.MasterAudioEffects);

        string filePath = GetFilePath(slot);
        string json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    public SynthPreset? LoadPresetData(int slot)
    {
        string filePath = GetFilePath(slot);
        if (!File.Exists(filePath)) return null;

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<SynthPreset>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void ApplyPreset(SynthPreset preset, MixerChannel[] channels, EffectsChain masterEffects, Action<int, string> loadSoundFontCallback)
    {
        // 1. Restore Mixer Channels
        foreach (var chState in preset.Channels)
        {
            var channel = channels.FirstOrDefault(c => c.ChannelId == chState.ChannelId);
            if (channel == null) continue;

            channel.IsEnabled = chState.IsEnabled;
            channel.Volume = chState.Volume;
            channel.CurrentInstrumentName = chState.InstrumentName;

            // Reload SoundFont if file still exists
            if (!string.IsNullOrEmpty(chState.SoundFontPath) && File.Exists(chState.SoundFontPath))
            {
                loadSoundFontCallback(channel.ChannelId - 1, chState.SoundFontPath);
            }

            // Re-instantiate Channel Local Effects
            RestoreEffectsList(chState.MidiEffects, chState.AudioEffects, channel.Effects);
        }

        // 2. Restore Master Effects
        RestoreEffectsList(preset.MasterMidiEffects, preset.MasterAudioEffects, masterEffects);
    }

    private void RestoreEffectsList(List<EffectState> midiStates, List<EffectState> audioStates, EffectsChain chain)
    {
        chain.MidiEffects.Clear();
        chain.AudioEffects.Clear();

        foreach (var state in midiStates)
        {
            if (EffectsChain.CreateEffectByName(state.Name) is IMidiEffect effect)
            {
                effect.IsEnabled = state.IsEnabled;
                ApplyParameters(state.Parameters, effect.Parameters);
                chain.MidiEffects.Add(effect);
            }
        }

        foreach (var state in audioStates)
        {
            if (EffectsChain.CreateEffectByName(state.Name) is IAudioEffect effect)
            {
                effect.Initialize(48000);
                effect.IsEnabled = state.IsEnabled;
                ApplyParameters(state.Parameters, effect.Parameters);
                chain.AudioEffects.Add(effect);
            }
        }
    }

    private void ApplyParameters(List<EffectParameterState> paramStates, IReadOnlyList<EffectParameter> targetParams)
    {
        foreach (var pState in paramStates)
        {
            var param = targetParams.FirstOrDefault(p => p.Name == pState.Name);
            if (param != null)
            {
                param.Value = pState.Value;
                param.MappedCc = pState.MappedCc;
            }
        }
    }

    public bool HasPreset(int slot) => File.Exists(GetFilePath(slot));

    private string GetFilePath(int slot) => Path.Combine(_presetsFolder, $"preset_slot_{slot}.json");

    // Place these private helper methods inside GabSynth.Services.PresetManager

    private void SerializeEffects(IEnumerable<IMidiEffect> sourceEffects, List<EffectState> targetList)
    {
        foreach (var effect in sourceEffects)
        {
            var effectState = new EffectState
            {
                Name = effect.Name,
                IsEnabled = effect.IsEnabled
            };

            foreach (var param in effect.Parameters)
            {
                effectState.Parameters.Add(new EffectParameterState
                {
                    Name = param.Name,
                    Value = param.Value,
                    MappedCc = param.MappedCc
                });
            }

            targetList.Add(effectState);
        }
    }

    private void SerializeEffects(IEnumerable<IAudioEffect> sourceEffects, List<EffectState> targetList)
    {
        foreach (var effect in sourceEffects)
        {
            var effectState = new EffectState
            {
                Name = effect.Name,
                IsEnabled = effect.IsEnabled
            };

            foreach (var param in effect.Parameters)
            {
                effectState.Parameters.Add(new EffectParameterState
                {
                    Name = param.Name,
                    Value = param.Value,
                    MappedCc = param.MappedCc
                });
            }

            targetList.Add(effectState);
        }
    }
}