namespace MacShotThumbnail;

internal static class ShortcutRules
{
    public static bool Valid(Keys shortcut)
    {
        Keys key = shortcut & Keys.KeyCode;
        return key == Keys.PrintScreen || key is >= Keys.F1 and <= Keys.F24 ||
            ((shortcut & (Keys.Control | Keys.Alt)) != 0 && key is >= Keys.A and <= Keys.Z);
    }
}

internal sealed class CropForm : Form
{
    private readonly Bitmap source;
    private readonly PictureBox canvas;
    private Point start;
    private Rectangle selection;
    private bool selecting;
    public Bitmap CreateCrop()
    {
        var view = ImageBounds();
        int left = Math.Clamp((int)((selection.Left - view.Left) * source.Width / (double)view.Width), 0, source.Width - 1);
        int top = Math.Clamp((int)((selection.Top - view.Top) * source.Height / (double)view.Height), 0, source.Height - 1);
        int right = Math.Clamp((int)Math.Ceiling((selection.Right - view.Left) * source.Width / (double)view.Width), left + 1, source.Width);
        int bottom = Math.Clamp((int)Math.Ceiling((selection.Bottom - view.Top) * source.Height / (double)view.Height), top + 1, source.Height);
        return source.Clone(Rectangle.FromLTRB(left, top, right, bottom), source.PixelFormat);
    }
    private Rectangle ImageBounds()
    {
        double scale = Math.Min(canvas.ClientSize.Width / (double)source.Width, canvas.ClientSize.Height / (double)source.Height);
        var size = new Size(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)));
        return new Rectangle((canvas.Width - size.Width) / 2, (canvas.Height - size.Height) / 2, size.Width, size.Height);
    }
    public CropForm(Bitmap source)
    {
        this.source = source;
        Text = "Crop screenshot";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(850, 600);
        MinimumSize = new Size(360, 280);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(6), FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Crop", Enabled = false, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        bar.Controls.AddRange([save, cancel]);
        canvas = new PictureBox { Dock = DockStyle.Fill, Image = source, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(32, 32, 32), Cursor = Cursors.Cross };
        Controls.Add(canvas); Controls.Add(bar);
        CancelButton = cancel; AcceptButton = save;
        canvas.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left && ImageBounds().Contains(e.Location)) { start = e.Location; selecting = true; canvas.Capture = true; } };
        canvas.MouseMove += (_, e) =>
        {
            if (!selecting) return;
            selection = Rectangle.Intersect(ImageBounds(), Rectangle.FromLTRB(Math.Min(start.X, e.X), Math.Min(start.Y, e.Y), Math.Max(start.X, e.X), Math.Max(start.Y, e.Y)));
            save.Enabled = selection.Width >= 4 && selection.Height >= 4;
            canvas.Invalidate();
        };
        canvas.MouseUp += (_, _) => { selecting = false; canvas.Capture = false; };
        canvas.Resize += (_, _) => { selection = Rectangle.Empty; save.Enabled = false; canvas.Invalidate(); };
        canvas.Paint += (_, e) => { if (!selection.IsEmpty) { using var pen = new Pen(Color.LimeGreen, 2); e.Graphics.DrawRectangle(pen, selection); } };
    }
}
