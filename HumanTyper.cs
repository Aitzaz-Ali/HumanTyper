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
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// ===================== Custom flat controls =====================

class MenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
    public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
    public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
    public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
    public override Color MenuBorder { get { return Theme.Border; } }
    public override Color MenuItemBorder { get { return Theme.BtnHover; } }
    public override Color MenuItemSelected { get { return Theme.BtnHover; } }
    public override Color MenuItemSelectedGradientBegin { get { return Theme.BtnHover; } }
    public override Color MenuItemSelectedGradientEnd { get { return Theme.BtnHover; } }
    public override Color SeparatorDark { get { return Theme.Border; } }
    public override Color SeparatorLight { get { return Theme.Card; } }
}

static class Theme
{
    public static bool Dark;
    public static Color Bg, Card, Border, Ink, Muted, Faint, Accent, AccentHover, AccentDown, AccentDisabled,
        Danger, DangerHover, DangerDown, Input, InputAlt, ChipFill, ChipBorder, ChipInk,
        BtnFill, BtnHover, BtnDown, BtnEdge, ToggleOff, ToggleOffDisabled, Track;

    static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

    static Theme() { Apply(false); }

    public static void Apply(bool dark)
    {
        Dark = dark;
        if (!dark)
        {
            Bg = C(243, 244, 246); Card = Color.White; Border = C(229, 231, 235);
            Ink = C(17, 24, 39); Muted = C(107, 114, 128); Faint = C(156, 163, 175);
            Accent = C(37, 99, 235); AccentHover = C(29, 78, 216); AccentDown = C(30, 64, 175); AccentDisabled = C(147, 179, 245);
            Danger = C(220, 38, 38); DangerHover = C(185, 28, 28); DangerDown = C(153, 27, 27);
            Input = Color.White; InputAlt = C(249, 250, 251);
            ChipFill = C(239, 244, 255); ChipBorder = C(199, 215, 254); ChipInk = C(37, 99, 235);
            BtnFill = Color.White; BtnHover = C(243, 244, 246); BtnDown = C(229, 231, 235); BtnEdge = C(209, 213, 219);
            ToggleOff = C(209, 213, 219); ToggleOffDisabled = C(229, 231, 235); Track = C(229, 231, 235);
        }
        else
        {
            Bg = C(15, 17, 21); Card = C(26, 29, 35); Border = C(45, 50, 58);
            Ink = C(229, 231, 235); Muted = C(156, 163, 175); Faint = C(107, 114, 128);
            Accent = C(59, 130, 246); AccentHover = C(96, 165, 250); AccentDown = C(37, 99, 235); AccentDisabled = C(30, 58, 110);
            Danger = C(239, 68, 68); DangerHover = C(248, 113, 113); DangerDown = C(220, 38, 38);
            Input = C(31, 35, 43); InputAlt = C(22, 25, 31);
            ChipFill = C(30, 41, 70); ChipBorder = C(51, 65, 115); ChipInk = C(147, 197, 253);
            BtnFill = C(31, 35, 43); BtnHover = C(41, 46, 56); BtnDown = C(52, 58, 69); BtnEdge = C(63, 70, 82);
            ToggleOff = C(75, 85, 99); ToggleOffDisabled = C(45, 50, 58); Track = C(55, 65, 81);
        }
    }

    public static string PickFont(params string[] names)
    {
        using (InstalledFontCollection c = new InstalledFontCollection())
        {
            foreach (string n in names)
                foreach (FontFamily f in c.Families)
                    if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return n;
        }
        return names[names.Length - 1];
    }

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = Math.Max(1f, radius * 2f);
        GraphicsPath p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

class RoundedPanel : Panel
{
    public int Radius = 12;
    public Color Fill = Theme.Card;
    public Color BorderColor = Theme.Border;
    public int[] Dividers = new int[0];   // y positions (96-dpi px) of divider lines

    public RoundedPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Fill;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = g.DpiX / 96f;
        RectangleF rc = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (GraphicsPath p = Theme.RoundRect(rc, Radius * s))
        using (Brush b = new SolidBrush(Fill))
        using (Pen pen = new Pen(BorderColor))
        {
            g.FillPath(b, p);
            g.DrawPath(pen, p);
        }
        g.SmoothingMode = SmoothingMode.None;
        using (Pen dp = new Pen(BorderColor))
            foreach (int y in Dividers)
                g.DrawLine(dp, 20 * s, y * s, Width - 20 * s, y * s);
    }
}

class RoundButton : Button
{
    public Color Fill, FillHover, FillDown, Fg, Edge = Color.Empty;
    public int Radius = 9;
    bool hover, down;

    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = g.DpiX / 96f;
        Color f = !Enabled ? Theme.Border : (down ? FillDown : (hover ? FillHover : Fill));
        RectangleF rc = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (GraphicsPath p = Theme.RoundRect(rc, Radius * s))
        using (Brush b = new SolidBrush(f))
        {
            g.FillPath(b, p);
            if (Edge != Color.Empty) using (Pen pen = new Pen(Edge)) g.DrawPath(pen, p);
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? Fg : Theme.Faint,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

class Toggle : Control
{
    bool chk;
    public event EventHandler CheckedChanged;

    public Toggle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = 26;
    }

    public bool Checked
    {
        get { return chk; }
        set
        {
            if (chk == value) return;
            chk = value;
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e) { base.OnClick(e); if (Enabled) { Focus(); Checked = !Checked; } }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Space && Enabled) { Checked = !Checked; e.Handled = true; } }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = g.DpiX / 96f;
        float pw = 38 * s, ph = 20 * s, y = (Height - ph) / 2f;
        RectangleF pill = new RectangleF(1 * s + 1, y, pw, ph);
        Color on = Enabled ? Theme.Accent : Theme.AccentDisabled;
        Color off = Enabled ? Theme.ToggleOff : Theme.ToggleOffDisabled;
        using (GraphicsPath p = Theme.RoundRect(pill, ph / 2f))
        using (Brush b = new SolidBrush(chk ? on : off))
            g.FillPath(b, p);
        float d = ph - 6 * s;
        float kx = chk ? pill.Right - 3 * s - d : pill.X + 3 * s;
        g.FillEllipse(Brushes.White, kx, y + 3 * s, d, d);
        if (Focused && ShowFocusCues)
        {
            RectangleF fr = RectangleF.Inflate(pill, 2.5f * s, 2.5f * s);
            using (GraphicsPath p = Theme.RoundRect(fr, fr.Height / 2f))
            using (Pen pen = new Pen(Color.FromArgb(120, Theme.Accent), 1.5f))
                g.DrawPath(pen, p);
        }
        int tx = (int)(pw + 14 * s);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(tx, 0, Math.Max(1, Width - tx), Height),
            Enabled ? ForeColor : Theme.Faint,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}

class FlatSlider : Control
{
    public int Minimum = 0, Maximum = 100;
    int val;
    bool drag;
    public event EventHandler ValueChanged;

    public FlatSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = 28;
    }

    public int Value
    {
        get { return val; }
        set
        {
            int v = Math.Max(Minimum, Math.Min(Maximum, value));
            if (v == val) return;
            val = v;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    float Pad { get { return 11f * DeviceDpi / 96f; } }

    void SetFromX(int x)
    {
        float w = Math.Max(1f, Width - 2 * Pad);
        float f = Math.Max(0f, Math.Min(1f, (x - Pad) / w));
        Value = Minimum + (int)Math.Round(f * (Maximum - Minimum) / 5.0) * 5;
    }

    protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) { Value -= 5; e.Handled = true; }
        else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) { Value += 5; e.Handled = true; }
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); drag = true; SetFromX(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (drag) SetFromX(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = false; }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = g.DpiX / 96f;
        float pad = 11 * s, cy = Height / 2f;
        float x0 = pad, x1 = Width - pad;
        float f = (Maximum == Minimum) ? 0 : (float)(val - Minimum) / (Maximum - Minimum);
        float cx = x0 + f * (x1 - x0);
        using (Pen back = new Pen(Theme.Track, 5 * s))
        using (Pen fill = new Pen(Theme.Accent, 5 * s))
        {
            back.StartCap = back.EndCap = LineCap.Round;
            fill.StartCap = fill.EndCap = LineCap.Round;
            g.DrawLine(back, x0, cy, x1, cy);
            if (cx > x0) g.DrawLine(fill, x0, cy, cx, cy);
        }
        float d = 18 * s;
        RectangleF th = new RectangleF(cx - d / 2, cy - d / 2, d, d);
        using (Brush sh = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            g.FillEllipse(sh, th.X, th.Y + 1.5f * s, d, d);
        g.FillEllipse(Brushes.White, th);
        using (Pen pen = new Pen(Theme.Accent, 2 * s)) g.DrawEllipse(pen, th);
        if (Focused && ShowFocusCues)
            using (Pen pen = new Pen(Color.FromArgb(90, Theme.Accent), 2 * s))
                g.DrawEllipse(pen, th.X - 3 * s, th.Y - 3 * s, d + 6 * s, d + 6 * s);
    }
}

// ===================== Main window =====================

public class MainForm : Form
{
    // ---- Win32 ----
    const int WM_HOTKEY = 0x0312;
    const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
    const int ID_START = 1, ID_ESC = 2;
    const uint KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
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
    Font fUI, fUIBold, fMono, fMonoBold, fTitle;
    Label lblWpm, lblStatus, lblChip, lblCount, lblPlaceholder, lblSpeed;
    TextBox txt, txtHotkey;
    RoundButton btnPaste, btnClear, btnStart, btnTheme;
    RoundedPanel chip;
    ContextMenuStrip menuStrip;
    int themeMode;   // 0 = follow Windows, 1 = light, 2 = dark
    FlatSlider tbWpm;
    Toggle chkTypos, chkClip, chkChunk, chkWait;
    NumericUpDown numDelay, numChunk, numPause;
    RoundedPanel settingsCard;
    List<Control> chunkDependents = new List<Control>();
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (Brush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, 1, 1, 30, 30);
            using (Font f = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("T", f, Brushes.White, new RectangleF(0, 0, 32, 33), sf);
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

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

    public MainForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Human Typer";
        ClientSize = new Size(640, 790);
        MinimumSize = new Size(600, 720);
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
        Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.White, Padding = new Padding(0, 0, 0, 1) };
        header.Paint += delegate(object s, PaintEventArgs e)
        {
            using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, header.Height - 1, header.Width, header.Height - 1);
        };
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

        // ---------- Footer ----------
        Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(16, 13, 16, 13) };
        footer.Paint += delegate(object s, PaintEventArgs e)
        {
            using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0);
        };
        lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0), AutoEllipsis = true, Font = fUI, Tag = "muted" };
        btnStart = new RoundButton { Text = "Start typing", Dock = DockStyle.Left, Width = 160, Font = fUIBold, Radius = 10 };
        btnTheme = MkSecondary("Theme: Auto", 130); btnTheme.Dock = DockStyle.Right;
        footer.Controls.Add(lblStatus);
        footer.Controls.Add(btnTheme);
        footer.Controls.Add(btnStart);

        // ---------- Settings card ----------
        Panel settingsHost = new Panel { Dock = DockStyle.Bottom, Height = 350, Padding = new Padding(16, 4, 16, 12), Tag = "bg" };
        settingsCard = new RoundedPanel { Dock = DockStyle.Fill };
        settingsCard.Dividers = new int[] { 138, 268 };

        lblSpeed = Mk("Typing speed", 20, 21, true, false);
        tbWpm = new FlatSlider { Left = 140, Top = 14, Width = 300, Minimum = 20, Maximum = 200 };
        tbWpm.Value = 70;
        lblWpm = new Label { Text = "70 WPM", Width = 76, Top = 21, Height = 20, TextAlign = ContentAlignment.TopRight, Font = fUIBold, Tag = "accent" };

        chkTypos = MkToggle("Make occasional typos, then fix them", 20, 62, 420);
        chkTypos.Checked = true;
        chkClip = MkToggle("Hotkey types the clipboard instead of the text box", 20, 96, 460);

        chkChunk = MkToggle("Take a break after about", 20, 152, 225);
        numChunk = MkNum(250, 150, 80, 5, 5000, 200);
        Label lChars = Mk("characters", 340, 154, false, true);
        Label lBreak = Mk("Break length", 62, 192, false, true);
        numPause = MkNum(250, 189, 80, 1, 600, 5);
        Label lSecs = Mk("seconds", 340, 192, false, true);
        chkWait = MkToggle("Wait for me to press the hotkey instead", 40, 228, 420);
        chunkDependents.AddRange(new Control[] { numChunk, lChars, lBreak, numPause, lSecs, chkWait });

        Label lHk = Mk("Hotkey", 20, 289, true, false);
        txtHotkey = new TextBox { Left = 84, Top = 285, Width = 150, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(249, 250, 251), TextAlign = HorizontalAlignment.Center, Font = fMonoBold, Cursor = Cursors.Hand };
        Label lDelay = Mk("Button start delay", 262, 289, false, true);
        numDelay = MkNum(392, 286, 56, 1, 30, 4);
        Label lSec2 = Mk("sec", 454, 289, false, true);

        settingsCard.Controls.AddRange(new Control[] { lblSpeed, tbWpm, lblWpm, chkTypos, chkClip, chkChunk, numChunk, lChars,
            lBreak, numPause, lSecs, chkWait, lHk, txtHotkey, lDelay, numDelay, lSec2 });
        settingsHost.Controls.Add(settingsCard);
        settingsCard.Resize += delegate { LayoutCard(); };

        // ---------- Text card ----------
        Panel textHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 12), Tag = "bg" };
        RoundedPanel textCard = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 12, 0) };

        txt = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, AcceptsTab = true, Dock = DockStyle.Fill, Font = fMono, BorderStyle = BorderStyle.None, BackColor = Color.White };
        lblPlaceholder = new Label { Text = "Paste or type the text you want typed...", AutoSize = true, Left = 0, Top = 0, Font = fUI, Tag = "faint", Cursor = Cursors.IBeam };
        txt.Controls.Add(lblPlaceholder);
        lblPlaceholder.Click += delegate { txt.Focus(); };

        Panel strip = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.White, Padding = new Padding(0, 8, 4, 8) };
        lblCount = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = fUI, Tag = "muted" };
        btnClear = MkSecondary("Clear", 70); btnClear.Dock = DockStyle.Left;
        btnPaste = MkSecondary("Paste clipboard", 130); btnPaste.Dock = DockStyle.Left;
        strip.Controls.Add(lblCount);
        strip.Controls.Add(btnClear);
        strip.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8, BackColor = Color.White });
        strip.Controls.Add(btnPaste);

        textCard.Controls.Add(txt);
        textCard.Controls.Add(strip);
        textHost.Controls.Add(textCard);

        Controls.Add(textHost);
        Controls.Add(settingsHost);
        Controls.Add(footer);
        Controls.Add(header);

        // ---------- Events ----------
        txtHotkey.Enter += delegate { txtHotkey.Text = "Press keys..."; };
        txtHotkey.Leave += delegate { txtHotkey.Text = HK; };
        txtHotkey.KeyDown += HotkeyKeyDown;

        tbWpm.ValueChanged += delegate { lblWpm.Text = tbWpm.Value + " WPM"; UpdateCount(); };
        txt.TextChanged += delegate { lblPlaceholder.Visible = txt.TextLength == 0; UpdateCount(); };
        chkChunk.CheckedChanged += delegate { UpdateChunkEnabled(); };

        btnPaste.Click += delegate { try { if (Clipboard.ContainsText()) txt.Text = Clipboard.GetText(); } catch { } };
        btnClear.Click += delegate { txt.Clear(); txt.Focus(); };
        btnStart.Click += delegate
        {
            if (typing) { cancel = true; return; }
            Begin((int)numDelay.Value * 1000, txt.Text);
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
            Begin(1500, t);
        });
        miStop = new ToolStripMenuItem("Stop typing", null, delegate { cancel = true; });
        menu.Items.Add(miStop);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { exiting = true; Close(); });
        tray = new NotifyIcon { Icon = Icon, Text = "Human Typer", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += delegate { ShowWindow(); };

        btnTheme.Click += delegate { themeMode = (themeMode + 1) % 3; SaveSettings(); ApplyTheme(); };
        SystemEvents.UserPreferenceChanged += OnSystemPrefChanged;

        LoadSettings();
        ApplyTheme();
        txtHotkey.Text = HK;
        UpdateChunkEnabled();
        RefreshHints();
        UpdateCount();
        SetStatus("Ready. Esc stops typing at any time.");
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
        SetWindowTheme(c.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer", null);
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
                k.ForeColor = tag == "muted" ? Theme.Muted : tag == "faint" ? Theme.Faint : tag == "accent" ? Theme.Accent : tag == "chip" ? Theme.ChipInk : Theme.Ink;
                if (k == lblPlaceholder) k.BackColor = Theme.Card;
            }
            else if (k is Toggle)
            {
                k.ForeColor = Theme.Ink;
            }
            else if (k is TextBox)
            {
                k.ForeColor = Theme.Ink;
                k.BackColor = (k == txtHotkey) ? Theme.InputAlt : Theme.Card;
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
            DwmSetWindowAttribute(Handle, 20, ref v, 4);   // dark title bar (Windows 10 20H1+ / 11)
        }
        ResumeLayout(true);
        Invalidate(true);
    }

    void LayoutCard()
    {
        if (settingsCard == null || lblSpeed == null) return;
        int pad = lblSpeed.Left;
        lblWpm.Left = settingsCard.ClientSize.Width - pad - lblWpm.Width;
        tbWpm.Width = Math.Max(80, lblWpm.Left - tbWpm.Left - pad / 2);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyTheme();   // now that child handles exist, style scrollbars / spin buttons too
        LayoutCard();
        ActiveControl = txt;
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

    void UpdateChunkEnabled()
    {
        foreach (Control c in chunkDependents)
        {
            if (c is Label)
            {
                // Windows draws disabled labels in an off-palette color, so dim them by hand
                c.Tag = chkChunk.Checked ? "muted" : "faint";
                c.ForeColor = chkChunk.Checked ? Theme.Muted : Theme.Faint;
            }
            else c.Enabled = chkChunk.Checked;
        }
    }

    void UpdateCount()
    {
        if (lblCount == null) return;
        int n = txt.TextLength;
        if (n == 0) { lblCount.Text = "0 characters"; return; }
        double secs = n / 5.0 / Math.Max(1, tbWpm.Value) * 60.0;
        string t = secs < 60 ? ((int)Math.Ceiling(secs)) + " sec" : ((int)(secs / 60)) + " min " + ((int)(secs % 60)) + " sec";
        lblCount.Text = n.ToString("N0") + " characters  ·  about " + t + " at " + tbWpm.Value + " WPM";
    }

    void RefreshHints()
    {
        lblChip.Text = HK;
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
        themeMode = Math.Max(0, Math.Min(2, GetInt(d, "theme", 0)));
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
                "mods=" + hkMods, "vk=" + hkVk, "theme=" + themeMode, "wpm=" + tbWpm.Value,
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
        if (Theme.Dark) { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); }
        if (!RegisterStart())
            SetStatus(HK + " is in use by another app. Pick a different hotkey below.");
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
        if (tray != null)
        {
            string full = "Human Typer - " + s;
            tray.Text = full.Substring(0, Math.Min(63, full.Length));
        }
    }

    // ---- Typing ----
    void Begin(int delayMs, string text)
    {
        if (string.IsNullOrEmpty(text)) { SetStatus("Nothing to type."); return; }
        SaveSettings();
        typing = true; cancel = false; waiting = false;
        RegisterHotKey(Handle, ID_ESC, 0, (uint)Keys.Escape);
        SetTypingUi(true);
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
                    SetTypingUi(false);
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
