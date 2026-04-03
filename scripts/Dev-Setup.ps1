<#
.SYNOPSIS
    RTS Fejlesztői Környezet Automatikus Telepítő
    Rendszergazdai joggal és Winget használatával.
#>

# 1. Önemelés rendszergazdai jogra
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Write-Host "--- RTS Fejlesztői Környezet Kiépítése ---" -ForegroundColor Cyan

# 2. Alapvető eszközök listája (Winget ID-k)
$apps = @(
    "Microsoft.VisualStudio.2022.Community",
    "Git.Git",
    "Microsoft.VisualStudioCode",
    "Microsoft.DotNet.SDK.8"  # Ha az RTS-nek szüksége van a legújabb .NET-re
)

foreach ($app in $apps) {
    Write-Host "Telepítés folyamatban: $app..." -ForegroundColor Yellow
    # --silent: nincs telepítő ablak, --accept-package-agreements: elfogadja a licencet
    winget install --id $app --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [OK] $app sikeresen telepítve." -ForegroundColor Green
    } else {
        Write-Host "  [!] $app telepítése során hiba történt vagy már létezik." -ForegroundColor Gray
    }
}

# 3. Visual Studio specifikus komponensek (Workloads)
# Ez az RTS fordításához szükséges C++ vagy .NET desktop modulokat adja hozzá
Write-Host "Visual Studio workload-ok konfigurálása..." -ForegroundColor Yellow
$vsInstallerPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"

if (Test-Path $vsInstallerPath) {
    # Példa: .NET Desktop és C++ Desktop fejlesztés hozzáadása
    Start-Process $vsInstallerPath -ArgumentList "modify --installPath ""C:\Program Files\Microsoft Visual Studio\2022\Community"" --add Microsoft.VisualStudio.Workload.ManagedDesktop --add Microsoft.VisualStudio.Workload.NativeDesktop --passive --norestart" -Wait
    Write-Host "  [OK] VS Workload-ok hozzáadva." -ForegroundColor Green
}

Write-Host "`n--- Telepítés befejezve! ---" -ForegroundColor Cyan
pause
