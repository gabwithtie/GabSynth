using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GabSynth.Effects;

public class EffectParameter : INotifyPropertyChanged
{
    public string Name { get; }
    public float MinValue { get; }
    public float MaxValue { get; }
    public float DefaultValue { get; } // 👈 Remembers default baseline value

    private float _value;
    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, MinValue, MaxValue);
            if (Math.Abs(_value - clamped) > 0.0001f)
            {
                _value = clamped;
                OnPropertyChanged();
            }
        }
    }

    public int? MappedCc { get; set; }
    public bool IsLearning { get; set; }

    public string MappingStatusText => MappedCc.HasValue ? $"CC #{MappedCc.Value}" : (IsLearning ? "Learn..." : "Map CC");

    public EffectParameter(string name, float min, float max, float defaultValue)
    {
        Name = name;
        MinValue = min;
        MaxValue = max;
        DefaultValue = defaultValue;
        _value = defaultValue;
    }

    // 🔄 Resets value back to factory default
    public void ResetToDefault()
    {
        Value = DefaultValue;
        MappedCc = null; // Clear MIDI mapping unless overridden by patch
        IsLearning = false;
    }

    public void UpdateFromMidiCc(byte ccValue)
    {
        float normalized = ccValue / 127.0f;
        Value = MinValue + (normalized * (MaxValue - MinValue));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}