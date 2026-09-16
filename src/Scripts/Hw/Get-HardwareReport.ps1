# Verzio: v1.0.0 - 2026-09-16
# RTS - hardver-lekerdezes, ORCHESTRATOR (osszefogo script).
#
# Ez a script ONMAGABAN NEM kerdez le semmit: sorban meghivja a
# resz-scripteket (LordAthis 2026-09-16-i kerese: "csak sorban behivja a
# tobbit, tehat minden lekerdezes kulonallo ps1 legyen"), majd az
# eredmenyeiket EGY kozos JSON-ba fuzi ossze.
#
#   Get-SystemInfo.ps1  ->  gep, OS, alaplap, BIOS, memoria
#   Get-CpuInfo.ps1     ->  processzor
#   Get-GpuInfo.ps1     ->  videokartya
#   Get-DiskInfo.ps1    ->  lemezek + SMART
#   Get-SensorInfo.ps1  ->  homerseklet / ventilator / terheles (LibreHardwareMonitor)
#   Get-DxDiagInfo.ps1  ->  DXDIAG teljes riport (CSAK kulon keresre, mert lassu)
#
# CSENDES: nem ir a kepernyore, nem nyit ablakot, nem var billentyure.
# Minden resz-script sajat try/catch-ben fut - egy hibas resz SOSEM
# akasztja meg a tobbit, csak az adott szekcio marad ures + hibauzenetet
# kapunk az "errors" tombben.
#
# -Only kapcsoloval EGYETLEN szekcio is frissitheto (ezt hasznalja az
# "Uj gyorsjelentes" gomb az egyes eszkozok mellett): pl. -Only gpu
# Ilyenkor a MAR MEGLEVO JSON tobbi szekcioja VALTOZATLANUL megmarad.

[CmdletBinding()]
param(
    # Ide keszul a kozos JSON (alapertelmezes: a script melletti
    # ..\..\data\hardware-info.json, azaz az RTS telepitesi gyokeren beluli
    # data mappa - a hivo C# oldal amugy is mindig explicit utat ad at).
    [string]$OutFile = "",

    # "all" (alapertelmezes) vagy: system | cpu | gpu | disk | sensors | dxdiag
    # FIGYELEM: az "all" SZANDEKOSAN NEM tartalmazza a dxdiag-ot, mert az
    # 15-40 masodperc, es elnyujtana a gyors, induláskori lekerdezest.
    [string]$Only = "all",

    # Opcionalis, MAR MEGLEVO kulso eszkozok eleresi utja. Ha uresen
    # marad, a lekerdezes tisztan WMI/CIM-bol dolgozik. Ez a script SOSEM
    # tolt le es SOSEM telepit semmit.
    [string]$CpuZExe = "",
    [string]$GpuZExe = "",
    [string]$HdsExe  = "",
    [string]$LhmExe  = ""
)

$ErrorActionPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($OutFile -eq "") {
    $OutFile = Join-Path $scriptDir "hardware-info.json"
}

function Invoke-Part {
    param(
        [string]$ScriptName,
        [hashtable]$Arguments = @{}
    )
    $path = Join-Path $scriptDir $ScriptName
    if (-not (Test-Path $path)) {
        return @{ ok = $false; error = "Hianyzo resz-script: $ScriptName"; data = $null }
    }
    try {
        $json = & $path @Arguments
        if ($json -is [array]) { $json = ($json -join "`n") }
        if ("$json".Trim() -eq "") {
            return @{ ok = $false; error = "$ScriptName ures kimenetet adott."; data = $null }
        }
        $obj = "$json" | ConvertFrom-Json -ErrorAction Stop
        return @{ ok = $true; error = ""; data = $obj }
    } catch {
        return @{ ok = $false; error = "$ScriptName : $($_.Exception.Message)"; data = $null }
    }
}

# ─────────── Meglevo JSON betoltese (reszleges frissiteshez) ───────────
$report = $null
if (Test-Path $OutFile) {
    try { $report = (Get-Content $OutFile -Raw -ErrorAction Stop) | ConvertFrom-Json -ErrorAction Stop } catch { $report = $null }
}
if ($report -eq $null) {
    $report = [PSCustomObject]@{
        queried_at_utc = $null
        schema_version = 2
        system = $null
        cpu    = $null
        gpu    = $null
        disk    = $null
        sensors = $null
        dxdiag  = $null
        errors = @()
    }
}

# A ConvertFrom-Json PSCustomObject-et ad - a biztos mezo-hozzaadashoz
# hashtable-re valtunk, majd a vegen ugy irjuk ki.
$out = [ordered]@{}
foreach ($p in $report.PSObject.Properties) { $out[$p.Name] = $p.Value }
if (-not $out.Contains("schema_version")) { $out["schema_version"] = 2 }
$errors = @()

$only = "$Only".ToLower().Trim()
if ($only -eq "") { $only = "all" }

# ───────────────────────── Resz-lekerdezesek ─────────────────────────
if ($only -eq "all" -or $only -eq "system") {
    $r = Invoke-Part -ScriptName "Get-SystemInfo.ps1"
    if ($r.ok) { $out["system"] = $r.data } else { $errors += $r.error }
}

if ($only -eq "all" -or $only -eq "cpu") {
    $a = @{}
    if ($CpuZExe -ne "") { $a["CpuZExe"] = $CpuZExe }
    $r = Invoke-Part -ScriptName "Get-CpuInfo.ps1" -Arguments $a
    if ($r.ok) { $out["cpu"] = $r.data } else { $errors += $r.error }
}

if ($only -eq "all" -or $only -eq "gpu") {
    $a = @{}
    if ($GpuZExe -ne "") { $a["GpuZExe"] = $GpuZExe }
    $r = Invoke-Part -ScriptName "Get-GpuInfo.ps1" -Arguments $a
    if ($r.ok) { $out["gpu"] = $r.data } else { $errors += $r.error }
}

if ($only -eq "all" -or $only -eq "disk") {
    $a = @{}
    if ($HdsExe -ne "") { $a["HdsExe"] = $HdsExe }
    $r = Invoke-Part -ScriptName "Get-DiskInfo.ps1" -Arguments $a
    if ($r.ok) { $out["disk"] = $r.data } else { $errors += $r.error }
}

if ($only -eq "all" -or $only -eq "sensors") {
    $a = @{}
    if ($LhmExe -ne "") { $a["LhmExe"] = $LhmExe }
    $r = Invoke-Part -ScriptName "Get-SensorInfo.ps1" -Arguments $a
    if ($r.ok) { $out["sensors"] = $r.data } else { $errors += $r.error }
}

# A DXDIAG SZANDEKOSAN csak kulon keresre fut (lasd a fenti megjegyzest) -
# az "all" NEM inditja el.
if ($only -eq "dxdiag") {
    $r = Invoke-Part -ScriptName "Get-DxDiagInfo.ps1"
    if ($r.ok) { $out["dxdiag"] = $r.data } else { $errors += $r.error }
}

$out["queried_at_utc"] = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$out["last_refresh_scope"] = $only
$out["errors"] = $errors

# ───────────── Rovid, egysoros osszefoglalo a B2 dobozhoz ─────────────
try {
    $parts = @()
    if ($out["cpu"] -and "$($out["cpu"].summary)" -ne "")   { $parts += "CPU: $($out["cpu"].summary)" }
    if ($out["gpu"] -and "$($out["gpu"].summary)" -ne "")   { $parts += "GPU: $($out["gpu"].summary)" }
    if ($out["disk"] -and $out["disk"].health_percent -ne $null) { $parts += "Lemez: $($out["disk"].health_percent)%" }
    if ($out["sensors"] -and "$($out["sensors"].summary)" -ne "") { $parts += "$($out["sensors"].summary)" }
    $out["summary"] = ($parts -join "  |  ")
} catch { }

$json = [PSCustomObject]$out | ConvertTo-Json -Depth 8
try {
    $dir = Split-Path -Parent $OutFile
    if ($dir -ne "" -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [IO.File]::WriteAllText($OutFile, $json, [Text.UTF8Encoding]::new($false))
} catch {
    $errors += "A kimeneti fajl irasa sikertelen ($OutFile): $($_.Exception.Message)"
}

$json
