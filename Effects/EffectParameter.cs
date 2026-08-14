using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GabSynth.Effects;

public class EffectParameter : INotifyPropertyChanged
{
    private float _value;
    private int? _mappedCc;
    private bool _isLearning;

    public string Name { get; }
    public float MinValue { get; }
    public float MaxValue { get; }

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

    /// <summary>
    /// The physical MIDI CC knob number (0 - 127) mapped to this parameter.
    /// </summary>
    public int? MappedCc
    {
        get => _mappedCc;
        set { _mappedCc = value; OnPropertyChanged(); OnPropertyChanged(nameof(MappingStatusText)); }
    }

    /// <summary>
    /// Set to true when waiting for the user to twist a knob on their MIDI keyboard.
    /// </summary>
    public bool IsLearning
    {
        get => _isLearning;
        set { _isLearning = value; OnPropertyChanged(); OnPropertyChanged(nameof(MappingStatusText)); }
    }

    public string MappingStatusText => IsLearning
        ? "Turn Knob..."
        : (MappedCc.HasValue ? $"CC #{MappedCc.Value}" : "Learn CC");

    public event PropertyChangedEventHandler? PropertyChanged;

    public EffectParameter(string name, float minValue, float maxValue, float defaultValue)
    {
        Name = name;
        MinValue = minValue;
        MaxValue = maxValue;
        _value = defaultValue;
    }

    public void UpdateFromMidiCc(byte ccValue)
    {
        // Convert 0-127 MIDI CC range to [MinValue..MaxValue]
        float normalized = ccValue / 127.0f;
        Value = MinValue + (normalized * (MaxValue - MinValue));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}