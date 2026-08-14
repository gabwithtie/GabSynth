using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GabSynth.Audio;
using GabSynth.Interfaces;
using GabSynth.Models;

namespace GabSynth.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IAudioEngine _audioEngine;
    private readonly IMidiService _midiService;
    private readonly ISoundFontService _soundFontService;
    private readonly MeltyAudioProcessor? _meltyProcessor;

    private SoundFontItem? _selectedSoundFont;
    private string _statusText = "Initializing...";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SoundFontGroup> SoundFontGroups { get; } = new();

    public SoundFontItem? SelectedSoundFont
    {
        get => _selectedSoundFont;
        set
        {
            if (_selectedSoundFont != value && value != null)
            {
                _selectedSoundFont = value;
                OnPropertyChanged();

                // Swap active SoundFont in MeltySynth
                _meltyProcessor?.LoadSoundFont(value.FullPath);
                StatusText = $"Loaded Instrument: {value.Name}";
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public MainViewModel(
        IAudioEngine audioEngine,
        IMidiService midiService,
        IAudioProcessor audioProcessor,
        ISoundFontService soundFontService)
    {
        _audioEngine = audioEngine;
        _midiService = midiService;
        _soundFontService = soundFontService;
        _meltyProcessor = audioProcessor as MeltyAudioProcessor;

        _midiService.MidiMessageReceived += (sender, e) =>
        {
            _audioEngine.HandleMidiMessage(e.Command, e.Note, e.Velocity);
        };

        _audioEngine.Start();
        _midiService.Initialize();

        // Scan SoundFonts on startup
        Task.Run(LoadSoundFontsAsync);
    }

    private async Task LoadSoundFontsAsync()
    {
        var groups = await _soundFontService.SyncAndGetSoundFontsAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SoundFontGroups.Clear();
            foreach (var group in groups)
            {
                SoundFontGroups.Add(group);
            }

            // Automatically select the first found instrument
            if (SoundFontGroups.Count > 0 && SoundFontGroups[0].Count > 0)
            {
                SelectedSoundFont = SoundFontGroups[0][0];
            }
            else
            {
                StatusText = "No SoundFonts (.sf2) found in Resources/Raw/SoundFonts/";
            }
        });
    }

    public void TriggerNoteOn(byte note) => _audioEngine.HandleMidiMessage(0x90, note, 127);
    public void TriggerNoteOff(byte note) => _audioEngine.HandleMidiMessage(0x80, note, 0);

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        MainThread.BeginInvokeOnMainThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
    }
}