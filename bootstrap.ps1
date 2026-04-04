# Beolvassa a modules.json-t és minden modult git clone-ozza az Apps/ mappába
$modules = Get-Content modules.json | ConvertFrom-Json
foreach ($mod in $modules) {
    $target = "Apps\$($mod.name)"
    if (-not (Test-Path $target)) {
        git clone "https://github.com/$($mod.repo)" $target
        Write-Host "Letöltve: $($mod.name)"
    }
}
