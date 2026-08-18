using GabSynth.Effects;
using GabSynth.Models;
using GabSynth.ViewModels;

namespace GabSynth.Views;

public partial class ChannelEditorPage : ContentPage
{
    private readonly MixerChannel _channel;
    private readonly MainViewModel _viewModel;

    public ChannelEditorPage(MixerChannel channel, MainViewModel viewModel)
    {
        InitializeComponent();
        _channel = channel;
        _viewModel = viewModel;

        Title = $"Channel {channel.ChannelId} Settings";
        ChannelTitleLabel.Text = $"CHANNEL {channel.ChannelId} EDITOR";

        SoundFontCollectionView.ItemsSource = _viewModel.SoundFontGroups;

        // Highlight matching instrument on initial page load
        SyncSoundFontSelection();
        BuildChannelEffectsUi();
    }

    private void OnSoundFontItemTapped(object sender, EventArgs e)
    {
        if (sender is Element element && element.BindingContext is SoundFontItem item)
        {
            // Assign instrument to channel
            _viewModel.SelectedChannelIndex = _channel.ChannelId - 1;
            _viewModel.SelectedSoundFont = item;
            _channel.CurrentInstrumentName = item.Name;

            // Clear all previous highlights across ALL folders and highlight only the tapped item
            SyncSoundFontSelection(item);
        }
    }

    private void SyncSoundFontSelection(SoundFontItem? selectedItem = null)
    {
        foreach (var group in _viewModel.SoundFontGroups)
        {
            foreach (var item in group)
            {
                if (selectedItem != null)
                {
                    item.IsSelected = (item == selectedItem);
                }
                else
                {
                    // Restore highlight on page load matching current channel instrument
                    item.IsSelected = (!string.IsNullOrEmpty(_channel.CurrentInstrumentName) && item.Name == _channel.CurrentInstrumentName) ||
                                      (!string.IsNullOrEmpty(_channel.CurrentSoundFontPath) && item.FullPath == _channel.CurrentSoundFontPath);
                }
            }
        }
    }

    private async void OnAddChannelEffectClicked(object sender, EventArgs e)
    {
        // Include both MIDI and Audio Effects
        string[] available = { "Velocity Dynamics", "3-Band Equalizer", "Low-Pass Filter", "Cancel" };
        string action = await DisplayActionSheet("Add Channel Effect", "Cancel", null, available);

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        var newEffect = EffectsChain.CreateEffectByName(action);

        if (newEffect is IMidiEffect midiEffect)
        {
            _channel.Effects.MidiEffects.Add(midiEffect);
        }
        else if (newEffect is IAudioEffect audioEffect)
        {
            audioEffect.Initialize(48000);
            _channel.Effects.AudioEffects.Add(audioEffect);
        }

        BuildChannelEffectsUi();
    }

    private void BuildChannelEffectsUi()
    {
        ChannelEffectsContainer.Children.Clear();

        // Render MIDI Effects
        foreach (var effect in _channel.Effects.MidiEffects)
        {
            ChannelEffectsContainer.Children.Add(CreateEffectCard(effect));
        }

        // Render Audio Effects
        foreach (var effect in _channel.Effects.AudioEffects)
        {
            ChannelEffectsContainer.Children.Add(CreateEffectCard(effect));
        }
    }

    private Frame CreateEffectCard(object effectInstance)
    {
        string name = string.Empty;
        bool isEnabled = false;
        IReadOnlyList<EffectParameter> parameters = Array.Empty<EffectParameter>();

        if (effectInstance is IMidiEffect midiEffect)
        {
            name = midiEffect.Name;
            isEnabled = midiEffect.IsEnabled;
            parameters = midiEffect.Parameters;
        }
        else if (effectInstance is IAudioEffect audioEffect)
        {
            name = audioEffect.Name;
            isEnabled = audioEffect.IsEnabled;
            parameters = audioEffect.Parameters;
        }

        var card = new Frame
        {
            BackgroundColor = Color.FromArgb("#1E1E28"),
            BorderColor = Color.FromArgb("#2E2E3E"),
            CornerRadius = 12,
            Padding = 14
        };

        var stack = new VerticalStackLayout { Spacing = 10 };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };

        var title = new Label
        {
            Text = name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };

        var toggle = new Switch { IsToggled = isEnabled, OnColor = Color.FromArgb("#FF0055") };
        toggle.Toggled += (s, e) =>
        {
            if (effectInstance is IMidiEffect me) me.IsEnabled = e.Value;
            if (effectInstance is IAudioEffect ae) ae.IsEnabled = e.Value;
        };

        var deleteBtn = new Button
        {
            Text = "🗑️",
            FontSize = 12,
            HeightRequest = 32,
            WidthRequest = 36,
            BackgroundColor = Color.FromArgb("#331111"),
            TextColor = Color.FromArgb("#FF5555"),
            Padding = 0,
            CornerRadius = 6
        };

        deleteBtn.Clicked += (s, e) =>
        {
            if (effectInstance is IMidiEffect me) _channel.Effects.MidiEffects.Remove(me);
            if (effectInstance is IAudioEffect ae) _channel.Effects.AudioEffects.Remove(ae);
            BuildChannelEffectsUi();
        };

        header.Add(title, 0, 0);
        header.Add(toggle, 1, 0);
        header.Add(deleteBtn, 2, 0);
        stack.Children.Add(header);

        foreach (var param in parameters)
        {
            var paramGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(90) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };

            var nameLbl = new Label
            {
                Text = param.Name,
                TextColor = Color.FromArgb("#AAAA00"),
                FontSize = 11,
                VerticalOptions = LayoutOptions.Center
            };

            var slider = new Slider
            {
                Minimum = param.MinValue,
                Maximum = param.MaxValue,
                Value = param.Value,
                MinimumTrackColor = Color.FromArgb("#00E676")
            };

            slider.ValueChanged += (s, e) => param.Value = (float)e.NewValue;

            paramGrid.Add(nameLbl, 0, 0);
            paramGrid.Add(slider, 1, 0);
            stack.Children.Add(paramGrid);
        }

        card.Content = stack;
        return card;
    }
}