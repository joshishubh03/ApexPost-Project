<#
.SYNOPSIS
    Proves that tests can fail: runs them on the real code, breaks one piece of production code on purpose, runs
    them again (they must fail), then restores the file byte for byte and runs them a third time.

.DESCRIPTION
    Used for the Copilot evidence (COPILOT-LOG.md): a generated test is only meaningful if it fails when the
    behaviour it claims to check is broken. The log is written to docs/test-results/<Name>.txt.
    The production file is always restored, even if a run is interrupted.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/prove-tests-fail.ps1 `
        -Name "mutation-page-size" `
        -File "ApexPost.Application/Courier/CourierShipmentService.cs" `
        -Find "ThrowIfGreaterThan(query.PageSize, CourierShipmentQuery.MaximumPageSize" `
        -Replace "ThrowIfGreaterThan(query.PageSize, CourierShipmentQuery.MaximumPageSize + 1" `
        -Project "ApexPost.UnitTests" `
        -Filter "FullyQualifiedName~CourierShipmentServiceCopilotTests"
#>
param(
    [Parameter(Mandatory = $true)] [string] $File,
    [Parameter(Mandatory = $true)] [string] $Find,
    [Parameter(Mandatory = $true)] [string] $Replace,
    [Parameter(Mandatory = $true)] [string] $Project,
    [Parameter(Mandatory = $true)] [string] $Filter,
    [string] $Name = "mutation"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$path = Join-Path $root $File
$projectPath = Join-Path $root $Project
$originalBytes = [System.IO.File]::ReadAllBytes($path)
$originalText = [System.IO.File]::ReadAllText($path)

if (-not $originalText.Contains($Find)) {
    throw "The text to change was not found in $File. Nothing was modified."
}

$outDir = Join-Path $root "docs/test-results"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$logPath = Join-Path $outDir "$Name.txt"

function Invoke-Tests([string] $label) {
    Write-Host "== $label" -ForegroundColor Cyan
    $output = & dotnet test $projectPath --filter $Filter --nologo 2>&1 | Out-String
    $exit = $LASTEXITCODE
    $summary = ($output -split "`r?`n" | Where-Object { $_ -match "Passed!|Failed!|Failed |Total tests|error " }) -join "`n"
    Write-Host $summary
    return [pscustomobject]@{ Label = $label; Exit = $exit; Output = $output; Summary = $summary }
}

$runs = @()
try {
    $runs += Invoke-Tests "1. Real code (tests must pass)"

    $mutated = $originalText.Replace($Find, $Replace)
    [System.IO.File]::WriteAllText($path, $mutated, (New-Object System.Text.UTF8Encoding($false)))
    $runs += Invoke-Tests "2. Broken code (tests must fail)"
}
finally {
    [System.IO.File]::WriteAllBytes($path, $originalBytes)
    Write-Host "Restored $File" -ForegroundColor Green
}

$runs += Invoke-Tests "3. Restored code (tests must pass again)"

$proven = ($runs[0].Exit -eq 0) -and ($runs[1].Exit -ne 0) -and ($runs[2].Exit -eq 0)
$verdict = if ($proven) { "PROVEN: the tests fail when the code is broken and pass on the real code." } `
           else { "NOT PROVEN: check the three runs below." }

$log = @(
    "Mutation: $Name"
    "Date (UTC): $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm'))"
    "File: $File"
    "Changed: $Find"
    "To:      $Replace"
    "Tests: $Project --filter `"$Filter`""
    ""
    $verdict
    ""
)
foreach ($run in $runs) {
    $log += "---- $($run.Label) (exit code $($run.Exit))"
    $log += $run.Summary
    $log += ""
}
$log += "==== Full output of run 2 (broken code)"
$log += $runs[1].Output
$log | Set-Content -Path $logPath -Encoding UTF8

Write-Host $verdict -ForegroundColor ($(if ($proven) { "Green" } else { "Red" }))
Write-Host "Log: $logPath"
if (-not $proven) { exit 1 }
