// SPDX-License-Identifier: GPL-3.0-or-later
// Part of Human Typer. See LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

// ===================== Theme =====================

static class Theme
{
    public static bool Dark;
    public static Color Bg, Card, Border, Ink, Muted, Faint, Accent, AccentHover, AccentDown, AccentDisabled,
        Danger, DangerHover, DangerDown, Input, InputAlt, ChipFill, ChipBorder, ChipInk,
        BtnFill, BtnHover, BtnDown, BtnEdge, ToggleOff, ToggleOffDisabled, Track, Warn;

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
            Warn = C(217, 119, 6);
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
            Warn = C(245, 158, 11);
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

// ===================== Basic flat controls =====================

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
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

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
    public int Minimum = 0, Maximum = 100, Step = 5;
    int val;
    bool drag;
    public event EventHandler ValueChanged;

    public FlatSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = 28;
        val = int.MinValue;
    }

    public int Value
    {
        get { return val == int.MinValue ? Minimum : val; }
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
        Value = Minimum + (int)Math.Round(f * (Maximum - Minimum) / (double)Step) * Step;
    }

    protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) { Value -= Step; e.Handled = true; }
        else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) { Value += Step; e.Handled = true; }
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
        float f = (Maximum == Minimum) ? 0 : (float)(Value - Minimum) / (Maximum - Minimum);
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

// ===================== New: tabs, segmented choice, hotkey box, list =====================

class TabBar : Control
{
    string[] items = new string[0];
    int sel, hot = -1;
    public event EventHandler SelectedChanged;
    public Font BoldFont;

    public TabBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 46;
        Cursor = Cursors.Hand;
    }

    public string[] Items { get { return items; } set { items = value; Invalidate(); } }
    public int SelectedIndex
    {
        get { return sel; }
        set
        {
            if (value == sel || value < 0 || value >= items.Length) return;
            sel = value;
            Invalidate();
            if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
        }
    }

    List<Rectangle> TabRects()
    {
        List<Rectangle> r = new List<Rectangle>();
        float s = DeviceDpi / 96f;
        int x = (int)(12 * s);
        Font f = BoldFont ?? Font;
        foreach (string t in items)
        {
            int w = TextRenderer.MeasureText(t, f).Width + (int)(40 * s);
            r.Add(new Rectangle(x, 0, w, Height));
            x += w;
        }
        return r;
    }

    int HitTest(Point p)
    {
        List<Rectangle> r = TabRects();
        for (int i = 0; i < r.Count; i++) if (r[i].Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e) { int h = HitTest(e.Location); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { hot = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { int h = HitTest(e.Location); if (h >= 0) SelectedIndex = h; base.OnMouseDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Card);
        float s = g.DpiX / 96f;
        using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
        List<Rectangle> r = TabRects();
        for (int i = 0; i < r.Count; i++)
        {
            bool isSel = i == sel;
            Font f = isSel ? (BoldFont ?? Font) : Font;
            Color c = isSel ? Theme.Ink : (i == hot ? Theme.Ink : Theme.Muted);
            Rectangle tr = new Rectangle(r[i].X, r[i].Y, r[i].Width, r[i].Height - (int)(2 * s));
            TextRenderer.DrawText(g, items[i], f, tr, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (isSel)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float th = 3 * s;
                RectangleF ul = new RectangleF(r[i].X + 14 * s, Height - th - 0.5f, r[i].Width - 28 * s, th);
                using (GraphicsPath gp = Theme.RoundRect(ul, th / 2f))
                using (Brush b = new SolidBrush(Theme.Accent)) g.FillPath(b, gp);
            }
        }
    }
}

class SegmentedControl : Control
{
    string[] items = new string[0];
    int sel;
    public event EventHandler SelectedChanged;

    public SegmentedControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        Height = 30;
        Cursor = Cursors.Hand;
    }

    public string[] Items { get { return items; } set { items = value; Invalidate(); } }
    public int SelectedIndex
    {
        get { return sel; }
        set
        {
            if (value == sel || value < 0 || value >= items.Length) return;
            sel = value;
            Invalidate();
            if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (items.Length > 0) SelectedIndex = Math.Min(items.Length - 1, Math.Max(0, e.X * items.Length / Math.Max(1, Width)));
    }
    protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || base.IsInputKey(k); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left) { SelectedIndex = sel - 1; e.Handled = true; }
        else if (e.KeyCode == Keys.Right) { SelectedIndex = sel + 1; e.Handled = true; }
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = g.DpiX / 96f;
        RectangleF outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (GraphicsPath p = Theme.RoundRect(outer, 8 * s))
        using (Brush b = new SolidBrush(Theme.BtnFill))
        using (Pen pen = new Pen(Focused && ShowFocusCues ? Theme.Accent : Theme.BtnEdge))
        {
            g.FillPath(b, p);
            g.DrawPath(pen, p);
        }
        if (items.Length == 0) return;
        float w = outer.Width / items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            RectangleF seg = new RectangleF(outer.X + i * w, outer.Y, w, outer.Height);
            if (i == sel)
            {
                RectangleF inner = RectangleF.Inflate(seg, -2.5f * s, -2.5f * s);
                using (GraphicsPath p = Theme.RoundRect(inner, 6 * s))
                using (Brush b = new SolidBrush(Theme.ChipFill)) g.FillPath(b, p);
            }
            TextRenderer.DrawText(g, items[i], Font, Rectangle.Round(seg), i == sel ? Theme.ChipInk : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }
}

// Click it and press a key combination. Validate returns an error message, or null when the combination is acceptable.
class HotkeyBox : TextBox
{
    public uint Mods, Vk;
    public Func<uint, uint, string> Validate;
    public event Action<string> Notice;
    public event EventHandler Changed;

    public HotkeyBox()
    {
        ReadOnly = true;
        BorderStyle = BorderStyle.FixedSingle;
        TextAlign = HorizontalAlignment.Center;
        Cursor = Cursors.Hand;
    }

    public static string Describe(uint mods, uint vk)
    {
        if (vk == 0) return "(none)";
        string s = "";
        if ((mods & Native.MOD_CONTROL) != 0) s += "Ctrl+";
        if ((mods & Native.MOD_ALT) != 0) s += "Alt+";
        if ((mods & Native.MOD_SHIFT) != 0) s += "Shift+";
        if ((mods & Native.MOD_WIN) != 0) s += "Win+";
        Keys k = (Keys)vk;
        string name = k.ToString();
        if (k >= Keys.D0 && k <= Keys.D9) name = ((char)('0' + (k - Keys.D0))).ToString();
        else if (k >= Keys.NumPad0 && k <= Keys.NumPad9) name = "Num" + (k - Keys.NumPad0);
        return s + name;
    }

    public void SetKey(uint mods, uint vk) { Mods = mods; Vk = vk; Text = Describe(mods, vk); }

    protected override void OnEnter(EventArgs e) { base.OnEnter(e); Text = "Press keys..."; }
    protected override void OnLeave(EventArgs e) { base.OnLeave(e); Text = Describe(Mods, Vk); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;
        Keys k = e.KeyCode;
        if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin) return;
        Form f = FindForm();
        if (k == Keys.Escape) { Text = Describe(Mods, Vk); if (f != null) f.ActiveControl = null; return; }   // Esc is reserved for "stop"

        uint mods = 0;
        if (e.Control) mods |= Native.MOD_CONTROL;
        if (e.Alt) mods |= Native.MOD_ALT;
        if (e.Shift) mods |= Native.MOD_SHIFT;
        if (Native.Down(0x5B) || Native.Down(0x5C)) mods |= Native.MOD_WIN;
        bool fKey = k >= Keys.F1 && k <= Keys.F24;
        string err = null;
        if (mods == 0 && !fKey) err = "Use at least one modifier (Ctrl / Alt / Shift / Win) or an F-key.";
        else if (Validate != null) err = Validate(mods, (uint)k);

        if (err != null)
        {
            if (Notice != null) Notice(err);
            Text = Describe(Mods, Vk);
        }
        else
        {
            SetKey(mods, (uint)k);
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
        if (f != null) f.ActiveControl = null;
    }
}

interface IListRow { string Primary { get; } string Secondary { get; } }

class ThemedList : ListBox
{
    public ThemedList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        DoubleBuffered = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ItemHeight = (int)Math.Round(36 * DeviceDpi / 96.0);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        Graphics g = e.Graphics;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (Brush b = new SolidBrush(sel ? Theme.ChipFill : Theme.Card)) g.FillRectangle(b, e.Bounds);
        object item = Items[e.Index];
        IListRow row = item as IListRow;
        string primary = row != null ? row.Primary : item.ToString();
        string secondary = row != null ? row.Secondary : "";
        int pad = (int)(12 * DeviceDpi / 96f);
        Rectangle r = new Rectangle(e.Bounds.X + pad, e.Bounds.Y, e.Bounds.Width - 2 * pad, e.Bounds.Height);
        int secW = secondary.Length > 0 ? TextRenderer.MeasureText(secondary, Font).Width + pad : 0;
        TextRenderer.DrawText(g, primary, Font, new Rectangle(r.X, r.Y, Math.Max(1, r.Width - secW), r.Height), sel ? Theme.ChipInk : Theme.Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        if (secondary.Length > 0)
            TextRenderer.DrawText(g, secondary, Font, r, Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        using (Pen pen = new Pen(Theme.Border)) g.DrawLine(pen, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }
}

// ===================== Dialogs =====================

class ThemedDialog : Form
{
    protected Font fUI;

    public ThemedDialog(string title)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        fUI = new Font(Theme.PickFont("Segoe UI Variable Text", "Segoe UI"), 9.75f);
        Font = fUI;
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        TopMost = true;
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Theme.Dark) { int v = 1; Native.DwmSetWindowAttribute(Handle, 20, ref v, 4); }
    }

    protected RoundButton Btn(string text, int x, int y, int w, bool primary)
    {
        RoundButton b = new RoundButton { Text = text, Left = x, Top = y, Width = w, Height = 32, Font = fUI, Radius = 8 };
        if (primary) { b.Fill = Theme.Accent; b.FillHover = Theme.AccentHover; b.FillDown = Theme.AccentDown; b.Fg = Color.White; }
        else { b.Fill = Theme.BtnFill; b.FillHover = Theme.BtnHover; b.FillDown = Theme.BtnDown; b.Fg = Theme.Ink; b.Edge = Theme.BtnEdge; }
        return b;
    }

    protected TextBox Input(int x, int y, int w)
    {
        return new TextBox { Left = x, Top = y, Width = w, Font = fUI, BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Input, ForeColor = Theme.Ink };
    }
}

class PromptForm : ThemedDialog
{
    TextBox box;

    public static string Ask(string title, string label, string def)
    {
        using (PromptForm f = new PromptForm(title, label, def))
            return f.ShowDialog() == DialogResult.OK ? f.box.Text : null;
    }

    PromptForm(string title, string label, string def) : base(title)
    {
        ClientSize = new Size(440, 142);
        Label l = new Label { Text = label, Left = 18, Top = 16, Width = 404, Height = 22, AutoEllipsis = true };
        box = Input(18, 44, 404);
        box.Text = def ?? "";
        box.SelectAll();
        RoundButton ok = Btn("OK", 238, 92, 90, true);
        RoundButton cancel = Btn("Cancel", 332, 92, 90, false);
        ok.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        Controls.AddRange(new Control[] { l, box, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
        ActiveControl = box;
    }
}

class HotkeyDialog : ThemedDialog
{
    HotkeyBox box;
    Label err;
    public bool Removed;
    public uint Mods { get { return box.Mods; } }
    public uint Vk { get { return box.Vk; } }

    public HotkeyDialog(string title, uint mods, uint vk, Func<uint, uint, string> validate) : base(title)
    {
        ClientSize = new Size(440, 190);
        Label l = new Label { Text = "Click the box, then press the key combination you want.", Left = 18, Top = 16, Width = 404, Height = 22 };
        box = new HotkeyBox { Left = 18, Top = 46, Width = 404, Font = new Font(Theme.PickFont("Cascadia Mono", "Cascadia Code", "Consolas"), 11f, FontStyle.Bold), BackColor = Theme.InputAlt, ForeColor = Theme.Ink };
        box.Validate = validate;
        box.SetKey(mods, vk);
        err = new Label { Left = 18, Top = 84, Width = 404, Height = 40, ForeColor = Theme.Danger };
        box.Notice += delegate(string m) { err.Text = m; };
        box.Changed += delegate { err.Text = ""; };
        RoundButton remove = Btn("Remove hotkey", 18, 138, 130, false);
        RoundButton save = Btn("Save", 238, 138, 90, true);
        RoundButton cancel = Btn("Cancel", 332, 138, 90, false);
        save.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        remove.Click += delegate { Removed = true; DialogResult = DialogResult.OK; };
        Controls.AddRange(new Control[] { l, box, err, remove, save, cancel });
        CancelButton = cancel;
    }
}

// ===================== Floating progress badge =====================

class ProgressBadge : Form
{
    string sub = "";
    int pct;
    bool paused;
    readonly Font fBold, fSmall;

    public ProgressBadge()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Opacity = 0.96;
        DoubleBuffered = true;
        float s;
        using (Graphics g = CreateGraphics()) s = g.DpiX / 96f;
        string ui = Theme.PickFont("Segoe UI Variable Text", "Segoe UI");
        fBold = new Font(ui, 10f, FontStyle.Bold);
        fSmall = new Font(ui, 8.5f);
        ClientSize = new Size((int)(270 * s), (int)(68 * s));
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(wa.Right - Width - (int)(16 * s), wa.Bottom - Height - (int)(16 * s));
        using (GraphicsPath p = Theme.RoundRect(new RectangleF(0, 0, Width, Height), 10 * s)) Region = new Region(p);
    }

    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020;   // no-activate, tool window, click-through
            return cp;
        }
    }

    public void SetProgress(int percent, string text, bool isPaused)
    {
        pct = Math.Max(0, Math.Min(100, percent));
        sub = text;
        paused = isPaused;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = g.DpiX / 96f;
        Color accent = paused ? Theme.Warn : Theme.Accent;
        TextRenderer.DrawText(g, paused ? "Paused" : "Typing", fBold, new Rectangle((int)(14 * s), (int)(8 * s), (int)(150 * s), (int)(22 * s)), Theme.Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, pct + "%", fBold, new Rectangle((int)(170 * s), (int)(8 * s), (int)(86 * s), (int)(22 * s)), accent,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        RectangleF track = new RectangleF(14 * s, 34 * s, Width - 28 * s, 6 * s);
        using (GraphicsPath p = Theme.RoundRect(track, 3 * s)) using (Brush b = new SolidBrush(Theme.Track)) g.FillPath(b, p);
        if (pct > 0)
        {
            RectangleF fill = new RectangleF(track.X, track.Y, Math.Max(6 * s, track.Width * pct / 100f), track.Height);
            using (GraphicsPath p = Theme.RoundRect(fill, 3 * s)) using (Brush b = new SolidBrush(accent)) g.FillPath(b, p);
        }
        TextRenderer.DrawText(g, sub, fSmall, new Rectangle((int)(14 * s), (int)(44 * s), Width - (int)(28 * s), (int)(20 * s)), Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        using (Pen pen = new Pen(Theme.Border)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
