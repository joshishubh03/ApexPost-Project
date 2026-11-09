# 0049: NuGet packages outside the allowed list

- Status: Accepted
- Date: 2026-11-03

## Context

The Week 3 brief allows `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, `Microsoft.EntityFrameworkCore.*`
(SqlServer, Design, Tools), xUnit and `Microsoft.AspNetCore.Mvc.Testing`. Anything else needs trainer approval
and an ADR. The solution builds with `TreatWarningsAsErrors`, so NuGet audit warnings break the build.

## Decision

| Package | Project | Why it is needed | Alternative considered |
|---|---|---|---|
| `System.Security.Cryptography.Pkcs` 10.0.13 | Infrastructure | Not used directly. It pins the patched version of a *transitive* dependency of `Microsoft.Data.SqlClient` (pulled in by `Microsoft.EntityFrameworkCore.SqlServer`). Without the pin, NuGet audit raises NU1903 (known vulnerability) and the build fails. It ships from the .NET runtime repository, with the same version line as the framework. | Suppressing NU1903 would hide a real vulnerability. Downgrading EF Core is not allowed. |
| `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` | Tests | The test host and runner that `dotnet test` needs to execute xUnit tests. They are part of "xUnit" in practice. | None: without them no test runs. |
| `coverlet.collector` | Tests | Optional code-coverage collector (`dotnet test --collect "XPlat Code Coverage"`). It has no runtime effect. | Can be removed if the trainer prefers. Nothing depends on it. |

No other third-party packages are referenced. All data access uses EF Core and `Microsoft.Data.SqlClient` (via EF).

## Consequences

When EF Core releases a version whose SqlClient dependency is already patched, the `Pkcs` pin can be removed.
`dotnet list package --vulnerable --include-transitive` must stay empty.
