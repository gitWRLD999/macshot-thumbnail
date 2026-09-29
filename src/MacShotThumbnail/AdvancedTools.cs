using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MacShotThumbnail;

internal static class AdvancedTools
{
    internal sealed record Tool(string Command, string Name, bool AcceptsFile = false);
    internal static readonly Tool[] Catalog =
    [
        new("RectangleRegion", "Advanced region / freehand capture"), new("ScrollingCapture", "Scrolling capture"), new("AutoCapture", "Automatic / interval capture"),
        new("ScreenRecorder", "Record video"), new("ScreenRecorderActiveWindow", "Record active window"), new("ScreenRecorderCustomRegion", "Record configured region"),
        new("ScreenRecorderGIF", "Record GIF"), new("ScreenRecorderGIFActiveWindow", "Record active window as GIF"), new("ScreenRecorderGIFCustomRegion", "Record configured region as GIF"),
        new("StopScreenRecording", "Stop recording"), new("PauseScreenRecording", "Pause / resume recording"), new("AbortScreenRecording", "Abort recording"),
        new("ImageEditor", "Annotate / blur / redact / resize", true), new("OCR", "Extract text (OCR)", true), new("PinToScreenFromFile", "Pin image to screen", true),
        new("ImageBeautifier", "Beautify / background / shadow", true), new("ImageEffects", "Image effects / watermark", true), new("ImageViewer", "View image", true),
        new("ScreenColorPicker", "Screen color picker"), new("ColorPicker", "Color palette"), new("Ruler", "Screen ruler"),
        new("QRCode", "Read / create QR code", true), new("QRCodeScanRegion", "Scan QR from screen"),
        new("ImageCombiner", "Combine images"), new("ImageSplitter", "Split image"), new("ImageComparer", "Compare images"), new("ImageThumbnailer", "Resize / thumbnails"),
        new("VideoConverter", "Convert / trim video", true), new("VideoThumbnailer", "Video thumbnails"), new("BackgroundRemover", "Remove background (model download)"),
        new("PinToScreenCloseAll", "Close pinned images"), new("ClipboardViewer", "Clipboard viewer"), new("OpenImageHistory", "Advanced image history"), new("OpenHistory", "Advanced capture / recording history")
    ];
    internal static string Root => Path.Combine(Settings.DirectoryPath, "Tools", "ShareX");
    internal static string Executable => Path.Combine(Root, "ShareX.exe");
    internal static string Personal => Path.Combine(Root, "ShareX");
    internal static string Incoming => Path.Combine(Settings.DirectoryPath, "AdvancedCaptures");
    internal static bool Installed => File.Exists(Executable);
    internal static bool Running => Process.GetProcessesByName("ShareX").Any(p => { using (p) { try { return string.Equals(p.MainModule?.FileName, Executable, StringComparison.OrdinalIgnoreCase); } catch { return false; } } });
    private const string Download = "https://github.com/ShareX/ShareX/releases/download/v21.0.0/ShareX-21.0.0-portable-x64.zip";
    private const string Sha256 = "8124938fa718d702bb08ef29292f0140052bdad03d23b3041b5015054ff1c230";
    internal static async Task InstallAsync(Settings settings, IProgress<string>? progress, CancellationToken cancellation)
    {
        if (Installed) return;
        Directory.CreateDirectory(Settings.DirectoryPath);
        string zip = Path.Combine(Settings.DirectoryPath, "sharex-" + Guid.NewGuid().ToString("N") + ".zip");
        string staging = Path.Combine(Settings.DirectoryPath, "sharex-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MacShotThumbnail/0.4");
            using var response = await client.GetAsync(Download, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var output = new FileStream(zip, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920]; long received = 0; int count; long last = 0;
                while ((count = await input.ReadAsync(buffer, cancellation)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation); received += count;
                    if (received - last > 1_000_000) { progress?.Report($"Downloading: {received / 1_000_000} MB"); last = received; }
                }
            }
            progress?.Report("Verifying download...");
            await using (var file = File.OpenRead(zip))
            {
                string hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellation));
                if (!hash.Equals(Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ShareX download checksum did not match. Nothing was installed.");
            }
            progress?.Report("Installing portable toolkit...");
            await Task.Run(() => { cancellation.ThrowIfCancellationRequested(); ZipFile.ExtractToDirectory(zip, staging); }, cancellation);
            if (!File.Exists(Path.Combine(staging, "ShareX.exe"))) throw new InvalidDataException("Toolkit executable is missing.");
            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(Root)!);
            if (Directory.Exists(Root)) throw new IOException("An incomplete toolkit directory exists. Rename it before retrying.");
            Directory.Move(staging, Root);
            Prepare(settings);
            progress?.Report("ShareX 21.0.0 installed");
        }
        finally
        {
            if (File.Exists(zip)) File.Delete(zip);
            // Only remove the uniquely named staging directory owned by this download.
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }
    internal static void Prepare(Settings settings)
    {
        if (!Installed || Running) return;
        Directory.CreateDirectory(Personal); Directory.CreateDirectory(Incoming);
        string configPath = Path.Combine(Personal, "ApplicationConfig.json");
        var config = File.Exists(configPath) ? JsonNode.Parse(File.ReadAllText(configPath))!.AsObject() : new JsonObject();
        config["DisableHotkeys"] = true; config["DisableUpload"] = true; config["AutoCheckUpdate"] = false;
        config["ShowTray"] = true; config["FirstTimeMinimizeToTray"] = false;
        config["UseCustomScreenshotsPath"] = true; config["CustomScreenshotsPath"] = Incoming;
        config["SaveImageSubFolderPattern"] = ""; config["SaveImageSubFolderPatternWindow"] = "";
        var task = Object(config, "DefaultTaskSettings");
        task["AfterCaptureJob"] = "CopyImageToClipboard, SaveImageToFile"; task["AfterUploadJob"] = "None";
        var general = Object(task, "GeneralSettings");
        general["ShowToastNotificationAfterTaskCompleted"] = false;
        general["PlaySoundAfterCapture"] = settings.CaptureSound; general["PlaySoundAfterUpload"] = false;
        Object(task, "ImageSettings")["ImageAutoUseJPEG"] = false;
        var capture = Object(task, "CaptureSettings");
        capture["ShowCursor"] = settings.IncludeCursor; capture["ScreenshotDelay"] = settings.Delay;
        capture["ScreenRecordFPS"] = settings.RecordingFps; capture["GIFFPS"] = settings.GifFps; capture["ScreenRecordShowCursor"] = settings.RecordCursor;
        if (!string.IsNullOrWhiteSpace(settings.FfmpegPath))
        {
            var encoder = Object(capture, "FFmpegOptions"); encoder["OverrideCLIPath"] = true; encoder["CLIPath"] = settings.FfmpegPath;
        }
        var tools = Object(task, "ToolsSettings"); tools["ShowImageEditorSelector"] = false;
        Atomic(configPath, config);
        // This private portable copy must never register competing screenshot shortcuts.
        Atomic(Path.Combine(Personal, "HotkeysConfig.json"), new JsonObject { ["Hotkeys"] = new JsonArray() });
    }
    private static JsonObject Object(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing) return existing;
        var value = new JsonObject(); parent[key] = value; return value;
    }
    private static void Atomic(string path, JsonObject value)
    {
        File.WriteAllText(path + ".tmp", value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
    internal static Process Run(string command, Settings settings, string? file = null)
    {
        if (!Installed) throw new InvalidOperationException("Install the toolkit in MacShot settings > Advanced first.");
        if (command != "OpenMainWindow" && command != "ExitShareX" && !Catalog.Any(t => t.Command == command)) throw new ArgumentOutOfRangeException(nameof(command));
        Prepare(settings);
        var info = new ProcessStartInfo(Executable) { UseShellExecute = false, WorkingDirectory = Root };
        info.ArgumentList.Add("-portable"); info.ArgumentList.Add("-silent"); info.ArgumentList.Add("-" + command);
        if (file != null) info.ArgumentList.Add(Path.GetFullPath(file));
        return Process.Start(info) ?? throw new InvalidOperationException("Could not start the advanced toolkit.");
    }
}
