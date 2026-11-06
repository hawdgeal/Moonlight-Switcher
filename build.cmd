@echo off
rem Rebuilds MoonlightSwitch.exe after you edit MoonlightSwitch.cs or Bar.cs, then restarts it.
rem (The host bar picks up Bar.cs changes by itself.)
rem It compiles first and swaps the exe after, so the app is down for only a moment: a longer
rem gap would look like the end of the stream to the host bar.
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:MoonlightSwitch.new.exe MoonlightSwitch.cs Bar.cs
if errorlevel 1 (
  echo.
  echo Build failed - see the errors above.
  pause
  exit /b 1
)
taskkill /im MoonlightSwitch.exe /f >nul 2>&1
rem Half a second for Windows to release the old exe.
ping -n 1 -w 500 192.0.2.1 >nul
move /y MoonlightSwitch.new.exe MoonlightSwitch.exe >nul
start "" MoonlightSwitch.exe
