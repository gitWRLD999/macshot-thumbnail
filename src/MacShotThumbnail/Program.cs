using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace MacShotThumbnail;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        if (Environment.GetCommandLineArgs().Contains("--supervise")) return Supervisor.Run();
        using var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\MacShotThumbnail.Settings");
        using var mutex = new Mutex(true, "Local\\MacShotThumbnail.SingleInstance", out bool firstInstance);
        if (!firstInstance)
        {
            if (!Environment.GetCommandLineArgs().Contains("--background")) showSettings.Set();
            return 0;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) => Settings.Log(e.Exception);
        if (Environment.GetCommandLineArgs().Contains("--settings")) showSettings.Set();
        Application.Run(new ScreenshotApp(showSettings));
        return 0;
    }
}

internal sealed class ScreenshotApp : ApplicationContext
{
    private const int PrintScreenHotkey = 3;
    private const int PrintScreenMessage = 0x8001;
    private readonly HotkeyWindow hotkeys;
    private readonly KeyboardShortcuts shortcuts;
    private readonly NotifyIcon tray;
    private readonly List<ThumbnailForm> thumbnails = [];
    private Settings settings = Settings.Load();
    private SettingsForm? settingsForm;
    private readonly System.Windows.Forms.Timer settingsTimer;
    private bool capturing;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);


    public ScreenshotApp(EventWaitHandle showSettings)
    {
        hotkeys = new HotkeyWindow(id =>
        {
            if (capturing || settingsForm?.ContainsFocus == true) return;
            if (id == PrintScreenHotkey && settings.Enabled) { Settings.LogEvent("Area capture shortcut received."); RunCapture(CaptureRegion); }
            if (id == 4 && settings.Enabled) { Settings.LogEvent("Full-screen capture shortcut received."); RunCapture(CaptureFullScreen); }
        });
        _ = hotkeys.Handle;

        shortcuts = new KeyboardShortcuts(hotkeys.Handle, PrintScreenMessage, settings);

        var menu = new ContextMenuStrip();
        var enabled = new ToolStripMenuItem("Enabled") { Checked = settings.Enabled, CheckOnClick = true };
        enabled.Click += (_, _) => { settings.Enabled = enabled.Checked; settings.Save(); UpdateShortcuts(); ClosePreviews(); };
        menu.Items.Add(enabled);
        menu.Opening += (_, _) => enabled.Checked = settings.Enabled;
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Capture area", null, (_, _) => RunCapture(CaptureRegion));
        menu.Items.Add("Capture screen", null, (_, _) => RunCapture(CaptureFullScreen));
        menu.Items.Add("Open Screenshots", null, (_, _) => RunCapture(() => {
            Directory.CreateDirectory(settings.Folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(settings.Folder) { UseShellExecute = true });
        }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Screenshot thumbnail",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => OpenSettings();
        settingsTimer = new System.Windows.Forms.Timer { Interval = 250 };
        settingsTimer.Tick += (_, _) => { if (showSettings.WaitOne(0)) OpenSettings(); UpdateShortcuts(); };
        settingsTimer.Start();
        Settings.LogEvent("MacShot started; shortcuts use a dedicated message thread.");
    }

    private void OpenSettings()
    {
        if (settingsForm is { IsDisposed: false }) { ShowWindow(settingsForm.Handle, 9); settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(settings, value => { settings = value; UpdateShortcuts(); ClosePreviews(); });
        settingsForm.Activated += (_, _) => UpdateShortcuts(true);
        settingsForm.Deactivate += (_, _) => UpdateShortcuts();
        settingsForm.FormClosed += (_, _) => UpdateShortcuts();
        settingsForm.Show();
        // Override a hidden startup show state when settings are explicitly requested.
        ShowWindow(settingsForm.Handle, 5);
        settingsForm.Activate();
    }

    private void ClosePreviews()
    {
        foreach (var thumbnail in thumbnails.ToArray()) thumbnail.Close();
    }

    private void RunCapture(Action action)
    {
        if (capturing) return;
        capturing = true;
        UpdateShortcuts();
        try { action(); }
        catch (Exception error)
        {
            Settings.Log(error);
            tray.ShowBalloonTip(5000, "MacShot", error.Message, ToolTipIcon.Warning);
        }
        finally { capturing = false; UpdateShortcuts(); }
    }

    private void UpdateShortcuts(bool forceSuspend = false) => shortcuts.Update(settings, forceSuspend || capturing || settingsForm?.ContainsFocus == true);

    private static Bitmap Capture(Rectangle bounds)
    {
        var image = new Bitmap(bounds.Width, bounds.Height);
        try
        {
            using var graphics = Graphics.FromImage(image);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            return image;
        }
        catch { image.Dispose(); throw; }
    }

    private void CaptureRegion()
    {
        foreach (var thumbnail in thumbnails.ToArray()) thumbnail.Close();
        Rectangle desktop = SystemInformation.VirtualScreen;
        using var screen = Capture(desktop);
        using var selector = new SelectionForm(screen, desktop);
        if (selector.ShowDialog() != DialogResult.OK) return;

        Rectangle selection = selector.Selection;
        using var image = screen.Clone(selection, screen.PixelFormat);
        SaveAndShow(image);
    }

    private void CaptureFullScreen()
    {
        foreach (var thumbnail in thumbnails.ToArray()) thumbnail.Close();
        var screen = Screen.FromPoint(Cursor.Position);
        using var image = Capture(screen.Bounds);
        SaveAndShow(image);
    }

    private void SaveAndShow(Bitmap image)
    {
        string name = $"Screenshot {DateTime.Now:yyyy-MM-dd HH-mm-ss-fff}.png";
        Directory.CreateDirectory(settings.Folder);
        string path = Path.Combine(settings.Folder, name);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        if (settings.CopyToClipboard)
            try { Clipboard.SetDataObject(image, true, 5, 100); } catch (ExternalException error) { Settings.Log(error); }

        ShowPath(path);
    }

    private void CropScreenshot(string path)
    {
        RunCapture(() =>
        {
            foreach (var preview in thumbnails.ToArray()) { preview.Pause(); preview.Hide(); }
            try
            {
                using var original = new Bitmap(path);
                using var image = new Bitmap(original);
                original.Dispose();
                using var editor = new CropForm(image);
                if (editor.ShowDialog() != DialogResult.OK) return;
                using var cropped = editor.CreateCrop();
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    cropped.Save(temporary, System.Drawing.Imaging.ImageFormat.Png);
                    File.Move(temporary, path, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                if (settings.CopyToClipboard)
                    try { Clipboard.SetDataObject(cropped, true, 5, 100); } catch (ExternalException error) { Settings.Log(error); }
                ClosePreviews();
                ShowPath(path);
            }
            finally { foreach (var preview in thumbnails.ToArray()) { preview.Show(); preview.ResumeAfterDrag(); } }
        });
    }

    private void ShowPath(string path)
    {
        var displays = settings.AllDisplays ? Screen.AllScreens : [Screen.FromPoint(Cursor.Position)];
        foreach (var display in displays)
        {
            var thumbnail = new ThumbnailForm(path, form => thumbnails.Remove(form), display.WorkingArea, settings, ClosePreviews);
            thumbnail.CropRequested = () => CropScreenshot(path);
            thumbnails.Add(thumbnail);
            thumbnail.Show();
        }
    }

    protected override void ExitThreadCore()
    {
        foreach (var thumbnail in thumbnails.ToArray()) thumbnail.Close();
        shortcuts.Dispose();
        hotkeys.DestroyHandle();
        settingsTimer.Dispose();
        settingsForm?.Close();
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }

    private sealed class HotkeyWindow(Action<int> onHotkey) : NativeWindow
    {
        public HotkeyWindow() : this(_ => { }) { }

        public new void DestroyHandle() => base.DestroyHandle();

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == PrintScreenMessage) onHotkey(message.WParam.ToInt32());
            base.WndProc(ref message);
        }

        public new IntPtr Handle
        {
            get
            {
                if (base.Handle == IntPtr.Zero) CreateHandle(new CreateParams());
                return base.Handle;
            }
        }
    }
}

internal sealed class SelectionForm : Form
{
    private readonly Bitmap screen;
    private Point start;
    private Point current;
    private bool selecting;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Rectangle Selection { get; private set; }

    public SelectionForm(Bitmap screen, Rectangle desktop)
    {
        this.screen = screen;
        Text = "Select screenshot area";
        FormBorderStyle = FormBorderStyle.None;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = desktop;
        ShowInTaskbar = false;
        TopMost = true;
        Cursor = Cursors.Cross;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(screen, Point.Empty);
        using var dim = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
        e.Graphics.FillRectangle(dim, ClientRectangle);
        Rectangle rect = SelectionRect();
        if (!rect.IsEmpty)
        {
            e.Graphics.DrawImage(screen, rect, rect, GraphicsUnit.Pixel);
            using var pen = new Pen(Color.White, 2);
            e.Graphics.DrawRectangle(pen, rect);
        }
    }

    private Rectangle SelectionRect() => Rectangle.FromLTRB(
        Math.Min(start.X, current.X), Math.Min(start.Y, current.Y),
        Math.Max(start.X, current.X), Math.Max(start.Y, current.Y));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        start = current = e.Location;
        selecting = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!selecting) return;
        current = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!selecting || e.Button != MouseButtons.Left) return;
        selecting = false;
        current = e.Location;
        Selection = Rectangle.Intersect(SelectionRect(), ClientRectangle);
        DialogResult = Selection.Width >= 4 && Selection.Height >= 4 ? DialogResult.OK : DialogResult.Cancel;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        base.OnKeyDown(e);
    }
}

internal sealed class ThumbnailForm : Form
{
    private const int MarginSize = 16;
    private readonly string path;
    private readonly Action<ThumbnailForm> onClose;
    private readonly PictureBox imageBox;
    private readonly Button closeButton;
    private readonly Button trashButton;
    private readonly ToolTip toolTip = new();
    private readonly System.Windows.Forms.Timer timer;
    private DateTime expiresAt;
    private Point dragOrigin;
    private bool dragging;
    private bool suppressClick;
    private readonly int lifetime;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? CropRequested { get; set; }
    public void Pause() => timer.Stop();
    public void ResumeAfterDrag()
    {
        if (IsDisposed) return;
        expiresAt = DateTime.UtcNow.AddSeconds(Math.Max(8, lifetime));
        Opacity = 1;
        timer.Start();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

    public ThumbnailForm(string path, Action<ThumbnailForm> onClose) : this(path, onClose, Screen.FromPoint(Cursor.Position).WorkingArea, new Settings(), null) { }

    private readonly Action? closeAll;
    public ThumbnailForm(string path, Action<ThumbnailForm> onClose, Rectangle area, Settings settings, Action? closeAll)
    {
        this.closeAll = closeAll;
        lifetime = settings.Seconds;
        this.path = path;
        this.onClose = onClose;
        Text = "Screenshot thumbnail";
        using var original = Image.FromFile(path);
        AutoScaleMode = AutoScaleMode.None;
        float scale = Math.Min(1f, Math.Min((float)settings.Width / original.Width, (float)(settings.Width * 170 / 260) / original.Height));
        Width = Math.Max(100, (int)(original.Width * scale)) + 6;
        Height = Math.Max(66, (int)(original.Height * scale)) + 6;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(32, 32, 32);
        Padding = new Padding(3);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(settings.Corner.EndsWith("right") ? area.Right - Width - MarginSize : area.Left + MarginSize,
            settings.Corner.StartsWith("Bottom") ? area.Bottom - Height - MarginSize : area.Top + MarginSize);

        imageBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            // Copy pixels so the preview does not retain the PNG's file handle.
            Image = new Bitmap(original),
            Cursor = Cursors.Hand
        };
        Controls.Add(imageBox);

        closeButton = new Button
        {
            Text = "X", Width = 26, Height = 26, Left = Width - 31, Top = 5,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.White,
            Visible = false, TabStop = false
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (_, _) => Close();
        Controls.Add(closeButton);
        closeButton.BringToFront();

        trashButton = new Button
        {
            Text = "\uE74D", Font = new Font("Segoe Fluent Icons", 11),
            AccessibleName = "Move screenshot to Recycle Bin",
            Width = 26, Height = 26, Left = 5, Top = 5,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.White,
            Visible = false, TabStop = false
        };
        trashButton.FlatAppearance.BorderSize = 0;
        toolTip.SetToolTip(trashButton, "Move to Recycle Bin");
        toolTip.SetToolTip(closeButton, "Dismiss");
        trashButton.Click += (_, _) => Trash();
        Controls.Add(trashButton);
        trashButton.BringToFront();

        var cropButton = new Button
        {
            Text = "\uE7A8", Font = new Font("Segoe Fluent Icons", 11), AccessibleName = "Crop screenshot",
            Width = 26, Height = 26, Left = 35, Top = 5, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.White, Visible = false, TabStop = false
        };
        cropButton.FlatAppearance.BorderSize = 0;
        toolTip.SetToolTip(cropButton, "Crop screenshot");
        cropButton.Click += (_, _) => CropRequested?.Invoke();
        Controls.Add(cropButton); cropButton.BringToFront();

        imageBox.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right) Close();
            if (e.Button == MouseButtons.Left) { dragOrigin = e.Location; suppressClick = false; }
        };
        imageBox.MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || dragging) return;
            Size threshold = SystemInformation.DragSize;
            if (Math.Abs(e.X - dragOrigin.X) < threshold.Width / 2 &&
                Math.Abs(e.Y - dragOrigin.Y) < threshold.Height / 2) return;
            dragging = true;
            suppressClick = true;
            timer?.Stop();
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { path });
            data.SetData(DataFormats.Bitmap, imageBox.Image!);
            try
            {
                DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
            }
            catch (Exception error) { Settings.Log(error); }
            finally { dragging = false; ResumeAfterDrag(); }
        };
        imageBox.Click += (_, _) => { if (!dragging && !suppressClick) Close(); };

        expiresAt = DateTime.UtcNow.AddSeconds(settings.Seconds);
        timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) =>
        {
            bool hovering = Bounds.Contains(Cursor.Position);
            if (!File.Exists(path)) { Close(); return; }
            closeButton.Visible = hovering;
            trashButton.Visible = hovering;
            cropButton.Visible = hovering;
            if (hovering)
            {
                expiresAt = DateTime.UtcNow.AddSeconds(settings.Seconds);
                Opacity = 1;
            }
            else if (settings.Seconds != 0)
            {
                double remaining = (expiresAt - DateTime.UtcNow).TotalSeconds;
                if (remaining <= 0) Close();
                else Opacity = Math.Min(1, remaining / 0.5);
            }
        };
        timer.Start();
        FormClosed += (_, _) =>
        {
            timer.Dispose();
            toolTip.Dispose();
            imageBox.Image?.Dispose();
            onClose(this);
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x00000080 | 0x08000000;
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        int preference = 2;
        DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
    }

    private void Trash()
    {
        try
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            if (closeAll != null) closeAll(); else Close();
        }
        catch (Exception error)
        {
            Settings.Log(error);
            MessageBox.Show(this, error.Message, "Could not move screenshot to Recycle Bin",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
