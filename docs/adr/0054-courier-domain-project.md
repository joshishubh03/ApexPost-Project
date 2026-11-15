# 0054: Courier domain in its own project; exact package versions

- Status: Accepted (amends 0051)
- Date: 2026-11-13

## Context

In Part A, `ShipmentScan`, `IScanRepository` and `IShipmentUploader` lived in `ApexPost.Courier.Offline`, the same
project that references `Couchbase.Lite`. A reflection test (`ArchitectureTests`) checked that the domain did not
expose Couchbase types. But nothing stopped someone adding a `using Couchbase.Lite;` to a domain file: the package
was one line away, and the rule was only checked after the build.

Several package references also used wildcard versions (`Couchbase.Lite 3.2.*`, `Microsoft.AspNetCore.OpenApi 10.0.*`,
`Swashbuckle.AspNetCore.SwaggerUi 9.*`, `Asp.Versioning.* 8.*`, `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.*`).
Two restores on different days could build different code.

## Decision

- New project **`ApexPost.Courier.Offline.Domain`** with **no package or project references**. It holds
  `Domain/ShipmentScan.cs` (the scan record, `ScanType`, `SyncStatus`, `ShipmentScanId`, `TrackingNumbers`) and the
  ports `Abstractions/IScanRepository.cs` and `Abstractions/IShipmentUploader.cs`. Namespaces are unchanged
  (`ApexPost.Courier.Offline.Domain`, `ApexPost.Courier.Offline.Abstractions`), so no calling code changed.
- `ApexPost.Courier.Offline` keeps the Couchbase adapter (`CourierDatabase`, `CouchbaseScanRepository`), the outbox
  and `PendingScanMonitor`, and references the domain project.
- The compiler now enforces the rule: a Couchbase type in the domain project does not compile. `ArchitectureTests`
  remains as a guard against a reference being added to the domain project later.
- `PendingScanMonitor` is part of the Couchbase adapter, so it uses the synchronous
  `CouchbaseScanRepository.CountPending()` in its constructor and listener callback, instead of blocking on
  `CountPendingAsync().GetAwaiter().GetResult()`.
- **Every PackageReference uses an exact version**: `Couchbase.Lite 3.2.6`, `Microsoft.AspNetCore.OpenApi 10.0.14`,
  `Swashbuckle.AspNetCore.SwaggerUi 9.0.6`, `Asp.Versioning.Mvc 8.1.1`, `Asp.Versioning.Mvc.ApiExplorer 8.1.1`,
  `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.14`. These are the newest releases the old wildcards matched.
  The ASP.NET Core packages are on the same 10.0.14 servicing release as EF Core.
  `PackageVersionTests` fails if a wildcard or version range comes back.

## Consequences

- One more project in the solution. The dependency graph now shows the rule:
  `Courier.Offline.Domain <- Courier.Offline (Couchbase) <- Courier.Core / Harness / MAUI app`.
- Upgrading a package is a deliberate one-line change that shows up in review.
- `$(MauiVersion)` still comes from the installed MAUI workload. The SDK workload set controls it, not a floating
  NuGet version.
