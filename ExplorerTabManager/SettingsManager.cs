using System;
using System.IO;
using Newtonsoft.Json;
using ExplorerTabManager.Models;

namespace ExplorerTabManager
{
    public class SettingsManager
    {
        private readonly string settingsPath;
        public AppSettings Settings { get; private set; }

        public SettingsManager()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ExplorerTabManager"
            );
            Directory.CreateDirectory(appDataPath);
            settingsPath = Path.Combine(appDataPath, "settings.json");
            
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                Settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                Settings = new AppSettings();
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(settingsPath, json);
        }
    }
}
