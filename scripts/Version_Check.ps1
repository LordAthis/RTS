$config = Get-Content "Compatibility.json" | ConvertFrom-Json
$currentVer = $PSVersionTable.PSVersion.Major

Write-Host "Aktualis PS verzio: $currentVer"

if ($currentVer -lt 5) {
    Write-Host "Frissites szukseges: $($config.PS_Versions.'2.0'.Target)" -ForegroundColor Yellow
    $url = $config.PS_Versions.'2.0'.Update_URL
    Start-Process $url
} else {
    Write-Host "A rendszer naprakesz." -ForegroundColor Green
}
