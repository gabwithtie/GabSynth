using System;
using System.Collections.Generic;
using System.Text;

namespace GabSynth.Services
{
    public static class AppLogger
    {
        public static event Action<string>? OnLogUpdated;
        private static readonly object _lock = new();

        public static string CurrentLog { get; private set; } = "";

        public static void Log(string message, Exception? ex = null)
        {
            lock (_lock)
            {
                string time = DateTime.Now.ToString("HH:mm:ss.fff");
                string logEntry = $"[{time}] {message}";

                if (ex != null)
                {
                    logEntry += $"\n🔥 EXCEPTION: {ex.GetType().Name}\nMessage: {ex.Message}\nStack:\n{ex.StackTrace}\n";
                }

                CurrentLog = $"{logEntry}\n-------------------\n{CurrentLog}";

                try
                {
                    Preferences.Set("SavedCrashLog", CurrentLog);
                }
                catch { /* Ignore storage write races */ }

                // Safe dispatch to UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnLogUpdated?.Invoke(CurrentLog);
                });
            }
        }

        public static void LoadSavedLog()
        {
            CurrentLog = Preferences.Get("SavedCrashLog", "No crash logs recorded yet.");
            OnLogUpdated?.Invoke(CurrentLog);
        }

        public static void Clear()
        {
            lock (_lock)
            {
                CurrentLog = "";
                Preferences.Remove("SavedCrashLog");
                OnLogUpdated?.Invoke("");
            }
        }
    }
}
