# 1. Automatikus kezelés kikapcsolása (hogy mi mondjuk meg, hol legyen)
$ComputerSystem = Get-CimInstance Win32_ComputerSystem
$ComputerSystem | Set-CimInstance -Property @{AutomaticManagedPagefile = $False}

# 2. Meglévő lapozófájl beállítások törlése
Get-CimInstance Win32_PageFileSetting | Remove-CimInstance

# 3. Új lapozófájl beállítása a D: (Swap) meghajtóra
# A 0-0 érték jelenti azt, hogy "System Managed" (Rendszer által kezelt)
$drive = "D:\"
New-CimInstance -ClassName Win32_PageFileSetting -Property @{
    Name = "${drive}pagefile.sys";
    InitialSize = 0;
    MaximumSize = 0
}

# 4. Leállításkori ürítés beállítása (Registry)
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
Set-ItemProperty -Path $regPath -Name "ClearPageFileAtShutdown" -Value 1 -Type DWord

Write-Host "Lapozófájl átirányítva a D: (Swap) partícióra (Rendszer kezelt méret)." -ForegroundColor Green
Write-Host "Leállításkori ürítés aktiválva." -ForegroundColor Cyan
Write-Host "A módosítások érvénybe lépéséhez ÚJRAINDÍTÁS szükséges!" -ForegroundColor Yellow
