# FolderDock

![GitHub License](https://img.shields.io/github/license/zkelo/folderdock)
![GitHub Release](https://img.shields.io/github/v/release/zkelo/folderdock)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/zkelo/folderdock/total)
[![CodeFactor](https://www.codefactor.io/repository/github/zkelo/folderdock/badge)](https://www.codefactor.io/repository/github/zkelo/folderdock)

**English** | [Русский](README.ru.md)

Folders on the Windows 11 taskbar — like the macOS Dock. Click a pinned folder icon → a popup with its contents (grid or list) right above the taskbar, no File Explorer needed.

## Features

- **Pin any folder** to the taskbar via a shortcut with its own AppUserModelID — every folder gets a separate button, they never group with each other.
- **Popup above the taskbar**: a grid with thumbnails (images get previews) or a compact list. Toggle with a button in the header.
- Click a file → opens with the default app **with folder context** (`NeighboringFilesQuery`): in Photos and other viewers arrow keys browse to the next/previous file, exactly as when opening from Explorer. Popup closes.
- Click a subfolder → navigate inside right in the popup; a **Back** button returns to the parent folder.
- **Typed drag-and-drop**: the data package carries multiple representations — images are additionally added as Bitmap (paste "as picture" into Paint/Word/messengers), text files as Text (paste content into an editor), and always StorageItems (files for Explorer and everything else). The receiving app picks the format it understands. Dragging **moves** the item (like Explorer); hold **Ctrl** to copy instead. After a move the popup refreshes automatically.
- **Explorer context menu** (right-click an item): the real system shell menu — Open with, Send to, Copy/Cut/Delete/Rename, third-party extensions (7-Zip, TortoiseGit, …) — exactly as in Explorer. The same menu is available on rows in the manager window: right-click a pinned folder to **pin its shortcut to the taskbar**, rename or delete it — no Explorer round-trip.
- "Open in Explorer" button.
- Click outside the window → popup closes (flyout behavior).
- Click the icon again → toggle (close).
- The manager shows only pins that still exist: shortcuts whose target folder was deleted are filtered out on every window activation.
- **Updates from GitHub Releases**: a "Check for updates" button and an auto-check-on-launch toggle (stored in `%LOCALAPPDATA%\FolderDock\settings.json`). When a newer version is found, the app offers to download the installer for your architecture and runs a silent upgrade.
- Launching from the app's own shortcut (Start Menu) keeps its own taskbar identity — it never masquerades as one of the pinned folders.
- Dark/light theme — follows the system. Win11 rounded corners, always on top, hidden from Alt-Tab.
- Single-instance: repeated launches are redirected to the running instance (AppInstance redirect).

## Installation

Grab `FolderDock-Setup-<version>-<arch>.exe` from [Releases](https://github.com/zkelo/FolderDock/releases) and run it. The installer lets you pick the install folder, optionally creates a Start Menu group, and silently removes a previously installed version before upgrading.

## Building (on Windows)

Requirements:
- Windows 10 17763+ / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 with the **Windows application development** workload — or just the SDK + command line

Command line:

```powershell
cd FolderDock
dotnet build -c Release -p:Platform=x64
# or a ready-to-run exe (self-contained, WindowsAppSDKSelfContained — no MSIX needed):
dotnet publish -c Release -r win-x64 -p:Platform=x64
```

Output: `bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\FolderDock.exe`

> WinUI 3 does not build on Linux/macOS (XamlCompiler.exe is Windows-only). Build on Windows.

## Usage

1. Run `FolderDock.exe` with no arguments → the manager window.
2. "Add folder" → pick a folder → a shortcut is created in `%LOCALAPPDATA%\FolderDock\Shortcuts`.
3. Right-click the folder in the list → **"Pin to taskbar"** (the real Explorer shell menu — pin, rename, delete, properties without leaving the manager).
4. Click the pinned icon → a popup with the folder contents.

Pin as many folders as you like — each one gets its own icon.

## Localization

UI languages: **English (en-US, default)**, **Русский (ru)**, **Українська (uk)**. The language follows the Windows preferred-languages list; missing strings fall back to English.

All strings live in one file per language: `Strings/<BCP-47 tag>/Resources.resw`. To add a language:

1. Copy `Strings/en-US/` to `Strings/<your tag>/` (e.g. `Strings/de-DE/`).
2. Translate the `<value>` elements — keys must stay identical across languages.
3. Rebuild. That's it — no code changes: WinUI PRI picks the folder up automatically.

Key conventions: keys with a dot (`AddFolderLabel.Text`) bind to XAML elements via `x:Uid`; plain keys (`Update_Checking`) are used from code via `Loc.Get`/`Loc.Format` (`{0}`, `{1}` are placeholders — keep them in translations).

## How it works

- Windows 11 does not allow pinning a folder to the taskbar directly and has no API for a "taskbar popup". Workaround: a shortcut to `FolderDock.exe --folder "<path>"` with `System.AppUserModel.ID` written into the shortcut's property store, unique per folder (SHA1 of the path). On start the process sets the same AUMID via `SetCurrentProcessExplicitAppUserModelID` — the taskbar matches the window to the right pinned icon. Launched without `--folder`, the process (and each window via `SHGetPropertyStoreForWindow`) uses the app's own AUMID instead.
- One background process: `AppInstance.FindOrRegisterForKey` + `RedirectActivationToAsync`. A click on any shortcut lands in the live instance → instant popup with no cold start.
- The popup is a WinUI 3 `Window` with `OverlappedPresenter.CreateForContextMenu()` (frameless), `IsAlwaysOnTop`, `WS_EX_TOOLWINDOW` (not in Alt-Tab), `DWMWA_WINDOW_CORNER_PREFERENCE=ROUND`. Positioned at the cursor, snapped to the top edge of the work area above the taskbar.
- Thumbnails — `StorageFile.GetThumbnailAsync` (the same previews as Explorer), loaded asynchronously after the window is shown.

## Structure

| File | Purpose |
|---|---|
| `Program.cs` | Entry point: single-instance, activation redirect, process AUMID |
| `CommandLine.cs` | Tokenizing `--folder` from arguments |
| `App.xaml(.cs)` | Activation routing: popup or manager |
| `PopupWindow.xaml(.cs)` | Popup window: UI events, toggle, back navigation |
| `PopupChrome.cs` | Window chrome: presenter, DWM, positioning, Share interop |
| `PopupLayout.cs` | Pure popup size math (constants tied to XAML templates) |
| `ShareCoordinator.cs` | Share flow: DataTransferManager, state, light-dismiss guard |
| `FolderEntry.cs` | Folder item model |
| `FolderContents.cs` | Read result: entries + access-failure flag |
| `FolderReader.cs` | Reading folder contents |
| `ThumbnailLoader.cs` | Async Explorer thumbnails (cancellable) |
| `TypedDataPackage.cs` | DataPackage: Bitmap/Text/StorageItems; sync fill for drag |
| `ShellContextMenu.cs` | Explorer shell context menu: IContextMenu/2/3, menu-message subclassing |
| `ManagerWindow.xaml(.cs)` | Manager: adding folders, creating shortcuts, filtering dead pins |
| `PinInfo.cs` | Manager list row model |
| `AppInfo.cs` | Version, build timestamp, repository URL for the UI |
| `Localization.cs` | `Loc.Get`/`Loc.Format` — access to .resw strings |
| `Strings/<lang>/Resources.resw` | UI strings per language (en-US, ru, uk) |
| `Settings.cs` | JSON settings in `%LOCALAPPDATA%\FolderDock` |
| `UpdateService.cs` | Update check via GitHub Releases API, installer download |
| `ShortcutFactory.cs` | COM IShellLink + IPropertyStore: .lnk with AUMID, reading args back |
| `FolderAumid.cs` | AppUserModelID from a folder path |
| `WindowAumid.cs` | Per-window AUMID via SHGetPropertyStoreForWindow |
| `PropertyStoreInterop.cs` | Shared COM types: IPropertyStore, PropertyKey, PropVariant |
| `Shell.cs` | Launching files (Photos URI / NeighboringFilesQuery) and Explorer |
| `FileAssociation.cs` | Default-app detection per extension (AssocQueryString) |
| `Native.cs` | Win32/COM: DWM, cursor, DPI, window styles, Share interop |
| `installer/FolderDock.iss` | Inno Setup script (install dir, Start Menu, MIT license, upgrade) |

## Limitations

- Pinning to the taskbar is manual via right-click (Windows does not allow programmatic pinning for third-party apps).
- The first click after a reboot is a cold start (~1–2 s), instant afterwards.
- The shortcut icon is the standard folder icon from imageres; you can change it in the shortcut properties.

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

Release binaries (`FolderDock.exe` and the installers) are built from this repository by [GitHub Actions](.github/workflows/release.yml); only CI-built artifacts are submitted to SignPath for signing. The private key is HSM-backed and held by SignPath — this project never stores it.

Team roles (single-maintainer project):

- Authors (commit access): [zkelo](https://github.com/zkelo)
- Reviewers: [zkelo](https://github.com/zkelo) — all external pull requests are reviewed by the maintainer before merge.
- Approvers: [zkelo](https://github.com/zkelo) — each signing request requires explicit approval by the maintainer.

### Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. One exception: the optional update check contacts the GitHub API (`api.github.com`) to read the latest release version. It can be turned off in the app settings (`autoCheckUpdates`).

## License

[MIT](LICENSE)
