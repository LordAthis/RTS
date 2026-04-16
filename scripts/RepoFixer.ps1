<#
.SYNOPSIS
    RepoFixer - Fájl feloldó, kicsomagoló és rendszerintegráló eszköz.
#>

$ScriptName = "RepoFixer.ps1"
$TargetDir = "$env:SystemRoot\Scripts"
$LogFile = Join-Path $PSScriptRoot "repofixer_log.txt"

function Write-Log($Message) {
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogLine = "[$Stamp] $Message"
    Write-Host $LogLine -ForegroundColor Cyan
    $LogLine | Out-File -FilePath $LogFile -Append
}

# 1. ADMIN JOG ELLENŐRZÉSE (Szükséges a C:\Windows íráshoz)
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Kérlek, futtasd rendszergazdaként a telepítéshez/frissítéshez!"
    pause
    exit
}

Write-Log "--- RepoFixer Indítása ---"

# 2. TELEPÍTÉSI LOGIKA
$CurrentLocation = $MyInvocation.MyCommand.Definition
$InWindowsFolder = $CurrentLocation.StartsWith($TargetDir, [System.StringComparison]::OrdinalIgnoreCase)

if (-not $InWindowsFolder) {
    $Choice = Read-Host "Szeretnéd telepíteni/frissíteni a scriptet a Windows mappába és hozzáadni a Jobb klikk menühöz? (i/n)"
    if ($Choice -eq 'i') {
        if (-not (Test-Path $TargetDir)) { New-Item -Path $TargetDir -ItemType Directory }
        
        # Másolás/Frissítés
        Copy-Item -Path $CurrentLocation -Destination (Join-Path $TargetDir $ScriptName) -Force
        Write-Log "Script másolva: $TargetDir"

        # Jobb klikk menü (Registry)
        $RegPath = "Registry::HKEY_CLASSES_ROOT\Directory\Background\shell\RepoFixer"
        if (-not (Test-Path $RegPath)) { New-Item -Path $RegPath -Force }
        New-ItemProperty -Path $RegPath -Name "MUIVerb" -Value "Mappa javítása (RepoFixer)" -Force | Out-Null
        New-ItemProperty -Path $RegPath -Name "Icon" -Value "powershell.exe" -Force | Out-Null
        
        $CmdPath = Join-Path $RegPath "command"
        if (-not (Test-Path $CmdPath)) { New-Item -Path $CmdPath -Force }
        $Value = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$TargetDir\$ScriptName`""
        Set-ItemProperty -Path $CmdPath -Name "(Default)" -Value $Value
        
        Write-Log "Gyorsindító (Jobb klikk) létrehozva/frissítve."
    }
}

# 3. MŰVELETI RÉSZ (Feloldás és Kicsomagolás)
$WorkDir = Get-Location
Write-Log "Munkavégzés helye: $WorkDir"

# Fájlok feloldása
Write-Log "Zárolások feloldása (Unblock-File)..."
Get-ChildItem -Recurse | Unblock-File

# ZIP-ek kezelése
$zips = Get-ChildItem -Filter *.zip
foreach ($zip in $zips) {
    $zipBase = $zip.BaseName
    $dest = Join-Path $WorkDir $zipBase
    
    Write-Log "Kicsomagolás: $($zip.Name)"
    Expand-Archive -Path $zip.FullName -DestinationPath $dest -Force
    
    # Dupla mappa elleni logika
    $content = Get-ChildItem -Path $dest
    if ($content.Count -eq 1 -and $content.PSIsContainer) {
        Write-Log "Dupla mappa észlelve ($($content.Name)), javítás..."
        $tempPath = $dest + "_temp"
        Move-Item -Path "$($content.FullName)\*" -Destination $dest -Force
        Remove-Item -Path $content.FullName -Recurse -Force
    }
}

Write-Log "--- Művelet befejezve ---"
Write-Host "`nA napló mentve: $LogFile" -ForegroundColor Green
pause
