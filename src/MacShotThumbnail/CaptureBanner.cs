namespace MacShotThumbnail;

internal sealed class CaptureBanner : Form
{
    private readonly ToolTip tips = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 200 };
    private DateTime expires;
    public CaptureBanner(Settings settings, Rectangle area, Action<string> capture, Action openSettings, Action<string> tool)
    {
        SuspendLayout();
        Text = "MacShot capture banner";
        Font = new Font("Segoe UI", 9);
        BackColor = Color.FromArgb(28, 29, 31); ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; KeyPreview = true;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(7) };
        Controls.Add(bar);
        void Icon(string glyph, string label, Action click, bool selected = false)
        {
            var button = new Button { Text = glyph, AccessibleName = label, Width = 40, Height = 36, Margin = new Padding(2), FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe Fluent Icons", 14), BackColor = selected ? Color.FromArgb(46, 109, 99) : BackColor, ForeColor = Color.White };
            button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => click(); tips.SetToolTip(button, label); bar.Controls.Add(button);
        }
        foreach (string mode in settings.BannerModes) Icon(CaptureModes.Icons[mode], CaptureModes.Names[mode], () => capture(mode), mode == "Area");
        var delay = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 112, AccessibleName = "Capture delay", Margin = new Padding(8, 7, 8, 4) };
        int[] delays = [0, 3, 5, 10, 30];
        foreach (int value in delays) delay.Items.Add(value == 0 ? "No delay" : value + " sec");
        if (!delays.Contains(settings.Delay)) { delay.Items.Add(settings.Delay + " sec"); delays = [..delays, settings.Delay]; }
        delay.SelectedIndex = Array.IndexOf(delays, settings.Delay);
        delay.SelectedIndexChanged += (_, _) => { settings.Delay = delays[delay.SelectedIndex]; settings.Save(); };
        bar.Controls.Add(delay);
        if (settings.AdvancedEnabled)
        {
            Icon("\uE714", "Advanced tools", () =>
            {
                var menu = new ContextMenuStrip();
                foreach (var item in AdvancedTools.Catalog.Where(t => settings.Tools.Contains(t.Command)))
                    menu.Items.Add(item.Name, null, (_, _) => tool(item.Command));
                menu.Closed += (_, _) => menu.Dispose(); menu.Show(Cursor.Position);
            });
        }
        Icon("\uE713", "Settings", openSettings);
        Icon("\uE711", "Dismiss", Close);
        int desiredWidth = bar.Controls.Cast<Control>().Sum(c => c.Width + c.Margin.Horizontal) + bar.Padding.Horizontal;
        Width = Math.Min(desiredWidth, area.Width - 24);
        Height = desiredWidth > Width ? 98 : 54;
        Location = new Point(area.Left + (area.Width - Width) / 2, settings.BannerPosition == "Bottom" ? area.Bottom - Height - 16 : area.Top + 16);
        expires = DateTime.UtcNow.AddSeconds(settings.BannerSeconds);
        timer.Tick += (_, _) => { if (Bounds.Contains(Cursor.Position)) expires = DateTime.UtcNow.AddSeconds(settings.BannerSeconds); else if (settings.BannerSeconds > 0 && DateTime.UtcNow > expires) Close(); };
        timer.Start(); KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        FormClosed += (_, _) => { timer.Dispose(); tips.Dispose(); };
        AutoScaleMode = AutoScaleMode.None;
        ResumeLayout(true);
    }
}
