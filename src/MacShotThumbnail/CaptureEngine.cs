using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MacShotThumbnail;

internal static class CaptureModes
{
    internal static readonly Dictionary<string, string> Names = new()
    {
        ["Area"] = "Area", ["Monitor"] = "Current display", ["Combined"] = "All displays: mega image",
        ["Separate"] = "All displays: separate files", ["Window"] = "Active window", ["LastArea"] = "Last area", ["Fixed"] = "Fixed-size area"
    };
    internal static readonly Dictionary<string, string> Icons = new()
    {
        ["Area"] = "\uE7A8", ["Monitor"] = "\uE7F4", ["Combined"] = "\uEBC6", ["Separate"] = "\uE8A7",
        ["Window"] = "\uE737", ["LastArea"] = "\uE777", ["Fixed"] = "\uE9E9"
    };
}

internal static class CaptureEngine
{
    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo { public int Size, Flags; public IntPtr Handle; public Point Position; }
    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo { public bool Icon; public int X, Y; public IntPtr Mask, Color; }
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, int step, IntPtr brush, int flags);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect rect, int size);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    internal static Rectangle WindowBounds(IntPtr window)
    {
        if (window == IntPtr.Zero) throw new InvalidOperationException("No active window is available.");
        if (DwmGetWindowAttribute(window, 9, out var r, Marshal.SizeOf<NativeRect>()) != 0 && !GetWindowRect(window, out r))
            throw new InvalidOperationException("The active window could not be captured.");
        var bounds = Rectangle.Intersect(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom), SystemInformation.VirtualScreen);
        if (bounds.Width < 1 || bounds.Height < 1) throw new InvalidOperationException("The active window is outside the desktop.");
        return bounds;
    }
    internal static Bitmap Capture(Rectangle bounds, bool pointer)
    {
        CheckSize(bounds);
        var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(image);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            if (pointer)
            {
                var cursor = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
                if (GetCursorInfo(ref cursor) && cursor.Flags == 1 && GetIconInfo(cursor.Handle, out var icon))
                {
                    try
                    {
                        var dc = graphics.GetHdc();
                        try { DrawIconEx(dc, cursor.Position.X - bounds.X - icon.X, cursor.Position.Y - bounds.Y - icon.Y, cursor.Handle, 0, 0, 0, IntPtr.Zero, 3); }
                        finally { graphics.ReleaseHdc(dc); }
                    }
                    finally { if (icon.Mask != IntPtr.Zero) DeleteObject(icon.Mask); if (icon.Color != IntPtr.Zero) DeleteObject(icon.Color); }
                }
            }
            return image;
        }
        catch { image.Dispose(); throw; }
    }
    internal static void CheckSize(Rectangle bounds)
    {
        if (bounds.Width < 1 || bounds.Height < 1 || (long)bounds.Width * bounds.Height > 100_000_000)
            throw new InvalidOperationException("Capture dimensions must be between 1 pixel and 100 megapixels.");
    }
    internal static List<Bitmap> Split(Bitmap desktop, Rectangle virtualBounds, Rectangle[] displays)
    {
        var result = new List<Bitmap>();
        try
        {
            foreach (var bounds in displays)
            {
                if (!virtualBounds.Contains(bounds)) throw new ArgumentOutOfRangeException(nameof(displays));
                var region = new Rectangle(bounds.X - virtualBounds.X, bounds.Y - virtualBounds.Y, bounds.Width, bounds.Height);
                result.Add(desktop.Clone(region, desktop.PixelFormat));
            }
            return result;
        }
        catch { foreach (var image in result) image.Dispose(); throw; }
    }
    internal static string Extension(string format) => format switch { "JPEG" => ".jpg", "BMP" => ".bmp", "TIFF" => ".tiff", _ => ".png" };
    internal static void Write(Image image, Stream stream, string format, int quality)
    {
        if (format == "JPEG")
        {
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)Math.Clamp(quality, 1, 100));
            image.Save(stream, ImageCodecInfo.GetImageEncoders().Single(c => c.FormatID == ImageFormat.Jpeg.Guid), parameters);
        }
        else image.Save(stream, format switch { "BMP" => ImageFormat.Bmp, "TIFF" => ImageFormat.Tiff, _ => ImageFormat.Png });
    }
    internal static string FormatForPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "JPEG", ".bmp" => "BMP", ".tif" or ".tiff" => "TIFF", _ => "PNG" };
}
