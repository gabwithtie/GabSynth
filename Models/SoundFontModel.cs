namespace GabSynth.Models;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public class SoundFontItem : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        MainThread.BeginInvokeOnMainThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
    }
}
// Grouped collection for MAUI Grouped CollectionView UI
public class SoundFontGroup : List<SoundFontItem>
{
    public string Category { get; set; }

    public SoundFontGroup(string category, List<SoundFontItem> items) : base(items)
    {
        Category = category;
    }
}