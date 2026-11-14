# Week 4 assessment evidence

Week 4 builds the courier flow: an offline store on the device (Part A), then the API (Parts B-C), then the mobile
app (Part D). Each part was developed on its own branch (`week4-partA` ... `week4-partD`) and merged into `main`
with a merge commit.

## Part A - Couchbase Lite offline store (25)

| Criterion | Evidence |
|---|---|
| Database lifecycle and document model (5) | `ApexPost.Courier.Offline/Storage/CourierDatabase.cs`: `apexpost_courier`, default collection, `IDisposable`. `ApexPost.Courier.Offline.Domain/Domain/ShipmentScan.cs`: all the required fields, and IDs `scan::{Guid}`. |
| Repository and key-value CRUD (5) | `ApexPost.Courier.Offline.Domain/Abstractions/IScanRepository.cs`, `ApexPost.Courier.Offline/Storage/CouchbaseScanRepository.cs`. The domain is a separate project with no package references, so the compiler keeps Couchbase out of it; `ArchitectureTests` guards that project's references (ADR 0054). |
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

## Review follow-ups

| Feedback | Change | Evidence |
|---|---|---|
| Part A domain shared a project with Couchbase | `ShipmentScan` and the ports moved to `ApexPost.Courier.Offline.Domain`, a project with no package references | ADR 0054, `ArchitectureTests` |
| Wildcard package versions | Every PackageReference has an exact version | ADR 0054, `PackageVersionTests` |
| `PendingScanMonitor` blocked on a Task | Uses the synchronous `CouchbaseScanRepository.CountPending()` | `ChangeListenerTests.Monitor_starts_from_the_stored_pending_count_using_the_synchronous_count` |
| A shipped Week 3 migration was edited | `Week3SchemaCompliance` restored to the shipped version; the idempotent script is handled by `IdempotentScriptMigrationsSqlGenerator` | ADR 0053, `MigrationScriptTests` |

## Verification

```powershell
dotnet build ApexPost.Server.slnf      # everything except the MAUI app
dotnet test  ApexPost.Server.slnf      # LocalDB; first run also writes the generated contracts/v1.json and v2.json
dotnet build ApexPost.Courier -f net10.0-windows10.0.19041.0
```

---

# Week 5 assessment evidence (testing strategy)

| Criterion | Evidence |
|---|---|
| Strategy document (20) | [TESTING-STRATEGY.md](../TESTING-STRATEGY.md): risk map (10 risks), pyramid with proportions and reasons, xUnit justification, one row per existing test class (kept / modified / removed), data access strategy (Moq, fakes, why not SQLite, SQL Server test database, why not to mock `DbContext`), what is not tested |
| Unit tests (15) | `ApexPost.UnitTests`: AAA comments and `Method_Scenario_ExpectedResult` names in all new tests; `[Theory]` for status transitions (36 rows), tracking number, weight, phone, clock skew, scan ID, batch size; boundaries (0.09 / 0.1 / 70 / 70.01, 300 s / 301 s, 50 / 51); exception tests. Runs from Test Explorer and `dotnet test` |
| Mocking and isolation (20) | Moq: `CourierShipmentServiceMockTests`, `OutboxServiceMockTests` (`Setup`, `ReturnsAsync`, `SetupSequence`, `ThrowsAsync`, `It.Is`, `It.IsAny`, `Verify` + `Times`, `VerifyNoOtherCalls`). HTTP: `ApexApiClientHttpTests` (200 JSON, 404, 500, timeout, cancellation, ProblemDetails). Repositories on SQL Server only (`IntegrationTests/SqlServer`). End-to-end API test: `ShipmentsApiTests` with `WebApplicationFactory`, a disposable database and test JWTs |
| TDD (15) | Commits `RED:` / `GREEN:` / `REFACTOR:`; [TDD-LOG.md](../TDD-LOG.md); `StaleScanRuleTests` |
| BDD (15) | `ApexPost.Specs` with Reqnroll 3.3.4: `ShipmentCreation.feature` (API level), `ScanSync.feature` (service level), Background, Scenario Outlines with Examples, context injection (`ShipmentApiContext`, `CourierSyncContext`), hooks (`Support/Hooks.cs`) |
| Copilot (10) | [COPILOT-LOG.md](../COPILOT-LOG.md): prompts, review checklist and decisions, `scripts/prove-tests-fail.ps1` logs in `docs/test-results/` |
| Results and reflection (5) | README "Test results" (written by `scripts/run-tests.ps1`), reflection and viva notes in TESTING-STRATEGY.md sections 10-11 |
