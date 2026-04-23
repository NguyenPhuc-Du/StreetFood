param(
    [string]$BaseUrl = "https://localhost:7236",
    [string]$PoiId = "1",
    [string]$Scenario = "mixed",
    [string]$OutputDir = "tests/nfr/results"
)

$ErrorActionPreference = "Stop"

function Get-ScenarioFile([string]$name) {
    switch ($name.ToLowerInvariant()) {
        "read"  { return "tests/nfr/streetfood-read-load.js" }
        "write" { return "tests/nfr/streetfood-write-load.js" }
        default { return "tests/nfr/streetfood-mixed-load.js" }
    }
}

$scenarioFile = Get-ScenarioFile $Scenario
$levels = @("50", "100", "200")
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "StreetFood capacity run started"
Write-Host "BaseUrl  : $BaseUrl"
Write-Host "Scenario : $Scenario ($scenarioFile)"
Write-Host "POI_ID   : $PoiId"
Write-Host "Output   : $OutputDir"
Write-Host ""

foreach ($level in $levels) {
    $outFile = Join-Path $OutputDir "capacity-$Scenario-L$level-$timestamp.log"
    Write-Host "=== Running LOAD_LEVEL=$level ==="
    Write-Host "Log: $outFile"

    $cmd = "k6 run -e BASE_URL=$BaseUrl -e LOAD_LEVEL=$level -e POI_ID=$PoiId $scenarioFile"
    cmd /c $cmd *>&1 | Tee-Object -FilePath $outFile

    Write-Host "=== Done LOAD_LEVEL=$level ==="
    Write-Host ""
}

Write-Host "Capacity run complete."
Write-Host "Review logs in: $OutputDir"
