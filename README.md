![App logo](Assets/FolderDock.png)

# FolderDock

![GitHub License](https://img.shields.io/github/license/zkelo/folderdock)
![GitHub Release](https://img.shields.io/github/v/release/zkelo/folderdock)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/zkelo/folderdock/total)
[![CodeFactor](https://www.codefactor.io/repository/github/zkelo/folderdock/badge)](https://www.codefactor.io/repository/github/zkelo/folderdock)

**English** | [Русский](README.ru.md)

Folders on the Windows 11 taskbar — like the macOS Dock. Click a pinned folder icon → a popup with its contents (grid or list) right above the taskbar, no File Explorer needed.

This project is maintained entirely by AI models: **Claude Fable 5** and **GPT 6.1 Sol**.

## Features

- **Pin any folder** to the taskbar via a shortcut with its own AppUserModelID — every folder gets a separate button, they never group with each other.
- **Popup above the taskbar**: a grid with thumbnails (images get previews) or a compact list. Toggle with a button in the header.
- **Recursive search** in grid and list views: a nonempty query searches file and folder names in the current folder and its subfolders by a case-insensitive substring, in the background after a short typing delay. Relative paths appear in the list and in grid/list item tooltips; results are limited to the first 500 matches. Hidden items and inaccessible subfolders are skipped; directory links and junctions can match but are not followed. The popup reports incomplete searches and the result limit. Clearing the query restores the current folder's contents. **Ctrl+F** focuses the search box; the query clears when navigating to another folder or reopening the popup. Changing the query, navigating, or hiding the popup cancels the previous search.
- **Adaptive search field**: the search box shares the folder-title row when the complete title and header controls fit; otherwise it moves to a separate row below.
- Click a file → opens with the default app, passing **folder context** (`NeighboringFilesQuery`) to supported viewers so they can browse neighboring files. Windows Photos uses its viewer URI when it is the default app. If these launch methods fail, the app falls back to the normal shell launch. Popup closes unless pinned.
- Click a subfolder → navigate inside right in the popup; a **Back** button returns to the previous folder in navigation history.
- **Typed drag-and-drop**: the data package carries multiple representations — a single supported image is additionally offered as Bitmap (paste "as picture" into compatible apps), a single supported text file up to 256 KiB as Text, and files or folders as StorageItems. The receiving app picks the format it understands. Dragging requests **Move** by default; hold **Ctrl** to request Copy. The receiving app decides the completed operation. After a move the popup refreshes automatically.
- **Explorer context menu** (right-click an item): the real system shell menu — Open with, Send to, Copy/Cut/Delete/Rename, third-party extensions (7-Zip, TortoiseGit, …) — exactly as in Explorer. The same menu is available on rows in the manager window: right-click a pinned folder to **pin its shortcut to the taskbar**, rename or delete it — no Explorer round-trip.
- "Open in Explorer" button.
- **Pin the popup** with the button in its header to keep it open and on top when clicking outside, opening files, or choosing "Open in Explorer". Pinning survives navigation and grid/list changes, and resets each time a popup opens, including for another folder.
- Click outside an unpinned window → popup closes (flyout behavior).
- Close with the header's **Close** button or another click on the taskbar icon, even when pinned. **Esc** clears a nonempty search first; with the search empty, it closes the popup.
- The manager shows only pins that still exist: shortcuts whose target folder was deleted are filtered out on every window activation.
- **Updates from GitHub Releases**: a "Check for updates" button and an automatic-check toggle (stored in `%LOCALAPPDATA%\FolderDock\settings.json`). Automatic checks are enabled by default and run when a manager window is created. When a newer version is found, the app offers to download the installer for the running app's architecture and runs a silent upgrade after confirmation.
- Launching from the app's own shortcut (Start Menu) keeps its own taskbar identity — it never masquerades as one of the pinned folders.
- **Native Windows Acrylic** in the manager and folder popup, with rounded content surfaces and translucent edges. The blurred colors come from the desktop and windows behind FolderDock, so their appearance varies with the background.
- **System accent and theme**: controls and highlights follow the Windows accent color and light/dark theme. Windows supplies accessible solid-color fallbacks when transparency is disabled, a contrast theme is active, or Acrylic cannot be rendered.
- Win11 rounded popup corners, always on top, hidden from Alt-Tab.
- Single-instance: repeated launches are redirected to the running instance (AppInstance redirect).

## Installation

Grab `FolderDock-Setup-<version>-<arch>.exe` from [Releases](https://github.com/zkelo/FolderDock/releases) and run it. The installer lets you pick the install folder, optionally creates a Start Menu group, and silently removes a previously installed version before upgrading.

Builds are available for **x64** and **ARM64**. Portable `FolderDock-<version>-win-<arch>.zip` archives are also produced by the release workflow; extract the entire archive before running `FolderDock.exe`.

## Building (on Windows)

Requirements:
- Windows 10 17763+ / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 with the **Windows application development** workload is optional; the commands below use the SDK directly

Command line:

```powershell
cd FolderDock
dotnet build FolderDock.csproj -c Release -r win-x64 -p:Platform=x64
# or a ready-to-run exe (self-contained, WindowsAppSDKSelfContained — no MSIX needed):
dotnet publish FolderDock.csproj -c Release -r win-x64 -p:Platform=x64
# ARM64:
dotnet publish FolderDock.csproj -c Release -r win-arm64 -p:Platform=ARM64
```

Output: `bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\FolderDock.exe` (ARM64: `bin\ARM64\Release\net8.0-windows10.0.19041.0\win-arm64\publish\FolderDock.exe`). Keep the complete publish directory with the executable.

> WinUI 3 does not build on Linux/macOS (XamlCompiler.exe is Windows-only). Build on Windows.

## Tests

The Core tests cover popup sessions, name matching, recursive folder search, and dependency boundaries. They need only the .NET 8 SDK and run on Windows, Linux, and macOS:

```sh
dotnet test tests/FolderDock.Core.Tests/FolderDock.Core.Tests.csproj -c Release
```

The xUnit tests reference `FolderDock.Core` directly. [CI](.github/workflows/tests.yml) runs these tests on Ubuntu and builds the WinUI app for x64 and ARM64 on Windows.

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
- The popup is a WinUI 3 `Window` with `OverlappedPresenter.CreateForContextMenu()` (frameless), `IsAlwaysOnTop`, `WS_EX_TOOLWINDOW` (not in Alt-Tab), `DWMWA_WINDOW_CORNER_PREFERENCE=ROUND`. Its horizontal position is anchored to the cursor when opened and clamped to the selected monitor's work area; its bottom edge sits above the bottom of that work area, leaving a taskbar gap. The anchor stays fixed during navigation.
- Both windows use `Window.SystemBackdrop` with `DesktopAcrylicBackdrop`; Windows manages blur, activation, and material fallbacks. WinUI `ThemeResource` brushes provide the current system accent and theme colors without a separate app color setting.
- Thumbnails — `StorageFile.GetThumbnailAsync` (the same previews as Explorer), loaded asynchronously after the window is shown.

## Popup architecture

Popup pinning, dismissal policy, name matching, and recursive filesystem search live in `FolderDock.Core`, a plain `net8.0` library with no WinUI, Windows, or shell dependencies. Its `PopupSession` owns pin state, resets it with `Begin`, changes it with `TogglePin`, and decides dismissal with `ShouldDismiss` using a reason and context. Explicit dismissal always closes the popup; pinning prevents automatic dismissal.

Dependencies point inward: **WinUI app → Core** and **Tests → Core** through project references. `PopupWindow` translates UI and platform events into session calls and window actions. It runs `RecursiveFolderSearch` on a background task with debounce and cancellation, converts results into UI entries, and displays status messages and relative paths. Windows integration stays in the app.

## Structure

| File | Purpose |
|---|---|
| `Program.cs` | Entry point: single-instance, activation redirect, process AUMID |
| `CommandLine.cs` | Tokenizing `--folder` from arguments |
| `App.xaml(.cs)` | Activation routing: popup or manager |
| `PopupWindow.xaml(.cs)` | Popup UI, adaptive search header, background search, session integration, navigation |
| `FolderDock.Core/` | Portable popup session and dismissal policy, name filtering, recursive search (`net8.0`) |
| `FolderDock.Core/RecursiveFolderSearch.cs` | Cancellable recursive name search, inaccessible-folder handling, 500-result limit |
| `tests/FolderDock.Core.Tests/` | xUnit tests with a project reference to Core |
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
- The first launch starts the app process; later clicks are redirected to that instance while it remains running. Startup time depends on the system.
- Browsing a folder and recursive search display at most 500 entries. Search matches names, not file contents or the full relative path.
- The shortcut icon is the standard folder icon from imageres; you can change it in the shortcut properties.

## Code signing policy

When signing is enabled, free code signing is provided by [SignPath.io](https://about.signpath.io), with a certificate from [SignPath Foundation](https://signpath.org). Signing steps in the [release workflow](.github/workflows/release.yml) run only when the SignPath organization secret is configured; otherwise the workflow publishes unsigned artifacts.

Release binaries (`FolderDock.exe` and the installers) are built from this repository by GitHub Actions; the signing steps submit those CI artifacts to SignPath and wait for completion. Signing uses repository secrets for service access; no signing private key is stored in this repository.

Repository and signing authority:

- Authors (commit access): [zkelo](https://github.com/zkelo)
- Reviewers: [zkelo](https://github.com/zkelo) — all external pull requests are reviewed by the maintainer before merge.
- Approvers: [zkelo](https://github.com/zkelo) — each signing request requires explicit approval by the maintainer.

### Privacy policy

The app contains no telemetry code. Update checks contact the GitHub API (`api.github.com`) for release metadata and include the app version in the User-Agent header. Automatic checks are enabled by default when the manager opens and can be disabled in the app settings (`autoCheckUpdates`); manual checks remain available. After confirmation, updates download the selected release installer. Opening the repository link launches the default browser. Access to a selected network folder uses normal Windows filesystem operations.

## License

[MIT](LICENSE)
