# Meghajtók és Nevek társítása
$driveMapping = @{
    "C:" = "System"
    "D:" = "Swap"
    "E:" = "Container"
}

foreach ($drive in $driveMapping.Keys) {
    if (Test-Path $drive) {
        $currentLabel = (Get-Volume -DriveLetter $drive[0]).FileSystemLabel
        $targetLabel = $driveMapping[$drive]
        
        if ($currentLabel -ne $targetLabel) {
            Set-Volume -DriveLetter $drive[0] -NewFileSystemLabel $targetLabel
            Write-Host "$drive átnevezve: $currentLabel -> $targetLabel" -ForegroundColor Cyan
        }
    } else {
        Write-Warning "A(z) $drive meghajtó nem található, az elnevezés kihagyva."
    }
}
