# Moonlight Switcher

A small companion for [Moonlight](https://moonlight-stream.org/) on Windows that makes it easy to jump
between the streamed PC (the **host**, running [Sunshine](https://github.com/LizardByte/Sunshine))
and your own desktop (the **client**) without fiddling with Moonlight's keyboard shortcuts.

- **A bar on both screens.** It sits at a screen edge, tucked away as a thin handle until you hover
  over it. On the client it has **Host** / **Client** buttons. On the host (inside the stream) it has
  **Back to my PC**. Both have a menu of Moonlight's shortcuts and a **Quit** button.
- **One hotkey:** `Ctrl+Alt+Home` toggles between host and client. The mouse and keyboard are
  released properly when you leave the stream.
- **Disconnect / Quit / Sleep host.** *Disconnect* ends the stream and keeps Moonlight open.
  *Quit* closes Moonlight. *Sleep host* (host bar) ends the stream and puts the host to sleep.
- **Moonlight in the tray.** Moonlight's stream window stays out of the taskbar while you're on your
  desktop. A tray icon (with Moonlight's icon) shows it again.
- **Draggable, dockable bars.** Drag a bar to any edge of any screen. Colors, labels, sizes and
  behaviour live in one `settings.ini`, applied live.
- **Host-side extras:** switch the host's keyboard layout while streaming (for example to match the client's) and run your own commands
  when a stream starts and ends, without Sunshine's Command Preparations.
- **One settings file.** The host bar reads its looks and keyboard settings from the client's
  `settings.ini`, live. Code updates are installed on the host when you choose to.

The client app starts Moonlight with it and exits when Moonlight closes.

## Requirements

- Windows 10 or 11 on both PCs.
- Moonlight installed on the client in its default folder
  (`C:\Program Files\Moonlight Game Streaming`).
- Sunshine on the host.
- The .NET Framework 4.x that ships with Windows. Nothing else to install.

## Setup

### Client (the PC running Moonlight)

1. Put this folder anywhere, for example `C:\Tools\MoonlightSwitch`.
2. Run `build.cmd`. It compiles `MoonlightSwitch.exe` and the host bar `MoonlightSwitchHost.exe`
   with the C# compiler built into Windows, and starts `MoonlightSwitch.exe`.
3. Make a shortcut to `MoonlightSwitch.exe` and use it instead of Moonlight's own shortcut. It opens
   Moonlight, the bar and the tray icon together. Clicking it again while Moonlight is running brings
   Moonlight to the front.
4. When Windows Firewall asks, allow it on your private network. The app listens on TCP 47711 (to
   serve the host bar) and UDP 47710 (requests from the host bar).

`settings.ini` is created next to the exe on first run. Edit it from the bar's right-click menu or
the tray menu.

### Host (the PC running Sunshine)

With the client app running, open a browser on the host at the client's address (for example
`http://192.168.1.50:47711/`) and follow the page:

1. Download `MoonlightSwitchHost.exe` and run it. It isn't signed, so Windows may warn about an
   unknown publisher (**More info** → **Run anyway**).
2. Check that the fingerprint it shows matches the one on the page, and click **Install**.

It copies itself to `%LOCALAPPDATA%\MoonlightSwitch`, adds `Moonlight Switch bar.lnk` to the Startup
folder and starts. The bar shows up while you're streaming and hides otherwise.

Commands the host should run when a stream starts or ends go in
`%LOCALAPPDATA%\MoonlightSwitch\host.ini` on the host (`HostStreamStart`, `HostStreamEnd`).

**Updating:** after you rebuild on the client, the host bar's menu shows *Update available*. Open
the install page and repeat the steps.

**Removing:** delete `Moonlight Switch bar.lnk` from the Startup folder (`shell:startup`), end
`MoonlightSwitchHost.exe` in Task Manager and delete `%LOCALAPPDATA%\MoonlightSwitch`.

To preview the host bar without a stream: `MoonlightSwitchHost.exe -Preview -Client 192.168.1.50`.

## Settings

All options are listed, with comments, in the `settings.ini` created on first run (its default text
is in `Bar.cs`). Highlights:

| Setting | What it does |
|---|---|
| `Background`, `Foreground`, `Accent`, ... | Colors, as `#RRGGBB` or `#AARRGGBB` |
| `Scale`, `FontSize`, `CornerRadius` | Size and shape |
| `ShowLabels`, `ShowIcons` | Icon-only or text buttons |
| `HideDelayMs`, `PinOpen` | Auto-hide behaviour |
| `ClientEdge`, `HostEdge` (+ `Offset`) | Default positions (dragging overrides them) |
| `ConfirmQuit` | Ask before quitting |
| `MoonlightInTray` | Hide the stream from the taskbar while you're on your desktop |
| `HostKeyboardStream` | Host keyboard layout during a stream: `client` (match the client's layout) or an id like `04090409` (US) |
| `HostKeyboardIdle` | Host keyboard layout after a stream (empty = back to the host's own) |
| `Shortcut = label \| KEY` | Entries of the shortcuts menu (sent as `Ctrl+Alt+Shift+KEY`) |

## How it works

- **Client:** `MoonlightSwitch.cs` + `Bar.cs`, compiled into `MoonlightSwitch.exe`. It finds
  Moonlight's windows, switches between them with a low-level keyboard hook and Moonlight's own
  shortcuts, and serves the stream state, `settings.ini` and the host install page over HTTP on
  port 47711.
- **Host:** `HostMain.cs` + `Bar.cs`, compiled into `MoonlightSwitchHost.exe` and installed on the
  host. The bar polls the client's `/state` twice a second (streaming or not, settings version,
  latest host program, keyboard layout) and sends its requests (`CLIENT`, `DISCONNECT`, `QUIT`,
  `KEY x`) over UDP port 47710.

## Security

Use this on a home network you trust. The HTTP server and the UDP requests have no authentication.
The host bar never runs code or commands it receives: it only reads the stream state and display
settings, and its commands live in the host's own `host.ini`. Something on your network posing as
the client could still show or hide the bar or change its looks or keyboard layout, and anything
on your network can send the client app requests such as Quit. Don't expose ports 47710/47711 to the internet.

## Notes

- *Sleep host* turns the host's screen off, which sleeps laptops that use Modern Standby. Waking it
  remotely (Moonlight's *Wake PC*) generally needs a wired Ethernet connection on the host. Wi-Fi
  wake-on-LAN rarely works from sleep.
- Moonlight hides its main window during a stream, so its PC list only comes back once you
  disconnect.

## License

[MIT](LICENSE)
