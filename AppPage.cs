// SPDX-License-Identifier: GPL-3.0-or-later
// Part of Human Typer. See LICENSE.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// "App" tab: hotkeys, notifications, start-up behavior, theme, update check.
public partial class MainForm
{
    HotkeyBox hkStartBox, hkPauseBox;
    NumericUpDown numDelay;
    Toggle chkBadge, chkNotify, chkStartup, chkMin;
    RoundButton btnTheme, btnUpdate;
    Label lblUpdate;
    string updateUrl;

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    Panel BuildAppPage()
    {
        Panel page;
        RoundedPanel card = MkCardPage(out page, 16);
        card.Height = 432;
        card.Dividers = new int[] { 134, 292 };

        Font hkFont = fMonoBold;
        Label l1 = Mk("Start / stop hotkey", 20, 24, true, false);
        hkStartBox = new HotkeyBox { Left = 200, Top = 20, Width = 170, Font = hkFont };
        Label l2 = Mk("Pause / resume hotkey", 20, 60, true, false);
        hkPauseBox = new HotkeyBox { Left = 200, Top = 56, Width = 170, Font = hkFont };
        Label hint = Mk("click a box, then press the keys", 384, 24, false, true);
        Label l3 = Mk("Button start delay", 20, 98, true, false);
        numDelay = MkNum(200, 95, 60, 1, 30, 4);
        Label l3b = Mk("seconds", 268, 98, false, true);

        chkBadge = MkToggle("Show a floating progress badge while typing", 20, 150, 520);
        chkNotify = MkToggle("Play a sound when typing finishes", 20, 184, 520);
        chkStartup = MkToggle("Start with Windows", 20, 218, 520);
        chkMin = MkToggle("Start minimized to the tray", 20, 252, 520);

        Label l4 = Mk("Appearance", 20, 312, true, false);
        btnTheme = MkSecondary("Theme: Auto", 140); btnTheme.Left = 200; btnTheme.Top = 306; btnTheme.Height = 32;
        Label l5 = Mk("Updates", 20, 358, true, false);
        btnUpdate = MkSecondary("Check for updates", 170); btnUpdate.Left = 200; btnUpdate.Top = 352; btnUpdate.Height = 32;
        lblUpdate = Mk("Installed: v" + Version, 20, 398, false, true);

        card.Controls.AddRange(new Control[] { l1, hkStartBox, l2, hkPauseBox, hint, l3, numDelay, l3b,
            chkBadge, chkNotify, chkStartup, chkMin, l4, btnTheme, l5, btnUpdate, lblUpdate });

        // hotkey boxes
        hkStartBox.Validate = GlobalValidator(hkStartBox, ID_START);
        hkPauseBox.Validate = GlobalValidator(hkPauseBox, ID_PAUSE);
        hkStartBox.Notice += SetStatus;
        hkPauseBox.Notice += SetStatus;
        hkStartBox.Changed += delegate { RefreshHints(); SaveSettings(); SetStatus("Start / stop hotkey set to " + HK + "."); };
        hkPauseBox.Changed += delegate { SaveSettings(); SetStatus("Pause / resume hotkey set to " + PauseHK + "."); };

        chkStartup.CheckedChanged += delegate { if (!loading) SetStartup(chkStartup.Checked); };
        chkMin.CheckedChanged += delegate { SaveSettings(); };
        chkBadge.CheckedChanged += delegate { SaveSettings(); };
        chkNotify.CheckedChanged += delegate { SaveSettings(); };

        btnTheme.Click += delegate { themeMode = (themeMode + 1) % 3; SaveSettings(); ApplyTheme(); };
        btnUpdate.Click += delegate
        {
            if (updateUrl != null)
            {
                try { Process.Start(new ProcessStartInfo(updateUrl) { UseShellExecute = true }); } catch { }
            }
            else CheckForUpdates();
        };
        return page;
    }

    // ---- start with Windows ----
    static bool IsStartupEnabled()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("HumanTyper") != null;
        }
        catch { return false; }
    }

    void SetStartup(bool on)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue("HumanTyper", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("HumanTyper", false);
            }
        }
        catch { SetStatus("Could not change the Windows start-up setting."); }
    }

    // ---- update check (only when the button is clicked) ----
    void CheckForUpdates()
    {
        btnUpdate.Enabled = false;
        lblUpdate.Text = "Checking...";
        Thread t = new Thread(delegate()
        {
            string msg;
            string url = null;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(RepoLatestApi);
                req.UserAgent = "HumanTyper-update-check";
                req.Accept = "application/vnd.github+json";
                req.Timeout = 8000;
                string json;
                using (WebResponse r = req.GetResponse())
                using (StreamReader sr = new StreamReader(r.GetResponseStream()))
                    json = sr.ReadToEnd();
                Match m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9][^\"]*)\"");
                System.Version latest, current;
                if (m.Success && System.Version.TryParse(m.Groups[1].Value, out latest) && System.Version.TryParse(Version, out current))
                {
                    if (latest > current) { msg = "Version " + latest + " is available (you have " + Version + ")."; url = RepoReleases; }
                    else msg = "You're up to date (v" + Version + ").";
                }
                else msg = "Couldn't read the latest version.";
            }
            catch (WebException ex)
            {
                HttpWebResponse hr = ex.Response as HttpWebResponse;
                msg = (hr != null && hr.StatusCode == HttpStatusCode.NotFound) ? "No releases are published yet." : "Couldn't check for updates (are you offline?).";
            }
            catch { msg = "Couldn't check for updates."; }
            try
            {
                BeginInvoke(new Action(delegate
                {
                    lblUpdate.Text = msg;
                    updateUrl = url;
                    btnUpdate.Text = url != null ? "Open download page" : "Check for updates";
                    btnUpdate.Enabled = true;
                }));
            }
            catch { }
        });
        t.IsBackground = true;
        t.Start();
    }
}
