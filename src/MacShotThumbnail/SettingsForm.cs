namespace MacShotThumbnail;

internal sealed class SettingsForm : Form
{
    public SettingsForm(Settings current, Action<Settings> apply) : this(current, apply, null) { }

    public SettingsForm(Settings current, Action<Settings> apply, Action<string>? capture)
    {
        SuspendLayout();
        Text = "MacShot settings";
        Font = new Font("Segoe UI", 9);
        ClientSize = new Size(680, 520);
        MinimumSize = new Size(480, 360);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        var next = current.Copy();
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8), FlowDirection = FlowDirection.RightToLeft };
        Controls.Add(tabs); Controls.Add(footer);
        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true };
        cancel.Click += (_, _) => Close();
        footer.Controls.AddRange([save, cancel]);
        var bind = new List<Action>();
        TableLayoutPanel Page(string title)
        {
            var page = new TabPage(title) { AutoScroll = true, BackColor = SystemColors.Window };
            tabs.TabPages.Add(page);
            var grid = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Padding = new Padding(16), ColumnCount = 2 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            page.Controls.Add(grid);
            return grid;
        }
        void Row(TableLayoutPanel grid, string label, Control control, int height = 36)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(8, 5, 0, 5);
            grid.Controls.Add(control, 1, row);
        }
        void Check(TableLayoutPanel grid, string label, string property)
        {
            var p = typeof(Settings).GetProperty(property)!;
            var control = new CheckBox { Checked = (bool)p.GetValue(next)!, AutoSize = true, AccessibleName = label };
            Row(grid, label, control); bind.Add(() => p.SetValue(next, control.Checked));
        }
        void Number(TableLayoutPanel grid, string label, string property, int min, int max)
        {
            var p = typeof(Settings).GetProperty(property)!;
            var control = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp((int)p.GetValue(next)!, min, max), AccessibleName = label };
            Row(grid, label, control); bind.Add(() => p.SetValue(next, (int)control.Value));
        }
        TextBox TextField(TableLayoutPanel grid, string label, string property)
        {
            var p = typeof(Settings).GetProperty(property)!;
            var control = new TextBox { Text = (string)p.GetValue(next)!, AccessibleName = label };
            Row(grid, label, control); bind.Add(() => p.SetValue(next, control.Text.Trim())); return control;
        }
        void Choice(TableLayoutPanel grid, string label, string property, string[] choices)
        {
            var p = typeof(Settings).GetProperty(property)!;
            var control = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = label };
            control.Items.AddRange(choices); control.SelectedItem = p.GetValue(next);
            Row(grid, label, control); bind.Add(() => p.SetValue(next, control.SelectedItem));
        }
        void ActionButton(TableLayoutPanel grid, string label, string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true }; button.Click += (_, _) => action(); Row(grid, label, button);
        }
        var general = Page("Capture");
        Check(general, "Screenshot shortcuts enabled", nameof(Settings.Enabled));
        var startup = new CheckBox { Checked = Settings.StartupEnabled, AccessibleName = "Start with Windows" };
        Row(general, "Start with Windows", startup);
        Choice(general, "Full-screen shortcut captures", nameof(Settings.FullCaptureMode), ["Monitor", "Combined", "Separate"]);
        Number(general, "Capture delay (seconds)", nameof(Settings.Delay), 0, 30);
        Check(general, "Include mouse pointer", nameof(Settings.IncludeCursor));
        Number(general, "Fixed region width (pixels)", nameof(Settings.FixedWidth), 4, 16000);
        Number(general, "Fixed region height (pixels)", nameof(Settings.FixedHeight), 4, 16000);
        Check(general, "Capture sound", nameof(Settings.CaptureSound));
        foreach (var mode in new[] { "Combined", "Separate" })
            ActionButton(general, CaptureModes.Names[mode], "Capture", () => { if (capture != null) { WindowState = FormWindowState.Minimized; capture(mode); } });
        var banner = Page("Banner");
        Check(banner, "Show banner on area shortcut", nameof(Settings.ShowBanner));
        Check(banner, "Banner on every display", nameof(Settings.BannerAllDisplays));
        Choice(banner, "Banner position", nameof(Settings.BannerPosition), ["Top", "Bottom"]);
        Number(banner, "Dismiss seconds (0 = never)", nameof(Settings.BannerSeconds), 0, 120);
        var modes = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, Height = 180, AccessibleName = "Banner capture modes" };
        foreach (var mode in CaptureModes.Names) modes.Items.Add(mode.Value, next.BannerModes.Contains(mode.Key));
        Row(banner, "Capture buttons", modes, 190);
        bind.Add(() => next.BannerModes = modes.CheckedIndices.Cast<int>().Select(i => CaptureModes.Names.Keys.ElementAt(i)).ToArray());
        ActionButton(banner, "Capture banner", "Show", () => { if (capture != null) { WindowState = FormWindowState.Minimized; capture("Banner"); } });
        var preview = Page("Preview");
        Check(preview, "Show floating thumbnail", nameof(Settings.ShowPreview));
        Check(preview, "Preview on every display", nameof(Settings.AllDisplays));
        Choice(preview, "Position", nameof(Settings.Corner), Settings.Corners);
        Number(preview, "Preview width (pixels)", nameof(Settings.Width), 160, 480);
        Number(preview, "Edge margin (pixels)", nameof(Settings.PreviewMargin), 0, 100);
        Number(preview, "Opacity (%)", nameof(Settings.PreviewOpacity), 25, 100);
        Number(preview, "Dismiss seconds (0 = never)", nameof(Settings.Seconds), 0, 60);
        Number(preview, "Stay after drag (seconds)", nameof(Settings.PostDragSeconds), 1, 60);
        Check(preview, "Pause dismissal while hovered", nameof(Settings.PauseOnHover));
        Check(preview, "Always show action buttons", nameof(Settings.ShowButtonsAlways));
        Check(preview, "Show trash button", nameof(Settings.TrashButton));
        Check(preview, "Show crop button", nameof(Settings.CropButton));
        Check(preview, "Show edit / more button", nameof(Settings.EditButton));
        var files = Page("Files");
        var folder = TextField(files, "Screenshot folder", nameof(Settings.Folder));
        ActionButton(files, "Destination", "Browse...", () => { using var dialog = new FolderBrowserDialog { SelectedPath = folder.Text }; if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath; });
        Choice(files, "Image format", nameof(Settings.ImageFormat), ["PNG", "JPEG", "BMP", "TIFF"]);
        Number(files, "JPEG quality (%)", nameof(Settings.JpegQuality), 1, 100);
        TextField(files, "Filename prefix", nameof(Settings.NamePrefix));
        Check(files, "Copy image to clipboard", nameof(Settings.CopyToClipboard));
        Check(files, "Also copy screenshot files", nameof(Settings.CopyFiles));
        Check(files, "Open editor after capture", nameof(Settings.EditAfterCapture));
        ActionButton(files, "Saved screenshots", "Open folder", () => { Directory.CreateDirectory(folder.Text); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder.Text) { UseShellExecute = true }); });
        var shortcuts = Page("Shortcuts");
        string[] labels = ["Area / banner", "Full-screen", "All displays: combined", "All displays: separate", "Active window", "Show banner"];
        for (int i = 0; i < Settings.ShortcutProperties.Length; i++)
        {
            var property = typeof(Settings).GetProperty(Settings.ShortcutProperties[i])!;
            var control = new TextBox { ReadOnly = true, Tag = property.GetValue(next), AccessibleName = labels[i] };
            control.Text = new KeysConverter().ConvertToString(control.Tag);
            control.KeyDown += (_, e) => { e.SuppressKeyPress = true; if (ShortcutRules.Valid(e.KeyData)) { control.Tag = e.KeyData; control.Text = new KeysConverter().ConvertToString(e.KeyData); } };
            Row(shortcuts, labels[i], control); bind.Add(() => property.SetValue(next, control.Tag));
        }
        var toolChoice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Tool shortcut action" };
        toolChoice.Items.AddRange(AdvancedTools.Catalog.Select(t => t.Name).ToArray());
        var toolKeys = new TextBox { ReadOnly = true, AccessibleName = "Advanced tool shortcut" };
        Row(shortcuts, "Advanced tool", toolChoice); Row(shortcuts, "Tool shortcut", toolKeys);
        toolChoice.SelectedIndexChanged += (_, _) => { var command = AdvancedTools.Catalog[toolChoice.SelectedIndex].Command; toolKeys.Text = next.ToolShortcuts.TryGetValue(command, out Keys keys) ? new KeysConverter().ConvertToString(keys) : "None"; };
        toolKeys.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true;
            string command = AdvancedTools.Catalog[toolChoice.SelectedIndex].Command;
            if (e.KeyData == Keys.Delete || e.KeyData == Keys.Back) { next.ToolShortcuts.Remove(command); toolKeys.Text = "None"; }
            else if (ShortcutRules.Valid(e.KeyData)) { next.ToolShortcuts[command] = e.KeyData; toolKeys.Text = new KeysConverter().ConvertToString(e.KeyData); }
        };
        toolChoice.SelectedIndex = 0;
        ActionButton(shortcuts, "Tool shortcut", "Clear", () => { next.ToolShortcuts.Remove(AdvancedTools.Catalog[toolChoice.SelectedIndex].Command); toolKeys.Text = "None"; });
        var advanced = Page("Advanced");
        Check(advanced, "Enable ShareX tools", nameof(Settings.AdvancedEnabled));
        var status = new Label { Text = AdvancedTools.Installed ? "ShareX 21.0.0 installed" : "ShareX not installed", AutoSize = true };
        Row(advanced, "Advanced toolkit", status);
        var install = new Button { Text = "Install toolkit", AutoSize = true, Enabled = !AdvancedTools.Installed };
        Row(advanced, "Download", install);
        var lifetime = new CancellationTokenSource();
        FormClosed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        install.Click += async (_, _) =>
        {
            install.Enabled = false;
            try { await AdvancedTools.InstallAsync(next, new Progress<string>(s => { if (!IsDisposed) status.Text = s; }), lifetime.Token); status.Text = "ShareX 21.0.0 installed"; }
            catch (OperationCanceledException) { }
            catch (Exception e) { Settings.Log(e); if (!IsDisposed) { status.Text = "Install failed"; MessageBox.Show(this, e.Message, "MacShot"); } }
            finally { if (!IsDisposed) install.Enabled = !AdvancedTools.Installed; }
        };
        Number(advanced, "Video frames per second", nameof(Settings.RecordingFps), 1, 60);
        Number(advanced, "GIF frames per second", nameof(Settings.GifFps), 1, 30);
        Check(advanced, "Pointer in recordings", nameof(Settings.RecordCursor));
        var ffmpeg = TextField(advanced, "FFmpeg executable (optional)", nameof(Settings.FfmpegPath));
        ActionButton(advanced, "Video encoder", "Browse...", () => { using var dialog = new OpenFileDialog { Filter = "FFmpeg|ffmpeg.exe" }; if (dialog.ShowDialog(this) == DialogResult.OK) ffmpeg.Text = dialog.FileName; });
        ActionButton(advanced, "Editor / OCR / recording preferences", "Open toolkit settings", () => { try { using var process = AdvancedTools.Run("OpenMainWindow", next); } catch (Exception e) { MessageBox.Show(this, e.Message, "MacShot"); } });
        ActionButton(advanced, "Apply video defaults on next launch", "Close toolkit", () => { try { if (AdvancedTools.Running) { using var process = AdvancedTools.Run("ExitShareX", next); } } catch (Exception e) { MessageBox.Show(this, e.Message, "MacShot"); } });
        var tools = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, Height = 200, AccessibleName = "Visible advanced tools" };
        foreach (var tool in AdvancedTools.Catalog) tools.Items.Add(tool.Name, next.Tools.Contains(tool.Command));
        Row(advanced, "Visible tools", tools, 210);
        bind.Add(() => next.Tools = tools.CheckedIndices.Cast<int>().Select(i => AdvancedTools.Catalog[i].Command).ToArray());
        save.Click += (_, _) =>
        {
            try
            {
                foreach (var action in bind) action();
                if (Settings.ShortcutProperties.Select(n => (Keys)typeof(Settings).GetProperty(n)!.GetValue(next)!).Concat(next.ToolShortcuts.Values).Distinct().Count() != Settings.ShortcutProperties.Length + next.ToolShortcuts.Count)
                    throw new InvalidOperationException("Choose a different key combination for each shortcut.");
                if (next.NamePrefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidOperationException("The filename prefix contains invalid characters.");
                next.Folder = Path.GetFullPath(next.Folder);
                if (!string.IsNullOrWhiteSpace(next.FfmpegPath) && !File.Exists(next.FfmpegPath)) throw new FileNotFoundException("FFmpeg executable was not found.");
                Directory.CreateDirectory(next.Folder); next.Normalize();
                Settings.StartupEnabled = startup.Checked;
                next.Save(); apply(next); Close();
            }
            catch (Exception e) { Settings.Log(e); MessageBox.Show(this, e.Message, "Settings could not be saved", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        AcceptButton = save; CancelButton = cancel;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Shown += (_, _) => { var area = Screen.FromControl(this).WorkingArea; Size = new Size(Math.Min(Width, area.Width - 40), Math.Min(Height, area.Height - 40)); Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2); };
        ResumeLayout(true);
    }
}
