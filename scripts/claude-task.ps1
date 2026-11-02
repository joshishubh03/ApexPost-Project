$ErrorActionPreference = 'Continue'
Write-Output "=== cleanup ==="
$remove = @('PROJECT_REVIEW_AND_FIXES.md', 'docs/week3-sqlserver.md', 'sql/relational-schema-notes.sql', 'sql/seed-data.sql', 'scripts/database', 'app-out.log', 'app-err.log')
foreach ($p in $remove) { if (Test-Path $p) { Remove-Item -Recurse -Force $p; Write-Output "removed $p" } }
Write-Output "=== tracked? ==="
git ls-files -- LatestCode.zip failing-three.txt test-output.txt AUDIT-SAMPLE.json
Write-Output "=== branch ==="
git branch --show-current
Write-Output "=== status ==="
git status --short | Select-Object -First 120
