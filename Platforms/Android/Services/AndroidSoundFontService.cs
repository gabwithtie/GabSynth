using Android.Content.Res;
using GabSynth.Interfaces;
using GabSynth.Models;
using GabSynth.Services;

namespace GabSynth.Platforms.Android.Services;

public class AndroidSoundFontService : ISoundFontService
{
    public async Task<List<SoundFontGroup>> SyncAndGetSoundFontsAsync()
    {
        string targetBaseDir = Path.Combine(FileSystem.AppDataDirectory, "SoundFonts");
        if (!Directory.Exists(targetBaseDir))
        {
            Directory.CreateDirectory(targetBaseDir);
        }

        // 1. Extract bundled assets from APK assets if not extracted yet
        var context = global::Android.App.Application.Context;
        AssetManager? assets = context.Assets;

        if (assets != null)
        {
            await Task.Run(() => CopyAssetsRecursively(assets, "SoundFonts", targetBaseDir));
        }

        // 2. Scan Local AppData Directory for SoundFont files & Category Subfolders
        var groups = new List<SoundFontGroup>();
        var rootDir = new DirectoryInfo(targetBaseDir);

        if (rootDir.Exists)
        {
            // Scan subdirectories (e.g. Pianos, Synths)
            foreach (var subDir in rootDir.GetDirectories())
            {
                var files = subDir.GetFiles("*.sf2", SearchOption.AllDirectories)
                    .Select(f => new SoundFontItem
                    {
                        Name = Path.GetFileNameWithoutExtension(f.Name),
                        FullPath = f.FullName
                    }).ToList();

                if (files.Count > 0)
                {
                    groups.Add(new SoundFontGroup($"📁 {subDir.Name}", files));
                }
            }

            // Scan root directory for uncategorized .sf2 files
            var rootFiles = rootDir.GetFiles("*.sf2", SearchOption.TopDirectoryOnly)
                .Select(f => new SoundFontItem
                {
                    Name = Path.GetFileNameWithoutExtension(f.Name),
                    FullPath = f.FullName
                }).ToList();

            if (rootFiles.Count > 0)
            {
                groups.Add(new SoundFontGroup("📁 General", rootFiles));
            }
        }

        return groups;
    }

    private void CopyAssetsRecursively(AssetManager assets, string assetPath, string localPath)
    {
        try
        {
            string[]? items = assets.List(assetPath);
            if (items == null || items.Length == 0) return;

            if (!Directory.Exists(localPath))
            {
                Directory.CreateDirectory(localPath);
            }

            foreach (string item in items)
            {
                string subAssetPath = string.IsNullOrEmpty(assetPath) ? item : $"{assetPath}/{item}";
                string subLocalPath = Path.Combine(localPath, item);

                if (item.EndsWith(".sf2", StringComparison.OrdinalIgnoreCase))
                {
                    // Copy file if it doesn't already exist
                    if (!File.Exists(subLocalPath))
                    {
                        using var inputStream = assets.Open(subAssetPath);
                        using var outputStream = File.Create(subLocalPath);
                        inputStream.CopyTo(outputStream);
                        AppLogger.Log($"[SoundFontService] Extracted: {item}");
                    }
                }
                else if (!item.Contains(".")) // Subfolder
                {
                    CopyAssetsRecursively(assets, subAssetPath, subLocalPath);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"[SoundFontService] Asset sync warning at {assetPath}", ex);
        }
    }
}