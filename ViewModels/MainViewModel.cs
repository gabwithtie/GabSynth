using GabSynth.Audio;
using GabSynth.Config;
using GabSynth.Interfaces;
using GabSynth.Models;
using GabSynth.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GabSynth.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly MidiControlRouter _midiRouter;
    private readonly IAudioEngine _audioEngine;
    private readonly IMidiService _midiService;
    private readonly ISoundFontService _soundFontService;
    private readonly MeltyAudioProcessor? _meltyProcessor;

    private SoundFontItem? _selectedSoundFont;
    private int _selectedChannelIndex = 0;
    private string _statusText = "Initializing...";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SoundFontGroup> SoundFontGroups { get; } = new();
    public ObservableCollection<MixerChannel> Channels { get; } = new();

    /// <summary>
    /// Target channel (0 to MaxMixerChannels - 1) to receive the assigned SoundFont instrument.
    /// </summary>
    public int SelectedChannelIndex
    {
        get => _selectedChannelIndex;
        set
        {
            if (_selectedChannelIndex != value && value >= 0 && value < BuildSettings.MaxMixerChannels)
            {
                _selectedChannelIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedChannelName));
            }
        }
    }

    public string SelectedChannelName => $"Channel {SelectedChannelIndex + 1}";

    public SoundFontItem? SelectedSoundFont
    {
        get => _selectedSoundFont;
        set
        {
            if (_selectedSoundFont != value && value != null)
            {
                _selectedSoundFont = value;
                OnPropertyChanged();

                // Route SoundFont load to the specifically targeted mixer channel
                if (_meltyProcessor != null)
                {
                    _meltyProcessor.LoadSoundFontToChannel(SelectedChannelIndex, value.FullPath);
                    StatusText = $"CH {SelectedChannelIndex + 1} Loaded: {value.Name}";
                }
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

        // Populate mixer channel references for binding in UI
        if (_meltyProcessor != null)
        {
            foreach (var channel in _meltyProcessor.Channels)
            {
                Channels.Add(channel);
            }

            _midiRouter = new MidiControlRouter(
                _meltyProcessor.Channels,
                _meltyProcessor.MasterEffects,
                _meltyProcessor
            );
        }

        _midiService.MidiMessageReceived += (sender, e) =>
        {
            // Route raw MIDI into the synth-agnostic controller
            _midiRouter.ProcessRawMidi(e.Command, e.Note, e.Velocity);
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

            // Automatically load first instrument into Channel 1
            if (SoundFontGroups.Count > 0 && SoundFontGroups[0].Count > 0)
            {
                SelectedSoundFont = SoundFontGroups[0][0];
            }
            else
            {
                StatusText = "No SoundFonts (.sf2) found in local storage.";
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