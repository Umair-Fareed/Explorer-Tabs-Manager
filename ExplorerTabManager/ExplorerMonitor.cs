using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ExplorerTabManager.Models;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace ExplorerTabManager
{
    /// <summary>
    /// Simple logger for debugging and monitoring.
    /// </summary>
    public static class SimpleLogger
    {
        private static readonly object _sync = new object();
        private static readonly string LogPath;

        static SimpleLogger()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExplorerTabManager");
            Directory.CreateDirectory(dir);
            LogPath = Path.Combine(dir, "logs.txt");
        }

        public static void Log(string message)
        {
            try
            {
                lock (_sync)
                {
                    File.AppendAllText(LogPath, $"{DateTime.Now:O} | {message}{Environment.NewLine}");
                }
                Debug.WriteLine(message);
            }
            catch { }
        }
    }

    /// <summary>
    /// Manages saving and loading Explorer tab sessions.
    /// </summary>
    public class ExplorerMonitor : IDisposable
    {
        private readonly SettingsManager _settingsManager;
        private readonly string _sessionFilePath;
        private System.Timers.Timer _autoSaveTimer;
        private System.Timers.Timer _explorerMonitorTimer;
        private int _lastExplorerWindowCount = -1; // -1 = not yet initialized
        private readonly DateTime _startTime = DateTime.Now;

        // Startup grace period: don't auto-restore within this many seconds of launch.
        // This prevents false triggers caused by slow COM initialization at startup.
        private const int StartupGracePeriodSeconds = 8;

        public ExplorerMonitor(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ExplorerTabManager"
            );
            Directory.CreateDirectory(appDataPath);
            _sessionFilePath = Path.Combine(appDataPath, "tabs.json");

            InitializeAutoSave();
            InitializeExplorerMonitoring();
        }

        private void InitializeAutoSave()
        {
            if (_settingsManager.Settings.AutoSaveEnabled)
            {
                _autoSaveTimer = new System.Timers.Timer(_settingsManager.Settings.AutoSaveIntervalSeconds * 1000);
                _autoSaveTimer.Elapsed += (s, e) => SaveCurrentTabs();
                _autoSaveTimer.Start();
            }
        }

        private void InitializeExplorerMonitoring()
        {
            _explorerMonitorTimer = new System.Timers.Timer(2000);
            _explorerMonitorTimer.Elapsed += (s, e) => CheckExplorerWindows();
            _explorerMonitorTimer.Start();
        }

        private void CheckExplorerWindows()
        {
            try
            {
                int currentCount = GetExplorerWindowCount();

                // During startup grace period: just record the current state, never trigger restore.
                // This prevents false triggers from slow COM initialization at launch.
                if ((DateTime.Now - _startTime).TotalSeconds < StartupGracePeriodSeconds)
                {
                    _lastExplorerWindowCount = currentCount;
                    return;
                }

                // First real check after grace period
                if (_lastExplorerWindowCount == -1)
                {
                    _lastExplorerWindowCount = currentCount;
                    return;
                }

                // Explorer was closed and has just been reopened
                if (_lastExplorerWindowCount == 0 && currentCount > 0)
                {
                    SimpleLogger.Log($"Explorer reopened (was {_lastExplorerWindowCount}, now {currentCount})");

                    if (_settingsManager.Settings.AutoRestoreOnStartup)
                    {
                        SimpleLogger.Log("Triggering auto-restore...");
                        Task.Delay(1500).ContinueWith(_ => RestoreTabs());
                    }
                }

                _lastExplorerWindowCount = currentCount;
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Error in CheckExplorerWindows: {ex.Message}");
            }
        }

        private int GetExplorerWindowCount()
        {
            int count = 0;
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return 0;

                dynamic shell = Activator.CreateInstance(shellType);
                try
                {
                    foreach (dynamic window in shell.Windows())
                    {
                        try
                        {
                            string fullName = Path.GetFileName((string)window.FullName ?? "");
                            if (fullName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                                count++;
                        }
                        catch { }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
            catch { }

            return count;
        }

        /// <summary>
        /// Saves all current Explorer tabs to disk.
        /// </summary>
        public void SaveCurrentTabs()
        {
            try
            {
                var tabState = CollectCurrentTabs();
                int tabCount = tabState.Windows.Sum(w => w.TabPaths.Count);

                if (tabCount > 0)
                {
                    SaveSession(tabState);
                    SimpleLogger.Log($"Saved {tabCount} tab(s) across {tabState.Windows.Count} window(s)");
                }
                else
                {
                    SimpleLogger.Log("No tabs to save");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Error saving tabs: {ex.Message}");
            }
        }

        /// <summary>
        /// Collects all current Explorer tabs using Shell.Application COM interface.
        /// Each tab in Windows 11 Explorer appears as a separate entry with the same HWND.
        /// </summary>
        private TabState CollectCurrentTabs()
        {
            var tabState = new TabState { SavedAt = DateTime.Now };

            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return tabState;

                dynamic shell = Activator.CreateInstance(shellType);
                try
                {
                    var windowsDict = new Dictionary<IntPtr, ExplorerWindow>();

                    foreach (dynamic window in shell.Windows())
                    {
                        try
                        {
                            string fullName = Path.GetFileName((string)window.FullName ?? "");
                            if (!fullName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                                continue;

                            IntPtr hwnd = (IntPtr)window.HWND;
                            string locationUrl = window.LocationURL ?? "";

                            string path = null;
                            if (!string.IsNullOrEmpty(locationUrl) && locationUrl.StartsWith("file:///"))
                            {
                                path = Uri.UnescapeDataString(locationUrl.Replace("file:///", "").Replace("/", "\\"));
                            }

                            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                            {
                                try
                                {
                                    dynamic doc = window.Document;
                                    if (doc != null)
                                    {
                                        dynamic folder = doc.Folder;
                                        if (folder != null)
                                        {
                                            dynamic self = folder.Self;
                                            if (self != null) path = self.Path;
                                        }
                                    }
                                }
                                catch { }
                            }

                            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                            {
                                if (!windowsDict.ContainsKey(hwnd))
                                    windowsDict[hwnd] = new ExplorerWindow();

                                if (!windowsDict[hwnd].TabPaths.Contains(path))
                                {
                                    windowsDict[hwnd].TabPaths.Add(path);
                                    SimpleLogger.Log($"Collected tab: {path}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SimpleLogger.Log($"Error processing window: {ex.Message}");
                        }
                    }

                    tabState.Windows.AddRange(windowsDict.Values);
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Error collecting tabs: {ex.Message}");
            }

            return tabState;
        }

        private void SaveSession(TabState state)
        {
            try
            {
                var tmp = _sessionFilePath + ".tmp";
                var json = JsonConvert.SerializeObject(state, Formatting.Indented);
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Copy(tmp, _sessionFilePath, true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Error saving session: {ex.Message}");
            }
        }

        private TabState LoadSession()
        {
            if (!File.Exists(_sessionFilePath))
            {
                SimpleLogger.Log("No saved session found");
                return null;
            }

            try
            {
                var json = File.ReadAllText(_sessionFilePath, Encoding.UTF8);
                var state = JsonConvert.DeserializeObject<TabState>(json);
                SimpleLogger.Log($"Loaded session: {state?.Windows?.Count ?? 0} window(s), saved {state?.SavedAt}");
                return state;
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Error loading session: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Restores all saved tabs into Explorer using FlaUI UI Automation.
        /// Opens each path as a new tab without keyboard/clipboard tricks.
        /// </summary>
        public void RestoreTabs()
        {
            SimpleLogger.Log("=== RestoreTabs called ===");

            Task.Run(async () =>
            {
                try
                {
                    var state = LoadSession();
                    if (state?.Windows == null || state.Windows.Count == 0)
                    {
                        SimpleLogger.Log("No saved tabs to restore");
                        return;
                    }

                    var allPaths = state.Windows
                        .SelectMany(w => w.TabPaths)
                        .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                        .Distinct()
                        .ToList();

                    if (allPaths.Count == 0)
                    {
                        SimpleLogger.Log("No valid paths to restore");
                        return;
                    }

                    SimpleLogger.Log($"Restoring {allPaths.Count} tab(s)...");

                    // Find or open the Explorer window
                    IntPtr explorerHwnd = FindExplorerWindow();

                    if (explorerHwnd == IntPtr.Zero)
                    {
                        // No Explorer window open yet — launch one and wait for it
                        SimpleLogger.Log("No Explorer window found, launching...");
                        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
                        await Task.Delay(2000);
                        explorerHwnd = FindExplorerWindow();
                    }

                    if (explorerHwnd == IntPtr.Zero)
                    {
                        SimpleLogger.Log("ERROR: Could not get Explorer window handle");
                        return;
                    }

                    // Use FlaUI for ALL tabs including the first one.
                    // NavigateWindowViaCOM silently fails for the active tab, so we navigate
                    // the existing first tab via UIAutomation just like the rest.
                    await OpenTabsViaUIAutomation(explorerHwnd, allPaths, navigateFirstTabInPlace: true);

                    SimpleLogger.Log($"=== Restoration complete: {allPaths.Count} tab(s) ===");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"=== ERROR restoring tabs: {ex.Message} ===");
                    SimpleLogger.Log(ex.StackTrace);
                }
            });
        }

        /// <summary>
        /// Opens each path as a tab in the given Explorer window using FlaUI UI Automation.
        /// When navigateFirstTabInPlace is true, the first path navigates the existing active tab
        /// instead of opening a new one (used on restore so we don't leave an unwanted default tab).
        /// </summary>
        private async Task OpenTabsViaUIAutomation(IntPtr explorerHwnd, List<string> paths, bool navigateFirstTabInPlace = false)
        {
            using var automation = new UIA3Automation();
            var windowElement = automation.FromHandle(explorerHwnd);
            var window = windowElement.AsWindow();

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                try
                {
                    SimpleLogger.Log($"Opening tab {i + 1}/{paths.Count}: {path}");

                    // Bring Explorer to front
                    window.SetForeground();
                    await Task.Delay(150);

                    bool isFirstInPlace = (i == 0 && navigateFirstTabInPlace);
                    if (!isFirstInPlace)
                    {
                        // Click "New tab" button, or fall back to Ctrl+T
                        var newTabButton = window.FindFirstDescendant(
                            cf => cf.ByControlType(ControlType.Button).And(cf.ByName("New tab")));

                        if (newTabButton != null)
                        {
                            newTabButton.AsButton().Invoke();
                        }
                        else
                        {
                            SimpleLogger.Log("  'New tab' button not found, using Ctrl+T");
                            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
                        }

                        await Task.Delay(350);
                    }

                    // Navigate to the path
                    await NavigateCurrentTabViaUIAutomation(window, automation, path);
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"Error opening tab '{path}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Navigates the active Explorer tab to the given path.
        /// Uses Ctrl+L to activate the address bar, then sets the value directly
        /// via the UIAutomation Value pattern (no clipboard).
        /// </summary>
        private async Task<bool> NavigateCurrentTabViaUIAutomation(Window window, UIA3Automation automation, string path)
        {
            // Focus the address bar
            window.SetForeground();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_L);
            await Task.Delay(200);

            // Option 1: grab the currently focused element (should be the address bar edit box)
            var focused = automation.FocusedElement();
            if (focused != null && focused.ControlType == ControlType.Edit)
            {
                if (TrySetValueAndNavigate(focused, path))
                {
                    SimpleLogger.Log($"  Navigated via focused element");
                    return true;
                }
            }

            // Option 2: find the address bar by searching for the edit control inside
            // the "Address Band Root" toolbar
            var addressBand = window.FindFirstDescendant(
                cf => cf.ByName("Address Band Root"));
            var editBox = addressBand?.FindFirstDescendant(
                cf => cf.ByControlType(ControlType.Edit));

            if (editBox != null)
            {
                editBox.Click();
                await Task.Delay(100);
                if (TrySetValueAndNavigate(editBox, path))
                {
                    SimpleLogger.Log($"  Navigated via Address Band Root");
                    return true;
                }
            }

            // Option 3: clipboard fallback (last resort)
            SimpleLogger.Log($"  Using clipboard fallback for navigation");
            string previousClipboard = null;
            try { if (System.Windows.Forms.Clipboard.ContainsText()) previousClipboard = System.Windows.Forms.Clipboard.GetText(); } catch { }
            try
            {
                System.Windows.Forms.Clipboard.SetText(path);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                await Task.Delay(50);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                await Task.Delay(100);
                Keyboard.Press(VirtualKeyShort.RETURN);
                if (previousClipboard != null)
                    try { System.Windows.Forms.Clipboard.SetText(previousClipboard); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"  Clipboard fallback error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tries to set the value of an edit element directly via the UIAutomation Value pattern
        /// and press Enter to navigate. Returns true on success.
        /// </summary>
        private bool TrySetValueAndNavigate(AutomationElement element, string path)
        {
            try
            {
                var valuePattern = element.Patterns.Value.PatternOrDefault;
                if (valuePattern != null)
                {
                    valuePattern.SetValue(path);
                    Thread.Sleep(50);
                    Keyboard.Press(VirtualKeyShort.RETURN);
                    return true;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"  SetValue error: {ex.Message}");
            }
            return false;
        }

        private IntPtr FindExplorerWindow()
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return IntPtr.Zero;

                dynamic shell = Activator.CreateInstance(shellType);
                try
                {
                    foreach (dynamic window in shell.Windows())
                    {
                        try
                        {
                            string fullName = Path.GetFileName((string)window.FullName ?? "");
                            if (fullName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                IntPtr hwnd = (IntPtr)window.HWND;
                                return hwnd;
                            }
                        }
                        catch { }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"FindExplorerWindow error: {ex.Message}");
            }

            return IntPtr.Zero;
        }

        private void NavigateWindowViaCOM(IntPtr hwnd, string path)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return;

                dynamic shell = Activator.CreateInstance(shellType);
                try
                {
                    foreach (dynamic window in shell.Windows())
                    {
                        try
                        {
                            if ((IntPtr)window.HWND == hwnd)
                            {
                                dynamic doc = window.Document;
                                if (doc != null)
                                {
                                    try { doc.Navigate2(path); }
                                    catch { try { doc.Navigate(path); } catch { } }
                                }
                                break;
                            }
                        }
                        catch { }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"NavigateWindowViaCOM error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Dispose();
            _explorerMonitorTimer?.Stop();
            _explorerMonitorTimer?.Dispose();
        }
    }
}
