using System;
using System.IO;
using System.Web.Hosting; // <-- Fontos: Ezt a névteret használjuk

namespace Core.Helpers
{
    public static class DebugHelper
    {
        private static readonly object _lock = new object();
        private static bool _initialized = false;
        private static string _logPath;

        // A metódusnak már nincs szüksége a 'server' paraméterre
        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    // A HostingEnvironment.MapPath statikus, bárhonnan hívható
                    _logPath = HostingEnvironment.MapPath("~/App_Data/debug_log.txt");

                    // Ellenőrizzük, hogy az útvonal nem null-e (pl. ha nem webes környezetben fut)
                    if (_logPath == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Error initializing debug logger: Could not map path. Not running in a web context?");
                        return;
                    }

                    string dir = Path.GetDirectoryName(_logPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    using (var writer = new StreamWriter(_logPath, false))
                    {
                        writer.WriteLine($"Debug log initialized at {DateTime.Now}");
                    }

                    _initialized = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error initializing debug logger: {ex.Message}");
                }
            }
        }

        public static void Log(string message)
        {
            // Ha a naplózás valamiért nem inicializálódott, próbáljuk meg most.
            if (!_initialized)
            {
                Initialize();
            }

            // Ha még mindig nem sikerült (pl. _logPath null), akkor csak a Debug ablakba írunk.
            if (string.IsNullOrEmpty(_logPath))
            {
                System.Diagnostics.Debug.WriteLine($"[LOG-FALLBACK] {message}");
                return;
            }

            try
            {
                lock (_lock)
                {
                    using (var writer = new StreamWriter(_logPath, true))
                    {
                        writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
                    }
                }
                System.Diagnostics.Debug.WriteLine(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to debug log: {ex.Message}");
            }
        }
    }
}
