using System;
using System.IO;

namespace VKMusicController.Services
{
    public class LoggerService
    {
        private readonly object _sync = new();
        private string _logFilePath = string.Empty;

        public string LogFilePath => _logFilePath;

        public void Initialize()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "VKMusicController", "logs");
            Directory.CreateDirectory(folder);
            _logFilePath = Path.Combine(folder, "app.log");
            LogInfo("Logger initialized.");
        }

        public void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public void LogError(string message, Exception? exception = null)
        {
            var details = exception == null ? string.Empty : $" | {exception}";
            Log("ERROR", message + details);
        }

        private void Log(string level, string message)
        {
            lock (_sync)
            {
                if (string.IsNullOrEmpty(_logFilePath))
                    return;

                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level}: {message}{Environment.NewLine}";
                try
                {
                    File.AppendAllText(_logFilePath, text);
                }
                catch
                {
                    // ignore logger exceptions to avoid cascading failures
                }
            }
        }
    }
}
