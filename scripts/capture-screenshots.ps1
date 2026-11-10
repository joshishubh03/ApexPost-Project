<#
    Captures the Week 4 evidence images into docs/screenshots (fictional data only).

    Part A  Runs the real console harness against a fresh Couchbase Lite database and saves its output:
            a1-query-by-tracking-number.png, a2-pending-and-counts.png, a3-listener-log.png
    Part C  Starts ApexPost.Api (Development, https://localhost:7301), takes headless Microsoft Edge screenshots
            of Swagger UI and saves the real HTTP responses:
            c1-swagger-v2.png, c1-swagger-v1-deprecated.png, c2-400-validation.png, c3-409-conflict.png,
            c4-201-location.png

    Usage (repository root):  powershell -ExecutionPolicy Bypass -File scripts\capture-screenshots.ps1

    Needs: .NET 10 SDK, SQL Server LocalDB, Microsoft Edge. If ApexPost.Api has no User Secrets yet, the script sets
    a LocalDB connection string and a random development signing key (local development values only).
    Tokens are never printed or saved: the images show "Bearer <token>".
#>
# Native tools (dotnet, curl, Edge) write progress to stderr; failures are checked explicitly below.
$ErrorActionPreference = "Continue"
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
$outDir = Join-Path $repo "docs\screenshots"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Add-Type -AssemblyName System.Drawing

# ---------------------------------------------------------------------------------------------------------------
# Renders console text as a PNG (dark terminal style). Lines starting with "PS " are the command that was run.
# ---------------------------------------------------------------------------------------------------------------
function Save-ConsoleImage([string]$FileName, [string]$Title, [string[]]$Lines) {
    $font = New-Object System.Drawing.Font("Consolas", 15, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleFont = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $probe = New-Object System.Drawing.Bitmap 1, 1
    $g = [System.Drawing.Graphics]::FromImage($probe)
    $maxWidth = 900
    foreach ($line in $Lines) {
        $w = $g.MeasureString($line, $font).Width
        if ($w -gt $maxWidth) { $maxWidth = $w }
    }
    $g.Dispose(); $probe.Dispose()
    $lineHeight = [int][Math]::Ceiling($font.GetHeight() * 1.25)
    $width = [int][Math]::Min(2400, $maxWidth + 48)
    $height = [int](56 + $Lines.Count * $lineHeight + 24)

    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $g.Clear([System.Drawing.Color]::FromArgb(12, 12, 12))
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(36, 36, 40))), 0, 0, $width, 40)
    $g.DrawString($Title, $titleFont, [System.Drawing.Brushes]::Gainsboro, 16, 11)

    $normal = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(204, 204, 204))
    $command = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(97, 214, 214))
    $highlight = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(249, 241, 165))
    $good = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(22, 198, 12))
    $bad = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(231, 72, 86))
    $y = 56
    foreach ($line in $Lines) {
        $brush = $normal
        if ($line.StartsWith("PS ")) { $brush = $command }
        elseif ($line -match "\[listener\]|>> live pending") { $brush = $highlight }
        elseif ($line -match "^HTTP/\S+ 2\d\d") { $brush = $good }
        elseif ($line -match "^HTTP/\S+ [45]\d\d") { $brush = $bad }
        elseif ($line -match "^(Location|api-supported-versions|api-deprecated-versions):") { $brush = $highlight }
        $g.DrawString($line, $font, $brush, 20, $y)
        $y += $lineHeight
    }
    $path = Join-Path $outDir $FileName
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bitmap.Dispose()
    Write-Host "  saved docs\screenshots\$FileName" -ForegroundColor Green
}

Write-Host "Building..." -ForegroundColor Cyan
dotnet build ApexPost.Courier.Offline.Harness -v q -nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
dotnet build ApexPost.Api -v q -nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# ---------------------------------------------------------------------------------------------------------------
# Part A - console harness (fresh database so the results are reproducible)
# ---------------------------------------------------------------------------------------------------------------
Write-Host "Part A: running the courier harness..." -ForegroundColor Cyan
$dataDir = Join-Path $env:TEMP ("apexpost-evidence-" + [Guid]::NewGuid().ToString("N"))
$steps = @(
    @{ Key = "seed";    Inputs = @("9") },
    @{ Key = "find";    Inputs = @("2", "APX-10000001") },
    @{ Key = "pending"; Inputs = @("3") },
    @{ Key = "counts";  Inputs = @("7", "courier-007") },
    @{ Key = "add";     Inputs = @("1", "APX-10000004", "Pickup", "courier-007", "Oakfield Depot", "") },
    @{ Key = "sync";    Inputs = @("4") },
    @{ Key = "after";   Inputs = @("3") },
    @{ Key = "exit";    Inputs = @("6") }
)

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "run --no-build --project ApexPost.Courier.Offline.Harness -- --data `"$dataDir`""
$psi.WorkingDirectory = $repo
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
$process = [System.Diagnostics.Process]::Start($psi)
$outputTask = $process.StandardOutput.ReadToEndAsync()
Start-Sleep -Seconds 4
$allInputs = New-Object System.Collections.Generic.List[string]
foreach ($step in $steps) {
    foreach ($value in $step.Inputs) {
        $process.StandardInput.WriteLine($value)
        $allInputs.Add($value)
        Start-Sleep -Milliseconds 300
    }
    Start-Sleep -Milliseconds 1500   # lets the change listener log before the next step
}
$process.StandardInput.Close()
if (-not $process.WaitForExit(30000)) { $process.Kill(); throw "The harness did not exit." }
$transcript = $outputTask.Result -replace "`r", ""

# Redirected input is not echoed, so put each typed answer back after its prompt, in order.
$prompts = "Choose|Tracking number \(APX-12345678\)|Scan type \(Pickup/InTransit/Delivered/Failed\)|" +
           "Courier ID \(e\.g\. courier-007\)|Courier ID|Location|Notes \(optional, '#reject' makes the fake server reject it\)|" +
           "Recipient signature name|Scan ID \(scan::\.\.\.\)"
$builder = New-Object System.Text.StringBuilder
$cursor = 0
foreach ($value in $allInputs) {
    $match = [regex]::Match($transcript.Substring($cursor), "(?:$prompts): ")
    if (-not $match.Success) { break }
    $end = $cursor + $match.Index + $match.Length
    [void]$builder.Append($transcript.Substring($cursor, $end - $cursor)).Append($value).Append("`n")
    $cursor = $end
}
[void]$builder.Append($transcript.Substring($cursor))
$lines = $builder.ToString().TrimEnd() -split "`n"

# One block per "Choose:" answer (the block runs until the next menu).
$blocks = @{}
$intro = New-Object System.Collections.Generic.List[string]
$current = $null; $index = -1
foreach ($line in $lines) {
    if ($line -match "^Choose: ") {
        $index++
        $current = New-Object System.Collections.Generic.List[string]
        $blocks[$steps[$index].Key] = $current
    }
    if ($null -eq $current) { $intro.Add($line) } else { $current.Add($line) }
}
function Trim-Menu($block) {
    # Drop the menu that follows the result so each picture shows the command and its output.
    $result = New-Object System.Collections.Generic.List[string]
    foreach ($line in $block) {
        if ($line -match "^Pending scans: \d+ \| fake server" ) { break }
        $result.Add($line)
    }
    while ($result.Count -gt 0 -and $result[$result.Count - 1].Trim() -eq "") { $result.RemoveAt($result.Count - 1) }
    return ,$result
}
$cmd = "PS $repo> dotnet run --project ApexPost.Courier.Offline.Harness"
$introLines = @($intro | Where-Object { $_ -match "^ApexPost courier offline harness|^Value indexes" })
$menu = @($intro | Where-Object { $_ -match "^\d\) " })

Save-ConsoleImage "a1-query-by-tracking-number.png" "Part A - SQL++ query: all scans for a tracking number, ordered by scannedAtUtc" `
    (@($cmd) + $introLines + @("") + $menu + @("") + (Trim-Menu $blocks["seed"]) + @("") + (Trim-Menu $blocks["find"]))
Save-ConsoleImage "a2-pending-and-counts.png" "Part A - SQL++ queries: pending scans and number of scans per scanType for one courier" `
    (@($cmd, "...") + (Trim-Menu $blocks["pending"]) + @("") + (Trim-Menu $blocks["counts"]))
Save-ConsoleImage "a3-listener-log.png" "Part A - collection change listener: changed document IDs and the live pending count" `
    (@($cmd, "...") + (Trim-Menu $blocks["add"]) + @("") + (Trim-Menu $blocks["sync"]) + @("") + (Trim-Menu $blocks["after"]) + @("") + (Trim-Menu $blocks["exit"]))
Remove-Item -Recurse -Force $dataDir -ErrorAction SilentlyContinue

# ---------------------------------------------------------------------------------------------------------------
# Part C - API, Swagger UI and HTTP responses
# ---------------------------------------------------------------------------------------------------------------
Write-Host "Part C: starting ApexPost.Api..." -ForegroundColor Cyan
$secrets = (dotnet user-secrets list --project ApexPost.Api 2>$null) -join "`n"
if ($secrets -notmatch "ConnectionStrings:ApexPostDb") {
    dotnet user-secrets set "ConnectionStrings:ApexPostDb" "Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True" --project ApexPost.Api | Out-Null
}
if ($secrets -notmatch "Jwt:SigningKey") {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($bytes)) --project ApexPost.Api | Out-Null
}

$base = "https://localhost:7301"
$apiLog = Join-Path $env:TEMP "apexpost-api-evidence.log"
$api = Start-Process dotnet -ArgumentList "run --no-build --project ApexPost.Api --launch-profile https" `
    -WorkingDirectory $repo -PassThru -WindowStyle Hidden -RedirectStandardOutput $apiLog
try {
    # Local development only: -k accepts the ASP.NET Core development certificate on localhost.
    $ready = $false
    for ($i = 0; $i -lt 90; $i++) {
        Start-Sleep -Seconds 1
        $code = & curl.exe -k -s -o NUL -w "%{http_code}" "$base/openapi/v2.json"
        if ($code -eq "200") { $ready = $true; break }
    }
    if (-not $ready) { throw "ApexPost.Api did not start. See $apiLog" }

    $edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($edge) {
        foreach ($doc in @(@{ File = "c1-swagger-v2.png"; Name = "v2" }, @{ File = "c1-swagger-v1-deprecated.png"; Name = "v1 (deprecated)" })) {
            $target = Join-Path $outDir $doc.File
            $url = "$base/swagger/index.html?urls.primaryName=" + [Uri]::EscapeDataString($doc.Name)
            $profileDir = Join-Path $env:TEMP ("apexpost-edge-" + [Guid]::NewGuid().ToString("N"))
            & $edge --headless=new --disable-gpu --ignore-certificate-errors --hide-scrollbars --user-data-dir="$profileDir" `
                --window-size=1400,1300 --virtual-time-budget=10000 --screenshot="$target" $url 2>$null | Out-Null
            Start-Sleep -Seconds 2
            Remove-Item -Recurse -Force $profileDir -ErrorAction SilentlyContinue
            if (Test-Path $target) { Write-Host "  saved docs\screenshots\$($doc.File)" -ForegroundColor Green }
            else { Write-Host "  Edge did not save $($doc.File)" -ForegroundColor Yellow }
        }
    } else {
        Write-Host "  Microsoft Edge not found - Swagger UI screenshots skipped." -ForegroundColor Yellow
    }

    $token = (& curl.exe -k -s "$base/dev/token?roles=Admin,Courier&courierId=courier-007" | ConvertFrom-Json).accessToken
    $trackingNumber = "APX-" + (Get-Random -Minimum 20000000 -Maximum 99999999)

    function Invoke-Evidence([string]$FileName, [string]$Title, [System.Collections.IDictionary]$RequestBody) {
        $bodyFile = Join-Path $env:TEMP "apexpost-evidence-body.json"
        $json = $RequestBody | ConvertTo-Json -Compress
        [System.IO.File]::WriteAllText($bodyFile, $json, (New-Object System.Text.UTF8Encoding $false))
        $raw = (& curl.exe -k -s -i -X POST "$base/api/v2/shipments" -H "Authorization: Bearer $token" `
            -H "Content-Type: application/json" --data-binary "@$bodyFile") -join "`n"
        Remove-Item $bodyFile -ErrorAction SilentlyContinue
        $parts = $raw -split "`n`n", 2
        $headers = @($parts[0] -split "`n" | Where-Object { $_ -notmatch "^(Date|Server|Transfer-Encoding):" })
        $responseBody = ""
        if ($parts.Count -gt 1 -and $parts[1].Trim()) {
            try { $responseBody = ($parts[1] | ConvertFrom-Json) | ConvertTo-Json -Depth 10 } catch { $responseBody = $parts[1] }
        }
        $shown = @(
            "PS> curl -i -X POST $base/api/v2/shipments -H `"Authorization: Bearer <token>`" -H `"Content-Type: application/json`" \",
            "      -d '$json'", "") + $headers + @("") + @($responseBody -split "`r?`n")
        Save-ConsoleImage $FileName $Title $shown
    }

    $valid = [ordered]@{
        trackingNumber = $trackingNumber; weightKg = 2.5; recipientName = "Asha Verma"
        recipientPhone = "+15550100123"; recipientAddress = "12 Elm Row"; city = "Maplewood"
    }
    Invoke-Evidence "c4-201-location.png" "Part B/C - 201 Created with the Location header (POST /api/v2/shipments)" $valid
    Invoke-Evidence "c3-409-conflict.png" "Part B/C - 409 Conflict: the same tracking number posted again" $valid
    $invalid = [ordered]@{
        trackingNumber = "BAD-1"; weightKg = 0; recipientName = ""; recipientPhone = "abc"; recipientAddress = ""; city = ""
    }
    Invoke-Evidence "c2-400-validation.png" "Part B/C - 400 ValidationProblemDetails (automatic [ApiController] validation)" $invalid
}
finally {
    & taskkill.exe /PID $api.Id /T /F 2>$null | Out-Null
}

Write-Host "Done. Images are in docs\screenshots." -ForegroundColor Green
