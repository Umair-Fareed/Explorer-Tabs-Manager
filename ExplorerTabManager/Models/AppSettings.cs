using System;

namespace ExplorerTabManager.Models
{
    public class AppSettings
    {
        public string DefaultTabLocation { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public int AutoSaveIntervalSeconds { get; set; } = 5;
        public bool AutoSaveEnabled { get; set; } = true;
        public bool AutoRestoreOnStartup { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowNotifications { get; set; } = false;
        public int RestoreDelaySeconds { get; set; } = 1;
    }
}
