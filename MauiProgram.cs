using GabSynth;
using GabSynth.Interfaces;
using GabSynth.ViewModels;

namespace GabSynth;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Register Platform-Specific Audio/MIDI Services
#if ANDROID
        builder.Services.AddSingleton<IMidiService, Platforms.Android.Services.AndroidMidiService>();
        builder.Services.AddSingleton<IAudioEngine, Platforms.Android.Services.AndroidAudioEngine>();
#endif

        // Register Cross-Platform View Models & Views
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}