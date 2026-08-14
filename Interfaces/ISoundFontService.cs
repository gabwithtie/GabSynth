using GabSynth.Models;

namespace GabSynth.Interfaces;

public interface ISoundFontService
{
    Task<List<SoundFontGroup>> SyncAndGetSoundFontsAsync();
}