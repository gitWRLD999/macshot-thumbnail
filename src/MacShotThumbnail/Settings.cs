using System.Text.Json;
using Microsoft.Win32;

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
    internal static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacShotThumbnail");
    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public static Settings Load()
    {
        try
        {
            var value = File.Exists(FilePath) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new() : new Settings();
            value.Width = Math.Clamp(value.Width, 160, 480);
            value.Seconds = Math.Clamp(value.Seconds, 0, 60);
            if (!ShortcutRules.Valid(value.AreaShortcut)) value.AreaShortcut = Keys.PrintScreen;
            if (!ShortcutRules.Valid(value.FullShortcut) || value.FullShortcut == value.AreaShortcut)
                value.FullShortcut = value.AreaShortcut == (Keys.Control | Keys.PrintScreen) ? Keys.Alt | Keys.PrintScreen : Keys.Control | Keys.PrintScreen;
            if (!Corners.Contains(value.Corner)) value.Corner = Corners[0];
            if (string.IsNullOrWhiteSpace(value.Folder)) value.Folder = new Settings().Folder;
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

internal sealed class SettingsForm : Form
{
    public SettingsForm(Settings current, Action<Settings> apply)
    {
        SuspendLayout();
        Text = "MacShot settings";
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(550, 430);
        MinimumSize = new Size(570, 470);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 9 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(grid);
        var enabled = new CheckBox { Text = "Enabled", Checked = current.Enabled, AutoSize = true };
        var startup = new CheckBox { Text = "Start with Windows", Checked = Settings.StartupEnabled, AutoSize = true };
        var displays = new CheckBox { Text = "Show on every display", Checked = current.AllDisplays, AutoSize = true };
        var clipboard = new CheckBox { Text = "Copy image to clipboard", Checked = current.CopyToClipboard, AutoSize = true };
        var corner = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        corner.Items.AddRange(Settings.Corners); corner.SelectedItem = current.Corner;
        var width = new NumericUpDown { Minimum = 160, Maximum = 480, Increment = 20, Value = current.Width, Dock = DockStyle.Fill };
        var seconds = new NumericUpDown { Minimum = 0, Maximum = 60, Value = current.Seconds, Dock = DockStyle.Fill };
        var folder = new TextBox { Text = current.Folder, Dock = DockStyle.Fill };
        TextBox Shortcut(Keys value)
        {
            var box = new TextBox { ReadOnly = true, Tag = value, Text = new KeysConverter().ConvertToString(value), Dock = DockStyle.Fill };
            box.KeyDown += (_, e) =>
            {
                e.SuppressKeyPress = true;
                if (!ShortcutRules.Valid(e.KeyData)) return;
                box.Tag = e.KeyData;
                box.Text = new KeysConverter().ConvertToString(e.KeyData);
            };
            return box;
        }
        var areaShortcut = Shortcut(current.AreaShortcut);
        var fullShortcut = Shortcut(current.FullShortcut);
        void Row(int row, string label, Control control)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            grid.Controls.Add(control, 1, row);
        }
        Row(0, "Screenshot shortcuts", enabled);
        Row(1, "Startup", startup);
        Row(2, "Displays", displays);
        Row(3, "Clipboard", clipboard);
        Row(4, "Position", corner);
        Row(5, "Preview width (pixels)", width);
        Row(6, "Dismiss seconds (0 = never)", seconds);
        Row(7, "Screenshot folder", folder);
        Row(8, "Area capture shortcut", areaShortcut);
        Row(9, "Full-screen shortcut", fullShortcut);
        ClientSize = new Size(550, 510);
        grid.RowCount = 11;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        grid.Controls.Add(actions, 0, 10); grid.SetColumnSpan(actions, 2);
        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        cancel.Click += (_, _) => Close();
        var browse = new Button { Text = "Browse folder", AutoSize = true };
        actions.Controls.AddRange([save, cancel, browse]);
        browse.Click += (_, _) => { using var dialog = new FolderBrowserDialog { SelectedPath = folder.Text }; if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath; };
        save.Click += (_, _) =>
        {
            try
            {
                string destination = Path.GetFullPath(folder.Text.Trim());
                if (Equals(areaShortcut.Tag, fullShortcut.Tag)) throw new InvalidOperationException("Choose different shortcuts for area and full-screen capture.");
                Directory.CreateDirectory(destination);
                var next = new Settings { Enabled = enabled.Checked, AllDisplays = displays.Checked, CopyToClipboard = clipboard.Checked, Width = (int)width.Value, Seconds = (int)seconds.Value, Corner = (string)corner.SelectedItem!, Folder = destination };
                next.AreaShortcut = (Keys)areaShortcut.Tag!;
                next.FullShortcut = (Keys)fullShortcut.Tag!;
                Settings.StartupEnabled = startup.Checked;
                next.Save(); apply(next); Close();
            }
            catch (Exception error) { Settings.Log(error); MessageBox.Show(this, error.Message, "Settings could not be saved", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        AcceptButton = save; CancelButton = cancel;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ResumeLayout(true);
    }
}
