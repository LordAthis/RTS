# Verzio: v1.0.0 - 2026-09-16
# RTS - hardver-lekerdezes, RESZ-SCRIPT: lemezek + SMART allapot.
#
# ONALLOAN IS FUTTATHATO. Alap forras (mind CSENDES, kulso eszkoz nelkul):
#   - Win32_DiskDrive              : modell, meret, interfesz, sorozatszam
#   - Get-PhysicalDisk             : HealthStatus, MediaType (SSD/HDD)
#   - Get-StorageReliabilityCounter: homerseklet, uzemora, kopas, hibak
#   - MSStorageDriver_FailurePredictStatus (root\wmi): SMART elorejelzes
#   - Win32_LogicalDisk            : kotetek szabad/teljes merete
#
# OPCIONALIS MELYITES: -HdsExe eseten a MAR TELEPITETT H.D. Sentinel-lel
# keszitunk egy reszletes riportot is. Letoltes es telepites innen SOHA
# nem indul - ez volt a round16-os hiba: a kod a "hdsentinel_setup.zip"-et
# toltotte le (ami NEM portable, hanem a TELEPITO), kicsomagolta, majd a
# kicsomagolt TELEPITOT inditotta el egy riport-kapcsoloval, amitol az
# feltette a telepito-varazslot - holott a gepen mar telepitve volt.

[CmdletBinding()]
param(
    [string]$OutFile = "",
    [string]$HdsExe = ""
)

$ErrorActionPreference = "SilentlyContinue"

function Get-SafeCim {
    param([string]$ClassName, [string]$Namespace = "root\cimv2")
    try { return Get-CimInstance -ClassName $ClassName -Namespace $Namespace -ErrorAction Stop }
    catch {
        try { return Get-WmiObject -Class $ClassName -Namespace $Namespace -ErrorAction Stop } catch { return $null }
    }
}

$result = [ordered]@{
    section         = "disk"
    queried_at_utc  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    available       = $false
    health_percent  = $null
    drives          = @()
    volumes         = @()
    summary         = ""
    raw_text        = ""
    raw_source      = ""
    errors          = @()
}

# ───────────────────────── Fizikai lemezek ─────────────────────────
try {
    $physical = @{}
    try {
        foreach ($p in (Get-PhysicalDisk -ErrorAction Stop)) {
            $physical["$($p.DeviceId)"] = $p
        }
    } catch { }

    $smartFail = @{}
    try {
        foreach ($s in (Get-SafeCim -ClassName "MSStorageDriver_FailurePredictStatus" -Namespace "root\wmi")) {
            $smartFail["$($s.InstanceName)"] = $s.PredictFailure
        }
    } catch { }

    $disks = Get-SafeCim -ClassName "Win32_DiskDrive"
    $drives = @()
    foreach ($d in $disks) {
        $sizeGb = 0
        try { $sizeGb = [math]::Round([double]$d.Size / 1GB, 1) } catch { }

        $idx = "$($d.Index)"
        $p = $null
        if ($physical.ContainsKey($idx)) { $p = $physical[$idx] }

        $temp = $null; $hours = $null; $wear = $null; $readErr = $null
        try {
            if ($p) {
                $rc = $p | Get-StorageReliabilityCounter -ErrorAction Stop
                if ($rc) {
                    if ($rc.Temperature -ne $null)      { $temp    = [int]$rc.Temperature }
                    if ($rc.PowerOnHours -ne $null)     { $hours   = [int]$rc.PowerOnHours }
                    if ($rc.Wear -ne $null)             { $wear    = [int]$rc.Wear }
                    if ($rc.ReadErrorsTotal -ne $null)  { $readErr = [int]$rc.ReadErrorsTotal }
                }
            }
        } catch { }

        $predictFailure = $null
        foreach ($k in $smartFail.Keys) {
            if ("$k" -like "*$($d.PNPDeviceID)*" -or "$($d.PNPDeviceID)" -like "*$k*") {
                $predictFailure = [bool]$smartFail[$k]
                break
            }
        }

        $health = ""
        if ($p -and "$($p.HealthStatus)" -ne "") { $health = "$($p.HealthStatus)" }
        elseif ($predictFailure -eq $false) { $health = "Healthy" }
        elseif ($predictFailure -eq $true)  { $health = "Warning" }

        $media = ""
        if ($p -and "$($p.MediaType)" -ne "") { $media = "$($p.MediaType)" }

        $drives += [ordered]@{
            index           = $idx
            model           = "$($d.Model)".Trim()
            serial          = "$($d.SerialNumber)".Trim()
            interface       = "$($d.InterfaceType)".Trim()
            size_gb         = $sizeGb
            media_type      = $media
            health_status   = $health
            smart_predict_failure = $predictFailure
            temperature_c   = $temp
            power_on_hours  = $hours
            wear_percent    = $wear
            read_errors     = $readErr
        }
    }
    $result.drives    = $drives
    $result.available = ($drives.Count -gt 0)
} catch { $result.errors += "Lemezek: $($_.Exception.Message)" }

# ───────────────────────────── Kotetek ─────────────────────────────
try {
    $vols = Get-SafeCim -ClassName "Win32_LogicalDisk"
    $list = @()
    foreach ($v in $vols) {
        if ("$($v.DriveType)" -ne "3") { continue }  # csak helyi lemezek
        $total = 0; $free = 0
        try { $total = [math]::Round([double]$v.Size / 1GB, 1) } catch { }
        try { $free  = [math]::Round([double]$v.FreeSpace / 1GB, 1) } catch { }
        $pct = 0
        if ($total -gt 0) { $pct = [math]::Round((($total - $free) / $total) * 100, 0) }
        $list += [ordered]@{
            drive      = "$($v.DeviceID)"
            label      = "$($v.VolumeName)".Trim()
            filesystem = "$($v.FileSystem)".Trim()
            total_gb   = $total
            free_gb    = $free
            used_percent = $pct
        }
    }
    $result.volumes = $list
} catch { $result.errors += "Kotetek: $($_.Exception.Message)" }

# ─────────────────────── Osszesitett egeszseg-szazalek ───────────────────────
# A WMI nem ad szazalekos egeszseget (azt a H.D. Sentinel szamolja sajat
# algoritmussal), ezert itt egy OVATOS, egyszeru becslest adunk, es ezt
# EGYERTELMUEN jelezzuk is a nyers szovegben - nem allitjuk be HDS-ertekkent.
try {
    if ($result.drives.Count -gt 0) {
        $worst = 100
        foreach ($d in $result.drives) {
            $score = 100
            if ($d.smart_predict_failure -eq $true) { $score = 10 }
            elseif ($d.health_status -eq "Warning")  { $score = 50 }
            elseif ($d.health_status -eq "Unhealthy") { $score = 20 }
            if ($d.wear_percent -ne $null -and $d.wear_percent -gt 0) {
                $wearScore = 100 - [int]$d.wear_percent
                if ($wearScore -lt $score) { $score = $wearScore }
            }
            if ($score -lt $worst) { $worst = $score }
        }
        $result.health_percent = $worst
    }
} catch { }

# ───────────────────────── Rovid osszefoglalo ─────────────────────────
try {
    if ($result.drives.Count -gt 0) {
        $d0 = $result.drives[0]
        $s = "$($d0.model) ($($d0.size_gb) GB"
        if ($d0.media_type -ne "") { $s = "$s, $($d0.media_type)" }
        $s = "$s)"
        if ($d0.health_status -ne "") { $s = "$s - $($d0.health_status)" }
        if ($result.drives.Count -gt 1) { $s = "$s  [+$($result.drives.Count - 1) tovabbi lemez]" }
        $result.summary = $s
    }
} catch { }

# ───────── Opcionalis: H.D. Sentinel adatai a SAJAT WMI-nevterebol ─────────
# Ez a GYORSABB es TISZTABB ut (LordAthis 2026-09-16-i anyaga alapjan):
# ha a H.D. Sentinel fut, sajat WMI-osztalyt (root\wmi -> hdsentinel)
# publikal, amibol a kondicio, teljesitmeny, homerseklet es uzemido
# KOZVETLENUL kiolvashato - riport-fajl keszitese es parse-olasa nelkul,
# ablak megnyitasa nelkul.
$hdsFromWmi = $false
try {
    $hdsWmi = $null
    try { $hdsWmi = Get-CimInstance -Namespace "root\wmi" -ClassName "hdsentinel" -ErrorAction Stop } catch { }

    # Ha a WMI-osztaly meg nincs feltoltve, de az exe megvan, elinditjuk a
    # hatterben (-r: csendes riport-mod), es ujraprobaljuk.
    if (-not $hdsWmi -and $HdsExe -ne "" -and (Test-Path $HdsExe)) {
        if (-not (Get-Process "HDSentinel" -ErrorAction SilentlyContinue)) {
            Start-Process -FilePath $HdsExe -ArgumentList "-r" -WindowStyle Hidden -ErrorAction SilentlyContinue | Out-Null
            Start-Sleep -Seconds 3
        }
        try { $hdsWmi = Get-CimInstance -Namespace "root\wmi" -ClassName "hdsentinel" -ErrorAction Stop } catch { }
    }

    if ($hdsWmi) {
        $lines = @()
        $lines += "Forras: Hard Disk Sentinel (root\wmi -> hdsentinel)"
        $bestHealth = $null
        foreach ($d in $hdsWmi) {
            $model  = "$($d.Model)".Trim()
            $health = $null
            try { $health = [int]$d.Health } catch { }
            $perf = $null
            try { $perf = [int]$d.Performance } catch { }
            $temp = $null
            try { $temp = [int]$d.Temperature } catch { }
            $days = $null
            try { $days = [int]$d.PowerOnDays } catch { }

            $lines += ""
            $lines += "Lemez        : $model"
            $lines += "  Kondicio   : $health %"
            $lines += "  Teljesitm. : $perf %"
            $lines += "  Homerseklet: $temp C"
            $lines += "  Uzemido    : $days nap"

            if ($health -ne $null -and ($bestHealth -eq $null -or $health -lt $bestHealth)) { $bestHealth = $health }

            # A strukturalt lemez-listat is kiegeszitjuk a HDS ertekeivel,
            # ha megtalaljuk a megfelelo (azonos modellnevu) bejegyzest.
            for ($i = 0; $i -lt $result.drives.Count; $i++) {
                if ("$($result.drives[$i].model)".Trim() -eq $model) {
                    if ($temp -ne $null)   { $result.drives[$i].temperature_c = $temp }
                    if ($health -ne $null) { $result.drives[$i].health_status = "$health%" }
                    break
                }
            }
        }

        if ($bestHealth -ne $null) { $result.health_percent = $bestHealth }
        $result.raw_text   = ($lines -join "`r`n")
        $result.raw_source = "Hard Disk Sentinel (WMI)"
        $hdsFromWmi = $true
    }
} catch {
    $result.errors += "H.D. Sentinel WMI: $($_.Exception.Message)"
}

# ───────── Tartalek: riport-fajl a MAR TELEPITETT H.D. Sentinel-bol ─────────
# Csak akkor, ha a WMI-ut nem valt be.
if (-not $hdsFromWmi -and $HdsExe -ne "" -and (Test-Path $HdsExe)) {
    try {
        $reportFile = Join-Path $env:TEMP ("rts_hds_" + [Guid]::NewGuid().ToString("N") + ".txt")
        # A H.D. Sentinel "-r <fajl>" kapcsoloja szoveges riportot ir es
        # kilep. (A /REPORT= forma a GUI-valtozate; a "-r" a csendes,
        # parancssori mod - ha az egyik nem valik be az elo gepen, a masik
        # a tartalek, ezert probaljuk sorban mindkettot.)
        $args = @("-r", "`"$reportFile`"")
        $proc = Start-Process -FilePath $HdsExe -ArgumentList $args -WindowStyle Hidden -PassThru -ErrorAction Stop
        $null = $proc.WaitForExit(90000)
        if (-not $proc.HasExited) { try { $proc.Kill() } catch { } }

        if (-not (Test-Path $reportFile)) {
            $proc2 = Start-Process -FilePath $HdsExe -ArgumentList "/REPORT=`"$reportFile`"" `
                                   -WindowStyle Hidden -PassThru -ErrorAction Stop
            $null = $proc2.WaitForExit(90000)
            if (-not $proc2.HasExited) { try { $proc2.Kill() } catch { } }
        }

        if (Test-Path $reportFile) {
            $result.raw_text   = [IO.File]::ReadAllText($reportFile)
            $result.raw_source = "H.D. Sentinel ($HdsExe)"
            # A HDS sajat, pontosabb egeszseg-szazaleka felulirja a becslest.
            $m = [regex]::Match($result.raw_text, 'Health[^0-9]{0,30}(\d{1,3})\s*%', 'IgnoreCase')
            if ($m.Success) {
                try { $result.health_percent = [int]$m.Groups[1].Value } catch { }
            }
            Remove-Item $reportFile -Force -ErrorAction SilentlyContinue
        } else {
            $result.errors += "A H.D. Sentinel riport-fajl nem jott letre (-r es /REPORT= kapcsolo is sikertelen)."
        }
    } catch { $result.errors += "H.D. Sentinel: $($_.Exception.Message)" }
}

if ($result.raw_text -eq "") {
    $lines = @()
    $lines += "Forras: Windows WMI/CIM + Storage (SMART)"
    $lines += "MEGJEGYZES: a lenti egeszseg-szazalek OVATOS BECSLES a SMART-"
    $lines += "allapotbol es a kopasbol - NEM a H.D. Sentinel sajat ertekelese."
    foreach ($d in $result.drives) {
        $lines += ""
        $lines += "Lemez #$($d.index)   : $($d.model)"
        $lines += "  Meret       : $($d.size_gb) GB ($($d.media_type))"
        $lines += "  Interfesz   : $($d.interface)"
        $lines += "  Allapot     : $($d.health_status)  (SMART hiba-elorejelzes: $($d.smart_predict_failure))"
        $lines += "  Homerseklet : $($d.temperature_c) C"
        $lines += "  Uzemora     : $($d.power_on_hours) ora"
        $lines += "  Kopas       : $($d.wear_percent) %"
        $lines += "  Olvasasi hibak: $($d.read_errors)"
    }
    $lines += ""
    $lines += "Kotetek:"
    foreach ($v in $result.volumes) {
        $lines += "  $($v.drive) $($v.label) [$($v.filesystem)]  $($v.free_gb) GB szabad / $($v.total_gb) GB  ($($v.used_percent)% foglalt)"
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
