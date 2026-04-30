<#
.SYNOPSIS
    RepoFixer v3.5 - Admin-biztos, Mappa-megőrző és Folyamatjelzővel ellátott verzió
#>

param([string]$StartDir)

# --- KONFIGURÁCIÓ ---
$ScriptName = "RepoFixer.ps1"
$TargetDir = "$env:SystemRoot\Scripts"
$LogFile = Join-Path $TargetDir "repofixer_log.txt"

# --- ADMIN AUTO-RELAUNCH ---
# Ellenőrizzük, hogy admin-ként futunk-e. Ha nem, újraindítjuk magunkat admin joggal.
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    # Az aktuális munkakönyvtár megőrzése az újraindítás után is
    $CurrentDir = if ($StartDir) { $StartDir } else { (Get-Location).Path }
    $Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -StartDir `"$CurrentDir`""
    
    Start-Process powershell.exe -ArgumentList $Arguments -Verb RunAs
    exit
}

# Ha kaptunk kezdőkönyvtárat, lépjünk oda
if ($StartDir) { Set-Location $StartDir }

function Write-Log($Message) {
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogLine = "[$Stamp] $Message"
    Write-Host $LogLine -ForegroundColor Cyan
    # Csak akkor naplózunk fájlba, ha a célmappa már létezik
    if (Test-Path $TargetDir) { $LogLine | Out-File -FilePath $LogFile -Append }
}

$CurrentLocation = $PSCommandPath

# --- 1. TELEPÍTÉSI ÉS REGISTRY LOGIKA ---
# Ha a script nem a végleges helyéről fut, felajánlja a telepítést
if (-not ($CurrentLocation.StartsWith($TargetDir, [System.StringComparison]::OrdinalIgnoreCase))) {
    $Choice = Read-Host "Telepited/Frissited a scriptet a rendszerbe? (i/n)"
    if ($Choice -eq 'i') {
        Write-Log "Telepites inditasa..."
        
        # Mappa létrehozása, ha nem létezik
        if (-not (Test-Path $TargetDir)) { 
            New-Item -Path $TargetDir -ItemType Directory -Force | Out-Null 
        }
        
        # Másolás a rendszerkönyvtárba
        $FinalPath = Join-Path $TargetDir $ScriptName
        Copy-Item -Path $CurrentLocation -Destination $FinalPath -Force
        
        # REGISTRY JAVÍTÁS: Jobb klikkes menü hozzáadása a mappákhoz
        Write-Log "Registry bejegyzesek frissitese..."
        $RegShellPath = "Registry::HKEY_CLASSES_ROOT\Directory\shell\RepoFixer"
        $RegCommandPath = "$RegShellPath\command"
        
        if (-not (Test-Path $RegShellPath)) { New-Item -Path $RegShellPath -Force | Out-Null }
        Set-ItemProperty -Path $RegShellPath -Name "(Default)" -Value "RepoFixer futtatasa itt"
        Set-ItemProperty -Path $RegShellPath -Name "Icon" -Value "powershell.exe"
        
        if (-not (Test-Path $RegCommandPath)) { New-Item -Path $RegCommandPath -Force | Out-Null }
        # A parancs, ami elindítja a scriptet az adott mappában (%1)
        $CommandValue = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$FinalPath`" -StartDir `"%1`""
        Set-ItemProperty -Path $RegCommandPath -Name "(Default)" -Value $CommandValue
        
        Write-Log "Telepites sikeres! Mostantol jobb klikkel is elerheto mappakon."
    }
}

# --- 2. A SCRIPT TÉNYLEGES FELADATA ---
Write-Log "RepoFixer aktiv a kovetkezo helyen: $((Get-Location).Path)"
# Ide jöhet a további javítási logika (pl. git clean, törlések, stb.)
Write-Host "Folyamat kesz." -ForegroundColor Green
Pause
