using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class LiveDesktopDemo
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    public static void Run(string output, string device, string samplePath = null)
    {
        var screen = Screen.AllScreens.Single(s => s.DeviceName == device);
        Directory.CreateDirectory(output);
        string path = Path.Combine(output, "macshot-demo.png");
        var assembly = Assembly.Load("MacShotThumbnail");
        var settingsType = assembly.GetType("MacShotThumbnail.Settings", true)!;
        var type = assembly.GetType("MacShotThumbnail.ThumbnailForm", true)!;
        var settings = Activator.CreateInstance(settingsType)!;
        settingsType.GetProperty("Seconds")!.SetValue(settings, 120);
        settingsType.GetProperty("PostDragSeconds")!.SetValue(settings, 60);
        settingsType.GetProperty("Width")!.SetValue(settings, 420);
        settingsType.GetProperty("ShowButtonsAlways")!.SetValue(settings, true);
        Form preview = null;
        using var captureTimer = new System.Windows.Forms.Timer { Interval = 8000 };
        using var timeout = new System.Windows.Forms.Timer { Interval = 180000 };
        captureTimer.Tick += (_, _) =>
        {
            captureTimer.Stop();
            var engine = assembly.GetType("MacShotThumbnail.CaptureEngine", true)!;
            using var image = (Bitmap)engine.GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [screen.Bounds, false])!;
            if (samplePath is null) image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            else
            {
                using var sample = new Bitmap(samplePath);
                sample.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            var callbackType = typeof(Action<>).MakeGenericType(type);
            var callback = Expression.Lambda(callbackType, Expression.Empty(), Expression.Parameter(type)).Compile();
            preview = (Form)Activator.CreateInstance(type, path, callback, screen.WorkingArea, settings, null)!;
            // Restrict overlay hit testing during OLE, then restore normal thumbnail bounds.
            // This avoids the color-key repaint flashes of a full-monitor transparent form.
            var location = preview.Location - (Size)screen.Bounds.Location;
            var thumbnailBounds = new Rectangle(location, preview.Size);
            var imageBox = preview.Controls.OfType<PictureBox>().Single();
            imageBox.Dock = DockStyle.None;
            imageBox.Bounds = new Rectangle(location.X + 3, location.Y + 3, preview.Width - 6, preview.Height - 6);
            foreach (var button in preview.Controls.OfType<Button>()) button.Location += (Size)location;
            preview.Padding = Padding.Empty;
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(preview, true);
            var backdrop = new Bitmap(image); preview.BackgroundImage = backdrop;
            bool overlayCleared = false;
            imageBox.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                bool firstDrag = !overlayCleared;
                if (firstDrag)
                {
                    overlayCleared = true;
                    preview.BackgroundImage = null;
                    preview.Region = new Region(thumbnailBounds);
                }
                // The window-scoped driver sends a very short gesture. Enter the existing
                // native drag handler on press so OLE is ready before the pointer arrives.
                typeof(Control).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(imageBox,
                    [new MouseEventArgs(MouseButtons.Left, 0, e.X + SystemInformation.DragSize.Width, e.Y, 0)]);
                if (firstDrag)
                {
                    foreach (Control control in preview.Controls) control.Location -= (Size)location;
                    preview.Bounds = new Rectangle(screen.Bounds.Location + (Size)thumbnailBounds.Location, thumbnailBounds.Size);
                    preview.Region = null;
                }
            };
            preview.GiveFeedback += (_, e) => Console.WriteLine($"OLE effect={e.Effect}");
            preview.QueryContinueDrag += (_, e) => Console.WriteLine($"OLE action={e.Action}, keys={e.KeyState}");
            preview.Bounds = screen.Bounds;
            preview.ShowInTaskbar = true;
            preview.FormClosed += (_, _) => { backdrop.Dispose(); Application.ExitThread(); };
            preview.Show();
            SetWindowLongPtr(preview.Handle, -20, new IntPtr(GetWindowLongPtr(preview.Handle, -20).ToInt64() & ~0x08000080L));
            Console.WriteLine($"READY: real native preview of {screen.DeviceName} at {preview.Bounds}, sample={path}");
        };
        timeout.Tick += (_, _) => { preview?.Close(); Application.ExitThread(); };
        captureTimer.Start(); timeout.Start();
        Console.WriteLine("Capturing the chosen whole desktop in eight seconds; drag the native thumbnail into an app.");
        try { Application.Run(); }
        finally { preview?.Dispose(); }
    }
}
