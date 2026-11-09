<#
    Regenerates contracts/v1.json and contracts/v2.json from the running API code (in-memory test host, Development
    environment, no database), then rebuilds ApexPost.Client so the NSwag client matches the new v2 contract.
    Review the git diff, record the change in VERSIONING.md, then commit contracts/ and ApexPost.Client/Generated/.
#>
$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $env:APEXPOST_UPDATE_CONTRACTS = "1"
    dotnet test ApexPost.Api.Tests --filter "FullyQualifiedName~OpenApiDocumentTests.Committed_contract_matches_the_generated_document"
    Remove-Item Env:APEXPOST_UPDATE_CONTRACTS
    dotnet build ApexPost.Client
    git --no-pager diff --stat -- contracts ApexPost.Client/Generated
}
finally {
    Pop-Location
}
