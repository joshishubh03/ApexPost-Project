<#
.SYNOPSIS
    Runs the three test projects (unit, integration, BDD), saves TRX results and a full log in docs/test-results,
    and writes the passed / failed / skipped summary into README.md (between the test-results markers).

.PARAMETER Coverage
    Also collect line coverage with coverlet (XPlat Code Coverage) and add the percentage per project.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/run-tests.ps1
    powershell -ExecutionPolicy Bypass -File scripts/run-tests.ps1 -Coverage
#>
param([switch] $Coverage)

$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot -Parent
$resultsDir = Join-Path $root "docs/test-results"
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

$projects = @("ApexPost.UnitTests", "ApexPost.IntegrationTests", "ApexPost.Specs")
$summaryPattern = '(?<outcome>Passed|Failed)!\s+-\s+Failed:\s+(?<failed>\d+),\s+Passed:\s+(?<passed>\d+),\s+Skipped:\s+(?<skipped>\d+),\s+Total:\s+(?<total>\d+),\s+Duration:\s+(?<duration>[^-]+?)\s+-'

$rows = @()
$fullLog = @()
$exitCode = 0

foreach ($project in $projects) {
    Write-Host "== $project" -ForegroundColor Cyan
    $projectResults = Join-Path $resultsDir $project
    if (Test-Path $projectResults) { Remove-Item -Recurse -Force $projectResults }

    $arguments = @("test", (Join-Path $root $project), "--nologo",
        "--logger", "trx;LogFileName=$project.trx", "--results-directory", $projectResults)
    if ($Coverage) { $arguments += @("--collect", "XPlat Code Coverage") }

    $output = & dotnet @arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { $exitCode = 1 }
    $fullLog += "==== $project", $output

    $match = [regex]::Match($output, $summaryPattern)
    $row = [ordered]@{
        Project = $project
        Passed = if ($match.Success) { [int]$match.Groups["passed"].Value } else { "?" }
        Failed = if ($match.Success) { [int]$match.Groups["failed"].Value } else { "?" }
        Skipped = if ($match.Success) { [int]$match.Groups["skipped"].Value } else { "?" }
        Total = if ($match.Success) { [int]$match.Groups["total"].Value } else { "?" }
        Duration = if ($match.Success) { $match.Groups["duration"].Value.Trim() } else { "build or run error, see log" }
        Coverage = ""
    }

    if ($Coverage) {
        $cobertura = Get-ChildItem -Path $projectResults -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($cobertura) {
            [xml]$xml = Get-Content $cobertura.FullName
            $row.Coverage = "{0:P1}" -f [double]$xml.coverage.'line-rate'
        }
    }

    $rows += [pscustomobject]$row
    Write-Host ("   passed {0}, failed {1}, skipped {2}" -f $row.Passed, $row.Failed, $row.Skipped)
}

$fullLog | Set-Content -Path (Join-Path $resultsDir "test-output.txt") -Encoding UTF8

$numeric = $rows | Where-Object { $_.Total -is [int] }
$totalPassed = ($numeric | Measure-Object -Property Passed -Sum).Sum
$totalFailed = ($numeric | Measure-Object -Property Failed -Sum).Sum
$totalSkipped = ($numeric | Measure-Object -Property Skipped -Sum).Sum
$total = ($numeric | Measure-Object -Property Total -Sum).Sum

$coverageHeader = if ($Coverage) { " Line coverage |" } else { "" }
$coverageRule = if ($Coverage) { "---|" } else { "" }
$table = @(
    "Last run: $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm')) UTC on $env:COMPUTERNAME, ``scripts/run-tests.ps1``. Full log: [docs/test-results/test-output.txt](docs/test-results/test-output.txt).",
    "",
    "| Project | Passed | Failed | Skipped | Total | Duration |$coverageHeader",
    "|---|---|---|---|---|---|$coverageRule"
)
foreach ($row in $rows) {
    $coverageCell = if ($Coverage) { " $($row.Coverage) |" } else { "" }
    $table += "| $($row.Project) | $($row.Passed) | $($row.Failed) | $($row.Skipped) | $($row.Total) | $($row.Duration) |$coverageCell"
}
$table += "| **All** | **$totalPassed** | **$totalFailed** | **$totalSkipped** | **$total** | |$(if ($Coverage) { ' |' })"

$readmePath = Join-Path $root "README.md"
$readme = Get-Content $readmePath -Raw
$start = "<!-- test-results:start -->"
$end = "<!-- test-results:end -->"
if ($readme.Contains($start) -and $readme.Contains($end)) {
    $before = $readme.Substring(0, $readme.IndexOf($start) + $start.Length)
    $after = $readme.Substring($readme.IndexOf($end))
    $newReadme = $before + "`n" + ($table -join "`n") + "`n" + $after
    [System.IO.File]::WriteAllText($readmePath, $newReadme, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "README.md test results updated." -ForegroundColor Green
}

Write-Host ""
Write-Host ($table -join "`n")
exit $exitCode
