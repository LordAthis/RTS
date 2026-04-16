# 1. Minden fájl feloldása a jelenlegi mappában és almappákban
Write-Host "Fájlok feloldása folyamatban..." -ForegroundColor Cyan
Get-ChildItem -Recurse | Unblock-File

# 2. ZIP fájlok keresése és kicsomagolása
$zips = Get-ChildItem -Filter *.zip

foreach ($zip in $zips) {
    $targetDir = Join-Path $PSScriptRoot $zip.BaseName
    
    Write-Host "Kicsomagolás: $($zip.Name)..." -ForegroundColor Yellow
    
    # Kicsomagolás egy ideiglenes helyre a dupla mappák elkerülése végett
    Expand-Archive -Path $zip.FullName -DestinationPath $targetDir -Force
    
    # Dupla mappa ellenőrzése: ha csak egyetlen mappa van a célban, és az ugyanaz mint a ZIP neve
    $content = Get-ChildItem -Path $targetDir
    if ($content.Count -eq 1 -and $content.PSIsContainer) {
        $innerDir = $content.FullName
        $tempDir = "$targetDir" + "_temp"
        
        # Tartalom mozgatása felfelé
        Move-Item -Path "$innerDir\*" -Destination $targetDir -Force
        Remove-Item -Path $innerDir -Recurse -Force
    }
}

Write-Host "Kész! Minden fájl feloldva és kicsomagolva." -ForegroundColor Green
pause
