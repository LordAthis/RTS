#Requires -Version 5.1
<#
.SYNOPSIS
    RTS Bootstrap – Modulok első letöltése és telepítése

.DESCRIPTION
    Ez a script fut le ELSŐ ALKALOMMAL, miután az RTS repozitóriumot
    klónoztad vagy letöltötted. Beolvassa a modules.json fájlt, és
    minden engedélyezett modult legit klónoz a saját Apps\ almappájába.

    Nem duplikál: ha egy modul már létezik, átugorja (nem írja felül).
    Git nélküli környezetben ZIP letöltést használ fallback-ként.

.PARAMETER Force
    Ha megadod, a már meglévő modulokat is újra letölti (felülírja).

.PARAMETER ModuleFilter
    Csak az itt megadott nevű modulokat tölti le (vesszővel elválasztva).
    Pl.: -ModuleFilter "IWS,CoffeTime"

.EXAMPLE
    .\bootstrap.ps1
    .\bootstrap.ps1 -Force
    .\bootstrap.ps1 -ModuleFilter "IWS,Network-Tools"

.NOTES
    Szerző : LordAthis
    Projekt: RTS – Reparing's Tuning's Setting's
    GitHub : https://github.com/LordAthis/RTS
    Fontos : Futtasd az RTS gyökérmappájából!
#>

[CmdletBinding()]
param(
    [switch]$Force,
    [string]$ModuleFilter = ""
)

# ─────────────────────────────────────────────
#  KONFIGURÁCIÓ
# ─────────────────────────────────────────────
$ScriptDir    = $PSScriptRoot
$ModulesFile  = Join-Path $ScriptDir "modules.json"
$AppsDir      = Join-Path $ScriptDir "Apps"
$LogFile      = Join-Path $ScriptDir "logs\bootstrap_$(Get-Date -Format 'yyyy-MM-dd').log"
$GithubBase   = "https://github.com"
$GithubZipUrl = "https://github.com/{0}/archive/refs/heads/main.zip"

# ─────────────────────────────────────────────
#  SEGÉDFÜGGVÉNYEK
# ─────────────────────────────────────────────
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[$timestamp] [$Level] $Message"
    Write-Host $line -ForegroundColor $(
        switch ($Level) {
            "OK"      { "Green" }
            "WARN"    { "Yellow" }
            "ERROR"   { "Red" }
            "SKIP"    { "Cyan" }
            default   { "Gray" }
        }
    )
    Add-Content -Path $LogFile -Value $line -ErrorAction SilentlyContinue
}

function Test-GitAvailable {
    try {
        $null = git --version 2>&1
        return $true
    } catch {
        return $false
    }
}

function Install-ModuleViaGit {
    param([string]$Repo, [string]$TargetPath)
    $url = "$GithubBase/$Repo.git"
    Write-Log "Git klónozás: $url → $TargetPath"
    $result = git clone $url $TargetPath 2>&1
    if ($LASTEXITCODE -eq 0) {
        return $true
    } else {
        Write-Log "Git klónozás sikertelen: $result" "ERROR"
        return $false
    }
}

function Install-ModuleViaZip {
    param([string]$Repo, [string]$TargetPath, [string]$ModuleName)
    $zipUrl  = $GithubZipUrl -f $Repo
    $tmpZip  = Join-Path $env:TEMP "rts_$ModuleName.zip"
    $tmpDir  = Join-Path $env:TEMP "rts_$ModuleName"

    Write-Log "ZIP letöltés (Git nem elérhető): $zipUrl"
    try {
        Invoke-WebRequest -Uri $zipUrl -OutFile $tmpZip -UseBasicParsing -ErrorAction Stop
        Expand-Archive -Path $tmpZip -DestinationPath $tmpDir -Force -ErrorAction Stop

        # GitHub a ZIP-ben "Repo-main" nevű mappát hoz létre
        $extracted = Get-ChildItem $tmpDir -Directory | Select-Object -First 1
        if ($extracted) {
            Move-Item $extracted.FullName $TargetPath -ErrorAction Stop
            Write-Log "ZIP kibontva és áthelyezve: $TargetPath" "OK"
            return $true
        } else {
            Write-Log "Nem található kibontott mappa a ZIP-ben." "ERROR"
            return $false
        }
    } catch {
        Write-Log "ZIP letöltés/kibontás hiba: $_" "ERROR"
        return $false
    } finally {
        Remove-Item $tmpZip  -Force -ErrorAction SilentlyContinue
        Remove-Item $tmpDir  -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ─────────────────────────────────────────────
#  ELŐKÉSZÍTÉS
# ─────────────────────────────────────────────
$logsDir = Join-Path $ScriptDir "logs"
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }
if (-not (Test-Path $AppsDir))  { New-Item -ItemType Directory -Path $AppsDir  | Out-Null }

Clear-Host
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║     RTS – Bootstrap / Modulok telepítése         ║" -ForegroundColor Cyan
Write-Host "  ║     Reparing's · Tuning's · Setting's            ║" -ForegroundColor Cyan
Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# modules.json ellenőrzése
if (-not (Test-Path $ModulesFile)) {
    Write-Log "modules.json nem található: $ModulesFile" "ERROR"
    Write-Log "Ellenőrizd, hogy az RTS gyökérmappájából futtatod-e a scriptet!" "ERROR"
    exit 1
}

# JSON beolvasás
try {
    $modules = Get-Content $ModulesFile -Raw | ConvertFrom-Json
} catch {
    Write-Log "modules.json olvasási hiba: $_" "ERROR"
    exit 1
}

# Git elérhetőség
$gitAvailable = Test-GitAvailable
if ($gitAvailable) {
    Write-Log "Git elérhető – klónozással dolgozunk." "OK"
} else {
    Write-Log "Git NEM elérhető – ZIP letöltési módra váltunk." "WARN"
}

# Szűrőlista feldolgozása
$filterList = @()
if ($ModuleFilter -ne "") {
    $filterList = $ModuleFilter -split "," | ForEach-Object { $_.Trim() }
    Write-Log "Szűrő aktív – csak ezek: $($filterList -join ', ')"
}

# ─────────────────────────────────────────────
#  FŐ TELEPÍTÉSI LOGIKA
# ─────────────────────────────────────────────
$results = @{ success = 0; skipped = 0; failed = 0 }

Write-Log "─────────────────────────────────────────"
Write-Log "Feldolgozandó modulok: $($modules.Count)"
Write-Log "─────────────────────────────────────────"

foreach ($mod in $modules) {

    # Kihagyás, ha a szűrőben nem szerepel
    if ($filterList.Count -gt 0 -and $mod.name -notin $filterList) {
        continue
    }

    # Kihagyás, ha disabled
    if (-not $mod.enabled) {
        Write-Log "[$($mod.name)] Letiltva a modules.json-ban – kihagyva." "SKIP"
        $results.skipped++
        continue
    }

    $targetPath = Join-Path $AppsDir $mod.name

    # Ha már létezik és nem Force mód
    if ((Test-Path $targetPath) -and (-not $Force)) {
        Write-Log "[$($mod.name)] Már létezik – kihagyva. (Használd -Force a felülíráshoz)" "SKIP"
        $results.skipped++
        continue
    }

    # Ha Force és már létezik: törlés
    if ((Test-Path $targetPath) -and $Force) {
        Write-Log "[$($mod.name)] Force mód – régi mappa törlése..."
        Remove-Item $targetPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Log "[$($mod.name)] Telepítés folyamatban... ($($mod.repo))"
    Write-Log "  Leírás: $($mod.description)"

    $success = $false
    if ($gitAvailable) {
        $success = Install-ModuleViaGit -Repo $mod.repo -TargetPath $targetPath
    }
    # Git fallback: ZIP
    if (-not $success) {
        $success = Install-ModuleViaZip -Repo $mod.repo -TargetPath $targetPath -ModuleName $mod.name
    }

    if ($success) {
        Write-Log "[$($mod.name)] Telepítve!" "OK"
        $results.success++
    } else {
        Write-Log "[$($mod.name)] SIKERTELEN!" "ERROR"
        $results.failed++
    }

    Write-Host ""
}

# ─────────────────────────────────────────────
#  ÖSSZEFOGLALÓ
# ─────────────────────────────────────────────
Write-Log "─────────────────────────────────────────"
Write-Log "Bootstrap kész!"
Write-Log "  ✓ Sikeres : $($results.success)"
Write-Log "  ⊘ Kihagyva: $($results.skipped)"
Write-Log "  ✗ Sikertelen: $($results.failed)"
Write-Log "─────────────────────────────────────────"

if ($results.failed -gt 0) {
    Write-Log "Egyes modulok nem töltődtek le. Ellenőrizd az internetkapcsolatot és a logs\ mappát." "WARN"
}

Write-Host ""
Write-Host "  Kövesd a frissítéseket: .\update_check.ps1" -ForegroundColor Cyan
Write-Host ""
