using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace MacShotThumbnail;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        if (Environment.GetCommandLineArgs().Contains("--supervise")) return Supervisor.Run();
        if (Environment.GetCommandLineArgs().Contains("--install-toolkit"))
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var settings = Settings.Load();
                int encoder = Array.IndexOf(args, "--ffmpeg");
                if (encoder >= 0)
                {
                    if (encoder + 1 >= args.Length || !File.Exists(args[encoder + 1])) throw new FileNotFoundException("FFmpeg executable was not found.");
                    settings.FfmpegPath = Path.GetFullPath(args[encoder + 1]);
                }
                AdvancedTools.InstallAsync(settings, null, CancellationToken.None).GetAwaiter().GetResult();
                settings.Save(); Settings.LogEvent("Optional toolkit installation completed.");
                return 0;
            }
            catch (Exception e) { Settings.Log(e); return 1; }
        }
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
    private int dismissSeconds;
    private readonly int postDragSeconds;
    private readonly double previewOpacity;
    private readonly List<Button> actionButtons = [];
    internal string ImagePath => path;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? CropRequested { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? MoreRequested { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? DismissRequested { get; set; }
    public void Pause() => timer.Stop();
    public void ResumeAfterDrag()
    {
        if (IsDisposed) return;
        dismissSeconds = postDragSeconds;
        expiresAt = DateTime.UtcNow.AddSeconds(dismissSeconds);
        Opacity = previewOpacity;
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
        dismissSeconds = lifetime;
        postDragSeconds = settings.PostDragSeconds;
        previewOpacity = settings.PreviewOpacity / 100.0;
        Opacity = previewOpacity;
        this.path = path;
        this.onClose = onClose;
        Text = "Screenshot thumbnail";
        using var original = Image.FromFile(path);
        AutoScaleMode = AutoScaleMode.None;
        float scale = Math.Min(1f, Math.Min((float)settings.Width / original.Width, (float)(settings.Width * 170 / 260) / original.Height));
        Width = Math.Max(160, (int)(original.Width * scale)) + 6;
        Height = Math.Max(66, (int)(original.Height * scale)) + 6;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(32, 32, 32);
        Padding = new Padding(3);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(settings.Corner.EndsWith("right") ? area.Right - Width - settings.PreviewMargin : area.Left + settings.PreviewMargin,
            settings.Corner.StartsWith("Bottom") ? area.Bottom - Height - settings.PreviewMargin : area.Top + settings.PreviewMargin);

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
            Text = "\uE711", Font = new Font("Segoe Fluent Icons", 11), AccessibleName = "Dismiss screenshot",
            Width = 26, Height = 26, Left = Width - 31, Top = 5,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.White,
            Visible = false, TabStop = false
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (_, _) => Dismiss();
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
        var moreButton = new Button
        {
            Text = "\uE70D", Font = new Font("Segoe Fluent Icons", 11), AccessibleName = "Screenshot actions",
            Width = 26, Height = 26, Left = 65, Top = 5, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.White, Visible = false, TabStop = false
        };
        moreButton.FlatAppearance.BorderSize = 0; toolTip.SetToolTip(moreButton, "Edit / OCR / pin / copy / more");
        moreButton.Click += (_, _) => MoreRequested?.Invoke(); Controls.Add(moreButton); moreButton.BringToFront();
        actionButtons.Add(closeButton);
        if (settings.TrashButton) actionButtons.Add(trashButton);
        if (settings.CropButton) actionButtons.Add(cropButton);
        if (settings.EditButton) actionButtons.Add(moreButton);

        imageBox.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right) Dismiss();
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
        imageBox.Click += (_, _) => { if (!dragging && !suppressClick) Dismiss(); };

        expiresAt = DateTime.UtcNow.AddSeconds(settings.Seconds);
        timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) =>
        {
            bool hovering = Bounds.Contains(Cursor.Position);
            if (!File.Exists(path)) { Close(); return; }
            foreach (var button in actionButtons) button.Visible = hovering || settings.ShowButtonsAlways;
            if (hovering && settings.PauseOnHover)
            {
                expiresAt = DateTime.UtcNow.AddSeconds(dismissSeconds);
                Opacity = previewOpacity;
            }
            else if (lifetime != 0)
            {
                double remaining = (expiresAt - DateTime.UtcNow).TotalSeconds;
                if (remaining <= 0) Close();
                else Opacity = Math.Min(previewOpacity, remaining / 0.5 * previewOpacity);
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

    private void Dismiss() { if (DismissRequested != null) DismissRequested(); else Close(); }

    public void ConfigureBatch(int index, int count, Action<int> navigate)
    {
        if (count < 2) return;
        var position = new Label { Text = $"{index + 1} / {count}", AutoSize = false, Width = 64, Height = 24, TextAlign = ContentAlignment.MiddleCenter,
            Left = (Width - 64) / 2, Top = Height - 29, BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.White };
        Controls.Add(position); position.BringToFront();
        foreach (int direction in new[] { -1, 1 })
        {
            var button = new Button { Text = direction < 0 ? "\uE76B" : "\uE76C", AccessibleName = direction < 0 ? "Previous screenshot" : "Next screenshot",
                Font = new Font("Segoe Fluent Icons", 10), Width = 26, Height = 24, Left = direction < 0 ? position.Left - 27 : position.Right + 1,
                Top = position.Top, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.White };
            button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => navigate(direction);
            toolTip.SetToolTip(button, button.AccessibleName); Controls.Add(button); button.BringToFront();
        }
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
