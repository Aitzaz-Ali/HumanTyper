// SPDX-License-Identifier: GPL-3.0-or-later
// Part of Human Typer. See LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

class Snippet : IListRow
{
    public string Name, Text;
    public uint Mods, Vk;
    public string Primary { get { return Name; } }
    public string Secondary { get { return Vk == 0 ? "" : HotkeyBox.Describe(Mods, Vk); } }
    public override string ToString() { return Name; }
}

// "Snippets" tab: saved texts, each with an optional global hotkey, and {placeholders}.
public partial class MainForm
{
    readonly List<Snippet> snippets = new List<Snippet>();
    ThemedList lstSnips;
    TextBox txtPreview;
    RoundButton btnSnipNew, btnSnipUpdate, btnSnipRename, btnSnipHotkey, btnSnipType, btnSnipDelete;
    int registeredSnippetCount;

    Panel BuildSnippetsPage()
    {
        Panel page = new Panel { Padding = new Padding(16, 16, 16, 12), Tag = "bg" };

        // preview card (fills the rest)
        RoundedPanel previewCard = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 10) };
        txtPreview = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = fMono, BorderStyle = BorderStyle.None };
        Label hint = new Label { Dock = DockStyle.Bottom, Height = 44, Font = fUI, Tag = "muted", AutoSize = false,
            Text = "Tip: put {date}, {time}, {clipboard} or your own {name} in a snippet. You'll be asked to fill in custom ones before it's typed. Double-click a snippet to edit it on the Text tab." };
        previewCard.Controls.Add(txtPreview);
        previewCard.Controls.Add(hint);

        // buttons
        Panel btnStrip = new Panel { Dock = DockStyle.Bottom, Height = 98, Padding = new Padding(0, 10, 0, 0), Tag = "bg" };
        btnSnipNew = MkSecondary("Save editor text as new", 200); btnSnipNew.Left = 0; btnSnipNew.Top = 10;
        btnSnipUpdate = MkSecondary("Update from editor", 170); btnSnipUpdate.Left = 208; btnSnipUpdate.Top = 10;
        btnSnipRename = MkSecondary("Rename", 90); btnSnipRename.Left = 386; btnSnipRename.Top = 10;
        btnSnipHotkey = MkSecondary("Set hotkey", 120); btnSnipHotkey.Left = 0; btnSnipHotkey.Top = 54;
        btnSnipType = MkSecondary("Type now", 110); btnSnipType.Left = 128; btnSnipType.Top = 54;
        btnSnipDelete = MkSecondary("Delete", 90); btnSnipDelete.Left = 246; btnSnipDelete.Top = 54;
        foreach (RoundButton b in new RoundButton[] { btnSnipNew, btnSnipUpdate, btnSnipRename, btnSnipHotkey, btnSnipType, btnSnipDelete })
        { b.Height = 34; btnStrip.Controls.Add(b); }

        // list card (top)
        Panel gap = new Panel { Dock = DockStyle.Top, Height = 12, Tag = "bg" };
        RoundedPanel listCard = new RoundedPanel { Dock = DockStyle.Top, Height = 212, Padding = new Padding(2, 8, 2, 8) };
        lstSnips = new ThemedList { Dock = DockStyle.Fill, Font = fUI };
        listCard.Controls.Add(lstSnips);

        page.Controls.Add(previewCard);
        page.Controls.Add(btnStrip);
        page.Controls.Add(gap);
        page.Controls.Add(listCard);

        // events
        lstSnips.SelectedIndexChanged += delegate { ShowSnippetPreview(); };
        lstSnips.DoubleClick += delegate { LoadSnippetIntoEditor(); };
        btnSnipNew.Click += delegate { NewSnippetFromEditor(); };
        btnSnipUpdate.Click += delegate { UpdateSnippetFromEditor(); };
        btnSnipRename.Click += delegate { RenameSnippet(); };
        btnSnipHotkey.Click += delegate { EditSnippetHotkey(); };
        btnSnipType.Click += delegate { TypeSelectedSnippet(); };
        btnSnipDelete.Click += delegate { DeleteSnippet(); };

        LoadSnippets();
        RefreshSnippetList(0);
        return page;
    }

    Snippet SelectedSnippet { get { return lstSnips.SelectedItem as Snippet; } }

    // ---- storage ----
    void LoadSnippets()
    {
        try
        {
            foreach (string line in File.ReadAllLines(Store.SnippetsFile))
            {
                string[] p = line.Split('|');
                if (p.Length < 4) continue;
                uint m, v;
                uint.TryParse(p[1], out m); uint.TryParse(p[2], out v);
                snippets.Add(new Snippet { Name = Store.UnB64(p[0]), Mods = m, Vk = v, Text = Store.UnB64(p[3]) });
            }
        }
        catch { }
    }

    void SaveSnippets()
    {
        try
        {
            Directory.CreateDirectory(Store.Dir);
            List<string> lines = new List<string>();
            foreach (Snippet s in snippets) lines.Add(Store.B64(s.Name) + "|" + s.Mods + "|" + s.Vk + "|" + Store.B64(s.Text));
            File.WriteAllLines(Store.SnippetsFile, lines.ToArray());
        }
        catch { }
    }

    void RefreshSnippetList(int select)
    {
        lstSnips.BeginUpdate();
        lstSnips.Items.Clear();
        foreach (Snippet s in snippets) lstSnips.Items.Add(s);
        lstSnips.EndUpdate();
        if (lstSnips.Items.Count > 0) lstSnips.SelectedIndex = Math.Max(0, Math.Min(select, lstSnips.Items.Count - 1));
        ShowSnippetPreview();
    }

    void ShowSnippetPreview()
    {
        Snippet s = SelectedSnippet;
        txtPreview.Text = s != null ? s.Text
            : "No snippets yet.\r\n\r\nGo to the Text tab, type or paste something, then come back here and click \"Save editor text as new\".";
        bool has = s != null;
        btnSnipUpdate.Enabled = has; btnSnipRename.Enabled = has; btnSnipHotkey.Enabled = has;
        btnSnipType.Enabled = has; btnSnipDelete.Enabled = has;
    }

    // ---- actions ----
    void NewSnippetFromEditor()
    {
        if (string.IsNullOrWhiteSpace(txt.Text)) { SetStatus("Type or paste some text on the Text tab first."); return; }
        string first = txt.Text.Replace("\r", " ").Replace("\n", " ").Trim();
        string name = PromptForm.Ask("Save snippet", "Name for this snippet:", first.Length > 28 ? first.Substring(0, 28) : first);
        if (string.IsNullOrWhiteSpace(name)) return;
        snippets.Add(new Snippet { Name = name.Trim(), Text = txt.Text });
        SaveSnippets();
        RefreshSnippetList(snippets.Count - 1);
        SetStatus("Saved snippet \"" + name.Trim() + "\".");
    }

    void UpdateSnippetFromEditor()
    {
        Snippet s = SelectedSnippet;
        if (s == null) return;
        if (string.IsNullOrWhiteSpace(txt.Text)) { SetStatus("The text box on the Text tab is empty."); return; }
        s.Text = txt.Text;
        SaveSnippets();
        ShowSnippetPreview();
        SetStatus("Updated \"" + s.Name + "\" from the text box.");
    }

    void RenameSnippet()
    {
        Snippet s = SelectedSnippet;
        if (s == null) return;
        string name = PromptForm.Ask("Rename snippet", "New name:", s.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        s.Name = name.Trim();
        SaveSnippets();
        int i = lstSnips.SelectedIndex;
        RefreshSnippetList(i);
    }

    void DeleteSnippet()
    {
        Snippet s = SelectedSnippet;
        if (s == null) return;
        if (MessageBox.Show(this, "Delete the snippet \"" + s.Name + "\"?", "Delete snippet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        int i = lstSnips.SelectedIndex;
        snippets.Remove(s);
        SaveSnippets();
        RefreshSnippetList(i);
        RegisterSnippetHotkeys();
    }

    void LoadSnippetIntoEditor()
    {
        Snippet s = SelectedSnippet;
        if (s == null) return;
        txt.Text = s.Text;
        tabs.SelectedIndex = 0;
        txt.Focus();
    }

    void EditSnippetHotkey()
    {
        Snippet s = SelectedSnippet;
        if (s == null) return;
        uint oldM = s.Mods, oldV = s.Vk;
        using (HotkeyDialog d = new HotkeyDialog("Hotkey for \"" + s.Name + "\"", s.Mods, s.Vk, delegate(uint m, uint v)
        {
            string clash = HotkeyClash(m, v, s);
            return clash != null ? HotkeyBox.Describe(m, v) + " is already used for " + clash + "." : null;
        }))
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            if (d.Removed) { s.Mods = 0; s.Vk = 0; } else { s.Mods = d.Mods; s.Vk = d.Vk; }
        }
        if (!RegisterSnippetHotkeys())
        {
            s.Mods = oldM; s.Vk = oldV;
            RegisterSnippetHotkeys();
            SetStatus("That hotkey is in use by another app.");
        }
        else SetStatus(s.Vk == 0 ? "Removed the hotkey from \"" + s.Name + "\"." : "\"" + s.Name + "\" now types with " + HotkeyBox.Describe(s.Mods, s.Vk) + ".");
        SaveSnippets();
        lstSnips.Invalidate();
    }

    void TypeSelectedSnippet()
    {
        Snippet s = SelectedSnippet;
        if (s == null || typing) return;
        string text = FillPlaceholders(s.Text, IntPtr.Zero);
        if (text == null) return;
        Begin(text, (int)numDelay.Value * 1000, false);   // the delay gives you time to click into the target
    }

    // ---- hotkeys ----
    string SnippetHotkeyClash(uint m, uint v, object except)
    {
        foreach (Snippet s in snippets)
            if (s != except && s.Vk != 0 && s.Mods == m && s.Vk == v) return "the snippet \"" + s.Name + "\"";
        return null;
    }

    void UnregisterSnippetHotkeys()
    {
        for (int i = 0; i < registeredSnippetCount; i++) Native.UnregisterHotKey(Handle, ID_SNIP + i);
        registeredSnippetCount = 0;
    }

    // Returns false if any snippet hotkey could not be registered.
    bool RegisterSnippetHotkeys()
    {
        if (!IsHandleCreated) return true;
        UnregisterSnippetHotkeys();
        bool ok = true;
        for (int i = 0; i < snippets.Count; i++)
        {
            Snippet s = snippets[i];
            if (s.Vk == 0) continue;
            if (!Native.RegisterHotKey(Handle, ID_SNIP + i, s.Mods | Native.MOD_NOREPEAT, s.Vk)) ok = false;
        }
        registeredSnippetCount = snippets.Count;
        return ok;
    }

    void TypeSnippetByHotkey(int index)
    {
        if (typing || index < 0 || index >= snippets.Count) return;
        IntPtr fg = Native.GetForegroundWindow();   // remember the target; prompts may take focus
        string text = FillPlaceholders(snippets[index].Text, fg);
        if (text == null) return;
        Begin(text, 400, false);
    }

    // ---- placeholders ----
    // {date} {time} {datetime} {clipboard} are filled in automatically; any other {word} asks you for a value.
    // Returns null if you cancel. Only snippets use placeholders, so ordinary text with braces is never touched.
    string FillPlaceholders(string text, IntPtr restoreWindow)
    {
        Regex rx = new Regex(@"\{([^{}\r\n]{1,40})\}");
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in rx.Matches(text))
        {
            string key = m.Groups[1].Value.Trim();
            if (values.ContainsKey(key)) continue;
            string low = key.ToLowerInvariant();
            string v;
            if (low == "date") v = DateTime.Now.ToString("d");
            else if (low == "time") v = DateTime.Now.ToString("t");
            else if (low == "datetime") v = DateTime.Now.ToString("g");
            else if (low == "clipboard") { try { v = Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch { v = ""; } }
            else
            {
                v = PromptForm.Ask("Fill in snippet", "Value for {" + key + "}:", "");
                if (v == null) { SetStatus("Cancelled."); return null; }
            }
            values[key] = v;
        }
        if (restoreWindow != IntPtr.Zero && restoreWindow != Handle) Native.SetForegroundWindow(restoreWindow);   // back to where you were
        return rx.Replace(text, delegate(Match m) { return values[m.Groups[1].Value.Trim()]; });
    }
}
