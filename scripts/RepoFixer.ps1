<#
.SYNOPSIS
    RepoFixer v3.3 - Ultra-stabil verzió .NET hívásokkal
#>

$ScriptName = "RepoFixer.ps1"
$TargetDir = "$env:SystemRoot\Scripts"
$LogFile = Join-Path $TargetDir "repofixer_log.txt"

function Write-Log($Message) {
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogLine = "[$Stamp] $Message"
    Write-Host $LogLine -ForegroundColor Cyan
    if (Test-Path $TargetDir) { $LogLine | Out-File -FilePath $LogFile -Append }
}

# 1. ADMIN ELLENŐRZÉS
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Admin jog szükséges!"
    pause; exit
}

$CurrentLocation = $MyInvocation.MyCommand.Definition

# TELEPÍTÉSI LOGIKA
if (-not ($CurrentLocation.StartsWith($TargetDir))) {
    $Choice = Read-Host "Telepíted/Frissíted a scriptet a rendszerbe? (i/n)"
    if ($Choice -eq 'i') {
        Write-Log "Telepítés indítása..."
        if (-not (Test-Path $TargetDir)) { New-Item -Path $TargetDir -ItemType Directory -Force | Out-Null }
        Copy-Item -Path $CurrentLocation -Destination (Join-Path $TargetDir $ScriptName) -Force
        
        # Registry ágak (csak a kulcs nevei)
        $RegKeys = @(
            "Directory\Background\shell\RepoFixer",
            "Directory\shell\RepoFixer",
            "*\shell\RepoFixer"
        )

        foreach ($SubKey in $RegKeys) {
            Write-Log "Regisztrálás: HKCR\$SubKey"
            try {
                # .NET direkt elérés a PowerShell parancsok helyett (NEM tud lefagyni)
                $Key = [Microsoft.Win32.Registry]::ClassesRoot.CreateSubKey($SubKey)
                $Key.SetValue("MUIVerb", "RepoFixer - Javítás és Feloldás")
                $Key.SetValue("Icon", "powershell.exe")
                
                $CmdKey = $Key.CreateSubKey("command")
                $ExecLine = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$TargetDir\$ScriptName`""
                $CmdKey.SetValue("", $ExecLine) # Az üres név az (Alapértelmezett)
                
                $CmdKey.Close(); $Key.Close()
            } catch {
                Write-Error "Hiba a Registry írásakor: $($_.Exception.Message)"
            }
        }
        Write-Log "KÉSZ! A telepítés befejeződött."
        pause; exit
    }
}

# 2. MŰVELETI RÉSZ
$WorkDir = Get-Location
Write-Log "Munkavégzés: $WorkDir"
Get-ChildItem -Recurse | Unblock-File
Write-Log "Fájlok feloldva. Kész!"
Start-Sleep -Seconds 5
