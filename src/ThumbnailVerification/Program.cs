using System.Reflection;
using System.Linq.Expressions;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
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
        var trash = form.Controls.OfType<Button>().Single(b => b.AccessibleName == "Move screenshot to Recycle Bin");
        if (trash.Text != "\uE74D") throw new Exception("Missing trash-can glyph.");
        type.GetMethod("Trash", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, null);
        if (File.Exists(path)) throw new Exception("Recycle failed.");
        Console.WriteLine("PASS: Trash recycled the test PNG while its thumbnail was open.");
        Console.WriteLine("RECYCLED_TEST=" + Path.GetFileName(path));
    }
}
