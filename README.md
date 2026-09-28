# MacShot Thumbnail

A small Windows screenshot utility inspired by the Mac's floating screenshot thumbnail.

Press **Print Screen**, drag to select an area, and a temporary thumbnail appears in the bottom-right corner of the display under your pointer.

- Drag the thumbnail into an application that accepts image files.
- Click the thumbnail, right-click it, or click X to dismiss it.
- Hover to reveal a trash-can button that moves the saved PNG to the Recycle Bin.
- Leave it alone and it fades after eight seconds. Hovering restarts the countdown.
- Screenshots are saved in your Windows Pictures folder under `Screenshots` and copied to the clipboard when available.
- The tray menu provides full-screen capture, access to saved screenshots, and Quit.

This is an **early release**, developed and tested on one Windows 11 computer. It is not affiliated with Apple or Microsoft.

## Download and Run

Download the Windows x64 ZIP from [Releases](https://github.com/gitWRLD999/macshot-thumbnail/releases), extract it, and run `MacShotThumbnail.exe`. The download includes the .NET runtime; you do not need to install .NET separately.

Windows 11 x64 is the tested platform. The executable is unsigned. There is no account, updater, telemetry, or screenshot upload feature.

For installation and automatic startup at sign-in, run `Install.ps1` from PowerShell in the extracted folder:

```powershell
.\Install.ps1
```

It installs under `%LOCALAPPDATA%\Programs\MacShotThumbnail` and adds a startup entry for the current user. Administrator access is not required. Use `Uninstall.ps1` to remove the installed app and startup entry. Uninstalling does not delete your screenshots.

Running the EXE directly is portable and does not enable startup. Only one instance runs at a time; quit an installed copy from its tray menu before trying a portable copy.

## Behavior and Limits

The app listens for plain Print Screen through a Windows keyboard hook. Modified combinations such as Alt+Print Screen pass through. It does not record or store keystrokes. Quit from the tray to release the key.

Dismissal keeps the saved screenshot. The trash-can button sends it to the Recycle Bin. If your Pictures folder is redirected to OneDrive, Windows/OneDrive may sync the saved files according to your existing settings.

Drag-and-drop depends on the receiving app. Windows may prevent drops between apps running at different privilege levels. Mixed-DPI monitors, HDR, protected content, unusual keyboard mappings, and competing screenshot utilities have not been comprehensively tested. There is no annotation editor.

## Build

Requires Windows and the .NET 9 SDK.

```powershell
dotnet build src/MacShotThumbnail/MacShotThumbnail.csproj -c Release
dotnet publish src/MacShotThumbnail/MacShotThumbnail.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o dist/windows-x64
```

## Verification

```powershell
dotnet run --project src/ThumbnailVerification/ThumbnailVerification.csproj -c Release
```

Run in an interactive Windows desktop session. The check creates a synthetic image in the temporary folder, opens its thumbnail, verifies exclusive access to the PNG and bottom-right placement, checks the trash icon, and invokes the recycle action. It leaves its synthetic test image recoverable in the Recycle Bin.

The Print Screen area selector was also exercised through desktop control. Physical drag-and-drop into other apps and behavior after reboot remain unverified. Bug reports with Windows version, display scaling, and reproduction steps are welcome in [Issues](https://github.com/gitWRLD999/macshot-thumbnail/issues).

## License

[MIT](LICENSE). The bundled .NET runtime remains under its own third-party license notices.
