# 1. Célmappa létrehozása és Jogosultság (SYSTEM-nek is)
$tempPath = "C:\Temp"
if (!(Test-Path $tempPath)) {
    New-Item -ItemType Directory -Path $tempPath -Force
}

# Jogosultság biztosítása a Rendszer (SYSTEM) és a Felhasználók számára
$acl = Get-Acl $tempPath
$rules = @(
    New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"),
    New-Object System.Security.AccessControl.FileSystemAccessRule("Users", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
)
foreach ($rule in $rules) { $acl.SetAccessRule($rule) }
Set-Acl $tempPath $acl

# 2. Régi elérési utak kimentése (hogy tudjuk, mit kell törölni)
$oldUserTemp = [Environment]::GetEnvironmentVariable("TEMP", "User")
$oldSystemTemp = [Environment]::GetEnvironmentVariable("TEMP", "Machine")

# 3. ÚJ ÚTVONALAK ALKALMAZÁSA (Azonnal él)
[Environment]::SetEnvironmentVariable("TEMP", $tempPath, "Machine")
[Environment]::SetEnvironmentVariable("TMP", $tempPath, "Machine")
[Environment]::SetEnvironmentVariable("TEMP", $tempPath, "User")
[Environment]::SetEnvironmentVariable("TMP", $tempPath, "User")

Write-Host "Környezeti változók átirányítva ide: $tempPath" -ForegroundColor Green

# 4. TISZTÍTÓ FUNKCIÓ (Opcionális/Választható)
$cleanOld = Read-Host "Szeretnéd törölni a régi TEMP mappák tartalmát? (I/N)"
if ($cleanOld -eq "I") {
    $targets = @($oldUserTemp, $oldSystemTemp, "C:\Windows\Temp") | Select-Object -Unique
    
    foreach ($folder in $targets) {
        if (Test-Path $folder) {
            Write-Host "Takarítás: $folder" -ForegroundColor Cyan
            # Csak a tartalmat töröljük, a mappát nem feltétlenül (Windows stabilitás miatt)
            Get-ChildItem -Path $folder -Recurse -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "A nem használt fájlok törölve. (Ami használatban van, az maradt.)" -ForegroundColor Yellow
}
