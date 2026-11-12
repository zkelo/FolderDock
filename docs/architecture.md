# Architecture

FolderDock separates application rules from operating-system details. Dependencies
point toward `FolderDock.Core`; the application creates and connects adapters in
`App.xaml.cs`.

| Location | Responsibility | Dependencies |
| --- | --- | --- |
| `FolderDock.Core` | Folder navigation, load/search lifetimes, preferences, available shortcuts, login startup and update selection | .NET base types and interfaces owned by Core |
| `FolderDock.Infrastructure` | Filesystem access, JSON preferences, GitHub releases, installer downloads, process launch and login registration | Core and platform/framework APIs |
| WinUI application | Composition, event handling, rendering, localization and Windows Shell/WinRT adapters | Core, Infrastructure and Windows UI APIs |

Windows Shell helpers remain in the outer application assembly because they share
COM/WinRT integration and window handles. They do not become dependencies of Core.
`WindowsFolderShortcutStore` implements Core's shortcut port; windows call
`PinnedFolderService` instead of enumerating the filesystem themselves. UI layout,
icons and localization remain presentation concerns.

## Application boundaries

- `SettingsService` owns the active preference and the fallback to defaults.
  `JsonSettingsStore` owns the compatible `autoCheckUpdates` JSON field and atomic
  replacement of the settings file. A failed save retains the active session value.
- `PinnedFolderService` selects shortcuts whose destination folders still exist.
  The Windows adapter reads and writes `.lnk` files and owns COM resources.
- `UpdateService` compares versions and selects the installer for the injected
  architecture. Release parsing, HTTP requests, downloading and process execution
  are separate adapters. Only a user-confirmed update invokes installation.
  Typed update failures are localized by the presentation layer.
- `FolderNavigation` owns root/current folder identity, back history and query
  resets. `FolderLoadSession` and `FolderSearchSession` share cancellation and
  latest-request ownership. Search waits 180 ms by default before reading folders.
- A completion is only a receipt, not permission to render: the UI must check
  `IsCurrent` after awaiting and confirm the window is still showing that folder.
  Cancelling also invalidates results that already completed but have not rendered.

Construction of settings and manager services stays on the manager path. Popup
preparation does not read user folders. Folder loading and thumbnails remain
deferred until after the popup has been shown.

## File dragging

`FileDragGesture` is a platform-independent gesture state machine: it owns
pointer identity, the drag threshold and cancellation. `FileDragSource` translates
WinUI mouse events and Windows DPI metrics into that policy. Native WinUI dragging
is disabled for both item hosts, so only one drag mechanism owns the gesture.
Ordinary clicks remain single-click actions; the release after a drag cannot
open the source item.

`IFileDragDrop` is a presentation-side port because it accepts a native window
handle. `ShellDragDrop` implements it with Shell `IDataObject` and `SHDoDragDrop`.
Windows owns modifier keys, destination negotiation and filesystem operations.
Neither Core nor the UI deletes sources based on a returned drop effect.

`PopupSession` owns dismissal policy, including keeping an unpinned popup alive
during a drag. The window invalidates stale drag completions when it is reopened
or hidden. `IFolderChangeSource` belongs to Core; its filesystem adapter reports
changes, while `FolderRefreshController` marshals and coalesces notifications on
the UI queue. Watcher registration runs off the UI thread and stale registrations
are disposed. Unhealthy subscriptions are replaced on the next refresh; failed
registration remains a graceful fallback. Recursive observation is enabled for
search results only.

## Changes and validation

Add application decisions to Core and introduce an interface only for a real
external dependency. Implement that dependency in an outer adapter and wire it
in `App`. Do not introduce HTTP, persistence formats, registry access, processes,
native imports or UI framework types into Core.

Architecture tests inspect assembly references and metadata to enforce those
boundaries. Behavioral tests cover cancellation races, navigation, preference
fallbacks, shortcut selection, release responses and installer staging with fakes
and temporary directories; they do not install updates or edit real settings.

```powershell
dotnet test tests/FolderDock.Core.Tests/FolderDock.Core.Tests.csproj -c Release
dotnet test tests/FolderDock.Infrastructure.Tests/FolderDock.Infrastructure.Tests.csproj -c Release
# Windows only: query actual Shell formats without moving user files
dotnet test tests/FolderDock.Shell.Tests/FolderDock.Shell.Tests.csproj -c Release
dotnet publish FolderDock.csproj -c Release -r win-x64 -p:Platform=x64
dotnet publish FolderDock.csproj -c Release -r win-arm64 -p:Platform=ARM64
```

CI runs the test projects on Linux and Windows, publishes both Windows
architectures and compiles their installers. Live Windows interaction still needs
manual verification: taskbar activation, drag-and-drop, Shell menus, neighboring
photos, pinning and update dialogs. Test such interactions in an isolated copy.
