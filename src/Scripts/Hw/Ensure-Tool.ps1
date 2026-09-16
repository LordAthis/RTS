# Verzio: v1.1.0 - 2026-09-16
# RTS - kulso segedeszkoz TELEPITESE / FRISSITESE a Windows sajat
# csomagkezelojevel (winget). Ez a script KIZAROLAG a beszerzesert felel -
# lekerdezest NEM vegez (lasd Get-*Info.ps1). A ketto szetvalasztasa a
# round17 legfontosabb javitasa: korabban minden egyes "Informaciok
# frissitese" gombnyomas ujra megprobalta beszerezni az eszkozoket is.
#
# MIERT WINGET (LordAthis 2026-09-16-i dontese, a Gemini-vel egyeztetett
# megoldas alapjan): a techpowerup (GPU-Z) Cloudflare-t es dinamikus
# szerver-ID-kat hasznal, ezert NINCS fix letoltesi URL - a korabbi
# "us1-dl.techpowerup.com/files/GPU-Z.{verzio}.exe" minta 404-et adott.
# A winget a hivatalos forrasbol, mindig a legfrissebb verziot hozza, es
# beepitett frissitesi funkcioval rendelkezik.
#
# ELTERESEK a kiindulasi Gemini-scripttol (szandekosak, elo hibak miatt):
#  1. A "winget list" AKKOR IS ir kimenetet, ha az adott csomag NINCS
#     telepitve ("No installed package found..."), ezert az eredeti
#     "if ($CheckInstalled)" vizsgalat MINDIG igaznak bizonyult volna.
#     Itt ezert a KILEPESI KODOT (-eq 0) es a kimenet tartalmat is nezzuk.
#  2. Nincs "-Verb RunAs" ujraindulas: az RTS mar emelt jogosultsaggal fut,
#     es a hatterben futo beszerzesnek TILOS UAC-ablakot felvillantania.
#  3. Nincs "nyomj meg egy gombot" varakozas - az ablak nelkuli, csendes
#     futashoz az blokkolna a folyamatot.
#  4. Ha a wingetet nem talaljuk (pl. regi Windows), NEM hibazunk el
#     csendben: egyertelmu, gepi uton feldolgozhato JSON valaszt adunk,
#     amibol a hivo eldontheti, mit irjon ki a felhasznalonak.
#
# ROUND18 JAVITAS - "A winget katalogusaban egyik megadott csomag-azonosito
# sem talalhato" HIBAS UZENET. LordAthis 2026-09-16-i logja szerint a
# LibreHardwareMonitor telepitese ezzel az uzenettel allt le. Utananeztem a
# Microsoft hivatalos winget-pkgs katalogusaban: a
# "LibreHardwareMonitor.LibreHardwareMonitor" azonosito NAGYON IS LETEZIK
# (0.9.3 es 0.9.4 verzioval) - es ugyanigy letezik mind a negy tobbi is
# (TechPowerUp.GPU-Z, CPUID.CPU-Z, CPUID.HWMonitor,
# AngusJohnson.ResourceHacker). Vagyis NEM az azonositokkal volt a baj,
# hanem AZ EN ELLENORZO HIVASOMMAL:
#
# a "winget show" parancsnak atadtam a "--accept-package-agreements" es a
# "--disable-interactivity" kapcsolot is, holott azok az install/upgrade
# parancsokhoz valok. Az ismeretlen kapcsolotol a winget nem nullas
# kilepesi koddal ter vissza, az en feltetelem pedig ezt "a csomag nem
# letezik"-kent ertelmezte. Javitas: MOSTANTOL PARANCSONKENT csak a hozza
# TENYLEGESEN illo kapcsolokat adjuk at (lasd $ReadArgs / $WriteArgs), es
# a feloldas NEM blokkolo: ha a "show" barmi okbol nem ad egyertelmu
# valaszt, az ELSO jelolt azonositoval dolgozunk tovabb - a tenyleges
# telepites hibauzenete ugyis pontosan megmondja, ha valami nem stimmel.

[CmdletBinding()]
param(
    # Egy VAGY TOBB winget csomag-azonosito, vesszovel elvalasztva, pl.
    # "LibreHardwareMonitor.LibreHardwareMonitor,LibreHardwareMonitor".
    # A script sorban ellenorzi oket (winget show), es az ELSO LETEZOT
    # hasznalja. Miert: a winget-katalogus azonositoi idonkent valtoznak
    # vagy mas kiadohoz kerulnek - igy egy atnevezes nem teszi
    # hasznalhatatlanna a funkciot, es nem is talalgatunk vaktaban.
    [Parameter(Mandatory = $true)]
    [string]$PackageIds,

    # "check" = csak allapot + elerheto frissites vizsgalata (SEMMIT nem
    #           telepit, nem modosit - ezt hasznalja a lista-epites)
    # "install" = telepites, ha meg nincs telepitve
    # "upgrade" = frissites, ha van ujabb verzio
    [ValidateSet("check", "install", "upgrade")]
    [string]$Action = "check",

    [string]$OutFile = ""
)

$ErrorActionPreference = "SilentlyContinue"

$candidates = @()
foreach ($c in ("$PackageIds" -split ",")) {
    $t = "$c".Trim()
    if ($t -ne "") { $candidates += $t }
}
$PackageId = ""
if ($candidates.Count -gt 0) { $PackageId = $candidates[0] }

$result = [ordered]@{
    package_id        = $PackageId
    candidates        = $candidates
    resolved          = $false
    action            = $Action
    winget_available  = $false
    installed         = $false
    installed_version = ""
    available_version = ""
    update_available  = $false
    changed           = $false
    exit_code         = 0
    message           = ""
    errors            = @()
}

# ───────────────────────── winget elerheto-e ─────────────────────────
$winget = $null
try {
    $cmd = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($cmd) { $winget = $cmd.Source }
} catch { }

if (-not $winget) {
    # Tartalek: a WindowsApps mappaban is megkeressuk (nem mindig van PATH-ban)
    try {
        $cand = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe"
        if (Test-Path $cand) { $winget = $cand }
    } catch { }
}

if (-not $winget) {
    $result.message = "A winget (Windows Csomagkezelo) nem erheto el ezen a gepen - az automatikus telepites/frissites igy nem lehetseges. Telepitsd az eszkozt kezzel, vagy telepitsd az 'App Installer' csomagot a Microsoft Store-bol."
    $result.errors += "winget_not_found"
    $json = $result | ConvertTo-Json -Depth 5
    if ($OutFile -ne "") { try { [IO.File]::WriteAllText($OutFile, $json, [Text.UTF8Encoding]::new($false)) } catch { } }
    $json
    exit 0
}
$result.winget_available = $true

# OLVASO parancsok (show / list): CSAK a forras-felteteleket fogadjuk el.
# A "--accept-package-agreements" es a "--disable-interactivity" ezeknel
# ismeretlen kapcsolo lenne, amitol a winget hibakoddal lep ki - pontosan
# ez okozta a round18-ban javitott hibas "nem talalhato" uzenetet.
$ReadArgs  = @("--accept-source-agreements")

# IRO parancsok (install / upgrade): itt van helye a csomag-feltetelek
# elfogadasanak es az interaktivitas kikapcsolasanak.
$WriteArgs = @("--accept-source-agreements", "--accept-package-agreements", "--silent", "--disable-interactivity")

function Invoke-Winget {
    param([string[]]$Arguments)
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName  = $winget
        $psi.Arguments = ($Arguments -join " ")
        $psi.UseShellExecute        = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError  = $true
        $psi.CreateNoWindow         = $true
        $p = [System.Diagnostics.Process]::Start($psi)
        $stdout = $p.StandardOutput.ReadToEnd()
        $stderr = $p.StandardError.ReadToEnd()
        $p.WaitForExit()
        return @{ code = $p.ExitCode; out = "$stdout`n$stderr" }
    } catch {
        return @{ code = -1; out = "$($_.Exception.Message)" }
    }
}

# ───────────────────── Csomag-azonosito feloldasa ─────────────────────
# Sorban megnezzuk, melyik jelolt azonosito letezik egyaltalan a
# katalogusban. Az elso letezot hasznaljuk. Ha egyik sem letezik, NEM
# talalgatunk tovabb - egyertelmu uzenettel visszaterunk.
$resolvedId = ""
foreach ($cand in $candidates) {
    $show = Invoke-Winget -Arguments (@("show", "--id", $cand, "--exact") + $ReadArgs)
    $showOut = "$($show.out)"
    # Akkor fogadjuk el, ha a winget 0-val tert vissza ES a kimenet nem
    # mondja kifejezetten, hogy nincs talalat.
    if ($show.code -eq 0 -and $showOut -notmatch "No package found|Nem talalhato|No packages found") {
        $resolvedId = $cand
        break
    }
}

if ($resolvedId -eq "") {
    # NEM allunk le: az elso jelolttel dolgozunk tovabb. A "show" sokfele
    # okbol adhat nem-nullas kodot (nyelvi elteres, katalogus-frissites,
    # halozati hiba, kapcsolo-elteres a winget adott verziojaban) - ezek
    # egyike sem bizonyitja, hogy a csomag nem letezik. A tenyleges
    # install/upgrade hibauzenete pontos lesz, ha tenyleg nincs meg.
    $resolvedId = $candidates[0]
    $result.errors += "resolve_uncertain"
}
else {
    $result.resolved = $true
}

$PackageId            = $resolvedId
$result.package_id    = $resolvedId

# ───────────────────── Telepitett allapot lekerdezese ─────────────────────
# FONTOS: a "winget list" akkor is 0-tol elteroret ad vissza (vagy "No
# installed package found" szoveget ir), ha a csomag NINCS telepitve -
# ezert BOTH a kilepesi kodot es a szoveget vizsgaljuk.
$listRes = Invoke-Winget -Arguments (@("list", "--id", $PackageId, "--exact") + $ReadArgs)
$listOut = "$($listRes.out)"

if ($listRes.code -eq 0 -and $listOut -notmatch "No installed package found" -and $listOut -notmatch "Nem talalhato" -and $listOut -match [regex]::Escape($PackageId)) {
    $result.installed = $true
    # A kimenet utolso, csomag-ID-t tartalmazo soranak oszlopaibol
    # probaljuk kiszedni a verziot (a winget tablazatos kimenete
    # nyelvfuggo, ezert ovatos, "legjobb probalkozas" logika).
    foreach ($line in ($listOut -split "`r?`n")) {
        if ($line -match [regex]::Escape($PackageId)) {
            $cols = ($line -replace '\s{2,}', "`t") -split "`t" | Where-Object { "$_".Trim() -ne "" }
            if ($cols.Count -ge 3) { $result.installed_version = "$($cols[2])".Trim() }
            break
        }
    }
}

# ───────────────────── Elerheto frissites vizsgalata ─────────────────────
if ($result.installed) {
    $upRes = Invoke-Winget -Arguments (@("upgrade", "--id", $PackageId, "--exact", "--include-unknown") + $ReadArgs)
    $upOut = "$($upRes.out)"
    # A --dry-run nem minden winget-verzioban letezik; ha nem tamogatott,
    # a sima "upgrade" listazassal probalkozunk (az nem telepit, csak kiir,
    # ha nincs megadva a csomag telepitesi szandeka).
    if ($upOut -match "unrecognized|ismeretlen|Unknown argument") {
        $upRes = Invoke-Winget -Arguments (@("upgrade") + $ReadArgs)
        $upOut = "$($upRes.out)"
    }
    if ($upOut -match [regex]::Escape($PackageId) -and $upOut -notmatch "No applicable upgrade|Nincs elerheto") {
        $result.update_available = $true
        foreach ($line in ($upOut -split "`r?`n")) {
            if ($line -match [regex]::Escape($PackageId)) {
                $cols = ($line -replace '\s{2,}', "`t") -split "`t" | Where-Object { "$_".Trim() -ne "" }
                if ($cols.Count -ge 4) { $result.available_version = "$($cols[3])".Trim() }
                break
            }
        }
    }
}

# ─────────────────────────── Muvelet vegrehajtasa ───────────────────────────
switch ($Action) {
    "install" {
        if ($result.installed) {
            $result.message = "Mar telepitve van ($($result.installed_version)) - nem tortent valtozas."
        } else {
            $r = Invoke-Winget -Arguments (@("install", "--id", $PackageId, "--exact") + $WriteArgs)
            $result.exit_code = $r.code
            if ($r.code -eq 0) {
                $result.installed = $true
                $result.changed   = $true
                $result.message   = "Sikeresen telepitve."
            } else {
                $result.message = "A telepites sikertelen (winget kilepesi kod: $($r.code))."
                $result.errors += ("$($r.out)".Trim() -split "`r?`n" | Select-Object -Last 5) -join " "
            }
        }
    }
    "upgrade" {
        if (-not $result.installed) {
            $result.message = "Meg nincs telepitve - eloszor telepiteni kell."
        } elseif (-not $result.update_available) {
            $result.message = "Naprakesz ($($result.installed_version)) - nincs elerheto frissites."
        } else {
            $r = Invoke-Winget -Arguments (@("upgrade", "--id", $PackageId, "--exact") + $WriteArgs)
            $result.exit_code = $r.code
            if ($r.code -eq 0) {
                $result.changed          = $true
                $result.update_available = $false
                $result.message          = "Sikeresen frissitve."
            } else {
                $result.message = "A frissites sikertelen (winget kilepesi kod: $($r.code))."
                $result.errors += ("$($r.out)".Trim() -split "`r?`n" | Select-Object -Last 5) -join " "
            }
        }
    }
    default {
        if ($result.installed) {
            $result.message = "Telepitve ($($result.installed_version))."
            if ($result.update_available) { $result.message = "$($result.message) Elerheto frissites: $($result.available_version)." }
        } else {
            $result.message = "Nincs telepitve."
        }
    }
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
