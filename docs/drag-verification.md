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

### Automated native Windows coverage

42 native cases passed locally through a temporary .NET 10 runner invoking the
same test methods. The normal xUnit/.NET 8 run is part of the Windows CI job.
These tests exercise the production Shell data object and the actual Windows
filesystem drop target; they do not synthesize mouse input or prove WinUI event
routing. All transfers use disposable per-test directories.

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

### Interactive and environment-specific checks still required

| Destination or behavior | Status / requirement |
| --- | --- |
| Grid, list and recursive-search mouse gestures | Not verified interactively in this session; the desktop tool timed out waiting for test-app launch approval |
| Explorer windows and desktop | Filesystem target contract tested; visible mouse drop still required |
| Recycle Bin | Not exercised; use disposable files only and verify cancellation/restore |
| Text/image editors and media applications | Not exercised; each application decides which formats it accepts |
| Browser upload areas and chat/email attachments | Not exercised; test attachment selection without sending or uploading personal data |
| Archive applications and virtual Shell folders | Not exercised; target-specific acceptance must be checked |
| UNC/network shares, removable drives, cloud placeholders | Not exercised; requires those environments, including copy across volumes and offline cases |
| Protected/elevated destinations | Not exercised; Windows permission and integrity-level boundaries apply |
| Modifier changes during drag, Escape, keyboard/touch, DPI and swapped buttons | Retain the interactive checklist above; native payload tests do not cover these input paths |

Paths of 260 or more characters are not guaranteed: Windows Shell may use an
8.3 short path or fail to materialize CF_HDROP with `0x8007007A`. This was observed
in a separate boundary probe and is not covered by the successful path assertions.
