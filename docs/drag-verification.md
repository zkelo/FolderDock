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
- Repeat at 100%, 150% and 200% display scaling and with swapped mouse buttons.
- Hide/reopen the popup or change folders while a pending gesture is canceled:
  no stale source should start dragging or update the replacement folder.

Touch/pen retain ordinary scrolling and activation; native file dragging is
initiated by a mouse gesture. Destination support and Windows privilege boundaries
still determine whether a particular drop is accepted.
