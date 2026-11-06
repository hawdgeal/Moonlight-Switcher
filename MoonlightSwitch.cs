// MoonlightSwitch (client PC) - opens Moonlight, shows the switch bar, serves the host bar to
// the host PC, and switches between the stream (host) and your desktop (client).
//
// How the bars look and behave: Bar.cs and settings.ini (both shared with the host).
// After editing a .cs file, run build.cmd.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MoonlightSwitchUI;
using Timer = System.Windows.Forms.Timer;

static class Native
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    public const uint WM_CLOSE = 0x0010;

    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr hMod, uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public const int SW_MINIMIZE = 6, SW_RESTORE = 9;
}

// The taskbar's own API for adding/removing a window's button (used for "Moonlight in the tray").
[ComImport, Guid("56FDF342-FD6D-11d0-958A-006097C9A090"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ITaskbarList
{
    void HrInit();
    void AddTab(IntPtr hwnd);
    void DeleteTab(IntPtr hwnd);
    void ActivateTab(IntPtr hwnd);
    void SetActiveAlt(IntPtr hwnd);
}
[ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
class TaskbarListClass { }

// ---------------------------------------------------------------- network + log

static class Link
{
    public const int SwitchPort = 47710, ServerPort = 47711;
    const string MsgPrefix = "MOONLIGHTSWITCH/1 ";
    public static string Folder;
    static readonly object logLock = new object();

    // Small activity log next to the exe (log.txt), cleared when it gets big.
    public static void Log(string message)
    {
        try
        {
            lock (logLock)
            {
                string path = Path.Combine(Folder, "log.txt");
                if (File.Exists(path) && new FileInfo(path).Length > 200000) File.Delete(path);
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
            }
        }
        catch { }
    }

    // True while Moonlight has a stream window open (minimized counts).
    public static bool IsStreaming()
    {
        var pids = new HashSet<uint>();
        foreach (var p in Process.GetProcessesByName("Moonlight")) { pids.Add((uint)p.Id); p.Dispose(); }
        if (pids.Count == 0) return false;
        bool found = false;
        Native.EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            if (!pids.Contains(pid) || !Native.IsWindowVisible(h)) return true;
            var cls = new StringBuilder(128);
            Native.GetClassName(h, cls, cls.Capacity);
            if (cls.ToString().StartsWith("SDL")) { found = true; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    static long FileVersion(string name)
    {
        try { return File.GetLastWriteTimeUtc(Path.Combine(Folder, name)).Ticks; } catch { return 0; }
    }

    static byte[] StripBom(byte[] b)
    {
        if (b.Length < 3 || b[0] != 0xEF || b[1] != 0xBB || b[2] != 0xBF) return b;
        var trimmed = new byte[b.Length - 3];
        Array.Copy(b, 3, trimmed, 0, trimmed.Length);
        return trimmed;
    }

    // Serves the host bar's files to Sunshine/the host. Files are read fresh on every request,
    // so edits apply right away. Only these paths are served.
    public static void Serve()
    {
        var thread = new Thread(delegate ()
        {
            TcpListener listener;
            try { listener = new TcpListener(IPAddress.Any, ServerPort); listener.Start(); }
            catch { return; }
            while (true)
            {
                try
                {
                    using (var c = listener.AcceptTcpClient())
                    {
                        c.ReceiveTimeout = 3000;
                        var s = c.GetStream();
                        var reader = new StreamReader(s, Encoding.UTF8);
                        string request = reader.ReadLine() ?? "";
                        while (!string.IsNullOrEmpty(reader.ReadLine())) { }   // skip the headers

                        string[] parts = request.Split(' ');
                        string path = parts.Length >= 2 ? parts[1] : "/";
                        int q = path.IndexOf('?');
                        if (q >= 0) path = path.Substring(0, q);
                        bool quiet = path == "/status" || path == "/state";
                        if (!quiet) Log("request from " + c.Client.RemoteEndPoint + ": " + request);

                        byte[] body = null;
                        if (path == "/status")
                            body = Encoding.ASCII.GetBytes(IsStreaming() ? "streaming" : "idle");
                        else if (path == "/state")
                            // streaming/idle, settings.ini version, version of the bar's code (Bar.cs + host.ps1)
                            body = Encoding.ASCII.GetBytes((IsStreaming() ? "streaming" : "idle") + "\n" + FileVersion("settings.ini") +
                                                           "\n" + (FileVersion("Bar.cs") ^ FileVersion("host.ps1")));
                        else if (path == "/install-host.ps1")
                        {
                            // Run once on the host: starts the bar at every login (and right now).
                            string me = ((IPEndPoint)c.Client.LocalEndPoint).Address.ToString();
                            // The loop notes its start in %TEMP%\MoonlightSwitch-loop.log, so a missing loop can
                            // be told apart from one that never started.
                            string loop = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"ac ($env:TEMP+'\\MoonlightSwitch-loop.log') ((Get-Date -f s)+' loop start '+$PID); " +
                                          "while ($true) { try { iex (Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 http://" +
                                          me + ":" + ServerPort + "/boot.ps1).Content } catch {}; Start-Sleep 10 }\"";
                            body = Encoding.UTF8.GetBytes(
                                "$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'Moonlight Switch bar.lnk'\n" +
                                "$s = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)\n" +
                                "$s.TargetPath = \"$env:WINDIR\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\"\n" +
                                "$s.Arguments = '" + loop.Replace("'", "''") + "'\n" +
                                "$s.WindowStyle = 7\n" +
                                "$s.Description = 'Moonlight Switch host bar (downloads the bar from " + me + ")'\n" +
                                "$s.Save()\n" +
                                // Stop a loop that's already running (and the bar it started), so only one is left.
                                "$all = @(Get-CimInstance Win32_Process -Filter \"Name='powershell.exe'\")\n" +
                                "$loops = @($all | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -match 'boot\\.ps1' })\n" +
                                "$bars = @($all | Where-Object { $loops.ProcessId -contains $_.ParentProcessId })\n" +
                                "foreach ($p in $loops + $bars) { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue }\n" +
                                "Start-Process $s.TargetPath -ArgumentList $s.Arguments -WindowStyle Hidden\n" +
                                "Write-Host \"Installed: $lnk\"\n" +
                                "Write-Host 'The bar now starts at every login. To remove it, delete that shortcut and restart.'\n");
                        }
                        else if (path == "/boot.ps1")
                        {
                            // One round of the host's login loop: run the bar in its own process and wait
                            // until it exits (it exits by itself when its code changes, to pick up the update).
                            string me = ((IPEndPoint)c.Client.LocalEndPoint).Address.ToString();
                            string barCmd = "& ([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 http://" + me + ":" + ServerPort +
                                            "/host.ps1).Content)) -Client " + me;
                            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(barCmd));
                            // A bar that ran for a while and then closed (Restart this bar, or a code update)
                            // starts again right away. One that fails quickly (client unreachable, broken code)
                            // falls back to the login loop's 10 s retry.
                            // Each start and exit goes to %TEMP%\MoonlightSwitch-loop.log (kept small).
                            body = Encoding.UTF8.GetBytes(
                                "$log = Join-Path $env:TEMP 'MoonlightSwitch-loop.log'\n" +
                                "if ((Test-Path $log) -and (Get-Item $log).Length -gt 100KB) { Remove-Item $log -ErrorAction SilentlyContinue }\n" +
                                "do {\n" +
                                "  $t = Get-Date\n" +
                                "  $p = Start-Process powershell -WindowStyle Hidden -Wait -PassThru -ArgumentList '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " + encoded + "'\n" +
                                "  $secs = [int]((Get-Date) - $t).TotalSeconds\n" +
                                "  Add-Content $log ((Get-Date -f s) + ' bar ' + $p.Id + ' exited with ' + $p.ExitCode + ' after ' + $secs + ' s')\n" +
                                "} while ($secs -gt 20)\n");
                        }
                        else
                        {
                            string file = path == "/host.ps1" ? "host.ps1" : path == "/bar.cs" ? "Bar.cs" : path == "/settings.ini" ? "settings.ini" : null;
                            if (file != null)
                            {
                                string full = Path.Combine(Folder, file);
                                if (File.Exists(full)) body = StripBom(File.ReadAllBytes(full));
                            }
                        }

                        string status = body != null ? "200 OK" : "404 Not Found";
                        if (body == null) body = Encoding.ASCII.GetBytes("Not found");
                        string head = "HTTP/1.1 " + status + "\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: " + body.Length +
                                      "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
                        byte[] hb = Encoding.ASCII.GetBytes(head);
                        s.Write(hb, 0, hb.Length);
                        s.Write(body, 0, body.Length);
                    }
                }
                catch { }
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    // Calls onCommand (on a background thread) with each request from the host bar:
    // "CLIENT", "QUIT", "DISCONNECT" or "KEY x".
    public static void Listen(Action<string> onCommand)
    {
        var thread = new Thread(delegate ()
        {
            UdpClient udp;
            try { udp = new UdpClient(new IPEndPoint(IPAddress.Any, SwitchPort)); }
            catch { return; }
            while (true)
            {
                try
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udp.Receive(ref from);
                    string msg = Encoding.ASCII.GetString(data);
                    if (msg.StartsWith(MsgPrefix)) onCommand(msg.Substring(MsgPrefix.Length).Trim());
                }
                catch { Thread.Sleep(200); }
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }
}

// ---------------------------------------------------------------- Moonlight + switching

class Switcher
{
    HashSet<uint> pids = new HashSet<uint>();
    DateTime lastPidRefresh = DateTime.MinValue;

    public IntPtr Stream { get; private set; }
    public bool HasStream { get { return Stream != IntPtr.Zero; } }
    public bool MoonlightRunning { get { return pids.Count > 0; } }

    public void Refresh()
    {
        if ((DateTime.Now - lastPidRefresh).TotalMilliseconds >= 1000)
        {
            lastPidRefresh = DateTime.Now;
            var fresh = new HashSet<uint>();
            foreach (var p in Process.GetProcessesByName("Moonlight")) { fresh.Add((uint)p.Id); p.Dispose(); }
            pids = fresh;
        }
        Stream = FindStreamWindow();
    }

    // Prefer the SDL streaming window; fall back to the main Moonlight window.
    IntPtr FindStreamWindow()
    {
        if (pids.Count == 0) return IntPtr.Zero;
        IntPtr sdl = IntPtr.Zero, any = IntPtr.Zero;
        long bestArea = 0;
        Native.EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            if (!pids.Contains(pid) || !Native.IsWindowVisible(h) || Native.GetWindowTextLength(h) == 0) return true;
            var cls = new StringBuilder(128);
            Native.GetClassName(h, cls, cls.Capacity);
            if (cls.ToString().StartsWith("SDL")) { sdl = h; return false; }
            Native.RECT r;
            Native.GetWindowRect(h, out r);
            long area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
            if (area > bestArea) { bestArea = area; any = h; }
            return true;
        }, IntPtr.Zero);
        return sdl != IntPtr.Zero ? sdl : any;
    }

    public bool HostActive()
    {
        return Stream != IntPtr.Zero && !Native.IsIconic(Stream) && Native.GetForegroundWindow() == Stream;
    }

    public void Toggle()
    {
        Refresh();
        if (HostActive()) SwitchToClient(); else SwitchToHost();
    }

    public void SwitchToHost()
    {
        Refresh();
        if (Stream == IntPtr.Zero) return;
        if (IsStreamWindow(Stream)) EnsureTab(Stream);   // before it shows, so Windows treats it as full screen
        if (Native.IsIconic(Stream)) Native.ShowWindow(Stream, Native.SW_RESTORE);
        // A tap of Alt lets Windows hand focus to another app's window.
        Native.keybd_event(0x12, 0, 0, UIntPtr.Zero);
        Native.keybd_event(0x12, 0, 2, UIntPtr.Zero);
        Native.SetForegroundWindow(Stream);
        Link.Log("switch: to host");
    }

    public void SwitchToClient()
    {
        Refresh();
        if (Stream == IntPtr.Zero) return;
        IntPtr target = Stream;
        Link.Log("switch: to client");
        if (!HostActive()) { Native.ShowWindow(target, Native.SW_MINIMIZE); return; }

        // Moonlight's own minimize shortcut (Ctrl+Alt+Shift+D) releases the mouse and keyboard;
        // minimizing the window from outside doesn't.
        PressCombo('D');
        var check = new Timer { Interval = 150 };
        check.Tick += delegate
        {
            check.Stop();
            check.Dispose();
            if (!Native.IsIconic(target)) Native.ShowWindow(target, Native.SW_MINIMIZE);
            // A minimized Moonlight can keep the focus, and it only lets go of the mouse once
            // another window has it, so hand the focus over explicitly.
            if (Native.GetForegroundWindow() == target) FocusAwayFrom(target);
        };
        check.Start();
    }

    // ---------- Moonlight's Ctrl+Alt+Shift shortcuts ----------

    // Presses Ctrl+Alt+Shift+key in the stream, bringing the stream to the front first if needed.
    public void PressShortcut(char key)
    {
        Refresh();
        if (!IsStreamWindow(Stream)) return;
        Link.Log("shortcut: Ctrl+Alt+Shift+" + key);
        if (key == 'D') { SwitchToClient(); return; }   // our minimize also hands the focus back
        if (HostActive()) { PressCombo(key); return; }
        SwitchToHost();
        var wait = new Timer { Interval = 300 };
        wait.Tick += delegate
        {
            wait.Stop();
            wait.Dispose();
            if (HostActive()) PressCombo(key);
        };
        wait.Start();
    }

    // ---------- quitting ----------

    // Ends the stream (like closing its window), then closes Moonlight itself.
    // Ends the stream and leaves Moonlight open (ready to reconnect).
    public void Disconnect()
    {
        Refresh();
        Link.Log("disconnect");
        CloseStreamWindow();
    }

    IntPtr CloseStreamWindow()
    {
        IntPtr stream = IntPtr.Zero;
        foreach (var h in MoonlightWindows()) if (IsStreamWindow(h)) stream = h;
        if (stream != IntPtr.Zero) Native.PostMessage(stream, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        return stream;
    }

    // Ends the stream, then closes Moonlight itself. Moonlight shows its main window again only a
    // moment after the stream window goes away, so keep asking it to close until it has exited.
    public void QuitMoonlight()
    {
        Refresh();
        Link.Log("quit Moonlight");
        IntPtr stream = CloseStreamWindow();
        int waited = 0;
        var poll = new Timer { Interval = 250 };
        poll.Tick += delegate
        {
            waited += 250;
            if (stream != IntPtr.Zero && Native.IsWindow(stream) && waited < 6000) return;   // stream still ending
            Refresh();
            if (!MoonlightRunning) { poll.Stop(); poll.Dispose(); return; }
            if (waited % 1000 == 0)
                foreach (var h in MoonlightWindows(true))
                    if (!IsStreamWindow(h)) Native.PostMessage(h, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            if (waited >= 15000)
            {
                // Still running after 15 s: it's no longer streaming, so ending it is safe.
                Link.Log("quit Moonlight: did not close by itself, ending it");
                foreach (var p in Process.GetProcessesByName("Moonlight")) { try { p.Kill(); } catch { } p.Dispose(); }
                poll.Stop();
                poll.Dispose();
            }
        };
        poll.Start();
    }

    // ---------- tray mode ----------

    ITaskbarList taskbar;
    readonly HashSet<IntPtr> removedTabs = new HashSet<IntPtr>();
    readonly HashSet<IntPtr> shownTabs = new HashSet<IntPtr>();

    // Hides the stream's taskbar button (or brings it back). Moonlight's main window (the PC list)
    // keeps its button. Windows re-adds a button now and then (e.g. when a window is restored),
    // so this is called regularly.
    public void UpdateTaskbar(bool hide)
    {
        if (!hide) { RestoreTaskbar(); return; }
        if (taskbar == null)
        {
            try { taskbar = (ITaskbarList)new TaskbarListClass(); taskbar.HrInit(); }
            catch { taskbar = null; return; }
        }
        foreach (var h in MoonlightWindows())
        {
            // Windows only hides its taskbar for a full-screen window that has a taskbar button,
            // so the stream keeps its button while it's showing, and loses it only while minimized.
            if (!IsStreamWindow(h))
            {
                // Once per window, in case an earlier copy of this app hid its button.
                if (shownTabs.Add(h)) { try { taskbar.AddTab(h); } catch { } removedTabs.Remove(h); }
                continue;
            }
            if (!Native.IsIconic(h)) { EnsureTab(h); continue; }
            try { taskbar.DeleteTab(h); removedTabs.Add(h); } catch { }
        }
    }

    void EnsureTab(IntPtr h)
    {
        if (taskbar == null || !removedTabs.Contains(h)) return;
        try { taskbar.AddTab(h); } catch { }
        removedTabs.Remove(h);
    }

    public void RestoreTaskbar()
    {
        if (taskbar == null) return;
        foreach (var h in removedTabs)
        {
            try { if (Native.IsWindow(h)) taskbar.AddTab(h); } catch { }
        }
        removedTabs.Clear();
    }

    // Tray icon click: show Moonlight (the stream, or its main window), or tuck it away again.
    public void ShowOrHideMoonlight()
    {
        Refresh();
        if (Stream == IntPtr.Zero) return;
        if (IsStreamWindow(Stream)) { if (HostActive()) SwitchToClient(); else SwitchToHost(); return; }
        if (!Native.IsIconic(Stream) && Native.GetForegroundWindow() == Stream) { Native.ShowWindow(Stream, Native.SW_MINIMIZE); return; }
        SwitchToHost();   // restores and focuses the main window the same way
    }

    // ---------- helpers ----------

    public List<IntPtr> MoonlightWindows(bool includeHidden = false)
    {
        var list = new List<IntPtr>();
        if (pids.Count == 0) return list;
        Native.EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            if (pids.Contains(pid) && (includeHidden || Native.IsWindowVisible(h)) && Native.GetWindowTextLength(h) > 0) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static bool IsStreamWindow(IntPtr h)
    {
        if (h == IntPtr.Zero) return false;
        var cls = new StringBuilder(128);
        Native.GetClassName(h, cls, cls.Capacity);
        return cls.ToString().StartsWith("SDL");
    }

    static void PressCombo(char key)
    {
        byte[] keys = { 0xA2 /* LCtrl */, 0xA4 /* LAlt */, 0xA0 /* LShift */, (byte)char.ToUpperInvariant(key) };
        foreach (byte k in keys) Native.keybd_event(k, (byte)Native.MapVirtualKey(k, 0), 0, UIntPtr.Zero);
        for (int i = keys.Length - 1; i >= 0; i--) Native.keybd_event(keys[i], (byte)Native.MapVirtualKey(keys[i], 0), 2 /* KEYUP */, UIntPtr.Zero);
    }

    // Give the focus to the window you used before streaming, or the desktop.
    void FocusAwayFrom(IntPtr moonlightWindow)
    {
        IntPtr next = IntPtr.Zero;
        Native.EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            if (h == moonlightWindow || !Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
            if (Native.GetWindowTextLength(h) == 0) return true;
            if ((Native.GetWindowLong(h, -20) & 0x80) != 0) return true;   // tool windows (like our bar)
            int cloaked;
            if (Native.DwmGetWindowAttribute(h, 14 /* DWMWA_CLOAKED */, out cloaked, 4) == 0 && cloaked != 0) return true;
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            if (pids.Contains(pid)) return true;
            next = h;
            return false;
        }, IntPtr.Zero);
        if (next == IntPtr.Zero) next = Native.FindWindow("Progman", null);   // the desktop
        if (next == IntPtr.Zero) return;

        // Windows only lets the app that has the focus give it away, so borrow
        // Moonlight's input queue for a moment while switching.
        uint dummy;
        uint fgThread = Native.GetWindowThreadProcessId(moonlightWindow, out dummy);
        uint me = Native.GetCurrentThreadId();
        bool attached = fgThread != me && Native.AttachThreadInput(me, fgThread, true);
        Native.SetForegroundWindow(next);
        if (attached) Native.AttachThreadInput(me, fgThread, false);
    }
}

// ---------------------------------------------------------------- Ctrl+Alt+Home

// Works even while Moonlight has captured the keyboard: Moonlight installs its own keyboard
// hook when it captures input and the newest hook runs first, so we re-install ours regularly.
static class Hotkey
{
    static Native.HookProc proc;   // kept in a field so it isn't garbage collected
    static IntPtr hook = IntPtr.Zero;
    static bool ctrlDown, altDown, swallowingHome;
    static Control invoker;
    static Action onToggle;

    public static void Install(Control target, Action toggle)
    {
        invoker = target;
        onToggle = toggle;
        proc = Callback;
        Rehook();
        var timer = new Timer { Interval = 1500 };
        timer.Tick += delegate { Rehook(); };
        timer.Start();
    }

    public static void Remove()
    {
        if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
    }

    static void Rehook()
    {
        IntPtr fresh = Native.SetWindowsHookEx(13 /* WH_KEYBOARD_LL */, proc, Native.GetModuleHandle(null), 0);
        if (fresh == IntPtr.Zero) return;
        if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
        hook = fresh;
    }

    static IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
            int msg = wParam.ToInt32();
            bool down = msg == 0x100 || msg == 0x104;
            bool up = msg == 0x101 || msg == 0x105;
            Keys vk = (Keys)k.vkCode;

            // Track modifiers ourselves: Moonlight may swallow them before Windows sees them.
            if (vk == Keys.LControlKey || vk == Keys.RControlKey) { if (down) ctrlDown = true; if (up) ctrlDown = false; }
            if (vk == Keys.LMenu || vk == Keys.RMenu) { if (down) altDown = true; if (up) altDown = false; }

            if (vk == Keys.Home)
            {
                if (down && ctrlDown && altDown)
                {
                    if (!swallowingHome && invoker != null && invoker.IsHandleCreated)
                        invoker.BeginInvoke((MethodInvoker)delegate { onToggle(); });
                    swallowingHome = true;
                    return new IntPtr(1);   // don't pass Home to the host
                }
                if (up && swallowingHome) { swallowingHome = false; return new IntPtr(1); }
            }
        }
        return Native.CallNextHookEx(hook, code, wParam, lParam);
    }
}

// ---------------------------------------------------------------- the app

class ClientApp
{
    readonly Switcher sw = new Switcher();
    readonly DateTime startedAt = DateTime.Now;
    BarWindow bar;
    NotifyIcon tray;
    string settingsPath;
    DateTime settingsStamp, lastSettingsCheck, lastTaskbarUpdate;
    readonly Dictionary<string, DateTime> lastCommand = new Dictionary<string, DateTime>();
    bool seenMoonlight;

    public void Run()
    {
        settingsPath = Path.Combine(Link.Folder, "settings.ini");
        if (!File.Exists(settingsPath))
        {
            try { File.WriteAllText(settingsPath, BarConfig.DefaultText); } catch { }
        }

        var buttons = new List<BarButton>();
        buttons.Add(new BarButton(c => c.HostLabel, Glyphs.Host, () => sw.SwitchToHost(), () => sw.HostActive(), () => sw.HasStream));
        buttons.Add(new BarButton(c => c.ClientLabel, Glyphs.Client, () => sw.SwitchToClient(), () => sw.HasStream && !sw.HostActive(), () => sw.HasStream));
        BarButton shortcuts = null;
        shortcuts = new BarButton(c => c.ShortcutsLabel, Glyphs.More,
            () =>
            {
                var items = BarWindow.ShortcutItems(bar.Config, k => sw.PressShortcut(k));
                items.Add(new ToolStripSeparator());
                items.Add(new ToolStripMenuItem("Disconnect (keep Moonlight open)", null, delegate { sw.Disconnect(); }));
                bar.ShowItems(items, shortcuts);
            },
            null, () => Switcher.IsStreamWindow(sw.Stream));
        shortcuts.GapBefore = true;
        shortcuts.IsVisible = c => c.ShowShortcutsButton;
        buttons.Add(shortcuts);
        BarButton quit = BarButton.Quit(() => sw.QuitMoonlight(), () => sw.MoonlightRunning, () => bar.Config.ConfirmQuit);
        quit.GapBefore = false;
        buttons.Add(quit);

        bar = new BarWindow("client", LoadSettings(), buttons, Path.Combine(Link.Folder, "position-client.txt"),
                            () => sw.HasStream ? Screen.FromHandle(sw.Stream) : Screen.PrimaryScreen, MenuItems);

        // The tray icon stands in for Moonlight: click it to show or tuck away Moonlight.
        Icon icon = SystemIcons.Application;
        try { string exe = Program.MoonlightExe(); if (exe != null) icon = Icon.ExtractAssociatedIcon(exe); } catch { }
        tray = new NotifyIcon
        {
            Icon = icon,
            Text = "Moonlight (click to show or hide; Ctrl+Alt+Home switches)",
            Visible = true,
        };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Opening += delegate
        {
            // Rebuilt each time so it follows settings.ini.
            trayMenu.Items.Clear();
            trayMenu.Items.Add(new ToolStripMenuItem("Show / hide Moonlight", null, delegate { sw.ShowOrHideMoonlight(); }));
            foreach (var it in MenuItems()) trayMenu.Items.Add(it);
        };
        trayMenu.Items.Add("...");   // placeholder so the menu opens; replaced in Opening
        tray.ContextMenuStrip = trayMenu;
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) sw.ShowOrHideMoonlight(); };

        bar.Load += delegate
        {
            Hotkey.Install(bar, () => sw.Toggle());
            Link.Listen(cmd => { try { bar.BeginInvoke((MethodInvoker)delegate { OnRemoteCommand(cmd); }); } catch { } });
            Link.Serve();
        };

        var timer = new Timer { Interval = 100 };
        timer.Tick += delegate { OwnerTick(); };
        timer.Start();

        Application.Run(bar);

        Hotkey.Remove();
        sw.RestoreTaskbar();   // never leave Moonlight without its taskbar buttons
        tray.Visible = false;
        tray.Dispose();
    }

    List<ToolStripItem> MenuItems()
    {
        var items = new List<ToolStripItem>();
        items.Add(new ToolStripMenuItem("Switch to Host", null, delegate { sw.SwitchToHost(); }));
        items.Add(new ToolStripMenuItem("Switch to Client", null, delegate { sw.SwitchToClient(); }));
        var sub = new ToolStripMenuItem("Moonlight shortcuts");
        foreach (var it in BarWindow.ShortcutItems(bar.Config, k => sw.PressShortcut(k))) sub.DropDownItems.Add(it);
        sub.Enabled = Switcher.IsStreamWindow(sw.Stream);
        items.Add(sub);
        items.Add(new ToolStripSeparator());
        items.Add(new ToolStripMenuItem("Edit settings", null, delegate
        {
            try { Process.Start("notepad.exe", "\"" + settingsPath + "\""); } catch { }
        }));
        var disconnect = new ToolStripMenuItem("Disconnect (keep Moonlight open)", null, delegate { sw.Disconnect(); });
        disconnect.Enabled = Switcher.IsStreamWindow(sw.Stream);
        items.Add(disconnect);
        items.Add(new ToolStripMenuItem("Quit Moonlight", null, delegate { sw.QuitMoonlight(); }));
        items.Add(new ToolStripMenuItem("Close the switch bar only", null, delegate { bar.Close(); }));
        return items;
    }

    BarConfig LoadSettings()
    {
        try
        {
            settingsStamp = File.GetLastWriteTimeUtc(settingsPath);
            return BarConfig.Parse(File.ReadAllText(settingsPath));
        }
        catch { return new BarConfig(); }
    }

    void OwnerTick()
    {
        sw.Refresh();

        // Live and die with Moonlight: close once it exits (or if it never starts).
        if (sw.MoonlightRunning) seenMoonlight = true;
        else if (seenMoonlight || (DateTime.Now - startedAt).TotalSeconds > 30) { bar.Close(); return; }

        // While you're on the host, the host's own bar takes over.
        bar.OwnerHidden = sw.HostActive();

        // Tray mode: keep Moonlight's taskbar buttons hidden.
        if ((DateTime.Now - lastTaskbarUpdate).TotalMilliseconds > 250)
        {
            lastTaskbarUpdate = DateTime.Now;
            sw.UpdateTaskbar(bar.Config.MoonlightInTray);
        }

        // Apply settings.ini edits live.
        if ((DateTime.Now - lastSettingsCheck).TotalMilliseconds > 1000)
        {
            lastSettingsCheck = DateTime.Now;
            try
            {
                DateTime stamp = File.GetLastWriteTimeUtc(settingsPath);
                if (stamp != settingsStamp) bar.ApplyConfig(LoadSettings());
            }
            catch { }
        }
    }

    // Requests from the host bar. The host sends each one to several addresses,
    // so the same request can arrive more than once; act on it once.
    void OnRemoteCommand(string cmd)
    {
        DateTime last;
        if (lastCommand.TryGetValue(cmd, out last) && (DateTime.Now - last).TotalMilliseconds < 800) return;
        lastCommand[cmd] = DateTime.Now;
        Link.Log("request from host: " + cmd);

        if (cmd == "CLIENT") sw.SwitchToClient();
        else if (cmd == "QUIT") sw.QuitMoonlight();
        else if (cmd == "DISCONNECT") sw.Disconnect();
        else if (cmd.StartsWith("KEY ") && cmd.Length == 5 && char.IsLetter(cmd[4])) sw.PressShortcut(char.ToUpperInvariant(cmd[4]));
    }
}

static class Program
{
    public static string MoonlightExe()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Moonlight Game Streaming\Moonlight.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Moonlight Game Streaming\Moonlight.exe"),
        };
        foreach (var exe in candidates) if (File.Exists(exe)) return exe;
        return null;
    }

    static void StartMoonlightIfNeeded()
    {
        Process[] running = Process.GetProcessesByName("Moonlight");
        bool isRunning = running.Length > 0;
        foreach (var p in running) p.Dispose();
        if (isRunning) return;

        string exe = MoonlightExe();
        if (exe != null)
        {
            try { Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe) }); } catch { }
            return;
        }
        MessageBox.Show("Couldn't find Moonlight.exe.", "Moonlight Switch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    [STAThread]
    static void Main()
    {
        // Draw at the screen's real resolution (sharp at 150%/200% scaling).
        try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
        Link.Folder = Path.GetDirectoryName(Application.ExecutablePath);

        StartMoonlightIfNeeded();

        bool created;
        using (new Mutex(true, "MoonlightSwitch_Client", out created))
        {
            // Already running (e.g. the pinned shortcut was clicked again): bring Moonlight up,
            // the stream if there is one, otherwise its main window.
            if (!created) { try { new Switcher().SwitchToHost(); } catch { } return; }
            Application.EnableVisualStyles();
            new ClientApp().Run();
        }
    }
}
