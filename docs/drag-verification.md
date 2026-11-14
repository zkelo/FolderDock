# Manual file-drag verification

Use an isolated build with disposable source and destination folders; do not
replace the installed FolderDock or use personal files. Automated tests cover
gesture policy, dismissal policy, filesystem notifications and Shell data formats.
They do not exercise a physical mouse gesture between WinUI and other applications.

Check both grid and list views, including a nested recursive search result:

- Click without moving, and move less than the drag threshold: open the item once.
- Drag a file, folder, `.lnk`, `.url`, unknown extension and Unicode name to
  Explorer, the desktop and a compatible application. Use a disposable file for
  the Recycle Bin check. Confirm shortcuts remain shortcut files.
- Change Ctrl, Shift and Ctrl+Shift after starting the drag. Check the cursor's
  operation indicator and the resulting copy, move or shortcut.
- Cancel with Esc, and drop on a destination that rejects files. Neither action
  should open the source item or change it.
- Repeat with pinned and unpinned popups. An unpinned popup must remain visible
  during the drag; after completion it may dismiss if another window has focus.
- Wait for a deferred move to finish: the source list should update. Repeat with
  an item found below the current folder by search.
- After a drag, click another item and switch views: ordinary clicks still work.
  Also activate an item with Enter/Space and through accessibility automation;
  neither should require an intervening mouse click.
- Repeat at 100%, 150% and 200% display scaling and with swapped mouse buttons.
- Hide/reopen the popup or change folders while a pending gesture is canceled:
  no stale source should start dragging or update the replacement folder.

Touch/pen retain ordinary scrolling and activation; native file dragging is
initiated by a mouse gesture. Destination support and Windows privilege boundaries
still determine whether a particular drop is accepted.

## Regression and coverage record (2026-11-14)

The mouse adapter now handles an uninitialized `UIElement.PointerCaptures`
collection. WinUI creates it lazily; enumerating it without a null check can
throw during the first press, before the adapter obtains capture. The fix treats
that state as no existing capture and preserves the item control's click handling.

The first click and first drag must be checked on newly realized GridViewItem
and ListViewItem containers, before any previous successful capture. Repeat after
navigation, changing views, and replacing results with recursive search results.

Real system mouse input reproduced a release-routing failure in the original
synchronous `SHDoDragDrop` path: the native loop stayed active after the button
was released, until Escape. The same gesture completed in an independent
WinForms reference. Deferring the native call and moving it to another STA did
not resolve the failure. The adapter now starts and awaits WinUI's drag operation
and supplies the actual Shell data through `IDataObjectProvider`.

The first WinUI version transferred a file successfully but later crashed while
the CLR finalizer released a native drag object. This matches the upstream
[StartDragAsync finalizer failure](https://github.com/microsoft/microsoft-ui-xaml/issues/10948).
Windows App SDK is therefore updated from 1.6.250108002 to 1.8.260921001, with its
required Windows SDK BuildTools version. A separate diagnostic build forces
collection on a worker after each operation; completed copy and refusal cases
survive collection on the updated runtime. Diagnostic logging and forced
collection are not part of the application build.

### Automated native Windows coverage

All 267 xUnit cases passed locally on .NET 8.0.29: 130 Core, 95 Infrastructure,
and 42 native Shell cases, with no failures or skipped cases. The Windows CI job
runs the same projects. The Shell tests populate the production WinRT data package, release
the local Shell wrapper, force collection, and consume the package through its
native `IDataObject` interface. They inspect both file formats and actual Windows
filesystem drop-target operations. These tests do not synthesize mouse input or
prove WinUI event routing. All transfers use disposable per-test directories.

The x64 Release publish, including ReadyToRun, also passed. Its generated resource
index contains the expected application, manager and popup XAML resources.

| Area | Verified contract |
| --- | --- |
| Files | Actual paths in both CF_HDROP and Shell IDList Array (CIDA), with both payloads decoded |
| Representative extensions | Text, PDF, DOCX, XLSX, PPTX, PNG, JPEG, SVG, MP3, MP4, ZIP, 7Z, HTML, PS1, EXE, unknown and no extension |
| Names and attributes | Unicode, emoji, spaces, hidden, read-only, zero-byte files, full paths of 240 and 259 characters |
| Directories | Empty directories, nested directories and recursive-search source paths |
| Shortcuts | Actual .url/.lnk files, including file/folder targets and missing targets |
| Ordinary and nested Unicode destinations | Real filesystem-target copy, with source and destination content checked |
| Move and link | Shift move removes the source through Windows; Ctrl+Shift creates a shortcut to the source |
| Directory and shortcut copies | Nested directory contents preserved; .lnk copied without replacing it with its target |
| Cancellation and refusal | DragLeave and no allowed operations leave source and destination unchanged |

Extension fixtures contain test bytes. They validate identity and transfer, not
whether a particular application can decode that document/media format.

### Interactive results

The checks use disposable fixtures, an isolated build with the original popup
window style, and system mouse input across application windows. Successful
copies are checked against the source bytes or hash; application opening is
checked against the source path rather than a similarly named destination copy.

| Destination or behavior | Observed result |
| --- | --- |
| Pinned grid to Explorer | Ctrl-copy of a ZIP completed on button release; destination hash matched the source and the popup stayed usable |
| List to Explorer | Shift moved the ZIP, removing the source and preserving its hash; Ctrl copied a folder with its nested file intact |
| Shortcut creation | Ctrl+Shift created a shortcut whose target was the original PNG source path |
| Recursive search to browser | A nested search result was accepted as the actual 78-byte file, with matching hash |
| Escape and the following click | Canceling a search-result drag created no destination file and preserved the source hash; the next ordinary click opened the exact source file in Notepad++ |
| Unpinned popup | Copying the nested result to Explorer completed and the destination hash matched the source |
| Notepad++ tab strip | Accepted a text file and opened its exact source path |
| Notepad++ editor pane | Refused the same file with a `None` result on SDK 1.8; Explorer accepted a file at the same editor position. Use the tab strip for FolderDock drops |
| Browser local file-drop area | The final runtime delivered the PNG and nested file with matching source hashes; no upload was made |
| Clean Release build | A separate build without diagnostic logging delivered the 133-byte text fixture to the browser's local drop area and remained responsive |
| Open Recycle Bin window | A default drag in the clean Release build removed the text source and produced exactly one recycled item with the same 133 bytes, hash and original folder; FolderDock remained alive |
| Runtime lifetime | Ten operations completed with forced worker-thread collection afterward, and the process remained alive on SDK 1.8 |

The Notepad++ editor-pane difference is a known compatibility limitation, not a
verified pass. The current bridge exposes native `FileDrop`, Shell IDList Array
and Windows Runtime `StorageItems`; the destination still controls acceptance.
No general claim that every application or every area within an application
accepts these drops follows from this matrix.

### Remaining environment-specific checks

| Destination or behavior | Status / requirement |
| --- | --- |
| Desktop background and desktop Recycle Bin icon | Not exercised; Explorer and an open Recycle Bin window were verified separately |
| Other text/image editors and media applications | Not exercised; Notepad++ results are recorded above |
| Network upload areas and chat/email attachments | Not exercised; the browser check uses a local drop area without uploading |
| Archive applications and virtual Shell folders | Not exercised; target-specific acceptance must be checked |
| UNC/network shares, removable drives, cloud placeholders | Not exercised; requires those environments, including copy across volumes and offline cases |
| Elevated source and protected destinations | Not exercised; [WinUI does not support StartDragAsync when FolderDock runs as administrator](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.startdragasync?view=windows-app-sdk-1.8). Windows integrity boundaries also apply to destinations |
| Modifier changes during an active drag, keyboard/touch, DPI and swapped buttons | Initial Ctrl/Shift/Ctrl+Shift and Escape were verified; changing modifiers mid-gesture and the other input paths remain unverified |

Paths of 260 or more characters are not guaranteed: Windows Shell may use an
8.3 short path or fail to materialize CF_HDROP with `0x8007007A`. This was observed
in a separate boundary probe and is not covered by the successful path assertions.
