using GabSynth.Audio;
using GabSynth.Effects;
using GabSynth.Interfaces;

namespace GabSynth.Views;

public partial class EffectsPage : ContentPage
{
    private readonly MeltyAudioProcessor? _processor;
    private int _activeSlot = 1; // Tracks currently active slot

    public EffectsPage(IAudioProcessor audioProcessor)
    {
        InitializeComponent();

        if (audioProcessor is MeltyAudioProcessor melty)
        {
            _processor = melty;
            BuildPresetBar();
            BuildEffectsUi(melty.EffectsChain);
        }
    }

    private void BuildPresetBar()
    {
        if (_processor == null) return;

        PresetGrid.Children.Clear();

        for (int i = 1; i <= 8; i++)
        {
            int slot = i;
            int ccNum = 35 + slot; // CC 36 to 43
            bool hasData = _processor.Presets.HasPreset(slot);
            bool isActive = (slot == _activeSlot);

            var btn = new Button
            {
                Text = $"P{slot}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                Padding = 0,
                CornerRadius = 6,
                BorderWidth = isActive ? 2 : 0,
                BorderColor = isActive ? Color.FromArgb("#00FF66") : Colors.Transparent
            };

            // Set background color based on saved state & active selection
            UpdateButtonAppearance(btn, hasData, isActive);

            // SINGLE TAP ONLY: Load preset & update active state
            btn.Clicked += (s, e) =>
            {
                _activeSlot = slot;
                _processor.LoadPreset(slot);
                PresetStatusLabel.Text = $"Active Slot: #{slot} (CC #{ccNum})";
                
                RefreshPresetBarStyles();
                RebindUiValues();
            };

            Grid.SetColumn(btn, i - 1);
            PresetGrid.Children.Add(btn);
        }
    }

    private void RefreshPresetBarStyles()
    {
        if (_processor == null) return;

        for (int i = 0; i < PresetGrid.Children.Count; i++)
        {
            if (PresetGrid.Children[i] is Button btn)
            {
                int slot = i + 1;
                bool hasData = _processor.Presets.HasPreset(slot);
                bool isActive = (slot == _activeSlot);

                btn.BorderWidth = isActive ? 2 : 0;
                btn.BorderColor = isActive ? Color.FromArgb("#00FF66") : Colors.Transparent;
                
                UpdateButtonAppearance(btn, hasData, isActive);
            }
        }
    }

    private void UpdateButtonAppearance(Button btn, bool hasData, bool isActive)
    {
        if (isActive)
        {
            btn.BackgroundColor = Color.FromArgb("#FF0055"); // Bright Red for Active
            btn.TextColor = Colors.White;
        }
        else if (hasData)
        {
            btn.BackgroundColor = Color.FromArgb("#331122"); // Dimmed Maroon for Saved Slots
            btn.TextColor = Color.FromArgb("#FF88AA");
        }
        else
        {
            btn.BackgroundColor = Color.FromArgb("#252533"); // Dark Gray for Empty Slots
            btn.TextColor = Color.FromArgb("#8888A0");
        }
    }

    private async void OnSavePresetClicked(object sender, EventArgs e)
    {
        if (_processor == null) return;

        // Save current state into the currently active slot
        _processor.SavePreset(_activeSlot);

        // Flash status label for visual confirmation
        PresetStatusLabel.Text = $"💾 Saved successfully to Slot #{_activeSlot}!";
        PresetStatusLabel.TextColor = Color.FromArgb("#00FF66");

        // Flash Save button briefly to confirm action
        SavePresetBtn.BackgroundColor = Color.FromArgb("#FF0055");
        SavePresetBtn.TextColor = Colors.White;

        RefreshPresetBarStyles();

        await Task.Delay(1000);

        SavePresetBtn.BackgroundColor = Color.FromArgb("#332233");
        SavePresetBtn.TextColor = Color.FromArgb("#FF0055");
        PresetStatusLabel.TextColor = Color.FromArgb("#8888A0");
    }

    private void RebindUiValues()
    {
        if (_processor != null)
        {
            BuildEffectsUi(_processor.EffectsChain);
        }
    }

    private void BuildEffectsUi(AudioEffectsChain chain)
    {
        EffectsContainer.Children.Clear();

        foreach (var effect in chain.Effects)
        {
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
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            var title = new Label
            {
                Text = effect.Name,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                VerticalOptions = LayoutOptions.Center
            };

            var toggle = new Switch
            {
                IsToggled = effect.IsEnabled,
                OnColor = Color.FromArgb("#FF0055")
            };
            toggle.Toggled += (s, e) => effect.IsEnabled = e.Value;

            header.Add(title, 0, 0);
            header.Add(toggle, 1, 0);
            stack.Children.Add(header);

            foreach (var param in effect.Parameters)
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
                param.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(EffectParameter.Value))
                    {
                        MainThread.BeginInvokeOnMainThread(() => slider.Value = param.Value);
                    }
                };

                var learnBtn = new Button
                {
                    Text = param.MappingStatusText,
                    FontSize = 9,
                    HeightRequest = 30,
                    BackgroundColor = Color.FromArgb("#2A2A38"),
                    TextColor = Colors.White,
                    Padding = new Thickness(8, 0)
                };

                learnBtn.Clicked += (s, e) =>
                {
                    param.IsLearning = !param.IsLearning;
                    learnBtn.Text = param.MappingStatusText;
                    learnBtn.BackgroundColor = param.IsLearning ? Color.FromArgb("#FF0055") : Color.FromArgb("#2A2A38");
                };

                param.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(EffectParameter.MappingStatusText))
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            learnBtn.Text = param.MappingStatusText;
                            if (!param.IsLearning)
                                learnBtn.BackgroundColor = Color.FromArgb("#2A2A38");
                        });
                    }
                };

                paramGrid.Add(nameLbl, 0, 0);
                paramGrid.Add(slider, 1, 0);
                paramGrid.Add(learnBtn, 2, 0);

                stack.Children.Add(paramGrid);
            }

            card.Content = stack;
            EffectsContainer.Children.Add(card);
        }
    }
}