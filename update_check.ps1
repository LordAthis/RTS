#Requires -Version 5.1
<#
.SYNOPSIS
    RTS Update Check – Dátum alapú frissítésfigyelő

.DESCRIPTION
    Lekérdezi a GitHub API-n keresztül minden modul utolsó commit-jának
    dátumát, és összehasonlítja a helyi modules.json-ban tárolt értékekkel.

    Ha talál frissítést, elmenti az updates.json fájlba – amit a GUI,
    launcher, vagy más script beolvashat és megjeleníthet.

    Futtatható:
      - Kézzel, bármikor
      - Automatikusan, az RTS indításakor (launcher hívja)
      - Task Scheduler-rel ütemezve

.PARAMETER Silent
    Nem ír konzolra, csak a fájlokba logol. Launcher/Task Scheduler módhoz.

.PARAMETER AutoUpdate
    Ha van frissítés, automatikusan le is tölti (git pull) – kérdés nélkül.

.PARAMETER ModuleFilter
    Csak az itt megadott nevű modulokat ellenőrzi (vesszővel elválasztva).

.EXAMPLE
    .\update_check.ps1
    .\update_check.ps1 -Silent
    .\update_check.ps1 -AutoUpdate
    .\update_check.ps1 -ModuleFilter "IWS,RescueData"

.NOTES
    Szerző : LordAthis
    Projekt: RTS – Reparing's Tuning's Setting's
    GitHub : https://github.com/LordAthis/RTS

    A dátum-összehasonlítás ISO 8601 formátumban (YYYY-MM-DD) történik.
    Verzióhivatkozás NINCS – kizárólag a commit dátuma számít.
#>

[CmdletBinding()]
param(
    [switch]$Silent,
    [switch]$AutoUpdate,
    [string]$ModuleFilter = ""
)

# ─────────────────────────────────────────────
#  KONFIGURÁCIÓ
# ─────────────────────────────────────────────
$ScriptDir   = $PSScriptRoot
$ModulesFile = Join-Path $ScriptDir "modules.json"
$UpdatesFile = Join-Path $ScriptDir "updates.json"
$AppsDir     = Join-Path $ScriptDir "Apps"
$LogFile     = Join-Path $ScriptDir "logs\update_check_$(Get-Date -Format 'yyyy-MM-dd').log"
$ApiBase     = "https://api.github.com/repos"
$Today       = Get-Date -Format "yyyy-MM-dd"

# GitHub API rate limit: bejelentkezés nélkül 60 kérés/óra
# Saját token megadható a $env:GITHUB_TOKEN változóban (opcionális)
$ApiHeaders = @{ "User-Agent" = "RTS-UpdateChecker/1.0" }
if ($env:GITHUB_TOKEN) {
    $ApiHeaders["Authorization"] = "token $env:GITHUB_TOKEN"
}

# ─────────────────────────────────────────────
#  SEGÉDFÜGGVÉNYEK
# ─────────────────────────────────────────────
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[$timestamp] [$Level] $Message"
    if (-not $Silent) {
        Write-Host $line -ForegroundColor $(
            switch ($Level) {
                "OK"      { "Green" }
                "UPDATE"  { "Yellow" }
                "WARN"    { "DarkYellow" }
                "ERROR"   { "Red" }
                "SKIP"    { "Cyan" }
                default   { "Gray" }
            }
        )
    }
    Add-Content -Path $LogFile -Value $line -ErrorAction SilentlyContinue
}

function Get-LastCommitDate {
    param([string]$Repo)
    $url = "$ApiBase/$Repo/commits?per_page=1"
    try {
        $response = Invoke-RestMethod -Uri $url -Headers $ApiHeaders -ErrorAction Stop
        if ($response -and $response.Count -gt 0) {
            # Dátum kinyerése: "2026-03-15T10:23:00Z" → "2026-03-15"
            $rawDate = $response[0].commit.author.date
            return $rawDate.Substring(0, 10)
        }
    } catch {
        $statusCode = $_.Exception.Response?.StatusCode
        if ($statusCode -eq 403) {
            Write-Log "GitHub API rate limit elérve! Várj egy órát, vagy adj meg GITHUB_TOKEN-t." "WARN"
        } elseif ($statusCode -eq 404) {
            Write-Log "Repozitórium nem található: $Repo" "WARN"
        } else {
            Write-Log "API lekérdezési hiba ($Repo): $_" "ERROR"
        }
    }
    return $null
}

function Update-ModuleViaGit {
    param([string]$ModulePath, [string]$ModuleName)
    if (-not (Test-Path (Join-Path $ModulePath ".git"))) {
        Write-Log "[$ModuleName] Nem git repo – git pull nem lehetséges. (ZIP-pel lett telepítve?)" "WARN"
        return $false
    }
    Push-Location $ModulePath
    try {
        $result = git pull 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Log "[$ModuleName] git pull sikeres." "OK"
            return $true
        } else {
            Write-Log "[$ModuleName] git pull hiba: $result" "ERROR"
            return $false
        }
    } finally {
        Pop-Location
    }
}

# ─────────────────────────────────────────────
#  ELŐKÉSZÍTÉS
# ─────────────────────────────────────────────
$logsDir = Join-Path $ScriptDir "logs"
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }

if (-not $Silent) {
    Clear-Host
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Yellow
    Write-Host "  ║     RTS – Frissítés ellenőrzés                   ║" -ForegroundColor Yellow
    Write-Host "  ║     Dátum alapú · GitHub API                     ║" -ForegroundColor Yellow
    Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Yellow
    Write-Host ""
}

# modules.json ellenőrzése
if (-not (Test-Path $ModulesFile)) {
    Write-Log "modules.json nem található: $ModulesFile" "ERROR"
    exit 1
}

try {
    $modules = Get-Content $ModulesFile -Raw | ConvertFrom-Json
} catch {
    Write-Log "modules.json olvasási hiba: $_" "ERROR"
    exit 1
}

# Szűrőlista
$filterList = @()
if ($ModuleFilter -ne "") {
    $filterList = $ModuleFilter -split "," | ForEach-Object { $_.Trim() }
    Write-Log "Szűrő aktív – csak ezek: $($filterList -join ', ')"
}

# ─────────────────────────────────────────────
#  FŐ ELLENŐRZŐ LOGIKA
# ─────────────────────────────────────────────
$updatesFound  = [System.Collections.Generic.List[object]]::new()
$checkedCount  = 0
$skippedCount  = 0
$errorCount    = 0

Write-Log "Ellenőrzés kezdete: $Today"
Write-Log "─────────────────────────────────────────"

foreach ($mod in $modules) {

    # Szűrő
    if ($filterList.Count -gt 0 -and $mod.name -notin $filterList) {
        continue
    }

    # Disabled modulok kihagyása
    if (-not $mod.enabled) {
        Write-Log "[$($mod.name)] Letiltva – kihagyva." "SKIP"
        $skippedCount++
        continue
    }

    Write-Log "[$($mod.name)] Lekérdezés... ($($mod.repo))"

    $latestDate = Get-LastCommitDate -Repo $mod.repo

    if ($null -eq $latestDate) {
        Write-Log "[$($mod.name)] Nem sikerült lekérdezni." "ERROR"
        $errorCount++
        continue
    }

    $storedDate = $mod.last_commit_date

    # Frissítés szükséges-e?
    $hasUpdate = ($storedDate -eq "" -or $latestDate -gt $storedDate)

    if ($hasUpdate) {
        $updateInfo = [PSCustomObject]@{
            name             = $mod.name
            repo             = $mod.repo
            description      = $mod.description
            stored_date      = if ($storedDate -eq "") { "Még nem volt lekérdezve" } else { $storedDate }
            latest_date      = $latestDate
            days_behind      = if ($storedDate -ne "") {
                                   ([datetime]$latestDate - [datetime]$storedDate).Days
                               } else { $null }
            installed        = Test-Path (Join-Path $AppsDir $mod.name)
        }
        $updatesFound.Add($updateInfo)
        Write-Log "[$($mod.name)] ⚡ FRISSÍTÉS ELÉRHETŐ! Repón: $latestDate | Helyi: $($storedDate -or 'N/A')" "UPDATE"
    } else {
        Write-Log "[$($mod.name)] Naprakész. ($latestDate)" "OK"
    }

    # modules.json frissítése (last_checked + last_commit_date)
    $mod.last_checked     = $Today
    $mod.last_commit_date = $latestDate
    $checkedCount++
}

# ─────────────────────────────────────────────
#  FÁJLOK MENTÉSE
# ─────────────────────────────────────────────

# modules.json visszaírása a frissített dátumokkal
try {
    $modules | ConvertTo-Json -Depth 5 | Set-Content $ModulesFile -Encoding UTF8
    Write-Log "modules.json frissítve (last_checked, last_commit_date)." "OK"
} catch {
    Write-Log "modules.json visszaírási hiba: $_" "ERROR"
}

# updates.json létrehozása/frissítése
$updatesOutput = [PSCustomObject]@{
    generated_at    = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    checked_modules = $checkedCount
    updates_found   = $updatesFound.Count
    updates         = $updatesFound
}

try {
    $updatesOutput | ConvertTo-Json -Depth 5 | Set-Content $UpdatesFile -Encoding UTF8
    Write-Log "updates.json mentve: $UpdatesFile"
} catch {
    Write-Log "updates.json mentési hiba: $_" "ERROR"
}

# ─────────────────────────────────────────────
#  AUTOMATIKUS FRISSÍTÉS (ha -AutoUpdate)
# ─────────────────────────────────────────────
if ($AutoUpdate -and $updatesFound.Count -gt 0) {
    Write-Log "─────────────────────────────────────────"
    Write-Log "AutoUpdate mód: frissítések letöltése..."
    foreach ($upd in $updatesFound) {
        if ($upd.installed) {
            $modPath = Join-Path $AppsDir $upd.name
            Update-ModuleViaGit -ModulePath $modPath -ModuleName $upd.name
        } else {
            Write-Log "[$($upd.name)] Nincs telepítve – futtasd a bootstrap.ps1-et!" "WARN"
        }
    }
}

# ─────────────────────────────────────────────
#  ÖSSZEFOGLALÓ
# ─────────────────────────────────────────────
Write-Log "─────────────────────────────────────────"
Write-Log "Ellenőrzés kész!"
Write-Log "  Ellenőrzött : $checkedCount"
Write-Log "  Frissíthető : $($updatesFound.Count)"
Write-Log "  Kihagyva    : $skippedCount"
Write-Log "  Hiba        : $errorCount"
Write-Log "─────────────────────────────────────────"

if (-not $Silent -and $updatesFound.Count -gt 0) {
    Write-Host ""
    Write-Host "  🔔 Frissítések elérhetők a következő modulokhoz:" -ForegroundColor Yellow
    foreach ($upd in $updatesFound) {
        $behind = if ($upd.days_behind) { " ($($upd.days_behind) nap)" } else { "" }
        Write-Host "     • $($upd.name) – $($upd.latest_date)$behind" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "  Frissítéshez futtasd: .\update_check.ps1 -AutoUpdate" -ForegroundColor Cyan
    Write-Host "  Az updates.json tartalmazza a részletes listát."       -ForegroundColor Gray
    Write-Host ""
}

# Visszatérési kód: 0 = nincs frissítés, 1 = van frissítés, 2 = hiba volt
if ($errorCount -gt 0) { exit 2 }
if ($updatesFound.Count -gt 0) { exit 1 }
exit 0
