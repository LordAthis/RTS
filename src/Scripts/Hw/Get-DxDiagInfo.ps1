# Verzio: v1.1.0 - 2026-09-16
# RTS - hardver-lekerdezes, RESZ-SCRIPT: DXDIAG teljes rendszer-riport.
#
# ONALLOAN IS FUTTATHATO. A Windows sajat dxdiag eszkozet hivja
# "/t <fajl> /whql:off" kapcsolokkal, CSENDES modon (rejtett ablak).
#
# FONTOS - MIERT NEM RESZE AZ ALAPERTELMEZETT (-Only all) LEKERDEZESNEK:
# a dxdiag LASSU (tipikusan 15-40 masodperc), es a riportot ASZINKRON,
# a folyamat kilepese UTAN irja ki, ezert varakozni kell ra. Egy gyors,
# induláskori hatter-lekerdezest ez indokolatlanul elnyujtana - a
# leggyakrabban keresett adatokat (CPU/GPU/lemez/OS) a tobbi resz-script
# mar masodpercek alatt megadja. A DXDIAG igy KULON, kezi keresre fut:
# az Eszkozok panelen a DXDIAG sor melletti "Uj gyorsjelentes" gombbal,
# vagy a Get-HardwareReport.ps1 -Only dxdiag hivassal.
#
# ROUND18: az alapertelmezett varakozas 120 -> 240 masodpercre nott.
# LordAthis 2026-09-16-i gepen a dxdiag tobb mint 2 percig futott, ezert a
# korabbi 120 masodperces korlat lejart, mielott a riport elkeszult volna
# ("A DXDIAG riport-fajl nem jott letre a varakozasi ido alatt (120 mp)").
# A hivo C# oldalon is egyutt nott a hatarido (lasd HardwareQueryService).

[CmdletBinding()]
param(
    [string]$OutFile = "",
    [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = "SilentlyContinue"

$result = [ordered]@{
    section        = "dxdiag"
    queried_at_utc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    available      = $false
    summary        = ""
    raw_text       = ""
    raw_source     = ""
    errors         = @()
}

try {
    $reportFile = Join-Path $env:TEMP ("rts_dxdiag_" + [Guid]::NewGuid().ToString("N") + ".txt")
    if (Test-Path $reportFile) { Remove-Item $reportFile -Force -ErrorAction SilentlyContinue }

    $proc = Start-Process -FilePath "dxdiag.exe" -ArgumentList "/t `"$reportFile`" /whql:off" `
                          -WindowStyle Hidden -PassThru -ErrorAction Stop
    $null = $proc.WaitForExit($TimeoutSeconds * 1000)
    if (-not $proc.HasExited) { try { $proc.Kill() } catch { } }

    # A dxdiag a fajlt a folyamat kilepese UTAN is irhatja meg - rovid,
    # ismetelt ellenorzessel varunk ra.
    $waited = 0
    while (-not (Test-Path $reportFile) -and $waited -lt $TimeoutSeconds) {
        Start-Sleep -Seconds 1
        $waited++
    }

    if (Test-Path $reportFile) {
        $result.raw_text   = [IO.File]::ReadAllText($reportFile)
        $result.raw_source = "dxdiag /t"
        $result.available  = $true

        # Rovid osszefoglalo a legfontosabb sorokbol.
        $parts = @()
        foreach ($pattern in @('Operating System:\s*(.+)', 'System Model:\s*(.+)', 'Processor:\s*(.+)')) {
            $m = [regex]::Match($result.raw_text, $pattern)
            if ($m.Success) { $parts += $m.Groups[1].Value.Trim() }
        }
        $result.summary = ($parts -join " | ")

        Remove-Item $reportFile -Force -ErrorAction SilentlyContinue
    } else {
        $result.errors += "A DXDIAG riport-fajl nem jott letre a varakozasi ido alatt ($TimeoutSeconds mp)."
    }
} catch {
    $result.errors += "DXDIAG: $($_.Exception.Message)"
}

$json = $result | ConvertTo-Json -Depth 5
if ($OutFile -ne "") {
    try {
        $dir = Split-Path -Parent $OutFile
        if ($dir -ne "" -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        [IO.File]::WriteAllText($OutFile, $json, [Text.UTF8Encoding]::new($false))
    } catch { }
}
$json
