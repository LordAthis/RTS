# Verzio: v1.1.0 - 2026-09-16
# RTS - hardver-lekerdezes, RESZ-SCRIPT: gep / OS / alaplap / BIOS / memoria.
#
# ONALLOAN IS FUTTATHATO (ezt kerte a felhasznalo: "minden lekerdezes
# kulonallo ps1 legyen"), es a Get-HardwareReport.ps1 orchestrator is
# ezt hivja meg. Kimenet: EGY JSON objektum a standard kimenetre
# (-OutFile eseten fajlba is).
#
# CSENDES: nem ir a kepernyore, nem nyit ablakot, nem var billentyure,
# nem igenyel rendszergazdai jogot. Minden lekerdezes sajat try/catch-ben
# fut - egy hibas ag SOSEM allitja meg a tobbit, csak ures/null mezot ad.
#
# KOMPATIBILITAS: PowerShell 3.0+ (Get-CimInstance). A projekt retro-
# iranyultsaga miatt szandekosan NEM hasznal PowerShell 7-only szintaxist
# (nincs ternary, nincs ?? operator, nincs -Parallel).

[CmdletBinding()]
param(
    [string]$OutFile = ""
)

$ErrorActionPreference = "SilentlyContinue"

function Get-SafeCim {
    param([string]$ClassName, [string]$Query = "")
    try {
        if ($Query -ne "") { return Get-CimInstance -Query $Query -ErrorAction Stop }
        return Get-CimInstance -ClassName $ClassName -ErrorAction Stop
    } catch {
        try {
            # Tartalek regebbi rendszerekre, ahol a CIM/WinRM nem elerheto
            if ($Query -ne "") { return Get-WmiObject -Query $Query -ErrorAction Stop }
            return Get-WmiObject -Class $ClassName -ErrorAction Stop
        } catch {
            return $null
        }
    }
}

$result = [ordered]@{
    section        = "system"
    queried_at_utc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    machine_name   = $env:COMPUTERNAME
    os_caption     = ""
    os_version     = ""
    os_build       = ""
    os_arch        = ""
    os_install_date = ""
    last_boot      = ""
    manufacturer   = ""
    model          = ""
    baseboard      = ""
    bios           = ""
    bios_date      = ""
    ram_total_gb   = 0
    ram_modules    = @()
    local_user_accounts = @()
    errors         = @()
}

# ───────────────────────── Operacios rendszer ─────────────────────────
try {
    $os = Get-SafeCim -ClassName "Win32_OperatingSystem" | Select-Object -First 1
    if ($os) {
        $result.os_caption = "$($os.Caption)".Trim()
        $result.os_version = "$($os.Version)".Trim()
        $result.os_build   = "$($os.BuildNumber)".Trim()
        $result.os_arch    = "$($os.OSArchitecture)".Trim()
        if ($os.InstallDate) {
            try { $result.os_install_date = ([Management.ManagementDateTimeConverter]::ToDateTime($os.InstallDate)).ToString("yyyy-MM-dd") } catch { }
            if ($result.os_install_date -eq "" -and $os.InstallDate -is [DateTime]) {
                $result.os_install_date = $os.InstallDate.ToString("yyyy-MM-dd")
            }
        }
        if ($os.LastBootUpTime) {
            try { $result.last_boot = ([Management.ManagementDateTimeConverter]::ToDateTime($os.LastBootUpTime)).ToString("yyyy-MM-dd HH:mm") } catch { }
            if ($result.last_boot -eq "" -and $os.LastBootUpTime -is [DateTime]) {
                $result.last_boot = $os.LastBootUpTime.ToString("yyyy-MM-dd HH:mm")
            }
        }
    }
} catch { $result.errors += "OS: $($_.Exception.Message)" }

# ───────────────────────── Gep / alaplap / BIOS ─────────────────────────
try {
    $cs = Get-SafeCim -ClassName "Win32_ComputerSystem" | Select-Object -First 1
    if ($cs) {
        $result.manufacturer = "$($cs.Manufacturer)".Trim()
        $result.model        = "$($cs.Model)".Trim()
    }
} catch { $result.errors += "ComputerSystem: $($_.Exception.Message)" }

try {
    $bb = Get-SafeCim -ClassName "Win32_BaseBoard" | Select-Object -First 1
    if ($bb) { $result.baseboard = ("$($bb.Manufacturer) $($bb.Product)").Trim() }
} catch { $result.errors += "BaseBoard: $($_.Exception.Message)" }

try {
    $bios = Get-SafeCim -ClassName "Win32_BIOS" | Select-Object -First 1
    if ($bios) {
        $result.bios = ("$($bios.Manufacturer) $($bios.SMBIOSBIOSVersion)").Trim()
        if ($bios.ReleaseDate) {
            try { $result.bios_date = ([Management.ManagementDateTimeConverter]::ToDateTime($bios.ReleaseDate)).ToString("yyyy-MM-dd") } catch { }
            if ($result.bios_date -eq "" -and $bios.ReleaseDate -is [DateTime]) {
                $result.bios_date = $bios.ReleaseDate.ToString("yyyy-MM-dd")
            }
        }
    }
} catch { $result.errors += "BIOS: $($_.Exception.Message)" }

# ───────────────────────────── Memoria ─────────────────────────────
try {
    $mem = Get-SafeCim -ClassName "Win32_PhysicalMemory"
    if ($mem) {
        $totalBytes = 0
        $modules = @()
        foreach ($m in $mem) {
            $cap = 0
            try { $cap = [double]$m.Capacity } catch { $cap = 0 }
            $totalBytes += $cap
            $modules += [ordered]@{
                slot     = "$($m.DeviceLocator)".Trim()
                size_gb  = [math]::Round($cap / 1GB, 1)
                speed    = "$($m.Speed)".Trim()
                manufacturer = "$($m.Manufacturer)".Trim()
                part_number  = "$($m.PartNumber)".Trim()
            }
        }
        $result.ram_total_gb = [math]::Round($totalBytes / 1GB, 1)
        $result.ram_modules  = $modules
    }
} catch { $result.errors += "Memoria: $($_.Exception.Message)" }

# ─────────────────────── Helyi felhasznaloi fiokok ───────────────────────
try {
    $users = Get-SafeCim -Query "SELECT Name FROM Win32_UserAccount WHERE LocalAccount = True AND Disabled = False"
    if ($users) {
        $names = @()
        foreach ($u in $users) { if ("$($u.Name)".Trim() -ne "") { $names += "$($u.Name)".Trim() } }
        $result.local_user_accounts = $names
    }
} catch { $result.errors += "Fiokok: $($_.Exception.Message)" }

# ───────────────────────── Rovid osszefoglalo ─────────────────────────
$summaryParts = @()
if ($result.os_caption -ne "") { $summaryParts += $result.os_caption }
if ($result.ram_total_gb -gt 0) { $summaryParts += "$($result.ram_total_gb) GB RAM" }
$result.summary = ($summaryParts -join " | ")

# ───────────────────── Nyers, olvashato nezet ─────────────────────
# ROUND18 JAVITAS: ez a szekcio korabban NEM allitott be raw_text-et, csak
# summary-t - ezert a felulet "Rendszer, alaplap, memoria" lenyiloja
# "Nincs adat."-ot mutatott, holott az adatok megvoltak (LordAthis
# 2026-09-16-i kepe). A tobbi resz-script mindig adott raw_text-et; ez
# egyszeruen kimaradt.
$result.available  = $true
$result.raw_source = "WMI"

$lines = @()
$lines += "Forras: Windows WMI/CIM"
$lines += ""
$lines += "Gep            : $($result.machine_name)"
$lines += "Gyarto / modell: $($result.manufacturer) $($result.model)"
$lines += "Alaplap        : $($result.baseboard)"
$lines += "BIOS           : $($result.bios)  ($($result.bios_date))"
$lines += ""
$lines += "Operacios rendszer"
$lines += "  Nev          : $($result.os_caption)"
$lines += "  Verzio       : $($result.os_version)  (build $($result.os_build))"
$lines += "  Architektura : $($result.os_arch)"
$lines += "  Telepitve    : $($result.os_install_date)"
$lines += "  Utolso indit.: $($result.last_boot)"
$lines += ""
$lines += "Memoria: osszesen $($result.ram_total_gb) GB"
foreach ($m in $result.ram_modules) {
    $lines += ("  {0,-12} {1,6} GB  {2,6} MHz  {3} {4}" -f $m.slot, $m.size_gb, $m.speed, $m.manufacturer, $m.part_number)
}
if ($result.local_user_accounts.Count -gt 0) {
    $lines += ""
    $lines += "Helyi felhasznaloi fiokok: " + ($result.local_user_accounts -join ", ")
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
