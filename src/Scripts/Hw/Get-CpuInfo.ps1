# Verzio: v1.0.0 - 2026-09-16
# RTS - hardver-lekerdezes, RESZ-SCRIPT: processzor (CPU).
#
# ONALLOAN IS FUTTATHATO. Alap forras: WMI/CIM (Win32_Processor) - ez
# CSENDES, azonnali, es NEM igenyel semmilyen kulso eszkozt.
#
# OPCIONALIS MELYITES: ha a -CpuZExe parameterrel kapunk egy MAR MEGLEVO
# CPU-Z peldanyt, abbol egy reszletes -txt riportot is keszitunk. Ha nincs
# ilyen (vagy hibara fut), az alap WMI-adatok akkor is megvannak - a script
# SOSEM tolt le semmit es SOSEM indit telepitot (ez volt a round16-os hiba
# gyokere: a lekerdezes es a beszerzes ossze volt keverve).

[CmdletBinding()]
param(
    [string]$OutFile = "",
    [string]$CpuZExe = ""
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
    section        = "cpu"
    queried_at_utc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    available      = $false
    name           = ""
    manufacturer   = ""
    cores          = 0
    threads        = 0
    max_clock_mhz  = 0
    socket         = ""
    l2_cache_kb    = 0
    l3_cache_kb    = 0
    summary        = ""
    raw_text       = ""
    raw_source     = ""
    errors         = @()
}

try {
    $cpus = Get-SafeCim -ClassName "Win32_Processor"
    $cpu = $cpus | Select-Object -First 1
    if ($cpu) {
        $result.available     = $true
        $result.name          = "$($cpu.Name)".Trim()
        $result.manufacturer  = "$($cpu.Manufacturer)".Trim()
        try { $result.cores   = [int]$cpu.NumberOfCores } catch { }
        try { $result.threads = [int]$cpu.NumberOfLogicalProcessors } catch { }
        try { $result.max_clock_mhz = [int]$cpu.MaxClockSpeed } catch { }
        $result.socket        = "$($cpu.SocketDesignation)".Trim()
        try { $result.l2_cache_kb = [int]$cpu.L2CacheSize } catch { }
        try { $result.l3_cache_kb = [int]$cpu.L3CacheSize } catch { }
    } else {
        $result.errors += "A Win32_Processor lekerdezes nem adott vissza adatot."
    }
} catch { $result.errors += "CPU (WMI): $($_.Exception.Message)" }

# Rovid osszefoglalo - ezt mutatja a B2 doboz es az A2 fejlec.
$parts = @()
if ($result.name -ne "") { $parts += $result.name }
if ($result.cores -gt 0) {
    $c = "$($result.cores) mag"
    if ($result.threads -gt 0) { $c = "$c / $($result.threads) szal" }
    $parts += $c
}
$result.summary = ($parts -join " (")
if ($result.cores -gt 0 -and $result.name -ne "") { $result.summary = "$($result.summary))" }

# ───────────── Opcionalis: reszletes riport MEGLEVO CPU-Z-bol ─────────────
# FONTOS: itt NINCS letoltes es NINCS telepites - csak akkor hasznaljuk,
# ha a hivo atadott egy letezo exe-t.
if ($CpuZExe -ne "" -and (Test-Path $CpuZExe)) {
    try {
        $tmpBase = Join-Path $env:TEMP ("rts_cpuz_" + [Guid]::NewGuid().ToString("N"))
        $proc = Start-Process -FilePath $CpuZExe -ArgumentList "-txt=`"$tmpBase`"" `
                              -WindowStyle Hidden -PassThru -ErrorAction Stop
        $null = $proc.WaitForExit(60000)
        if (-not $proc.HasExited) { try { $proc.Kill() } catch { } }

        $txt = "$tmpBase.txt"
        if (Test-Path $txt) {
            $result.raw_text   = [IO.File]::ReadAllText($txt)
            $result.raw_source = "CPU-Z ($CpuZExe)"
            Remove-Item $txt -Force -ErrorAction SilentlyContinue
        } else {
            $result.errors += "A CPU-Z riport-fajl nem jott letre (-txt kapcsolo)."
        }
    } catch { $result.errors += "CPU-Z: $($_.Exception.Message)" }
}

if ($result.raw_text -eq "") {
    # Ha nincs CPU-Z, a nyers nezet a WMI-adatokbol keszul - igy a
    # "reszletek" lenyilo SOSEM marad uresen.
    $lines = @()
    $lines += "Forras: Windows WMI/CIM (Win32_Processor)"
    $lines += "Nev            : $($result.name)"
    $lines += "Gyarto         : $($result.manufacturer)"
    $lines += "Magok / szalak : $($result.cores) / $($result.threads)"
    $lines += "Max. orajel    : $($result.max_clock_mhz) MHz"
    $lines += "Foglalat       : $($result.socket)"
    $lines += "L2 / L3 cache  : $($result.l2_cache_kb) KB / $($result.l3_cache_kb) KB"
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
