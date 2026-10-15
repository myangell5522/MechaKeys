@echo off
rem Uploads preview.gif as the animated Steam Workshop preview for Mecha Keys.
rem Run this after every publish/update from inside tModLoader.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0upload_preview.ps1" %*
echo.
pause
