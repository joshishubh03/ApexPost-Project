# ApexPost testing strategy (Week 5)

This document was written **before** any new Week 5 test. It says what we test, at which level, with which tools,
and why. The goal is not the largest number of tests. The goal is evidence that the parts of ApexPost that are
most likely to break, or most expensive when they break, are covered by the cheapest test that can catch the
problem.

The work is organised in six groups. Each group has one job and its own deliverables:

| Group | Topic | Deliverable |
|---|---|---|
| 0 | Strategy | This document (risk map, pyramid, framework, existing-test review, data access, what we do not test) |
| 1 | Unit testing | `ApexPost.UnitTests`: domain, validation, services and client logic, with no I/O |
| 2 | Mocking and isolation | Moq tests for service dependencies, fake `HttpMessageHandler` tests for the typed API client, real databases for repositories, one end-to-end API test |
| 3 | TDD | One new rule built test-first, with `RED:` / `GREEN:` / `REFACTOR:` commits and [TDD-LOG.md](TDD-LOG.md) |
| 4 | BDD | `ApexPost.Specs` with Reqnroll: two business-language features, Background, Scenario Outline, hooks and context injection |
| 5 | Copilot | Copilot-drafted tests reviewed by a person, recorded in [COPILOT-LOG.md](COPILOT-LOG.md), with proof that they can fail |
| 6 | Results | Test run summary in the README and the reflection at the end of this document |

---

## 1. Risk map

Ranked by **likelihood x cost**. "Cost" is what happens in production if the behaviour breaks. The last column
says where the risk is tested; the cheapest level that can really catch the problem wins.

| # | Risk | Why it is likely | Cost if it breaks | Main test level |
|---|---|---|---|---|
| R1 | **Shipment status transitions** (`Created -> PickedUp -> InTransit -> OutForDelivery -> Delivered / Failed`) | Every new feature (PATCH, scans, deletes) touches the status machine; one wrong entry in the transition table lets a parcel go backwards | Wrong tracking for customers and partners; a "Delivered" parcel shown as in transit; disputes | Unit (`[Theory]` over the full transition table) + API integration (409) |
| R2 | **Scan de-duplication** (idempotent upload by `scan::{Guid}`) | Mobile networks drop responses, so the app retries; Polly retries too | Duplicate scans, wrong scan history, double status moves | Unit (service) + BDD `ScanSync.feature` + SQL Server (primary key) |
| R3 | **Duplicate tracking numbers** | Partners and the counter both create shipments; case differences (`apx-` vs `APX-`) | Two parcels share one number; tracking shows the wrong parcel | Unit (service) + BDD `ShipmentCreation.feature` + SQL Server unique index |
| R4 | **JWT role rules and the courierId check** | Every new endpoint needs the right `[Authorize]`; easy to forget | A courier deletes shipments, or uploads scans as another courier | API integration (401 / 403 with real JWT validation) |
| R5 | **Client retry behaviour** (retry only 408 / 5xx / network errors, never 4xx; circuit breaker) | Resilience settings are easy to change and hard to see | Retry storms against a failing server, or a 400 retried forever; battery drain | Unit with a fake `HttpMessageHandler` |
| R6 | **Stock concurrency** (two clerks selling the last unit) | Real concurrency only appears under load | Negative stock, money taken for goods that do not exist | SQL Server integration (rowversion) |
| R7 | **Offline outbox state** (Pending -> Synced / Failed; transient errors keep scans Pending) | Many paths: offline, rejected, duplicate, circuit open | Lost scans (data loss) or scans stuck forever | Unit (Moq) + integration (real Couchbase Lite) + BDD |
| R8 | **Validation rules** (tracking number format, weight 0.1-70 kg, phone, scan time not in future) | Regexes and limits get edited | Bad data in the system of record | Unit (`[Theory]` with boundary values) + API (400 ValidationProblemDetails) |
| R9 | **Error responses** (Problem Details, no stack traces outside Development) | A new exception type falls through to 500 | Information leak; clients cannot react | Unit (exception mapping) + API integration |
| R10 | **Identity documents stored masked** | Money orders are a regulatory area | Personal data breach | Unit (value object) + SQL Server CHECK constraint |

---

## 2. Test pyramid for ApexPost

```
             /\         BDD (ApexPost.Specs)            ~10%   business promises, end to end in one layer
            /  \        2 features: ShipmentCreation (API level), ScanSync (service level)
           /----\
          /      \      Integration (ApexPost.IntegrationTests)   ~35%   real SQL Server, real Couchbase Lite,
         /        \     WebApplicationFactory, EF model and migrations, scripts
        /----------\
       /            \   Unit (ApexPost.UnitTests)                  ~55%   domain rules, validation, services
      /______________\  with Moq / fakes, typed client with fake HttpMessageHandler. No I/O.
```

| Level | Project | What belongs here | What does not |
|---|---|---|---|
| **Unit** | `ApexPost.UnitTests` | Domain rules (status machine, weights, value objects), validation attributes, application services with Moq or in-memory fakes, outbox logic with mocked `IScanRepository` / `IShipmentUploader`, typed API client with a fake `HttpMessageHandler`, exception -> Problem Details mapping | Anything that opens a file, a socket or a database |
| **Integration** | `ApexPost.IntegrationTests` | EF Core repositories and stored procedures on a disposable SQL Server database, constraints, migrations and scripts, `WebApplicationFactory` tests of the MVC app and the API (JWT, 401/403, Problem Details, versioning, OpenAPI), Couchbase Lite repository and change listener on a temporary folder | Business rules that a unit test can prove faster |
| **BDD** | `ApexPost.Specs` | A few business scenarios written in business language, run as executable specifications | Exhaustive combinations (those are `[Theory]` rows in unit tests) |

**Why these proportions.** Most ApexPost rules are pure logic (status machine, weights, de-duplication decisions,
retry decisions), so they are cheap to test in microseconds. ApexPost is also unusually dependent on its
infrastructure: rowversion concurrency, stored procedures, CHECK constraints, JWT validation and an embedded
NoSQL store. Those can only be proved against the real thing, which is why the integration layer is larger
than in a textbook pyramid (35%, not 20%). BDD stays small: two features that a business owner can read, not a
second copy of the unit tests.

The proportions are by test method. The actual numbers after the work are in the README ("Test results").

---

## 3. Framework choice: xUnit (one framework for the whole solution)

| Criterion | xUnit | NUnit | Decision |
|---|---|---|---|
| Consistency with existing tests | All ~255 existing tests are xUnit 2.9.3 | Would mean rewriting them or running two frameworks | **xUnit** |
| Test isolation | A **new instance of the test class per test**; no shared `[SetUp]` fields leak between tests | One instance per fixture by default; state can leak between tests unless care is taken | xUnit makes the safe thing the default |
| Setup / teardown style | Constructor = setup, `IDisposable` / `IAsyncLifetime` = teardown; expensive shared resources (a SQL Server database) are explicit `ICollectionFixture`s | `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]` attributes | xUnit's style keeps setup next to the code that needs it; shared state has to be declared on purpose |
| Parameterised tests | `[Theory]` + `[InlineData]` / `[MemberData]` / `TheoryData<>` | `[TestCase]`, `[TestCaseSource]` | Equal |
| Reqnroll support | `Reqnroll.xUnit` (xUnit 2.8+) | `Reqnroll.NUnit` | Equal; we use `Reqnroll.xUnit` |
| Parallel execution | Test collections run in parallel by default; database tests are grouped in one collection so they share one database safely | Opt-in with `[Parallelizable]` | xUnit's default gives fast runs; collections control the exceptions |
| Team familiarity | Already used for 4 weeks | Not used | **xUnit** |

**Mocking:** Moq 4.20.72 (`Setup`, `ReturnsAsync`, `It.Is`, `It.IsAny`, `Verify` with `Times`).
**BDD:** Reqnroll 3.3.4 (`Reqnroll.xUnit`). There was no SpecFlow work to migrate.
**HTTP:** a hand-written `StubHandler : HttpMessageHandler` (simpler and more readable than mocking the protected
`SendAsync` with Moq).

---

## 4. Existing tests review

Every existing test class was reviewed. "Kept" means unchanged apart from the move to its new project.
"Modified" means the test changed; the reason is given. Nothing was deleted without a row here.

### From `ApexPost.Tests` (Weeks 1-3)

| Test class | Tests | Decision | New location | Reason |
|---|---|---|---|---|
| `BusinessRuleTests` | 26 | **Modified** (1 test removed) | UnitTests | Core counter, tax, shipping and money-order rules: exactly what unit tests are for. `DateTimeOffset.UtcNow` replaced by a fixed instant (determinism). `Concurrent_counter_sales_preserve_every_receipt_and_stock_change` **removed**: it ran 20 tasks against an in-memory fake with `Task.Delay`, so it proved the fake unit of work's semaphore, not ApexPost. Real concurrency is proved by `Two_clerks_selling_the_last_unit_cannot_drive_stock_negative` (SQL Server rowversion) and `Concurrency_conflict_on_stock_returns_409`. |
| `CachedAttributeValidatorTests` | 2 | Kept | UnitTests | Validation logic, no I/O |
| `CounterTransactionReversalTests` | 3 | **Modified** | UnitTests | Void/refund rules. Fixed instant instead of `DateTimeOffset.UtcNow` |
| `CourierShipmentDomainTests` | 7 | Kept | UnitTests | Status machine examples (R1). The exhaustive transition table, tracking-number formats and weight boundaries were added in a new class, `CourierShipmentRulesTests`, so the original examples stay readable |
| `CourierShipmentServiceTests` | 6 | **Modified** | UnitTests | State-based service tests with an in-memory repository: they prove outcomes (what was stored). Its in-memory repository and clock moved to `TestDoubles/` so the TDD tests and the BDD scenarios share them. Moq tests were added next to it to prove interactions |
| `CourierTrackingClientTests` | 3 | Kept | UnitTests | Concurrency limit of the simulated courier client; asserts a maximum, not a duration, so it is not timing-sensitive |
| `HttpCourierTrackingClientTests` | 4 | Kept | UnitTests | Fake handler, no network; timeouts use cancellation, not wall-clock assertions |
| `ProblemDetailsMappingTests` | 7 | Kept | UnitTests | Exception -> status mapping (R9) |
| `ProductCatalogueCacheTests` | 1 | Kept | UnitTests | Cache invalidation logic |
| `ReportSpecificationTests` | 1 | **Modified** | UnitTests | Fixed instant instead of `DateTimeOffset.UtcNow` |
| `ReportingServiceTests` | 1 | Kept | UnitTests | Report aggregation with fakes |
| `ApplicationServiceTests` | 11 | Kept | UnitTests | Services with fake repositories and a spy unit of work |
| `AuthenticationServiceTests` | 3 | Kept | UnitTests | Login rules |
| `BranchTests` | 7 | Kept | UnitTests | Branch rules |
| `UnitOfWorkExtensionsTests` | 5 | Kept | UnitTests | Nested use cases join the outer transaction |
| `UnitOfWorkSpyTests` | 7 | **Modified** | UnitTests | "Commit once, never on failure" with a hand-written spy. Kept: it reads well and covers every Week 3 service. Fixed instant instead of `UtcNow` |
| `ValueObjectTests` | 10 | Kept | UnitTests | Money, SKU, branch code, weight, document mask (R10) |
| `ApexPostDbContextModelTests` | 8 | Kept | IntegrationTests | Builds the real EF Core model (keys, lengths, constraints, rowversion). No database, but it exercises EF Core, so it is not a pure unit test |
| `CourierPersistenceModelTests` | 2 | Kept | IntegrationTests | Same reason (EF model for the Week 4 tables) |
| `MigrationScriptTests` | 5 | Kept | IntegrationTests | Generates migration scripts with EF Core and compares with the committed script |
| `PackageVersionTests` | 1 | Kept | IntegrationTests | Reads the repository's project files (exact package versions) |
| `SqlConstraintTests` | 9 | Kept | IntegrationTests | SQL Server rejects bad rows itself (CHECK / UNIQUE / FK) |
| `SqlScriptTests` | 9 | Kept | IntegrationTests | Hand-written SQL scripts really run |
| `SqlServerIntegrationTests` | 8 | Kept | IntegrationTests | Repository mapping, rollback, concurrency (R6), stored procedures, N+1 |
| `SqlServerWebTests` | 6 | **Modified** | IntegrationTests | MVC end to end on SQL Server. Uses a `WebAssemblyMarker` type instead of `Program`, because the integration project now hosts both the Web app and the API and both have a `Program` class |
| `WebIntegrationTests` | 17 | **Modified** | IntegrationTests | Security headers, rate limits, IP lists, roles, audit. Same `WebAssemblyMarker` change |

### From `ApexPost.Api.Tests` (Week 4 Parts B-D)

| Test class | Tests | Decision | New location | Reason |
|---|---|---|---|---|
| `ShipmentsApiTests` | 11 | Kept | IntegrationTests | Verbs, status codes, Location header, 409s (R1, R3) |
| `ScansApiTests` | 5 | Kept | IntegrationTests | Idempotent upload over HTTP (R2) |
| `SecurityApiTests` | 11 | Kept | IntegrationTests | JWT 401/403, courierId check, partner key (R4) |
| `VersioningApiTests` | 5 | Kept | IntegrationTests | v1/v2 contracts and deprecation headers |
| `OpenApiDocumentTests` | 7 | Kept | IntegrationTests | Generated OpenAPI documents |
| `ExceptionMappingTests` | 2 | Kept | IntegrationTests | 500 through the global handler without a stack trace (R9) |

### From `ApexPost.Courier.Offline.Tests` (Week 4 Part A)

| Test class | Tests | Decision | New location | Reason |
|---|---|---|---|---|
| `ArchitectureTests` | 3 | Kept | UnitTests | Reflection only, no I/O |
| `CouchbaseScanRepositoryTests` | 11 | Kept | IntegrationTests | Real Couchbase Lite: CRUD, SQL++ parameters, indexes |
| `OutboxServiceTests` | 6 | Kept | IntegrationTests | Outbox against the real store. Moq unit tests were added for the decision logic (R7) |
| `ChangeListenerTests` | 4 | **Modified** | IntegrationTests | Real listener tokens. Fixed instants instead of `DateTimeOffset.UtcNow` |

### From `ApexPost.Courier.Core.Tests` (Week 4 Part D)

| Test class | Tests | Decision | New location | Reason |
|---|---|---|---|---|
| `ResilienceTests` | 8 | Kept | UnitTests | Retry / no retry on 4xx / circuit breaker / timeouts with a fake handler (R5) |
| `AuthAndJsonTests` | 7 | Kept | UnitTests | Auth header handler, 401 handling, JSON tolerance |
| `OfflineSyncTests` | 3 | **Modified** | IntegrationTests | Real Couchbase Lite + fake HTTP server: end-to-end device sync. Fixed instants instead of `DateTimeOffset.UtcNow` |

### New in Week 5

| Test class | Project | Level | Purpose |
|---|---|---|---|
| `CourierShipmentRulesTests` | UnitTests | Unit | All 36 status pairs (`[MemberData]`), forbidden transitions, tracking-number formats, weight boundaries 0.09 / 0.1 / 70 / 70.01, exceptions (R1, R8) |
| `ApiRequestValidationTests` | UnitTests | Unit | Request DTO rules: tracking number, weight, E.164 phone, required fields, `[NotInFuture]` with an injected clock (300 s vs 301 s), Delivered needs a signature, 1-100 scans (R8) |
| `CourierShipmentServiceMockTests` | UnitTests | Unit (Moq) | Service interactions: `Add` / `Update` / `Remove` / `AddScan` staged once or never, normalisation before the duplicate check, ownership checked before any unit of work (R1-R4) |
| `OutboxServiceMockTests` | UnitTests | Unit (Moq) | Outbox decisions: Synced / Failed / stays Pending, batches of 20, stops when the server is down or returns nothing (R7) |
| `ApexApiClientHttpTests` | UnitTests | Unit (fake handler) | Typed client: 200 + JSON, 404, 500 with retries, ProblemDetails, per-attempt timeout, caller cancellation (R5) |
| `StaleScanRuleTests` | UnitTests | Unit (TDD) | The new stale-scan rule, see [TDD-LOG.md](TDD-LOG.md) |
| `ShipmentCreation.feature`, `ScanSync.feature` | Specs | BDD | Section 6 |

The required end-to-end API test with a test database and test authentication is
`ShipmentsApiTests.Create_returns_201_with_a_location_header_and_the_shipment` (plus the rest of
`ApexPost.IntegrationTests/Api`): `WebApplicationFactory`, a disposable SQL Server database migrated with the real
migrations, and JWTs signed with a random per-run test key, validated by the real JWT bearer handler.

**Naming.** Existing tests use full-sentence names (`Delete_after_pickup_is_refused`). They are kept: they are
clear, and renaming 250 tests would add no value and break `git blame`. **New Week 5 tests use
`Method_Scenario_ExpectedResult`** (for example `UploadScansAsync_ScanOlderThanSevenDays_IsRejected`) and mark the
Arrange / Act / Assert sections with comments.

---

## 5. Data access strategy

| Approach | Used for | What it proves | What it does **not** prove |
|---|---|---|---|
| **Moq** mock of a repository interface (`ICourierShipmentRepository`, `IScanRepository`, `IShipmentUploader`) | Unit tests of services and the outbox | The service's decisions and its interactions: what it asks for, what it stages, that it commits once or never | That the query is correct SQL, that constraints hold, mapping, transactions, concurrency. A mock returns whatever we told it, so a wrong assumption about the real repository goes unnoticed |
| **Hand-written in-memory fakes** (existing `InMemory*Repository`, `SpyUnitOfWork`) | State-based service tests | Outcomes: what ends up stored after a use case | Same limits as Moq; the fake can drift from the real repository. The SQL Server repository tests act as the contract check |
| **SQLite in-memory** | **Not used** | - | ApexPost's schema depends on SQL Server features SQLite does not have: `rowversion` concurrency, T-SQL stored procedures, filtered indexes, `datetimeoffset`, `CHECK ... LIKE` patterns and raw SQL in migrations. SQLite would also force `EnsureCreated` instead of the real migrations. The concurrency test would pass on SQLite and fail on SQL Server: false confidence |
| **Disposable SQL Server database** (`ApexPostDb_Test_<guid>` / `ApexPostDb_ApiTest_<guid>`, LocalDB by default) | Repositories, stored procedures, constraints, migrations, API and MVC end to end | The real migrations, real SQL translation, constraints, transactions, rowversion conflicts and stored procedures | Production data volumes and query plans (covered by `PERFORMANCE.md`), network latency, a different server version or collation |
| **Couchbase Lite in a temporary folder** | Courier repository, change listener, outbox end to end | Real document storage, SQL++ with parameters, value indexes, listener tokens | Several devices, replication, mobile file-system limits |
| **Fake `HttpMessageHandler`** | Typed API client and Polly pipeline | Request building, JSON, ProblemDetails handling, retry / circuit / timeout decisions | The real server's behaviour (covered by the API integration tests) and TLS |

**Why we never mock `DbContext` / `DbSet` for query logic.** A mocked `DbSet` runs LINQ **in memory** (LINQ to
Objects), not through EF Core's SQL translation. Such a test passes when:
- the query cannot be translated (EF throws at run time);
- string comparison is case-sensitive in C# but not in SQL Server, or `null` behaves differently;
- `Include` / `AsSplitQuery` / `AsNoTracking` are ignored;
- a unique index or CHECK constraint would reject the row;
- a `rowversion` conflict would happen;
- the code uses `ExecuteUpdate`, `FromSql` or a stored procedure, which cannot be mocked meaningfully.

The test would then describe our mock, not our database, and couple the test to the implementation. So
repositories are tested only against a real SQL Server test database, and services are tested against the
**repository interface**, which is the boundary we own.

---

## 6. BDD levels

| Feature | Level | Why |
|---|---|---|
| `ShipmentCreation.feature` | **API** (`WebApplicationFactory` + disposable SQL Server database + test JWT) | The business promises here are HTTP promises: "created with a Location", "rejected as a duplicate" (409), "invalid weight" (400). Only the real pipeline (model validation, exception handler, unique index) proves them together |
| `ScanSync.feature` | **Service** (real `OutboxService` + Couchbase Lite in a temp folder, uploading through `CourierShipmentService` in-process) | The rules are about device state and server de-duplication, not HTTP. Running both sides in-process keeps each scenario fast and deterministic, with no network. The HTTP contract of the upload is already covered by `ScansApiTests` |

---

## 7. Determinism rules

- No `DateTime.Now` / `UtcNow` in unit tests: fixed instants or a fixed `TimeProvider`.
- Production code that makes time-based decisions takes a `TimeProvider` (the courier service already did; the new
  TDD rule uses it).
- API integration tests run against the real system clock, exactly like production. Their scan times are minutes
  away from any limit (the 5-minute future allowance, the 7-day age limit), so they cannot flip.
- No test depends on another test or on execution order. Database tests create their own rows with unique keys;
  each test database is created and dropped by its fixture.
- No real network: HTTP goes through a fake handler or `WebApplicationFactory`'s in-memory server.

---

## 8. What we do not test, and why

| Not tested | Why |
|---|---|
| MAUI pages, navigation, the Windows/Android app shell | Out of scope (brief). All app logic lives in `ApexPost.Courier.Core`, which is unit-tested |
| Razor views' HTML and CSS, browser behaviour | Out of scope (no browser testing). MVC behaviour is tested over HTTP |
| Generated code (`ApexPost.Client/Generated`, EF migration designers) | Generated by tools; we test the contract it is generated from (OpenAPI document tests) |
| `DemoDataSeeder` contents | Demo data only; the seeder runs in every SQL Server test, so a broken seeder fails those tests |
| Third-party libraries (Polly, EF Core, Couchbase Lite, Asp.Versioning) | We test our configuration and use of them, not the libraries |
| Performance and load | Measured separately with real execution plans (`PERFORMANCE.md`); unit tests are not a load test |
| Real external services (courier provider, identity provider) | Not allowed in tests; simulated or faked |

---

## 9. How to run

```powershell
dotnet test ApexPost.Server.slnf                     # everything (MAUI app excluded)
dotnet test ApexPost.UnitTests                       # fast, no database (seconds)
dotnet test ApexPost.IntegrationTests                # SQL Server LocalDB + Couchbase Lite
dotnet test ApexPost.Specs                           # Reqnroll scenarios
```

All three projects appear in Visual Studio Test Explorer. SQL Server tests use `(localdb)\MSSQLLocalDB` (installed
with Visual Studio) unless `APEXPOST_TEST_SQLSERVER` points to another server.

---

## 10. Reflection

**What the strategy caught.**

- **A real production bug.** The new boundary tests for the scan ID format (risk R8) failed on the first run:
  the validation message `"... scan::{Guid}."` is used by DataAnnotations as a format string, so `{Guid}` made
  `string.Format` throw. Any upload with a malformed scan ID would have returned **500** instead of a **400**
  with a clear message. The Week 4 API tests only sent valid scan IDs, so they never reached that path. Fixed by
  escaping the braces (`{{Guid}}`), with a regression test that checks the message text.

Reviewing the existing tests against the risk map also found problems that were invisible while the suite was green:

- `Concurrent_counter_sales_preserve_every_receipt_and_stock_change` gave false confidence. It ran 20 parallel
  sales against an in-memory fake and passed because of the fake unit of work's semaphore, not because of ApexPost.
  The real protection (SQL Server rowversion) is proved elsewhere, so the test was removed and the review table
  says why.
- Six test classes read the real clock (`DateTimeOffset.UtcNow`, 24 places). None had failed yet, but each one was
  a future flaky test (a midnight or month-end boundary). They now use fixed instants.
- Writing the BDD scan-sync scenario showed that two scans recorded in the same instant could be applied in either
  order by the server (it sorts by time, then by random ID). A fixed clock would have made the scenario flaky, so
  the phone uses a stepping clock. The determinism rule in section 7 caught it before it ever failed.
- Putting the MVC app and the API in one integration project broke the build (two global `Program` classes). The
  fix (an assembly marker type) is a small testability change in production code.
- The API's tracking-number regex and the domain's are two separate constants; a unit test now keeps them equal.
- The full run's numbers are in the README ("Test results").

**What we would change.**

- Run the SQL Server tests against a **container** (Testcontainers) in CI, so they do not depend on LocalDB being
  installed.
- Run the **same contract tests against the in-memory fake and the EF Core repository**, so the fakes cannot
  drift from the real behaviour.
- Replace the manual break-the-code script with a **mutation-testing tool** (Stryker.NET) on the domain and
  service projects.
- Rename the older sentence-style tests to `Method_Scenario_ExpectedResult` gradually, as each one is touched.

**Most confidence per minute.** Parameterised unit tests over the business rules: one `[Theory]` with the full
status table (36 rows) or a few weight boundaries took minutes to write, runs in milliseconds and pins down
exactly the rules that change most often. The `WebApplicationFactory` API tests come second. They are slower to
write and run, but one test exercises routing, JWT validation, model validation, the service, EF Core and SQL
Server together, so a wiring mistake anywhere in the chain shows up.

## 11. Viva notes (5 minutes)

1. **Pyramid:** about 55% unit, 35% integration, 10% BDD. ApexPost has more integration tests than a textbook
   pyramid because its key guarantees (rowversion, constraints, JWT, embedded store) only exist in real
   infrastructure.
2. **Framework:** xUnit. Consistency with ~255 existing tests, a new test-class instance per test (isolation),
   explicit fixtures for the expensive SQL Server database, `[Theory]`, and `Reqnroll.xUnit`.
3. **One mock and what it does not prove:** `CourierShipmentServiceMockTests` mocks `ICourierShipmentRepository`.
   It proves that a duplicate tracking number is refused **before** anything is staged (`Add` verified
   `Times.Never`). It does not prove that the database would also refuse it: the mock's `ExistsAsync` returns
   whatever we tell it. The unique index is proved by `SqlConstraintTests` and the 409 by `ShipmentsApiTests`.
4. **One Copilot test we rejected:** see [COPILOT-LOG.md](COPILOT-LOG.md), section 2.2.
