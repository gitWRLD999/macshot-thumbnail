# Windows screenshot feature audit

Reviewed official product pages and the ShareX v21.0.0 source on September 29, 2026. This is a survey of major tools, not an exhaustive inventory of every Windows application. Closed-source features were reviewed from documentation, not reverse engineered or copied.

## Products reviewed

| Product | Useful workflows reviewed | MacShot approach |
| --- | --- | --- |
| [ShareX](https://getsharex.com/) (open source) | Region, scrolling, interval capture, recordings, OCR, annotation, effects, utilities | Separate optional portable ShareX engine, launched through its supported command interface |
| [Greenshot](https://getgreenshot.org/help/) (open source) | Region/window/full capture, last region, annotation, clipboard and output destinations | Native capture/clipboard; ShareX annotation and printing |
| [Flameshot](https://flameshot.org/docs/overview/overview/) (open source) | Region selection, drawing, arrows, text, blur and customization | Native lightweight selector plus optional ShareX editor |
| [Windows Snipping Tool](https://support.microsoft.com/en-us/windows/apps/use-snipping-tool-to-capture-screenshots) | Delay, shapes, crop, OCR, redaction, recording | Native delay/crop; ShareX editor, local Windows OCR and recording |
| [Snagit](https://www.techsmith.com/snagit/features/) | Scrolling/panoramic capture, annotation, text recognition, recordings and sharing | ShareX scrolling, annotations, OCR and recording; proprietary AI/team/cloud services excluded |
| [PicPick](https://picpick.app/en/readme/) | Capture presets, multi-monitor capture, editor, color tools and ruler | Native fixed-size/multi-display modes; ShareX editor and utilities |
| [Screenpresso](https://www.screenpresso.com/docs/ScreenpressoHelp.pdf) | Workspace/history, editor, video and documentation workflows | Native searchable screenshot history; ShareX editor/video; document authoring not reproduced |
| [Lightshot](https://app.prntscr.com/en/learnmore.html) | Fast area capture, annotation and sharing | Native fast area capture; ShareX annotation; no automatic public links |
| [Snipaste](https://www.snipaste.com/faq.html) | Floating reference images, pin-to-screen | Mac-style temporary thumbnails plus ShareX persistent pins |
| [FastStone Capture](https://www.faststone.org/FSCaptureDetail.htm) | Floating capture panel, fixed/scrolling capture, video, effects and tools | Native optional capture banner/fixed region; ShareX scrolling/video/effects/tools |
| [Adobe Express Photos](https://helpx.adobe.com/au/express-photos/desktop/edit-images/revert-print-screen-settings.html) | Print Screen capture toolbar and shortcut ownership | Optional MacShot banner; its own configurable shortcuts, no Adobe integration |

## Coverage and customization

**Native:** rectangular area, current monitor, active window, last area, fixed-size area, timed capture, optional pointer, all-display mega image and simultaneous separate monitor files. Capture the virtual desktop once, then split it for separate files. The mega image follows Windows monitor placement, preserves resolutions, and fills gaps black. Native captures are visible desktop pixels, not hidden/occluded-window rendering.

**Native settings:** banner activation, visible modes, display mirroring, top/bottom position and timeout; preview visibility, size, corner, margin, opacity, hover pause, lifetime, post-drag lifetime and action buttons; PNG/JPEG/BMP/TIFF output, JPEG quality, folder, prefix, clipboard image/files, sound and automatic editor. Six native shortcuts and optional shortcuts for each advanced tool are configurable. Filename suffixes remain collision-resistant. No automatic file deletion/retention policy.

**ShareX-backed:** advanced region/freehand capture, scrolling capture, interval capture, MP4/GIF recording with pause/stop/abort, annotation, arrows, text, callouts, steps, shapes, freehand, blur, pixelate, crop, resizing/effects, backgrounds/shadows, persistent pins, OCR, QR, color picker, ruler, combine/split/compare/thumbnail images, video conversion/trim/thumbnails, clipboard viewer and capture history. Background removal requires an additional model download. Availability and supported formats depend on ShareX and its encoders.

Choose visible tools in **Settings > Advanced**, and assign keys in **Shortcuts**. **Open toolkit settings** opens ShareX; its **Task settings** contains region, recording, OCR, effects and tool-specific options. Individual editors also expose tool colors, widths, fonts, effects and other editing options. MacShot-managed defaults apply the next time the toolkit starts. Close it before changing those defaults; advanced options not managed by MacShot remain intact.

**Not claimed:** every proprietary feature, vendor AI services, cloud storage, team comments, hosted public links, paid templates, editable proprietary document formats, webcam composition, dedicated video-timeline editing, native multi-page document authoring or scanner integration. Audio/codec/source options are available through ShareX recording preferences; microphone/system-audio sources need appropriate devices/encoders. Some advanced operations open a deliberate editor/dialog; ordinary screenshots still use only the temporary thumbnail.

## Privacy and licensing

MacShot does not upload screenshots. Its private ShareX profile starts with uploads disabled, no after-upload tasks, and no registered global hotkeys. OCR uses Windows' local recognition engine; optional service-link actions in the OCR dialog are separate from recognition. Users can alter ShareX's preferences independently, so this is not a sandbox or a network-blocking guarantee. Do not enable upload actions for sensitive captures.

MacShot remains MIT. ShareX is a separate GPL program, downloaded unchanged from its official release, not statically linked or included in MacShot's release ZIP. See [integration details](INTEGRATIONS.md).
