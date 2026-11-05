@echo off
rem Rebuilds MoonlightSwitch.exe after you edit MoonlightSwitch.cs or Bar.cs, then restarts it.
rem (The host bar picks up Bar.cs changes by itself.)
cd /d "%~dp0"
taskkill /im MoonlightSwitch.exe /f >nul 2>&1
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:MoonlightSwitch.exe MoonlightSwitch.cs Bar.cs
if errorlevel 1 (
  echo.
  echo Build failed - see the errors above.
  pause
  exit /b 1
)
start "" MoonlightSwitch.exe
