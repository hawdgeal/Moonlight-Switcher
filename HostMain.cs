// HostMain.cs - the host bar as a program of its own: MoonlightSwitchHost.exe (with Bar.cs).
//
// Downloaded from the client's install page and run once, it asks, then installs itself:
// copies itself to %LOCALAPPDATA%\MoonlightSwitch, writes host.ini there and adds a Startup
// shortcut. Started from there (at sign-in), it runs the bar. It never downloads or runs code:
// from the client it only reads the stream state and settings.ini.
//
//   -Preview        show the bar right away, even without a stream (for testing; no install)
//   -Client <ip>    the client PC's address (default: Client in host.ini)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MoonlightSwitchUI
{
    static class HostProgram
    {
        public static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoonlightSwitch");
        public static readonly string InstalledExe = Path.Combine(Folder, "MoonlightSwitchHost.exe");
        public static readonly string IniPath = Path.Combine(Folder, "host.ini");
        public static readonly string StartupLink =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Moonlight Switch bar.lnk");

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            bool preview = false;
            string client = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].TrimStart('-', '/').ToLowerInvariant();
                if (a == "preview") preview = true;
                else if (a == "client" && i + 1 < args.Length) client = args[++i];
            }

            string exe = Application.ExecutablePath;
            bool installed = string.Equals(Path.GetFullPath(exe), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);
            if (!installed && !preview) return Installer.Run(exe);

            if (string.IsNullOrEmpty(client)) client = HostIni.Read().Get("client");
            if (string.IsNullOrEmpty(client))
            {
                MessageBox.Show("The client PC's address isn't set. Run the installer again, or set Client in\n" + IniPath,
                                "Moonlight Switch host bar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }

            // One bar per user session (a preview can run next to it).
            Mutex mutex = null;
            if (!preview)
            {
                bool created;
                mutex = new Mutex(true, "MoonlightSwitch_HostBar", out created);
                if (!created) return 0;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) => HostAgent.LogLine("exit: unexpected error: " + e.ExceptionObject);
            Application.ThreadException += (s, e) => HostAgent.LogLine("error (kept running): " + e.Exception);
            HostAgent.LocalCommand = streaming => HostIni.Read().Get(streaming ? "hoststreamstart" : "hoststreamend");
            HostAgent.ResetLanguageBarCommand = () => HostIni.Read().Get("hostresetlanguagebar");
            try { HostAgent.OwnFingerprint = Fingerprint.OfFile(exe); } catch { }
            HostAgent.LogLine("start " + (preview ? "(preview) " : "") + "pid " + Process.GetCurrentProcess().Id + ", client " + client +
                              ", fingerprint " + (HostAgent.OwnFingerprint.Length > 12 ? HostAgent.OwnFingerprint.Substring(0, 12) : "?"));

            HostAgent.Run(client, preview);

            HostAgent.LogLine(HostAgent.RestartRequested ? "exit: restarting" : "exit: closed");
            if (mutex != null) { mutex.ReleaseMutex(); mutex.Dispose(); }
            if (HostAgent.RestartRequested)
            {
                try { Process.Start(new ProcessStartInfo(exe, string.Join(" ", args)) { UseShellExecute = false }); } catch { }
            }
            return 0;
        }
    }

    // host.ini: settings that belong to the host only (the client's address and the commands to
    // run on stream start/end). key = value lines; # starts a comment line.
    class HostIni
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static HostIni Read()
        {
            var ini = new HostIni();
            try
            {
                foreach (string raw in File.ReadAllLines(HostProgram.IniPath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int eq = line.IndexOf('=');
                    if (eq > 0) ini.values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch { }
            return ini;
        }

        public string Get(string key) { string v; return values.TryGetValue(key, out v) ? v : ""; }

        // Creates host.ini, or sets Client in an existing one (keeping everything else).
        public static void SaveClient(string client)
        {
            if (!File.Exists(HostProgram.IniPath))
            {
                File.WriteAllText(HostProgram.IniPath,
                    "# Moonlight Switch host bar: settings for this PC only.\r\n" +
                    "\r\n" +
                    "# The client PC's address (the PC running Moonlight and MoonlightSwitch.exe).\r\n" +
                    "Client = " + client + "\r\n" +
                    "\r\n" +
                    "# Commands to run on this PC when a stream starts, and when it ends (about 6 seconds after).\r\n" +
                    "# Leave empty for none. Read at each stream, so edits apply without a restart. Example:\r\n" +
                    "#   HostStreamStart = powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"C:\\path\\to\\script.ps1\" -Mode stream\r\n" +
                    "HostStreamStart =\r\n" +
                    "HostStreamEnd   =\r\n" +
                    "\r\n" +
                    "# Command run by the host bar's \"Reset language bar\" button, on this PC. Empty = no button.\r\n" +
                    "HostResetLanguageBar =\r\n");
                return;
            }
            var lines = new List<string>(File.ReadAllLines(HostProgram.IniPath));
            int at = lines.FindIndex(l => l.TrimStart().StartsWith("client", StringComparison.OrdinalIgnoreCase) && l.Contains("="));
            if (at >= 0) lines[at] = "Client = " + client;
            else lines.Insert(0, "Client = " + client);
            File.WriteAllLines(HostProgram.IniPath, lines.ToArray());
        }
    }

    // The one-time setup, run when the downloaded file is opened. Asks first, shows the
    // fingerprint to compare with the install page, and changes nothing until you click Install.
    static class Installer
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool DeleteFile(string name);

        public static int Run(string exe)
        {
            string fingerprint = "";
            try { fingerprint = Fingerprint.OfFile(exe); } catch { }
            string guess = ClientFromDownload(exe);
            if (guess == "") guess = HostIni.Read().Get("client");

            using (var form = new Form())
            {
                form.Text = "Install the Moonlight Switch host bar";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = form.MinimizeBox = false;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.AutoScaleMode = AutoScaleMode.Dpi;
                form.AutoSize = true;
                form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                form.Padding = new Padding(12);

                var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
                layout.Controls.Add(new Label
                {
                    AutoSize = true, MaximumSize = new Size(520, 0),
                    Text = "This copies the host bar to " + HostProgram.Folder + " and starts it at every sign-in " +
                           "(Startup folder). It shows the bar while this PC is being streamed with Moonlight.\n\n" +
                           "Client PC address (the PC running Moonlight):"
                });
                var clientBox = new TextBox { Text = guess, Width = 220 };
                layout.Controls.Add(clientBox);
                layout.Controls.Add(new Label { AutoSize = true, Text = "\nFingerprint (should match the install page):" });
                layout.Controls.Add(new TextBox { Text = fingerprint, ReadOnly = true, Width = 520, Font = new Font(FontFamily.GenericMonospace, 8.5f) });
                var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
                var install = new Button { Text = "Install", DialogResult = DialogResult.OK, AutoSize = true };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(install);
                layout.Controls.Add(buttons);
                form.Controls.Add(layout);
                form.AcceptButton = install;
                form.CancelButton = cancel;

                while (true)
                {
                    if (form.ShowDialog() != DialogResult.OK) return 1;
                    string client = clientBox.Text.Trim();
                    IPAddress ip;
                    if (IPAddress.TryParse(client, out ip)) return Install(exe, client);
                    MessageBox.Show(form, "Enter the client PC's IP address, e.g. 192.168.1.50.", form.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        static int Install(string exe, string client)
        {
            try
            {
                StopRunningBars();
                Directory.CreateDirectory(HostProgram.Folder);
                for (int i = 0; ; i++)
                {
                    try { File.Copy(exe, HostProgram.InstalledExe, true); break; }
                    catch (IOException) { if (i >= 20) throw; Thread.Sleep(250); }   // an old bar still letting go of the file
                }
                // The copy is now an installed program, not a fresh download: without this, Windows
                // would ask about it at every sign-in.
                DeleteFile(HostProgram.InstalledExe + ":Zone.Identifier");
                HostIni.SaveClient(client);
                CreateStartupShortcut();
                Process.Start(new ProcessStartInfo(HostProgram.InstalledExe) { WorkingDirectory = HostProgram.Folder, UseShellExecute = false });
                HostAgent.LogLine("installed, client " + client);
            }
            catch (Exception e)
            {
                MessageBox.Show("Installing failed: " + e.Message, "Moonlight Switch host bar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            if (MessageBox.Show("Installed. The bar is running and starts at every sign-in.\n\n" +
                                "Commands to run when a stream starts or ends go in host.ini. Open it now?",
                                "Moonlight Switch host bar", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Process.Start("notepad.exe", "\"" + HostProgram.IniPath + "\""); } catch { }
            }
            return 0;
        }

        // Stops a running host bar, and the PowerShell login loop of the old version (and its bar),
        // so only the new one is left.
        static void StopRunningBars()
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("MoonlightSwitchHost"))
            {
                try { if (p.Id != me) { p.Kill(); p.WaitForExit(3000); } } catch { }
                p.Dispose();
            }
            try
            {
                var loops = new List<uint>();
                var all = new List<ManagementObject>();
                using (var search = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId, CommandLine FROM Win32_Process WHERE Name='powershell.exe'"))
                    foreach (ManagementObject o in search.Get()) all.Add(o);
                foreach (var o in all)
                {
                    string cmd = (o["CommandLine"] as string) ?? "";
                    if (cmd.Contains("boot.ps1") && cmd.Contains(":47711/")) loops.Add((uint)o["ProcessId"]);
                }
                foreach (var o in all)
                {
                    uint pid = (uint)o["ProcessId"], parent = (uint)o["ParentProcessId"];
                    if (loops.Contains(pid) || loops.Contains(parent))
                        try { Process.GetProcessById((int)pid).Kill(); } catch { }
                }
            }
            catch { }
        }

        static void CreateStartupShortcut()
        {
            // Made from scratch: updating the old version's shortcut would keep its PowerShell arguments.
            if (File.Exists(HostProgram.StartupLink)) File.Delete(HostProgram.StartupLink);
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            try
            {
                object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { HostProgram.StartupLink });
                Type t = link.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { HostProgram.InstalledExe });
                t.InvokeMember("Arguments", BindingFlags.SetProperty, null, link, new object[] { "" });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { HostProgram.Folder });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "Moonlight Switch host bar" });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                Marshal.FinalReleaseComObject(link);
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }

        // The browser records where a download came from (HostUrl=http://<client>:47711/...), which
        // gives the client's address without typing it.
        static string ClientFromDownload(string exe)
        {
            try
            {
                byte[] raw;
                using (var h = CreateFile(exe + ":Zone.Identifier", 0x80000000 /* read */, 1 /* share read */, IntPtr.Zero, 3 /* open existing */, 0, IntPtr.Zero))
                {
                    if (h.IsInvalid) { HostAgent.LogLine("installer: no download info (error " + Marshal.GetLastWin32Error() + ")"); return ""; }
                    using (var f = new FileStream(h, FileAccess.Read))
                    using (var m = new MemoryStream()) { f.CopyTo(m); raw = m.ToArray(); }
                }
                // Browsers write it as plain text; accept UTF-16 too (with or without a byte order mark).
                bool utf16 = raw.Length > 1 && (raw[1] == 0 || (raw[0] == 0xFF && raw[1] == 0xFE));
                string text = (utf16 ? Encoding.Unicode : Encoding.UTF8).GetString(raw).TrimStart('﻿');
                foreach (string raw2 in text.Split('\n'))
                {
                    string line = raw2.Trim().TrimStart('﻿');
                    foreach (string key in new[] { "HostUrl=", "ReferrerUrl=" })
                    {
                        if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
                        Uri u;
                        IPAddress ip;
                        if (Uri.TryCreate(line.Substring(key.Length).Trim(), UriKind.Absolute, out u) && IPAddress.TryParse(u.Host, out ip)) return u.Host;
                    }
                }
                HostAgent.LogLine("installer: download info has no client address: " + text.Replace("\r", "").Replace("\n", " | "));
            }
            catch { }
            return "";
        }
    }
}
