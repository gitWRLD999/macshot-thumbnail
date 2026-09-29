using System.Reflection;
using System.Linq.Expressions;

internal static class ExpandedVerification
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run(Assembly assembly)
    {
        var engine = assembly.GetType("MacShotThumbnail.CaptureEngine", true)!;
        var settingsType = assembly.GetType("MacShotThumbnail.Settings", true)!;
        var settings = Activator.CreateInstance(settingsType)!;
        using (var desktop = new Bitmap(600, 300))
        {
            using (var g = Graphics.FromImage(desktop)) { g.Clear(Color.Red); g.FillRectangle(Brushes.Blue, 200, 0, 400, 300); }
            Rectangle bounds = new(-200, -100, 600, 300);
            Rectangle[] displays = [new(-200, -100, 200, 300), new(0, -100, 400, 300)];
            var split = (List<Bitmap>)engine.GetMethod("Split", Static)!.Invoke(null, [desktop, bounds, displays])!;
            try
            {
                if (split.Count != 2 || split[0].Size != displays[0].Size || split[1].Size != displays[1].Size || split[0].GetPixel(50, 50).ToArgb() != Color.Red.ToArgb() || split[1].GetPixel(50, 50).ToArgb() != Color.Blue.ToArgb()) throw new Exception("Multi-display pixel mapping failed.");
            }
            finally { foreach (var image in split) image.Dispose(); }
            foreach (string format in new[] { "PNG", "JPEG", "BMP", "TIFF" })
            {
                using var stream = new MemoryStream(); engine.GetMethod("Write", Static)!.Invoke(null, [desktop, stream, format, 90]); stream.Position = 0;
                using var result = Image.FromStream(stream); if (result.Size != desktop.Size) throw new Exception("Output codec dimensions failed: " + format);
            }
        }
        Console.WriteLine("PASS: Single-snapshot display splitting maps negative origins and exact pixels; four output codecs round-trip.");
        using (var canvas = new Form { AutoScaleMode = AutoScaleMode.None, FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(Screen.PrimaryScreen!.WorkingArea.Location + new Size(50, 50), new Size(200, 100)), TopMost = true, ShowInTaskbar = false, BackColor = Color.LimeGreen })
        {
            canvas.Show(); Application.DoEvents(); Thread.Sleep(200);
            using var captured = (Bitmap)engine.GetMethod("Capture", Static)!.Invoke(null, [new Rectangle(canvas.Left + 20, canvas.Top + 20, 30, 30), false])!;
            Color pixel = captured.GetPixel(15, 15);
            if (pixel.A != 255 || Math.Abs(pixel.G - Color.LimeGreen.G) > 2 || Math.Abs(pixel.R - Color.LimeGreen.R) > 2) throw new Exception("Desktop capture pixel content or alpha failed.");
            canvas.Close();
        }
        Console.WriteLine("PASS: Real desktop capture contains opaque, correct sample-canvas pixels.");
        settingsType.GetProperty("Width")!.SetValue(settings, -9);
        settingsType.GetProperty("PreviewOpacity")!.SetValue(settings, 999);
        settingsType.GetProperty("CombinedShortcut")!.SetValue(settings, Keys.PrintScreen);
        settingsType.GetMethod("Normalize")!.Invoke(settings, null);
        if ((int)settingsType.GetProperty("Width")!.GetValue(settings)! != 160 || (int)settingsType.GetProperty("PreviewOpacity")!.GetValue(settings)! != 100) throw new Exception("Invalid settings not normalized.");
        var shortcutNames = (string[])settingsType.GetField("ShortcutProperties", Static)!.GetValue(null)!;
        if (shortcutNames.Select(n => settingsType.GetProperty(n)!.GetValue(settings)).Distinct().Count() != 6) throw new Exception("Shortcut collision recovery failed.");
        Console.WriteLine("PASS: Settings normalize corrupt ranges and recover six distinct shortcuts.");
        var bannerType = assembly.GetType("MacShotThumbnail.CaptureBanner", true)!;
        string chosen = "";
        using (var banner = (Form)Activator.CreateInstance(bannerType, settings, new Rectangle(-1280, 0, 1280, 720), new Action<string>(s => chosen = s), new Action(() => { }), new Action<string>(_ => { }))!)
        {
            banner.Show(); Application.DoEvents();
            var buttons = banner.Controls[0].Controls.OfType<Button>().ToArray();
            buttons.Single(b => b.AccessibleName == "All displays: separate files").PerformClick();
            if (chosen != "Separate" || banner.Right > 0 || banner.Left < -1280) throw new Exception("Banner action or display placement failed.");
            if (buttons.Any(b => b.Bottom > banner.Controls[0].ClientSize.Height)) throw new Exception("Banner buttons exceed container.");
            banner.Close();
        }
        Console.WriteLine("PASS: Banner buttons dispatch modes and fit a negative-coordinate display.");
        var formType = assembly.GetType("MacShotThumbnail.SettingsForm", true)!;
        var apply = Expression.Lambda(typeof(Action<>).MakeGenericType(settingsType), Expression.Empty(), Expression.Parameter(settingsType)).Compile();
        using (var form = (Form)Activator.CreateInstance(formType, settings, apply)!)
        {
            form.Show(); Application.DoEvents();
            var tabs = form.Controls.OfType<TabControl>().Single();
            if (tabs.TabPages.Count != 6) throw new Exception("Missing settings categories.");
            foreach (TabPage page in tabs.TabPages) { tabs.SelectedTab = page; Application.DoEvents(); if (page.Controls.Count != 1) throw new Exception("Missing settings page content."); }
            var screen = Screen.FromControl(form).WorkingArea;
            if (form.Width > screen.Width || form.Height > screen.Height) throw new Exception("Settings exceed working area.");
            form.Close();
        }
        Console.WriteLine("PASS: All six settings pages render and fit the working area.");
        string temporary = Path.Combine(Path.GetTempPath(), "MacShot-native-test-" + Guid.NewGuid());
        Directory.CreateDirectory(temporary);
        var appType = assembly.GetType("MacShotThumbnail.ScreenshotApp", true)!;
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset);
        var app = (ApplicationContext)Activator.CreateInstance(appType, signal)!;
        settings = Activator.CreateInstance(settingsType)!;
        settingsType.GetProperty("Folder")!.SetValue(settings, temporary);
        settingsType.GetProperty("CopyToClipboard")!.SetValue(settings, false);
        settingsType.GetProperty("ShowPreview")!.SetValue(settings, false);
        appType.GetField("settings", Instance)!.SetValue(app, settings);
        try
        {
            void Capture(string mode)
            {
                appType.GetMethod("RequestCapture", Instance)!.Invoke(app, [mode]);
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while ((bool)appType.GetField("capturing", Instance)!.GetValue(app)!) { Application.DoEvents(); Thread.Sleep(10); if (DateTime.UtcNow > deadline) throw new TimeoutException(mode); }
            }
            Capture("Combined");
            var combinedFiles = Directory.GetFiles(temporary);
            if (combinedFiles.Length != 1) throw new Exception("Combined capture did not create exactly one file.");
            using (var image = Image.FromFile(combinedFiles[0])) if (image.Size != SystemInformation.VirtualScreen.Size) throw new Exception("Mega capture dimensions failed.");
            Capture("Separate");
            var separate = Directory.GetFiles(temporary).Except(combinedFiles).ToArray();
            if (separate.Length != Screen.AllScreens.Length) throw new Exception("Separate capture count failed.");
            var actual = separate.Select(path => { using var image = Image.FromFile(path); return image.Size; }).OrderBy(s => s.Width).ThenBy(s => s.Height).ToArray();
            var expected = Screen.AllScreens.Select(s => s.Bounds.Size).OrderBy(s => s.Width).ThenBy(s => s.Height).ToArray();
            if (!actual.SequenceEqual(expected)) throw new Exception("Separate capture dimensions failed.");
            Console.WriteLine($"PASS: Real mega desktop {SystemInformation.VirtualScreen.Size} and {separate.Length} separate displays saved through the full application pipeline.");
        }
        finally
        {
            app.ExitThread(); app.Dispose();
            foreach (var path in Directory.GetFiles(temporary)) File.Delete(path);
            Directory.Delete(temporary);
        }
    }
}
