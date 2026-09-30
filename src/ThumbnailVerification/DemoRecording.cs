using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class DemoRecording
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

    public static void Run(string output, string encoder, string device)
    {
        var screen = Screen.AllScreens.Single(s => s.DeviceName == device);
        if (screen.Bounds.Width != 1920 || screen.Bounds.Height != 1080) throw new InvalidOperationException("This demo requires a 1920x1080 SideScreen.");
        Directory.CreateDirectory(output);
        string fixture = Path.Combine(Path.GetTempPath(), "MacShot-demo-" + Guid.NewGuid().ToString("N") + ".png");
        var assembly = Assembly.Load("MacShotThumbnail");
        var settingsType = assembly.GetType("MacShotThumbnail.Settings", true)!;
        var thumbnailType = assembly.GetType("MacShotThumbnail.ThumbnailForm", true)!;
        var cropType = assembly.GetType("MacShotThumbnail.CropForm", true)!;
        var bannerType = assembly.GetType("MacShotThumbnail.CaptureBanner", true)!;
        var engine = assembly.GetType("MacShotThumbnail.CaptureEngine", true)!;
        var settings = Activator.CreateInstance(settingsType)!;
        void Set(string name, object value) => settingsType.GetProperty(name)!.SetValue(settings, value);
        Set("Folder", @"C:\Pictures\Screenshots"); Set("ShowButtonsAlways", true); Set("Seconds", 60);
        Set("Width", 420); Set("CopyToClipboard", false); Set("AdvancedEnabled", false); Set("BannerSeconds", 0);
        using var canvas = new DemoCanvas { Bounds = screen.Bounds };
        Form thumbnail = null, crop = null, banner = null, preferences = null;
        Bitmap cropImage = null;
        Process recording = null;
        string log = Path.Combine(output, "recording.log");
        var messages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        void Show(Form form, Rectangle bounds)
        {
            if (!screen.Bounds.Contains(bounds)) throw new InvalidOperationException("Demo window escaped SideScreen.");
            form.StartPosition = FormStartPosition.Manual; form.Bounds = bounds;
            form.PerformLayout();
            void Handles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) Handles(child); }
            Handles(form);
            var foreground = GetForegroundWindow();
            if (!SetWindowPos(form.Handle, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x10 | 0x40)) throw new InvalidOperationException("Could not show demo window without activation.");
            form.Refresh(); Application.DoEvents();
            if (GetForegroundWindow() != foreground) throw new InvalidOperationException("Foreground changed while showing demo; recording stopped.");
        }
        void Caption(string title, string detail) { canvas.Headline = title; canvas.Detail = detail; canvas.Invalidate(); }
        void Click(Button button) => typeof(Button).GetMethod("OnClick", Private)!.Invoke(button, [EventArgs.Empty]);
        IEnumerable<Button> Buttons(Control control) => control.Controls.Cast<Control>().SelectMany(c => c is Button b ? new[] { b } : Buttons(c));
        void Preview()
        {
            thumbnail?.Dispose();
            var actionType = typeof(Action<>).MakeGenericType(thumbnailType);
            var noop = Expression.Lambda(actionType, Expression.Empty(), Expression.Parameter(thumbnailType)).Compile();
            thumbnail = (Form)Activator.CreateInstance(thumbnailType, fixture, noop, screen.Bounds, settings, null)!;
            thumbnailType.GetMethod("Pause")!.Invoke(thumbnail, null);
            foreach (var button in Buttons(thumbnail)) button.Visible = true;
            thumbnailType.GetProperty("CropRequested")!.SetValue(thumbnail, new Action(() =>
            {
                thumbnail.Dispose(); thumbnail = null;
                using var source = new Bitmap(fixture); cropImage = new Bitmap(source);
                crop = (Form)Activator.CreateInstance(cropType, cropImage)!;
                var size = crop.Size;
                Show(crop, new Rectangle(screen.Bounds.X + (1920 - size.Width) / 2, (1080 - size.Height) / 2, size.Width, size.Height));
            }));
            Show(thumbnail, thumbnail.Bounds);
        }
        void Capture()
        {
            banner?.Dispose(); banner = null;
            var area = screen.Bounds;
            using var bitmap = (Bitmap)engine.GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [area, false])!;
            bitmap.Save(fixture, System.Drawing.Imaging.ImageFormat.Png); Preview();
        }
        void Banner()
        {
            banner = (Form)Activator.CreateInstance(bannerType, settings, new Rectangle(screen.Bounds.X, 75, 1920, 940), new Action<string>(_ => Capture()), new Action(() => { }), new Action<string>(_ => { }))!;
            Show(banner, banner.Bounds);
        }
        async Task Sequence()
        {
            try
            {
                Caption("MacShot Thumbnail", "A lightweight screenshot workflow for Windows"); Banner();
                Console.WriteLine($"READY: SideScreen-only demo at {screen.Bounds}; no keyboard, pointer, clipboard or settings changes.");
                await Task.Delay(8000);
                var info = new ProcessStartInfo(encoder) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                string[] args = ["-y", "-f", "gdigrab", "-draw_mouse", "0", "-framerate", "30", "-offset_x", screen.Bounds.X.ToString(), "-offset_y", screen.Bounds.Y.ToString(), "-video_size", "1920x1080", "-i", "desktop", "-t", "36", "-vf", "scale=1280:720", "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", "-movflags", "+faststart", Path.Combine(output, "macshot-demo.mp4")];
                foreach (string arg in args) info.ArgumentList.Add(arg);
                recording = Process.Start(info)!;
                recording.ErrorDataReceived += (_, e) => { if (e.Data != null) messages.Enqueue(e.Data); }; recording.BeginErrorReadLine();
                await Task.Delay(3000);
                Caption("Capture", "Optional capture banner + a bottom-right preview");
                Click(Buttons(banner).First(b => b.AccessibleName == "Current display"));
                await Task.Delay(4500);
                Caption("Crop", "Refine the screenshot without opening a separate editor");
                Click(Buttons(thumbnail).Single(b => b.AccessibleName == "Crop screenshot"));
                await Task.Delay(1800);
                var view = (Rectangle)cropType.GetMethod("ImageBounds", Private)!.Invoke(crop, null)!;
                for (int i = 1; i <= 25; i++)
                {
                    cropType.GetField("selection", Private)!.SetValue(crop, new Rectangle(view.Left + view.Width * 13 / 100, view.Top + view.Height * 18 / 100, view.Width * 74 / 100 * i / 25, view.Height * 61 / 100 * i / 25));
                    crop.Refresh(); await Task.Delay(40);
                }
                var cropButton = Buttons(crop).Single(b => b.Text == "Crop"); cropButton.Enabled = true;
                await Task.Delay(2000);
                cropButton.Click += (_, _) =>
                {
                    using var result = (Bitmap)cropType.GetMethod("CreateCrop")!.Invoke(crop, null)!;
                    result.Save(fixture, System.Drawing.Imaging.ImageFormat.Png);
                    crop.Dispose(); crop = null; cropImage.Dispose(); cropImage = null; Preview();
                };
                Click(cropButton);
                await Task.Delay(3000);
                Caption("Trash", "The trash-can button moves the screenshot to Recycle Bin");
                await Task.Delay(1500);
                Click(Buttons(thumbnail).Single(b => b.AccessibleName == "Move screenshot to Recycle Bin"));
                if (File.Exists(fixture)) throw new InvalidOperationException("Recycle did not remove the demo fixture.");
                Console.WriteLine("PASS: Native crop and native Recycle Bin action completed.");
                thumbnail = null; await Task.Delay(2200);
                Caption("Dismiss", "Close the preview and keep the screenshot"); Capture();
                await Task.Delay(2200);
                Click(Buttons(thumbnail).Single(b => b.AccessibleName == "Dismiss screenshot")); thumbnail = null;
                if (!File.Exists(fixture)) throw new InvalidOperationException("Dismiss deleted the screenshot.");
                await Task.Delay(1800);
                Caption("Make it yours", "Capture modes, shortcuts, previews and advanced tools");
                var settingsFormType = assembly.GetType("MacShotThumbnail.SettingsForm", true)!;
                var actionType = typeof(Action<>).MakeGenericType(settingsType);
                var noop = Expression.Lambda(actionType, Expression.Empty(), Expression.Parameter(settingsType)).Compile();
                preferences = (Form)Activator.CreateInstance(settingsFormType, settings, noop)!;
                var size = preferences.Size;
                Show(preferences, new Rectangle(screen.Bounds.X + (1920 - size.Width) / 2, (1080 - size.Height) / 2, size.Width, size.Height));
                var tabs = preferences.Controls.OfType<TabControl>().Single();
                foreach (int index in new[] { 0, 2, 4, 5 }) { tabs.SelectedIndex = index; preferences.Refresh(); await Task.Delay(1300); }
                preferences.Dispose(); preferences = null;
                Caption("MacShot Thumbnail", "Capture. Crop. Keep it or trash it."); Banner();
                await recording.WaitForExitAsync();
                if (recording.ExitCode != 0) throw new InvalidOperationException("FFmpeg recording failed.");
                Console.WriteLine("PASS: 36-second SideScreen-only video created; native dismiss kept its file.");
            }
            catch (Exception error) { Console.WriteLine(error); Environment.ExitCode = 1; }
            finally
            {
                if (recording != null && !recording.HasExited) { recording.Kill(); await recording.WaitForExitAsync(); }
                File.WriteAllLines(log, messages); canvas.Close();
            }
        }
        Show(canvas, screen.Bounds);
        using var start = new System.Windows.Forms.Timer { Interval = 100 };
        start.Tick += (_, _) => { start.Stop(); _ = Sequence(); }; start.Start();
        try { Application.Run(); }
        finally
        {
            thumbnail?.Dispose(); crop?.Dispose(); banner?.Dispose(); preferences?.Dispose(); cropImage?.Dispose(); recording?.Dispose();
            if (File.Exists(fixture)) File.Delete(fixture);
        }
    }

    private sealed class DemoCanvas : Form
    {
        public string Headline = "MacShot Thumbnail", Detail = "";
        public DemoCanvas()
        {
            FormBorderStyle = FormBorderStyle.None; AutoScaleMode = AutoScaleMode.None; ShowInTaskbar = false;
            Text = "MacShot safe demo canvas"; DoubleBuffered = true;
            FormClosed += (_, _) => Application.ExitThread();
        }
        protected override bool ShowWithoutActivation => true;
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Color.FromArgb(242, 245, 248));
            using var ink = new SolidBrush(Color.FromArgb(26, 31, 35));
            using var green = new SolidBrush(Color.FromArgb(28, 130, 103));
            using var blue = new SolidBrush(Color.FromArgb(62, 113, 217));
            using var large = new Font("Segoe UI", 36, FontStyle.Bold);
            using var title = new Font("Segoe UI", 26, FontStyle.Bold);
            using var text = new Font("Segoe UI", 19);
            using var small = new Font("Segoe UI", 14);
            g.FillRectangle(ink, 0, 0, Width, 68);
            g.DrawString("MACSHOT / LIVE UI DEMO", text, Brushes.White, 42, 18);
            g.DrawString("A little room to create.", large, ink, 280, 230);
            g.DrawString("STUDIO / Sample project", text, ink, 282, 310);
            g.FillRectangle(green, 280, 405, 645, 320); g.FillRectangle(blue, 955, 405, 685, 320);
            g.DrawString("01", large, Brushes.White, 320, 440); g.DrawString("Collect ideas", title, Brushes.White, 320, 620);
            g.DrawString("02", large, Brushes.White, 995, 440); g.DrawString("Make something", title, Brushes.White, 995, 620);
            g.DrawString("Sample content only", small, ink, 282, 775);
            g.FillRectangle(ink, 0, 920, Width, 160);
            g.DrawString(Headline, title, Brushes.White, 42, 946);
            g.DrawString(Detail, text, Brushes.White, 44, 1005);
        }
    }
}
