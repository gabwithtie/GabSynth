using GabSynth.Interfaces;
using GabSynth.Models;
using GabSynth.ViewModels;

namespace GabSynth.Views;

public partial class InstrumentDatabasePage : ContentPage
{
    private readonly ISoundFontService _soundFontService;
    private readonly MainViewModel _viewModel;

    public InstrumentDatabasePage(ISoundFontService soundFontService, MainViewModel viewModel)
    {
        InitializeComponent();
        _soundFontService = soundFontService;
        _viewModel = viewModel;

        LoadSoundFonts();
    }

    private async void LoadSoundFonts()
    {
        SoundFontRefreshView.IsRefreshing = true;

        var groups = await _soundFontService.SyncAndGetSoundFontsAsync();

        // Update both local screen and global View Model
        _viewModel.SoundFontGroups.Clear();
        foreach (var group in groups)
        {
            _viewModel.SoundFontGroups.Add(group);
        }

        SoundFontCollectionView.ItemsSource = null;
        SoundFontCollectionView.ItemsSource = _viewModel.SoundFontGroups;

        SoundFontRefreshView.IsRefreshing = false;
    }

    private void OnRefreshTriggered(object sender, EventArgs e)
    {
        LoadSoundFonts();
    }

    private async void OnImportSoundFontClicked(object sender, EventArgs e)
    {
        try
        {
            // Custom file picker configuration for .sf2 SoundFonts
            var sf2FileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "application/octet-stream", "*/*" } },
                { DevicePlatform.iOS, new[] { "public.data" } },
                { DevicePlatform.WinUI, new[] { ".sf2" } },
                { DevicePlatform.MacCatalyst, new[] { "sf2" } }
            });

            var options = new PickOptions
            {
                PickerTitle = "Select a SoundFont (.sf2) file",
                FileTypes = sf2FileType
            };

            var result = await FilePicker.Default.PickAsync(options);
            if (result == null) return;

            if (!result.FileName.EndsWith(".sf2", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("Invalid File", "Please select a file with a .sf2 extension.", "OK");
                return;
            }

            // Optional Sub-Category Prompt
            string category = await DisplayActionSheet("Assign Category", "Cancel", null, "General", "Pianos", "Synths", "Drums", "Custom");
            if (category == "Cancel" || string.IsNullOrEmpty(category)) return;

            if (category == "Custom")
            {
                category = await DisplayPromptAsync("Custom Category", "Enter category folder name:", "OK", "Cancel", "MyInstruments");
                if (string.IsNullOrWhiteSpace(category)) category = "General";
            }

            string subFolder = category == "General" ? "" : category;

            bool success = await _soundFontService.ImportSoundFontAsync(result, subFolder);
            if (success)
            {
                await DisplayAlert("Success", $"Imported '{result.FileName}' successfully!", "OK");
                LoadSoundFonts();
            }
            else
            {
                await DisplayAlert("Error", "Failed to import SoundFont file.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Import failed: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteSoundFontClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SoundFontItem item)
        {
            bool confirm = await DisplayAlert("Delete Instrument", $"Are you sure you want to delete '{item.Name}'?", "Delete", "Cancel");
            if (!confirm) return;

            bool deleted = await _soundFontService.DeleteSoundFontAsync(item.FullPath);
            if (deleted)
            {
                LoadSoundFonts();
            }
            else
            {
                await DisplayAlert("Error", "Could not delete file.", "OK");
            }
        }
    }
}