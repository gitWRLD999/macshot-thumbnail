MacShot Thumbnail v0.3.1 for Windows 11 x64.

- Replace the registry startup entry with a per-user sign-in task, delayed ten seconds, with retries after failures and no battery or execution-time cutoff.
- Supervise the capture process and restart unexpected exits after five seconds; a normal Quit exits both processes.
- Keep the Start with Windows setting and uninstaller synchronized with that task.
- Move keyboard handling to a dedicated message thread and renew its hook periodically, so capture work cannot block shortcut delivery.
- Add bounded startup/capture diagnostics and fix Cancel in the nonmodal settings window.
- Document Adobe Express Photos' competing Print Screen setting.

Verified: clean build, three-display thumbnail/recycle checks, task registration and launch, installed settings startup state, supervisor restarting a forcibly terminated capture process after five seconds, and Print Screen opening MacShot's area selector with Adobe Express Photos open after its override was disabled. Full reboot behavior and broad receiving-app drag-and-drop compatibility remain unverified.

Extract the ZIP and run Install.ps1 to upgrade the installed app and startup task. The executable is unsigned. See README.md for controls and limitations.
