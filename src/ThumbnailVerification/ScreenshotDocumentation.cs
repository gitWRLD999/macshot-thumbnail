using System.Linq.Expressions;
using System.Reflection;

internal static class ScreenshotDocumentation
{
    public static void Generate(string output)
    {
        Directory.CreateDirectory(output);
        var assembly = Assembly.Load("MacShotThumbnail");
        var settingsType = assembly.GetType("MacShotThumbnail.Settings", true)!;
        var settings = Activator.CreateInstance(settingsType)!;
        settingsType.GetProperty("Folder")!.SetValue(settings, @"C:\Pictures\Screenshots");
        settingsType.GetProperty("Seconds")!.SetValue(settings, 8);
        using var sample = new Bitmap(1000, 650);
        using (var g = Graphics.FromImage(sample))
        {
            g.Clear(Color.FromArgb(246, 248, 250));
            using var title = new Font("Segoe UI", 32, FontStyle.Bold);
            using var text = new Font("Segoe UI", 18);
            using var small = new Font("Segoe UI", 14);
            using var ink = new SolidBrush(Color.FromArgb(28, 36, 43));
            using var green = new SolidBrush(Color.FromArgb(24, 122, 91));
            using var blue = new SolidBrush(Color.FromArgb(48, 107, 207));
            g.FillRectangle(ink, 0, 0, 1000, 70);
            g.DrawString("STUDIO / Sample project", text, Brushes.White, 35, 18);
            g.DrawString("A little room to create.", title, ink, 48, 114);
            g.DrawString("A clean demo canvas for screenshot capture.", text, ink, 50, 183);
            g.FillRectangle(green, 50, 270, 420, 255);
            g.FillRectangle(blue, 500, 270, 450, 255);
            g.DrawString("01", title, Brushes.White, 76, 293);
            g.DrawString("Collect ideas", text, Brushes.White, 76, 445);
            g.DrawString("02", title, Brushes.White, 526, 293);
            g.DrawString("Make something", text, Brushes.White, 526, 445);
            g.DrawString("Sample content only. No personal files or account information.", small, ink, 50, 571);
        }
        string samplePath = Path.Combine(Path.GetTempPath(), "MacShot-docs-" + Guid.NewGuid() + ".png");
        sample.Save(samplePath);
        try
        {
            var thumbnailType = assembly.GetType("MacShotThumbnail.ThumbnailForm", true)!;
            using var thumbnail = (Form)Activator.CreateInstance(thumbnailType, samplePath, Noop(thumbnailType), new Rectangle(0, 0, 1000, 800), settings, null)!;
            thumbnail.Show(); Application.DoEvents();
            thumbnailType.GetMethod("Pause")!.Invoke(thumbnail, null);
            foreach (var button in thumbnail.Controls.OfType<Button>()) button.Visible = true;
            Save(thumbnail, Path.Combine(output, "thumbnail.png"));
            thumbnail.Close();

            var cropType = assembly.GetType("MacShotThumbnail.CropForm", true)!;
            using var crop = (Form)Activator.CreateInstance(cropType, sample)!;
            crop.Show(); Application.DoEvents();
            var view = (Rectangle)cropType.GetMethod("ImageBounds", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(crop, null)!;
            cropType.GetField("selection", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(crop,
                new Rectangle(view.Left + view.Width / 25, view.Top + view.Height / 6, view.Width * 23 / 25, view.Height * 2 / 3));
            foreach (Control panel in crop.Controls)
                foreach (var button in panel.Controls.OfType<Button>()) if (button.Text == "Crop") button.Enabled = true;
            crop.Refresh();
            Save(crop, Path.Combine(output, "crop.png"));
            using var result = (Bitmap)cropType.GetMethod("CreateCrop")!.Invoke(crop, null)!;
            if (result.Width >= sample.Width || result.Height >= sample.Height) throw new Exception("Demo crop failed.");
            crop.Close();

            var settingsFormType = assembly.GetType("MacShotThumbnail.SettingsForm", true)!;
            using var settingsForm = (Form)Activator.CreateInstance(settingsFormType, settings, Noop(settingsType))!;
            settingsForm.Show(); Application.DoEvents();
            Save(settingsForm, Path.Combine(output, "settings.png"));
            settingsForm.Close();
            Console.WriteLine("Rendered thumbnail, functional crop selection, and settings with non-personal sample data.");
        }
        finally { File.Delete(samplePath); }
    }

    private static Delegate Noop(Type type) => Expression.Lambda(typeof(Action<>).MakeGenericType(type), Expression.Empty(), Expression.Parameter(type)).Compile();

    private static void Save(Form form, string path)
    {
        form.TopMost = true;
        form.Refresh();
        Application.DoEvents();
        Thread.Sleep(250);
        Rectangle bounds = form.RectangleToScreen(form.ClientRectangle);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
