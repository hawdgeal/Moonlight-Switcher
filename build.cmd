@echo off
rem Rebuilds MoonlightSwitch.exe (this PC) and MoonlightSwitchHost.exe (the host bar, installed on
rem the host from http://<this PC>:47711/), then restarts MoonlightSwitch.exe.
rem It compiles first and swaps the exe after, so the app is down for only a moment: a longer
rem gap would look like the end of the stream to the host bar.
cd /d "%~dp0"
set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
%CSC% /nologo /target:winexe /r:System.Management.dll /out:MoonlightSwitch.new.exe MoonlightSwitch.cs Bar.cs
if errorlevel 1 goto failed
%CSC% /nologo /target:winexe /r:System.Management.dll /out:MoonlightSwitchHost.new.exe HostMain.cs Bar.cs
if errorlevel 1 goto failed
move /y MoonlightSwitchHost.new.exe MoonlightSwitchHost.exe >nul
taskkill /im MoonlightSwitch.exe /f >nul 2>&1
rem Half a second for Windows to release the old exe.
ping -n 1 -w 500 192.0.2.1 >nul
move /y MoonlightSwitch.new.exe MoonlightSwitch.exe >nul
start "" MoonlightSwitch.exe
exit /b 0

:failed
echo.
echo Build failed - see the errors above.
del MoonlightSwitch.new.exe MoonlightSwitchHost.new.exe >nul 2>&1
pause
exit /b 1
