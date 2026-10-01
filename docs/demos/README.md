# MacShot in Action

## Full Desktop and Real Drag

- `macshot-claude-drag-v2.mp4`: 18.3 seconds, 1920x1080, H.264, 30 fps, no audio.
- `macshot-claude-drag-v2.gif`: looping version, 1280x720, up to 20 fps with timed static holds, approximately 1.2 MB.
- `macshot-claude-attached.png`: full-desktop still showing the received image and the remaining preview.

The entire SideScreen desktop remains in view, including wallpaper, Windows taskbar, Claude in Chrome, and the bottom-right screenshot preview. A real mouse gesture invokes the production thumbnail's `DoDragDrop` with its PNG file and bitmap. Claude receives `macshot-demo.png` as a second image attachment in an unsent draft; the original file and preview remain available after the drag. No clipboard paste, file chooser, simulated browser drop, or fake drop animation is used. No message was sent.

The computer-use driver only permits gestures within a selected window. The test-only `--live-desktop-demo` harness therefore briefly displays the captured desktop as a full-monitor backdrop behind the native thumbnail controls. On mouse-down the window region is restricted to the thumbnail so the underlying browser can receive the real OLE drop. The harness enters the existing drag handler early to accommodate the driver's short gesture and restores normal thumbnail bounds after the drop. This replaces the previous color-key overlay that could flash purple. Window classification and preview lifetime are adjusted only in the harness; the overlay is not part of the installed app. Global shortcut handling is not demonstrated by this recording.

This is one recorded take, trimmed to remove setup and idle time. The real drag segment is slowed to four times its original duration for visibility, with phase captions added. The screenshot being dragged is an earlier, reviewed 1920x1080 desktop capture containing generic demo content. The browser's greeting name and account avatar are masked throughout the published footage, and the chat sidebar is hidden. The taskbar and wallpaper were approved for recording. No installed settings or clipboard contents are changed by the harness. All 40 encoded GIF frames were inspected, and both exports were checked for full-screen purple flashes. Private raw footage is not included in the repository.

For reproduction, prepare a clean destination app on the selected monitor, record that monitor with FFmpeg, and run:

```powershell
dotnet run --project src/ThumbnailVerification -c Release -- --live-desktop-demo '<private-output-directory>' '<current SideScreen device name>'
```

After eight seconds the harness captures the entire selected monitor. Optionally append a fourth argument containing a previously reviewed PNG to use that image instead of the new capture as the dragged sample. Drag the corner preview into the destination app. Dismiss the preview to end the harness, or let its three-minute safety timeout expire. `macshot-demo.png` remains in the supplied directory. Inspect every screenshot before uploading or publishing it, and do not send the destination draft without separate approval.

After trimming and redacting the recorded MP4, encode a compact GIF with a shared palette and timed holds:

```powershell
ffmpeg -i macshot-claude-drag-v2.mp4 -filter_complex "[0:v]fps=20,mpdecimate,scale=1280:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=256:reserve_transparent=1:stats_mode=full[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle" -fps_mode vfr -gifflags +offsetting+transdiff -loop 0 -final_delay 20 macshot-claude-drag-v2.gif
```

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
