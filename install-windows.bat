@echo off
rem Opens the installer window. -STA is what WinForms needs, and the execution policy is
rem bypassed for this one file only - nothing on the machine is changed by running it.
rem
rem If PowerShell cannot even start the script, the window below stays open with the reason.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0install-windows.ps1"
if errorlevel 1 (
    echo.
    echo The installer did not start. The reason is above this line.
    pause
)
