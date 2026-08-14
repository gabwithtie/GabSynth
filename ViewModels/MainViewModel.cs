using GabSynth.Interfaces;

namespace GabSynth.ViewModels;

public class MainViewModel
{
    private readonly IAudioEngine _audioEngine;
    private readonly IMidiService _midiService;

    public MainViewModel(IAudioEngine audioEngine, IMidiService midiService)
    {
        _audioEngine = audioEngine;
        _midiService = midiService;

        // Wire up MIDI input to Audio Engine
        _midiService.MidiMessageReceived += (sender, e) =>
        {
            _audioEngine.HandleMidiMessage(e.Command, e.Note, e.Velocity);
        };

        // Start services
        _audioEngine.Start();
        _midiService.Initialize();
    }

    public void OnCutoffSliderChanged(float newValue)
    {
        _audioEngine.UpdateFilterCutoff(newValue);
    }
}