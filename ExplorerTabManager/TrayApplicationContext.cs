using System;
using System.Drawing;
using System.Windows.Forms;

namespace ExplorerTabManager
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ExplorerMonitor explorerMonitor;
        private SettingsManager settingsManager;

        public TrayApplicationContext()
        {
            settingsManager = new SettingsManager();
            explorerMonitor = new ExplorerMonitor(settingsManager);

            InitializeTrayIcon();

            // NOTE: Do NOT call RestoreTabs() here on startup.
            // Auto-restore is triggered by ExplorerMonitor.CheckExplorerWindows()
            // which detects when Explorer goes from 0 windows to 1+ windows.
            // Calling it here would incorrectly restore tabs every time the app launches.
        }

        private void InitializeTrayIcon()
        {
            Icon appIcon = null;
            try
            {
                // First try: extract icon embedded in the .exe (set via ApplicationIcon in .csproj)
                appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            if (appIcon == null)
            {
                try
                {
                    string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", "Explorer Tabs Manager.ico");
                    if (System.IO.File.Exists(iconPath))
                        appIcon = new Icon(iconPath);
                }
                catch { }
            }

            trayIcon = new NotifyIcon()
            {
                Icon = appIcon ?? SystemIcons.Application,
                ContextMenuStrip = CreateContextMenu(),
                Visible = true,
                Text = "Explorer Tabs Manager"
            };

            trayIcon.DoubleClick += (s, e) => ShowSettings();
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            menu.Items.Add("Save Tabs Now", null, (s, e) => explorerMonitor.SaveCurrentTabs());
            menu.Items.Add("Restore Tabs Now", null, (s, e) => explorerMonitor.RestoreTabs());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Settings", null, (s, e) => ShowSettings());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => Exit());

            return menu;
        }

        private void ShowSettings()
        {
            var settingsForm = new SettingsForm(settingsManager);
            settingsForm.ShowDialog();
        }

        private void Exit()
        {
            explorerMonitor.SaveCurrentTabs();
            trayIcon.Visible = false;
            explorerMonitor.Dispose();
            Application.Exit();
        }
    }
}
