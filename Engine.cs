// SPDX-License-Identifier: GPL-3.0-or-later
// Part of Human Typer. See LICENSE.

using System;
using System.Media;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

class RunOptions
{
    public int Wpm, Newline, ChunkN, PauseSec;
    public double TypoRate, PauseRate;
    public bool WaitKey, Strip, Guard, Badge, Notify;
}

// The typing engine: runs on a background thread and "types" with human-like timing.
public partial class MainForm
{
    ProgressBadge badge;
    IntPtr ownHandle;

    void Begin(string text, int delayMs, bool addToHistory)
    {
        if (typing) return;
        if (string.IsNullOrEmpty(text)) { SetStatus("Nothing to type."); return; }
        if (addToHistory) AddHistory(text);
        SaveSettings();

        RunOptions o = new RunOptions();
        o.Wpm = tbWpm.Value;
        o.TypoRate = TypoRate[slTypo.Value];
        o.PauseRate = PauseRate[slPause.Value];
        o.ChunkN = chkChunk.Checked ? (int)numChunk.Value : 0;
        o.PauseSec = (int)numPause.Value;
        o.WaitKey = chkWait.Checked;
        o.Newline = segNewline.SelectedIndex;
        o.Strip = chkStrip.Checked;
        o.Guard = chkGuard.Checked;
        o.Badge = chkBadge.Checked;
        o.Notify = chkNotify.Checked;

        ownHandle = Handle;
        typing = true; cancel = false; waiting = false; userPaused = false;
        Native.RegisterHotKey(Handle, ID_ESC, 0, (uint)Keys.Escape);
        SetTypingUi(true);
        if (o.Badge) ShowBadge();

        Thread th = new Thread(delegate() { Run(text, delayMs, o); });
        th.IsBackground = true;
        th.Start();
    }

    void TogglePause()
    {
        if (!typing) return;
        userPaused = !userPaused;
        if (!userPaused) SetStatus("Resuming...");
    }

    // ---- floating badge ----
    void ShowBadge()
    {
        HideBadge();
        badge = new ProgressBadge();
        badge.SetProgress(0, "Esc stops  ·  " + PauseHK + " pauses", false);
        badge.Show();
    }

    void HideBadge()
    {
        if (badge == null) return;
        try { badge.Close(); badge.Dispose(); } catch { }
        badge = null;
    }

    void UpdateBadge(int pct, string text, bool paused)
    {
        try
        {
            BeginInvoke(new Action(delegate { if (badge != null) badge.SetProgress(pct, text, paused); }));
        }
        catch { }
    }

    // ---- timing helpers ----
    // Sleep in small slices so Stop reacts quickly.
    void Pause(double ms)
    {
        int left = (int)ms;
        while (left > 0 && !cancel) { int s = Math.Min(left, 30); Thread.Sleep(s); left -= s; }
    }

    void WaitModifiersUp()
    {
        for (int k = 0; k < 60 && !cancel && (Native.Down(0x11) || Native.Down(0x12) || Native.Down(0x10) || Native.Down(0x5B) || Native.Down(0x5C)); k++) Thread.Sleep(50);
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

    // Handles the pause hotkey and the "window changed" guard. Returns false when typing was stopped.
    bool WaitIfNeeded(RunOptions o, IntPtr target, int i, int n)
    {
        int pct = n == 0 ? 0 : i * 100 / n;
        if (userPaused)
        {
            SetStatus("Paused at " + i + " / " + n + ". Press " + PauseHK + " to resume (Esc stops).");
            UpdateBadge(pct, "Press " + PauseHK + " to resume", true);
            while (userPaused && !cancel) Thread.Sleep(50);
            if (cancel) return false;
            WaitModifiersUp();
            Pause(300);
        }
        if (o.Guard && Native.GetForegroundWindow() != target)
        {
            SetStatus("Paused: you switched windows. Click back into the target to continue (Esc stops).");
            UpdateBadge(pct, "Click back into the target window", true);
            while (!cancel && Native.GetForegroundWindow() != target) Thread.Sleep(100);
            if (cancel) return false;
            Pause(600);
        }
        return !cancel;
    }

    void Run(string text, int delayMs, RunOptions o)
    {
        bool finished = false;
        int total = 0;
        try
        {
            SetStatus("Starting... click into the target text box.");
            Pause(delayMs);
            WaitModifiersUp();
            if (cancel) { SetStatus("Stopped."); return; }

            IntPtr target = Native.GetForegroundWindow();
            if (target == IntPtr.Zero || target == ownHandle)
            {
                SetStatus("Click into another app's text box first, then start again.");
                return;
            }

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (o.Strip) text = Regex.Replace(text, @"(?m)^[ \t]+", "");
            total = text.Length;

            double baseMs = 60000.0 / (o.Wpm * 5.0);
            double speed = 1.0;
            int i = 0, sinceBreak = 0, nextBreak = o.ChunkN > 0 ? NextBreak(o.ChunkN) : 0, lastPct = -1;

            while (i < text.Length && !cancel)
            {
                if (!WaitIfNeeded(o, target, i, text.Length)) break;

                char c = text[i];
                speed += (rng.NextDouble() - 0.5) * 0.1;
                speed = Math.Max(0.7, Math.Min(1.35, speed));

                if (o.TypoRate > 0 && c < 128 && char.IsLetter(c) && rng.NextDouble() < o.TypoRate)
                {
                    char w = Neighbor(c);
                    if (w != c)
                    {
                        Native.TypeChar(w, o.Newline);
                        Pause(Delay(baseMs, speed));
                        int extra = 0, maxExtra = rng.Next(0, 3);
                        while (extra < maxExtra && i + extra + 1 < text.Length && text[i + extra + 1] != '\n')
                        {
                            extra++;
                            Native.TypeChar(text[i + extra], o.Newline);
                            Pause(Delay(baseMs, speed));
                        }
                        Pause(rng.Next(180, 450));   // "notice" the mistake
                        for (int b = 0; b <= extra; b++) { Native.Key(0x08); Pause(rng.Next(50, 110)); }
                        Pause(rng.Next(80, 200));
                        if (cancel) break;
                    }
                }

                Native.TypeChar(c, o.Newline);
                i++; sinceBreak++;

                double d = Delay(baseMs, speed);
                if (c == ' ') d *= 1.2;
                else if (".!?".IndexOf(c) >= 0 && rng.NextDouble() < 0.6) d += rng.Next(200, 500);
                else if (",;:".IndexOf(c) >= 0 && rng.NextDouble() < 0.4) d += rng.Next(80, 250);
                else if (c == '\n') d += rng.Next(250, 700);
                if (o.PauseRate > 0 && rng.NextDouble() < o.PauseRate) d += rng.Next(400, 1300);   // brief "thinking" pause
                Pause(d);

                int pct = i * 100 / text.Length;
                if (i % 10 == 0) SetStatus("Typing... " + i + " / " + text.Length);
                if (pct != lastPct)
                {
                    lastPct = pct;
                    UpdateBadge(pct, "Esc stops  ·  " + PauseHK + " pauses", false);
                }

                // "Type N characters, then pause": break at a word boundary once the target is reached
                if (o.ChunkN > 0 && !cancel && i < text.Length && sinceBreak >= nextBreak && (c == ' ' || c == '\n' || sinceBreak >= nextBreak + 15))
                {
                    sinceBreak = 0;
                    nextBreak = NextBreak(o.ChunkN);
                    if (o.WaitKey)
                    {
                        waiting = true;
                        SetStatus("Paused at " + i + " / " + text.Length + ". Press " + HK + " to resume (Esc stops).");
                        UpdateBadge(pct, "Press " + HK + " to resume", true);
                        while (waiting && !cancel) Thread.Sleep(50);
                        waiting = false;
                        if (cancel) break;
                        WaitModifiersUp();
                        Pause(400);
                    }
                    else
                    {
                        double secs = o.PauseSec * (0.8 + 0.4 * rng.NextDouble());
                        for (double left = secs; left > 0 && !cancel; left -= 1)
                        {
                            SetStatus("Paused at " + i + " / " + text.Length + ". Resuming in " + (int)Math.Ceiling(left) + " s...");
                            UpdateBadge(pct, "Resuming in " + (int)Math.Ceiling(left) + " s", true);
                            Pause(Math.Min(1000, left * 1000));
                        }
                    }
                }
            }
            finished = !cancel;
            SetStatus(cancel ? "Stopped at " + i + " / " + text.Length + "." : "Done. Typed " + text.Length + " characters.");
        }
        finally
        {
            typing = false; waiting = false; userPaused = false;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    Native.UnregisterHotKey(Handle, ID_ESC);
                    SetTypingUi(false);
                    HideBadge();
                    if (finished && o.Notify) NotifyFinished(total);
                }));
            }
            catch { }
        }
    }

    void NotifyFinished(int chars)
    {
        try { SystemSounds.Asterisk.Play(); } catch { }
        if (!Visible)
            tray.ShowBalloonTip(2000, "Human Typer", "Finished typing " + chars + " characters.", ToolTipIcon.Info);
    }
}
