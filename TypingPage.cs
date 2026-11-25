// SPDX-License-Identifier: GPL-3.0-or-later
// Part of Human Typer. See LICENSE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// "Typing" tab: how text is typed (line breaks, indentation, window guard, typos, pauses, breaks).
public partial class MainForm
{
    SegmentedControl segNewline;
    Toggle chkStrip, chkGuard, chkChunk, chkWait;
    FlatSlider slTypo, slPause;
    Label lblTypo, lblPause;
    NumericUpDown numChunk, numPause;
    readonly List<Control> chunkDependents = new List<Control>();

    static readonly string[] TypoNames = { "Off", "Rare", "Normal", "Often", "Lots", "Sloppy" };
    static readonly double[] TypoRate = { 0, 0.010, 0.025, 0.040, 0.060, 0.090 };
    static readonly string[] PauseNames = { "None", "Rare", "Normal", "Often", "Lots" };
    static readonly double[] PauseRate = { 0, 0.006, 0.012, 0.025, 0.040 };

    Panel BuildTypingPage()
    {
        Panel page;
        RoundedPanel card = MkCardPage(out page, 16);
        card.Height = 372;
        card.Dividers = new int[] { 138, 232 };

        Label lblLines = Mk("Line breaks", 20, 24, true, false);
        segNewline = new SegmentedControl { Left = 140, Top = 18, Width = 330, Font = fUI, Items = new string[] { "Enter", "Shift+Enter", "Space" } };
        chkStrip = MkToggle("Strip leading indentation (for auto-indenting editors)", 20, 62, 520);
        chkGuard = MkToggle("Pause if I switch to another window", 20, 96, 520);

        Label lblT = Mk("Typos", 20, 160, true, false);
        slTypo = new FlatSlider { Left = 140, Top = 153, Width = 300, Minimum = 0, Maximum = TypoNames.Length - 1, Step = 1 };
        lblTypo = new Label { Width = 80, Top = 160, Height = 20, TextAlign = ContentAlignment.TopRight, Font = fUIBold, Tag = "accent" };
        Label lblP = Mk("Thinking pauses", 20, 196, true, false);
        slPause = new FlatSlider { Left = 140, Top = 189, Width = 300, Minimum = 0, Maximum = PauseNames.Length - 1, Step = 1 };
        lblPause = new Label { Width = 80, Top = 196, Height = 20, TextAlign = ContentAlignment.TopRight, Font = fUIBold, Tag = "accent" };
        slTypo.Value = 2; slPause.Value = 2;
        lblTypo.Text = TypoNames[slTypo.Value];
        lblPause.Text = PauseNames[slPause.Value];
        slTypo.ValueChanged += delegate { lblTypo.Text = TypoNames[slTypo.Value]; };
        slPause.ValueChanged += delegate { lblPause.Text = PauseNames[slPause.Value]; };

        chkChunk = MkToggle("Take a break after about", 20, 250, 225);
        numChunk = MkNum(250, 248, 80, 5, 5000, 200);
        Label lChars = Mk("characters", 340, 252, false, true);
        Label lBreak = Mk("Break length", 62, 290, false, true);
        numPause = MkNum(250, 287, 80, 1, 600, 5);
        Label lSecs = Mk("seconds", 340, 290, false, true);
        chkWait = MkToggle("Wait for me to press the hotkey instead", 40, 326, 460);
        chunkDependents.AddRange(new Control[] { numChunk, lChars, lBreak, numPause, lSecs, chkWait });
        chkChunk.CheckedChanged += delegate { UpdateChunkEnabled(); };

        card.Controls.AddRange(new Control[] { lblLines, segNewline, chkStrip, chkGuard, lblT, slTypo, lblTypo, lblP, slPause, lblPause,
            chkChunk, numChunk, lChars, lBreak, numPause, lSecs, chkWait });
        FitSlider(card, lblT, slTypo, lblTypo);
        FitSlider(card, lblP, slPause, lblPause);
        relayouts.Add(delegate { segNewline.Width = Math.Max(200, Math.Min(380, card.ClientSize.Width - segNewline.Left - lblLines.Left)); });
        return page;
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
}
