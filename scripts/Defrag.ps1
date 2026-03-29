Write-Host "Meghajtók optimalizálása folyamatban (SSD: Retrim / HDD: Defrag)..." -ForegroundColor Yellow

foreach ($drive in $driveMapping.Keys) {
    if (Test-Path $drive) {
        Write-Host "$drive optimalizálása..." -NoNewline
        Optimize-Volume -DriveLetter $drive[0] -ReTrim -Defrag -Verbose
        Write-Host " Kész." -ForegroundColor Green
    }
}
