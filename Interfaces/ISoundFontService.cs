using GabSynth.Models;

namespace GabSynth.Interfaces;

public interface ISoundFontService
{
    Task<List<SoundFontGroup>> SyncAndGetSoundFontsAsync();
    Task<bool> ImportSoundFontAsync(FileResult file, string categorySubFolder = "");
    Task<bool> DeleteSoundFontAsync(string fullPath);
}