// Human Typer - types text into any text box with human-like timing.
// Copyright (C) 2026 Human Typer contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

// Main window: chrome (header, tabs, footer), theme, tray, hotkey routing, settings.
// The pages and the typing engine live in the other partial files.
public partial class MainForm : Form
{
    public const string Version = "1.3.0";
    const string RepoReleases = "https://github.com/Aitzaz-Ali/HumanTyper/releases";
    const string RepoLatestApi = "https://api.github.com/repos/Aitzaz-Ali/HumanTyper/releases/latest";

    const int ID_START = 1, ID_ESC = 2, ID_PAUSE = 3, ID_SNIP = 1000;

    // ---- State ----
    volatile bool typing, cancel, waiting, userPaused;
    bool exiting, balloonShown, loading = true, startHidden, firstShow = true;
    int themeMode;   // 0 = follow Windows, 1 = light, 2 = dark
    readonly Random rng = new Random();

    // ---- Chrome ----
    Font fUI, fUIBold, fMono, fMonoBold, fTitle;
    Label lblStatus, lblChip;
    RoundedPanel chip;
    TabBar tabs;
    Panel[] pages;
    RoundButton btnStart;
    NotifyIcon tray;
    ContextMenuStrip menuStrip;
    ToolStripMenuItem miPause;
    readonly List<Action> relayouts = new List<Action>();

    string HK { get { return HotkeyBox.Describe(hkStartBox.Mods, hkStartBox.Vk); } }
    string PauseHK { get { return HotkeyBox.Describe(hkPauseBox.Mods, hkPauseBox.Vk); } }

    static Icon MakeIcon()
    {
        Bitmap bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (Brush b = new SolidBrush(Color.FromArgb(37, 99, 235))) g.FillEllipse(b, 1, 1, 30, 30);
            using (Font f = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("T", f, Brushes.White, new RectangleF(0, 0, 32, 33), sf);
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    // ---- small builders shared by the pages ----
    Label Mk(string text, int x, int y, bool bold, bool muted)
    {
        Label l = new Label { Text = text, Left = x, Top = y, AutoSize = true, Font = bold ? fUIBold : fUI };
        l.Tag = muted ? "muted" : null;
        return l;
    }

    Toggle MkToggle(string text, int x, int y, int w)
    {
        return new Toggle { Text = text, Left = x, Top = y, Width = w, Font = fUI, ForeColor = Theme.Ink };
    }

    NumericUpDown MkNum(int x, int y, int w, int min, int max, int val)
    {
        return new NumericUpDown { Left = x, Top = y, Width = w, Minimum = min, Maximum = max, Value = val, Font = fUI, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
    }

    RoundButton MkSecondary(string text, int w)
    {
        return new RoundButton { Text = text, Width = w, Font = fUI, Radius = 8 };   // colors applied by ApplyTheme
    }

    // A rounded card that fills a padded host panel.
    RoundedPanel MkCardPage(out Panel page, int padTop)
    {
        page = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, padTop, 16, 12), Tag = "bg" };
        RoundedPanel card = new RoundedPanel { Dock = DockStyle.Top };
        page.Controls.Add(card);
        return card;
    }

    // Slider with a value label on the right; the slider stretches with the card.
    void FitSlider(RoundedPanel card, Label leftLabel, FlatSlider s, Label val)
    {
        relayouts.Add(delegate
        {
            int pad = leftLabel.Left;
            val.Left = card.ClientSize.Width - pad - val.Width;
            s.Width = Math.Max(80, val.Left - s.Left - pad / 2);
        });
        card.Resize += delegate { Relayout(); };
    }

    void Relayout() { foreach (Action a in relayouts) a(); }

    public MainForm()
    {
        Store.Load();
        themeMode = Math.Max(0, Math.Min(2, Store.Int("theme", 0)));
        startHidden = Store.Bool("minimized", false);

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Human Typer";
        ClientSize = new Size(640, 800);
        MinimumSize = new Size(600, 740);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = MakeIcon();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;

        string ui = Theme.PickFont("Segoe UI Variable Text", "Segoe UI");
        string display = Theme.PickFont("Segoe UI Variable Display", "Segoe UI");
        string mono = Theme.PickFont("Cascadia Mono", "Cascadia Code", "Consolas");
        fUI = new Font(ui, 9.75f);
        fUIBold = new Font(ui, 9.75f, FontStyle.Bold);
        fMono = new Font(mono, 10.5f);
        fMonoBold = new Font(mono, 10f, FontStyle.Bold);
        fTitle = new Font(display, 16f, FontStyle.Bold);
        Font = fUI;

        // ---------- Header ----------
        Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.White };
        Label lblTitle = new Label { Text = "Human Typer", Left = 20, Top = 10, AutoSize = true, Font = fTitle };
        Label lblSub = new Label { Text = "Paste text, click into any text box, then press the hotkey.", Left = 22, Top = 42, AutoSize = true, Font = fUI, Tag = "muted" };
        Panel chipHost = new Panel { Dock = DockStyle.Right, Width = 210, Padding = new Padding(0, 18, 20, 18), BackColor = Color.White };
        chip = new RoundedPanel { Dock = DockStyle.Fill, Radius = 8 };
        lblChip = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = fMonoBold, BackColor = Color.Transparent, Tag = "chip" };
        chip.Controls.Add(lblChip);
        chipHost.Controls.Add(chip);
        header.Controls.Add(lblTitle);
        header.Controls.Add(lblSub);
        header.Controls.Add(chipHost);

        // ---------- Tabs + pages ----------
        tabs = new TabBar { Dock = DockStyle.Top, Font = fUI, BoldFont = fUIBold, Items = new string[] { "Text", "Snippets", "Typing", "App" } };
        Panel pagesHost = new Panel { Dock = DockStyle.Fill, Tag = "bg" };
        pages = new Panel[] { BuildTextPage(), BuildSnippetsPage(), BuildTypingPage(), BuildAppPage() };
        foreach (Panel p in pages) { p.Dock = DockStyle.Fill; pagesHost.Controls.Add(p); }
        tabs.SelectedChanged += delegate { ShowPage(tabs.SelectedIndex); };
        ShowPage(0);

        // ---------- Footer ----------
        Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(16, 13, 16, 13) };
        footer.Paint += delegate(object s, PaintEventArgs e)
        {
            using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0);
        };
        lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0), AutoEllipsis = true, Font = fUI, Tag = "muted" };
        btnStart = new RoundButton { Text = "Start typing", Dock = DockStyle.Left, Width = 160, Font = fUIBold, Radius = 10 };
        footer.Controls.Add(lblStatus);
        footer.Controls.Add(btnStart);

        Controls.Add(pagesHost);
        Controls.Add(footer);
        Controls.Add(tabs);
        Controls.Add(header);

        btnStart.Click += delegate
        {
            if (typing) { cancel = true; return; }
            Begin(txt.Text, (int)numDelay.Value * 1000, true);
        };

        // ---------- Tray ----------
        ContextMenuStrip menu = new ContextMenuStrip();
        menuStrip = menu;
        menu.Font = fUI;
        menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors()) { RoundedEdges = false };
        menu.Items.Add("Show window", null, delegate { ShowWindow(); });
        menu.Items.Add("Type clipboard now (1.5 s delay)", null, delegate
        {
            if (typing) return;
            string t = ""; try { t = Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch { }
            Begin(t, 1500, false);
        });
        miPause = new ToolStripMenuItem("Pause / resume typing", null, delegate { TogglePause(); });
        menu.Items.Add(miPause);
        menu.Items.Add("Stop typing", null, delegate { cancel = true; });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { exiting = true; Close(); });
        tray = new NotifyIcon { Icon = Icon, Text = "Human Typer", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += delegate { ShowWindow(); };

        SystemEvents.UserPreferenceChanged += OnSystemPrefChanged;

        LoadSettings();
        loading = false;
        ApplyTheme();
        UpdateChunkEnabled();
        RefreshHints();
        UpdateCount();
        Relayout();
        SetStatus("Ready. Esc stops typing at any time.");
    }

    void ShowPage(int i)
    {
        for (int k = 0; k < pages.Length; k++) pages[k].Visible = (k == i);
        pages[i].BringToFront();
        if (!loading) Skin(pages[i]);   // controls on a page get their native handles (scrollbars, spin buttons) only once it is shown
    }

    void RefreshHints() { lblChip.Text = HK; }

    void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    // ---- Theme ----
    static bool SystemUsesDark()
    {
        try
        {
            object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            return v is int && (int)v == 0;
        }
        catch { return false; }
    }

    void OnSystemPrefChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (themeMode != 0 || IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(ApplyTheme)); } catch { }
    }

    void SkinNative(Control c)
    {
        if (!c.IsHandleCreated) return;
        Native.SetWindowTheme(c.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer", null);
    }

    void Skin(Control parent)
    {
        foreach (Control k in parent.Controls)
        {
            string tag = k.Tag as string;
            RoundedPanel rp = k as RoundedPanel;
            if (rp != null)
            {
                bool isChip = (rp == chip);
                rp.Fill = isChip ? Theme.ChipFill : Theme.Card;
                rp.BorderColor = isChip ? Theme.ChipBorder : Theme.Border;
                rp.BackColor = rp.Fill;
                rp.Invalidate();
            }
            else if (k is Panel)
            {
                k.BackColor = tag == "bg" ? Theme.Bg : Theme.Card;
            }
            else if (k is Label)
            {
                k.ForeColor = tag == "muted" ? Theme.Muted : tag == "faint" ? Theme.Faint : tag == "accent" ? Theme.Accent : tag == "chip" ? Theme.ChipInk : tag == "danger" ? Theme.Danger : Theme.Ink;
                if (k == lblPlaceholder) k.BackColor = Theme.Card;
            }
            else if (k is Toggle || k is SegmentedControl)
            {
                k.ForeColor = Theme.Ink;
            }
            else if (k is ThemedList)
            {
                k.BackColor = Theme.Card; k.ForeColor = Theme.Ink;
                SkinNative(k);
            }
            else if (k is TextBox)
            {
                k.ForeColor = Theme.Ink;
                k.BackColor = (k is HotkeyBox) ? Theme.InputAlt : Theme.Card;
                SkinNative(k);
            }
            else if (k is NumericUpDown)
            {
                k.ForeColor = Theme.Ink;
                k.BackColor = Theme.Input;
                SkinNative(k);
                foreach (Control inner in k.Controls) { inner.BackColor = Theme.Input; inner.ForeColor = Theme.Ink; SkinNative(inner); }
            }
            else if (k is RoundButton && k != btnStart)
            {
                RoundButton b = (RoundButton)k;
                b.Fill = Theme.BtnFill; b.FillHover = Theme.BtnHover; b.FillDown = Theme.BtnDown; b.Fg = Theme.Ink; b.Edge = Theme.BtnEdge;
                b.Invalidate();
            }
            k.Invalidate();
            Skin(k);
        }
    }

    void ApplyTheme()
    {
        Theme.Apply(themeMode == 2 || (themeMode == 0 && SystemUsesDark()));
        SuspendLayout();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        Skin(this);
        SetTypingUi(typing);
        btnTheme.Text = "Theme: " + (themeMode == 0 ? "Auto" : themeMode == 1 ? "Light" : "Dark");
        if (menuStrip != null) { menuStrip.BackColor = Theme.Card; menuStrip.ForeColor = Theme.Ink; }
        if (IsHandleCreated)
        {
            int v = Theme.Dark ? 1 : 0;
            Native.DwmSetWindowAttribute(Handle, 20, ref v, 4);   // dark title bar (Windows 10 20H1+ / 11)
        }
        ResumeLayout(true);
        Invalidate(true);
    }

    void SetTypingUi(bool on)
    {
        btnStart.Text = on ? "Stop" : "Start typing";
        btnStart.Fill = on ? Theme.Danger : Theme.Accent;
        btnStart.FillHover = on ? Theme.DangerHover : Theme.AccentHover;
        btnStart.FillDown = on ? Theme.DangerDown : Theme.AccentDown;
        btnStart.Fg = Color.White;
        btnStart.Invalidate();
    }

    // ---- Settings ----
    void LoadSettings()
    {
        hkStartBox.SetKey((uint)Store.Int("mods", (int)(Native.MOD_CONTROL | Native.MOD_ALT)), (uint)Store.Int("vk", (int)Keys.T));
        hkPauseBox.SetKey((uint)Store.Int("pmods", (int)(Native.MOD_CONTROL | Native.MOD_ALT)), (uint)Store.Int("pvk", (int)Keys.P));
        tbWpm.Value = Store.Int("wpm", 70);
        chkClip.Checked = Store.Bool("clip", false);
        segNewline.SelectedIndex = Math.Max(0, Math.Min(2, Store.Int("newline", 0)));
        chkStrip.Checked = Store.Bool("strip", false);
        chkGuard.Checked = Store.Bool("guard", true);
        slTypo.Value = Store.Int("typoLevel", 2);
        slPause.Value = Store.Int("pauseLevel", 2);
        chkChunk.Checked = Store.Bool("chunk", false);
        numChunk.Value = Math.Max(numChunk.Minimum, Math.Min(numChunk.Maximum, Store.Int("chunkN", 200)));
        numPause.Value = Math.Max(numPause.Minimum, Math.Min(numPause.Maximum, Store.Int("pauseSec", 5)));
        chkWait.Checked = Store.Bool("waitKey", false);
        numDelay.Value = Math.Max(numDelay.Minimum, Math.Min(numDelay.Maximum, Store.Int("delay", 4)));
        chkBadge.Checked = Store.Bool("badge", true);
        chkNotify.Checked = Store.Bool("notify", true);
        chkMin.Checked = Store.Bool("minimized", false);
        chkStartup.Checked = IsStartupEnabled();
    }

    void SaveSettings()
    {
        if (loading) return;
        Store.Set("mods", (int)hkStartBox.Mods); Store.Set("vk", (int)hkStartBox.Vk);
        Store.Set("pmods", (int)hkPauseBox.Mods); Store.Set("pvk", (int)hkPauseBox.Vk);
        Store.Set("theme", themeMode);
        Store.Set("wpm", tbWpm.Value);
        Store.Set("clip", chkClip.Checked);
        Store.Set("newline", segNewline.SelectedIndex);
        Store.Set("strip", chkStrip.Checked);
        Store.Set("guard", chkGuard.Checked);
        Store.Set("typoLevel", slTypo.Value);
        Store.Set("pauseLevel", slPause.Value);
        Store.Set("chunk", chkChunk.Checked);
        Store.Set("chunkN", (int)numChunk.Value);
        Store.Set("pauseSec", (int)numPause.Value);
        Store.Set("waitKey", chkWait.Checked);
        Store.Set("delay", (int)numDelay.Value);
        Store.Set("badge", chkBadge.Checked);
        Store.Set("notify", chkNotify.Checked);
        Store.Set("minimized", chkMin.Checked);
        Store.Save();
    }

    // ---- Hotkeys ----
    bool RegisterId(int id, uint mods, uint vk)
    {
        Native.UnregisterHotKey(Handle, id);
        return vk == 0 || Native.RegisterHotKey(Handle, id, mods | Native.MOD_NOREPEAT, vk);
    }

    // Returns the name of whatever already uses this combination, or null.
    string HotkeyClash(uint m, uint v, object except)
    {
        if (except != hkStartBox && hkStartBox.Mods == m && hkStartBox.Vk == v) return "start / stop";
        if (except != hkPauseBox && hkPauseBox.Mods == m && hkPauseBox.Vk == v) return "pause / resume";
        return SnippetHotkeyClash(m, v, except);
    }

    // Used by the two global hotkey boxes: checks for clashes and tries to register the new combination.
    Func<uint, uint, string> GlobalValidator(HotkeyBox box, int id)
    {
        return delegate(uint m, uint v)
        {
            string clash = HotkeyClash(m, v, box);
            if (clash != null) return HotkeyBox.Describe(m, v) + " is already used for " + clash + ".";
            Native.UnregisterHotKey(Handle, id);
            if (Native.RegisterHotKey(Handle, id, m | Native.MOD_NOREPEAT, v)) return null;
            Native.RegisterHotKey(Handle, id, box.Mods | Native.MOD_NOREPEAT, box.Vk);   // put the old one back
            return HotkeyBox.Describe(m, v) + " is in use by another app.";
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Theme.Dark) { int v = 1; Native.DwmSetWindowAttribute(Handle, 20, ref v, 4); }
        string problem = null;
        if (!RegisterId(ID_START, hkStartBox.Mods, hkStartBox.Vk)) problem = HK + " (start / stop) is in use by another app.";
        if (!RegisterId(ID_PAUSE, hkPauseBox.Mods, hkPauseBox.Vk)) problem = PauseHK + " (pause) is in use by another app.";
        RegisterSnippetHotkeys();
        if (problem != null) SetStatus(problem + " Pick another on the App tab.");
    }

    // ---- Window events ----
    protected override void SetVisibleCore(bool value)
    {
        if (firstShow)
        {
            firstShow = false;
            if (startHidden)
            {
                if (!IsHandleCreated) CreateHandle();   // register hotkeys etc. without showing the window
                base.SetVisibleCore(false);
                return;
            }
        }
        base.SetVisibleCore(value);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible || loading) return;
        ApplyTheme();   // child handles exist now, so scrollbars / spin buttons get styled too
        Relayout();
        if (ActiveControl == null) ActiveControl = txt;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (tray != null && WindowState == FormWindowState.Minimized) Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!balloonShown)
            {
                balloonShown = true;
                tray.ShowBalloonTip(2500, "Human Typer is still running", "Hotkey: " + HK + ". Right-click the tray icon to exit.", ToolTipIcon.Info);
            }
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        cancel = true;
        SystemEvents.UserPreferenceChanged -= OnSystemPrefChanged;
        SaveSettings();
        SaveSnippets();
        Native.UnregisterHotKey(Handle, ID_START);
        Native.UnregisterHotKey(Handle, ID_PAUSE);
        Native.UnregisterHotKey(Handle, ID_ESC);
        UnregisterSnippetHotkeys();
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == ID_ESC) cancel = true;
            else if (id == ID_PAUSE) TogglePause();
            else if (id == ID_START)
            {
                if (typing) { if (waiting) waiting = false; else cancel = true; }
                else
                {
                    string t = txt.Text;
                    if (chkClip.Checked) { try { t = Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch { t = ""; } }
                    Begin(t, 300, true);
                }
            }
            else if (id >= ID_SNIP) TypeSnippetByHotkey(id - ID_SNIP);
        }
        base.WndProc(ref m);
    }

    void SetStatus(string s)
    {
        if (InvokeRequired) { try { BeginInvoke(new Action<string>(SetStatus), s); } catch { } return; }
        lblStatus.Text = s;
        if (tray != null)
        {
            string full = "Human Typer - " + s;
            tray.Text = full.Substring(0, Math.Min(63, full.Length));
        }
    }

    [STAThread]
    static void Main()
    {
        try { Native.SetProcessDPIAware(); } catch { }   // crisp text on scaled displays
        Application.EnableVisualStyles();
        Application.Run(new MainForm());
    }
}
