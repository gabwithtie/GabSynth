using GabSynth;
using GabSynth.Audio;
using GabSynth.Interfaces;
using GabSynth.Services;
using GabSynth.ViewModels;
using GabSynth.Views;

namespace GabSynth;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // 1. Catch C# AppDomain Unhandled Exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            AppLogger.Log("CRITICAL: Unhandled AppDomain Exception", ex);
        };

        // 2. Catch Async Task Exceptions
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            AppLogger.Log("CRITICAL: Unobserved Task Exception", e.Exception);
        };

#if ANDROID
        // 3. Catch Native Android/JNI Native Exceptions
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
        {
            AppLogger.Log("CRITICAL: Native Android JNI Exception", e.Exception);
            e.Handled = true; // Prevents instant crash so log can render/save
        };
#endif

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Register Platform-Specific Audio/MIDI Services
#if ANDROID
        builder.Services.AddSingleton<IMidiService, GabSynth.Platforms.Android.Services.AndroidMidiService>();
        builder.Services.AddSingleton<IAudioEngine, GabSynth.Platforms.Android.Services.AndroidAudioEngine>();
        builder.Services.AddSingleton<ISoundFontService, GabSynth.Platforms.Android.Services.AndroidSoundFontService>();
#endif

        builder.Services.AddSingleton<IAudioProcessor, GabSynth.Audio.MeltyAudioProcessor>();
        builder.Services.AddSingleton<ViewModels.MainViewModel>();
        builder.Services.AddSingleton<Views.ChannelEditorPage>();
        builder.Services.AddTransient<MainPage>();

        builder.Services.AddTransient<InstrumentDatabasePage>();

        return builder.Build();
    }
}