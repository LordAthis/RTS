# Verzio: v1.0.0 - 2026-09-16
# RTS - hardver-lekerdezes, RESZ-SCRIPT: szenzorok (homerseklet, terheles,
# ventilator, feszultseg).
#
# ONALLOAN IS FUTTATHATO.
#
# FO FORRAS: LibreHardwareMonitor (LordAthis 2026-09-16-i valasztasa - ez
# az "ajanlott" eszkoz, amit az automatikusan beszerzettek koze is felvett).
# Miert EZ, es miert nem a HWMonitor: a HWMonitornak NINCS csendes,
# parancssoros exportalasa - adatot csak a megnyitott ablakbol, kezzel
# (F5 -> CSV) lehet kinyerni belole. A LibreHardwareMonitor ezzel szemben
# nyilt forrasu, es SAJAT WMI-nevteret publikal (root\LibreHardwareMonitor,
# "Sensor" osztaly), amit a PowerShell kozvetlenul, ablak-megnyitas nelkul
# tud olvasni.
#
# TARTALEK, ha a LibreHardwareMonitor nem fut/nincs telepitve: a Windows
# sajat MSAcpi_ThermalZoneTemperature osztalya (root\wmi). Ez csak egy
# durva, alaplapi termikus zona-homersekletet ad, es nem minden gepen
# erheto el - ezert a kimenetben EGYERTELMUEN jelezzuk, melyik forrasbol
# szarmazik az adat, hogy senki ne higgye pontosabbnak, mint amilyen.
#
# FONTOS: ez a script SEMMIT nem tolt le es SEMMIT nem telepit. A
# LibreHardwareMonitor exe-jenek utjat a hivo adja at (-LhmExe), es CSAK
# akkor inditjuk el a hatterben, ha meg nem fut.

[CmdletBinding()]
param(
    [string]$OutFile = "",
    [string]$LhmExe = "",
    [int]$StartupWaitSeconds = 4
)

$ErrorActionPreference = "SilentlyContinue"

$result = [ordered]@{
    section        = "sensors"
    queried_at_utc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    available      = $false
    sensors        = @()
    summary        = ""
    raw_text       = ""
    raw_source     = ""
    errors         = @()
}

# ───────── LibreHardwareMonitor elinditasa, ha kell es lehet ─────────
$lhmRunning = $false
try {
    $lhmRunning = [bool](Get-Process "LibreHardwareMonitor" -ErrorAction SilentlyContinue)
} catch { }

if (-not $lhmRunning -and $LhmExe -ne "" -and (Test-Path $LhmExe)) {
    try {
        # A LibreHardwareMonitor a sajat WMI-nevteret csak akkor tolti fel,
        # ha fut - ezert a hatterben, rejtett ablakkal elinditjuk.
        Start-Process -FilePath $LhmExe -WindowStyle Hidden -ErrorAction Stop | Out-Null
        Start-Sleep -Seconds $StartupWaitSeconds
        $lhmRunning = [bool](Get-Process "LibreHardwareMonitor" -ErrorAction SilentlyContinue)
    } catch {
        $result.errors += "LibreHardwareMonitor inditasa sikertelen: $($_.Exception.Message)"
    }
}

# ───────────── Szenzorok kiolvasasa a LHM WMI-nevterebol ─────────────
if ($lhmRunning) {
    try {
        $sensors = Get-CimInstance -Namespace "root\LibreHardwareMonitor" -ClassName "Sensor" -ErrorAction Stop
        $list = @()
        foreach ($s in $sensors) {
            # Csak az ertelmes, nem nulla ertekeket tartjuk meg, es a
            # legfontosabb tipusokat (homerseklet, terheles, ventilator,
            # orajel, teljesitmeny).
            if ("$($s.SensorType)" -notin @("Temperature", "Load", "Fan", "Clock", "Power")) { continue }
            $val = $null
            try { $val = [math]::Round([double]$s.Value, 1) } catch { continue }
            if ($val -eq $null) { continue }

            $list += [ordered]@{
                type       = "$($s.SensorType)"
                name       = "$($s.Name)".Trim()
                identifier = "$($s.Identifier)".Trim()
                parent     = "$($s.Parent)".Trim()
                value      = $val
                min        = $(try { [math]::Round([double]$s.Min, 1) } catch { $null })
                max        = $(try { [math]::Round([double]$s.Max, 1) } catch { $null })
            }
        }
        $result.sensors    = $list
        $result.available  = ($list.Count -gt 0)
        $result.raw_source = "LibreHardwareMonitor (root\LibreHardwareMonitor WMI)"
    } catch {
        $result.errors += "A LibreHardwareMonitor WMI-nevtere nem olvashato: $($_.Exception.Message)"
    }
} else {
    $result.errors += "A LibreHardwareMonitor nem fut - a szenzoradatok a Windows sajat, korlatozott forrasabol keszulnek."
}

# ───────────────── Tartalek: Windows termikus zona ─────────────────
if (-not $result.available) {
    try {
        $tz = Get-CimInstance -Namespace "root\wmi" -ClassName "MSAcpi_ThermalZoneTemperature" -ErrorAction Stop
        $list = @()
        foreach ($z in $tz) {
            $c = $null
            try { $c = [math]::Round((([double]$z.CurrentTemperature) / 10) - 273.15, 1) } catch { continue }
            if ($c -eq $null -or $c -le 0) { continue }
            $list += [ordered]@{
                type       = "Temperature"
                name       = "Termikus zona"
                identifier = "$($z.InstanceName)".Trim()
                parent     = "ACPI"
                value      = $c
                min        = $null
                max        = $null
            }
        }
        if ($list.Count -gt 0) {
            $result.sensors    = $list
            $result.available  = $true
            $result.raw_source = "Windows ACPI termikus zona (durva becsles - nem szenzoronkenti adat)"
        }
    } catch {
        $result.errors += "A Windows termikus zona sem elerheto: $($_.Exception.Message)"
    }
}

# ───────────────────────── Osszefoglalo + nyers nezet ─────────────────────────
try {
    $temps = @($result.sensors | Where-Object { $_.type -eq "Temperature" })
    if ($temps.Count -gt 0) {
        $maxTemp = ($temps | Sort-Object -Property value -Descending | Select-Object -First 1)
        $result.summary = "Legmagasabb homerseklet: $($maxTemp.value) C ($($maxTemp.name))"
    } elseif ($result.sensors.Count -gt 0) {
        $result.summary = "$($result.sensors.Count) szenzor-ertek beolvasva"
    }
} catch { }

$lines = @()
$lines += "Forras: $($result.raw_source)"
if ($result.sensors.Count -eq 0) {
    $lines += ""
    $lines += "Nincs beolvasott szenzor-adat."
    $lines += "Tipp: telepitsd a LibreHardwareMonitort az Eszkozok panelrol -"
    $lines += "az reszletes homerseklet-, ventilator- es terheles-adatokat ad,"
    $lines += "csendben, ablak megnyitasa nelkul."
} else {
    foreach ($t in @("Temperature", "Load", "Fan", "Clock", "Power")) {
        $group = @($result.sensors | Where-Object { $_.type -eq $t })
        if ($group.Count -eq 0) { continue }
        $lines += ""
        $lines += "[$t]"
        foreach ($s in $group) {
            $lines += ("  {0,-34} {1,8}" -f "$($s.parent) / $($s.name)", $s.value)
        }
    }
}
$result.raw_text = ($lines -join "`r`n")

$json = $result | ConvertTo-Json -Depth 6
if ($OutFile -ne "") {
    try {
        $dir = Split-Path -Parent $OutFile
        if ($dir -ne "" -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        [IO.File]::WriteAllText($OutFile, $json, [Text.UTF8Encoding]::new($false))
    } catch { }
}
$json
