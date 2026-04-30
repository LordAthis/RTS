<#
.SYNOPSIS
    RepoFixer v4.0 - Windows letöltési zárolás feloldó, jobb klikkes telepítéssel
#>

param([string]$StartDir)

# --- ADMIN AUTO-RELAUNCH ---
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $CurrentDir = if ($StartDir) { $StartDir } else { (Get-Location).Path }
    $Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -StartDir `"$CurrentDir`""
    Start-Process powershell.exe -ArgumentList $Arguments -Verb RunAs
    exit
}

# Beállítjuk a munkakönyvtárat
if ($StartDir) { Set-Location $StartDir }

$ScriptName  = "RepoFixer.ps1"
$TargetDir   = "$env:SystemRoot\Scripts"
$FinalPath   = Join-Path $TargetDir $ScriptName

# --- 1. TELEPÍTÉSI LOGIKA ---
# Ha a script nem a végleges helyéről fut, felajánlja a telepítést
$CurrentLocation = $PSCommandPath
if (-not ($CurrentLocation.StartsWith($TargetDir, [System.StringComparison]::OrdinalIgnoreCase))) {

    Write-Host "--- TELEPITO MOD ---" -ForegroundColor Yellow
    $Choice = Read-Host "Telepited/Frissited a scriptet a rendszerbe? (i/n)"

    if ($Choice -ieq 'i') {

        # Célmappa létrehozása, ha még nem létezik
        if (-not (Test-Path $TargetDir)) {
            New-Item -Path $TargetDir -ItemType Directory -Force | Out-Null
        }

        # Script másolása
        Copy-Item -Path $CurrentLocation -Destination $FinalPath -Force
        Write-Host "Script masolva: $FinalPath" -ForegroundColor Cyan

        # Registry bejegyzések:
        #   Directory\shell            -> jobb klikk MAPPÁN
        #   Directory\Background\shell -> jobb klikk a mappa HÁTTERÉN (benne állva)
        #   *\shell                    -> jobb klikk FÁJLON
        #
        # FONTOS: a HKCR\*\shell kulcsot a PowerShell New-Item befagyasztja,
        # ezért arra reg.exe-t használunk közvetlenül.

        # --- 2a. Mappa + Háttér (PowerShell API) ---
        $PsEntries = @(
            @{ Path = "Registry::HKEY_CLASSES_ROOT\Directory\shell\RepoFixer";            Param = '"%1"' },
            @{ Path = "Registry::HKEY_CLASSES_ROOT\Directory\Background\shell\RepoFixer"; Param = '"%V"' }
        )

        foreach ($Entry in $PsEntries) {
            $ShellPath = $Entry.Path
            $CmdPath   = "$ShellPath\command"
            $CmdValue  = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$FinalPath`" -StartDir $($Entry.Param)"

            Write-Host "  Registry: $ShellPath" -ForegroundColor DarkCyan
            try {
                if (-not (Test-Path $ShellPath)) { New-Item -Path $ShellPath -Force | Out-Null }
                Set-ItemProperty -Path $ShellPath -Name "(Default)" -Value "Fajlok feloldasa (RepoFixer)" -Force
                Set-ItemProperty -Path $ShellPath -Name "Icon"      -Value "powershell.exe,0"             -Force

                if (-not (Test-Path $CmdPath)) { New-Item -Path $CmdPath -Force | Out-Null }
                Set-ItemProperty -Path $CmdPath -Name "(Default)" -Value $CmdValue -Force

                Write-Host "  OK" -ForegroundColor Green
            }
            catch {
                Write-Host "  HIBA: $_" -ForegroundColor Red
            }
        }

        # --- 2b. Fájl (reg.exe – a HKCR\* kulcs PS alatt lefagy!) ---
        Write-Host "  Registry: HKCR\*\shell\RepoFixer (reg.exe)" -ForegroundColor DarkCyan
        try {
            $CmdValue = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$FinalPath`" -StartDir `"%1`""
            & reg.exe add "HKCR\*\shell\RepoFixer"          /ve /d "Fajlok feloldasa (RepoFixer)" /f | Out-Null
            & reg.exe add "HKCR\*\shell\RepoFixer"          /v "Icon" /d "powershell.exe,0"       /f | Out-Null
            & reg.exe add "HKCR\*\shell\RepoFixer\command"  /ve /d $CmdValue                      /f | Out-Null
            Write-Host "  OK" -ForegroundColor Green
        }
        catch {
            Write-Host "  HIBA: $_" -ForegroundColor Red
        }

        Write-Host "Telepites sikeres! Jobb klikknel megjelenik: 'Fajlok feloldasa (RepoFixer)'" -ForegroundColor Green
        Write-Host "Nyomjon meg egy gombot a kilepeshez..."
        Pause
        exit
    }
}

# --- 2. FELOLDÁS (a tényleges munka) ---
$Target = (Get-Location).Path
Write-Host ""
Write-Host "--- RepoFixer AKTIV ---" -ForegroundColor Cyan
Write-Host "Helyszin: $Target"       -ForegroundColor Gray
Write-Host ""

$Files   = Get-ChildItem -Path $Target -Recurse -File -ErrorAction SilentlyContinue
$Total   = $Files.Count
$Counter = 0
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
Write-Host "  Feloldva : $Unblocked fajl"
Write-Host "  Athugralva: $Skipped fajl (hozzaferes megtagadva vagy mar feloldva)"
Write-Host ""
Write-Host "Nyomjon meg egy gombot a bezarashoz..." -ForegroundColor Yellow
Pause
