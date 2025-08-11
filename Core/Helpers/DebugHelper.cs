using System;
using System.IO;
using System.Web;

namespace Core.Helpers
{
    public static class DebugHelper
    {
        private static readonly object _lock = new object();
        private static bool _initialized = false;
        private static string _logPath;

        public static void Initialize(HttpServerUtilityBase server)
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    _logPath = server.MapPath("~/App_Data/debug_log.txt");

                    // Ensure directory exists
                    string dir = Path.GetDirectoryName(_logPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // Create or clear the log file
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
            try
            {
                if (!_initialized || string.IsNullOrEmpty(_logPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Debug log not initialized. Message: {message}");
                    return;
                }

                lock (_lock)
                {
                    using (var writer = new StreamWriter(_logPath, true))
                    {
                        writer.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} - {message}");
                    }
                }

                // Also output to debug window
                System.Diagnostics.Debug.WriteLine(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to debug log: {ex.Message}");
            }
        }
    }
}