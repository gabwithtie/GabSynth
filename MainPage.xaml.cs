using GabSynth.Interfaces;
using GabSynth.Services;
using GabSynth.ViewModels;

namespace GabSynth;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Listen for live updates
        AppLogger.OnLogUpdated += (logText) =>
        {
            LogEditor.Text = logText;
        };

        // Load any saved crash logs from local storage on app startup
        AppLogger.LoadSavedLog();
    }

    private void OnClearLogClicked(object sender, EventArgs e)
    {
        AppLogger.Clear();
    }

    private async void OnOpenEffectsClicked(object sender, EventArgs e)
    {
        var audioProcessor = Handler?.MauiContext?.Services.GetService<IAudioProcessor>();
        if (audioProcessor != null)
        {
            await Navigation.PushAsync(new GabSynth.Views.EffectsPage(audioProcessor));
        }
    }
}