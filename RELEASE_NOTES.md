## ClearHistory 1.0.0

First Windows release.

- Review Recent Items shortcuts by file category and time range.
- Review recently accessed folders in their own category.
- Preview entries before clearing.
- Dry run is enabled by default; clearing requires confirmation.
- Removes only selected `.lnk` shortcuts and never the original files.
- Includes the Windows Recent folder icon.

The app currently clears only resolvable entries under `%APPDATA%\Microsoft\Windows\Recent`. Windows Jump Lists and Start recommendations use separate stores and are not selectively cleared.

The Windows release is a self-contained x64 executable and does not require a separate .NET runtime installation.
