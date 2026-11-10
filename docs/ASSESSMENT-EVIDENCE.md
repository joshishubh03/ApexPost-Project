# Week 4 assessment evidence

Week 4 builds the courier flow: an offline store on the device (Part A), then the API (Parts B-C), then the mobile
app (Part D). Each part was developed on its own branch (`week4-partA` ... `week4-partD`) and merged into `main`
with a merge commit.

## Part A - Couchbase Lite offline store (25)

| Criterion | Evidence |
|---|---|
| Database lifecycle and document model (5) | `ApexPost.Courier.Offline/Storage/CourierDatabase.cs`: `apexpost_courier`, default collection, `IDisposable`. `Domain/ShipmentScan.cs`: all the required fields, and IDs `scan::{Guid}`. |
| Repository and key-value CRUD (5) | `Abstractions/IScanRepository.cs`, `Storage/CouchbaseScanRepository.cs`. `ArchitectureTests` proves the domain has no Couchbase types. |
| SQL++ queries, parameters, indexes (6) | Queries by tracking number (ordered by `scannedAtUtc`), pending, and counts per scan type for a courier. All use `$parameters`. Value indexes on `trackingNumber`, `syncStatus` and `courierId`. Tests: `CouchbaseScanRepositoryTests`. |
| Outbox / offline persistence (4) | `Outbox/OutboxService.cs` (save locally as Pending; Synced or Failed; transient errors stay Pending) and `FakeShipmentUploader`. Tests: `OutboxServiceTests`. |
| Change listener and clean-up (3) | `Monitoring/PendingScanMonitor.cs` logs the changed IDs and the live pending count. `CourierDatabase.Dispose()` removes the tokens. Tests: `ChangeListenerTests`. |
| Design note (2) | `OFFLINE-DESIGN.md` |
| Harness, query results and listener log | `ApexPost.Courier.Offline.Harness` (menu); screenshots `a1`-`a3` in [docs/screenshots](screenshots/README.md) |

## Part B - REST API (25)

| Criterion | Evidence |
|---|---|
| Endpoints and verbs (6) | `ApexPost.Api/Controllers/V1|V2/ShipmentsController.cs`, `Controllers/ScansController.cs`, `API-NOTES.md` |
| Status codes and Location (5) | `CreatedAtAction` (201 + Location), 204, 400, 404, 409, 415 and 500. Tests: `ShipmentsApiTests`, `ScansApiTests`. |
| DTOs and validation (6) | `Contracts/ShipmentRequests.cs` and `ScanRequests.cs`. Custom rules: `[NotInFuture]` and the `IValidatableObject` rule that Delivered needs a signature. |
| ProblemDetails and global handler (4) | `ErrorHandling/ApiExceptionHandler.cs` (`IExceptionHandler`; stack traces only in Development), `UseStatusCodePages`. Tests: `ExceptionMappingTests`. |
| Thin controllers, DI, idempotent upload (4) | `CourierShipmentService`, `AddApexPostSqlServer` + `AddApexPostCourierShipments` (ADR 0052). Scan GUID is the primary key. Tests: `CourierShipmentServiceTests`. |

## Part C - OpenAPI (25)

| Criterion | Evidence |
|---|---|
| OpenAPI and document transformer (4) | `ApexPost.Api/OpenApi/OpenApiConfiguration.cs` |
| Swagger UI, Development only, README (3) | `Program.cs`, README "OpenAPI specification vs the .NET generator vs Swagger UI" |
| XML comments, response types, tags, operationIds (5) | Controllers; `GenerateDocumentationFile`. Tests: `OpenApiDocumentTests`. |
| v1/v2 versioning with deprecation (6) | `[ApiVersion("1.0", Deprecated = true)]`, Sunset policy, `ReportApiVersions`, `ShipmentResponseV2`. Tests: `VersioningApiTests`. |
| Version-specific documents (3) | `/openapi/v1.json`, `/openapi/v2.json`, Swagger dropdown "v1 (deprecated)" / "v2", `contracts/` |
| SDK and consumer (4) | `ApexPost.Client` (`nswag.json`, `Generated/`), `ApexPost.Client.Consumer` |
| Screenshots | Swagger v1/v2, 400, 409 and 201 with `Location`: `c1`-`c4` in [docs/screenshots](screenshots/README.md) |

## Part D - Security and mobile integration (25)

| Criterion | Evidence |
|---|---|
| JWT and roles, 401/403, courierId check (5) | `ApexPost.Api/Program.cs`, `Security/`. Tests: `SecurityApiTests`. |
| Partner API key, HTTPS/HSTS (3) | `Security/PartnerApiKeyMiddleware.cs`, `UseHttpsRedirection`, `UseHsts` |
| SECURITY.md (2) | `SECURITY.md` |
| Typed client and auth handler (4) | `ApexPost.Courier.Core/Api/ApexApiClient.cs`, `Auth/TokenAndSession.cs`, `CourierCoreServiceCollectionExtensions.cs` |
| Polly retry, circuit breaker, timeout with logging (5) | `Resilience/CourierResilience.cs`, `RESILIENCE.md`. Tests: `ResilienceTests`. |
| JSON and ProblemDetails (2) | `Api/ApiDtos.cs` (`CourierJsonContext`), `ProblemMessages`. Tests: `AuthAndJsonTests`. |
| Offline sync and SecureStorage (4) | `Sync/ApiShipmentUploader.cs`, `Sync/SyncCoordinator.cs`, `ApexPost.Courier/Services/PlatformServices.cs`. Tests: `OfflineSyncTests`. |
| End-to-end demo | Demo script in README ("Demo script"); automated evidence in `OfflineSyncTests`, `ResilienceTests`, `SecurityApiTests` |

## Verification

```powershell
dotnet build ApexPost.Server.slnf      # everything except the MAUI app
dotnet test  ApexPost.Server.slnf      # LocalDB; first run also writes the generated contracts/v1.json and v2.json
dotnet build ApexPost.Courier -f net10.0-windows10.0.19041.0
```
