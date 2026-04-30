<#
.SYNOPSIS
    RepoFixer v4.3 - Windows letöltési zárolás feloldó, jobb klikkes telepítéssel
#>

param([string]$StartDir)

# --- ADMIN AUTO-RELAUNCH ---
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $CurrentDir = if ($StartDir) { $StartDir } else { (Get-Location).Path }
    $Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -StartDir `"$CurrentDir`""
    Start-Process powershell.exe -ArgumentList $Arguments -Verb RunAs
    exit
}

# Beállítjuk a munkakönyvtárat – idézőjeleket és szóközt trimmelünk az útvonalból
if ($StartDir) {
    $StartDir = $StartDir.Trim().Trim('"').Trim("'")
    if (Test-Path $StartDir) {
        Set-Location $StartDir
    } else {
        Write-Host "  [FIGYELEM] A kapott StartDir nem letezik: '$StartDir'" -ForegroundColor Yellow
        Write-Host "  Az aktualis mappa lesz a cel: $((Get-Location).Path)"  -ForegroundColor Yellow
    }
}

$ScriptName  = "RepoFixer.ps1"
$TargetDir   = "$env:SystemRoot\Scripts"
$FinalPath   = Join-Path $TargetDir $ScriptName

# --- 1. TELEPÍTÉSI LOGIKA ---
$CurrentLocation = $PSCommandPath
if (-not ($CurrentLocation.StartsWith($TargetDir, [System.StringComparison]::OrdinalIgnoreCase))) {

    Write-Host "--- TELEPITO MOD ---" -ForegroundColor Yellow
    $Choice = Read-Host "Telepited/Frissited a scriptet a rendszerbe? (i/n)"

    if ($Choice -ieq 'i') {

        if (-not (Test-Path $TargetDir)) {
            New-Item -Path $TargetDir -ItemType Directory -Force | Out-Null
        }

        Copy-Item -Path $CurrentLocation -Destination $FinalPath -Force
        Write-Host "Script masolva: $FinalPath" -ForegroundColor Cyan

        # --- Registry műveletek .NET API-val ---
        # A Microsoft.Win32.Registry közvetlenül kezeli a HKCR\* kulcsot,
        # nem fagy be mint a PS provider vagy a reg.exe.

        $MenuLabel = "Fajlok feloldasa (RepoFixer)"
        $MenuIcon  = "powershell.exe,0"

        # Parancs mappára / mappa hátterére: StartDir = az adott mappa
        $CmdFolder = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$FinalPath`" -StartDir `"%1`""
        $CmdBg     = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$FinalPath`" -StartDir `"%V`""
        # Parancs fájlra: StartDir = a fájl szülőmappája
        $CmdFile   = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$FinalPath`" -StartDir `"%~dp1.`""

        $RegEntries = @(
            @{ Hive = "Directory\shell\RepoFixer";            Cmd = $CmdFolder },
            @{ Hive = "Directory\Background\shell\RepoFixer"; Cmd = $CmdBg     },
            @{ Hive = "*\shell\RepoFixer";                    Cmd = $CmdFile   }
        )

        $HKCR = [Microsoft.Win32.Registry]::ClassesRoot

        foreach ($Entry in $RegEntries) {
            Write-Host "  Registry: HKCR\$($Entry.Hive)" -ForegroundColor DarkCyan
            try {
                # Takarítás: töröljük a régi kulcsot ha létezik
                try { $HKCR.DeleteSubKeyTree($Entry.Hive, $false) } catch {}

                # Új kulcs létrehozása
                $Key = $HKCR.CreateSubKey($Entry.Hive)
                $Key.SetValue("", $MenuLabel)
                $Key.SetValue("Icon", $MenuIcon)
                $Key.Close()

                $CmdKey = $HKCR.CreateSubKey("$($Entry.Hive)\command")
                $CmdKey.SetValue("", $Entry.Cmd)
                $CmdKey.Close()

                Write-Host "  OK" -ForegroundColor Green
            }
            catch {
                Write-Host "  HIBA: $_" -ForegroundColor Red
            }
        }

        Write-Host "Telepites sikeres! Jobb klikknel megjelenik: 'Fajlok feloldasa (RepoFixer)'" -ForegroundColor Green
        Write-Host "Nyomjon meg egy gombot a kilepeshez..."
        Pause
        exit
    }
}

# --- 2. FELOLDÁS (a tényleges munka) ---
$Target = (Get-Location).Path

# --- BIZTONSÁGI TILTÓLISTA ---
$BlockedPaths = @(
    $env:SystemRoot,
    "$env:SystemRoot\System32",
    "$env:SystemRoot\SysWOW64",
    "$env:SystemRoot\WinSxS",
    "$env:SystemRoot\Scripts",
    $env:ProgramFiles,
    ${env:ProgramFiles(x86)},
    $env:ProgramData,
    [System.Environment]::GetFolderPath("System"),
    [System.Environment]::GetFolderPath("Windows")
)

foreach ($Blocked in $BlockedPaths) {
    if (-not $Blocked) { continue }
    if ($Target.TrimEnd('\') -eq $Blocked.TrimEnd('\') -or
        $Target.StartsWith($Blocked.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Host ""
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
        Write-Host "  VEDETT RENDSZERMAPPA - MUVELET MEGTAGADVA  " -ForegroundColor Red
        Write-Host "  Cel : $Target"                               -ForegroundColor Red
        Write-Host "  Ok  : tiltott teruleten belul: $Blocked"     -ForegroundColor Red
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Nyomjon meg egy gombot a kilepeshez..." -ForegroundColor Yellow
        Pause
        exit 1
    }
}

Write-Host ""
Write-Host "--- RepoFixer AKTIV ---" -ForegroundColor Cyan
Write-Host "  Cel: $Target"          -ForegroundColor White
Write-Host ""

$Files     = Get-ChildItem -Path $Target -Recurse -File -ErrorAction SilentlyContinue
$Total     = $Files.Count
$Counter   = 0
$Unblocked = 0
$Skipped   = 0

foreach ($File in $Files) {
    $Counter++
    $Percent = [int](($Counter / [Math]::Max($Total, 1)) * 100)
    Write-Progress -Activity "Fajlok feloldasa..." -Status "$Counter / $Total - $($File.Name)" -PercentComplete $Percent

    try {
        Unblock-File -Path $File.FullName -ErrorAction Stop
        $Unblocked++
    }
    catch {
        Write-Host "  [SKIP] $($File.FullName): $_" -ForegroundColor DarkYellow
        $Skipped++
    }
}

Write-Progress -Activity "Fajlok feloldasa..." -Completed

Write-Host ""
Write-Host "KESZ!" -ForegroundColor Green
Write-Host "  Feloldva   : $Unblocked fajl"
Write-Host "  Athugralva : $Skipped fajl (hozzaferes megtagadva vagy mar feloldva)"
Write-Host ""
Write-Host "Nyomjon meg egy gombot a bezarashoz..." -ForegroundColor Yellow
Pause
