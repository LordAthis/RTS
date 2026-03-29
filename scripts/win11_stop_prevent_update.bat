@echo off
setlocal enabledelayedexpansion
title Windows 11 Frissites Kezelo + Naplozas

set LOGFILE=%~dp0win11_update_log.txt

:MENU
cls
echo ===========================================
echo    Windows 11 Frissites Tiltas/Engedelyezes
echo ===========================================
echo.
echo 1. Windows 11 frissites LETILTASA (Win 10 rogzitese)
echo 2. Eredeti allapot VISSZAALLITASA (Frissites engedelyezese)
echo 3. Kilepes
echo.
set /p choice="Valassz egy opciot (1-3): "

if "%choice%"=="1" goto LOCK
if "%choice%"=="2" goto UNLOCK
if "%choice%"=="3" exit
goto MENU

:LOCK
cls
echo [%date% %time%] - LETILTAS INDITVA >> "%LOGFILE%"
echo Szolgaltatasok leallitasa a biztos beallitashoz...
net stop wuauserv >nul 2>&1
net stop bits >nul 2>&1

for /f "tokens=3" %%a in ('reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v DisplayVersion') do set WIN_VER=%%a
if "%WIN_VER%"=="" (
    for /f "tokens=3" %%a in ('reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v ReleaseId') do set WIN_VER=%%a
)

echo Aktualitas Windows 10 verzio: %WIN_VER%
echo Beallitasok alkalmazasa a Registry-ben...

reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" /v TargetReleaseVersion /t REG_DWORD /d 1 /f >nul
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" /v ProductVersion /t REG_SZ /d "Windows 10" /f >nul
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" /v TargetReleaseVersionInfo /t REG_SZ /d "%WIN_VER%" /f >nul

echo [%date% %time%] - SIKERES: Win 10 rogzitve (%WIN_VER%) >> "%LOGFILE%"
echo.
echo SIKERES: A rendszer rogzitve a Windows 10 %WIN_VER% verziojanal.
goto REBOOT_PROMPT

:UNLOCK
cls
echo [%date% %time%] - VISSZAALLITAS INDITVA >> "%LOGFILE%"
echo Szolgaltatasok leallitasa a visszaallitashoz...
net stop wuauserv >nul 2>&1
net stop bits >nul 2>&1

echo Korlatozasok eltavolitasa...
reg delete "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" /v TargetReleaseVersion /f >nul
reg delete "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" /v ProductVersion /f >nul
reg delete "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" /v TargetReleaseVersionInfo /f >nul

echo [%date% %time%] - SIKERES: Korlatozasok torolve >> "%LOGFILE%"
echo.
echo SIKERES: A korlatozasok torolve.
goto REBOOT_PROMPT

:REBOOT_PROMPT
echo.
echo ====================================================
echo FIGYELEM: A valtozasok ervenybe leptetesehez
echo UJRAINDITAS SZUKSEGES!
echo ====================================================
echo.
set /p rb="Szeretned most ujrainditani a gepet? (I/N): "
if /i "%rb%"=="I" (
    echo [%date% %time%] - Rendszer ujrainditasa... >> "%LOGFILE%"
    shutdown /r /t 5 /c "A Windows Update beallitasok veglegesitese..."
    exit
)
if /i "%rb%"=="N" (
    echo Szolgaltatasok ujrainditasa...
    net start bits >nul 2>&1
    net start wuauserv >nul 2>&1
    goto MENU
)
exit
