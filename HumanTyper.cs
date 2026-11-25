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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// HumanTyper - paste text, press a global hotkey, and it is typed into whatever
// text box has focus, with human-like timing (variable speed, pauses, optional typos).
public class MainForm : Form
{
    // ---- Win32 ----
    const int WM_HOTKEY = 0x0312;
    const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
    const int ID_START = 1, ID_ESC = 2;
    const uint KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION u; }

    static void Send(ushort vk, ushort scan, uint flags)
    {
        INPUT[] a = new INPUT[1];
        a[0].type = 1;
        a[0].u.ki.wVk = vk; a[0].u.ki.wScan = scan; a[0].u.ki.dwFlags = flags;
        SendInput(1, a, Marshal.SizeOf(typeof(INPUT)));
    }
    static void Key(ushort vk) { Send(vk, 0, 0); Send(vk, 0, KEYEVENTF_KEYUP); }
    static void Uni(char c) { Send(0, c, KEYEVENTF_UNICODE); Send(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP); }
    static void TypeChar(char c)
    {
        if (c == '\n') Key(0x0D);
        else if (c == '\t') Key(0x09);
        else Uni(c);
    }
    static bool Down(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

    // ---- State ----
    uint hkMods = MOD_CONTROL | MOD_ALT;
    uint hkVk = (uint)Keys.T;
    volatile bool typing, cancel, waiting;
    bool exiting, balloonShown;
    readonly Random rng = new Random();

    // ---- UI ----
    Label lblHint, lblWpm, lblStatus;
    TextBox txt, txtHotkey;
    Button btnPaste, btnClear, btnStart;
    TrackBar tbWpm;
    CheckBox chkTypos, chkClip, chkChunk, chkWait;
    NumericUpDown numDelay, numChunk, numPause;
    NotifyIcon tray;
    ToolStripMenuItem miStop;

    static string SettingsPath
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HumanTyper", "settings.ini"); }
    }

    static string HotkeyText(uint mods, uint vk)
    {
        string s = "";
        if ((mods & MOD_CONTROL) != 0) s += "Ctrl+";
        if ((mods & MOD_ALT) != 0) s += "Alt+";
        if ((mods & MOD_SHIFT) != 0) s += "Shift+";
        if ((mods & MOD_WIN) != 0) s += "Win+";
        return s + ((Keys)vk).ToString();
    }
    string HK { get { return HotkeyText(hkMods, hkVk); } }

    static Icon MakeIcon()
    {
        Bitmap bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
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

    public MainForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Human Typer";
        ClientSize = new Size(580, 640);
        MinimumSize = new Size(520, 560);
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = MakeIcon();

        lblHint = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 44, Padding = new Padding(10, 10, 10, 2) };

        txt = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, AcceptsTab = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 10.5f) };
        Panel txtHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 6) };
        txtHost.Controls.Add(txt);

        Panel opts = new Panel { Dock = DockStyle.Bottom, Height = 270 };

        // Speed
        Label l1 = new Label { Text = "Speed:", Left = 12, Top = 14, AutoSize = true };
        tbWpm = new TrackBar { Left = 70, Top = 6, Width = 320, Minimum = 20, Maximum = 200, Value = 70, TickFrequency = 20, SmallChange = 5, LargeChange = 10 };
        lblWpm = new Label { Left = 400, Top = 14, AutoSize = true, Text = "70 WPM" };
        tbWpm.ValueChanged += delegate { lblWpm.Text = tbWpm.Value + " WPM"; };

        chkTypos = new CheckBox { Text = "Make occasional typos and fix them", Left = 12, Top = 50, AutoSize = true, Checked = true };
        chkClip = new CheckBox { Text = "Hotkey types whatever is on the clipboard (ignore box above)", Left = 12, Top = 74, AutoSize = true };

        // Hotkey
        Label l3 = new Label { Text = "Hotkey:", Left = 12, Top = 104, AutoSize = true };
        txtHotkey = new TextBox { Left = 70, Top = 100, Width = 170, ReadOnly = true, BackColor = SystemColors.Window, TextAlign = HorizontalAlignment.Center };
        Label l3b = new Label { Text = "click the box, then press the new combination", Left = 250, Top = 104, AutoSize = true, ForeColor = SystemColors.GrayText };
        txtHotkey.Enter += delegate { txtHotkey.Text = "Press keys..."; };
        txtHotkey.Leave += delegate { txtHotkey.Text = HK; };
        txtHotkey.KeyDown += HotkeyKeyDown;

        // Chunking
        chkChunk = new CheckBox { Text = "Type about", Left = 12, Top = 134, AutoSize = true };
        numChunk = new NumericUpDown { Left = 105, Top = 131, Width = 70, Minimum = 5, Maximum = 5000, Value = 200 };
        Label l4 = new Label { Text = "characters (±20%), then pause, and repeat", Left = 182, Top = 135, AutoSize = true };
        Label l5 = new Label { Text = "Pause for", Left = 32, Top = 166, AutoSize = true };
        numPause = new NumericUpDown { Left = 105, Top = 163, Width = 70, Minimum = 1, Maximum = 600, Value = 5 };
        Label l6 = new Label { Text = "seconds", Left = 182, Top = 167, AutoSize = true };
        chkWait = new CheckBox { Text = "...or wait until I press the hotkey to resume", Left = 32, Top = 192, AutoSize = true };

        // Start delay + buttons
        Label l2 = new Label { Text = "Start delay for button (sec):", Left = 12, Top = 224, AutoSize = true };
        numDelay = new NumericUpDown { Left = 200, Top = 221, Width = 55, Minimum = 1, Maximum = 30, Value = 4 };
        btnPaste = new Button { Text = "Paste clipboard", Left = 290, Top = 216, Width = 120, Height = 30 };
        btnClear = new Button { Text = "Clear", Left = 416, Top = 216, Width = 60, Height = 30 };

        // Bottom bar: start button + status
        Panel bar = new Panel { Dock = DockStyle.Bottom, Height = 44 };
        btnStart = new Button { Text = "Start typing", Left = 12, Top = 7, Width = 140, Height = 30 };
        lblStatus = new Label { Left = 164, Top = 14, Width = 400, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        bar.Controls.Add(btnStart);
        bar.Controls.Add(lblStatus);

        btnPaste.Click += delegate { if (Clipboard.ContainsText()) txt.Text = Clipboard.GetText(); };
        btnClear.Click += delegate { txt.Clear(); };
        btnStart.Click += delegate
        {
            if (typing) { cancel = true; return; }
            Begin((int)numDelay.Value * 1000, txt.Text);
        };

        opts.Controls.AddRange(new Control[] { l1, tbWpm, lblWpm, chkTypos, chkClip, l3, txtHotkey, l3b,
            chkChunk, numChunk, l4, l5, numPause, l6, chkWait, l2, numDelay, btnPaste, btnClear });

        Controls.Add(txtHost);
        Controls.Add(opts);
        Controls.Add(bar);
        Controls.Add(lblHint);

        // Tray icon
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Show window", null, delegate { ShowWindow(); });
        menu.Items.Add("Type clipboard now (1.5 s delay)", null, delegate
        {
            if (typing) return;
            string t = ""; try { t = Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch { }
            Begin(1500, t);
        });
        miStop = new ToolStripMenuItem("Stop typing", null, delegate { cancel = true; });
        menu.Items.Add(miStop);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { exiting = true; Close(); });
        tray = new NotifyIcon { Icon = Icon, Text = "Human Typer", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += delegate { ShowWindow(); };

        LoadSettings();
        txtHotkey.Text = HK;
        RefreshHints();
        SetStatus("Ready.");
    }

    void RefreshHints()
    {
        lblHint.Text = "Paste or type your text below. Click into the target text box in any app, then press " + HK + " (Esc stops).";
        FitHint();
    }

    // Size the hint label to its wrapped text so nothing is clipped at any window width or DPI.
    void FitHint()
    {
        if (lblHint == null || lblHint.Text.Length == 0) return;
        int w = Math.Max(50, ClientSize.Width - lblHint.Padding.Horizontal);
        Size s = TextRenderer.MeasureText(lblHint.Text, lblHint.Font, new Size(w, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        lblHint.Height = s.Height + lblHint.Padding.Vertical + 8;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitHint();
    }

    void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    // ---- Settings ----
    static int GetInt(Dictionary<string, string> d, string key, int def)
    {
        string v; int n;
        return d.TryGetValue(key, out v) && int.TryParse(v, out n) ? n : def;
    }
    static decimal Clamp(decimal v, decimal min, decimal max) { return Math.Max(min, Math.Min(max, v)); }

    void LoadSettings()
    {
        Dictionary<string, string> d = new Dictionary<string, string>();
        try
        {
            foreach (string line in File.ReadAllLines(SettingsPath))
            {
                int p = line.IndexOf('=');
                if (p > 0) d[line.Substring(0, p)] = line.Substring(p + 1);
            }
        }
        catch { }
        hkMods = (uint)GetInt(d, "mods", (int)hkMods);
        hkVk = (uint)GetInt(d, "vk", (int)hkVk);
        tbWpm.Value = (int)Clamp(GetInt(d, "wpm", 70), tbWpm.Minimum, tbWpm.Maximum);
        chkTypos.Checked = GetInt(d, "typos", 1) != 0;
        chkClip.Checked = GetInt(d, "clip", 0) != 0;
        chkChunk.Checked = GetInt(d, "chunk", 0) != 0;
        numChunk.Value = Clamp(GetInt(d, "chunkN", 200), numChunk.Minimum, numChunk.Maximum);
        numPause.Value = Clamp(GetInt(d, "pauseSec", 5), numPause.Minimum, numPause.Maximum);
        chkWait.Checked = GetInt(d, "waitKey", 0) != 0;
        numDelay.Value = Clamp(GetInt(d, "delay", 4), numDelay.Minimum, numDelay.Maximum);
    }

    void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllLines(SettingsPath, new string[] {
                "mods=" + hkMods, "vk=" + hkVk, "wpm=" + tbWpm.Value,
                "typos=" + (chkTypos.Checked ? 1 : 0), "clip=" + (chkClip.Checked ? 1 : 0),
                "chunk=" + (chkChunk.Checked ? 1 : 0), "chunkN=" + (int)numChunk.Value,
                "pauseSec=" + (int)numPause.Value, "waitKey=" + (chkWait.Checked ? 1 : 0),
                "delay=" + (int)numDelay.Value });
        }
        catch { }
    }

    // ---- Hotkey handling ----
    bool RegisterStart()
    {
        UnregisterHotKey(Handle, ID_START);
        return RegisterHotKey(Handle, ID_START, hkMods | MOD_NOREPEAT, hkVk);
    }

    void HotkeyKeyDown(object sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;
        Keys k = e.KeyCode;
        if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin) return;
        if (k == Keys.Escape) { txtHotkey.Text = HK; ActiveControl = null; return; }   // Esc is reserved for "stop"

        uint mods = 0;
        if (e.Control) mods |= MOD_CONTROL;
        if (e.Alt) mods |= MOD_ALT;
        if (e.Shift) mods |= MOD_SHIFT;
        if (Down(0x5B) || Down(0x5C)) mods |= MOD_WIN;
        bool fKey = k >= Keys.F1 && k <= Keys.F24;
        if (mods == 0 && !fKey) { SetStatus("Use at least one modifier (Ctrl / Alt / Shift / Win) or an F-key."); return; }

        uint oldMods = hkMods, oldVk = hkVk;
        hkMods = mods; hkVk = (uint)k;
        if (RegisterStart())
        {
            SetStatus("Hotkey set to " + HK + ".");
            RefreshHints();
            SaveSettings();
        }
        else
        {
            hkMods = oldMods; hkVk = oldVk;
            RegisterStart();
            SetStatus(HotkeyText(mods, (uint)k) + " is in use by another app. Kept " + HK + ".");
        }
        txtHotkey.Text = HK;
        ActiveControl = null;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RegisterStart())
            SetStatus(HK + " is in use by another app. Pick a different hotkey below.");
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        FitHint();
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
        SaveSettings();
        UnregisterHotKey(Handle, ID_START);
        UnregisterHotKey(Handle, ID_ESC);
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == ID_ESC) cancel = true;
            else if (id == ID_START)
            {
                if (typing) { if (waiting) waiting = false; else cancel = true; }
                else
                {
                    string t = txt.Text;
                    if (chkClip.Checked) { try { t = Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch { t = ""; } }
                    Begin(300, t);
                }
            }
        }
        base.WndProc(ref m);
    }

    void SetStatus(string s)
    {
        if (InvokeRequired) { try { BeginInvoke(new Action<string>(SetStatus), s); } catch { } return; }
        lblStatus.Text = s;
        if (tray != null) tray.Text = ("Human Typer - " + s).Substring(0, Math.Min(63, ("Human Typer - " + s).Length));
    }

    // ---- Typing ----
    void Begin(int delayMs, string text)
    {
        if (string.IsNullOrEmpty(text)) { SetStatus("Nothing to type."); return; }
        SaveSettings();
        typing = true; cancel = false; waiting = false;
        RegisterHotKey(Handle, ID_ESC, 0, (uint)Keys.Escape);
        btnStart.Text = "Stop";
        int wpm = tbWpm.Value;
        bool typos = chkTypos.Checked;
        int chunkN = chkChunk.Checked ? (int)numChunk.Value : 0;
        int pauseSec = (int)numPause.Value;
        bool waitKey = chkWait.Checked;
        Thread th = new Thread(delegate() { Run(text, delayMs, wpm, typos, chunkN, pauseSec, waitKey); });
        th.IsBackground = true;
        th.Start();
    }

    // Sleep in small slices so Stop reacts quickly.
    void Pause(double ms)
    {
        int left = (int)ms;
        while (left > 0 && !cancel) { int s = Math.Min(left, 30); Thread.Sleep(s); left -= s; }
    }

    void WaitModifiersUp()
    {
        for (int k = 0; k < 60 && !cancel && (Down(0x11) || Down(0x12) || Down(0x10) || Down(0x5B) || Down(0x5C)); k++) Thread.Sleep(50);
    }

    double Delay(double baseMs, double speed)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        double d = baseMs / speed * (1.0 + 0.3 * z);
        return Math.Max(baseMs * 0.35, d);
    }

    int NextBreak(int n) { return (int)(n * (0.8 + 0.4 * rng.NextDouble())); }

    static readonly string[] Rows = { "qwertyuiop", "asdfghjkl", "zxcvbnm" };
    char Neighbor(char c)
    {
        char lo = char.ToLower(c);
        for (int r = 0; r < Rows.Length; r++)
        {
            int x = Rows[r].IndexOf(lo);
            if (x < 0) continue;
            string cand = "";
            if (x > 0) cand += Rows[r][x - 1];
            if (x < Rows[r].Length - 1) cand += Rows[r][x + 1];
            if (r > 0 && x < Rows[r - 1].Length) cand += Rows[r - 1][x];
            if (r < Rows.Length - 1 && x < Rows[r + 1].Length) cand += Rows[r + 1][x];
            char w = cand[rng.Next(cand.Length)];
            return char.IsUpper(c) ? char.ToUpper(w) : w;
        }
        return c;
    }

    void Run(string text, int delayMs, int wpm, bool typos, int chunkN, int pauseSec, bool waitKey)
    {
        try
        {
            SetStatus("Starting... click into the target text box.");
            Pause(delayMs);
            WaitModifiersUp();

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            double baseMs = 60000.0 / (wpm * 5.0);
            double speed = 1.0;
            int i = 0, sinceBreak = 0, nextBreak = chunkN > 0 ? NextBreak(chunkN) : 0;

            while (i < text.Length && !cancel)
            {
                char c = text[i];
                speed += (rng.NextDouble() - 0.5) * 0.1;
                speed = Math.Max(0.7, Math.Min(1.35, speed));

                if (typos && c < 128 && char.IsLetter(c) && rng.NextDouble() < 0.025)
                {
                    char w = Neighbor(c);
                    if (w != c)
                    {
                        TypeChar(w);
                        Pause(Delay(baseMs, speed));
                        int extra = 0, maxExtra = rng.Next(0, 3);
                        while (extra < maxExtra && i + extra + 1 < text.Length && text[i + extra + 1] != '\n')
                        {
                            extra++;
                            TypeChar(text[i + extra]);
                            Pause(Delay(baseMs, speed));
                        }
                        Pause(rng.Next(180, 450));   // "notice" the mistake
                        for (int b = 0; b <= extra; b++) { Key(0x08); Pause(rng.Next(50, 110)); }
                        Pause(rng.Next(80, 200));
                        if (cancel) break;
                    }
                }

                TypeChar(c);
                i++; sinceBreak++;

                double d = Delay(baseMs, speed);
                if (c == ' ') d *= 1.2;
                else if (".!?".IndexOf(c) >= 0 && rng.NextDouble() < 0.6) d += rng.Next(200, 500);
                else if (",;:".IndexOf(c) >= 0 && rng.NextDouble() < 0.4) d += rng.Next(80, 250);
                else if (c == '\n') d += rng.Next(250, 700);
                if (rng.NextDouble() < 0.012) d += rng.Next(400, 1300);   // brief "thinking" pause
                Pause(d);

                if (i % 10 == 0) SetStatus("Typing... " + i + " / " + text.Length);

                // "Type N characters, then pause": break at a word boundary once the target is reached
                if (chunkN > 0 && !cancel && i < text.Length && sinceBreak >= nextBreak && (c == ' ' || c == '\n' || sinceBreak >= nextBreak + 15))
                {
                    sinceBreak = 0;
                    nextBreak = NextBreak(chunkN);
                    if (waitKey)
                    {
                        waiting = true;
                        SetStatus("Paused at " + i + " / " + text.Length + ". Press " + HK + " to resume (Esc stops).");
                        while (waiting && !cancel) Thread.Sleep(50);
                        waiting = false;
                        if (cancel) break;
                        WaitModifiersUp();
                        Pause(400);
                    }
                    else
                    {
                        double secs = pauseSec * (0.8 + 0.4 * rng.NextDouble());
                        for (double left = secs; left > 0 && !cancel; left -= 1)
                        {
                            SetStatus("Paused at " + i + " / " + text.Length + ". Resuming in " + (int)Math.Ceiling(left) + " s...");
                            Pause(Math.Min(1000, left * 1000));
                        }
                    }
                }
            }
            SetStatus(cancel ? "Stopped at " + i + " / " + text.Length + "." : "Done. Typed " + text.Length + " characters.");
        }
        finally
        {
            typing = false; waiting = false;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    UnregisterHotKey(Handle, ID_ESC);
                    btnStart.Text = "Start typing";
                }));
            }
            catch { }
        }
    }

    [STAThread]
    static void Main()
    {
        try { SetProcessDPIAware(); } catch { }   // crisp text on scaled displays
        Application.EnableVisualStyles();
        Application.Run(new MainForm());
    }
}
