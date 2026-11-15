# ApexPost Logistics - Week 3 (SQL Server + EF Core 10), Week 4 (courier offline -> API -> mobile app) and Week 5 (testing strategy)

ApexPost is a branch counter system: product sales, stock, shipping, money orders with identity checks, and
reporting. **Week 4** (Parts A-D) is described in the [Week 4 section](#week-4-courier-offline---api---mobile-app)
below, and the **Week 5** testing strategy in the [Week 5 section](#week-5-testing-strategy). In Week 3, SQL Server becomes the system of record:

- EF Core 10 maps a 3NF schema (20 tables) with migrations.
- Three stored procedures handle the set-based work.
- The application runs on Repository + Unit of Work with optimistic concurrency.
- Index choices are backed by measured execution plans.

In Week 3 Couchbase was out of scope. Week 4 Part A adds Couchbase Lite, but only on the courier device
(`ApexPost.Courier.Offline`). SQL Server stays the server's system of record.

| Document | What it covers |
|---|---|
| [DESIGN.md](DESIGN.md) | Layers, ER diagram, normalization, delete behaviours, data access rules, SOLID |
| [PERFORMANCE.md](PERFORMANCE.md) | Workloads, indexes, before/after plans and numbers, write overhead, SARGability, statistics |
| [docs/sql-transactions-and-concurrency.md](docs/sql-transactions-and-concurrency.md) | T-SQL transaction, blocking demo output, isolation level, deadlock avoidance |
| [docs/generated-sql-samples.md](docs/generated-sql-samples.md) | SQL that EF Core generated for the main queries (`ToQueryString`) |
| [docs/adr/](docs/adr/) | Architecture decisions; 0041-0049 are the Week 3 ones, 0051-0054 the Week 4 ones |
| [OFFLINE-DESIGN.md](OFFLINE-DESIGN.md) | Week 4 Part A: save locally first, conflicts, one process per embedded database |
| [API-NOTES.md](API-NOTES.md) / [VERSIONING.md](VERSIONING.md) | Week 4 Parts B-C: endpoints and status codes, v1/v2 changes and the v1 sunset plan |
| [SECURITY.md](SECURITY.md) / [RESILIENCE.md](RESILIENCE.md) | Week 4 Part D: JWT, roles, API keys, OAuth2, TLS/HSTS; retry, circuit breaker and timeout values |
| [docs/ASSESSMENT-EVIDENCE.md](docs/ASSESSMENT-EVIDENCE.md) | Week 4 requirement -> evidence map |
| [TESTING-STRATEGY.md](TESTING-STRATEGY.md) | Week 5: risk map, test pyramid, xUnit choice, review of every existing test, data access strategy, reflection |
| [TDD-LOG.md](TDD-LOG.md) / [COPILOT-LOG.md](COPILOT-LOG.md) | Week 5: the stale-scan rule built Red -> Green -> Refactor; Copilot prompts and review decisions |

## Solution layout

```
ApexPost.Domain          entities, value objects (Sku, BranchCode, Money, Weight, IdentityDocumentMask), invariants
ApexPost.Application     use-case services, ports (IUnitOfWork, repositories, IReportingQueries, IAuditStore), DTOs
ApexPost.Infrastructure  Persistence/ (ApexPostDbContext, Configurations/, Migrations/, Repositories/, Queries/,
                         StoredProcedures/, EfUnitOfWork, EfAuditStore), Seed/
ApexPost.Web             MVC controllers + views, reports API, Problem Details, audit filter, composition root
ApexPost.UnitTests       unit tests (no I/O): domain, validation, services with Moq/fakes, typed API client
ApexPost.IntegrationTests SQL Server, WebApplicationFactory (MVC + API), EF model/migrations, Couchbase Lite
ApexPost.Specs           Reqnroll BDD scenarios (business language)
sql/                     schema script, procedures, report queries, transaction + blocking demos, perf data/workloads
scripts/                 measure-performance.ps1, run-blocking-demo.ps1
```

## Prerequisites

- .NET 10 SDK
- SQL Server 2019 or later. LocalDB (`(localdb)\MSSQLLocalDB`, installed with Visual Studio) is enough to run the app and the tests.
- EF Core CLI: `dotnet tool install --global dotnet-ef` (or `dotnet tool update --global dotnet-ef`)
- Optional: `sqlcmd`, used by the scripts in `scripts/`

## 1. Configure secrets (never in appsettings)

`appsettings.json` contains no connection string and no passwords. Set them with user-secrets for `ApexPost.Web`.
The EF CLI design-time factory reads the same values.

```powershell
cd <repo root>
# connection string (LocalDB shown; for a full instance use Server=localhost)
dotnet user-secrets set "ConnectionStrings:ApexPostDb" "Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True" --project ApexPost.Web

# password used by the Development seeder for every demo account
dotnet user-secrets set "DemoData:Password" "CounterDemo!2026" --project ApexPost.Web

# optional: partner API key for /api/partner/* (stored hashed in memory, compared in constant time)
dotnet user-secrets set "PartnerApi:Partners:demo-partner" "<a long random key>" --project ApexPost.Web

# optional: only when using a real courier tracking provider instead of the simulator
dotnet user-secrets set "CourierTracking:ApiKey" "<provider key>" --project ApexPost.Web
```

In a deployment, use environment variables instead, for example `ConnectionStrings__ApexPostDb`. The legacy
`APEXPOST_DB_CONNECTION` variable is also read.

## 2. Create the database (migrations)

```powershell
dotnet ef database update --project ApexPost.Infrastructure --startup-project ApexPost.Web --context ApexPost.Infrastructure.Persistence.ApexPostDbContext
```

This creates `ApexPostDb` with all tables, constraints, indexes and the three stored procedures
(`usp_DailyBranchSales`, `usp_LowStockReport`, `usp_AdjustStock`).

In Development, `Persistence:ApplyMigrationsOnStartup` is `true`, so `dotnet run` also migrates. Outside
Development it is `false` and migrations are applied as a deployment step, using the script below.

- **Idempotent script (for DBAs / CI):**
  ```powershell
  dotnet ef migrations script --idempotent --project ApexPost.Infrastructure --startup-project ApexPost.Web --context ApexPost.Infrastructure.Persistence.ApexPostDbContext --output sql/ApexPostDb_schema.sql
  ```
- **New migration after a model change:**
  ```powershell
  dotnet ef migrations add <Name> --project ApexPost.Infrastructure --startup-project ApexPost.Web --context ApexPost.Infrastructure.Persistence.ApexPostDbContext --output-dir Persistence/Migrations
  ```
- **Upgrading an old Week 2 database:** migration `Week3SchemaCompliance` moves shipping data into the new
  `Shipments` table before dropping the old columns. Back up first. If the database was created by the removed
  legacy `Data` context, drop it and recreate it from the migrations.

## 3. Run

```powershell
dotnet build ApexPost.slnx
dotnet run --project ApexPost.Web
```

Open the URL printed in the console. `/health` returns `ok`. In Development, the seeder fills an empty database with:

- 5 branches, 20 staff, 30 products and stock rows
- 10 shipping zones and 30 rates
- 240 historical transactions over 60 days, with lines, payments and 120 shipments
- money orders, identity checks and stock adjustments

### Demo accounts (password = the `DemoData:Password` secret, e.g. `CounterDemo!2026`)

| Role | Accounts |
|---|---|
| Branch manager | `manager1@apexpost.local` ... `manager5@apexpost.local` (one per branch) |
| Counter clerk | `clerk11@apexpost.local`, `clerk12@apexpost.local`, ... `clerk52@apexpost.local` (two per branch); `clerk-extra1..3@apexpost.local` |
| Auditor | `auditor1@apexpost.local` |
| Administrator | `admin@apexpost.local` |

Things to try:

- **Counter -> New sale.** Stock and the receipt are saved in one transaction.
- **Inventory -> Adjust.** Runs `usp_AdjustStock`.
- **Reports -> Operations reports.** Shows daily sales and low stock (procedures), shipping volume by zone,
  money orders by branch, and branches with no sales.
- **Catalog -> Revise prices** (manager/admin). Runs a set-based `ExecuteUpdate` plus an audit row.
- **JSON API:** `GET /api/reports/daily-sales?branchCode=AP-001&date=2026-11-01` and `/api/reports/low-stock`
  (requires an authenticated manager, auditor or admin).
- Open the same sale/stock item in two tabs and save both. The second save gets **409 Conflict**.

## 4. Tests

```powershell
dotnet test ApexPost.Server.slnf      # all test projects; the MAUI app is left out (it needs the MAUI workload)
```

The tests are split into `ApexPost.UnitTests`, `ApexPost.IntegrationTests` and `ApexPost.Specs`; see the
[Week 5 section](#week-5-testing-strategy) for how to run each one and the latest results.

- Unit and unit-of-work spy tests need no database.
- SQL Server integration and web tests do the following:
  1. Create a throw-away database `ApexPostDb_Test_<guid>`.
  2. Apply the real migrations and run the real seeder.
  3. Drop the database at the end.
- These tests cover round-trips, rollback, concurrency conflicts, procedures, N+1 query count, 409/404/400
  Problem Details, over-posting and authorization.
- By default they use LocalDB. To use another server:
  ```powershell
  $env:APEXPOST_TEST_SQLSERVER = "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True"
  ```

## 5. SQL artefacts (`sql/`)

| File | Content |
|---|---|
| `ApexPostDb_schema.sql` | Idempotent script generated from the migrations |
| `stored-procedures.sql` | The three procedures (same text as the migration) with sample calls |
| `reporting-queries.sql` | Daily sales, low stock, shipping volume, money orders by branch, branches without sales (LEFT JOIN / NOT EXISTS), GROUP BY + HAVING |
| `stock-sale-transaction.sql` | Multi-table T-SQL transaction with TRY/CATCH, `XACT_ABORT`, `THROW` |
| `blocking-demo.sql` | Two-session blocking demo plus observer queries |
| `least-privilege.sql` | `apexpost_app` role: EXECUTE on procedures, DML on tables, DENY DELETE on history |
| `assignment3-compliance.sql` | Catalog queries that list every PK/FK/UNIQUE/CHECK/DEFAULT/rowversion |
| `performance-data.sql`, `performance-workloads.sql` | Volume data (300k transactions) and the measured workloads |
| `performance-evidence/` | `results.json`, actual `.sqlplan` files (open in SSMS), blocking-demo output |

Reproduce the evidence on a full SQL Server instance (it creates and drops `ApexPostDb_Perf`):

```powershell
powershell -ExecutionPolicy Bypass -File scripts/measure-performance.ps1 -Server localhost
powershell -ExecutionPolicy Bypass -File scripts/run-blocking-demo.ps1
```

## Persistence

SQL Server is the only persistence model. The JSON repositories from Weeks 1-2 were removed (ADRs 0002 and 0030
are marked superseded), so every environment, including the tests, runs the same EF Core code.

---

# Week 4: courier offline -> API -> mobile app

## Part A: offline courier store (Couchbase Lite)

| Project | Purpose |
|---|---|
| `ApexPost.Courier.Offline.Domain` | `ShipmentScan` domain record and the ports `IScanRepository` and `IShipmentUploader`. No package references, so the compiler keeps Couchbase out of the domain (ADR 0054) |
| `ApexPost.Courier.Offline` | Couchbase Lite adapter: `CouchbaseScanRepository`, `CourierDatabase` (IDisposable), plus `OutboxService`, `FakeShipmentUploader`, `PendingScanMonitor` |
| `ApexPost.Courier.Offline.Harness` | Console menu: Add Scan, Find by Tracking Number, List Pending, Simulate Sync, Delete Scan, Exit, plus scans per type, an offline toggle, demo seeding and query plans |
| Tests | `ApexPost.IntegrationTests/CourierOffline` (CRUD, SQL++ queries and parameters, indexes, outbox, change listener and clean-up) and `ApexPost.UnitTests/CourierOffline` (architecture rule, outbox logic with Moq) |

```powershell
dotnet run --project ApexPost.Courier.Offline.Harness           # data in %LOCALAPPDATA%\ApexPost\courier-harness
dotnet run --project ApexPost.Courier.Offline.Harness -- --data .\work\courier   # custom folder
dotnet test ApexPost.IntegrationTests --filter "FullyQualifiedName~Courier.Offline"
```

Try it: **9** (seed fictional scans), **2** with `APX-10000001`, **3**, **7** with `courier-007`, **8** (go
offline), **4** (scans stay Pending), **8** again, then **4** (Synced, plus one Failed because of `#reject`).
Every change prints a `[listener]` log line with the changed IDs and the live pending count. The design notes are
in [OFFLINE-DESIGN.md](OFFLINE-DESIGN.md).

## Part B: REST API (`ApexPost.Api`)

ASP.NET Core Web API with minimal hosting (`Program.cs`, no `Startup.cs`). It reuses the Week 3 DbContext,
unit of work and SQL Server composition (`AddApexPostSqlServer`). The additive Week 4 pieces are
`CourierShipmentService`, `EfCourierShipmentRepository` and the `CourierShipments` / `ShipmentScans` tables
(migration `Week4CourierShipmentsApi`).

```powershell
dotnet user-secrets set "ConnectionStrings:ApexPostDb" "Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True" --project ApexPost.Api
dotnet run --project ApexPost.Api --launch-profile https      # Development applies migrations on start
dotnet test ApexPost.IntegrationTests --filter "FullyQualifiedName~ApexPost.Api.Tests"   # disposable LocalDB database per run
```

- **Controllers stay thin.** They bind the input, call `CourierShipmentService` and return the HTTP result.
  The status machine, duplicate checks and the delete-only-while-Created rule live in the domain or the service.
- **DTOs only.** The requests are `CreateShipmentRequest`, `UpdateShipmentRequest`, `UpdateStatusRequest` and
  `ScanUploadRequest`; no EF entity is ever bound. Validation uses data annotations plus two custom rules: the
  `[NotInFuture]` attribute and an `IValidatableObject` rule that a Delivered scan needs a signature name.
- **Errors.** `[ApiController]` returns `ValidationProblemDetails` for 400s. `ApiExceptionHandler`
  (`IExceptionHandler`) produces ProblemDetails for 404, 409, 403 and 500, and stack traces appear only in
  Development. `UseStatusCodePages` gives empty-body 404, 405 and 415 responses a ProblemDetails body too.
- **Idempotent scan upload.** `ShipmentScans.Id` *is* the device scan GUID (primary key). Stored IDs and IDs
  repeated within one batch are counted as `duplicatesSkipped`. Scans that would break the status machine are
  listed in `rejected` with a reason.

## Part C: OpenAPI, Swagger UI, versioning and the client SDK

### OpenAPI specification vs the .NET generator vs Swagger UI

| Piece | What it is | In this repo |
|---|---|---|
| **OpenAPI Specification** | A vendor-neutral *standard* (a JSON/YAML format) that describes an HTTP API: paths, operations, parameters, schemas, status codes and security schemes. It is the contract, not a tool. | `contracts/v1.json`, `contracts/v2.json` (OpenAPI 3.0) |
| **Built-in .NET generator** (`Microsoft.AspNetCore.OpenApi`) | Code that *produces* an OpenAPI document from the running app's endpoints. It reads routes, `[ProducesResponseType]`, `[Tags]`, `[EndpointSummary]`, XML comments and the API Explorer groups. `AddOpenApi("v1")` / `AddOpenApi("v2")` register the documents and `MapOpenApi()` serves them at `/openapi/{v}.json`. Transformers customise the result. | `OpenApi/OpenApiConfiguration.cs` (title, description, version, contact; v1 operations marked deprecated) |
| **Swagger UI** (`Swashbuckle.AspNetCore.SwaggerUi`) | A static web page that *reads* an OpenAPI document and renders interactive docs with "Try it out". It generates nothing. Only the UI package is referenced; Swashbuckle's own generator is **not** used, so there is exactly one generator. | `/swagger`, with a dropdown for "v2" and "v1 (deprecated)" |

The OpenAPI documents and Swagger UI are enabled **in Development only**. Hiding documentation is not a security
control: the endpoints are protected by authentication and authorization (Part D), not by being undocumented.

### Rich metadata

- `GenerateDocumentationFile` is on, and every action and DTO has XML comments, which become summaries,
  descriptions and per-response texts.
- Every possible status code has a `[ProducesResponseType]`.
- `[Tags("Shipments")]` / `[Tags("Scans")]` group the operations, and `[EndpointSummary]` gives each one a summary.
- Every action has a stable route name, which becomes its `operationId` and the SDK method name (`CreateShipment`
  becomes `CreateShipmentAsync`). v1 names end in `V1` because route names must be unique.

### Versioning

URL-segment versioning (`/api/v1/...`, `/api/v2/...`) with `ReportApiVersions`. v1 is `Deprecated = true` and has
a Sunset policy. v2 nests the recipient, adds `latestScan` and returns `status` as a string. See
[VERSIONING.md](VERSIONING.md) and [API-NOTES.md](API-NOTES.md).

### Generated client SDK

| Project | Purpose |
|---|---|
| `ApexPost.Client` | NSwag-generated `ApexPostApiClient` / `IApexPostApiClient` from `contracts/v2.json`. The config is `ApexPost.Client/nswag.json`, with the NSwag version pinned in `.config/dotnet-tools.json`. Building the project regenerates `Generated/ApexPostApiClient.g.cs` when the contract changes. Additions live in `ApexPostApiClient.Extensions.cs` (partial class). |
| `ApexPost.Client.Consumer` | Console app. It registers the client with `AddHttpClient`, creates a shipment, fetches it, uploads two scans, and handles a 404 through `ApexPostApiException<ProblemDetails>`. |

```powershell
dotnet tool restore
dotnet build ApexPost.Client                      # runs: dotnet tool run nswag run nswag.json
dotnet run --project ApexPost.Api --launch-profile https
dotnet run --project ApexPost.Client.Consumer -- https://localhost:7301/
powershell -File scripts/update-contracts.ps1     # after an intentional API change
```

`ApexPost.Api/ApexPost.Api.http` has one success and one failure request per endpoint, for both versions.

## Part D: API security and the courier MAUI app

| Project | Purpose |
|---|---|
| `ApexPost.Api` (updated) | JWT bearer (issuer, audience, lifetime and key validated from config), roles (`Courier`/`Admin`), courierId ownership check, partner `X-Api-Key` middleware, HTTPS redirection everywhere plus HSTS outside Development, `Bearer` and `PartnerApiKey` schemes in OpenAPI, Development-only `/dev/token`. |
| `ApexPost.Courier.Core` | Typed `ApexApiClient` (`AddHttpClient<ApexApiClient>()`), `AuthHeaderHandler`, Polly retry / circuit breaker / timeouts with logging, source-generated `CourierJsonContext`, ProblemDetails to friendly messages, `ApiShipmentUploader` (replaces Part A's fake), `SyncCoordinator` (batches of 20 on reconnect). |
| `ApexPost.Courier` | .NET MAUI app (Android emulator + Windows). Screens: Login, New Scan, Pending Scans, Sync Status. Uses SecureStorage for the token, Connectivity for online/offline, and the offline banner. |
| Tests | `ApexPost.UnitTests/CourierClient` (retry, no retry on 4xx, circuit breaker, timeouts, auth handler and 401, JSON tolerance, HTTP status handling) and `ApexPost.IntegrationTests/CourierClient` (end-to-end sync with Couchbase Lite). |

### Setup

```powershell
# API secrets (never committed)
dotnet user-secrets set "Jwt:SigningKey" "$([Convert]::ToBase64String((1..32 | % { [byte](Get-Random -Max 256) })))" --project ApexPost.Api
dotnet user-secrets set "PartnerApi:Keys:demo-partner" "demo-partner-$(New-Guid)" --project ApexPost.Api
dotnet dev-certs https --trust
dotnet run --project ApexPost.Api --launch-profile https

# Tokens (Development): https://localhost:7301/dev/token?roles=Courier&courierId=courier-007   (or roles=Admin)
# Swagger UI: https://localhost:7301/swagger  -> Authorize -> paste the accessToken

# Courier app (needs: dotnet workload install maui)
dotnet build ApexPost.Courier -f net10.0-windows10.0.19041.0 -t:Run       # Windows
dotnet build ApexPost.Courier -f net10.0-android -t:Run                    # running Android emulator

# Everything except the MAUI app (no MAUI workload needed)
dotnet test ApexPost.Server.slnf
```

### Demo script (3-5 minute video)

1. Start the API. In Swagger UI, show 401 (no token), then Authorize with a Courier token and show
   `DELETE` returning 403 and `GET` returning 200. Optionally show `expired=true` giving a 401 "expired" message.
2. Start the app and use **Login**: "Get development token" for `courier-007`.
3. Turn on **airplane mode** in the emulator. Save two **New Scan** entries (create the shipment first as Admin in
   Swagger). The banner says no network, and **Pending** shows 2.
4. Turn airplane mode off. `SyncCoordinator` uploads automatically, **Pending** drops to 0 and **Sync Status**
   shows the time.
5. **Stop the API** and save a scan, then press "Sync now" a few times. The retries are logged, the circuit opens,
   and the banner reads **"Server unavailable â€“ working offline"**. Start the API again: after 20 s the circuit
   goes half-open, then closed, and the scan syncs.

See [SECURITY.md](SECURITY.md) and [RESILIENCE.md](RESILIENCE.md).

---

# Week 5: testing strategy

The strategy (what we test, at which level and why) is in [TESTING-STRATEGY.md](TESTING-STRATEGY.md). In short:

| Project | Level | What it covers | Needs |
|---|---|---|---|
| `ApexPost.UnitTests` | Unit (~55%) | Domain rules (status machine, weights, value objects), API request validation, services with **Moq** and in-memory fakes, outbox decisions, typed API client with a fake `HttpMessageHandler` (200, 404, 500, timeout, cancellation, ProblemDetails) | Nothing: no database, files or network |
| `ApexPost.IntegrationTests` | Integration (~35%) | EF Core repositories, constraints, stored procedures and migrations on a disposable SQL Server database; MVC app and API end to end with `WebApplicationFactory` and test JWTs; Couchbase Lite repository, listener and device sync | SQL Server LocalDB (installed with Visual Studio) |
| `ApexPost.Specs` | BDD (~10%) | Reqnroll features in business language: `ShipmentCreation.feature` (API level), `ScanSync.feature` (service level) | SQL Server LocalDB |

## Running the tests

```powershell
dotnet test ApexPost.Server.slnf                       # everything (MAUI app excluded)
dotnet test ApexPost.UnitTests                         # seconds, no set-up at all
dotnet test ApexPost.IntegrationTests                  # SQL Server LocalDB + Couchbase Lite
dotnet test ApexPost.Specs                             # Reqnroll scenarios
dotnet test ApexPost.UnitTests --filter "FullyQualifiedName~StaleScanRuleTests"   # the TDD rule only

# Full run with TRX files, a log, and the summary below written into this README (add -Coverage for line coverage)
powershell -ExecutionPolicy Bypass -File scripts/run-tests.ps1
```

All three projects appear in Visual Studio **Test Explorer** (Test > Test Explorer > Run All). Nothing has to be
set up by hand: each SQL Server test fixture creates its own database (`ApexPostDb_Test_<guid>`,
`ApexPostDb_ApiTest_<guid>`, `ApexPostDb_Specs_<guid>`), applies the real migrations and drops it at the end.
Another server can be used with `$env:APEXPOST_TEST_SQLSERVER = "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True"`.
No secrets are needed: the test JWT signing key is random per run.

## Test results

<!-- test-results:start -->
Last run: 2026-11-15 10:49 UTC on DESKTOP-FB55L35, `scripts/run-tests.ps1`. Full log: [docs/test-results/test-output.txt](docs/test-results/test-output.txt).

| Project | Passed | Failed | Skipped | Total | Duration |
|---|---|---|---|---|---|
| ApexPost.UnitTests | 302 | 0 | 0 | 302 | 1 s |
| ApexPost.IntegrationTests | 148 | 0 | 0 | 148 | 28 s |
| ApexPost.Specs | 13 | 0 | 0 | 13 | 1 s |
| **All** | **463** | **0** | **0** | **463** | |
<!-- test-results:end -->

## TDD, BDD and Copilot evidence

- **TDD:** commits `RED:`, `GREEN:` and `REFACTOR:` on `week5-testing`, explained in [TDD-LOG.md](TDD-LOG.md).
- **BDD:** `ApexPost.Specs/Features/*.feature`, with the Background, Scenario Outlines, hooks
  (`Support/Hooks.cs`) and context classes (`Support/ShipmentApiContext.cs`, `Support/CourierSyncContext.cs`).
- **Copilot:** prompts, review decisions and the break-the-code proof in [COPILOT-LOG.md](COPILOT-LOG.md)
  (`scripts/prove-tests-fail.ps1`).
