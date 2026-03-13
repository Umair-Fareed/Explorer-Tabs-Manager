<div align="center">

<img src="ExplorerTabManager/icons/Explorer Tabs Manager.png" width="80" alt="Explorer Tabs Manager icon">

# Explorer Tabs Manager

**Automatically saves and restores your Windows 11 File Explorer tabs.**
Runs silently in the system tray. Never lose your workspace again.

[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4?style=flat-square&logo=windows)](https://www.microsoft.com/windows/windows-11)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-Freeware-00b894?style=flat-square)](#license)

<br>

<a href="https://suzagear.com/tabs">
  <img src="https://img.shields.io/badge/⬇%20%20Get%20the%20One--Click%20Installer-suzagear.com%2Ftabs-00b894?style=for-the-badge" alt="Get the One-Click Installer">
</a>

<br><br>

</div>

---

## What it does

- Saves all open File Explorer tabs to disk every few seconds
- Detects when Explorer is reopened and restores all tabs automatically
- Sits in the system tray — right-click to save, restore, or open settings
- Double-click the tray icon to open Settings

---

## Requirements

- Windows 11 22H2 or later (Build 22621+) — tabs are a Windows 11 feature
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Desktop Runtime)

---

## Build from source

```bash
# 1. Clone the repo
git clone https://github.com/Umair-Fareed/Explorer-Tabs-Manager.git
cd Explorer-Tabs-Manager

# 2. Restore & build
dotnet build ExplorerTabManager/ExplorerTabManager.csproj -c Release

# 3. Run
dotnet run --project ExplorerTabManager/ExplorerTabManager.csproj
```

Or open `ExplorerTabManager.sln` in **Visual Studio 2022** and press `F5`.

The app starts in the system tray. No window appears on launch — look for the icon in the notification area.

---

## Want the one-click installer instead?

No .NET knowledge required. The installer sets everything up automatically, including autostart on Windows login.

<div align="center">
<br>

<a href="https://suzagear.com/tabs">
  <img src="https://img.shields.io/badge/⬇%20%20Download%20Installer-suzagear.com%2Ftabs-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="Download Installer">
</a>

<br><br>
</div>

---

## License

Source code is free to use and modify for personal use.
You may not sell or redistribute this software.
See [LICENSE](LICENSE) for full terms.

© Umair Fareed / SUZA
