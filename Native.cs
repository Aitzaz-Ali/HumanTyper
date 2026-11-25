// SPDX-License-Identifier: GPL-3.0-or-later
// Part of Human Typer. See LICENSE.

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

// Win32 helpers: global hotkeys, synthetic keystrokes, foreground window.
static class Native
{
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
    const uint KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;

    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
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

    public static void Key(ushort vk) { Send(vk, 0, 0); Send(vk, 0, KEYEVENTF_KEYUP); }
    static void Uni(char c) { Send(0, c, KEYEVENTF_UNICODE); Send(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP); }

    // newline: 0 = Enter, 1 = Shift+Enter, 2 = a space
    public static void TypeChar(char c, int newline)
    {
        if (c == '\n')
        {
            if (newline == 1) { Send(0x10, 0, 0); Key(0x0D); Send(0x10, 0, KEYEVENTF_KEYUP); }
            else if (newline == 2) Uni(' ');
            else Key(0x0D);
        }
        else if (c == '\t') Key(0x09);
        else Uni(c);
    }

    public static bool Down(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
}

// Tiny key=value settings store plus helpers for the data files.
static class Store
{
    static Dictionary<string, string> d = new Dictionary<string, string>();

    public static string Dir
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HumanTyper"); }
    }
    public static string SettingsFile { get { return Path.Combine(Dir, "settings.ini"); } }
    public static string SnippetsFile { get { return Path.Combine(Dir, "snippets.dat"); } }
    public static string HistoryFile { get { return Path.Combine(Dir, "history.dat"); } }

    public static void Load()
    {
        d = new Dictionary<string, string>();
        try
        {
            foreach (string line in File.ReadAllLines(SettingsFile))
            {
                int p = line.IndexOf('=');
                if (p > 0) d[line.Substring(0, p)] = line.Substring(p + 1);
            }
        }
        catch { }
    }

    public static int Int(string key, int def)
    {
        string v; int n;
        return d.TryGetValue(key, out v) && int.TryParse(v, out n) ? n : def;
    }
    public static bool Bool(string key, bool def) { return Int(key, def ? 1 : 0) != 0; }
    public static void Set(string key, int v) { d[key] = v.ToString(); }
    public static void Set(string key, bool v) { d[key] = v ? "1" : "0"; }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, string> kv in d) lines.Add(kv.Key + "=" + kv.Value);
            File.WriteAllLines(SettingsFile, lines.ToArray());
        }
        catch { }
    }

    public static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? "")); }
    public static string UnB64(string s) { try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); } catch { return ""; } }
}
