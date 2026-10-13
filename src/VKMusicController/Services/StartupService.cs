using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace VKMusicController.Services
{
    public class StartupService
    {
        private const string RegistryRunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "VKMusicController";
        private readonly LoggerService _logger;

        public StartupService(LoggerService logger)
        {
            _logger = logger;
        }

        public void UpdateStartupRegistration(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: true);
                if (key == null)
                {
                    _logger.LogError("Failed to open registry key for startup registration.");
                    return;
                }

                if (enabled)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, BuildStartupCommand(exePath, Assembly.GetEntryAssembly()?.Location ?? string.Empty));
                        _logger.LogInfo("Startup registration enabled.");
                    }
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                    {
                        key.DeleteValue(AppName, false);
                    }
                    _logger.LogInfo("Startup registration disabled.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update startup registration", ex);
            }
        }

        public static bool IsBackgroundLaunch(IEnumerable<string> args) => args.Any(argument =>
            string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase));

        internal static string BuildStartupCommand(string executablePath, string assemblyPath)
        {
            // Framework-dependent debug builds are launched through dotnet.exe.
            return string.Equals(Path.GetFileNameWithoutExtension(executablePath), "dotnet", StringComparison.OrdinalIgnoreCase)
                ? $"\"{executablePath}\" \"{assemblyPath}\" --startup"
                : $"\"{executablePath}\" --startup";
        }
    }
}
