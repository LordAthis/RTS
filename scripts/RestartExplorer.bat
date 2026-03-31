@echo off
timeout /t 20 /nobreak
taskkill /f /im explorer.exe
start explorer.exe
exit
