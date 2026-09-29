using System.Reflection;
using System.Text.Json.Nodes;

internal static class AdvancedToolkitVerification
{
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    public static void Run(Assembly assembly)
    {
        var toolkit = assembly.GetType("MacShotThumbnail.AdvancedTools", true)!;
        var settingsType = assembly.GetType("MacShotThumbnail.Settings", true)!;
        var settings = settingsType.GetMethod("Load")!.Invoke(null, null)!;
        bool Running() => (bool)toolkit.GetProperty("Running", Static)!.GetValue(null)!;
        if (!(bool)toolkit.GetProperty("Installed", Static)!.GetValue(null)! || Running()) throw new InvalidOperationException("Install the toolkit, then close it before running recording verification.");
        string encoder = (string)settingsType.GetProperty("FfmpegPath")!.GetValue(settings)!;
        if (!File.Exists(encoder)) throw new FileNotFoundException("Set the FFmpeg path first.");
        string personal = (string)toolkit.GetProperty("Personal", Static)!.GetValue(null)!;
        string incoming = (string)toolkit.GetProperty("Incoming", Static)!.GetValue(null)!;
        toolkit.GetMethod("Prepare", Static)!.Invoke(null, [settings]);
        string configPath = Path.Combine(personal, "ApplicationConfig.json"), original = File.ReadAllText(configPath);
        string marker = "MacShotRecordingTest-" + Guid.NewGuid().ToString("N");
        var display = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == @"\\.\DISPLAY2") ?? Screen.PrimaryScreen!;
        using var canvas = new Form { FormBorderStyle = FormBorderStyle.None, AutoScaleMode = AutoScaleMode.None, ShowInTaskbar = false, TopMost = true,
            StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(display.Bounds.Location + new Size(120, 120), new Size(640, 360)), Text = "MacShot recording verification" };
        int frame = 0;
        canvas.Paint += (_, e) => { e.Graphics.Clear(Color.FromArgb(24, 122, 91)); e.Graphics.FillRectangle(Brushes.CornflowerBlue, 40 + frame % 450, 120, 100, 120); using var font = new Font("Segoe UI", 24); e.Graphics.DrawString("MacShot sample recording", font, Brushes.White, 30, 30); };
        using var animation = new System.Windows.Forms.Timer { Interval = 80 };
        animation.Tick += (_, _) => { frame += 8; canvas.Invalidate(); }; animation.Start(); canvas.Show(); Application.DoEvents();
        var config = JsonNode.Parse(original)!;
        var capture = config["DefaultTaskSettings"]!["CaptureSettings"]!;
        var bounds = canvas.RectangleToScreen(canvas.ClientRectangle);
        capture["CaptureCustomRegion"] = $"{bounds.X}, {bounds.Y}, {bounds.Width}, {bounds.Height}";
        capture["ScreenRecordFixedDuration"] = true; capture["ScreenRecordDuration"] = 3.0; capture["ScreenRecordAutoStart"] = true;
        config["DefaultTaskSettings"]!["UploadSettings"]!["NameFormatPattern"] = marker + "_%y-%mo-%d_%h-%mi-%s";
        config["DefaultTaskSettings"]!["UploadSettings"]!["NameFormatPatternActiveWindow"] = marker + "_%y-%mo-%d_%h-%mi-%s";
        File.WriteAllText(configPath, config.ToJsonString());
        void Run(string command) { using var process = (System.Diagnostics.Process)toolkit.GetMethod("Run", Static)!.Invoke(null, [command, settings, null])!; }
        try
        {
            foreach (var mode in new[] { ("ScreenRecorderCustomRegion", ".mp4"), ("ScreenRecorderGIFCustomRegion", ".gif") })
            {
                Run(mode.Item1);
                var deadline = DateTime.UtcNow.AddSeconds(45); string result = null;
                while (DateTime.UtcNow < deadline)
                {
                    Application.DoEvents(); Thread.Sleep(50);
                    result = Directory.EnumerateFiles(incoming, marker + "*" + mode.Item2, SearchOption.AllDirectories).FirstOrDefault(path => new FileInfo(path).Length > 1024 && (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalMilliseconds > 800);
                    if (result != null) { try { using var file = new FileStream(result, FileMode.Open, FileAccess.Read, FileShare.None); break; } catch (IOException) { result = null; } }
                }
                if (result == null) throw new TimeoutException(mode.Item1 + " did not produce an unlocked recording.");
                Console.WriteLine($"PASS: {mode.Item1} produced {new FileInfo(result).Length:N0} bytes; private engine running={Running()}.");
                if (mode.Item2 == ".gif") { using var image = Image.FromFile(result); if (image.Width != bounds.Width || image.Height != bounds.Height) throw new Exception("GIF region dimensions failed."); }
            }
        }
        finally
        {
            if (Running()) Run("ExitShareX");
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (Running() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(100); }
            if (!Running()) File.WriteAllText(configPath, original);
            else Console.WriteLine("WARNING: Toolkit did not exit; capture defaults were not restored while it was running.");
            foreach (string path in Directory.EnumerateFiles(incoming, marker + "*", SearchOption.AllDirectories)) { try { File.Delete(path); } catch (IOException) { } }
            canvas.Close();
        }
    }
}
