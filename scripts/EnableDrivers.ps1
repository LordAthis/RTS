# 1. Eszköztelepítési beállítások engedélyezése (SearchOrderConfig: 1 = Igen)
$Path1 = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching"
if (-not (Test-Path $Path1)) { New-Item -Path $Path1 -Force }
Set-ItemProperty -Path $Path1 -Name "SearchOrderConfig" -Value 1

# 2. Windows Update driver-kizárás feloldása (0 = Engedélyezve)
$Path2 = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"
if (-not (Test-Path $Path2)) { New-Item -Path $Path2 -Force }
Set-ItemProperty -Path $Path2 -Name "ExcludeWUDriversInQualityUpdate" -Value 0

# 3. Eszköz metaadatok letöltésének engedélyezése (0 = Engedélyezve)
$Path3 = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata"
if (-not (Test-Path $Path3)) { New-Item -Path $Path3 -Force }
Set-ItemProperty -Path $Path3 -Name "PreventDeviceMetadataFromNetwork" -Value 0

# Frissítés kényszerítése
gpupdate /force
Write-Host "Beállítások sikeresen módosítva. Indítsd újra a gépet!" -ForegroundColor Green
