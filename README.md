# Clear Recent Activity

A native WPF utility for reviewing and clearing Windows Recent Items shortcuts, including folder shortcuts. It never opens or deletes the target files. The default time range is the last 24 hours and **Dry run** is enabled on first launch.

## Run

Requires Windows and the .NET 8 SDK.

```powershell
dotnet run --project ClearHistory.csproj
```

Build output: `bin\Debug\net8.0-windows\ClearHistory.exe`.

The project root also contains a Release build's `ClearHistory.exe` for convenience, with its required companion files. It requires the .NET 8 Desktop Runtime. GitHub Releases are created automatically for tags named `v*`; when a commit is pushed to `main`, the latest release receives an updated x64 package. The attached x64 package is self-contained.

## Sources and safety limits

| Source | Discovery / timestamp | Selective clear |
|---|---|---|
| `%APPDATA%\Microsoft\Windows\Recent\*.lnk` | WScript Shell resolves the shortcut target. The shortcut file's modified time is used as a proxy for recency; Windows does not expose a dependable last-opened timestamp for each entry here. | Yes. The app deletes only the exact previewed `.lnk` files in this folder. |
| Explorer Home / Recent | Resolved targets are passed to `IApplicationDestinations::RemoveDestination` using the Windows Explorer AppUserModelID. | Per-entry removal from Explorer's automatic Recent/Frequent destinations; pins and app-defined custom categories are outside this API. |
| Other apps' Jump Lists | Per-app automatic and custom destination stores. Windows APIs operate on a known app identity, not a cross-app, timestamped inventory. | Not included. |
| `CustomDestinations` | App-managed binary Jump List stores; may mix pinned and custom groups with recent items. | No. No general supported selective-clear interface. |
| Start Recommended | Windows uses Shell activity for recent files, while other app/provider recommendations may also appear. It is a separate surface from Explorer Home. | Not included in this version. |

The app previews resolved Recent Items shortcuts and uses Windows' per-destination removal API for Explorer's automatic recent list before deleting those shortcuts. The Preview table is the exact shortcut deletion list. Unresolvable shortcuts are skipped. The shortcut modified time is only a recency proxy. Start Recommended, other apps' Jump Lists, pinned items, and custom app categories are not changed. The operation log is stored at `%LOCALAPPDATA%\ClearHistory\operations.log`. Explorer may cache the display briefly; refresh Home if a removed entry remains visible.

## Architecture

- `RecentDiscovery`: reads Recent Items shortcuts and resolves targets.
- `CategoryRow` / `HistoryEntry`: category and preview models.
- `MainWindow`: range filtering, preview, confirmation, dry run, deletion report, and operation logging.
- WPF UI: native Windows desktop controls, independent category selection, time range selection, and preview grid.

The clear is limited to the preview's shortcut paths and matching Explorer destinations. It calls the documented `IApplicationDestinations::RemoveDestination` API for Explorer, then deletes the corresponding Recent-folder shortcuts. Original targets are never deleted.
