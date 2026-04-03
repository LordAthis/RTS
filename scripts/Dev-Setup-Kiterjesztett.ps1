<#
.SYNOPSIS
    RTS Full Stack Fejlesztői Környezet (XP portolástól a modern .NET 9-ig)
#>

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Write-Host "--- RTS MULTI-TARGET FEJLESZTŐI KÖRNYEZET KIÉPÍTÉSE ---" -ForegroundColor Cyan

# 1. Alapvető szoftverek telepítése Winget-tel
$apps = @(
    "Microsoft.VisualStudio.2022.Community",
    "Git.Git",
    "Microsoft.DotNet.SDK.8",
    "Microsoft.DotNet.SDK.9"
)

foreach ($app in $apps) {
    Write-Host "Telepítés: $app..." -ForegroundColor Yellow
    winget install --id $app --silent --accept-package-agreements --accept-source-agreements
}

# 2. Visual Studio Workload-ok a Multi-Targetinghoz
# C++ Desktop: Kell a Win32 Launcherhez (XP kompatibilitás)
# .NET Desktop: Modern verziókhoz
# MSVC v141_xp: Ez az extra eszköz, ami lehetővé teszi az XP-re való fordítást VS 2022-ből!
Write-Host "Speciális VS komponensek konfigurálása (XP support + C++)..." -ForegroundColor Yellow
$vsInstallerPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"

if (Test-Path $vsInstallerPath) {
    Start-Process $vsInstallerPath -ArgumentList "modify --installPath ""C:\Program Files\Microsoft Visual Studio\2022\Community"" `
    --add Microsoft.VisualStudio.Workload.ManagedDesktop `
    --add Microsoft.VisualStudio.Workload.NativeDesktop `
    --add Microsoft.VisualStudio.Component.VC.v141.x86.x64.v411.xp `
    --passive --norestart" -Wait
    Write-Host "  [OK] Modern és Legacy (XP) fejlesztői eszközök kész." -ForegroundColor Green
}

# 3. .NET Framework Multi-Targeting Pack (W7 és régebbi rendszerekhez)
Write-Host "Régebbi .NET targeting pack-ek ellenőrzése..." -ForegroundColor Yellow
winget install --id Microsoft.DotNet.Framework.DeveloperPack_4.8 --silent --accept-package-agreements

Write-Host "`n--- A rendszer készen áll az RTS hibrid fejlesztésére! ---" -ForegroundColor Cyan
pause
