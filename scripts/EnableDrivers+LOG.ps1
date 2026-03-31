# Naplófájl útvonala az asztalon
$LogFile = "$env:USERPROFILE\Desktop\Log_DriverFix.txt"
"--- Szkript indítása: $(Get-Date) ---" | Out-File $LogFile

function Log-Write {
    param([string]$Message, [string]$Color = "White")
    $Timestamp = Get-Date -Format "HH:mm:ss"
    "$Timestamp : $Message" | Out-File $LogFile -Append
    Write-Host $Message -ForegroundColor $Color
}

try {
    Log-Write "Jogosultságok ellenőrzése..."
    if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Log-Write "HIBA: Nem rendszergazdaként futtatod! Kérlek, jobb klikk -> Futtatás rendszergazdaként." "Red"
        return
    }

    # 1. Driver keresés engedélyezése
    Log-Write "DriverSearching beállítása..."
    $Path1 = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching"
    if (-not (Test-Path $Path1)) { New-Item -Path $Path1 -Force | Out-Null }
    Set-ItemProperty -Path $Path1 -Name "SearchOrderConfig" -Value 1 -ErrorAction Stop
    Log-Write "Sikeres: SearchOrderConfig = 1" "Green"

    # 2. Windows Update driver-kizárás feloldása
    Log-Write "Windows Update driver-kizárás feloldása..."
    $Path2 = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"
    if (-not (Test-Path $Path2)) { New-Item -Path $Path2 -Force | Out-Null }
    Set-ItemProperty -Path $Path2 -Name "ExcludeWUDriversInQualityUpdate" -Value 0 -ErrorAction Stop
    Log-Write "Sikeres: ExcludeWUDriversInQualityUpdate = 0" "Green"

    # 3. GPO kényszerítés
    Log-Write "Házirendek frissítése (gpupdate)..."
    gpupdate /force | Out-Null
    Log-Write "Házirend frissítve." "Green"

} catch {
    Log-Write "KRITIKUS HIBA: $($_.Exception.Message)" "Red"
}

Log-Write "--- Folyamat vége ---"
