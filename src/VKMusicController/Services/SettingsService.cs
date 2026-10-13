using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VKMusicController.Models;

namespace VKMusicController.Services
{
    public class SettingsService
    {
        private readonly LoggerService _logger;
        private readonly string _settingsPath;

        public AppSettings CurrentSettings { get; private set; } = new AppSettings();
        public event EventHandler? SettingsSaved;

        public SettingsService(LoggerService logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "VKMusicController");
            Directory.CreateDirectory(folder);
            _settingsPath = Path.Combine(folder, "settings.json");
        }

        public async Task<AppSettings> LoadAsync()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    CurrentSettings = new AppSettings();
                    await SaveAsync();
                    return CurrentSettings;
                }

                var json = await File.ReadAllTextAsync(_settingsPath);
                CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                }) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load settings", ex);
                CurrentSettings = new AppSettings();
            }

            // Older configurations omitted volume actions. Keep existing custom bindings.
            var added = false;
            CurrentSettings.Hotkeys ??= new();
            foreach (var defaults in new AppSettings().Hotkeys)
            {
                if (!CurrentSettings.Hotkeys.Exists(h => h.Action == defaults.Action))
                {
                    // Never silently reuse a combination already assigned by the user.
                    defaults.Enabled = !CurrentSettings.Hotkeys.Exists(h => h.Enabled &&
                        h.Key == defaults.Key && h.Modifiers == defaults.Modifiers);
                    CurrentSettings.Hotkeys.Add(defaults);
                    added = true;
                }
            }
            if (added)
                await SaveAsync();
            return CurrentSettings;
        }

        public async Task SaveAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_settingsPath, json);
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save settings", ex);
            }
        }

        public void Save()
        {
            SaveAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
