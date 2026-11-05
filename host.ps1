# MoonlightSwitch host bar launcher.
#
# This file lives on the CLIENT PC, next to MoonlightSwitch.exe, which serves it. On the host, a
# login entry (set up with install-host.ps1) keeps running it. It downloads the bar (Bar.cs) and its
# settings (settings.ini) from the client, so edit those files on the client to change how the bar
# looks or works. When Bar.cs or this file change, the running bar restarts itself with the update.
#
#   -Client <ip>   the client PC's address
#   -Preview       show the bar right away and keep it open, even without a stream (for testing)
param([string]$Client = '', [switch]$Preview)

if (-not $Client) { return }

# ---------- Single instance: a new copy replaces the old one, so edits take effect ----------
$pidFile = Join-Path $env:TEMP 'MoonlightSwitchHost.pid'
if (Test-Path $pidFile) {
    $oldPid = 0
    if ([int]::TryParse((Get-Content $pidFile -ErrorAction SilentlyContinue | Select-Object -First 1), [ref]$oldPid) -and $oldPid -ne $PID) {
        # Only stop it if it really is an old bar. Windows reuses process numbers, and the login loop
        # (a PowerShell process too) must never be stopped by mistake.
        $old = Get-CimInstance Win32_Process -Filter "ProcessId=$oldPid" -ErrorAction SilentlyContinue
        if ($old -and $old.Name -eq 'powershell.exe' -and $old.CommandLine -match 'EncodedCommand|host\.ps1' -and $old.CommandLine -notmatch 'boot\.ps1') {
            Stop-Process -Id $oldPid -Force -ErrorAction SilentlyContinue
        }
    }
}
$mutex = New-Object System.Threading.Mutex($false, 'MoonlightSwitch_HostScript')
try { $owned = $mutex.WaitOne(5000) } catch [System.Threading.AbandonedMutexException] { $owned = $true }
if (-not $owned) { return }
Set-Content -Path $pidFile -Value $PID

try {
    $source = (Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 "http://${Client}:47711/bar.cs").Content
    Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing -TypeDefinition $source
    [MoonlightSwitchUI.HostAgent]::Run($Client, [bool]$Preview)
}
finally {
    Remove-Item $pidFile -ErrorAction SilentlyContinue
    $mutex.ReleaseMutex()
}
