# MacShot in Action

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
