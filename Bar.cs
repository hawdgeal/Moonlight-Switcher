// Bar.cs - the look and behaviour of both MoonlightSwitch bars.
//
// Compiled into MoonlightSwitch.exe (the bar on this PC) and downloaded + compiled by host.ps1
// (the bar on the host), so editing this file changes both. Colors, labels and sizes live in
// settings.ini, which both bars reload live. After editing this file, run build.cmd for the
// client's bar; the host bar notices the change and restarts with it by itself.
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
        public bool MoonlightInTray = true;
        public string HostStreamStart = "";   // command the host bar runs on the host when a stream starts
        public string HostStreamEnd = "";     // ...and when it ends
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
ShowLabels   = true     # false = icon-only buttons
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

# --- Moonlight ---
MoonlightInTray = true   # hide Moonlight's taskbar buttons; use the tray icon instead

# --- Commands the host bar runs on the host ---
# Run when a stream starts and when it ends (about 6 seconds after), instead of Sunshine's
# Command Preparations. Leave empty for none. Example:
#   HostStreamStart = powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""C:\path\to\script.ps1"" -Mode stream
HostStreamStart =
HostStreamEnd   =
# Keyboard layout to switch to on the host, as 8 hex digits (e.g. 04090409 = US). It must be
# installed there. Switched instantly by the bar itself. Leave empty to not switch.
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
                case "moonlightintray": MoonlightInTray = B(v); break;
                case "hoststreamstart": HostStreamStart = v; break;
                case "hoststreamend": HostStreamEnd = v; break;
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
            return b;
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
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, ref IntPtr value, uint flags);
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
        readonly List<BarButton> buttons;
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
            this.buttons = buttons;
            this.positionFile = positionFile;
            this.defaultScreen = defaultScreen;
            this.extraMenuItems = extraMenuItems;
            Config = config ?? new BarConfig();
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
            bool labels = Config.ShowLabels || !icons;
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
            bool keepOpen = zone.Contains(c) || menuOpen || dragging || pressed >= 0 || dragArmed || PinnedOpen || Config.PinOpen;
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
            if (e.Button == MouseButtons.Right) { ShowMenu(c); return; }
            if (e.Button != MouseButtons.Left || t < 0.95) return;
            pressPoint = c;
            pressed = hover;
            dragArmed = true;
            lastFrameKey = null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragArmed && !dragging)
            {
                Point c = Cursor.Position;
                double dx = c.X - pressPoint.X, dy = c.Y - pressPoint.Y;
                if (Math.Sqrt(dx * dx + dy * dy) > 6 * scale) { dragging = true; pressed = -1; }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int clicked = -1;
            if (dragging) SavePosition();
            else if (pressed >= 0 && pressed == hover) clicked = pressed;
            dragging = false; dragArmed = false; pressed = -1; lastFrameKey = null;
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

    // Runs the bar on the host. Started by host.ps1, which Sunshine runs at each stream start.
    public static class HostAgent
    {
        const int SwitchPort = 47710, ServerPort = 47711;
        const string MsgPrefix = "MOONLIGHTSWITCH/1 ";

        public static void Run(string clientIp, bool preview)
        {
            BarWindow.UseCrispScaling();
            string baseUrl = "http://" + clientIp + ":" + ServerPort;
            BarConfig cfg;
            try { cfg = BarConfig.Parse(Http(baseUrl + "/settings.ini")); } catch { cfg = new BarConfig(); }
            IPAddress client = IPAddress.Parse(clientIp);

            BarWindow bar = null;
            var buttons = new List<BarButton>();
            buttons.Add(new BarButton(c => c.BackLabel, Glyphs.Back, () => Send(client, "CLIENT"), () => true, null));
            BarButton shortcuts = null;
            shortcuts = new BarButton(c => c.ShortcutsLabel, Glyphs.More,
                () =>
                {
                    var items = BarWindow.ShortcutItems(bar.Config, k => Send(client, "KEY " + k));
                    items.Add(new ToolStripSeparator());
                    items.Add(new ToolStripMenuItem("Disconnect (keep Moonlight open)", null, delegate { Send(client, "DISCONNECT"); }));
                    items.Add(new ToolStripMenuItem("Sleep host", null, delegate { SleepHost(client); }));
                    bar.ShowItems(items, shortcuts);
                }, null, null);
            shortcuts.GapBefore = true;
            shortcuts.IsVisible = c => c.ShowShortcutsButton;
            buttons.Add(shortcuts);
            BarButton quit = BarButton.Quit(() => Send(client, "QUIT"), null, () => bar.Config.ConfirmQuit);
            quit.GapBefore = false;
            buttons.Add(quit);

            bar = new BarWindow("host", cfg, buttons, PositionFile(), () => Screen.PrimaryScreen, () =>
            {
                var items = new List<ToolStripItem>();
                var sub = new ToolStripMenuItem("Moonlight shortcuts");
                foreach (var it in BarWindow.ShortcutItems(bar.Config, k => Send(client, "KEY " + k))) sub.DropDownItems.Add(it);
                items.Add(sub);
                items.Add(new ToolStripMenuItem("Disconnect (keep Moonlight open)", null, delegate { Send(client, "DISCONNECT"); }));
                items.Add(new ToolStripMenuItem("Quit Moonlight", null, delegate { Send(client, "QUIT"); }));
                items.Add(new ToolStripMenuItem("Sleep host", null, delegate { SleepHost(client); }));
                items.Add(new ToolStripSeparator());
                items.Add(new ToolStripMenuItem("Restart this bar", null, delegate { bar.Close(); }));
                return items;
            });
            // Only show while this PC is being streamed (or right away in preview mode).
            bar.OwnerHidden = !preview;
            bar.PinnedOpen = preview;
            bar.Load += delegate
            {
                var poll = new Thread(() => Poll(bar, baseUrl, preview));
                poll.IsBackground = true;
                poll.Start();
            };
            Application.EnableVisualStyles();
            Application.Run(bar);
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
        static void Poll(BarWindow bar, string baseUrl, bool preview)
        {
            string lastVersion = null, firstCodeVersion = null;
            int idle = 0;
            bool active = false;      // a stream is on
            BarConfig cfg = null;
            while (true)
            {
                bool streaming = false, codeChanged = false;
                string version = null, settings = null;
                try
                {
                    string[] s = Http(baseUrl + "/state").Split('\n');
                    streaming = s[0].Trim() == "streaming";
                    if (s.Length > 1) version = s[1].Trim();
                    if (s.Length > 2)
                    {
                        string code = s[2].Trim();
                        if (firstCodeVersion == null) firstCodeVersion = code;
                        else if (code != firstCodeVersion) codeChanged = true;
                    }
                }
                catch { }   // client unreachable counts as "not streaming"
                if (codeChanged && !preview)
                {
                    // Bar.cs or host.ps1 changed on the client: exit so the login loop starts the new version.
                    try { bar.BeginInvoke((MethodInvoker)delegate { bar.Close(); }); } catch { }
                    return;
                }
                if (version != null && version != lastVersion)
                {
                    try { settings = Http(baseUrl + "/settings.ini"); lastVersion = version; cfg = BarConfig.Parse(settings); } catch { }
                }

                // Stream started / ended (ended = ~6 s without a stream): show or hide the bar,
                // and run the start/end commands from settings.ini.
                bool changed = false;
                if (streaming) { idle = 0; if (!active) { active = true; changed = true; } }
                else if (++idle >= 12 && active) { active = false; changed = true; }   // ~6 s at 0.5 s per check
                if (changed && !preview && cfg != null)
                {
                    // Keyboard first: it's what you notice. Then the slower command (e.g. a script).
                    SwitchKeyboard(active ? cfg.HostKeyboardStream : cfg.HostKeyboardIdle);
                    RunCommand(active ? cfg.HostStreamStart : cfg.HostStreamEnd);
                }
                CheckSleep(streaming, idle);

                bool show = active;
                try
                {
                    bar.BeginInvoke((MethodInvoker)delegate
                    {
                        if (settings != null) bar.ApplyConfig(BarConfig.Parse(settings));
                        if (!preview) bar.OwnerHidden = !show;
                    });
                }
                catch { return; }   // the bar was closed
                Thread.Sleep(500);
            }
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

        // Switches the keyboard layout: the default for new windows, plus the window you're typing in.
        // Unlike a broadcast to every window, this leaves the taskbar's language indicator alone.
        static void SwitchKeyboard(string hex)
        {
            IntPtr hkl = FindKeyboardLayout(hex);
            if (hkl == IntPtr.Zero) return;
            try
            {
                BarNative.SystemParametersInfo(0x005A /* SPI_SETDEFAULTINPUTLANG */, 0, ref hkl, 2 /* SPIF_SENDCHANGE */);
                BarNative.PostMessage(BarNative.GetForegroundWindow(), 0x0050 /* WM_INPUTLANGCHANGEREQUEST */, IntPtr.Zero, hkl);
                // The stream start can shuffle windows; ask the active window once more a moment later.
                var again = new Thread(delegate ()
                {
                    Thread.Sleep(1500);
                    try { BarNative.PostMessage(BarNative.GetForegroundWindow(), 0x0050, IntPtr.Zero, hkl); } catch { }
                });
                again.IsBackground = true;
                again.Start();
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
        static void Send(IPAddress client, string command)
        {
            byte[] data = Encoding.ASCII.GetBytes(MsgPrefix + command);
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
}
