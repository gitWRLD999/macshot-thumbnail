# MacShot in Action

## Full Desktop and Real Drag

- `macshot-desktop-drag.mp4`: approximately 23 seconds, 1920x1080, H.264, 30 fps, no audio.
- `macshot-desktop-drag.gif`: looping version, 960x540, 12 fps.

The entire clean SideScreen desktop remains in view, including wallpaper, Windows taskbar, Paint, and the bottom-right screenshot preview. A real mouse gesture invokes the production thumbnail's `DoDragDrop` with its PNG file and bitmap. Microsoft Paint receives and opens that PNG; its canvas reports 1920x1080, and the original file and preview remain available after the drag. The final scene uses Paint's Fit to window control to show the complete received image. No clipboard paste, direct Paint file-open command, or fake drop animation is used.

The computer-use driver only permits gestures within a selected window. The test-only `--live-desktop-demo` harness therefore briefly displays the captured desktop as a full-monitor backdrop behind the unchanged native thumbnail controls. On mouse-down that backdrop becomes transparent, before the native OLE drag begins; the actual underlying Paint window receives the drop. Window classification and preview lifetime are adjusted only in the harness. This capture overlay is not part of the installed MacShot app. Global shortcut handling is not demonstrated by this recording.

The footage is trimmed to remove setup/idle time and a brief capture-overlay repaint, and the drag segment is slowed to 1.5x its original duration for visibility. It includes no personal files or account names; the taskbar and wallpaper were approved for recording. No installed settings or clipboard contents are changed by the harness.

For reproduction, prepare a clean destination app on the selected monitor, record that monitor with FFmpeg, and run:

```powershell
dotnet run --project src/ThumbnailVerification -c Release -- --live-desktop-demo '<private-output-directory>' '<current SideScreen device name>'
```

After eight seconds the harness captures the entire selected monitor. Drag the corner preview into the destination app. Dismiss the preview to end the harness, or let its three-minute safety timeout expire. The PNG remains in the supplied directory. Always inspect a full-desktop capture before publishing it.

## Scripted Feature Walkthrough

- `macshot-demo.mp4`: 36 seconds, 1280x720, H.264, 30 fps, no audio.
- `macshot-demo.gif`: looping version, 960x540, 12 fps.

Recorded from a 1920x1080 SideScreen virtual monitor. Every frame contains only a generated sample canvas and MacShot's real native forms. No personal desktop, account, clipboard, or saved settings are included or changed.

The recording harness runs current-display capture through `CaptureEngine`, creates the real preview and banner, invokes their native crop/trash/dismiss callbacks, and displays a temporary settings form. The crop selection is animated programmatically and the action sequence is scripted. The harness verifies that crop produces an image, trash recycles the owned sample file, and dismiss keeps its file. This is not evidence of end-to-end keyboard shortcut or live drag-and-drop behavior.

Windows are shown with `SWP_NOACTIVATE`; the harness checks foreground identity around each show and never sends global mouse or keyboard input. The user's existing installed MacShot process is untouched. The synthetic trash sample remains recoverable in Recycle Bin.

## Reproduce

Get the current SideScreen device name and bounds using SideScreen's `agent.ps1 -Action Status`; do not reuse a stale display number. The harness currently requires 1920x1080. Supply your own output directory, installed FFmpeg executable, and current device name:

```powershell
dotnet run --project src/ThumbnailVerification -c Release -- --demo-record '<output-directory>' '<ffmpeg.exe>' '<SideScreen device name>'
```

Use FFmpeg to create the looping GIF:

```powershell
ffmpeg -i macshot-demo.mp4 -filter_complex "[0:v]fps=12,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle" -loop 0 macshot-demo.gif
```

Inspect the entire output for personal content before sharing. FFmpeg's local `recording.log` is diagnostic data, not a published demo asset.
