<#
.SYNOPSIS
    Letiltja az energiatakarékossági kikapcsolást a Bluetooth és WiFi eszközöknél.
    Automatikusan rendszergazdai jogot kér és megkerüli az Execution Policy-t.
#>

# 1. Önmagát rendszergazdaként és Bypass módban újraindító blokk
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Rendszergazdai jogosultság kérése..." -ForegroundColor Yellow
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Write-Host "--- RTS Karbantartó: Energiagazdálkodás optimalizálása ---" -ForegroundColor Cyan

# 2. Eszközök keresése (Bluetooth + WiFi / Hálózati kártyák)
$devices = Get-PnpDevice -Class Bluetooth, Net | Where-Object { 
    $_.FriendlyName -match "Atheros" -or 
    $_.FriendlyName -match "Wireless" -or 
    $_.FriendlyName -match "Wi-Fi" -or
    $_.FriendlyName -match "Adapter"
}

if ($null -eq $devices) {
    Write-Warning "Nem található releváns eszköz."
    pause
    exit
}

foreach ($dev in $devices) {
    $instanceId = $dev.InstanceId
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Enum\$instanceId\Device Parameters"

    Write-Host "Módosítás: $($dev.FriendlyName)" -ForegroundColor White

    try {
        if (!(Test-Path $regPath)) {
            New-Item -Path $regPath -Force -ErrorAction SilentlyContinue | Out-Null
        }

        # PnPCapabilities = 24 (0x18 hex) -> Megakadályozza a Windows általi leállítást
        Set-ItemProperty -Path $regPath -Name "PnPCapabilities" -Value 24 -Type DWord -ErrorAction Stop
        Write-Host "  [OK] Energiatakarékosság letiltva." -ForegroundColor Green
    }
    catch {
        Write-Host "  [HIBA] Nem sikerült módosítani: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`nKész! A módosítások érvénybe lépéséhez javasolt egy újraindítás." -ForegroundColor Cyan
Start-Sleep -Seconds 3
