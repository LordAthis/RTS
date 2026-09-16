# Verzio: v1.0.0 - 2026-09-16
# RTS - hardver-lekerdezes, RESZ-SCRIPT: videokartya (GPU).
#
# ONALLOAN IS FUTTATHATO. Alap forras: WMI/CIM (Win32_VideoController) -
# CSENDES, azonnali, kulso eszkoz NELKUL megadja a GPU nevet, a VRAM
# meretet, a driver verziojat es datumat, valamint a felbontast.
#
# MIERT NEM A GPU-Z A FO FORRAS (round17-es dontes, LordAthis jovahagyasaval):
# a GPU-Z-nek NINCS csendes riport-modja - a korabbi megoldas elinditotta,
# irt neki egy ini-t, 6 masodpercet vart, majd KILOTTE a folyamatot es egy
# sensor-log utolso sorat probalta ertelmezni. Ez lassu, torekeny, es
# ablakot villantott. A techpowerup ezen felul Cloudflare-t es dinamikus
# szerver-ID-kat hasznal, ezert FIX letoltesi URL SINCS (ez okozta a
# 404-es hibat). A GPU-Z igy opcionalis, KEZZEL megnyithato eszkoz marad,
# a riport pedig nem fugg tole.
#
# OPCIONALIS MELYITES: -GpuZExe eseten a MEGLEVO GPU-Z peldanyt hasznaljuk
# egy sensor-log keszitesere. Letoltes/telepites innen SOHA nem indul.

[CmdletBinding()]
param(
    [string]$OutFile = "",
    [string]$GpuZExe = "",
    [int]$GpuZWaitSeconds = 6
)

$ErrorActionPreference = "SilentlyContinue"

function Get-SafeCim {
    param([string]$ClassName)
    try { return Get-CimInstance -ClassName $ClassName -ErrorAction Stop }
    catch {
        try { return Get-WmiObject -Class $ClassName -ErrorAction Stop } catch { return $null }
    }
}

$result = [ordered]@{
    section        = "gpu"
    queried_at_utc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    available      = $false
    adapters       = @()
    summary        = ""
    raw_text       = ""
    raw_source     = ""
    errors         = @()
}

try {
    $gpus = Get-SafeCim -ClassName "Win32_VideoController"
    if ($gpus) {
        $adapters = @()
        foreach ($g in $gpus) {
            $vramGb = 0
            try {
                # AdapterRAM 32 biten 4 GB felett tulcsordulhat - ilyenkor a
                # registry-bol probaljuk (HardwareInformation.qwMemorySize).
                $ram = [double]$g.AdapterRAM
                if ($ram -gt 0) { $vramGb = [math]::Round($ram / 1GB, 2) }
            } catch { }

            $drvDate = ""
            if ($g.DriverDate) {
                try { $drvDate = ([Management.ManagementDateTimeConverter]::ToDateTime($g.DriverDate)).ToString("yyyy-MM-dd") } catch { }
                if ($drvDate -eq "" -and $g.DriverDate -is [DateTime]) { $drvDate = $g.DriverDate.ToString("yyyy-MM-dd") }
            }

            $adapters += [ordered]@{
                name            = "$($g.Name)".Trim()
                vram_gb         = $vramGb
                driver_version  = "$($g.DriverVersion)".Trim()
                driver_date     = $drvDate
                video_processor = "$($g.VideoProcessor)".Trim()
                resolution      = "$($g.CurrentHorizontalResolution)x$($g.CurrentVerticalResolution)"
                refresh_hz      = "$($g.CurrentRefreshRate)".Trim()
                status          = "$($g.Status)".Trim()
            }
        }
        $result.adapters  = $adapters
        $result.available = ($adapters.Count -gt 0)
    } else {
        $result.errors += "A Win32_VideoController lekerdezes nem adott vissza adatot."
    }
} catch { $result.errors += "GPU (WMI): $($_.Exception.Message)" }

# VRAM tartalek a registrybol, ha a WMI 0-t/tulcsordult erteket adott.
try {
    $regBase = "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
    if (Test-Path $regBase) {
        $i = 0
        foreach ($key in (Get-ChildItem $regBase -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^\d{4}$' })) {
            $qw = (Get-ItemProperty -Path $key.PSPath -Name "HardwareInformation.qwMemorySize" -ErrorAction SilentlyContinue)."HardwareInformation.qwMemorySize"
            if ($qw -and $i -lt $result.adapters.Count) {
                if ($result.adapters[$i].vram_gb -le 0) {
                    $result.adapters[$i].vram_gb = [math]::Round([double]$qw / 1GB, 2)
                }
            }
            $i++
        }
    }
} catch { }

if ($result.adapters.Count -gt 0) {
    $first = $result.adapters[0]
    $s = $first.name
    if ($first.vram_gb -gt 0) { $s = "$s ($($first.vram_gb) GB)" }
    $result.summary = $s
}

# ───────────── Opcionalis: sensor-log MEGLEVO GPU-Z-bol ─────────────
if ($GpuZExe -ne "" -and (Test-Path $GpuZExe)) {
    try {
        $toolDir = Split-Path -Parent $GpuZExe
        $logFile = Join-Path $env:TEMP ("rts_gpuz_" + [Guid]::NewGuid().ToString("N") + ".txt")
        $iniPath = Join-Path $toolDir "GPU-Z.ini"
        try {
            [IO.File]::WriteAllText($iniPath, "[General]`r`nOpenAtStartup = 0`r`nSensorLogFileName = $logFile`r`nLogSensorsOnStartup = 1`r`n")
        } catch { }

        $proc = Start-Process -FilePath $GpuZExe -ArgumentList "-minimized" -WorkingDirectory $toolDir `
                              -WindowStyle Hidden -PassThru -ErrorAction Stop
        Start-Sleep -Seconds $GpuZWaitSeconds
        try { if (-not $proc.HasExited) { $proc.Kill() } } catch { }

        if (Test-Path $logFile) {
            $lines = Get-Content $logFile -ErrorAction SilentlyContinue
            if ($lines -and $lines.Count -gt 0) {
                $result.raw_text   = ($lines -join "`r`n")
                $result.raw_source = "GPU-Z ($GpuZExe)"
            }
            Remove-Item $logFile -Force -ErrorAction SilentlyContinue
        } else {
            $result.errors += "A GPU-Z sensor-log nem jott letre."
        }
    } catch { $result.errors += "GPU-Z: $($_.Exception.Message)" }
}

if ($result.raw_text -eq "") {
    $lines = @()
    $lines += "Forras: Windows WMI/CIM (Win32_VideoController)"
    foreach ($a in $result.adapters) {
        $lines += ""
        $lines += "Adapter        : $($a.name)"
        $lines += "VRAM           : $($a.vram_gb) GB"
        $lines += "Driver         : $($a.driver_version) ($($a.driver_date))"
        $lines += "Video processz.: $($a.video_processor)"
        $lines += "Felbontas      : $($a.resolution) @ $($a.refresh_hz) Hz"
        $lines += "Allapot        : $($a.status)"
    }
    $result.raw_text   = ($lines -join "`r`n")
    $result.raw_source = "WMI"
}

$json = $result | ConvertTo-Json -Depth 6
if ($OutFile -ne "") {
    try {
        $dir = Split-Path -Parent $OutFile
        if ($dir -ne "" -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        [IO.File]::WriteAllText($OutFile, $json, [Text.UTF8Encoding]::new($false))
    } catch { }
}
$json
