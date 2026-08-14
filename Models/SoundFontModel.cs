namespace GabSynth.Models;

public class SoundFontItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
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