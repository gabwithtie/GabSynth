using GabSynth.Audio;
using GabSynth.Interfaces;
using GabSynth.Models;
using GabSynth.Services;
using GabSynth.ViewModels;
using GabSynth.Views;

namespace GabSynth;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly MeltyAudioProcessor? _processor;

    public MainPage(MainViewModel viewModel, IAudioProcessor audioProcessor)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _processor = audioProcessor as MeltyAudioProcessor;

        BuildMixerUi();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BuildMixerUi(); // Refresh channel labels/states when returning from ChannelEditorPage
    }

    private void BuildMixerUi()
    {
        if (_processor == null) return;
        MixerContainer.Children.Clear();

        foreach (var channel in _processor.Channels)
        {
            var card = new Frame
            {
                BackgroundColor = Color.FromArgb("#1A1A22"),
                BorderColor = Color.FromArgb("#2E2E3E"),
                CornerRadius = 10,
                Padding = 12
            };

            var grid = new Grid
            {
                ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(60) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(100) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
                ColumnSpacing = 10
            };

            var chLabel = new Label
            {
                Text = $"CH {channel.ChannelId}",
                TextColor = Color.FromArgb("#00FF66"),
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };

            var instLabel = new Label
            {
                Text = channel.CurrentInstrumentName,
                TextColor = Colors.White,
                FontSize = 12,
                LineBreakMode = LineBreakMode.TailTruncation,
                VerticalOptions = LayoutOptions.Center
            };

            var toggle = new Switch
            {
                IsToggled = channel.IsEnabled,
                OnColor = Color.FromArgb("#00FF66"),
                VerticalOptions = LayoutOptions.Center
            };
            toggle.Toggled += (s, e) =>
            {
                if (channel.IsEnabled != e.Value) channel.IsEnabled = e.Value;
            };

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                Value = channel.Volume,
                MinimumTrackColor = Color.FromArgb("#FF0055"),
                VerticalOptions = LayoutOptions.Center
            };
            slider.ValueChanged += (s, e) =>
            {
                if (Math.Abs(channel.Volume - (float)e.NewValue) > 0.001f)
                    channel.Volume = (float)e.NewValue;
            };

            // MIDI CC Live UI Binding
            channel.PropertyChanged += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (e.PropertyName == nameof(MixerChannel.IsEnabled))
                    {
                        if (toggle.IsToggled != channel.IsEnabled)
                            toggle.IsToggled = channel.IsEnabled;
                    }
                    else if (e.PropertyName == nameof(MixerChannel.Volume))
                    {
                        if (Math.Abs(slider.Value - channel.Volume) > 0.001)
                            slider.Value = channel.Volume;
                    }
                    else if (e.PropertyName == nameof(MixerChannel.CurrentInstrumentName))
                    {
                        instLabel.Text = channel.CurrentInstrumentName;
                    }
                });
            };

            var editBtn = new Button
            {
                Text = "⚙️ Edit",
                FontSize = 11,
                HeightRequest = 36,
                BackgroundColor = Color.FromArgb("#FF0055"),
                TextColor = Colors.White,
                CornerRadius = 6
            };

            editBtn.Clicked += async (s, e) =>
            {
                await Navigation.PushAsync(new ChannelEditorPage(channel, _viewModel));
            };

            grid.Add(chLabel, 0, 0);
            grid.Add(instLabel, 1, 0);
            grid.Add(toggle, 2, 0);
            grid.Add(slider, 3, 0);
            grid.Add(editBtn, 4, 0);

            card.Content = grid;
            MixerContainer.Children.Add(card);
        }
    }

    private void OnAddMasterEffectClicked(object sender, EventArgs e) { /* Add Master Effect Flow */ }

    private async void OnOpenInstrumentDatabaseClicked(object sender, EventArgs e)
    {
        // Resolve InstrumentDatabasePage with its DI dependencies
        var databasePage = Handler?.MauiContext?.Services.GetService<InstrumentDatabasePage>();

        if (databasePage != null)
        {
            await Navigation.PushAsync(databasePage);
        }
    }

    private async void OnLoadPresetClicked(object sender, EventArgs e)
    {
        if (_processor == null) return;

        var presetManager = new PresetManager();
        var availableSlots = new List<string>();

        // Check which slots have saved files
        for (int i = 1; i <= 4; i++)
        {
            if (presetManager.HasPreset(i))
                availableSlots.Add($"Slot {i}");
        }

        if (availableSlots.Count == 0)
        {
            await DisplayAlert("No Presets Found", "There are no saved preset slots available.", "OK");
            return;
        }

        availableSlots.Add("Cancel");

        string action = await DisplayActionSheet("Load Mixer Preset From", "Cancel", null, availableSlots.ToArray());

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        int slotIndex = int.Parse(action.Replace("Slot ", ""));

        var preset = presetManager.LoadPresetData(slotIndex);
        if (preset != null)
        {
            presetManager.ApplyPreset(
                preset,
                _processor.Channels,
                _processor.MasterEffects,
                (chIndex, path) => _processor.LoadSoundFontToChannel(chIndex, path)
            );

            // Rebuild UI components to mirror loaded channels & instrument names
            BuildMixerUi();
            _viewModel.StatusText = $"Loaded Global Snapshot from Slot #{slotIndex}";
        }
    }

    private async void OnSavePresetClicked(object sender, EventArgs e)
    {
        if (_processor == null) return;

        string[] slots = { "Slot 1", "Slot 2", "Slot 3", "Slot 4", "Cancel" };
        string action = await DisplayActionSheet("Save Mixer Preset To", "Cancel", null, slots);

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        int slotIndex = action switch
        {
            "Slot 1" => 1,
            "Slot 2" => 2,
            "Slot 3" => 3,
            "Slot 4" => 4,
            _ => 1
        };

        var presetManager = new PresetManager();
        presetManager.SaveFullSnapshot(slotIndex, _processor.Channels, _processor.MasterEffects);

        _viewModel.StatusText = $"Saved Global Snapshot to Slot #{slotIndex}";
    }
}