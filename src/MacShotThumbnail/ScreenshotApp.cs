using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MacShotThumbnail;

internal sealed class ScreenshotApp : ApplicationContext
{
    private const int CaptureMessage = 0x8001;
    private readonly HotkeyWindow hotkeys;
    private readonly KeyboardShortcuts shortcuts;
    private readonly NotifyIcon tray;
    private readonly List<ThumbnailForm> thumbnails = [];
    private readonly List<CaptureBanner> banners = [];
    private readonly List<string> batch = [];
    private int batchIndex;
    private Settings settings = Settings.Load();
    private SettingsForm? settingsForm;
    private HistoryForm? history;
    private readonly System.Windows.Forms.Timer settingsTimer;
    private readonly FileSystemWatcher incoming;
    private readonly ConcurrentDictionary<string, int> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> imported = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource shutdown = new();
    private bool capturing;
    private Rectangle lastArea;
    private IntPtr lastWindow;
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    public ScreenshotApp(EventWaitHandle showSettings)
    {
        hotkeys = new HotkeyWindow(id =>
        {
            if (!settings.Enabled || capturing || settingsForm?.ContainsFocus == true) return;
            if (id >= 100 && id - 100 < AdvancedTools.Catalog.Length) { RunTool(AdvancedTools.Catalog[id - 100].Command); return; }
            string mode = id switch { 3 => settings.ShowBanner ? "Banner" : "Area", 4 => settings.FullCaptureMode, 5 => "Combined", 6 => "Separate", 7 => "Window", 8 => "Banner", _ => "" };
            if (mode != "") { Settings.LogEvent(mode + " capture shortcut received."); RequestCapture(mode); }
        });
        shortcuts = new KeyboardShortcuts(hotkeys.Handle, CaptureMessage, settings);
        var menu = new ContextMenuStrip();
        var enabled = new ToolStripMenuItem("Enabled") { Checked = settings.Enabled, CheckOnClick = true };
        enabled.Click += (_, _) => { settings.Enabled = enabled.Checked; settings.Save(); UpdateShortcuts(); ClosePreviews(); CloseBanners(); };
        menu.Items.Add(enabled); menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add("Capture banner", null, (_, _) => ShowBanner());
        menu.Items.Add(new ToolStripSeparator());
        foreach (var mode in CaptureModes.Names) menu.Items.Add(mode.Value, null, (_, _) => RequestCapture(mode.Key));
        var tools = new ToolStripMenuItem("Advanced tools"); menu.Items.Add(tools);
        menu.Opening += (_, _) =>
        {
            enabled.Checked = settings.Enabled;
            tools.DropDownItems.Clear(); tools.Visible = settings.AdvancedEnabled;
            foreach (var tool in AdvancedTools.Catalog.Where(t => settings.Tools.Contains(t.Command))) tools.DropDownItems.Add(tool.Name, null, (_, _) => RunTool(tool.Command));
            tools.DropDownItems.Add(new ToolStripSeparator()); tools.DropDownItems.Add("Toolkit settings...", null, (_, _) => RunTool("OpenMainWindow"));
        };
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Screenshot history...", null, (_, _) => OpenHistory());
        menu.Items.Add("Open Screenshots", null, (_, _) => { Directory.CreateDirectory(settings.Folder); Process.Start(new ProcessStartInfo(settings.Folder) { UseShellExecute = true }); });
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "MacShot", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => OpenSettings();
        Directory.CreateDirectory(AdvancedTools.Incoming);
        incoming = new FileSystemWatcher(AdvancedTools.Incoming) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size, IncludeSubdirectories = true };
        incoming.Created += (_, e) => Queue(e.FullPath); incoming.Changed += (_, e) => Queue(e.FullPath); incoming.Renamed += (_, e) => Queue(e.FullPath);
        incoming.EnableRaisingEvents = true;
        settingsTimer = new System.Windows.Forms.Timer { Interval = 250 };
        settingsTimer.Tick += (_, _) =>
        {
            if (showSettings.WaitOne(0)) OpenSettings();
            IntPtr foreground = CaptureEngine.GetForegroundWindow();
            if (foreground != IntPtr.Zero && !Application.OpenForms.Cast<Form>().Any(f => f.IsHandleCreated && f.Handle == foreground)) lastWindow = foreground;
            ProcessIncoming(); UpdateShortcuts();
        };
        settingsTimer.Start(); Settings.LogEvent("MacShot started; shortcuts use a dedicated message thread.");
    }
    private void Queue(string path)
    {
        if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" }.Contains(Path.GetExtension(path).ToLowerInvariant())) pending.TryAdd(path, 0);
    }
    private void ProcessIncoming()
    {
        if (capturing) return;
        foreach (var entry in pending.ToArray())
        {
            if (imported.Contains(entry.Key)) { pending.TryRemove(entry.Key, out _); continue; }
            try
            {
                if (!File.Exists(entry.Key)) { pending.TryRemove(entry.Key, out _); continue; }
                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(entry.Key)).TotalMilliseconds < 500) continue;
                using var stream = new FileStream(entry.Key, FileMode.Open, FileAccess.Read, FileShare.None);
                using var source = new Bitmap(stream); using var image = new Bitmap(source);
                string path = SaveImage(image, "Advanced");
                imported.Add(entry.Key); pending.TryRemove(entry.Key, out _);
                if (settings.Enabled) Present([path], image);
                history?.RefreshFiles();
            }
            catch (Exception e) when (e is IOException or ArgumentException or ExternalException)
            {
                if (entry.Value >= 20) { pending.TryRemove(entry.Key, out _); Settings.Log(e); }
                else pending.TryUpdate(entry.Key, entry.Value + 1, entry.Value);
            }
            catch (Exception e) { pending.TryRemove(entry.Key, out _); Settings.Log(e); }
        }
    }
    private void OpenSettings()
    {
        CloseBanners();
        if (settingsForm is { IsDisposed: false }) { ShowWindow(settingsForm.Handle, 9); settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(settings, value => { settings = value; UpdateShortcuts(); ClosePreviews(); CloseBanners(); history?.Close(); }, RequestCapture);
        settingsForm.Activated += (_, _) => UpdateShortcuts(true); settingsForm.Deactivate += (_, _) => UpdateShortcuts(); settingsForm.FormClosed += (_, _) => UpdateShortcuts();
        settingsForm.Show(); ShowWindow(settingsForm.Handle, 5); settingsForm.Activate();
    }
    private void OpenHistory()
    {
        if (history is { IsDisposed: false }) { history.Activate(); return; }
        history = new HistoryForm(settings, path => { batch.Clear(); batch.Add(path); batchIndex = 0; ShowCurrent(); }, CropScreenshot, (command, path) => RunTool(command, path));
        history.Show(); ShowWindow(history.Handle, 5); history.Activate();
    }
    private void ClosePreviews() { foreach (var preview in thumbnails.ToArray()) preview.Close(); }
    private void CloseBanners() { foreach (var banner in banners.ToArray()) banner.Close(); }
    private void ShowBanner()
    {
        if (capturing) return;
        if (banners.Count > 0) { CloseBanners(); return; }
        var foreground = CaptureEngine.GetForegroundWindow();
        if (!Application.OpenForms.Cast<Form>().Any(f => f.IsHandleCreated && f.Handle == foreground)) lastWindow = foreground;
        foreach (var screen in settings.BannerAllDisplays ? Screen.AllScreens : [Screen.FromPoint(Cursor.Position)])
        {
            var banner = new CaptureBanner(settings, screen.WorkingArea, RequestCapture, OpenSettings, command => RunTool(command));
            banners.Add(banner); banner.FormClosed += (_, _) => banners.Remove(banner); banner.Show();
        }
    }
    private async void RequestCapture(string mode)
    {
        if (mode == "Banner") { ShowBanner(); return; }
        if (capturing || shutdown.IsCancellationRequested) return;
        Point pointer = Cursor.Position;
        IntPtr target = CaptureEngine.GetForegroundWindow();
        if (Application.OpenForms.Cast<Form>().Any(f => f.IsHandleCreated && f.Handle == target)) target = lastWindow;
        capturing = true; UpdateShortcuts(); CloseBanners(); ClosePreviews();
        try
        {
            // Let topmost previews disappear before sampling desktop pixels.
            await Task.Delay(150 + settings.Delay * 1000, shutdown.Token);
            switch (mode)
            {
                case "Area": CaptureRegion(); break;
                case "Monitor": using (var image = CaptureEngine.Capture(Screen.FromPoint(pointer).Bounds, settings.IncludeCursor)) SaveAndShow(image); break;
                case "Combined": case "Separate": CaptureDisplays(mode == "Separate"); break;
                case "Window": using (var image = CaptureEngine.Capture(CaptureEngine.WindowBounds(target), settings.IncludeCursor)) SaveAndShow(image); break;
                case "LastArea":
                    if (lastArea.IsEmpty) throw new InvalidOperationException("Capture an area first.");
                    var bounds = Rectangle.Intersect(lastArea, SystemInformation.VirtualScreen);
                    using (var image = CaptureEngine.Capture(bounds, settings.IncludeCursor)) SaveAndShow(image); break;
                case "Fixed":
                    var display = Screen.FromPoint(pointer).Bounds;
                    int w = Math.Min(settings.FixedWidth, display.Width), h = Math.Min(settings.FixedHeight, display.Height);
                    lastArea = new Rectangle(Math.Clamp(pointer.X - w / 2, display.Left, display.Right - w), Math.Clamp(pointer.Y - h / 2, display.Top, display.Bottom - h), w, h);
                    using (var image = CaptureEngine.Capture(lastArea, settings.IncludeCursor)) SaveAndShow(image); break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { NotifyError(e); }
        finally { capturing = false; if (!shutdown.IsCancellationRequested) UpdateShortcuts(); }
    }
    private void CaptureRegion()
    {
        Rectangle desktop = SystemInformation.VirtualScreen;
        using var screen = CaptureEngine.Capture(desktop, settings.IncludeCursor);
        using var selector = new SelectionForm(screen, desktop);
        if (selector.ShowDialog() != DialogResult.OK) return;
        Rectangle selected = selector.Selection; lastArea = new Rectangle(selected.X + desktop.X, selected.Y + desktop.Y, selected.Width, selected.Height);
        using var image = screen.Clone(selected, screen.PixelFormat); SaveAndShow(image);
    }
    private void CaptureDisplays(bool separate)
    {
        Rectangle desktop = SystemInformation.VirtualScreen;
        var displays = Screen.AllScreens.OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToArray();
        using var image = CaptureEngine.Capture(desktop, settings.IncludeCursor);
        // Desktop gaps are not monitors; clear them for a deterministic mega image.
        using (var graphics = Graphics.FromImage(image))
        using (var gaps = new Region(new Rectangle(Point.Empty, desktop.Size)))
        {
            foreach (var screen in displays) gaps.Exclude(new Rectangle(screen.Bounds.X - desktop.X, screen.Bounds.Y - desktop.Y, screen.Bounds.Width, screen.Bounds.Height));
            graphics.FillRegion(Brushes.Black, gaps);
        }
        if (!separate) { SaveAndShow(image, "All displays"); return; }
        var split = CaptureEngine.Split(image, desktop, displays.Select(s => s.Bounds).ToArray());
        try
        {
            var paths = new List<string>();
            for (int i = 0; i < split.Count; i++) paths.Add(SaveImage(split[i], displays[i].DeviceName.Replace("\\", "").Replace(".", "")));
            Present(paths, split[0]);
        }
        finally { foreach (var bitmap in split) bitmap.Dispose(); }
    }
    private string SaveImage(Bitmap image, string suffix = "")
    {
        Directory.CreateDirectory(settings.Folder);
        string name = $"{settings.NamePrefix} {DateTime.Now:yyyy-MM-dd HH-mm-ss-fff}{(suffix.Length > 0 ? " " + suffix : "")} {Guid.NewGuid().ToString("N")[..6]}{CaptureEngine.Extension(settings.ImageFormat)}";
        string path = Path.Combine(settings.Folder, name);
        bool created = false;
        try { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); created = true; CaptureEngine.Write(image, stream, settings.ImageFormat, settings.JpegQuality); }
        catch { if (created && File.Exists(path)) File.Delete(path); throw; }
        return path;
    }
    private void SaveAndShow(Bitmap image, string suffix = "") => Present([SaveImage(image, suffix)], image);
    private void Present(IEnumerable<string> paths, Bitmap image)
    {
        batch.Clear(); batch.AddRange(paths); batchIndex = 0;
        if (settings.CopyToClipboard || settings.CopyFiles)
        {
            try
            {
                var data = new DataObject();
                if (settings.CopyToClipboard) data.SetData(DataFormats.Bitmap, image);
                if (settings.CopyFiles) data.SetData(DataFormats.FileDrop, batch.ToArray());
                Clipboard.SetDataObject(data, true, 5, 100);
            }
            catch (ExternalException e) { Settings.Log(e); }
        }
        if (settings.CaptureSound) System.Media.SystemSounds.Asterisk.Play();
        ShowCurrent(); history?.RefreshFiles();
        if (settings.EditAfterCapture && settings.AdvancedEnabled) RunTool("ImageEditor", batch[0], false);
    }
    private void ShowCurrent()
    {
        ClosePreviews(); batch.RemoveAll(path => !File.Exists(path));
        if (batch.Count == 0 || !settings.ShowPreview) return;
        batchIndex = Math.Clamp(batchIndex, 0, batch.Count - 1);
        string path = batch[batchIndex];
        foreach (var display in settings.AllDisplays ? Screen.AllScreens : [Screen.FromPoint(Cursor.Position)])
        {
            var preview = new ThumbnailForm(path, form => thumbnails.Remove(form), display.WorkingArea, settings, () => RemovePath(path));
            preview.CropRequested = () => CropScreenshot(path);
            preview.DismissRequested = ClosePreviews;
            preview.MoreRequested = () => PreviewMenu(path);
            preview.ConfigureBatch(batchIndex, batch.Count, direction => { batchIndex = (batchIndex + direction + batch.Count) % batch.Count; ShowCurrent(); });
            thumbnails.Add(preview); preview.Show();
        }
    }
    private void RemovePath(string path) { batch.Remove(path); ShowCurrent(); history?.RefreshFiles(); }
    private void PreviewMenu(string path)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Copy image", null, (_, _) => { using var image = new Bitmap(path); Clipboard.SetDataObject(image, true, 5, 100); });
        menu.Items.Add("Copy file", null, (_, _) => Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { path }));
        if (batch.Count > 1) menu.Items.Add("Copy all files", null, (_, _) => { var list = new System.Collections.Specialized.StringCollection(); list.AddRange(batch.ToArray()); Clipboard.SetFileDropList(list); });
        menu.Items.Add("Show in folder", null, (_, _) => { var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true }; info.ArgumentList.Add("/select," + path); Process.Start(info); });
        menu.Items.Add("History...", null, (_, _) => OpenHistory());
        if (settings.AdvancedEnabled)
        {
            menu.Items.Add(new ToolStripSeparator());
            foreach (var tool in AdvancedTools.Catalog.Where(t => t.AcceptsFile && settings.Tools.Contains(t.Command))) menu.Items.Add(tool.Name, null, (_, _) => RunTool(tool.Command, path));
        }
        menu.Closed += (_, _) => menu.Dispose(); menu.Show(Cursor.Position);
    }
    private void RunTool(string command, string? path = null, bool hidePreview = true)
    {
        try
        {
            if (!settings.AdvancedEnabled) throw new InvalidOperationException("Enable advanced tools in settings first.");
            CloseBanners(); if (hidePreview) ClosePreviews();
            using var process = AdvancedTools.Run(command, settings, path);
        }
        catch (Exception e) { NotifyError(e); }
    }
    private void CropScreenshot(string path)
    {
        if (capturing) return;
        capturing = true; UpdateShortcuts();
        foreach (var preview in thumbnails.ToArray()) { preview.Pause(); preview.Hide(); }
        try
        {
            using var source = new Bitmap(path); using var image = new Bitmap(source); source.Dispose();
            using var editor = new CropForm(image);
            if (editor.ShowDialog() != DialogResult.OK) return;
            using var cropped = editor.CreateCrop();
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { using (var stream = File.Create(temporary)) CaptureEngine.Write(cropped, stream, CaptureEngine.FormatForPath(path), settings.JpegQuality); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            if (settings.CopyToClipboard) Clipboard.SetDataObject(cropped, true, 5, 100);
            ShowCurrent(); history?.RefreshFiles();
        }
        catch (Exception e) { NotifyError(e); }
        finally { capturing = false; UpdateShortcuts(); foreach (var preview in thumbnails.ToArray()) { preview.Show(); preview.ResumeAfterDrag(); } }
    }
    private void NotifyError(Exception e) { Settings.Log(e); tray.ShowBalloonTip(5000, "MacShot", e.Message, ToolTipIcon.Warning); }
    private void UpdateShortcuts(bool forceSuspend = false) => shortcuts.Update(settings, forceSuspend || capturing || settingsForm?.ContainsFocus == true);
    protected override void ExitThreadCore()
    {
        shutdown.Cancel(); ClosePreviews(); CloseBanners(); shortcuts.Dispose(); hotkeys.DestroyHandle(); incoming.Dispose(); settingsTimer.Dispose();
        settingsForm?.Close(); history?.Close(); tray.Visible = false; tray.Dispose(); base.ExitThreadCore();
    }
    private sealed class HotkeyWindow(Action<int> capture) : NativeWindow
    {
        public new void DestroyHandle() => base.DestroyHandle();
        protected override void WndProc(ref Message message) { if (message.Msg == CaptureMessage) capture(message.WParam.ToInt32()); base.WndProc(ref message); }
        public new IntPtr Handle { get { if (base.Handle == IntPtr.Zero) CreateHandle(new CreateParams()); return base.Handle; } }
    }
}
