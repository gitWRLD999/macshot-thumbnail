using System.Text.Json;

namespace MacShotThumbnail;

internal sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public bool AllDisplays { get; set; } = true;
    public bool CopyToClipboard { get; set; } = true;
    public int Width { get; set; } = 260;
    public int Seconds { get; set; } = 8;
    public Keys AreaShortcut { get; set; } = Keys.PrintScreen;
    public Keys FullShortcut { get; set; } = Keys.Control | Keys.PrintScreen;
    public string Corner { get; set; } = "Bottom right";
    public string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
    public bool ShowBanner { get; set; }
    public bool BannerAllDisplays { get; set; }
    public string BannerPosition { get; set; } = "Top";
    public int BannerSeconds { get; set; } = 20;
    public string FullCaptureMode { get; set; } = "Monitor";
    public string[] BannerModes { get; set; } = ["Area", "Monitor", "Combined", "Separate", "Window", "LastArea"];
    public string[] Tools { get; set; } = AdvancedTools.Catalog.Select(t => t.Command).ToArray();
    public Dictionary<string, Keys> ToolShortcuts { get; set; } = new();
    public int Delay { get; set; }
    public bool IncludeCursor { get; set; }
    public int FixedWidth { get; set; } = 800;
    public int FixedHeight { get; set; } = 600;
    public bool ShowPreview { get; set; } = true;
    public bool PauseOnHover { get; set; } = true;
    public bool ShowButtonsAlways { get; set; }
    public bool CropButton { get; set; } = true;
    public bool TrashButton { get; set; } = true;
    public bool EditButton { get; set; } = true;
    public int PostDragSeconds { get; set; } = 8;
    public int PreviewOpacity { get; set; } = 100;
    public int PreviewMargin { get; set; } = 16;
    public string ImageFormat { get; set; } = "PNG";
    public int JpegQuality { get; set; } = 90;
    public string NamePrefix { get; set; } = "Screenshot";
    public bool CopyFiles { get; set; }
    public bool CaptureSound { get; set; }
    public bool EditAfterCapture { get; set; }
    public Keys CombinedShortcut { get; set; } = Keys.Control | Keys.Shift | Keys.PrintScreen;
    public Keys SeparateShortcut { get; set; } = Keys.Alt | Keys.Shift | Keys.PrintScreen;
    public Keys WindowShortcut { get; set; } = Keys.Alt | Keys.PrintScreen;
    public Keys BannerShortcut { get; set; } = Keys.Shift | Keys.PrintScreen;
    public bool AdvancedEnabled { get; set; } = true;
    public int RecordingFps { get; set; } = 30;
    public int GifFps { get; set; } = 15;
    public bool RecordCursor { get; set; } = true;
    public string FfmpegPath { get; set; } = "";
    public Settings Copy() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
    public void Normalize()
    {
        Width = Math.Clamp(Width, 160, 480); Seconds = Math.Clamp(Seconds, 0, 60);
        Delay = Math.Clamp(Delay, 0, 30); BannerSeconds = Math.Clamp(BannerSeconds, 0, 120);
        FixedWidth = Math.Clamp(FixedWidth, 4, 16000); FixedHeight = Math.Clamp(FixedHeight, 4, 16000);
        PostDragSeconds = Math.Clamp(PostDragSeconds, 1, 60); PreviewMargin = Math.Clamp(PreviewMargin, 0, 100);
        PreviewOpacity = Math.Clamp(PreviewOpacity, 25, 100); JpegQuality = Math.Clamp(JpegQuality, 1, 100);
        RecordingFps = Math.Clamp(RecordingFps, 1, 60); GifFps = Math.Clamp(GifFps, 1, 30);
        if (!Corners.Contains(Corner)) Corner = Corners[0];
        if (!new[] { "PNG", "JPEG", "BMP", "TIFF" }.Contains(ImageFormat)) ImageFormat = "PNG";
        if (!new[] { "Monitor", "Combined", "Separate" }.Contains(FullCaptureMode)) FullCaptureMode = "Monitor";
        if (BannerPosition != "Bottom") BannerPosition = "Top";
        BannerModes = (BannerModes ?? []).Where(CaptureModes.Names.ContainsKey).Distinct().ToArray();
        Tools = (Tools ?? []).Where(c => AdvancedTools.Catalog.Any(t => t.Command == c)).Distinct().ToArray();
        if (string.IsNullOrWhiteSpace(Folder)) Folder = new Settings().Folder;
        if (string.IsNullOrWhiteSpace(NamePrefix) || NamePrefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) NamePrefix = "Screenshot";
        var defaults = new Settings();
        var used = new HashSet<Keys>();
        foreach (string name in ShortcutProperties)
        {
            var property = typeof(Settings).GetProperty(name)!;
            Keys keys = (Keys)property.GetValue(this)!;
            if (!ShortcutRules.Valid(keys) || !used.Add(keys))
            {
                keys = (Keys)property.GetValue(defaults)!;
                if (!used.Add(keys)) keys = Enumerable.Range((int)Keys.F13, 12).Select(v => (Keys)v).First(used.Add);
                property.SetValue(this, keys);
            }
        }
        ToolShortcuts = (ToolShortcuts ?? new()).Where(pair => AdvancedTools.Catalog.Any(t => t.Command == pair.Key) && ShortcutRules.Valid(pair.Value) && used.Add(pair.Value)).ToDictionary(pair => pair.Key, pair => pair.Value);
    }
    internal static readonly string[] ShortcutProperties = [nameof(AreaShortcut), nameof(FullShortcut), nameof(CombinedShortcut), nameof(SeparateShortcut), nameof(WindowShortcut), nameof(BannerShortcut)];
    internal static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacShotThumbnail");
    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public static Settings Load()
    {
        try
        {
            var value = File.Exists(FilePath) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new() : new Settings();
            value.Normalize();
            return value;
        }
        catch (Exception error) { Log(error); return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        string temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
    public static void Log(Exception error)
        => LogEvent(error.ToString());

    public static void LogEvent(string message)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, "errors.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:u} {message}\n");
        }
        catch { /* Logging must not prevent recovery. */ }
    }
    public static readonly string[] Corners = ["Bottom right", "Bottom left", "Top right", "Top left"];
    public static bool StartupEnabled
    {
        get => StartupRegistration.Enabled;
        set => StartupRegistration.Enabled = value;
    }
}
