# Week 2 assessment evidence

This index points reviewers from each assignment area to the implementation and verification evidence. It records the current design and its limits; it does not claim benchmark results that were not collected.

| Assessment area | Implementation evidence | Verification evidence |
| --- | --- | --- |
| Domain rules and mixed checkout | `ApexPost.Domain/Entities`, `ApexPost.Application/Services/CounterTransactionService.cs`, `MoneyOrderService.cs`, and `ReportingService.cs` | `ApexPost.Tests/BusinessRuleTests.cs`, `CounterTransactionReversalTests.cs`, and `Reporting/ReportingServiceTests.cs` |
| Advanced C# techniques | `DESIGN.md`, “SOLID mapping and advanced C# usage”; examples include `Money`, `Weight`, and `Sku` value objects, C# 14 extension members, pattern matching, generic JSON storage/event handlers, delegates, events, async cancellation, `Task.WhenAll`, `SemaphoreSlim`, `ValueTask`, and the catalogue cache | `ValueObjectTests.cs`, `ProductCatalogueCacheTests.cs`, `CourierTrackingClientTests.cs`, and the relevant domain/service tests |
| MVC workflows | `ApexPost.Web/Controllers` and `ApexPost.Web/Views`; Auditors have a cross-branch overview, while the standard report remains branch-scoped | `ApexPost.Tests/WebIntegrationTests.cs` covers login redirects, role restrictions, auditor overview, safe MVC audit records, and branch-scoped label access |
| Middleware and security | `ApexPost.Web/Program.cs` and `ApexPost.Web/Middleware`; forwarded headers run before request processing and trust only configured proxy IPs | `WebIntegrationTests.cs` covers security headers, API-key and IP behavior, trusted/untrusted forwarded addresses, login limiting, correlation IDs, and maintenance mode |
| Action filters and audit | `ApexPost.Web/Middleware/MvcActionFilters.cs`, Application events, and generated `AUDIT-SAMPLE.json` entries | `Mvc_audit_filter_records_safe_product_changes` sends real MVC POST requests through `WebApplicationFactory`, verifies audit metadata, and can refresh the sample file; entries omit request bodies and raw identity values |
| Persistence and seed data | `ApexPost.Infrastructure/Repositories` and `ApexPost.Infrastructure/Seed/DemoDataSeeder.cs` | `ApexPost.Tests/Infrastructure/JsonPersistenceReliabilityTests.cs` covers missing/malformed files, write failure, concurrent audit appends, and repeatable seeding |
| Architecture decisions | `docs/adr/` and the architecture, domain, middleware, and filter sections of `DESIGN.md` | ADRs record context, decision, alternatives, and consequences; the forwarded-header decision is ADR 0028 |

## Seed data counts

Development seeding creates five branches, 20 staff accounts across all four roles, 30 products, ten shipping zones, Standard/Express/Registered rates for the seeded zones, and 40 historical counter transactions (36 Paid, two Voided, two Refunded). It also seeds ten money orders and five successful identity verification records for the auditor view. The seed is conditional and deterministic; repeated seeding is covered by `Development_seed_can_run_twice_without_duplicates`.

## Performance decisions and limits

`ReportingService` loads eight independent repository collections concurrently with `Task.WhenAll`. `JsonCollectionStore<T>`, the audit repository, and the notification repository use `SemaphoreSlim` to serialize writes within one process. Application mutation gates span the full read/modify/write path for sales, reversals, stock adjustments, and money orders; `Concurrent_counter_sales_preserve_every_receipt_and_stock_change` checks that 20 overlapping checkouts retain all receipts and stock changes. Login and partner requests use separate rate-limit policies. `SimulatedCourierTrackingClient` bounds concurrent lookups, distinguishes caller cancellation from its timeout, and is covered by dedicated tests. These are bounded behaviors, not a throughput benchmark: no benchmark numbers are claimed.

The demo has only a few dozen branches/products and a small local JSON dataset. The singleton catalogue service caches an immutable product snapshot and refreshes it after create/activation changes; `Catalogue_reads_are_cached_and_mutations_refresh_the_snapshot` covers cache invalidation. This demonstrates the requested `ValueTask` pattern, not a measured throughput gain. Courier tracking is simulated and does not measure a real provider.

JSON write gates protect one process only. Separate files do not provide a cross-file database transaction, and the audit event write remains best-effort after a business write. These limits are described in `DESIGN.md` and the relevant ADRs.

## Reproducible checks

Verified on 2026-10-30: `Directory.Build.props` sets .NET 10, C# 14, and nullable reference types; `dotnet build ApexPost.slnx --no-restore` succeeded with zero warnings, and `dotnet test ApexPost.slnx --no-build --no-restore` passed all 70 tests.

From the repository root, run:

```powershell
dotnet build ApexPost.slnx
dotnet test ApexPost.slnx
```

The README contains local run instructions, demo accounts, user-secrets setup, and a partner tracking request. The tests use temporary data directories and do not require production credentials.
