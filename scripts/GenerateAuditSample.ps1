$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$webOutputDirectory = Join-Path $repositoryRoot "ApexPost.Web\bin\Debug\net10.0"
$webAssembly = Join-Path $webOutputDirectory "ApexPost.Web.dll"
if (-not (Test-Path -LiteralPath $webAssembly)) {
    throw "Build the solution first with 'dotnet build ApexPost.slnx'."
}

$runId = [Guid]::NewGuid().ToString("N")
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) "ApexPostAuditSample-$runId"
$dataDirectory = Join-Path $temporaryRoot "data"
$stdoutPath = Join-Path $temporaryRoot "web.stdout.log"
$stderrPath = Join-Path $temporaryRoot "web.stderr.log"
$temporaryPassword = "Audit-Sample-$runId!"
$staffEmail = "manager1@apexpost.local"
$sampleSku = "AUDIT-LIVE-$($runId.Substring(0, 8).ToUpperInvariant())"
$process = $null
$client = $null
$savedEnvironment = @{}
$cookieJar = [ordered]@{}

function Set-ChildEnvironmentValue {
    param([string]$Name, [string]$Value)
    $savedEnvironment[$Name] = [Environment]::GetEnvironmentVariable($Name, "Process")
    [Environment]::SetEnvironmentVariable($Name, $Value, "Process")
}

function Invoke-LocalRequest {
    param(
        [System.Net.Http.HttpMethod]$Method,
        [string]$Path,
        [System.Collections.IDictionary]$Form = $null
    )

    $request = [System.Net.Http.HttpRequestMessage]::new($Method, "$baseUrl$Path")
    $response = $null
    if ($cookieJar.Count -gt 0) {
        $cookieHeader = ($cookieJar.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "; "
        [void]$request.Headers.TryAddWithoutValidation("Cookie", $cookieHeader)
    }
    if ($null -ne $Form) {
        $formValues = [System.Collections.Generic.Dictionary[string, string]]::new()
        foreach ($key in $Form.Keys) {
            $formValues[[string]$key] = [string]$Form[$key]
        }
        $request.Content = [System.Net.Http.FormUrlEncodedContent]::new($formValues)
    }

    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            foreach ($headerValue in $response.Headers.GetValues("Set-Cookie")) {
                $pair = ($headerValue -split ";", 2)[0]
                $separator = $pair.IndexOf("=", [StringComparison]::Ordinal)
                if ($separator -gt 0) {
                    $cookieJar[$pair.Substring(0, $separator)] = $pair.Substring($separator + 1)
                }
            }
        }
        catch [InvalidOperationException] {
            # Responses without Set-Cookie keep the current local-session cookies.
        }

        return [PSCustomObject]@{
            StatusCode = [int]$response.StatusCode
            Content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }
    finally {
        $request.Dispose()
        if ($null -ne $response) { $response.Dispose() }
    }
}

function Get-AntiforgeryToken {
    param([string]$Html)
    $match = [Regex]::Match($Html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) {
        $match = [Regex]::Match($Html, 'value="([^"]+)"[^>]*name="__RequestVerificationToken"')
    }
    if (-not $match.Success) { throw "The live MVC page did not return an antiforgery token." }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

$portListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$portListener.Start()
$port = ([System.Net.IPEndPoint]$portListener.LocalEndpoint).Port
$portListener.Stop()
$baseUrl = "http://127.0.0.1:$port"

try {
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    Set-ChildEnvironmentValue "ASPNETCORE_ENVIRONMENT" "Development"
    Set-ChildEnvironmentValue "ASPNETCORE_URLS" $baseUrl
    Set-ChildEnvironmentValue "DemoData__Password" $temporaryPassword
    Set-ChildEnvironmentValue "ApexPost__DataDirectory" $dataDirectory

    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList @($webAssembly, "--urls", $baseUrl) `
        -WorkingDirectory $webOutputDirectory `
        -WindowStyle Hidden `
        -PassThru `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(10)

    $ready = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        if ($process.HasExited) { throw "The local ApexPost host stopped before becoming ready." }
        try {
            $health = Invoke-LocalRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/health"
            if ($health.StatusCode -eq 200) { $ready = $true; break }
        }
        catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $ready) { throw "The local ApexPost host did not become ready within 20 seconds." }

    $loginPage = Invoke-LocalRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/Account/Login"
    if ($loginPage.StatusCode -ne 200) { throw "The live login page returned HTTP $($loginPage.StatusCode)." }
    $loginToken = Get-AntiforgeryToken $loginPage.Content
    $loginResponse = Invoke-LocalRequest -Method ([System.Net.Http.HttpMethod]::Post) -Path "/Account/Login" -Form @{
        Email = $staffEmail
        Password = $temporaryPassword
        RememberMe = "false"
        ReturnUrl = ""
        __RequestVerificationToken = $loginToken
    }
    if ($loginResponse.StatusCode -ne 302) { throw "The local demo manager sign-in returned HTTP $($loginResponse.StatusCode)." }

    $cataloguePage = Invoke-LocalRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/Catalog/Index"
    if ($cataloguePage.StatusCode -ne 200) { throw "The live catalogue page returned HTTP $($cataloguePage.StatusCode)." }
    $catalogueToken = Get-AntiforgeryToken $cataloguePage.Content
    $createResponse = Invoke-LocalRequest -Method ([System.Net.Http.HttpMethod]::Post) -Path "/Catalog/Create" -Form @{
        "NewProduct.Sku" = $sampleSku
        "NewProduct.Name" = "Audit Sample Product"
        "NewProduct.Description" = "Created in a temporary local MVC session."
        "NewProduct.Category" = "Stationery"
        "NewProduct.TaxCategory" = "Standard"
        "NewProduct.Price" = "8.95"
        __RequestVerificationToken = $catalogueToken
    }
    if ($createResponse.StatusCode -ne 302) { throw "Live product creation returned HTTP $($createResponse.StatusCode)." }

    $productsPath = Join-Path $dataDirectory "products.json"
    $product = Get-Content -LiteralPath $productsPath -Raw | ConvertFrom-Json | Where-Object { $_.sku -eq $sampleSku } | Select-Object -First 1
    if ($null -eq $product) { throw "The local MVC session did not persist the audit sample product." }

    $deactivateResponse = Invoke-LocalRequest -Method ([System.Net.Http.HttpMethod]::Post) -Path "/Catalog/SetActive" -Form @{
        id = [string]$product.id
        isActive = "false"
        __RequestVerificationToken = $catalogueToken
    }
    if ($deactivateResponse.StatusCode -ne 302) { throw "Live product deactivation returned HTTP $($deactivateResponse.StatusCode)." }

    $recordsPath = Join-Path $dataDirectory "audit-records.json"
    $records = @(Get-Content -LiteralPath $recordsPath -Raw | ConvertFrom-Json | Where-Object {
        $_.routeValues.id -eq [string]$product.id -or $_.action -eq "MvcCatalogCreate"
    })
    if ($records.Count -ne 2) { throw "Expected two live MVC audit records; found $($records.Count)." }

    $samplePath = Join-Path $repositoryRoot "AUDIT-SAMPLE.json"
    $records | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $samplePath -Encoding UTF8
    Write-Output "Wrote $($records.Count) sanitized audit records from a local ApexPost MVC session to AUDIT-SAMPLE.json."
}
finally {
    if ($null -ne $client) { $client.Dispose() }
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (Test-Path -LiteralPath $temporaryRoot) {
        $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
        $systemTemporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolvedTemporaryRoot.StartsWith($systemTemporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove the generated data outside the temporary directory."
        }
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
    }
}
