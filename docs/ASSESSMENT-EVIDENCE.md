# Week 2 assessment evidence

This index points reviewers from each assignment area to the implementation and verification evidence. It records the current design and its limits; it does not claim benchmark results that were not collected.

| Assessment area | Implementation evidence | Verification evidence |
| --- | --- | --- |
| Domain rules and mixed checkout | `ApexPost.Domain/Entities`, `ApexPost.Application/Services/CounterTransactionService.cs`, `MoneyOrderService.cs`, and `ReportingService.cs` | `ApexPost.Tests/BusinessRuleTests.cs`, `CounterTransactionReversalTests.cs`, and `Reporting/ReportingServiceTests.cs` |
| Advanced C# techniques | `DESIGN.md`, “SOLID mapping and advanced C# usage”; examples include records, pattern matching, generic JSON storage/event handlers, delegates, events, async cancellation, `Task.WhenAll`, and `SemaphoreSlim` | Unit tests exercise the injected low-stock predicate and the relevant domain/service rules |
| MVC workflows | `ApexPost.Web/Controllers` and `ApexPost.Web/Views`; the shared layout uses conventional MVC routes | `ApexPost.Tests/WebIntegrationTests.cs` covers login redirects, role restrictions, and branch-scoped label access |
| Middleware and security | `ApexPost.Web/Program.cs` and `ApexPost.Web/Middleware`; forwarded headers run before request processing and trust only configured proxy IPs | `WebIntegrationTests.cs` covers security headers, API-key and IP behavior, trusted/untrusted forwarded addresses, login limiting, correlation IDs, and maintenance mode |
| Action filters and audit | `ApexPost.Web/Middleware/MvcActionFilters.cs`, Application events, and `AUDIT-SAMPLE.json` | Integration and reporting tests exercise protected actions and report calculations; audit entries omit request bodies and raw identity values |
| Persistence and seed data | `ApexPost.Infrastructure/Repositories` and `ApexPost.Infrastructure/Seed/DemoDataSeeder.cs` | `ApexPost.Tests/Infrastructure/JsonPersistenceReliabilityTests.cs` covers missing/malformed files, write failure, concurrent audit appends, and repeatable seeding |
| Architecture decisions | `docs/adr/` and the architecture, domain, middleware, and filter sections of `DESIGN.md` | ADRs record context, decision, alternatives, and consequences; the forwarded-header decision is ADR 0028 |

## Seed data counts

Development seeding creates five branches, 20 staff accounts across all four roles, 30 products, ten shipping zones, Standard/Express/Registered rates for the seeded zones, and 40 historical counter transactions. The seed is conditional and deterministic; repeated seeding is covered by `Development_seed_can_run_twice_without_duplicates`.

## Performance decisions and limits

`ReportingService` loads eight independent repository collections concurrently with `Task.WhenAll`. `JsonCollectionStore<T>`, the audit repository, and the notification repository use `SemaphoreSlim` to serialize writes within one process. Login and partner requests use separate rate-limit policies. These are bounded behaviors, not a throughput benchmark: no benchmark numbers are claimed.

The demo has only a few dozen branches/products and a small local JSON dataset. It does not use an in-memory cache because there is no measured read bottleneck, and caching mutable JSON collections would require invalidation after each catalogue/rate change. The application has no real courier network call to bound; a future courier adapter should add a dedicated concurrency limit and timeout at that integration boundary.

JSON write gates protect one process only. Separate files do not provide a cross-file database transaction, and the audit event write remains best-effort after a business write. These limits are described in `DESIGN.md` and the relevant ADRs.

## Reproducible checks

From the repository root, run:

```powershell
dotnet build ApexPost.slnx
dotnet test ApexPost.slnx
```

The README contains local run instructions, demo accounts, user-secrets setup, and a partner tracking request. The tests use temporary data directories and do not require production credentials.
