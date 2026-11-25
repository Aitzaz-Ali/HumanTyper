// SPDX-License-Identifier: GPL-3.0-or-later
// Part of Human Typer. See LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// "Text" tab: the text box, speed + presets, and recent-text history.
public partial class MainForm
{
    TextBox txt;
    Label lblPlaceholder, lblCount, lblWpm;
    RoundButton btnPaste, btnClear, btnRecent;
    FlatSlider tbWpm;
    Toggle chkClip;
    readonly List<string> history = new List<string>();

    Panel BuildTextPage()
    {
        Panel page = new Panel { Tag = "bg" };

        // ----- speed card (bottom) -----
        Panel quickHost = new Panel { Dock = DockStyle.Bottom, Height = 164, Padding = new Padding(16, 4, 16, 12), Tag = "bg" };
        RoundedPanel quick = new RoundedPanel { Dock = DockStyle.Fill };
        Label lblSpeed = Mk("Typing speed", 20, 21, true, false);
        tbWpm = new FlatSlider { Left = 140, Top = 14, Width = 300, Minimum = 20, Maximum = 200, Step = 5 };
        tbWpm.Value = 70;
        lblWpm = new Label { Text = "70 WPM", Width = 76, Top = 21, Height = 20, TextAlign = ContentAlignment.TopRight, Font = fUIBold, Tag = "accent" };
        Label lblPre = Mk("Presets", 20, 64, true, false);
        string[] presetNames = { "Careful", "Normal", "Fast", "Turbo" };
        int[] presetWpm = { 35, 70, 110, 160 };
        quick.Controls.AddRange(new Control[] { lblSpeed, tbWpm, lblWpm, lblPre });
        for (int i = 0; i < presetNames.Length; i++)
        {
            RoundButton b = MkSecondary(presetNames[i], 84);
            b.Left = 140 + i * 92; b.Top = 58; b.Height = 30;
            int w = presetWpm[i];
            b.Click += delegate { tbWpm.Value = w; };
            quick.Controls.Add(b);
        }
        chkClip = MkToggle("Hotkey types the clipboard instead of the text box", 20, 106, 460);
        quick.Controls.Add(chkClip);
        quickHost.Controls.Add(quick);
        FitSlider(quick, lblSpeed, tbWpm, lblWpm);

        // ----- text card (fills the rest) -----
        Panel textHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 8), Tag = "bg" };
        RoundedPanel textCard = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 12, 0) };

        txt = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, AcceptsTab = true, Dock = DockStyle.Fill, Font = fMono, BorderStyle = BorderStyle.None };
        lblPlaceholder = new Label { Text = "Paste or type the text you want typed...", AutoSize = true, Left = 0, Top = 0, Font = fUI, Tag = "faint", Cursor = Cursors.IBeam };
        txt.Controls.Add(lblPlaceholder);
        lblPlaceholder.Click += delegate { txt.Focus(); };

        Panel strip = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(0, 8, 4, 8) };
        lblCount = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = fUI, Tag = "muted", AutoEllipsis = true };
        btnRecent = MkSecondary("Recent ▾", 90); btnRecent.Dock = DockStyle.Left;
        btnClear = MkSecondary("Clear", 66); btnClear.Dock = DockStyle.Left;
        btnPaste = MkSecondary("Paste clipboard", 126); btnPaste.Dock = DockStyle.Left;
        strip.Controls.Add(lblCount);
        strip.Controls.Add(btnRecent);
        strip.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8 });
        strip.Controls.Add(btnClear);
        strip.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8 });
        strip.Controls.Add(btnPaste);

        textCard.Controls.Add(txt);
        textCard.Controls.Add(strip);
        textHost.Controls.Add(textCard);

        page.Controls.Add(textHost);
        page.Controls.Add(quickHost);

        // ----- events -----
        tbWpm.ValueChanged += delegate { lblWpm.Text = tbWpm.Value + " WPM"; UpdateCount(); };
        txt.TextChanged += delegate { lblPlaceholder.Visible = txt.TextLength == 0; UpdateCount(); };
        btnPaste.Click += delegate { try { if (Clipboard.ContainsText()) txt.Text = Clipboard.GetText(); } catch { } };
        btnClear.Click += delegate { txt.Clear(); txt.Focus(); };
        btnRecent.Click += delegate { ShowRecent(); };

        LoadHistory();
        return page;
    }

    void UpdateCount()
    {
        if (lblCount == null) return;
        int n = txt.TextLength;
        if (n == 0) { lblCount.Text = "0 characters"; return; }
        double secs = n / 5.0 / Math.Max(1, tbWpm.Value) * 60.0;
        string t = secs < 60 ? ((int)Math.Ceiling(secs)) + " sec" : ((int)(secs / 60)) + " min " + ((int)(secs % 60)) + " sec";
        lblCount.Text = n.ToString("N0") + " characters  ·  about " + t;
    }

    // ---- recent texts ----
    void LoadHistory()
    {
        try
        {
            foreach (string line in File.ReadAllLines(Store.HistoryFile))
            {
                string t = Store.UnB64(line);
                if (t.Length > 0 && history.Count < 10) history.Add(t);
            }
        }
        catch { }
    }

    void AddHistory(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 8) return;
        history.Remove(text);
        history.Insert(0, text);
        while (history.Count > 10) history.RemoveAt(history.Count - 1);
        try
        {
            Directory.CreateDirectory(Store.Dir);
            List<string> lines = new List<string>();
            foreach (string h in history) lines.Add(Store.B64(h));
            File.WriteAllLines(Store.HistoryFile, lines.ToArray());
        }
        catch { }
    }

    void ShowRecent()
    {
        ContextMenuStrip m = new ContextMenuStrip();
        m.Font = fUI;
        m.Renderer = new ToolStripProfessionalRenderer(new MenuColors()) { RoundedEdges = false };
        m.BackColor = Theme.Card;
        m.ForeColor = Theme.Ink;
        if (history.Count == 0)
        {
            ToolStripMenuItem none = new ToolStripMenuItem("No recent texts yet");
            none.Enabled = false;
            m.Items.Add(none);
        }
        foreach (string h in history)
        {
            string label = h.Replace("\r", " ").Replace("\n", " ");
            if (label.Length > 70) label = label.Substring(0, 70) + "...";
            string full = h;
            m.Items.Add(label, null, delegate { txt.Text = full; txt.Focus(); });
        }
        m.Show(btnRecent, new Point(0, btnRecent.Height));
    }
}
