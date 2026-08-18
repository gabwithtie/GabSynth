using System.ComponentModel;
using System.Runtime.CompilerServices;
using GabSynth.Effects;
using MeltySynth;

namespace GabSynth.Models;

public class MixerChannel : INotifyPropertyChanged
{
    public int ChannelId { get; }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    private float _volume = 0.8f;
    public float Volume
    {
        get => _volume;
        set { _volume = Math.Clamp(value, 0f, 1f); OnPropertyChanged(); }
    }

    public Synthesizer? Synth { get; set; }
    public string CurrentSoundFontPath { get; set; } = string.Empty;
    public string CurrentInstrumentName { get; set; } = "None Selected";

    // Local channel effects chain
    public EffectsChain Effects { get; } = new();

    public MixerChannel(int id)
    {
        ChannelId = id;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        });
    }
}