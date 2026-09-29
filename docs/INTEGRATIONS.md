# Advanced toolkit

MacShot's native screenshot path has no ShareX dependency. Advanced tools are optional, out-of-process commands to the unmodified official portable ShareX distribution.

- Version: ShareX 21.0.0 x64 portable.
- Download: https://github.com/ShareX/ShareX/releases/download/v21.0.0/ShareX-21.0.0-portable-x64.zip
- SHA-256: `8124938fa718d702bb08ef29292f0140052bdad03d23b3041b5015054ff1c230`.
- Upstream source: https://github.com/ShareX/ShareX/tree/v21.0.0
- Upstream license: https://github.com/ShareX/ShareX/blob/v21.0.0/LICENSE.txt

The installer verifies the pinned checksum before extraction, uses a unique staging directory, and retains upstream files and license notices. It needs no administrator access. Future toolkit releases require a reviewed version/checksum update; MacShot does not silently install new versions.

For unattended setup, `MacShotThumbnail.exe --install-toolkit` downloads into the current execution user's application-data folder; optional `--ffmpeg "C:\path\to\ffmpeg.exe"` selects an existing encoder. Exit code 0 means success, 1 means failure; errors are recorded in MacShot's log. Packaged development environments may redirect application data, so run this command in the same normal desktop context as the installed application.

Toolkit directory: `%LOCALAPPDATA%\MacShotThumbnail\Tools\ShareX`. Its private profile lives in the `ShareX` subdirectory. Defaults disable uploads and competing hotkeys. It runs only when an advanced command is requested and can be closed from MacShot's Advanced settings or its own tray menu. It is separate from MacShot's startup supervisor.

The native screenshot folder receives copies of new advanced image captures, in the selected native output format. Original advanced captures, recordings, and edited exports remain in `%LOCALAPPDATA%\MacShotThumbnail\AdvancedCaptures` and the toolkit's history. Video/GIF recording needs FFmpeg; specify an existing encoder in MacShot settings, or use ShareX's encoder setup. FFmpeg is not bundled by MacShot.

No GPL implementation code is copied into the MIT application. Communication uses executable arguments and files. Source references used to verify integration contracts:

- [CLI dispatch](https://github.com/ShareX/ShareX/blob/v21.0.0/ShareX/ShareXCLIManager.cs)
- [Command names](https://github.com/ShareX/ShareX/blob/v21.0.0/ShareX/Enums.cs)
- [Task settings](https://github.com/ShareX/ShareX/blob/v21.0.0/ShareX/TaskSettings.cs)
- [Local OCR](https://github.com/ShareX/ShareX/blob/v21.0.0/ShareX/Tools/OCR/OCRHelper.cs)

MacShot's uninstaller preserves settings, toolkit files and capture history to avoid deleting user data. They can be removed separately when no longer needed.
