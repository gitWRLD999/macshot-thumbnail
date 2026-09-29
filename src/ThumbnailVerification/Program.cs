using System.Reflection;
using System.Linq.Expressions;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        if (args.Length == 2 && args[0] == "--desktop-screenshot")
        {
            ScreenshotDocumentation.Generate(args[1], true);
            return;
        }
        if (args.Length == 2 && args[0] == "--screenshots")
        {
            ScreenshotDocumentation.Generate(args[1]);
            return;
        }
        string path = Path.Combine(Path.GetTempPath(), "MacShot-recycle-test-" + Guid.NewGuid() + ".png");
        using (var bitmap = new Bitmap(300, 180))
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.CornflowerBlue);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        var type = Assembly.Load("MacShotThumbnail").GetType("MacShotThumbnail.ThumbnailForm", true)!;
        var actionType = typeof(Action<>).MakeGenericType(type);
        var callback = Expression.Lambda(actionType, Expression.Empty(), Expression.Parameter(type)).Compile();
        using var form = (Form)Activator.CreateInstance(type, path, callback)!;
        form.Show();
        Application.DoEvents();
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Console.WriteLine("PASS: PNG can be opened exclusively while thumbnail is visible.");
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        if (form.Right != area.Right - 16 || form.Bottom != area.Bottom - 16)
            throw new Exception("Thumbnail is not at bottom right.");
        Console.WriteLine("PASS: Thumbnail is positioned at bottom right.");
        type.GetMethod("ResumeAfterDrag")!.Invoke(form, null);
        var expiry = (DateTime)type.GetField("expiresAt", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
        if (form.IsDisposed || expiry < DateTime.UtcNow.AddSeconds(7)) throw new Exception("Missing post-drag grace period.");
        if (!form.Controls.OfType<Button>().Any(b => b.AccessibleName == "Crop screenshot")) throw new Exception("Missing crop button.");
        Console.WriteLine("PASS: Post-drag grace period and crop control.");
        var cropType = type.Assembly.GetType("MacShotThumbnail.CropForm", true)!;
        using (var input = new Bitmap(400, 200))
        using (var crop = (Form)Activator.CreateInstance(cropType, input)!)
        {
            crop.Show(); Application.DoEvents();
            var view = (Rectangle)cropType.GetMethod("ImageBounds", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(crop, null)!;
            cropType.GetField("selection", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(crop, new Rectangle(view.Left, view.Top, view.Width / 2, view.Height / 2));
            using var result = (Bitmap)cropType.GetMethod("CreateCrop")!.Invoke(crop, null)!;
            if (Math.Abs(result.Width - 200) > 1 || Math.Abs(result.Height - 100) > 1) throw new Exception("Crop coordinate mapping failed.");
            crop.Close();
        }
        Console.WriteLine("PASS: Crop maps zoomed image selection to original pixels.");
        var settingsType = type.Assembly.GetType("MacShotThumbnail.Settings", true)!;
        var settings = Activator.CreateInstance(settingsType)!;
        settingsType.GetProperty("Seconds")!.SetValue(settings, 0);
        var mirrors = new List<Form>();
        Action closeAll = () => { foreach (var mirror in mirrors.ToArray()) mirror.Close(); };
        foreach (var display in Screen.AllScreens)
        {
            var mirror = (Form)Activator.CreateInstance(type, path, callback, display.WorkingArea, settings, closeAll)!;
            mirrors.Add(mirror); mirror.Show(); Application.DoEvents();
            if (mirror.Right != display.WorkingArea.Right - 16 || mirror.Bottom != display.WorkingArea.Bottom - 16)
                throw new Exception("Incorrect display placement: " + display.DeviceName);
        }
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        Console.WriteLine($"PASS: {mirrors.Count} simultaneous display previews, no file locks.");
        foreach (string corner in new[] { "Bottom left", "Top left", "Top right" })
        {
            settingsType.GetProperty("Corner")!.SetValue(settings, corner);
            var bounds = new Rectangle(-1920, -1080, 1920, 1080);
            using var preview = (Form)Activator.CreateInstance(type, path, callback, bounds, settings, null)!;
            if ((corner.EndsWith("left") ? preview.Left != bounds.Left + 16 : preview.Right != bounds.Right - 16) ||
                (corner.StartsWith("Top") ? preview.Top != bounds.Top + 16 : preview.Bottom != bounds.Bottom - 16))
                throw new Exception("Incorrect corner: " + corner);
        }
        Console.WriteLine("PASS: All corners support negative display coordinates.");
        var trash = form.Controls.OfType<Button>().Single(b => b.AccessibleName == "Move screenshot to Recycle Bin");
        if (trash.Text != "\uE74D") throw new Exception("Missing trash-can glyph.");
        type.GetMethod("Trash", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(mirrors[0], null);
        if (File.Exists(path)) throw new Exception("Recycle failed.");
        if (mirrors.Any(mirror => !mirror.IsDisposed)) throw new Exception("Other display previews remained after recycling.");
        Console.WriteLine("PASS: Recycling closes every mirrored preview.");
        Console.WriteLine("PASS: Trash recycled the test PNG while its thumbnail was open.");
        Console.WriteLine("RECYCLED_TEST=" + Path.GetFileName(path));
    }
}
