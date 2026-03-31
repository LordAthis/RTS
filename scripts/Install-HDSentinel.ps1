# Beállítások
$appName = "Hard Disk Sentinel"
$installDir = "C:\Program Files (x86)\Hard Disk Sentinel"
$appsFolder = "C:\Apps" # A projekted központi mappája
$localZip = Join-Path $appsFolder "hdsentinel_pro_setup.zip"
$url = "https://harddisksentinel.com"
$email = "nexusszerviz@gmail.com"

# 1. Mappa ellenőrzése
if (!(Test-Path $appsFolder)) { New-Item -ItemType Directory -Path $appsFolder | Out-Null }

# 2. Letöltés vagy Offline mód
Write-Host "Forrás ellenőrzése..." -ForegroundColor Cyan
$hasInternet = Test-Connection -ComputerName google.com -Count 1 -Quiet

if ($hasInternet) {
    Write-Host "Letöltés folyamatban..."
    Invoke-WebRequest -Uri $url -OutFile $localZip
} elseif (Test-Path $localZip) {
    Write-Host "Offline mód: Helyi fájl használata a(z) $appsFolder mappából." -ForegroundColor Yellow
} else {
    Write-Error "Nincs internet és nem található telepítő a $appsFolder mappában!"
    exit
}

# 3. Kicsomagolás
Expand-Archive -Path $localZip -DestinationPath "$appsFolder\HDS_Temp" -Force

# 4. Telepítés (Silent módban, szervizként)
Write-Host "Telepítés/Frissítés folyamatban..." -ForegroundColor Cyan
$setupExe = Get-ChildItem "$appsFolder\HDS_Temp\*.exe" | Select-Object -First 1
Start-Process -FilePath $setupExe.FullName -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS" -Wait

# 5. Konfiguráció (INI fájl injektálása az értesítésekhez)
# Megjegyzés: A szerviz mód és az e-mail beállítások az INI-ben tárolódnak
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

# 6. Takarítás
Remove-Item "$appsFolder\HDS_Temp" -Recurse -Force

Write-Host "A Hard Disk Sentinel telepítése és konfigurálása sikeres!" -ForegroundColor Green
