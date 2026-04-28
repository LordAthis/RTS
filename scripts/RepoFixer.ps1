<#
.SYNOPSIS
    RepoFixer v3.5 - Admin-biztos, Mappa-megőrző és Folyamatjelzővel ellátott verzió
#>

$ScriptName = "RepoFixer.ps1"
$TargetDir = "$env:SystemRoot\Scripts"
$LogFile = Join-Path $TargetDir "repofixer_log.txt"

# --- ADMIN AUTO-RELAUNCH ---
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -StartDir `"$Get-Location`""
    Start-Process powershell.exe -ArgumentList $Arguments -Verb RunAs
    exit
}

param([string]$StartDir)
if ($StartDir) { Set-Location $StartDir }

function Write-Log($Message) {
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogLine = "[$Stamp] $Message"
    Write-Host $LogLine -ForegroundColor Cyan
    if (Test-Path $TargetDir) { $LogLine | Out-File -FilePath $LogFile -Append }
}

$CurrentLocation = $PSCommandPath

# 1. TELEPÍTÉSI LOGIKA (Registry javítással)
if (-not ($CurrentLocation.StartsWith($TargetDir, [System.StringComparison]::OrdinalIgnoreCase))) {
    $Choice = Read-Host "Telepíted/Frissíted a scriptet a rendszerbe? (i/n)"
    if ($Choice -eq 'i') {
        Write-Log "Telepítés indítása..."
        if (-not (Test-Path $TargetDir)) { New-Item -Path $TargetDir -ItemType Directory -Force | Out-Null }
        Copy-Item -Path $CurrentLocation -Destination (Join-Path $TargetDir $ScriptName) -Force
        
        $RegKeys = @("Directory\Background\shell\RepoFixer", "Directory\shell\RepoFixer", "*\shell\RepoFixer")
        foreach ($SubKey in $RegKeys) {
            $Key = [Microsoft.Win32.Registry]::ClassesRoot.CreateSubKey($SubKey)
            $Key.SetValue("MUIVerb", "RepoFixer - Javítás és Feloldás")
            $Key.SetValue("Icon", "powershell.exe")
            $CmdKey = $Key.CreateSubKey("command")
            
            # FIGYELEM: A %V paraméter biztosítja a menüből induló helyszínt!
            $ExecLine = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$TargetDir\$ScriptName`" -StartDir `"%V`""
            $CmdKey.SetValue("", $ExecLine)
            $CmdKey.Close(); $Key.Close()
        }
        Write-Log "KÉSZ! A menüpont frissítve."
        pause; exit
    }
}

# 2. MŰVELETI RÉSZ FOLYAMATJELZŐVEL
$WorkDir = Get-Location
if ($WorkDir -eq "$env:SystemRoot\System32") {
    Write-Warning "Hiba: A script a System32-ben ragadt. Leállítás!"
    pause; exit
}

Write-Log "Fájlok listázása..."
$Files = Get-ChildItem -Recurse -File -ErrorAction SilentlyContinue
$Total = $Files.Count
$Counter = 0

if ($Total -gt 0) {
    foreach ($File in $Files) {
        $Counter++
        $Percent = ($Counter / $Total) * 100
        Write-Progress -Activity "Fájlok feloldása folyamatban..." -Status "Fájl: $($File.Name)" -PercentComplete $Percent
        
        $File | Unblock-File
    }
    Write-Log "Kész! $Total fájl sikeresen feloldva."
} else {
    Write-Log "Nem található feloldandó fájl ebben a mappában."
}

Start-Sleep -Seconds 3
