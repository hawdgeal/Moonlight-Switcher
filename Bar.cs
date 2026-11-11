// Bar.cs - the look and behaviour of both MoonlightSwitch bars.
//
// Compiled into MoonlightSwitch.exe (the bar on this PC) and MoonlightSwitchHost.exe (the bar on
// the host), so editing this file changes both. Colors, labels and sizes live in settings.ini,
// which both bars reload live. After editing this file, run build.cmd, then reinstall the host
// bar from the install page (its menu says when an update is available).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MoonlightSwitchUI
{
    public enum Edge { Top, Bottom, Left, Right }

    // ---------------------------------------------------------------- settings

    public class BarConfig
    {
        public Color Background = Color.FromArgb(240, 30, 30, 36);
        public Color Foreground = Color.FromArgb(240, 240, 245);
        public Color Accent = Color.FromArgb(90, 140, 255);
        public Color ActiveText = Color.White;
        public Color HandleColor = Color.FromArgb(90, 140, 255);
        public double Opacity = 0.97;
        public double IdleOpacity = 0.7;
        public double Scale = 1.0;
        public double CornerRadius = 12;
        public string FontName = "Segoe UI Semibold";
        public double FontSize = 9.5;
        public bool ShowIcons = true;
        public bool ShowLabels = true;
        public bool Shadow = true;
        public bool Animate = true;
        public int HideDelayMs = 700;
        public bool PinOpen = false;
        public double HandleLength = 80;
        public double HandleThickness = 5;
        public Edge ClientEdge = Edge.Top;
        public double ClientOffset = 0.5;
        public Edge HostEdge = Edge.Top;
        public double HostOffset = 0.5;
        public string HostLabel = "Host";
        public string ClientLabel = "Client";
        public string BackLabel = "Back to my PC";
        public string ShortcutsLabel = "";
        public string QuitLabel = "";
        public string ConfirmQuitLabel = "Quit";
        public Color Danger = Color.FromArgb(229, 72, 77);
        public bool ShowShortcutsButton = true;
        public bool ShowQuitButton = true;
        public bool ConfirmQuit = true;
        public bool ShowTooltips = true;
        public bool ShowClientPanelButton = true;
        public string ClientPanelLabel = "";
        public bool MoonlightInTray = true;
        public string MoonlightResolution = "";   // "screen" = match this PC's screen when starting Moonlight
        public string HostKeyboardStream = "";   // keyboard layout (8 hex digits) on the host while streaming
        public string HostKeyboardIdle = "";     // ...and after the stream

        // Moonlight's Ctrl+Alt+Shift shortcuts offered in the shortcuts menu: label + key.
        public List<KeyValuePair<string, char>> Shortcuts = DefaultShortcuts();
        bool shortcutsFromFile;

        static List<KeyValuePair<string, char>> DefaultShortcuts()
        {
            var l = new List<KeyValuePair<string, char>>();
            l.Add(new KeyValuePair<string, char>("Disconnect (keep the host session)", 'Q'));
            l.Add(new KeyValuePair<string, char>("Minimize the stream", 'D'));
            l.Add(new KeyValuePair<string, char>("Toggle mouse and keyboard capture", 'Z'));
            l.Add(new KeyValuePair<string, char>("Toggle full screen / window", 'X'));
            l.Add(new KeyValuePair<string, char>("Toggle performance stats", 'S'));
            l.Add(new KeyValuePair<string, char>("Toggle mouse mode", 'M'));
            l.Add(new KeyValuePair<string, char>("Toggle local cursor", 'L'));
            l.Add(new KeyValuePair<string, char>("Type the clipboard text", 'V'));
            return l;
        }

        public const string DefaultText =
@"# MoonlightSwitch bar settings
# Used by both bars: the one on this PC and the one on the host. Changes apply live
# within a few seconds. Colors are #RRGGBB, or #AARRGGBB to add transparency.

# --- Colors ---
Background   = #F01E1E24
Foreground   = #F0F0F5
Accent       = #5A8CFF
ActiveText   = #FFFFFF
HandleColor  = #5A8CFF

# --- Look ---
Opacity      = 0.97     # bar opacity when open (0.1 - 1)
IdleOpacity  = 0.7      # handle opacity when tucked away
Scale        = 1.0      # overall size, on top of Windows display scaling
CornerRadius = 12
Font         = Segoe UI Semibold
FontSize     = 9.5
ShowIcons    = true
ShowLabels   = true     # false = icon-only buttons (or per bar: right-click > Buttons > Icons only)
ShowTooltips = true     # names (and the client tray's details) when you point at something
Shadow       = true
Animate      = true

# --- Behaviour ---
HideDelayMs     = 700   # how long the bar stays open after the mouse leaves
PinOpen         = false # true = never tuck away
HandleLength    = 80    # size of the tucked-away handle
HandleThickness = 5

# --- Default positions ---
# Dragging a bar overrides these; right-click a bar > Reset position to come back here.
ClientEdge   = Top      # Top, Bottom, Left or Right
ClientOffset = 0.5      # 0 = start of the edge, 0.5 = middle, 1 = end
HostEdge     = Top
HostOffset   = 0.5

# --- Labels ---
HostLabel    = Host
ClientLabel  = Client
BackLabel    = Back to my PC
ShortcutsLabel   =                  # empty = icon only
QuitLabel        =                  # empty = icon only
ConfirmQuitLabel = Quit             # shown after the first click; click again to quit

# --- Extra buttons ---
ShowShortcutsButton = true
ShowQuitButton      = true
ConfirmQuit         = true          # false = the power button quits on the first click
Danger              = #E5484D       # color of the quit confirmation
# Host bar: a button that opens a mini copy of this PC's taskbar corner (keyboard layout, Wi-Fi,
# volume, battery, time). Click its icons for volume and brightness sliders. Drag it anywhere.
ShowClientPanelButton = true
ClientPanelLabel      =             # empty = icon only

# --- Moonlight ---
MoonlightInTray = true   # hide Moonlight's taskbar buttons; use the tray icon instead
# Stream size Moonlight asks the host for. ""screen"" = this PC's screen size, set each time this app
# starts Moonlight (so it also follows a virtual screen, e.g. when this PC is itself being streamed).
# Empty = keep Moonlight's own resolution setting.
MoonlightResolution =

# --- Host ---
# Commands the host runs when a stream starts or ends are set on the host itself, in
# %LOCALAPPDATA%\MoonlightSwitch\host.ini, never here.
# Keyboard layout on the host while streaming. ""client"" = the layout you're typing with on this PC
# (it follows if you switch layouts mid-stream), or a layout id as 8 hex digits (e.g. 04090409 = US).
# It must be installed on the host. Leave empty to not switch.
# HostKeyboardIdle: the layout after the stream. Leave empty to go back to the host's own one.
HostKeyboardStream =
HostKeyboardIdle   =

# --- Shortcuts menu ---
# Each line: Shortcut = <label> | <key>, sent as Ctrl+Alt+Shift+<key> to Moonlight.
# Listing any Shortcut lines here replaces the built-in list.
Shortcut = Disconnect (keep the host session) | Q
Shortcut = Minimize the stream | D
Shortcut = Toggle mouse and keyboard capture | Z
Shortcut = Toggle full screen / window | X
Shortcut = Toggle performance stats | S
Shortcut = Toggle mouse mode | M
Shortcut = Toggle local cursor | L
Shortcut = Type the clipboard text | V
";

        public static BarConfig Parse(string text)
        {
            var c = new BarConfig();
            if (string.IsNullOrEmpty(text)) return c;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();
                int comment = val.IndexOf(" #");
                if (comment >= 0) val = val.Substring(0, comment).Trim();
                if (val.StartsWith("# ") || val == "#") val = "";   // empty value followed by a comment
                try { c.Set(key, val); } catch { }   // a bad value keeps its default
            }
            return c;
        }

        void Set(string k, string v)
        {
            switch (k)
            {
                case "background": Background = ParseColor(v); break;
                case "foreground": Foreground = ParseColor(v); break;
                case "accent": Accent = ParseColor(v); break;
                case "activetext": ActiveText = ParseColor(v); break;
                case "handlecolor": HandleColor = ParseColor(v); break;
                case "opacity": Opacity = Clamp(D(v), 0.1, 1); break;
                case "idleopacity": IdleOpacity = Clamp(D(v), 0.05, 1); break;
                case "scale": Scale = Clamp(D(v), 0.5, 3); break;
                case "cornerradius": CornerRadius = Clamp(D(v), 0, 50); break;
                case "font": FontName = v; break;
                case "fontsize": FontSize = Clamp(D(v), 6, 30); break;
                case "showicons": ShowIcons = B(v); break;
                case "showlabels": ShowLabels = B(v); break;
                case "shadow": Shadow = B(v); break;
                case "animate": Animate = B(v); break;
                case "hidedelayms": HideDelayMs = (int)Clamp(D(v), 0, 60000); break;
                case "pinopen": PinOpen = B(v); break;
                case "handlelength": HandleLength = Clamp(D(v), 10, 1000); break;
                case "handlethickness": HandleThickness = Clamp(D(v), 1, 40); break;
                case "clientedge": ClientEdge = E(v); break;
                case "clientoffset": ClientOffset = Clamp(D(v), 0, 1); break;
                case "hostedge": HostEdge = E(v); break;
                case "hostoffset": HostOffset = Clamp(D(v), 0, 1); break;
                case "hostlabel": HostLabel = v; break;
                case "clientlabel": ClientLabel = v; break;
                case "backlabel": BackLabel = v; break;
                case "shortcutslabel": ShortcutsLabel = v; break;
                case "quitlabel": QuitLabel = v; break;
                case "confirmquitlabel": ConfirmQuitLabel = v; break;
                case "danger": Danger = ParseColor(v); break;
                case "showshortcutsbutton": ShowShortcutsButton = B(v); break;
                case "showquitbutton": ShowQuitButton = B(v); break;
                case "confirmquit": ConfirmQuit = B(v); break;
                case "showclientpanelbutton": ShowClientPanelButton = B(v); break;
                case "showtooltips": ShowTooltips = B(v); break;
                case "clientpanellabel": ClientPanelLabel = v; break;
                case "moonlightintray": MoonlightInTray = B(v); break;
                case "moonlightresolution": MoonlightResolution = v; break;
                case "hostkeyboardstream": HostKeyboardStream = v; break;
                case "hostkeyboardidle": HostKeyboardIdle = v; break;
                case "shortcut":
                {
                    int bar = v.LastIndexOf('|');
                    if (bar <= 0) break;
                    string label = v.Substring(0, bar).Trim();
                    string key = v.Substring(bar + 1).Trim().ToUpperInvariant();
                    if (label.Length == 0 || key.Length != 1 || key[0] < 'A' || key[0] > 'Z') break;
                    if (!shortcutsFromFile) { Shortcuts = new List<KeyValuePair<string, char>>(); shortcutsFromFile = true; }
                    Shortcuts.Add(new KeyValuePair<string, char>(label, key[0]));
                    break;
                }
            }
        }

        public BarConfig Copy() { return (BarConfig)MemberwiseClone(); }

        // Sets one setting by its settings.ini name (e.g. "Accent", "#5A8CFF").
        public void SetValue(string key, string value) { try { Set(key.ToLowerInvariant(), value); } catch { } }

        // settings.ini text with one "Key = value" line set: the existing line is replaced (keeping
        // its comment), or a new line is added at the end.
        public static string WithLine(string text, string key, string value)
        {
            var lines = new List<string>((text ?? "").Replace("\r\n", "\n").Split('\n'));
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("#") || t.StartsWith(";")) continue;
                int eq = t.IndexOf('=');
                if (eq <= 0 || !string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase)) continue;
                string raw = lines[i];
                int rawEq = raw.IndexOf('=');
                string after = raw.Substring(rawEq + 1);
                int comment = after.IndexOf(" #");
                string tail = comment >= 0 ? "   " + after.Substring(comment).TrimStart() : "";
                lines[i] = raw.Substring(0, rawEq + 1) + " " + value + tail;
                return string.Join("\r\n", lines.ToArray());
            }
            while (lines.Count > 0 && lines[lines.Count - 1].Trim() == "") lines.RemoveAt(lines.Count - 1);
            lines.Add(key + " = " + value);
            return string.Join("\r\n", lines.ToArray()) + "\r\n";
        }

        static double D(string v) { return double.Parse(v, CultureInfo.InvariantCulture); }
        static double Clamp(double v, double lo, double hi) { return Math.Max(lo, Math.Min(hi, v)); }
        static bool B(string v) { v = v.ToLowerInvariant(); return v == "true" || v == "yes" || v == "on" || v == "1"; }
        static Edge E(string v) { return (Edge)Enum.Parse(typeof(Edge), v, true); }

        public static Color ParseColor(string v)
        {
            v = v.Trim().TrimStart('#');
            if (v.Length == 6) v = "FF" + v;
            if (v.Length != 8) throw new FormatException();
            return Color.FromArgb(unchecked((int)uint.Parse(v, NumberStyles.HexNumber)));
        }
    }

    // ---------------------------------------------------------------- buttons

    public class BarButton
    {
        public Func<BarConfig, string> Label;
        public string Glyph;
        public Action Click;
        public Func<bool> IsActive;
        public Func<bool> IsEnabled;
        public Func<BarConfig, bool> IsVisible;   // null = always shown
        public bool Danger;                       // highlight with the Danger color instead of Accent
        public bool GapBefore;                    // a little extra space before this button
        public bool Subtle;                       // dimmed until hovered (the quit button)
        public string Id = "";                    // remembers the button's place in the bar's layout file
        public string Name = "";                  // in the Buttons menu, and the tooltip
        public bool ShownByDefault = true;        // false = offered in the Buttons menu, off at first
        public Func<BarConfig, string> Tooltip;   // null = Name

        internal RectangleF Bounds;          // relative to the bar; empty when hidden
        internal float IconWidth, TextWidth, ContentWidth;

        public BarButton(Func<BarConfig, string> label, string glyph, Action click, Func<bool> isActive, Func<bool> isEnabled)
        {
            Label = label; Glyph = glyph; Click = click; IsActive = isActive; IsEnabled = isEnabled;
        }

        internal bool Active() { try { return IsActive != null && IsActive(); } catch { return false; } }
        internal bool Enabled() { try { return IsEnabled == null || IsEnabled(); } catch { return false; } }
        internal bool Visible(BarConfig c) { try { return IsVisible == null || IsVisible(c); } catch { return true; } }
        internal bool Shown { get { return Bounds.Width > 0; } }

        // A quit button. With ConfirmQuit on, the first click turns it red and asks
        // "Quit Moonlight?", and a second click within 3 seconds quits.
        public static BarButton Quit(Action doQuit, Func<bool> isEnabled, Func<bool> confirm)
        {
            bool armed = false;
            var disarm = new System.Windows.Forms.Timer { Interval = 3000 };
            disarm.Tick += delegate { disarm.Stop(); armed = false; };
            BarButton b = null;
            b = new BarButton(c => armed ? c.ConfirmQuitLabel : c.QuitLabel, Glyphs.Power, () =>
            {
                bool ask = true;
                try { ask = confirm == null || confirm(); } catch { }
                if (ask && !armed) { armed = true; disarm.Stop(); disarm.Start(); return; }
                armed = false;
                disarm.Stop();
                doQuit();
            }, () => armed, isEnabled);
            b.Danger = true;
            b.Subtle = true;
            b.GapBefore = true;
            b.IsVisible = c => c.ShowQuitButton;
            b.Id = "quit";
            b.Name = "Quit Moonlight";
            b.Tooltip = c => armed ? "Click again to quit Moonlight" : "Quit Moonlight";
            return b;
        }

        // Fills in a button's id and name; returns it, so buttons can be set up in one line.
        public BarButton Named(string id, string name, bool shownByDefault = true)
        {
            Id = id; Name = name; ShownByDefault = shownByDefault;
            return this;
        }

        // Buttons for Moonlight's shortcuts (settings.ini's Shortcut lines), off by default: turn them
        // on from the bar's Buttons menu. The same button objects are returned each time.
        public static Func<BarConfig, List<BarButton>> ShortcutButtons(Action<char> press, Func<bool> isEnabled)
        {
            var made = new Dictionary<char, BarButton>();
            return c =>
            {
                var list = new List<BarButton>();
                foreach (var s in c.Shortcuts)
                {
                    char key = s.Value;
                    BarButton b;
                    if (!made.TryGetValue(key, out b))
                    {
                        b = new BarButton(cfg => "", Glyphs.ForShortcut(key), () => press(key), null, isEnabled);
                        made[key] = b;
                    }
                    string label = s.Key;
                    b.Named("key:" + key, label, false);
                    b.Tooltip = cfg => label + " (Ctrl+Alt+Shift+" + key + ")";
                    list.Add(b);
                }
                return list;
            };
        }
    }

    // Icons from the Windows icon font (Segoe Fluent Icons / Segoe MDL2 Assets).
    public static class Glyphs
    {
        public const string Host = "\uE7F4";     // monitor
        public const string Client = "\uE977";   // PC
        public const string Back = "\uE72B";     // back arrow
        public const string More = "\uE712";     // three dots
        public const string Power = "\uE7E8";    // power
        public const string Language = "\uE8C1"; // characters (Windows' language icon)
        public const string Panel = "\uE7F8";    // the other PC's tray: a laptop
        public const string Disconnect = "\uE71A"; // stop
        public const string Sleep = "\uE708";    // moon
        public const string Keyboard = "\uE765";

        // An icon for one of Moonlight's shortcut keys (the default ones), else a keyboard.
        public static string ForShortcut(char key)
        {
            switch (char.ToUpperInvariant(key))
            {
                case 'Q': return "\uE711";   // cancel: disconnect
                case 'D': return "\uE921";   // minimize
                case 'Z': return "\uE962";   // mouse: capture
                case 'X': return "\uE740";   // full screen
                case 'S': return "\uE9D9";   // diagnostic: stats
                case 'M': return "\uE8B0";   // pointer: mouse mode
                case 'L': return "\uE7C9";   // cursor
                case 'V': return "\uE77F";   // paste
                default: return Keyboard;
            }
        }
    }

    static class BarNative
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int flags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern int GetKeyboardLayoutList(int n, IntPtr[] list);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SystemParametersInfo(uint action, uint param, ref IntPtr value, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint threadId);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint evMin, uint evMax, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    }

    // ---------------------------------------------------------------- the bar

    // A borderless, always-on-top bar drawn with per-pixel transparency. Tucks into a slim
    // handle on a screen edge, opens when the mouse comes near, can be dragged to any edge.
    public class BarWindow : Form
    {
        public BarConfig Config { get; private set; }
        public bool OwnerHidden { get; set; }   // the owner wants the bar out of the way
        public bool PinnedOpen { get; set; }    // stay open (preview mode)

        readonly string role;
        readonly List<BarButton> catalog;     // every button this bar offers
        List<BarButton> buttons;              // the ones shown, in order (from the layout file)
        List<string> order;                   // ids of the shown buttons; null = the defaults
        bool iconsOnly;
        Func<BarConfig, List<BarButton>> extraButtons;
        public Func<BarConfig, List<BarButton>> ExtraButtons   // more to offer, built from settings.ini
        {
            get { return extraButtons; }
            set { extraButtons = value; RebuildButtons(); }
        }
        readonly TipWindow tip = new TipWindow();
        int tipFor = -1, hoverWas = -1, dragButton = -1;
        long hoverSinceMs;
        readonly string positionFile;
        readonly Func<Screen> defaultScreen;
        readonly Func<List<ToolStripItem>> extraMenuItems;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Bitmap measureBitmap = new Bitmap(1, 1);
        readonly Graphics measure;

        Edge edge;
        double offset;
        string screenName;
        bool hasSavedPosition;

        float scale = 1f;
        Font font, iconFont;
        double t;                 // 0 = tucked-away handle, 1 = fully open
        bool open, menuOpen, visibleNow, dragging, dragArmed;
        long lastTickMs, lastHoverMs, lastTopmostMs;
        int hover = -1, pressed = -1;
        Point pressPoint;
        RectangleF pill, handle;
        Rectangle win;
        string lastFrameKey;

        public BarWindow(string role, BarConfig config, List<BarButton> buttons, string positionFile,
                         Func<Screen> defaultScreen, Func<List<ToolStripItem>> extraMenuItems)
        {
            this.role = role;
            this.catalog = buttons;
            this.positionFile = positionFile;
            this.defaultScreen = defaultScreen;
            this.extraMenuItems = extraMenuItems;
            Config = config ?? new BarConfig();
            LoadLayout();
            RebuildButtons();
            measure = Graphics.FromImage(measureBitmap);
            measure.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Location = new Point(-32000, -32000);
            Size = new Size(1, 1);

            LoadPosition();
            timer.Interval = 15;
            timer.Tick += delegate { OnTimer(); };
        }

        // Draw at the screen's real resolution instead of being scaled up (and blurred) by Windows.
        public static void UseCrispScaling()
        {
            try { BarNative.SetThreadDpiAwarenessContext(new IntPtr(-4)); } catch { }
        }

        public void ApplyConfig(BarConfig c)
        {
            Config = c ?? new BarConfig();
            if (!hasSavedPosition) DefaultPosition();
            font = null;            // rebuild fonts on the next frame
            lastFrameKey = null;
            if (dragButton < 0) RebuildButtons();   // the shortcut buttons follow settings.ini
        }

        // ---------- which buttons, in what order ----------
        // Kept per bar next to its position file (buttons-client.txt / buttons-host.txt):
        //   order = host,client,shortcuts,quit
        //   iconsonly = 1

        string LayoutFile
        {
            get { return positionFile == null ? null : Path.Combine(Path.GetDirectoryName(positionFile), "buttons-" + role + ".txt"); }
        }

        List<BarButton> AllButtons()
        {
            var all = new List<BarButton>(catalog);
            try
            {
                if (ExtraButtons != null)
                    foreach (var b in ExtraButtons(Config))
                        if (!all.Exists(x => x.Id == b.Id)) all.Add(b);
            }
            catch { }
            return all;
        }

        void RebuildButtons()
        {
            var all = AllButtons();
            var list = new List<BarButton>();
            if (order == null) { foreach (var b in all) if (b.ShownByDefault) list.Add(b); }
            else
                foreach (string id in order)
                {
                    var b = all.Find(x => x.Id == id);
                    if (b != null && !list.Contains(b)) list.Add(b);
                }
            buttons = list;
            hover = pressed = tipFor = -1;
            lastFrameKey = null;
        }

        void LoadLayout()
        {
            order = null; iconsOnly = false;
            try
            {
                if (LayoutFile == null || !File.Exists(LayoutFile)) return;
                foreach (string raw in File.ReadAllLines(LayoutFile))
                {
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                    if (k == "order") order = new List<string>(v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)).ConvertAll(s => s.Trim());
                    else if (k == "iconsonly") iconsOnly = v == "1" || v.ToLowerInvariant() == "true";
                }
            }
            catch { }
        }

        void SaveLayout()
        {
            try
            {
                if (LayoutFile == null) return;
                Directory.CreateDirectory(Path.GetDirectoryName(LayoutFile));
                File.WriteAllText(LayoutFile, (order != null ? "order = " + string.Join(",", order.ToArray()) + "\r\n" : "") +
                                              "iconsonly = " + (iconsOnly ? "1" : "0") + "\r\n");
            }
            catch { }
        }

        void SetShown(BarButton b, bool show)
        {
            var ids = buttons.ConvertAll(x => x.Id);
            ids.Remove(b.Id);
            if (show)
            {
                // Before the quit button, which stays last unless you move it.
                int q = ids.IndexOf("quit");
                if (q >= 0 && b.Id != "quit") ids.Insert(q, b.Id); else ids.Add(b.Id);
            }
            order = ids;
            SaveLayout();
            RebuildButtons();
        }

        // Set by the client app: saves one settings.ini line (key, value). The host bar can't change
        // settings.ini (it only reads the client's copy), so it has no Colors menu.
        public Action<string, string> SaveSetting;

        ToolStripMenuItem ColorsMenu()
        {
            var menu = new ToolStripMenuItem("Colors");
            if (SaveSetting == null)
            {
                menu.DropDownItems.Add(new ToolStripMenuItem("Colors are set from the client PC's bar") { Enabled = false });
                return menu;
            }
            AddColorItem(menu, "Background...", "Background", c => c.Background, true);
            AddColorItem(menu, "Text and icons...", "Foreground", c => c.Foreground, false);
            AddColorItem(menu, "Accent (active button)...", "Accent", c => c.Accent, false);
            AddColorItem(menu, "Text on the accent...", "ActiveText", c => c.ActiveText, false);
            AddColorItem(menu, "Handle (tucked away)...", "HandleColor", c => c.HandleColor, true);
            AddColorItem(menu, "Quit confirmation...", "Danger", c => c.Danger, false);
            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add(new ToolStripMenuItem("Reset colors", null, delegate
            {
                var d = new BarConfig();
                SaveSetting("Background", ColorText(d.Background));
                SaveSetting("Foreground", ColorText(d.Foreground));
                SaveSetting("Accent", ColorText(d.Accent));
                SaveSetting("ActiveText", ColorText(d.ActiveText));
                SaveSetting("HandleColor", ColorText(d.HandleColor));
                SaveSetting("Danger", ColorText(d.Danger));
            }));
            return menu;
        }

        void AddColorItem(ToolStripMenuItem menu, string label, string key, Func<BarConfig, Color> get, bool alpha)
        {
            Color now = get(Config);
            var swatch = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(swatch))
            {
                g.Clear(Color.FromArgb(255, now));
                g.DrawRectangle(Pens.Gray, 0, 0, 15, 15);
            }
            menu.DropDownItems.Add(new ToolStripMenuItem(label, swatch, delegate
            {
                BarConfig before = Config;
                PinnedOpen = true;   // stay open to show the preview
                try
                {
                    using (var d = new ColorWheelDialog(label.TrimEnd('.'), get(before), alpha, scale))
                    {
                        d.Changed = col => { var p = before.Copy(); p.SetValue(key, ColorText(col)); ApplyConfig(p); };
                        if (d.ShowDialog() == DialogResult.OK) SaveSetting(key, ColorText(d.Value));
                        else ApplyConfig(before);
                    }
                }
                finally { PinnedOpen = false; }
            }));
        }

        public static string ColorText(Color c)
        {
            return c.A == 255 ? string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B)
                              : string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
        }

        ToolStripMenuItem ButtonsMenu()
        {
            var menu = new ToolStripMenuItem("Buttons");
            foreach (var b in AllButtons())
            {
                if (string.IsNullOrEmpty(b.Id)) continue;
                var bb = b;
                bool shown = buttons.Contains(b);
                var it = new ToolStripMenuItem(string.IsNullOrEmpty(b.Name) ? b.Id : b.Name, null, delegate { SetShown(bb, !shown); });
                it.Checked = shown;
                menu.DropDownItems.Add(it);
            }
            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add(new ToolStripMenuItem("Icons only", null, delegate { iconsOnly = !iconsOnly; SaveLayout(); lastFrameKey = null; }) { Checked = iconsOnly });
            menu.DropDownItems.Add(new ToolStripMenuItem("Reset buttons", null, delegate
            {
                try { if (LayoutFile != null && File.Exists(LayoutFile)) File.Delete(LayoutFile); } catch { }
                order = null; iconsOnly = false;
                RebuildButtons();
            }));
            var hint = new ToolStripMenuItem("Drag a button along the bar to move it") { Enabled = false };
            menu.DropDownItems.Add(hint);
            return menu;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 /* LAYERED */ | 0x08000000 /* NOACTIVATE */ | 0x80 /* TOOLWINDOW */ | 0x8 /* TOPMOST */;
                return cp;
            }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            visibleNow = true;
            timer.Start();
            OnTimer();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            base.OnFormClosed(e);
        }

        // ---------- position ----------

        void LoadPosition()
        {
            hasSavedPosition = false;
            try
            {
                if (positionFile != null && File.Exists(positionFile))
                {
                    string[] p = File.ReadAllText(positionFile).Trim().Split('|');
                    edge = (Edge)Enum.Parse(typeof(Edge), p[0], true);
                    offset = Math.Max(0, Math.Min(1, double.Parse(p[1], CultureInfo.InvariantCulture)));
                    screenName = p.Length > 2 && p[2].Length > 0 ? p[2] : null;
                    hasSavedPosition = true;
                    return;
                }
            }
            catch { }
            DefaultPosition();
        }

        void DefaultPosition()
        {
            edge = role == "host" ? Config.HostEdge : Config.ClientEdge;
            offset = role == "host" ? Config.HostOffset : Config.ClientOffset;
            screenName = null;
        }

        void SavePosition()
        {
            hasSavedPosition = true;
            if (positionFile == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(positionFile));
                File.WriteAllText(positionFile, edge + "|" + offset.ToString("0.000", CultureInfo.InvariantCulture) + "|" + (screenName ?? ""));
            }
            catch { }
        }

        void ResetPosition()
        {
            try { if (positionFile != null && File.Exists(positionFile)) File.Delete(positionFile); } catch { }
            hasSavedPosition = false;
            DefaultPosition();
        }

        void DockTo(Edge e)
        {
            screenName = CurrentScreen().DeviceName;
            edge = e;
            SavePosition();
        }

        Screen CurrentScreen()
        {
            if (!string.IsNullOrEmpty(screenName))
                foreach (Screen s in Screen.AllScreens)
                    if (s.DeviceName == screenName) return s;
            Screen d = null;
            try { if (defaultScreen != null) d = defaultScreen(); } catch { }
            return d ?? Screen.PrimaryScreen;
        }

        // ---------- sizes and layout ----------

        void UpdateScale()
        {
            float dpi = 96f;
            try { if (IsHandleCreated) { uint d = BarNative.GetDpiForWindow(Handle); if (d > 0) dpi = d; } } catch { }
            float s = dpi / 96f * (float)Config.Scale;
            if (font == null || Math.Abs(s - scale) > 0.001f)
            {
                scale = s;
                float px = (float)Config.FontSize * 96f / 72f * scale;
                if (font != null) font.Dispose();
                if (iconFont != null) iconFont.Dispose();
                font = MakeFont(Config.FontName, px) ?? MakeFont("Segoe UI", px);
                iconFont = MakeFont("Segoe Fluent Icons", px * 1.15f) ?? MakeFont("Segoe MDL2 Assets", px * 1.15f);
                lastFrameKey = null;
            }
        }

        static Font MakeFont(string name, float px)
        {
            try { return new Font(new FontFamily(name), px, FontStyle.Regular, GraphicsUnit.Pixel); }
            catch { return null; }
        }

        static bool IsVertical(Edge e) { return e == Edge.Left || e == Edge.Right; }
        float Pad { get { return 5 * scale; } }
        float Grip { get { return 14 * scale; } }
        float ShadowPad { get { return Config.Shadow ? 14 * scale : 2 * scale; } }

        // Lays the buttons out in a row (top/bottom edges) or a column (left/right edges).
        SizeF LayoutButtons(bool vertical)
        {
            float h = 32 * scale, hp = 12 * scale, gap = 7 * scale, sp = 3 * scale;
            bool icons = Config.ShowIcons && iconFont != null;
            bool labels = (Config.ShowLabels && !iconsOnly) || !icons;
            float maxW = 0, gapBefore = 6 * scale;
            var shown = new List<BarButton>();
            foreach (var b in buttons)
            {
                if (!b.Visible(Config)) { b.Bounds = RectangleF.Empty; continue; }
                shown.Add(b);
                string label = (b.Label != null ? b.Label(Config) : null) ?? "";
                b.IconWidth = icons && b.Glyph != null ? measure.MeasureString(b.Glyph, iconFont, PointF.Empty, StringFormat.GenericTypographic).Width : 0;
                b.TextWidth = labels && label.Length > 0 ? measure.MeasureString(label, font, PointF.Empty, StringFormat.GenericTypographic).Width : 0;
                b.ContentWidth = b.IconWidth + b.TextWidth + (b.IconWidth > 0 && b.TextWidth > 0 ? gap : 0);
                float w = Math.Max(h, b.ContentWidth + hp * 2);
                b.Bounds = new RectangleF(0, 0, w, h);
                maxW = Math.Max(maxW, w);
            }
            if (!vertical)
            {
                float x = Pad + Grip;
                for (int i = 0; i < shown.Count; i++)
                {
                    var b = shown[i];
                    if (b.GapBefore && i > 0) x += gapBefore;
                    b.Bounds = new RectangleF(x, Pad, b.Bounds.Width, h);
                    x += b.Bounds.Width + sp;
                }
                return new SizeF(x - sp + Pad, h + Pad * 2);
            }
            else
            {
                float y = Pad + Grip;
                for (int i = 0; i < shown.Count; i++)
                {
                    var b = shown[i];
                    if (b.GapBefore && i > 0) y += gapBefore;
                    b.Bounds = new RectangleF(Pad, y, maxW, h);
                    y += h + sp;
                }
                return new SizeF(maxW + Pad * 2, y - sp + Pad);
            }
        }

        // Where the open bar and the tucked-away handle sit for a given edge.
        void DockedRects(Screen sc, Edge e, double off, SizeF size, out RectangleF pillR, out RectangleF handleR)
        {
            Rectangle b = sc.Bounds;
            float g = 6 * scale;
            float len = (float)Config.HandleLength * scale;
            float thick = Math.Max(2f, (float)Config.HandleThickness * scale);
            if (!IsVertical(e))
            {
                float half = Math.Max(size.Width, len) / 2 + 4 * scale;
                float c = Math.Max(b.Left + half, Math.Min(b.Right - half, b.Left + (float)off * b.Width));
                if (e == Edge.Top)
                {
                    pillR = new RectangleF(c - size.Width / 2, b.Top + g, size.Width, size.Height);
                    handleR = new RectangleF(c - len / 2, b.Top, len, thick);
                }
                else
                {
                    pillR = new RectangleF(c - size.Width / 2, b.Bottom - g - size.Height, size.Width, size.Height);
                    handleR = new RectangleF(c - len / 2, b.Bottom - thick, len, thick);
                }
            }
            else
            {
                float half = Math.Max(size.Height, len) / 2 + 4 * scale;
                float c = Math.Max(b.Top + half, Math.Min(b.Bottom - half, b.Top + (float)off * b.Height));
                if (e == Edge.Left)
                {
                    pillR = new RectangleF(b.Left + g, c - size.Height / 2, size.Width, size.Height);
                    handleR = new RectangleF(b.Left, c - len / 2, thick, len);
                }
                else
                {
                    pillR = new RectangleF(b.Right - g - size.Width, c - size.Height / 2, size.Width, size.Height);
                    handleR = new RectangleF(b.Right - thick, c - len / 2, thick, len);
                }
            }
        }

        static void NearestEdge(Point p, out Screen sc, out Edge e, out double off)
        {
            sc = Screen.FromPoint(p);
            Rectangle b = sc.Bounds;
            int dl = p.X - b.Left, dr = b.Right - 1 - p.X, dt = p.Y - b.Top, db = b.Bottom - 1 - p.Y;
            int m = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
            if (m == dt) e = Edge.Top;
            else if (m == db) e = Edge.Bottom;
            else if (m == dl) e = Edge.Left;
            else e = Edge.Right;
            off = IsVertical(e) ? (p.Y - b.Top) / (double)b.Height : (p.X - b.Left) / (double)b.Width;
        }

        RectangleF ButtonOnScreen(BarButton b)
        {
            RectangleF r = b.Bounds;
            r.Offset(pill.Location);
            return r;
        }

        // ---------- frame loop ----------

        void OnTimer()
        {
            long now = clock.ElapsedMilliseconds;
            double dt = Math.Min(100, now - lastTickMs);
            lastTickMs = now;

            if (OwnerHidden && !dragging)
            {
                open = false; t = 0; hover = -1; pressed = -1; dragArmed = false;
                HideTooltip();
                if (visibleNow) { BarNative.ShowWindow(Handle, 0); visibleNow = false; }
                return;
            }

            UpdateScale();
            Point c = Cursor.Position;
            Screen sc;
            if (dragging)
            {
                // While dragging, the bar docks live to whichever edge is nearest the mouse.
                Edge e; double off;
                NearestEdge(c, out sc, out e, out off);
                edge = e; offset = off; screenName = sc.DeviceName;
            }
            else sc = CurrentScreen();

            SizeF size = LayoutButtons(IsVertical(edge));
            DockedRects(sc, edge, offset, size, out pill, out handle);

            RectangleF zone = open
                ? RectangleF.Inflate(RectangleF.Union(pill, handle), 14 * scale, 14 * scale)
                : (IsVertical(edge) ? RectangleF.Inflate(handle, 6 * scale, 12 * scale) : RectangleF.Inflate(handle, 12 * scale, 6 * scale));
            bool keepOpen = zone.Contains(c) || menuOpen || dragging || dragButton >= 0 || pressed >= 0 || dragArmed || PinnedOpen || Config.PinOpen;
            if (keepOpen) { lastHoverMs = now; open = true; }
            else if (open && now - lastHoverMs > Config.HideDelayMs) open = false;

            double target = open ? 1 : 0;
            if (Config.Animate)
            {
                double step = dt / 170.0;
                t = target > t ? Math.Min(target, t + step) : Math.Max(target, t - step);
            }
            else t = target;

            hover = -1;
            if (t > 0.95 && !dragging)
                for (int i = 0; i < buttons.Count; i++)
                    if (buttons[i].Shown && ButtonOnScreen(buttons[i]).Contains(c)) hover = i;
            if (dragButton >= 0) { MoveDraggedButton(c); hover = dragButton; }
            UpdateTooltip(now);

            win = Rectangle.Round(RectangleF.Inflate(RectangleF.Union(pill, handle), ShadowPad, ShadowPad));

            var key = new StringBuilder();
            key.Append(win).Append((int)(t * 1000)).Append('|').Append(hover).Append('|').Append(pressed)
               .Append('|').Append(dragging).Append('|').Append(Config.GetHashCode()).Append('|').Append(scale);
            foreach (var b in buttons) key.Append(b.Active() ? 'A' : 'a').Append(b.Enabled() ? 'E' : 'e').Append(b.Bounds.Width);
            string k = key.ToString();
            if (k != lastFrameKey) { Render(); lastFrameKey = k; }

            if (!visibleNow) { BarNative.ShowWindow(Handle, 4 /* SHOWNOACTIVATE */); visibleNow = true; }
            if (now - lastTopmostMs > 500)
            {
                // A fullscreen window can push us under; climb back on top.
                BarNative.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
                lastTopmostMs = now;
            }
        }

        // Shows the hovered button's name after half a second (when its label isn't on the bar,
        // or it has more to say), next to the bar on the side away from the screen edge.
        void UpdateTooltip(long now)
        {
            if (hover != hoverWas) { hoverWas = hover; hoverSinceMs = now; HideTooltip(); }
            if (hover < 0 || tipFor == hover || !Config.ShowTooltips || menuOpen || dragButton >= 0 || pressed >= 0) return;
            if (now - hoverSinceMs < 450) return;
            BarButton b = buttons[hover];
            string text = null;
            try { text = b.Tooltip != null ? b.Tooltip(Config) : b.TextWidth > 0 ? null : b.Name; } catch { }
            if (string.IsNullOrEmpty(text)) { tipFor = hover; return; }
            RectangleF r = ButtonOnScreen(b);
            float g = 8 * scale;
            Point at;
            bool above = false;
            switch (edge)
            {
                case Edge.Top: at = new Point((int)r.Left, (int)(pill.Bottom + g)); break;
                case Edge.Bottom: at = new Point((int)r.Left, (int)(pill.Top - g)); above = true; break;
                case Edge.Left: at = new Point((int)(pill.Right + g), (int)r.Top); break;
                default: at = new Point((int)(pill.Left - g - 220 * scale), (int)r.Top); break;
            }
            try { tip.ShowAt(text, at, above, Config, scale); tipFor = hover; } catch { }
        }

        void HideTooltip()
        {
            if (tipFor < 0) return;
            tipFor = -1;
            try { tip.HideTip(); } catch { }
        }

        // While dragging a button: move it to the slot under the mouse.
        void MoveDraggedButton(Point c)
        {
            if (dragButton < 0 || dragButton >= buttons.Count) return;
            bool vertical = IsVertical(edge);
            float pos = vertical ? c.Y : c.X;
            var b = buttons[dragButton];
            var rest = new List<BarButton>(buttons);
            rest.Remove(b);
            // The new place: after the last other button whose middle is before the mouse.
            int target = 0;
            for (int i = 0; i < rest.Count; i++)
            {
                if (!rest[i].Shown) continue;
                RectangleF r = ButtonOnScreen(rest[i]);
                if (pos > (vertical ? r.Y + r.Height / 2 : r.X + r.Width / 2)) target = i + 1;
            }
            if (target == dragButton) return;
            rest.Insert(target, b);
            buttons = rest;
            dragButton = target;
            order = buttons.ConvertAll(x => x.Id);
            lastFrameKey = null;
        }

        // ---------- drawing ----------

        static double Ease(double x) { return x * x * (3 - 2 * x); }
        static float Lerp(float a, float b, float x) { return a + (b - a) * x; }
        static RectangleF Lerp(RectangleF a, RectangleF b, float x)
        {
            return new RectangleF(Lerp(a.X, b.X, x), Lerp(a.Y, b.Y, x), Lerp(a.Width, b.Width, x), Lerp(a.Height, b.Height, x));
        }
        static Color Mix(Color a, Color b, float x)
        {
            return Color.FromArgb((int)Lerp(a.A, b.A, x), (int)Lerp(a.R, b.R, x), (int)Lerp(a.G, b.G, x), (int)Lerp(a.B, b.B, x));
        }
        static Color Fade(Color c, float alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, (int)(c.A * alpha))), c.R, c.G, c.B);
        }

        static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void Render()
        {
            if (win.Width <= 0 || win.Height <= 0) return;
            float e = (float)Ease(t);
            RectangleF cur = dragging ? pill : Lerp(handle, pill, e);
            if (dragging) e = 1;

            using (var bmp = new Bitmap(win.Width, win.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);

                    RectangleF r = cur;
                    r.Offset(-win.X, -win.Y);
                    float radius = Math.Min((float)Config.CornerRadius * scale, Math.Min(r.Width, r.Height) / 2f);

                    if (Config.Shadow && e > 0.01f)
                    {
                        for (int i = 8; i >= 1; i--)
                        {
                            float grow = i * 1.3f * scale;
                            RectangleF sr = RectangleF.Inflate(r, grow, grow);
                            sr.Offset(0, 1.5f * scale);
                            using (var path = Rounded(sr, radius + grow))
                            using (var br = new SolidBrush(Color.FromArgb((int)(e * 9 * (1 - (i - 1) / 8f)), 0, 0, 0)))
                                g.FillPath(br, path);
                        }
                    }

                    using (var body = Rounded(r, radius))
                    {
                        using (var br = new SolidBrush(Mix(Config.HandleColor, Config.Background, e)))
                            g.FillPath(br, body);
                        if (e > 0.2f)
                        {
                            using (var edgePath = Rounded(RectangleF.Inflate(r, -0.5f, -0.5f), radius))
                            using (var pen = new Pen(Fade(Config.Foreground, 0.14f * e), 1f))
                                g.DrawPath(pen, edgePath);
                        }

                        float ca = Math.Max(0, Math.Min(1, (e - 0.55f) / 0.45f));
                        if (ca > 0)
                        {
                            g.SetClip(body);
                            DrawContents(g, new PointF(pill.X - win.X, pill.Y - win.Y), ca);
                            g.ResetClip();
                        }
                    }
                }
                Present(bmp, (float)(Config.IdleOpacity + (Config.Opacity - Config.IdleOpacity) * e));
            }
        }

        void DrawContents(Graphics g, PointF origin, float ca)
        {
            bool vertical = IsVertical(edge);

            // Grip dots: drag the bar from here (or from any empty spot on it).
            using (var dot = new SolidBrush(Fade(Config.Foreground, 0.45f * ca)))
            {
                float dr = 1.4f * scale, step = 4.5f * scale;
                int cols = vertical ? 3 : 2, rows = vertical ? 2 : 3;
                float gw = vertical ? pill.Width - Pad * 2 : Grip;
                float gx = vertical ? origin.X + Pad : origin.X + Pad;
                float gy = vertical ? origin.Y + Pad : origin.Y + Pad;
                float gh = vertical ? Grip : 32 * scale;
                float cx = gx + gw / 2 - (cols - 1) * step / 2, cy = gy + gh / 2 - (rows - 1) * step / 2;
                for (int i = 0; i < cols; i++)
                    for (int j = 0; j < rows; j++)
                        g.FillEllipse(dot, cx + i * step - dr, cy + j * step - dr, dr * 2, dr * 2);
            }

            var sf = new StringFormat(StringFormat.GenericTypographic);
            sf.LineAlignment = StringAlignment.Center;
            sf.FormatFlags |= StringFormatFlags.NoWrap;

            for (int i = 0; i < buttons.Count; i++)
            {
                BarButton b = buttons[i];
                if (!b.Shown) continue;
                RectangleF br = b.Bounds;
                br.Offset(origin);
                bool active = b.Active(), enabled = b.Enabled();
                bool hovered = i == hover && enabled;
                float k = enabled ? 1f : 0.4f;
                if (b.Subtle && !hovered && !active) k *= 0.45f;   // dimmed until you point at it
                float rad = Math.Min(8 * scale, br.Height / 2);
                Color highlight = b.Danger ? Config.Danger : Config.Accent;

                using (var path = Rounded(br, rad))
                {
                    if (active && enabled)
                        using (var fill = new SolidBrush(Fade(highlight, (b.Subtle ? 0.2f : 1f) * ca))) g.FillPath(fill, path);
                    if (hovered)
                    {
                        Color hoverColor = b.Subtle && b.Danger ? Config.Danger : (active ? Color.White : Config.Foreground);
                        float hoverAlpha = b.Subtle && b.Danger ? 0.14f : (active ? 0.16f : 0.1f);
                        using (var fill = new SolidBrush(Fade(hoverColor, hoverAlpha * ca))) g.FillPath(fill, path);
                    }
                    if (i == pressed)
                        using (var fill = new SolidBrush(Color.FromArgb((int)(40 * ca), 0, 0, 0))) g.FillPath(fill, path);
                }

                Color baseText = Config.Foreground;
                if (b.Subtle && b.Danger && (active || hovered)) baseText = Config.Danger;   // soft red, no solid block
                else if (active && enabled) baseText = Config.ActiveText;
                Color textColor = Fade(baseText, ca * k);
                float x = br.X + (br.Width - b.ContentWidth) / 2;
                using (var brush = new SolidBrush(textColor))
                {
                    if (b.IconWidth > 0)
                    {
                        g.DrawString(b.Glyph, iconFont, brush, new RectangleF(x, br.Y, b.IconWidth + 2, br.Height), sf);
                        x += b.IconWidth + (b.TextWidth > 0 ? 7 * scale : 0);
                    }
                    if (b.TextWidth > 0)
                        g.DrawString(b.Label(Config), font, brush, new RectangleF(x, br.Y, b.TextWidth + 4, br.Height), sf);
                }
            }
        }

        void Present(Bitmap bmp, float alpha)
        {
            IntPtr screenDc = BarNative.GetDC(IntPtr.Zero);
            IntPtr memDc = BarNative.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = BarNative.SelectObject(memDc, hBitmap);
            try
            {
                var size = new BarNative.SIZE(bmp.Width, bmp.Height);
                var src = new BarNative.POINT(0, 0);
                var dst = new BarNative.POINT(win.X, win.Y);
                var blend = new BarNative.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = (byte)Math.Max(0, Math.Min(255, alpha * 255)), AlphaFormat = 1 };
                BarNative.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
            }
            finally
            {
                BarNative.SelectObject(memDc, old);
                BarNative.DeleteObject(hBitmap);
                BarNative.DeleteDC(memDc);
                BarNative.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        // ---------- mouse ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Point c = Cursor.Position;
            if (e.Button == MouseButtons.Right) { HideTooltip(); ShowMenu(c); return; }
            if (e.Button != MouseButtons.Left || t < 0.95) return;
            pressPoint = c;
            pressed = hover;
            dragArmed = true;
            lastFrameKey = null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragArmed && !dragging && dragButton < 0)
            {
                Point c = Cursor.Position;
                double dx = c.X - pressPoint.X, dy = c.Y - pressPoint.Y;
                if (Math.Sqrt(dx * dx + dy * dy) > 6 * scale)
                {
                    // From a button: move that button along the bar. From the grip or a gap: move the bar.
                    if (pressed >= 0) { dragButton = pressed; HideTooltip(); }
                    else dragging = true;
                    pressed = -1;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int clicked = -1;
            if (dragging) SavePosition();
            else if (dragButton >= 0) SaveLayout();
            else if (pressed >= 0 && pressed == hover) clicked = pressed;
            dragging = false; dragArmed = false; pressed = -1; dragButton = -1; lastFrameKey = null;
            HideTooltip();
            if (clicked >= 0 && buttons[clicked].Enabled())
            {
                try { buttons[clicked].Click(); } catch { }
            }
        }

        void ShowMenu(Point at)
        {
            var m = new ContextMenuStrip();
            m.Items.Add(Item("Dock to top", Edge.Top));
            m.Items.Add(Item("Dock to bottom", Edge.Bottom));
            m.Items.Add(Item("Dock to left", Edge.Left));
            m.Items.Add(Item("Dock to right", Edge.Right));
            m.Items.Add(new ToolStripMenuItem("Reset position", null, delegate { ResetPosition(); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(ButtonsMenu());
            m.Items.Add(ColorsMenu());
            List<ToolStripItem> extra = null;
            try { if (extraMenuItems != null) extra = extraMenuItems(); } catch { }
            if (extra != null && extra.Count > 0)
            {
                m.Items.Add(new ToolStripSeparator());
                foreach (var it in extra) m.Items.Add(it);
            }
            TrackMenu(m);
            m.Show(at);
        }

        void TrackMenu(ContextMenuStrip m)
        {
            m.Opened += delegate { menuOpen = true; };
            m.Closed += delegate { menuOpen = false; BeginInvoke((MethodInvoker)delegate { m.Dispose(); }); };
        }

        // Opens a menu next to one of the bar's buttons, on the side facing away from the edge.
        public void ShowItems(List<ToolStripItem> items, BarButton anchor)
        {
            if (items == null || items.Count == 0) return;
            var m = new ContextMenuStrip();
            foreach (var it in items) m.Items.Add(it);
            TrackMenu(m);
            RectangleF r = anchor != null && anchor.Shown ? ButtonOnScreen(anchor) : pill;
            float g = 6 * scale;
            switch (edge)
            {
                case Edge.Top: m.Show(new Point((int)r.Left, (int)(pill.Bottom + g)), ToolStripDropDownDirection.BelowRight); break;
                case Edge.Bottom: m.Show(new Point((int)r.Left, (int)(pill.Top - g)), ToolStripDropDownDirection.AboveRight); break;
                case Edge.Left: m.Show(new Point((int)(pill.Right + g), (int)r.Top), ToolStripDropDownDirection.BelowRight); break;
                default: m.Show(new Point((int)(pill.Left - g), (int)r.Top), ToolStripDropDownDirection.BelowLeft); break;
            }
        }

        // The shortcuts menu, built from settings.ini. send('Q') etc. presses Ctrl+Alt+Shift+Q in Moonlight.
        public static List<ToolStripItem> ShortcutItems(BarConfig c, Action<char> send)
        {
            var items = new List<ToolStripItem>();
            foreach (var s in c.Shortcuts)
            {
                char key = s.Value;
                var it = new ToolStripMenuItem(s.Key, null, delegate { send(key); });
                it.ShortcutKeyDisplayString = "Ctrl+Alt+Shift+" + key;
                items.Add(it);
            }
            return items;
        }

        ToolStripMenuItem Item(string text, Edge e)
        {
            var it = new ToolStripMenuItem(text, null, delegate { DockTo(e); });
            it.Checked = edge == e;
            return it;
        }
    }

    // ---------------------------------------------------------------- host side

    // SHA-256 of a file as 64 hex digits: the host program's fingerprint, shown on the install
    // page and in the installer so you can check you're installing what this PC built.
    public static class Fingerprint
    {
        public static string OfFile(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var f = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "");
        }
    }

    // Runs the bar on the host, inside the host program (HostMain.cs). It only reads data from the
    // client (stream state, settings.ini) and sends it requests; it never runs anything it receives.
    public static class HostAgent
    {
        const int SwitchPort = 47710, ServerPort = 47711;
        const string MsgPrefix = "MOONLIGHTSWITCH/1 ";

        // Set by the host program (HostMain.cs):
        // the command to run when a stream starts (true) or ends (false), read from the host's own host.ini;
        public static Func<bool, string> LocalCommand;
        // the "Reset language bar" button's command (HostResetLanguageBar in host.ini; empty = no button).
        // Read when clicked; only a click on this PC runs it, never a request from the network.
        public static Func<string> ResetLanguageBarCommand;
        static volatile bool hasResetLanguageBar;   // refreshed by Poll every few seconds, to show or hide the button
        // the SHA-256 of the running program, to tell when the client has a newer one.
        public static string OwnFingerprint = "";
        // Set when "Restart this bar" closes the bar; the host program then starts itself again.
        public static bool RestartRequested;
        static volatile bool updateAvailable;

        public static void Run(string clientIp, bool preview)
        {
            BarWindow.UseCrispScaling();
            string baseUrl = "http://" + clientIp + ":" + ServerPort;
            BarConfig cfg;
            try { cfg = BarConfig.Parse(Http(baseUrl + "/settings.ini")); } catch { cfg = new BarConfig(); }
            IPAddress client = IPAddress.Parse(clientIp);
            clientAddress = client;
            // The layout of the window you're typing in, for the host tray.
            TrayInfo.Keyboard = () =>
            {
                uint pid;
                return Hex(BarNative.GetKeyboardLayout(BarNative.GetWindowThreadProcessId(BarNative.GetForegroundWindow(), out pid)));
            };

            BarWindow bar = null;
            var buttons = new List<BarButton>();
            buttons.Add(new BarButton(c => c.BackLabel, Glyphs.Back, () => Send(client, "CLIENT"), () => true, null).Named("back", "Back to my PC"));
            BarButton shortcuts = null;
            shortcuts = new BarButton(c => c.ShortcutsLabel, Glyphs.More,
                () =>
                {
                    var items = BarWindow.ShortcutItems(bar.Config, k => Send(client, "KEY " + k));
                    items.Add(new ToolStripSeparator());
                    items.Add(new ToolStripMenuItem("Disconnect (keep Moonlight open)", null, delegate { Send(client, "DISCONNECT"); }));
                    items.Add(new ToolStripMenuItem("Sleep host", null, delegate { SleepHost(client); }));
                    if (updateAvailable) items.Add(UpdateItem(baseUrl));
                    bar.ShowItems(items, shortcuts);
                }, null, null);
            shortcuts.GapBefore = true;
            shortcuts.IsVisible = c => c.ShowShortcutsButton;
            shortcuts.Named("shortcuts", "Moonlight shortcuts menu");
            buttons.Add(shortcuts);
            buttons.Add(new BarButton(c => "", Glyphs.Disconnect, () => Send(client, "DISCONNECT"), null, null)
                .Named("disconnect", "Disconnect (keep Moonlight open)", false));
            buttons.Add(new BarButton(c => "", Glyphs.Sleep, () => SleepHost(client), null, null)
                .Named("sleep", "Sleep host", false));
            BarButton quit = BarButton.Quit(() => Send(client, "QUIT"), null, () => bar.Config.ConfirmQuit);
            quit.GapBefore = false;
            buttons.Add(quit);

            // The client panel: the client PC's battery, volume, Wi-Fi... in a small floating window.
            var panel = new ClientPanel("Client", () => Http(baseUrl + "/info"), cmd => Send(client, cmd),
                                        Path.Combine(Path.GetDirectoryName(PositionFile()), "client-panel.txt"));
            panel.ApplyConfig(cfg);
            var panelButton = new BarButton(c => c.ClientPanelLabel, Glyphs.Panel, () =>
            {
                panel.Toggle();
                panel.UpdateVisibility(true);
            }, () => panel.WantOpen, null).Named("panel", "Client tray (show or hide)");
            panelButton.IsVisible = c => c.ShowClientPanelButton;
            panelButton.GapBefore = true;
            buttons.Insert(1, panelButton);
            var resetButton = new BarButton(c => "", Glyphs.Language, ResetLanguageBar, null, null).Named("resetlang", "Reset language bar");
            resetButton.IsVisible = c => hasResetLanguageBar;
            buttons.Insert(2, resetButton);

            bar = new BarWindow("host", cfg, buttons, PositionFile(), () => Screen.PrimaryScreen, () =>
            {
                var items = new List<ToolStripItem>();
                var sub = new ToolStripMenuItem("Moonlight shortcuts");
                foreach (var it in BarWindow.ShortcutItems(bar.Config, k => Send(client, "KEY " + k))) sub.DropDownItems.Add(it);
                items.Add(sub);
                items.Add(new ToolStripMenuItem("Disconnect (keep Moonlight open)", null, delegate { Send(client, "DISCONNECT"); }));
                items.Add(new ToolStripMenuItem("Quit Moonlight", null, delegate { Send(client, "QUIT"); }));
                items.Add(new ToolStripMenuItem("Sleep host", null, delegate { SleepHost(client); }));
                items.Add(new ToolStripMenuItem("Client panel", null, delegate { panel.Toggle(); panel.UpdateVisibility(true); }) { Checked = panel.WantOpen });
                if (hasResetLanguageBar) items.Add(new ToolStripMenuItem("Reset language bar", null, delegate { ResetLanguageBar(); }));
                items.Add(new ToolStripSeparator());
                if (updateAvailable) items.Add(UpdateItem(baseUrl));
                items.Add(new ToolStripMenuItem("Restart this bar", null, delegate { RestartRequested = true; bar.Close(); }));
                return items;
            });
            bar.ExtraButtons = BarButton.ShortcutButtons(k => Send(client, "KEY " + k), null);
            // Only show while this PC is being streamed (or right away in preview mode).
            bar.OwnerHidden = !preview;
            bar.PinnedOpen = preview;
            bar.Load += delegate
            {
                if (!preview) WatchForeground();
                var poll = new Thread(() => Poll(bar, panel, baseUrl, preview));
                poll.IsBackground = true;
                poll.Start();
            };
            Application.EnableVisualStyles();
            Application.Run(bar);
        }

        // Runs this PC's own HostResetLanguageBar command from host.ini, read now.
        static void ResetLanguageBar()
        {
            string cmd = "";
            try { if (ResetLanguageBarCommand != null) cmd = ResetLanguageBarCommand() ?? ""; } catch { }
            LogLine("reset language bar: " + (cmd.Length == 0 ? "no HostResetLanguageBar command in host.ini" : "running"));
            RunCommand(cmd);
        }

        // Opens the client's install page in the browser; installing stays your call.
        static ToolStripMenuItem UpdateItem(string baseUrl)
        {
            return new ToolStripMenuItem("Update available: open the install page", null, delegate
            {
                try { Process.Start(baseUrl + "/"); } catch { }
            });
        }

        // "Sleep host": end the stream (Moonlight stays open on the client, ready to reconnect after
        // you wake the host by hand), then, once the stream has been gone for ~8 s
        // (after the stream-end keyboard switch and command), turn the screen off. On this Modern
        // Standby laptop that puts it to sleep. Poll does the second half.
        static long sleepRequestedAt;   // DateTime ticks, 0 = none

        static void SleepHost(IPAddress client)
        {
            Interlocked.Exchange(ref sleepRequestedAt, DateTime.UtcNow.Ticks);
            Send(client, "DISCONNECT");
        }

        static void CheckSleep(bool streaming, int idle)
        {
            long at = Interlocked.Read(ref sleepRequestedAt);
            if (at == 0) return;
            double waited = (DateTime.UtcNow - new DateTime(at, DateTimeKind.Utc)).TotalSeconds;
            if (streaming)
            {
                if (waited > 60) Interlocked.Exchange(ref sleepRequestedAt, 0);   // the stream never ended: give up
                return;
            }
            if (idle < 16 || waited < 5) return;   // ~8 s without a stream
            Interlocked.Exchange(ref sleepRequestedAt, 0);
            // PostMessage, not SendMessage, so a hung window can't block it. 2 = monitor off.
            BarNative.PostMessage(new IntPtr(0xFFFF) /* HWND_BROADCAST */, 0x0112 /* WM_SYSCOMMAND */,
                new IntPtr(0xF170) /* SC_MONITORPOWER */, new IntPtr(2));
        }

        static string PositionFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoonlightSwitch", "position-host.txt");
        }

        // Every half second: is the client streaming, and did settings.ini or the bar's code change?
        static void Poll(BarWindow bar, ClientPanel panel, string baseUrl, bool preview)
        {
            string lastVersion = null;
            int idle = 0;
            DateTime lastReached = DateTime.Now;
            bool active = false;      // a stream is on
            BarConfig cfg = null;
            int loops = 0;
            while (true)
            {
                // Is there a HostResetLanguageBar command? (shows its button; checked every ~5 s)
                if (loops++ % 10 == 0)
                    try { hasResetLanguageBar = ResetLanguageBarCommand != null && (ResetLanguageBarCommand() ?? "").Trim().Length > 0; } catch { }
                bool streaming = false, reached = false;
                string version = null, settings = null, clientKeyboard = null;
                try
                {
                    string[] s = Http(baseUrl + "/state").Split('\n');
                    streaming = s[0].Trim() == "streaming";
                    reached = true;
                    if (s.Length > 1) version = s[1].Trim();
                    if (s.Length > 2)
                    {
                        // The client's current host program: a different one means an update is available.
                        string latest = s[2].Trim();
                        updateAvailable = latest.Length == 64 && OwnFingerprint.Length == 64 &&
                                          !string.Equals(latest, OwnFingerprint, StringComparison.OrdinalIgnoreCase);
                    }
                    if (s.Length > 3) clientKeyboard = s[3].Trim();
                }
                catch { }   // client unreachable: handled below
                if (version != null && version != lastVersion)
                {
                    try { settings = Http(baseUrl + "/settings.ini"); lastVersion = version; cfg = BarConfig.Parse(settings); } catch { }
                }

                // Stream started / ended: show or hide the bar, and run the start/end commands from
                // settings.ini. Ended = the client says "idle" for ~6 s, or doesn't answer for ~60 s
                // (so a client app restart or a network blip doesn't undo the stream setup).
                bool changed = false;
                if (!reached)
                {
                    if ((DateTime.Now - lastReached).TotalSeconds >= 60 && active) { active = false; changed = true; }
                }
                else
                {
                    lastReached = DateTime.Now;
                    if (streaming) { idle = 0; if (!active) { active = true; changed = true; } }
                    else if (++idle >= 12 && active) { active = false; changed = true; }   // ~6 s at 0.5 s per check
                }
                if (changed && !preview && cfg != null)
                {
                    // Keyboard first: it's what you notice. Then the slower command (e.g. a script).
                    SwitchKeyboard(cfg, active, clientKeyboard);
                    // From the host's own host.ini. The start command runs once per stream: not again
                    // when this bar restarts (or is updated) while the stream goes on.
                    if (active && StreamAlreadyStarted())
                        LogLine("stream start: already set up for this stream (the bar restarted), not running HostStreamStart again");
                    else if (LocalCommand != null) RunCommand(LocalCommand(active));
                    MarkStream(active);
                }
                if (active && !preview && loops % 20 == 0) MarkStream(true);   // every ~10 s while streaming
                else if (active && !preview && cfg != null && FollowsClient(cfg) && clientKeyboard != null && clientKeyboard != followedKeyboard)
                    SwitchKeyboard(cfg, true, clientKeyboard);   // you switched layouts on the client mid-stream
                CheckSleep(streaming, idle);

                // This PC's tray info, for the client's "Host tray": sent straight to the client
                // (never broadcast) every 2 s while a stream is on. The host doesn't listen for anything.
                if ((active || preview) && loops % 4 == 0)
                    try { SendTo(clientAddress, "INFO\n" + TrayInfo.Text()); } catch { }

                bool show = active;
                try
                {
                    bar.BeginInvoke((MethodInvoker)delegate
                    {
                        if (settings != null) { bar.ApplyConfig(BarConfig.Parse(settings)); panel.ApplyConfig(bar.Config); }
                        if (!preview) bar.OwnerHidden = !show;
                        panel.UpdateVisibility((show || preview) && bar.Config.ShowClientPanelButton);
                    });
                }
                catch { return; }   // the bar was closed
                Thread.Sleep(500);
            }
        }

        // stream-on.txt holds when the bar last saw this stream running (UTC ticks), rewritten every
        // ~10 s and deleted when the stream ends. Found fresh (under 90 s old) when the bar starts =
        // the same stream is still on. A stale one (the PC restarted mid-stream) doesn't count.
        static string StreamMarkerFile()
        {
            return Path.Combine(Path.GetDirectoryName(PositionFile()), "stream-on.txt");
        }

        static bool StreamAlreadyStarted()
        {
            try
            {
                string f = StreamMarkerFile();
                if (!File.Exists(f)) return false;
                var seen = new DateTime(long.Parse(File.ReadAllText(f).Trim()), DateTimeKind.Utc);
                return (DateTime.UtcNow - seen).TotalSeconds < 90;
            }
            catch { return false; }
        }

        static void MarkStream(bool on)
        {
            try
            {
                string f = StreamMarkerFile();
                if (on)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(f));
                    File.WriteAllText(f, DateTime.UtcNow.Ticks.ToString());
                }
                else if (File.Exists(f)) File.Delete(f);
            }
            catch { }
        }

        // Finds an installed keyboard layout by its 8-hex-digit id (e.g. "04090409"); IntPtr.Zero if missing.
        public static IntPtr FindKeyboardLayout(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return IntPtr.Zero;
            hex = hex.Trim().ToUpperInvariant();
            int n = BarNative.GetKeyboardLayoutList(0, null);
            if (n <= 0) return IntPtr.Zero;
            var list = new IntPtr[n];
            BarNative.GetKeyboardLayoutList(n, list);
            foreach (var h in list)
                if ((h.ToInt64() & 0xFFFFFFFFL).ToString("X8") == hex) return h;
            return IntPtr.Zero;
        }

        // Keyboard layouts from settings.ini, and whether the stream one applies (set by SwitchKeyboard).
        static IntPtr streamHkl, idleHkl;
        static volatile bool keyboardStreaming;
        static DateTime keyboardSwitchedAt;
        static BarNative.WinEventProc onForeground;   // kept in a field so the hook's callback isn't collected

        // Switches the keyboard layout: the default for new windows, plus the window you're typing in.
        // Other windows switch as they come to the front (FixLayout). Unlike a broadcast to every
        // window, this leaves the taskbar's language indicator alone.
        static void SwitchKeyboard(BarConfig cfg, bool streaming, string clientKeyboard)
        {
            if (string.IsNullOrEmpty(cfg.HostKeyboardStream)) { keyboardStreaming = false; streamHkl = idleHkl = IntPtr.Zero; return; }
            try
            {
                // The host's own layout from before the stream, to go back to when HostKeyboardIdle is
                // empty. Kept in a file so a bar restart mid-stream doesn't mistake the stream layout for it.
                string before = Path.Combine(Path.GetDirectoryName(PositionFile()), "keyboard-before-stream.txt");
                if (streaming && !File.Exists(before))
                {
                    IntPtr d = IntPtr.Zero;
                    BarNative.SystemParametersInfo(0x0059 /* SPI_GETDEFAULTINPUTLANG */, 0, ref d, 0);
                    Directory.CreateDirectory(Path.GetDirectoryName(before));
                    File.WriteAllText(before, Hex(d));
                }
                string beforeHex = File.Exists(before) ? File.ReadAllText(before).Trim() : "";
                if (!streaming) File.Delete(before);

                if (FollowsClient(cfg))
                {
                    followedKeyboard = clientKeyboard;
                    streamHkl = FindLayoutOrLanguage(clientKeyboard);
                    if (streaming && streamHkl == IntPtr.Zero)
                        LogLine("keyboard: the client's layout " + (clientKeyboard ?? "(not reported)") + " isn't installed on this PC");
                }
                else streamHkl = FindKeyboardLayout(cfg.HostKeyboardStream);
                idleHkl = FindKeyboardLayout(string.IsNullOrEmpty(cfg.HostKeyboardIdle) ? beforeHex : cfg.HostKeyboardIdle);
            }
            catch (Exception e) { LogLine("keyboard setup failed: " + e.Message); }
            keyboardSwitchedAt = DateTime.Now;
            keyboardStreaming = streaming;
            IntPtr hkl = streaming ? streamHkl : idleHkl;
            if (hkl == IntPtr.Zero) return;
            try
            {
                IntPtr set = hkl;
                bool ok = BarNative.SystemParametersInfo(0x005A /* SPI_SETDEFAULTINPUTLANG */, 0, ref set, 2 /* SPIF_SENDCHANGE */);
                int err = Marshal.GetLastWin32Error();
                IntPtr now = IntPtr.Zero;
                BarNative.SystemParametersInfo(0x0059 /* SPI_GETDEFAULTINPUTLANG */, 0, ref now, 0);
                LogLine("keyboard " + (streaming ? "stream " : "idle ") + Hex(hkl) + ": default " +
                        (ok ? "set" : "not set (error " + err + ")") + ", now " + Hex(now));
                FixLayout(BarNative.GetForegroundWindow());
            }
            catch (Exception e) { LogLine("keyboard switch failed: " + e.Message); }
        }

        // Called on the bar's thread (it has the message loop the hook needs).
        static void WatchForeground()
        {
            onForeground = (hook, ev, hwnd, idObject, idChild, thread, time) => FixLayout(hwnd);
            BarNative.SetWinEventHook(3 /* EVENT_SYSTEM_FOREGROUND */, 3, IntPtr.Zero, onForeground, 0, 0, 0 /* out of context */);
        }

        // Puts the window coming to the front on the wanted layout. While streaming: the stream layout.
        // In the 10 minutes after: windows still on the stream layout go back to the idle one (any
        // other layout was picked by hand and stays).
        static void FixLayout(IntPtr hwnd)
        {
            try
            {
                if (hwnd == IntPtr.Zero) return;
                bool streaming = keyboardStreaming;
                IntPtr target = streaming ? streamHkl : idleHkl;
                if (target == IntPtr.Zero) return;
                uint pid;
                IntPtr current = BarNative.GetKeyboardLayout(BarNative.GetWindowThreadProcessId(hwnd, out pid));
                if (Hex(current) == Hex(target)) return;
                if (!streaming && (Hex(current) != Hex(streamHkl) || (DateTime.Now - keyboardSwitchedAt).TotalMinutes > 10)) return;
                BarNative.PostMessage(hwnd, 0x0050 /* WM_INPUTLANGCHANGEREQUEST */, IntPtr.Zero, target);
            }
            catch { }
        }

        static string Hex(IntPtr hkl) { return (hkl.ToInt64() & 0xFFFFFFFFL).ToString("X8"); }

        static string followedKeyboard;   // the client layout the stream layout was last taken from

        static bool FollowsClient(BarConfig cfg)
        {
            return string.Equals((cfg.HostKeyboardStream ?? "").Trim(), "client", StringComparison.OrdinalIgnoreCase);
        }

        // The installed layout with this id, or else the language's standard layout (e.g. 04090409 for
        // F0010409), or else any layout for that language (the last 4 digits). The exact variant
        // may not be installed here, and one language can hold several layouts (even another
        // country's), so the standard one comes first.
        static IntPtr FindLayoutOrLanguage(string hex)
        {
            IntPtr exact = FindKeyboardLayout(hex);
            if (exact != IntPtr.Zero || string.IsNullOrEmpty(hex) || hex.Length != 8) return exact;
            IntPtr standard = FindKeyboardLayout(hex.Substring(4) + hex.Substring(4));
            if (standard != IntPtr.Zero) return standard;
            int n = BarNative.GetKeyboardLayoutList(0, null);
            if (n <= 0) return IntPtr.Zero;
            var list = new IntPtr[n];
            BarNative.GetKeyboardLayoutList(n, list);
            foreach (var h in list)
                if (Hex(h).EndsWith(hex.Substring(4).ToUpperInvariant())) return h;
            return IntPtr.Zero;
        }

        // The host bar's log: %TEMP%\MoonlightSwitch-host.log, started over when it gets big.
        public static void LogLine(string text)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "MoonlightSwitch-host.log");
                if (File.Exists(path) && new FileInfo(path).Length > 100000) File.Delete(path);
                File.AppendAllText(path, DateTime.Now.ToString("s") + " " + text + "\r\n");
            }
            catch { }
        }

        // Runs a command line like: powershell.exe -File "C:\x.ps1" -Mode stream (hidden, not waited on).
        // Started from here, as a normal program in your session, instead of from Sunshine.
        static void RunCommand(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine)) return;
            string cmd = commandLine.Trim(), exe, args;
            if (cmd.StartsWith("\""))
            {
                int end = cmd.IndexOf('"', 1);
                exe = end > 0 ? cmd.Substring(1, end - 1) : cmd.Trim('"');
                args = end > 0 ? cmd.Substring(end + 1).Trim() : "";
            }
            else
            {
                int sp = cmd.IndexOf(' ');
                exe = sp > 0 ? cmd.Substring(0, sp) : cmd;
                args = sp > 0 ? cmd.Substring(sp + 1).Trim() : "";
            }
            try
            {
                var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                Process.Start(psi).Dispose();
            }
            catch { }
        }

        static string Http(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 2500;
            req.ReadWriteTimeout = 2500;
            using (var resp = req.GetResponse())
            using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return r.ReadToEnd();
        }

        // Sends a request to the client (CLIENT = switch to it, QUIT = quit Moonlight, KEY x =
        // press Ctrl+Alt+Shift+x in Moonlight): directly, plus every local broadcast address.
        static IPAddress clientAddress;

        // To the client only.
        static void SendTo(IPAddress client, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(MsgPrefix + message);
            using (var udp = new UdpClient()) udp.Send(data, data.Length, new IPEndPoint(client, SwitchPort));
        }

        static void Send(IPAddress client, string command)
        {
            byte[] data = Encoding.UTF8.GetBytes(MsgPrefix + command);
            var targets = new List<IPAddress>();
            if (client != null) targets.Add(client);
            targets.Add(IPAddress.Broadcast);
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                    byte[] ip = ua.Address.GetAddressBytes(), mask = ua.IPv4Mask.GetAddressBytes();
                    for (int i = 0; i < 4; i++) ip[i] = (byte)(ip[i] | ~mask[i]);
                    targets.Add(new IPAddress(ip));
                }
            }
            using (var udp = new UdpClient())
            {
                udp.EnableBroadcast = true;
                foreach (var t in targets)
                {
                    try { udp.Send(data, data.Length, new IPEndPoint(t, SwitchPort)); } catch { }
                }
            }
        }
    }

    // ---------------------------------------------------------------- tray info (both PCs)

    // Windows' audio volume API (Core Audio), for the host's client panel.
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorClass { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float db, ref Guid context);
        int SetMasterVolumeLevelScalar(float level, ref Guid context);
        int GetMasterVolumeLevel(out float db);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float db, ref Guid context);
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        int GetChannelVolumeLevel(uint channel, out float db);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    // What a tray panel shows about a PC (battery, volume, Wi-Fi, keyboard layout, time), and its
    // volume and brightness controls. Used on both PCs: each one describes itself.
    public static class TrayInfo
    {
        // This PC's current keyboard layout as 8 hex digits (set by each program).
        public static Func<string> Keyboard;

        // Lines of key=value: the client serves them at /info, the host sends them to the client.
        public static string Text()
        {
            var sb = new StringBuilder();
            try
            {
                PowerStatus p = SystemInformation.PowerStatus;
                bool hasBattery = (p.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0 && p.BatteryLifePercent <= 1;
                sb.Append("battery=").Append(hasBattery ? ((int)Math.Round(p.BatteryLifePercent * 100)).ToString() : "").Append('\n');
                sb.Append("plugged=").Append(p.PowerLineStatus == PowerLineStatus.Online ? "1" : "0").Append('\n');
                sb.Append("charging=").Append((p.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0 ? "1" : "0").Append('\n');
            }
            catch { }
            float level; bool mute;
            if (GetVolume(out level, out mute))
            {
                sb.Append("volume=").Append((int)Math.Round(level * 100)).Append('\n');
                sb.Append("mute=").Append(mute ? "1" : "0").Append('\n');
            }
            string ssid, signal;
            Wifi(out ssid, out signal);
            sb.Append("wifi=").Append(ssid).Append('\n');
            sb.Append("signal=").Append(signal).Append('\n');
            string kb = (Keyboard != null ? Keyboard() : "");
            string kbName = KeyboardName(kb);
            sb.Append("keyboard=").Append(kbName).Append('\n');
            sb.Append("kbshort=").Append(KeyboardShort(kb, kbName)).Append('\n');
            try
            {
                if (kb.Length == 8)
                    sb.Append("language=").Append(new CultureInfo(Convert.ToInt32(kb.Substring(4), 16)).DisplayName).Append('\n');
            }
            catch { }
            sb.Append("brightness=").Append(Brightness()).Append('\n');
            sb.Append("batteries=").Append(Batteries()).Append('\n');
            sb.Append("time=").Append(DateTime.Now.ToString("HH:mm")).Append('\n');
            sb.Append("date=").Append(DateTime.Now.ToString("d")).Append('\n');
            sb.Append("longdate=").Append(DateTime.Now.ToString("D")).Append('\n');
            return sb.ToString();
        }

        // Each battery, for PCs with more than one (a Surface Book has one in the screen and one in
        // the keyboard): "percent|plugged|charging" joined with ';', in Windows' order
        // (Battery 1, Battery 2). Read at most every 5 seconds.
        static string batteries = "";
        static DateTime batteriesReadAt = DateTime.MinValue;

        static string Batteries()
        {
            if ((DateTime.Now - batteriesReadAt).TotalSeconds < 5) return batteries;
            batteriesReadAt = DateTime.Now;
            var full = new Dictionary<string, double>();
            var parts = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, FullChargedCapacity FROM BatteryFullChargedCapacity"))
                    foreach (System.Management.ManagementObject o in s.Get())
                        using (o) full[(string)o["InstanceName"]] = Convert.ToDouble(o["FullChargedCapacity"]);
                using (var s = new System.Management.ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, RemainingCapacity, Charging, PowerOnline FROM BatteryStatus"))
                    foreach (System.Management.ManagementObject o in s.Get())
                        using (o)
                        {
                            string name = (string)o["InstanceName"];
                            double cap;
                            if (!full.TryGetValue(name, out cap) || cap <= 0) continue;
                            int pct = (int)Math.Max(0, Math.Min(100, Math.Round(Convert.ToDouble(o["RemainingCapacity"]) / cap * 100)));
                            parts[name] = pct + "|" + ((bool)o["PowerOnline"] ? "1" : "0") + "|" + ((bool)o["Charging"] ? "1" : "0");
                        }
            }
            catch { }
            batteries = string.Join(";", new List<string>(parts.Values).ToArray());
            return batteries;
        }

        // "VOLUME SET n", "VOLUME UP", "VOLUME DOWN" (5% steps), "MUTE" (toggle) or "BRIGHTNESS SET n" (n = 0-100).
        public static void Command(string cmd)
        {
            int n = -1;
            if (cmd.StartsWith("VOLUME SET ") || cmd.StartsWith("BRIGHTNESS SET "))
            {
                if (!int.TryParse(cmd.Substring(cmd.LastIndexOf(' ') + 1), out n) || n < 0 || n > 100) return;
            }
            if (cmd.StartsWith("BRIGHTNESS SET ")) { SetBrightness(n); return; }
            WithVolume(v =>
            {
                Guid none = Guid.Empty;
                float level; bool mute;
                v.GetMasterVolumeLevelScalar(out level);
                v.GetMute(out mute);
                if (cmd == "MUTE") { v.SetMute(!mute, ref none); return; }
                float next;
                if (n >= 0) next = n / 100f;
                else if (cmd == "VOLUME UP" || cmd == "VOLUME DOWN")
                    next = (float)Math.Max(0, Math.Min(1, Math.Round((level + (cmd == "VOLUME UP" ? 0.05f : -0.05f)) * 20) / 20));
                else return;
                v.SetMasterVolumeLevelScalar(next, ref none);
                if (mute && next > level) v.SetMute(false, ref none);   // turning it up unmutes, like Windows
            });
        }

        // The built-in screen's brightness (0-100) through WMI, or "" when there's none (an external
        // monitor). Read at most every 2 seconds.
        static string brightness = "";
        static DateTime brightnessReadAt = DateTime.MinValue;

        static string Brightness()
        {
            if ((DateTime.Now - brightnessReadAt).TotalSeconds < 2) return brightness;
            brightnessReadAt = DateTime.Now;
            string b = "";
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(@"root\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness"))
                    foreach (System.Management.ManagementObject o in s.Get())
                        using (o) { b = Convert.ToInt32(o["CurrentBrightness"]).ToString(); break; }
            }
            catch { }
            brightness = b;
            return b;
        }

        static void SetBrightness(int level)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (var s = new System.Management.ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods"))
                        foreach (System.Management.ManagementObject o in s.Get())
                            using (o) o.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)level });
                    brightness = level.ToString();
                    brightnessReadAt = DateTime.Now;
                }
                catch { }
            });
        }

        // The two short lines Windows shows next to the clock, e.g. "ENG INTL" or "ENG US".
        static string KeyboardShort(string hex, string name)
        {
            try
            {
                var culture = new System.Globalization.CultureInfo(Convert.ToInt32(hex.Substring(4), 16));
                string lang = culture.ThreeLetterISOLanguageName.ToUpperInvariant();
                string second;
                if ((name ?? "").IndexOf("International", StringComparison.OrdinalIgnoreCase) >= 0) second = "INTL";
                else
                {
                    try { second = new System.Globalization.RegionInfo(culture.Name).TwoLetterISORegionName; }
                    catch { second = ""; }
                }
                return (lang + " " + second).Trim();
            }
            catch { return ""; }
        }

        static bool GetVolume(out float level, out bool mute)
        {
            float l = 0; bool m = false;
            bool ok = WithVolume(v => { v.GetMasterVolumeLevelScalar(out l); v.GetMute(out m); });
            level = l; mute = m;
            return ok;
        }

        // Runs work on the default playback device's volume control.
        static bool WithVolume(Action<IAudioEndpointVolume> work)
        {
            object enumerator = null, device = null, volume = null;
            try
            {
                enumerator = new MMDeviceEnumeratorClass();
                IMMDevice dev;
                if (((IMMDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0 /* render */, 1 /* multimedia */, out dev) != 0 || dev == null) return false;
                device = dev;
                Guid iid = typeof(IAudioEndpointVolume).GUID;
                if (dev.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out volume) != 0 || volume == null) return false;
                work((IAudioEndpointVolume)volume);
                return true;
            }
            catch { return false; }
            finally
            {
                foreach (object o in new[] { volume, device, enumerator })
                    if (o != null) try { Marshal.ReleaseComObject(o); } catch { }
            }
        }

        // Wi-Fi name and signal, from "netsh wlan show interfaces", refreshed in the background at
        // most every 10 seconds (it takes a moment to run).
        static string wifiName = "", wifiSignal = "";
        static DateTime wifiCheckedAt = DateTime.MinValue;
        static int wifiChecking;

        static void Wifi(out string ssid, out string signal)
        {
            ssid = wifiName; signal = wifiSignal;
            if ((DateTime.Now - wifiCheckedAt).TotalSeconds < 10 || Interlocked.Exchange(ref wifiChecking, 1) == 1) return;
            var t = new Thread(delegate ()
            {
                try
                {
                    var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
                    {
                        UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
                    };
                    string name = "", sig = "";
                    using (var p = Process.Start(psi))
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(5000);
                        bool connected = false;
                        foreach (string raw in output.Split('\n'))
                        {
                            int colon = raw.IndexOf(':');
                            if (colon < 0) continue;
                            string key = raw.Substring(0, colon).Trim(), val = raw.Substring(colon + 1).Trim();
                            if (key == "State") connected = val == "connected";
                            else if (key == "SSID" && name == "") name = val;
                            else if (key == "Signal" && sig == "") sig = val.TrimEnd('%');
                        }
                        if (!connected) { name = ""; sig = ""; }
                    }
                    wifiName = name; wifiSignal = sig;
                }
                catch { }
                wifiCheckedAt = DateTime.Now;
                Interlocked.Exchange(ref wifiChecking, 0);
            });
            t.IsBackground = true;
            t.Start();
        }

        // A readable name for a keyboard layout id like "F0010409": the layout's own name from the
        // registry (e.g. "United States-International"), or else the language's.
        static string KeyboardName(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex) || hex.Length != 8) return hex ?? "";
                string lang = hex.Substring(4);
                string device = hex.Substring(0, 4);
                const string root = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";
                using (var layouts = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(root))
                {
                    if (layouts != null)
                    {
                        // A plain layout: 0809 0409 -> 00000809. A variant: F001 0409 -> the entry with Layout Id 0001.
                        bool variant = device.StartsWith("F", StringComparison.OrdinalIgnoreCase);
                        string wanted = variant ? null : "0000" + device;
                        string layoutId = variant ? "0" + device.Substring(1) : null;
                        foreach (string klid in layouts.GetSubKeyNames())
                        {
                            using (var k = layouts.OpenSubKey(klid))
                            {
                                if (k == null) continue;
                                bool match = wanted != null ? string.Equals(klid, wanted, StringComparison.OrdinalIgnoreCase)
                                           : layoutId != null && string.Equals(k.GetValue("Layout Id") as string, layoutId, StringComparison.OrdinalIgnoreCase);
                                string text = k.GetValue("Layout Text") as string;
                                if (match && !string.IsNullOrEmpty(text)) return text;
                            }
                        }
                    }
                }
                return new System.Globalization.CultureInfo(Convert.ToInt32(lang, 16)).DisplayName;
            }
            catch { return hex; }
        }
    }

    // ---------------------------------------------------------------- tooltip

    // A small tooltip in the bars' colors, that never takes the focus. Placed by screen point:
    // below `at`, or above it when `above` is set.
    public class TipWindow : Form
    {
        string text = "";
        Font font;
        Color back = Color.FromArgb(44, 44, 50), fore = Color.White, border = Color.FromArgb(70, 70, 78);
        float k = 1;

        public TipWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 /* NOACTIVATE */ | 0x80 /* TOOLWINDOW */ | 0x8 /* TOPMOST */ | 0x20 /* TRANSPARENT: clicks pass through */;
                return cp;
            }
        }

        public void ShowAt(string s, Point at, bool above, BarConfig cfg, float scale)
        {
            if (string.IsNullOrEmpty(s)) { HideTip(); return; }
            text = s;
            k = Math.Max(1f, scale);
            if (font == null || Math.Abs(font.Size - 12 * k) > 0.1f) font = new Font("Segoe UI", 12 * k, GraphicsUnit.Pixel);
            if (cfg != null)
            {
                back = Color.FromArgb(255, Mix(cfg.Background, cfg.Foreground, 0.1f));
                fore = Color.FromArgb(255, cfg.Foreground);
                border = Color.FromArgb(255, Mix(cfg.Background, cfg.Foreground, 0.25f));
            }
            Size size = TextRenderer.MeasureText(text, font);
            var sz = new Size(size.Width + (int)(16 * k), size.Height + (int)(10 * k));
            Rectangle wa = Screen.FromPoint(at).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(wa.Right - sz.Width, at.X));
            int y = above ? at.Y - sz.Height : at.Y;
            y = Math.Max(wa.Top, Math.Min(wa.Bottom - sz.Height, y));
            Bounds = new Rectangle(x, y, sz.Width, sz.Height);
            Invalidate();
            if (!Visible) Show();
            BarNative.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
        }

        public void HideTip() { if (Visible) Hide(); }

        static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(back);
            using (var p = new Pen(border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, text, font, new Rectangle((int)(8 * k), (int)(5 * k), Width, Height), fore,
                                  TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
        }
    }

    // ---------------------------------------------------------------- color wheel

    // Picks a color: a hue/saturation wheel, a brightness bar, optionally a transparency bar, and
    // a hex box. Changed fires on every change, for a live preview.
    public class ColorWheelDialog : Form
    {
        public Color Value { get; private set; }
        public Action<Color> Changed;

        readonly bool withAlpha;
        readonly float k;
        readonly Color original;
        double hue, sat, val;
        int alpha;
        Bitmap wheel;
        RectangleF wheelR, valueR, alphaR, oldR, newR;
        readonly TextBox hex;
        string dragging;
        bool settingHex;

        public ColorWheelDialog(string title, Color start, bool withAlpha, float scale)
        {
            this.withAlpha = withAlpha;
            k = Math.Max(1f, scale);
            original = start;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(32, 32, 36);
            ForeColor = Color.FromArgb(235, 235, 240);
            Font = new Font("Segoe UI", 12 * k, GraphicsUnit.Pixel);

            float wheelSize = 220 * k, pad = 16 * k, bar = 22 * k, gap = 12 * k;
            wheelR = new RectangleF(pad, pad, wheelSize, wheelSize);
            valueR = new RectangleF(wheelR.Right + gap * 1.5f, pad, bar, wheelSize);
            alphaR = withAlpha ? new RectangleF(valueR.Right + gap, pad, bar, wheelSize) : RectangleF.Empty;
            float right = (withAlpha ? alphaR.Right : valueR.Right) + pad;
            float y = wheelR.Bottom + gap * 1.5f;
            oldR = new RectangleF(pad, y, 34 * k, 30 * k);
            newR = new RectangleF(oldR.Right, y, 34 * k, 30 * k);
            ClientSize = new Size((int)right, (int)(y + 30 * k + gap + 32 * k + pad));

            hex = new TextBox
            {
                Location = new Point((int)(newR.Right + gap), (int)(y + 3 * k)),
                Width = (int)(110 * k),
                BackColor = Color.FromArgb(48, 48, 54),
                ForeColor = ForeColor,
                BorderStyle = BorderStyle.FixedSingle,
                CharacterCasing = CharacterCasing.Upper,
            };
            hex.TextChanged += delegate { if (!settingHex) FromHex(); };
            Controls.Add(hex);

            var ok = MakeButton("OK", DialogResult.OK);
            var cancel = MakeButton("Cancel", DialogResult.Cancel);
            int by = (int)(y + 30 * k + gap);
            cancel.Location = new Point(ClientSize.Width - (int)pad - cancel.Width, by);
            ok.Location = new Point(cancel.Left - (int)(8 * k) - ok.Width, by);
            AcceptButton = ok;
            CancelButton = cancel;

            SetFrom(start, true);
        }

        Button MakeButton(string text, DialogResult result)
        {
            var b = new Button
            {
                Text = text, DialogResult = result, FlatStyle = FlatStyle.Flat,
                Size = new Size((int)(84 * k), (int)(30 * k)),
                BackColor = result == DialogResult.OK ? Color.FromArgb(90, 140, 255) : Color.FromArgb(52, 52, 58),
                ForeColor = Color.White,
            };
            b.FlatAppearance.BorderSize = 0;
            Controls.Add(b);
            return b;
        }

        // ---------- color math ----------

        static Color FromHsv(double h, double s, double v, int a)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromArgb(a, (int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }

        void SetFrom(Color c, bool updateHex)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            double h = 0;
            if (d > 0)
            {
                if (max == r) h = 60 * (((g - b) / d) % 6);
                else if (max == g) h = 60 * ((b - r) / d + 2);
                else h = 60 * ((r - g) / d + 4);
            }
            if (d > 0 || max == 0) hue = (h + 360) % 360;   // keep the hue for grays
            sat = max == 0 ? sat : d / max;
            val = max;
            alpha = withAlpha ? c.A : 255;
            Commit(updateHex);
        }

        void Commit(bool updateHex)
        {
            Value = FromHsv(hue, sat, val, alpha);
            if (updateHex)
            {
                settingHex = true;
                hex.Text = BarWindow.ColorText(Value);
                settingHex = false;
            }
            Invalidate();
            if (Changed != null) try { Changed(Value); } catch { }
        }

        void FromHex()
        {
            try
            {
                Color c = BarConfig.ParseColor(hex.Text);
                if (!withAlpha) c = Color.FromArgb(255, c);
                SetFrom(c, false);
            }
            catch { }   // not a full color yet
        }

        // ---------- drawing ----------

        Bitmap Wheel(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var px = new int[size * size];
            double r = size / 2.0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double dx = x + 0.5 - r, dy = y + 0.5 - r, dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    double h = Math.Atan2(-dy, dx) * 180 / Math.PI;
                    Color c = FromHsv(h, Math.Min(1, dist / r), 1, 255);
                    int edge = (int)Math.Max(0, Math.Min(255, (r - dist) * 255));   // smooth rim
                    px[y * size + x] = Color.FromArgb(edge, c).ToArgb();
                }
            Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);
            return bmp;
        }

        static void Checker(Graphics g, RectangleF r, float cell)
        {
            using (var light = new SolidBrush(Color.FromArgb(200, 200, 200)))
            using (var dark = new SolidBrush(Color.FromArgb(150, 150, 150)))
            {
                g.FillRectangle(light, r);
                for (float y = r.Top; y < r.Bottom; y += cell)
                    for (float x = r.Left + (((int)((y - r.Top) / cell)) % 2) * cell; x < r.Right; x += cell * 2)
                        g.FillRectangle(dark, x, y, Math.Min(cell, r.Right - x), Math.Min(cell, r.Bottom - y));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (wheel == null) wheel = Wheel((int)wheelR.Width);
            g.DrawImage(wheel, wheelR.X, wheelR.Y, wheelR.Width, wheelR.Height);
            using (var shade = new SolidBrush(Color.FromArgb((int)((1 - val) * 255), 0, 0, 0)))
                g.FillEllipse(shade, wheelR);

            // Marker on the wheel.
            double rad = hue * Math.PI / 180, rr = sat * wheelR.Width / 2;
            float mx = wheelR.X + wheelR.Width / 2 + (float)(Math.Cos(rad) * rr), my = wheelR.Y + wheelR.Height / 2 - (float)(Math.Sin(rad) * rr);
            float mr = 7 * k;
            using (var p1 = new Pen(Color.Black, 3 * k)) g.DrawEllipse(p1, mx - mr, my - mr, mr * 2, mr * 2);
            using (var p2 = new Pen(Color.White, 1.5f * k)) g.DrawEllipse(p2, mx - mr, my - mr, mr * 2, mr * 2);

            // Brightness bar.
            using (var br = new LinearGradientBrush(valueR, FromHsv(hue, sat, 1, 255), Color.Black, LinearGradientMode.Vertical))
                g.FillRectangle(br, valueR);
            Marker(g, valueR, 1 - val);

            // Transparency bar.
            if (withAlpha)
            {
                Checker(g, alphaR, 6 * k);
                Color solid = FromHsv(hue, sat, val, 255);
                using (var br = new LinearGradientBrush(alphaR, solid, Color.FromArgb(0, solid), LinearGradientMode.Vertical))
                    g.FillRectangle(br, alphaR);
                Marker(g, alphaR, 1 - alpha / 255.0);
            }

            // Old and new color.
            Checker(g, RectangleF.Union(oldR, newR), 6 * k);
            using (var b1 = new SolidBrush(original)) g.FillRectangle(b1, oldR);
            using (var b2 = new SolidBrush(Value)) g.FillRectangle(b2, newR);
        }

        void Marker(Graphics g, RectangleF bar, double at)
        {
            float y = bar.Top + (float)(at * bar.Height);
            using (var p1 = new Pen(Color.Black, 3 * k)) g.DrawRectangle(p1, bar.X - 2 * k, y - 3 * k, bar.Width + 4 * k, 6 * k);
            using (var p2 = new Pen(Color.White, 1.5f * k)) g.DrawRectangle(p2, bar.X - 2 * k, y - 3 * k, bar.Width + 4 * k, 6 * k);
        }

        // ---------- mouse ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            float cx = wheelR.X + wheelR.Width / 2, cy = wheelR.Y + wheelR.Height / 2;
            double dist = Math.Sqrt(Math.Pow(e.X - cx, 2) + Math.Pow(e.Y - cy, 2));
            if (dist <= wheelR.Width / 2 + 4 * k) dragging = "wheel";
            else if (RectangleF.Inflate(valueR, 4 * k, 6 * k).Contains(e.Location)) dragging = "value";
            else if (withAlpha && RectangleF.Inflate(alphaR, 4 * k, 6 * k).Contains(e.Location)) dragging = "alpha";
            else return;
            Capture = true;
            Pick(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e) { if (dragging != null) Pick(e.Location); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = null; Capture = false; }

        void Pick(Point p)
        {
            if (dragging == "wheel")
            {
                float cx = wheelR.X + wheelR.Width / 2, cy = wheelR.Y + wheelR.Height / 2;
                double dx = p.X - cx, dy = cy - p.Y;
                hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
                sat = Math.Min(1, Math.Sqrt(dx * dx + dy * dy) / (wheelR.Width / 2));
                if (val < 0.05) val = 1;   // picking a hue on a black color: make it visible
            }
            else if (dragging == "value") val = Math.Max(0, Math.Min(1, 1 - (p.Y - valueR.Top) / valueR.Height));
            else if (dragging == "alpha") alpha = (int)Math.Round(Math.Max(0, Math.Min(1, 1 - (p.Y - alphaR.Top) / alphaR.Height)) * 255);
            Commit(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && wheel != null) wheel.Dispose();
            base.Dispose(disposing);
        }
    }

    // ---------------------------------------------------------------- client panel (host)

    // The client PC's corner of the taskbar, floating on the host: keyboard layout, Wi-Fi, volume,
    // battery, time and date. Click the icons for a small Quick Settings with volume and brightness
    // sliders. Opened and closed from the host bar, drag it anywhere, shown only while the bar is
    // (during a stream). Data comes from the client's /info.
    public class ClientPanel : Form
    {
        public bool WantOpen { get; private set; }
        public BarConfig Config = new BarConfig();

        readonly Func<string> fetch;          // reads the client's /info (throws when unreachable)
        readonly Action<string> send;         // sends a request to the client
        readonly string stateFile;
        readonly AutoResetEvent poke = new AutoResetEvent(false);
        readonly System.Windows.Forms.Timer topmost = new System.Windows.Forms.Timer { Interval = 2000 };
        readonly System.Windows.Forms.Timer sendTimer = new System.Windows.Forms.Timer { Interval = 120 };
        static readonly Color Key = Color.FromArgb(255, 1, 2, 3);   // see-through color around the two cards

        Dictionary<string, string> info;      // null = client not reached
        volatile bool showing;
        int commandCounter;
        bool expanded, upward;
        float scale = 1f;
        Font font, small, icons, bigIcons;

        // Layout, in window coordinates (rebuilt by Arrange()).
        RectangleF strip, trayIcons, flyout, footer;
        RectangleF[] tiles = new RectangleF[3];
        RectangleF volIcon, volTrack, briIcon, briTrack;
        string hover = "";

        // Sliders: the value being dragged, and until when to trust it over what the client reports.
        string dragging;                       // "volume", "brightness" or null
        int volLocal = -1, briLocal = -1;
        DateTime volLocalUntil, briLocalUntil;
        string pendingSend;                    // newest slider value to send

        // Dragging the window by the strip.
        Point downAt;
        bool downOnStrip;

        // Tooltips: the strip's parts, like the taskbar's.
        RectangleF kbRect, wifiRect, volRect, batRect, clockRect;
        readonly TipWindow tip = new TipWindow();
        readonly System.Windows.Forms.Timer tipTimer = new System.Windows.Forms.Timer { Interval = 450 };
        string tipPart = "", tipShown = "";
        readonly string who;                  // "Client" or "Host": whose tray this is

        // fetch: the PC's info (key=value lines). send: its controls, or null for a read-only tray.
        public ClientPanel(string who, Func<string> fetch, Action<string> send, string stateFile)
        {
            this.who = who; this.fetch = fetch; this.send = send; this.stateFile = stateFile;
            tipTimer.Tick += delegate { tipTimer.Stop(); ShowTip(); };
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Key;
            TransparencyKey = Key;
            Text = who;
            Point? pos = null;
            try
            {
                string[] p = File.ReadAllText(stateFile).Trim().Split('|');
                WantOpen = p[0] == "1";
                if (p.Length >= 3) pos = new Point(int.Parse(p[1]), int.Parse(p[2]));
            }
            catch { }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = pos.HasValue && OnSomeScreen(pos.Value) ? pos.Value : new Point(wa.Right - 400, wa.Bottom - 120);
            Size = new Size(300, 60);
            topmost.Tick += delegate
            {
                if (Visible) BarNative.SetWindowPos(Handle, new IntPtr(-1) /* HWND_TOPMOST */, 0, 0, 0, 0, 0x0013 /* NOSIZE|NOMOVE|NOACTIVATE */);
            };
            topmost.Start();
            sendTimer.Tick += delegate
            {
                if (pendingSend == null) { if (dragging == null) sendTimer.Stop(); return; }
                Command(pendingSend);
                pendingSend = null;
            };
            var t = new Thread(PollLoop) { IsBackground = true };
            t.Start();
        }

        static bool OnSomeScreen(Point p)
        {
            foreach (Screen s in Screen.AllScreens) if (s.WorkingArea.Contains(p.X + 20, p.Y + 10)) return true;
            return false;
        }

        // Don't take the focus from the game or app you're in.
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 /* WS_EX_NOACTIVATE */ | 0x00000080 /* WS_EX_TOOLWINDOW */ | 0x00000008 /* WS_EX_TOPMOST */;
                return cp;
            }
        }

        public void Toggle()
        {
            WantOpen = !WantOpen;
            if (!WantOpen) SetExpanded(false);
            SaveState();
        }

        // Called by the bar: shown = the bar is on screen (a stream is on, or preview).
        public void UpdateVisibility(bool shown)
        {
            bool v = WantOpen && shown;
            if (v == Visible) return;
            if (v) { ApplyLook(); Show(); showing = true; poke.Set(); }
            else { showing = false; SetExpanded(false); HideTip(); Hide(); }
        }

        public void ApplyConfig(BarConfig c)
        {
            Config = c ?? new BarConfig();
            font = null;
            if (Visible) { ApplyLook(); Invalidate(); }
        }

        // Where the strip is on screen: the saved position (the window moves when the flyout opens upward).
        Point StripScreenPos()
        {
            return new Point(Left + (int)strip.X, Top + (int)strip.Y);
        }

        void SaveState()
        {
            try
            {
                Point p = IsHandleCreated ? StripScreenPos() : Location;
                Directory.CreateDirectory(Path.GetDirectoryName(stateFile));
                File.WriteAllText(stateFile, (WantOpen ? "1" : "0") + "|" + p.X + "|" + p.Y);
            }
            catch { }
        }

        void PollLoop()
        {
            while (true)
            {
                poke.WaitOne(expanded ? 1000 : 2000);
                if (!showing) continue;
                Dictionary<string, string> d = null;
                try
                {
                    d = new Dictionary<string, string>();
                    foreach (string raw in fetch().Split('\n'))
                    {
                        int eq = raw.IndexOf('=');
                        if (eq > 0) d[raw.Substring(0, eq).Trim()] = raw.Substring(eq + 1).Trim();
                    }
                }
                catch { d = null; }
                try { BeginInvoke((MethodInvoker)delegate { info = d; ApplyLook(); Invalidate(); }); }
                catch { }
            }
        }

        void Command(string cmd)
        {
            if (send == null) return;
            send(cmd + " #" + (++commandCounter));
            // Read the result back shortly after the client has applied it.
            var t = new System.Windows.Forms.Timer { Interval = 400 };
            t.Tick += delegate { t.Stop(); t.Dispose(); poke.Set(); };
            t.Start();
        }

        string Get(string key)
        {
            string v;
            return info != null && info.TryGetValue(key, out v) ? v : "";
        }

        int Num(string key)
        {
            int n;
            return int.TryParse(Get(key), out n) ? n : -1;
        }

        int Volume() { return dragging == "volume" || DateTime.Now < volLocalUntil ? volLocal : Num("volume"); }
        int BrightnessLevel() { return dragging == "brightness" || DateTime.Now < briLocalUntil ? briLocal : Num("brightness"); }

        // ---------- size and layout ----------

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        static Font Make(string name, float px, FontStyle style = FontStyle.Regular)
        {
            try { return new Font(new FontFamily(name), px, style, GraphicsUnit.Pixel); }
            catch { return null; }
        }

        float S(float v) { return v * scale; }

        void ApplyLook()
        {
            float dpi = 96f;
            try { uint d = BarNative.GetDpiForWindow(Handle); if (d > 0) dpi = d; } catch { }
            float s = dpi / 96f * (float)Config.Scale;
            if (font == null || Math.Abs(s - scale) > 0.001f)
            {
                scale = s;
                font = Make("Segoe UI", S(14)) ;
                small = Make("Segoe UI", S(12));
                icons = Make("Segoe Fluent Icons", S(16)) ?? Make("Segoe MDL2 Assets", S(16));
                bigIcons = Make("Segoe Fluent Icons", S(17)) ?? Make("Segoe MDL2 Assets", S(17));
            }
            Opacity = Config.Opacity;
            Arrange();
        }

        // Works out the strip (the mini taskbar corner) and, when open, the flyout above or below it,
        // then sizes and places the window so the strip stays where it was.
        void Arrange()
        {
            if (font == null) return;
            Point stripPos = IsHandleCreated && strip.Width > 0 ? StripScreenPos() : Location;

            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                string[] kb = KbLines();
                float langW = Math.Max(g.MeasureString(kb[0], small).Width, g.MeasureString(kb[1], small).Width);
                float clockW = Math.Max(g.MeasureString(Get("time"), small).Width, g.MeasureString(Get("date"), small).Width);
                float iconsW = S(3 * 28 + 12);
                float stripW = S(10) + langW + S(8) + iconsW + S(8) + clockW + S(12);
                float stripH = S(48);
                float flyW = S(340), gap = S(10);
                float flyH = FlyoutHeight();

                float w = expanded ? Math.Max(stripW, flyW) : stripW;
                // Open upward when the strip is in the lower half of its screen, like the taskbar.
                Screen scr = Screen.FromPoint(stripPos);
                upward = stripPos.Y > scr.WorkingArea.Top + scr.WorkingArea.Height / 2;
                float h = expanded ? stripH + gap + flyH : stripH;

                float stripX = w - stripW;   // strip on the right edge, the flyout right-aligned with it
                float stripY = expanded && upward ? flyH + gap : 0;
                strip = new RectangleF(stripX, stripY, stripW, stripH);
                trayIcons = new RectangleF(stripX + S(10) + langW + S(8) - S(4), stripY + S(6), iconsW, stripH - S(12));
                flyout = expanded ? new RectangleF(w - flyW, upward ? 0 : stripH + gap, flyW, flyH) : RectangleF.Empty;

                var size = new Size((int)Math.Ceiling(w), (int)Math.Ceiling(h));
                var loc = new Point(stripPos.X - (int)stripX, stripPos.Y - (int)stripY);
                if (size != Size || loc != Location) Bounds = new Rectangle(loc, size);
            }
            LayoutFlyout();
        }

        float FlyoutHeight()
        {
            float h = S(16) + S(56) + S(30);              // tiles and their labels
            h += S(16);                                    // divider
            if (Num("brightness") >= 0) h += S(44);
            h += S(44) + S(12);                            // volume
            h += S(48);                                    // footer
            return h;
        }

        void LayoutFlyout()
        {
            if (!expanded) return;
            float x = flyout.X + S(16), y = flyout.Y + S(16), w = flyout.Width - S(32);
            float tw = (w - S(2 * 12)) / 3;
            for (int i = 0; i < 3; i++) tiles[i] = new RectangleF(x + i * (tw + S(12)), y, tw, S(52));
            y += S(56) + S(30) + S(16);
            if (Num("brightness") >= 0)
            {
                briIcon = new RectangleF(x, y, S(32), S(36));
                briTrack = new RectangleF(x + S(44), y + S(17), w - S(44) - S(8), S(4));
                y += S(44);
            }
            else briIcon = briTrack = RectangleF.Empty;
            volIcon = new RectangleF(x, y, S(32), S(36));
            volTrack = new RectangleF(x + S(44), y + S(17), w - S(44) - S(8), S(4));
            footer = new RectangleF(flyout.X, flyout.Bottom - S(48), flyout.Width, S(48));
        }

        string[] KbLines()
        {
            string[] p = (Get("kbshort") + " ").Split(new[] { ' ' }, 2);
            return new[] { p[0], p.Length > 1 ? p[1].Trim() : "" };
        }

        void SetExpanded(bool on)
        {
            if (on == expanded) return;
            expanded = on;
            Arrange();
            Invalidate();
            if (on) poke.Set();
        }

        // ---------- drawing ----------

        Color Bg { get { return Color.FromArgb(255, Config.Background); } }
        Color Fg { get { return Color.FromArgb(255, Config.Foreground); } }
        Color Dim { get { return Color.FromArgb(255, Mix(Config.Background, Config.Foreground, 0.55f)); } }
        Color Raised { get { return Color.FromArgb(255, Mix(Config.Background, Config.Foreground, 0.09f)); } }
        Color Lower { get { return Color.FromArgb(255, Mix(Config.Background, Color.Black, 0.25f)); } }

        static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void Fill(Graphics g, RectangleF r, float radius, Color c)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        void DrawText(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h = StringAlignment.Near)
        {
            using (var b = new SolidBrush(c))
            using (var fmt = new StringFormat { Alignment = h, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(s ?? "", f, b, r, fmt);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (font == null) ApplyLook();
            if (font == null) return;
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            // The cards' edges are drawn without smoothing so no blend with the see-through color shows.
            g.SmoothingMode = SmoothingMode.None;
            float radius = (float)Math.Min(Config.CornerRadius, 12) * scale;
            Fill(g, strip, radius, Bg);
            if (expanded) Fill(g, flyout, radius, Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawStrip(g);
            if (expanded) DrawFlyout(g, radius);
        }

        void DrawStrip(Graphics g)
        {
            float y = strip.Y, h = strip.Height;
            if (info == null)
            {
                DrawText(g, who + " unreachable", small, Dim, new RectangleF(strip.X + S(12), y, strip.Width - S(24), h));
                return;
            }
            // Keyboard layout, two short lines.
            string[] kb = KbLines();
            float lx = strip.X + S(10), lw = trayIcons.X + S(4) - S(8) - lx;
            DrawText(g, kb[0], small, Fg, new RectangleF(lx, y + h / 2 - S(17), lw, S(17)), StringAlignment.Center);
            DrawText(g, kb[1], small, Fg, new RectangleF(lx, y + h / 2, lw, S(17)), StringAlignment.Center);

            // Wi-Fi, volume, battery: one button, like Windows.
            if (hover == "tray" || expanded) Fill(g, trayIcons, S(5), Raised);
            float ix = trayIcons.X + S(6), iy = trayIcons.Y, ih = trayIcons.Height;
            kbRect = new RectangleF(lx, y, lw, h);
            wifiRect = new RectangleF(ix, iy, S(28), ih);
            volRect = new RectangleF(ix + S(28), iy, S(28), ih);
            batRect = new RectangleF(ix + S(56), iy, trayIcons.Right - ix - S(56), ih);
            clockRect = new RectangleF(trayIcons.Right, y, strip.Right - trayIcons.Right, h);
            DrawText(g, WifiGlyph(), icons, Fg, new RectangleF(ix, iy, S(28), ih), StringAlignment.Center);
            DrawText(g, VolumeGlyph(), icons, Fg, new RectangleF(ix + S(28), iy, S(28), ih), StringAlignment.Center);
            DrawBattery(g, new RectangleF(ix + S(56) + S(3), iy + ih / 2 - S(6), S(22), S(12)));

            // Time and date.
            float cx = trayIcons.Right + S(8), cw = strip.Right - S(12) - cx;
            DrawText(g, Get("time"), small, Fg, new RectangleF(cx, y + h / 2 - S(17), cw, S(17)), StringAlignment.Far);
            DrawText(g, Get("date"), small, Fg, new RectangleF(cx, y + h / 2, cw, S(17)), StringAlignment.Far);
        }

        string WifiGlyph()
        {
            if (Get("wifi") == "") return "";        // no Wi-Fi
            int s = Num("signal");
            return s < 0 || s >= 75 ? "" : s >= 50 ? "" : s >= 25 ? "" : "";
        }

        string VolumeGlyph()
        {
            if (Get("volume") == "") return "";
            int v = Volume();
            if (Get("mute") == "1" || v == 0) return "";
            return v >= 66 ? "" : v >= 33 ? "" : "";
        }

        // A small power plug centered in r: two prongs on the left, the body, the cord to the right.
        void DrawPlug(Graphics g, RectangleF r, Color c)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            using (var b = new SolidBrush(c))
            using (var pen = new Pen(c, S(1.1f)))
            {
                var body = new RectangleF(cx - S(2), cy - S(3), S(4.5f), S(6));
                using (var path = Round(body, S(1.2f))) g.FillPath(b, path);
                g.DrawLine(pen, body.X - S(2.8f), cy - S(1.6f), body.X, cy - S(1.6f));
                g.DrawLine(pen, body.X - S(2.8f), cy + S(1.6f), body.X, cy + S(1.6f));
                g.DrawLine(pen, body.Right, cy, body.Right + S(3), cy);
            }
        }

        // A battery outline with its level, green while plugged in, red when low.
        void DrawBattery(Graphics g, RectangleF r)
        {
            int pct = Num("battery");
            if (pct < 0) { DrawText(g, "", icons, Fg, r, StringAlignment.Center); return; }
            bool charging = Get("charging") == "1", plugged = Get("plugged") == "1";
            // Like Windows: green with a bolt while charging, the text color with a plug when plugged
            // in and full, red when low on battery.
            Color level = charging ? Color.FromArgb(108, 203, 95) : pct <= 15 && !plugged ? Config.Danger : Fg;
            using (var pen = new Pen(Fg, S(1.2f)))
            using (var path = Round(r, S(2.5f))) g.DrawPath(pen, path);
            Fill(g, new RectangleF(r.Right + S(0.8f), r.Y + r.Height * 0.3f, S(2), r.Height * 0.4f), S(0.8f), Fg);
            float inner = (r.Width - S(3)) * Math.Max(0.06f, pct / 100f);
            Fill(g, new RectangleF(r.X + S(1.5f), r.Y + S(1.5f), inner, r.Height - S(3)), S(1.5f), level);
            if (plugged && !charging) DrawPlug(g, r, Bg);   // plugged in, not charging (full): a plug
            else if (charging)                               // charging: a bolt
                using (var f = Make("Segoe Fluent Icons", S(10)) ?? Make("Segoe MDL2 Assets", S(10)))
                    DrawText(g, "", f, Bg, r, StringAlignment.Center);
        }

        void DrawFlyout(Graphics g, float radius)
        {
            // Tiles: Wi-Fi (information), Mute (toggle), keyboard layout (information).
            bool wifiOn = Get("wifi") != "", muted = Get("mute") == "1";
            string[] glyphs = { WifiGlyph(), muted ? "" : "", "" };
            string[] labels = { wifiOn ? Get("wifi") : "Not connected", muted ? "Muted" : "Sound on", Get("keyboard") };
            bool[] on = { wifiOn, muted, false };
            for (int i = 0; i < 3; i++)
            {
                RectangleF t = tiles[i];
                bool hot = hover == "tile" + i && i == 1;
                Color fill = on[i] ? Config.Accent : Raised;
                if (hot) fill = on[i] ? Mix(Config.Accent, Color.White, 0.15f) : Mix(Raised, Config.Foreground, 0.08f);
                Fill(g, t, S(6), Color.FromArgb(255, fill));
                DrawText(g, glyphs[i], bigIcons, on[i] ? Color.FromArgb(255, Mix(Config.Accent, Color.Black, 0.75f)) : Fg, t, StringAlignment.Center);
                DrawText(g, labels[i], small, Fg, new RectangleF(t.X - S(4), t.Bottom + S(4), t.Width + S(8), S(22)), StringAlignment.Center);
            }
            // Divider.
            float dy = tiles[0].Bottom + S(30) + S(8);
            using (var pen = new Pen(Raised, S(1))) g.DrawLine(pen, flyout.X, dy, flyout.Right, dy);

            if (briTrack.Width > 0) DrawSlider(g, briIcon, "", briTrack, BrightnessLevel(), "brightness");
            DrawSlider(g, volIcon, VolumeGlyph(), volTrack, Volume(), "volume");

            // Footer: battery.
            g.SmoothingMode = SmoothingMode.None;
            g.SetClip(footer);
            Fill(g, flyout, radius, Lower);   // the flyout's shape, so the bottom corners stay round
            g.ResetClip();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int pct = Num("battery");
            DrawBattery(g, new RectangleF(footer.X + S(18), footer.Y + footer.Height / 2 - S(6), S(22), S(12)));
            string text = pct < 0 ? "No battery" : pct + "%";
            DrawText(g, text, font, Fg, new RectangleF(footer.X + S(48), footer.Y, S(80), footer.Height));
            string state = pct < 0 ? "" : Get("charging") == "1" ? "Charging" : Get("plugged") == "1" ? "Plugged in" : "On battery";
            DrawText(g, state, small, Dim, new RectangleF(footer.Right - S(150), footer.Y, S(134), footer.Height), StringAlignment.Far);
        }

        void DrawSlider(Graphics g, RectangleF iconBox, string glyph, RectangleF track, int value, string name)
        {
            bool hot = hover == name + "icon" && name == "volume";
            if (hot) Fill(g, iconBox, S(5), Raised);
            DrawText(g, glyph, bigIcons, Fg, iconBox, StringAlignment.Center);
            Fill(g, track, S(2), Dim);
            if (value < 0) return;
            float x = track.X + track.Width * Math.Max(0, Math.Min(100, value)) / 100f;
            Fill(g, new RectangleF(track.X, track.Y, x - track.X, track.Height), S(2), Config.Accent);
            float r = S(10);
            using (var b = new SolidBrush(Raised)) g.FillEllipse(b, x - r, track.Y + track.Height / 2 - r, 2 * r, 2 * r);
            float r2 = hover == name || dragging == name ? S(7) : S(5.5f);
            using (var b = new SolidBrush(Color.FromArgb(255, Config.Accent))) g.FillEllipse(b, x - r2, track.Y + track.Height / 2 - r2, 2 * r2, 2 * r2);
            if (dragging == name)
            {
                // The value, above the thumb, while dragging.
                var tip = new RectangleF(x - S(20), track.Y - S(34), S(40), S(24));
                Fill(g, tip, S(4), Raised);
                DrawText(g, value.ToString(), small, Fg, tip, StringAlignment.Center);
            }
        }

        // ---------- mouse ----------

        string HitTest(Point p)
        {
            if (trayIcons.Contains(p)) return "tray";
            if (strip.Contains(p)) return "strip";
            if (!expanded) return "";
            if (send == null) return "";   // read-only tray: nothing to press
            if (tiles[1].Contains(p)) return "tile1";
            if (volIcon.Contains(p)) return "volumeicon";
            if (Grab(volTrack).Contains(p)) return "volume";
            if (briTrack.Width > 0 && Grab(briTrack).Contains(p)) return "brightness";
            return "";
        }

        RectangleF Grab(RectangleF track) { return RectangleF.Inflate(track, S(10), S(14)); }

        int SliderValue(RectangleF track, int x)
        {
            return (int)Math.Round(Math.Max(0, Math.Min(1, (x - track.X) / track.Width)) * 100);
        }

        void SetSlider(string name, int x)
        {
            if (name == "volume")
            {
                volLocal = SliderValue(volTrack, x);
                volLocalUntil = DateTime.Now.AddSeconds(2);
                pendingSend = "VOLUME SET " + volLocal;
            }
            else
            {
                briLocal = SliderValue(briTrack, x);
                briLocalUntil = DateTime.Now.AddSeconds(3);
                pendingSend = "BRIGHTNESS SET " + briLocal;
            }
            if (!sendTimer.Enabled) { Command(pendingSend); pendingSend = null; sendTimer.Start(); }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging != null) { SetSlider(dragging, e.X); return; }
            if (downOnStrip && e.Button == MouseButtons.Left &&
                (Math.Abs(e.X - downAt.X) > S(4) || Math.Abs(e.Y - downAt.Y) > S(4)))
            {
                // Moved while holding on the strip: drag the window.
                downOnStrip = false;
                ReleaseCapture();
                SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, new IntPtr(2) /* HTCAPTION */, IntPtr.Zero);
                return;
            }
            string h = HitTest(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
            string part = TipPart(e.Location);
            if (part != tipPart)
            {
                tipPart = part;
                HideTip();
                tipTimer.Stop();
                if (part != "") tipTimer.Start();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hover != "") { hover = ""; Invalidate(); }
            tipPart = "";
            tipTimer.Stop();
            HideTip();
        }

        // Which part of the tray the mouse is on, for its tooltip.
        string TipPart(Point p)
        {
            if (info == null || !Config.ShowTooltips || dragging != null) return "";
            if (kbRect.Contains(p)) return "keyboard";
            if (wifiRect.Contains(p)) return "wifi";
            if (volRect.Contains(p)) return "volume";
            if (batRect.Contains(p) || (expanded && footer.Contains(p))) return "battery";
            if (clockRect.Contains(p)) return "clock";
            return "";
        }

        string TipText(string part)
        {
            switch (part)
            {
                case "keyboard":
                    return (Get("language") + "\n" + Get("keyboard")).Trim();
                case "wifi":
                    return Get("wifi") == "" ? "Not on Wi-Fi" : Get("wifi") + (Get("signal") != "" ? "\nSignal: " + Get("signal") + "%" : "");
                case "volume":
                    return Get("volume") == "" ? "No sound device" : Get("mute") == "1" ? "Muted (" + Get("volume") + "%)" : "Volume: " + Get("volume") + "%";
                case "battery":
                {
                    // Like Windows: one line per battery when there are several.
                    string[] list = Get("batteries").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (list.Length > 1)
                    {
                        var lines = new List<string>();
                        for (int i = 0; i < list.Length; i++)
                        {
                            string[] b = list[i].Split('|');
                            if (b.Length < 3) continue;
                            lines.Add("Battery " + (i + 1) + ": " + b[0] + "% " + (b[1] == "1" ? "available (plugged in)" : "remaining"));
                        }
                        return string.Join("\n", lines.ToArray());
                    }
                    int pct = Num("battery");
                    if (pct < 0) return "No battery";
                    return pct + "% " + (Get("plugged") == "1" ? "available (plugged in" + (Get("charging") == "1" ? ", charging)" : ")") : "remaining");
                }
                case "clock":
                    return Get("longdate") + "\n" + Get("time");
                default:
                    return "";
            }
        }

        void ShowTip()
        {
            string text = TipText(tipPart);
            if (text == "") return;
            // Above the tray when it's low on the screen (like the taskbar), else below.
            RectangleF anchor = tipPart == "battery" && expanded && footer.Contains(PointToClient(Cursor.Position)) ? footer :
                                tipPart == "keyboard" ? kbRect : tipPart == "wifi" ? wifiRect : tipPart == "volume" ? volRect :
                                tipPart == "battery" ? batRect : clockRect;
            // Above the strip's top when the tray sits low on the screen, like the taskbar's; else below.
            Point at = PointToScreen(new Point((int)anchor.X, (int)(upward ? strip.Y - S(8) : strip.Bottom + S(8))));
            if (anchor == footer) at = PointToScreen(new Point((int)anchor.X + (int)S(12), (int)(anchor.Bottom + S(8))));
            try { tip.ShowAt(text, at, upward && anchor != footer, Config, scale); tipShown = tipPart; } catch { }
        }

        void HideTip()
        {
            if (tipShown == "") return;
            tipShown = "";
            try { tip.HideTip(); } catch { }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            tipTimer.Stop();
            HideTip();
            if (e.Button != MouseButtons.Left) return;
            string h = HitTest(e.Location);
            downOnStrip = h == "tray" || h == "strip";
            downAt = e.Location;
            if (h == "volume" || h == "brightness") { dragging = h; Capture = true; SetSlider(h, e.X); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging != null)
            {
                SetSlider(dragging, e.X);
                dragging = null;
                Capture = false;
                Invalidate();
                return;
            }
            if (e.Button == MouseButtons.Right && (HitTest(e.Location) == "tray" || HitTest(e.Location) == "strip"))
            {
                SetExpanded(!expanded);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            bool wasDown = downOnStrip;
            downOnStrip = false;
            string h = HitTest(e.Location);
            if (wasDown && (h == "tray" || h == "strip")) SetExpanded(!expanded);
            else if (h == "tile1" || h == "volumeicon") Command("MUTE");
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x0232 /* WM_EXITSIZEMOVE */) { SaveState(); Arrange(); Invalidate(); }
            else if (m.Msg == 0x02E0 /* WM_DPICHANGED */) { font = null; ApplyLook(); Invalidate(); }
        }
    }
}
