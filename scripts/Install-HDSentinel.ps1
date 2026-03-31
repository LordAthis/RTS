# Útvonalak meghatározása
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
$appsFolder = Join-Path $scriptPath "..\Apps"
$tmpFolder = Join-Path $scriptPath "..\TMP"
$configFile = Join-Path $tmpFolder "hds_config.tmp"
$installDir = "C:\Program Files (x86)\Hard Disk Sentinel"
$localZip = Join-Path $appsFolder "hdsentinel_pro_setup.zip"

# 1. Konfiguráció beolvasása az RTS által létrehozott fájlból
if (Test-Path $configFile) {
    $configRaw = Get-Content $configFile -Raw
    $parts = $configRaw.Split('|')
    $pcIdentifier = $parts[0].Trim()      # Pl: "Kovacs_Janos_PC"
    $enableEmail = $parts[1].Trim()       # "1" vagy "0"
} else {
    # Alapértelmezett, ha nincs fájl (biztonsági tartalék)
    $pcIdentifier = "Ismeretlen_Gep"
    $enableEmail = "0"
}

# 2. Telepítő letöltése/ellenőrzése (Hibrid mód)
if (!(Test-Path $appsFolder)) { New-Item -ItemType Directory -Path $appsFolder | Out-Null }
try {
    if (Test-Connection -ComputerName 8.8.8.8 -Count 1 -Quiet -ErrorAction SilentlyContinue) {
        Invoke-WebRequest -Uri "https://harddisksentinel.com" -OutFile $localZip -TimeoutSec 30
    }
} catch { Write-Host "Offline mód..." }

# 3. Telepítés
if (Test-Path $localZip) {
    $tempExtract = Join-Path $appsFolder "HDS_Temp"
    Expand-Archive -Path $localZip -DestinationPath $tempExtract -Force
    $setupExe = Get-ChildItem "$tempExtract\*.exe" | Select-Object -First 1
    Start-Process -FilePath $setupExe.FullName -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES" -Wait

    # 4. HDSentinel.ini testreszabása
    # Az e-mail tárgymezőjébe vagy az üzenetbe bekerül az azonosító!
    $iniPath = Join-Path $installDir "HDSentinel.ini"
    $configContent = @"
[Settings]
ServiceMode=1
AutoUpdate=1
EmailEnabled=$enableEmail
EmailAddress=nexusszerviz@gmail.com
EmailSubject=HDS Alert - $pcIdentifier
AlertOnlyCritical=1
"@
    Set-Content -Path $iniPath -Value $configContent -Encoding UTF8

    # 5. Indítás és Riport küldése
    $hdsExe = Join-Path $installDir "HDSentinel.exe"
    Start-Process -FilePath $hdsExe -ArgumentList "-r" -WindowStyle Hidden
    
    # Ha kértünk e-mailt, küldünk egy azonnali állapotjelentést
    if ($enableEmail -eq "1") {
        Write-Host "Kezdeti állapotjelentés küldése: $pcIdentifier"
        # A HDS parancssori kapcsolója egy riport azonnali elküldéséhez:
        Start-Process -FilePath $hdsExe -ArgumentList "-REPORT", "-EMAIL" -WindowStyle Hidden
    }

    # Takarítás
    Remove-Item $tempExtract -Recurse -Force
}
