using System.Diagnostics;
using Microsoft.VisualBasic.FileIO;

namespace MacShotThumbnail;

internal sealed class HistoryForm : Form
{
    private readonly Settings settings;
    private readonly ListView files = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, HideSelection = false };
    private readonly TextBox search = new() { Dock = DockStyle.Top, PlaceholderText = "Search screenshots", Height = 32 };
    private readonly PictureBox preview = new() { Dock = DockStyle.Right, Width = 260, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 29, 31) };
    private readonly ToolTip tips = new();
    public HistoryForm(Settings settings, Action<string> show, Action<string> crop, Action<string, string?> tool)
    {
        this.settings = settings;
        Text = "MacShot screenshot history"; Font = new Font("Segoe UI", 9);
        ClientSize = new Size(860, 480); MinimumSize = new Size(500, 300); StartPosition = FormStartPosition.CenterScreen;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(4) };
        Controls.Add(files); Controls.Add(preview); Controls.Add(search); Controls.Add(toolbar);
        files.Columns.Add("Filename", 320); files.Columns.Add("Captured", 145); files.Columns.Add("Size", 80);
        string? Selected() => files.SelectedItems.Count == 0 ? null : (string)files.SelectedItems[0].Tag!;
        void Icon(string glyph, string name, Action action)
        {
            var button = new Button { Text = glyph, AccessibleName = name, Font = new Font("Segoe Fluent Icons", 12), Width = 34, Height = 30, FlatStyle = FlatStyle.Flat };
            button.FlatAppearance.BorderSize = 0; tips.SetToolTip(button, name);
            button.Click += (_, _) => { try { action(); } catch (Exception e) { Settings.Log(e); MessageBox.Show(this, e.Message, "MacShot"); } };
            toolbar.Controls.Add(button);
        }
        Icon("\uE72C", "Refresh", RefreshFiles);
        Icon("\uE8A7", "Show floating thumbnail", () => { if (Selected() is string path) show(path); });
        Icon("\uE8C8", "Copy selected files", () => { var list = new System.Collections.Specialized.StringCollection(); list.AddRange(files.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag!).ToArray()); if (list.Count > 0) Clipboard.SetFileDropList(list); });
        Icon("\uE7A8", "Crop", () => { if (Selected() is string path) crop(path); });
        Icon("\uE70F", "Annotate / edit", () => { if (Selected() is string path) tool("ImageEditor", path); });
        Icon("\uE8D2", "Extract text", () => { if (Selected() is string path) tool("OCR", path); });
        Icon("\uE718", "Pin to screen", () => { if (Selected() is string path) tool("PinToScreenFromFile", path); });
        Icon("\uE8B7", "Open folder", () => Process.Start(new ProcessStartInfo(settings.Folder) { UseShellExecute = true }));
        Icon("\uE74D", "Move selected screenshots to Recycle Bin", () =>
        {
            var paths = files.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag!).ToArray();
            foreach (string path in paths) FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            RefreshFiles();
        });
        files.SelectedIndexChanged += (_, _) =>
        {
            preview.Image?.Dispose(); preview.Image = null;
            if (Selected() is string path)
            {
                try { using var image = Image.FromFile(path); preview.Image = new Bitmap(image); }
                catch (Exception e) { Settings.Log(e); }
            }
        };
        files.DoubleClick += (_, _) => { if (Selected() is string path) show(path); };
        search.TextChanged += (_, _) => RefreshFiles();
        FormClosed += (_, _) => { preview.Image?.Dispose(); tips.Dispose(); };
        Shown += (_, _) => { var area = Screen.FromControl(this).WorkingArea; Size = new Size(Math.Min(Width, area.Width - 40), Math.Min(Height, area.Height - 40)); };
        RefreshFiles();
    }
    public void RefreshFiles()
    {
        if (IsDisposed) return;
        files.BeginUpdate(); files.Items.Clear();
        try
        {
            Directory.CreateDirectory(settings.Folder);
            foreach (var file in new DirectoryInfo(settings.Folder).EnumerateFiles().Where(f => new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif" }.Contains(f.Extension.ToLowerInvariant()) && f.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).OrderByDescending(f => f.LastWriteTime).Take(1000))
            {
                var item = new ListViewItem(file.Name) { Tag = file.FullName }; item.SubItems.Add(file.LastWriteTime.ToString("g")); item.SubItems.Add($"{file.Length / 1024:N0} KB"); files.Items.Add(item);
            }
        }
        catch (Exception e) { Settings.Log(e); }
        finally { files.EndUpdate(); }
    }
}
