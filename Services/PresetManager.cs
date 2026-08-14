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
        {
            Directory.CreateDirectory(_presetsFolder);
        }
    }

    public void SavePreset(int slot, string soundFontPath, AudioEffectsChain chain, string name = "")
    {
        var preset = new SynthPreset
        {
            SlotNumber = slot,
            Name = string.IsNullOrWhiteSpace(name) ? $"Preset Slot {slot}" : name,
            SoundFontPath = soundFontPath
        };

        foreach (var effect in chain.Effects)
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

            preset.Effects.Add(effectState);
        }

        string filePath = GetFilePath(slot);
        string json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        AppLogger.Log($"[Preset] Saved Slot #{slot} (CC #{slot + 35})");
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
        catch (Exception ex)
        {
            AppLogger.Log($"[Preset] Error reading slot {slot}", ex);
            return null;
        }
    }

    public void ApplyPresetToEffects(SynthPreset preset, AudioEffectsChain chain)
    {
        foreach (var effectState in preset.Effects)
        {
            var effect = chain.Effects.FirstOrDefault(e => e.Name == effectState.Name);
            if (effect == null) continue;

            effect.IsEnabled = effectState.IsEnabled;

            foreach (var paramState in effectState.Parameters)
            {
                var param = effect.Parameters.FirstOrDefault(p => p.Name == paramState.Name);
                if (param == null) continue;

                param.Value = paramState.Value;
                param.MappedCc = paramState.MappedCc;
            }
        }
    }

    public bool HasPreset(int slot) => File.Exists(GetFilePath(slot));

    private string GetFilePath(int slot) => Path.Combine(_presetsFolder, $"preset_slot_{slot}.json");
}