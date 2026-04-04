# GitHub API-val lekéri az utolsó commit dátumát, összehasonlítja a tárolt dátummal
$modules = Get-Content modules.json | ConvertFrom-Json
$updates = @()

foreach ($mod in $modules) {
    $apiUrl = "https://api.github.com/repos/$($mod.repo)/commits?per_page=1"
    $response = Invoke-RestMethod -Uri $apiUrl -Headers @{
        "User-Agent" = "RTS-UpdateChecker"
    }
    $latestDate = $response[0].commit.author.date.Substring(0,10)
    
    if ($latestDate -gt $mod.last_commit_date) {
        $updates += @{
            name = $mod.name
            old_date = $mod.last_commit_date
            new_date = $latestDate
        }
    }
}

# Elmenti az updates.json-ba, amit a GUI megjelenít
$updates | ConvertTo-Json | Set-Content updates.json
Write-Host "Frissítés ellenőrzése kész. Találatok: $($updates.Count)"
