MacShot Thumbnail v0.2.0 for Windows 11 x64.

- Mirrored screenshot previews on every display by default, with coordinated recycling.
- Settings window: enabled switch, startup, display scope, corner, size, timeout, clipboard, and output folder.
- Start-menu shortcut and single-instance settings reopening.
- Atomic settings writes, bounded error logs, capture reentrancy protection, and clipboard retries.
- Cancelled drag operations keep the thumbnail available.
- MIT-licensed source and a self-contained Windows x64 download.

Verified on one Windows 11 machine: clean build, three simultaneous display previews, exclusive PNG access, all corner calculations including negative coordinates, recycling with mirrored preview cleanup, settings layout at the current display scaling, and saving settings. Broad app drag-and-drop compatibility, every mixed-DPI configuration, and reboot behavior are not yet verified.

Extract the ZIP and run MacShotThumbnail.exe, or run Install.ps1 for installation and startup at sign-in. The executable is unsigned. See README.md for controls and limitations.
