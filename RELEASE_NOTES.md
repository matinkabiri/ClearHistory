## ClearHistory 1.0.0

First Windows release.

- Review Recent Items shortcuts by file category and time range.
- Review recently accessed folders in their own category.
- Remove selected entries from Explorer Home / Recent using Windows' per-destination removal API.
- Preview entries before clearing.
- Dry run is enabled by default; clearing requires confirmation.
- Removes only selected `.lnk` shortcuts and never the original files.
- Includes the Windows Recent folder icon.

The app clears resolvable entries under `%APPDATA%\Microsoft\Windows\Recent` and removes matching Explorer automatic destinations per item. It does not clear Start recommendations from other app/provider sources or other apps' Jump Lists.

The Windows release is a self-contained x64 executable and does not require a separate .NET runtime installation.
