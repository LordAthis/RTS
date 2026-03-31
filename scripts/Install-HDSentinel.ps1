# Relatív útvonal meghatározása az RTS projektstruktúrához
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
$appsFolder = Join-Path $scriptPath "..\Apps" 
$localZip = Join-Path $appsFolder "hdsentinel_pro_setup.zip"

# Beállítások
$appName = "Hard Disk Sentinel"
$installDir = "C:\Program Files (x86)\Hard Disk Sentinel"
$url = "https://harddisksentinel.com"
$email = "nexusszerviz@gmail.com"

# 1. Apps mappa biztosítása
if (!(Test-Path $appsFolder)) { New-Item -ItemType Directory -Path $appsFolder | Out-Null }

# 2. Letöltés vagy Offline mód (RTS /Apps mappából)
Write-Host "Forrás ellenőrzése..." -ForegroundColor Cyan
try {
    $hasInternet = Test-Connection -ComputerName 8.8.8.8 -Count 1 -Quiet -ErrorAction SilentlyContinue
    if ($hasInternet) {
        Write-Host "Letöltés folyamatban a hivatalos oldalról..."
        Invoke-WebRequest -Uri $url -OutFile $localZip -TimeoutSec 30
    } elseif (Test-Path $localZip) {
        Write-Host "Offline mód: Relatív /Apps mappa használata." -ForegroundColor Yellow
    } else {
        throw "Nincs internet és hiányzik a ZIP az /Apps mappából!"
    }
} catch {
    if (Test-Path $localZip) {
        Write-Host "Hiba a letöltéskor, de van helyi fájl. Folytatás..." -ForegroundColor Yellow
    } else {
        Write-Error "Kritikus hiba: A telepítő nem elérhető sehol!"
        exit
    }
}

# 3. Kicsomagolás (ideiglenes mappába az Apps-en belül)
$tempExtract = Join-Path $appsFolder "HDS_Temp"
Expand-Archive -Path $localZip -DestinationPath $tempExtract -Force

# 4. Silent Telepítés
Write-Host "Telepítés/Frissítés folyamatban..." -ForegroundColor Cyan
$setupExe = Get-ChildItem "$tempExtract\*.exe" | Select-Object -First 1
Start-Process -FilePath $setupExe.FullName -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS" -Wait

# 5. Konfiguráció (INI injektálás)
$iniPath = Join-Path $installDir "HDSentinel.ini"
$configContent = @"
[Settings]
ServiceMode=1
AutoUpdate=1
EmailEnabled=1
EmailAddress=$email
AlertOnlyCritical=1
"@
Set-Content -Path $iniPath -Value $configContent -Encoding UTF8

# 6. Szolgáltatás indítása
Write-Host "Szolgáltatás indítása..." -ForegroundColor Cyan
try {
    # Megpróbáljuk elindítani a szervizt
    Start-Service -Name "Hard Disk Sentinel" -ErrorAction SilentlyContinue
    # Biztonsági mentés: Ha nem szervizként futna, indítjuk az exe-t háttérben
    Start-Process -FilePath (Join-Path $installDir "HDSentinel.exe") -ArgumentList "-r" -WindowStyle Hidden
} catch {
    Write-Host "A szerviz indítása sikertelen, de az alkalmazás konfigurálva van." -ForegroundColor Red
}

# 7. Takarítás
Remove-Item $tempExtract -Recurse -Force

Write-Host "Kész! HDSentinel telepítve, konfigurálva és fut." -ForegroundColor Green
