$registryPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
$name = "ClearPageFileAtShutdown"
$value = 1

if (!(Test-Path $registryPath)) {
    New-Item -Path $registryPath -Force
}
Set-ItemProperty -Path $registryPath -Name $name -Value $value -Type DWord
Write-Host "Leállításkori lapozófájl ürítés aktiválva." -ForegroundColor Cyan
