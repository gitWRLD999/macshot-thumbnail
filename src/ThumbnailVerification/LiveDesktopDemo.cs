using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class LiveDesktopDemo
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    public static void Run(string output, string device)
    {
        var screen = Screen.AllScreens.Single(s => s.DeviceName == device);
        Directory.CreateDirectory(output);
        string path = Path.Combine(output, "desktop-drag-sample.png");
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
            image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            var callbackType = typeof(Action<>).MakeGenericType(type);
            var callback = Expression.Lambda(callbackType, Expression.Empty(), Expression.Parameter(type)).Compile();
            preview = (Form)Activator.CreateInstance(type, path, callback, screen.WorkingArea, settings, null)!;
            // A capture overlay clears on mouse-down before the real OLE drag begins.
            var location = preview.Location - (Size)screen.Bounds.Location;
            var imageBox = preview.Controls.OfType<PictureBox>().Single();
            imageBox.Dock = DockStyle.None;
            imageBox.Bounds = new Rectangle(location.X + 3, location.Y + 3, preview.Width - 6, preview.Height - 6);
            foreach (var button in preview.Controls.OfType<Button>()) button.Location += (Size)location;
            preview.Padding = Padding.Empty;
            preview.BackColor = Color.Magenta;
            var backdrop = new Bitmap(image); preview.BackgroundImage = backdrop;
            imageBox.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                preview.BackgroundImage = null; preview.TransparencyKey = Color.Magenta;
                preview.Refresh();
            };
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
