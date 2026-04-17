<#
.SYNOPSIS
    RepoFixer v2 - Teljes Windows integráció (Fájl/Mappa jobb klikk)
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

# 1. ADMIN ELLENŐRZÉS ÉS TELEPÍTÉS
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Admin jog szükséges a telepítéshez/frissítéshez!"
    pause; exit
}

$CurrentLocation = $MyInvocation.MyCommand.Definition
if ($CurrentLocation -notlike $TargetDir) {
    $Choice = Read-Host "Telepíted/Frissíted a scriptet a rendszerbe? (i/n)"
    if ($Choice -eq 'i') {
        if (-not (Test-Path $TargetDir)) { New-Item -Path $TargetDir -ItemType Directory | Out-Null }
        Copy-Item -Path $CurrentLocation -Destination (Join-Path $TargetDir $ScriptName) -Force
        
        # REGISTRY INTEGRÁCIÓ (Háttér, Mappa és Fájl jobb klikk)
        $ContextPaths = @(
            "Registry::HKEY_CLASSES_ROOT\Directory\Background\shell\RepoFixer",
            "Registry::HKEY_CLASSES_ROOT\Directory\shell\RepoFixer",
            "Registry::HKEY_CLASSES_ROOT\*\shell\RepoFixer"
        )

        foreach ($RegPath in $ContextPaths) {
            if (-not (Test-Path $RegPath)) { New-Item -Path $RegPath -Force | Out-Null }
            Set-ItemProperty -Path $RegPath -Name "MUIVerb" -Value "RepoFixer - Javítás és Feloldás"
            Set-ItemProperty -Path $RegPath -Name "Icon" -Value "powershell.exe"
            
            $CmdPath = Join-Path $RegPath "command"
            if (-not (Test-Path $CmdPath)) { New-Item -Path $CmdPath -Force | Out-Null }
            $ExecLine = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$TargetDir\$ScriptName`""
            Set-ItemProperty -Path $CmdPath -Name "(Default)" -Value $ExecLine
        }
        Write-Log "Telepítés és Registry integráció kész."
    }
}

# 2. MŰVELETI RÉSZ
$WorkDir = Get-Location
Write-Log "Indítás: $WorkDir"

# Feloldás
Write-Log "Zárolások feloldása..."
Get-ChildItem -Recurse | Unblock-File

# Kicsomagolás dupla mappa szűréssel
Get-ChildItem -Filter *.zip | ForEach-Object {
    $dest = Join-Path $WorkDir $_.BaseName
    Write-Log "Kicsomagolás: $($_.Name)"
    Expand-Archive -Path $_.FullName -DestinationPath $dest -Force
    
    $content = Get-ChildItem -Path $dest
    if ($content.Count -eq 1 -and $content.PSIsContainer) {
        $inner = $content.FullName
        Move-Item -Path "$inner\*" -Destination $dest -Force
        Remove-Item -Path $inner -Recurse -Force
        Write-Log "Dupla mappa korrigálva."
    }
}

Write-Log "Kész!"
if ($Host.Name -eq "ConsoleHost") { Start-Sleep -Seconds 2 }
