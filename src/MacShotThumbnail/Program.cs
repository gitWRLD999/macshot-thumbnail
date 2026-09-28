using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace MacShotThumbnail;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "Local\\MacShotThumbnail.SingleInstance", out bool firstInstance);
        if (!firstInstance) return;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new ScreenshotApp());
    }
}

internal sealed class ScreenshotApp : ApplicationContext
{
    private const int PrintScreenHotkey = 3;
    private const int KeyboardHook = 13;
    private const int PrintScreenKey = 0x2C;
    private const int PrintScreenMessage = 0x8001;
    private readonly HotkeyWindow hotkeys;
    private readonly KeyboardHookProc keyboardHookProc;
    private readonly IntPtr keyboardHook;
    private readonly NotifyIcon tray;
    private readonly List<ThumbnailForm> thumbnails = [];
    private readonly string captureFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
    private bool printScreenDown;
    private long lastPrintScreen;

    private delegate IntPtr KeyboardHookProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardEvent
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int id, KeyboardHookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    public ScreenshotApp()
    {
        Directory.CreateDirectory(captureFolder);
        hotkeys = new HotkeyWindow(id =>
        {
            if (id == PrintScreenHotkey) CaptureRegion();
        });

        keyboardHookProc = HandleKeyboard;
        keyboardHook = SetWindowsHookEx(KeyboardHook, keyboardHookProc, GetModuleHandle(null), 0);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Capture area   Print Screen", null, (_, _) => CaptureRegion());
        menu.Items.Add("Capture screen", null, (_, _) => CaptureFullScreen());
        menu.Items.Add("Open Screenshots", null, (_, _) => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(captureFolder) { UseShellExecute = true }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Screenshot thumbnail",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => CaptureRegion();

        if (keyboardHook == IntPtr.Zero)
        {
            tray.ShowBalloonTip(6000, "Screenshot shortcut unavailable",
                "Print Screen could not be connected. You can still capture from the tray icon.", ToolTipIcon.Warning);
        }
    }

    private IntPtr HandleKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && Marshal.PtrToStructure<KeyboardEvent>(data).VirtualKey == PrintScreenKey)
        {
            bool modified = (GetAsyncKeyState(0x10) & 0x8000) != 0 ||
                (GetAsyncKeyState(0x11) & 0x8000) != 0 ||
                (GetAsyncKeyState(0x12) & 0x8000) != 0 ||
                (GetAsyncKeyState(0x5B) & 0x8000) != 0 ||
                (GetAsyncKeyState(0x5C) & 0x8000) != 0;
            if (!modified)
            {
                int kind = message.ToInt32();
                if (kind is 0x0100 or 0x0104)
                {
                    if (!printScreenDown) QueuePrintScreen();
                    printScreenDown = true;
                }
                else if (kind is 0x0101 or 0x0105)
                {
                    if (!printScreenDown) QueuePrintScreen();
                    printScreenDown = false;
                }
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(keyboardHook, code, message, data);
    }

    private void QueuePrintScreen()
    {
        long now = Environment.TickCount64;
        if (now - lastPrintScreen < 400) return;
        lastPrintScreen = now;
        PostMessage(hotkeys.Handle, PrintScreenMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private static Bitmap Capture(Rectangle bounds)
    {
        var image = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(image);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        return image;
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
        string path = Path.Combine(captureFolder, name);
        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        try { Clipboard.SetImage(image); } catch (ExternalException) { /* Another app owns the clipboard. */ }

        var thumbnail = new ThumbnailForm(path, (form) => thumbnails.Remove(form));
        thumbnails.Add(thumbnail);
        thumbnail.Show();
    }

    protected override void ExitThreadCore()
    {
        foreach (var thumbnail in thumbnails.ToArray()) thumbnail.Close();
        if (keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(keyboardHook);
        hotkeys.DestroyHandle();
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
            if (message.Msg == PrintScreenMessage) onHotkey(PrintScreenHotkey);
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
        Bounds = desktop;
        FormBorderStyle = FormBorderStyle.None;
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
    private const int MaxWidth = 260;
    private const int MaxHeight = 170;
    private const int LifetimeSeconds = 8;
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

    public ThumbnailForm(string path, Action<ThumbnailForm> onClose)
    {
        this.path = path;
        this.onClose = onClose;
        Text = "Screenshot thumbnail";
        using var original = Image.FromFile(path);
        float scale = Math.Min(1f, Math.Min((float)MaxWidth / original.Width, (float)MaxHeight / original.Height));
        Width = Math.Max(100, (int)(original.Width * scale)) + 6;
        Height = Math.Max(66, (int)(original.Height * scale)) + 6;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(32, 32, 32);
        Padding = new Padding(3);
        StartPosition = FormStartPosition.Manual;
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - MarginSize, area.Bottom - Height - MarginSize);

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

        imageBox.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right) Close();
            if (e.Button == MouseButtons.Left) dragOrigin = e.Location;
        };
        imageBox.MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || dragging) return;
            Size threshold = SystemInformation.DragSize;
            if (Math.Abs(e.X - dragOrigin.X) < threshold.Width / 2 &&
                Math.Abs(e.Y - dragOrigin.Y) < threshold.Height / 2) return;
            dragging = true;
            timer?.Stop();
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { path });
            data.SetData(DataFormats.Bitmap, imageBox.Image!);
            DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
            Close();
        };
        imageBox.Click += (_, _) => { if (!dragging) Close(); };

        expiresAt = DateTime.UtcNow.AddSeconds(LifetimeSeconds);
        timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) =>
        {
            bool hovering = Bounds.Contains(Cursor.Position);
            closeButton.Visible = hovering;
            trashButton.Visible = hovering;
            if (hovering)
            {
                expiresAt = DateTime.UtcNow.AddSeconds(LifetimeSeconds);
                Opacity = 1;
            }
            else
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
            Close();
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "Could not move screenshot to Recycle Bin",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
