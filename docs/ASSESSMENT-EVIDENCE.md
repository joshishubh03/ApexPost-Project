# Week 2 assessment evidence

This index points reviewers to the current implementation and checks. The stated limitations are part of the design evidence; benchmark figures are local samples, not capacity guarantees.

| Assessment area | Implementation evidence | Verification evidence |
| --- | --- | --- |
| Domain rules and mixed checkout | `ApexPost.Domain/Entities`, `ApexPost.Application/Services/CounterTransactionService.cs`, `MoneyOrderService.cs`, and `ReportingService.cs` | `ApexPost.Tests/BusinessRuleTests.cs`, `CounterTransactionReversalTests.cs`, and `Reporting/ReportingServiceTests.cs` |
| Advanced C# techniques | `DESIGN.md`, “SOLID mapping and advanced C# usage”; records/value objects, C# 14 extension members, pattern matching, generics, delegates, events, cached reflection validation, async cancellation, `Task.WhenAll`, `SemaphoreSlim`, and `ValueTask` cache | `ValueObjectTests.cs`, `CachedAttributeValidatorTests.cs`, `ProductCatalogueCacheTests.cs`, courier tests, and application service tests |
| MVC workflows | `ApexPost.Web/Controllers` and `ApexPost.Web/Views`; Auditors have a cross-branch overview while the standard report remains branch-scoped | `ApexPost.Tests/WebIntegrationTests.cs` covers login redirects, role restrictions, auditor overview, safe MVC audit records, and branch-scoped label access |
| Middleware and security | `ApexPost.Web/Program.cs` and `ApexPost.Web/Middleware`; forwarded headers trust only configured proxy IPs | `WebIntegrationTests.cs` covers security headers, API-key/IP behavior, trusted/untrusted forwarding, login limiting, correlation IDs, and maintenance mode |
| Action filters and audit | `ApexPost.Web/Middleware/MvcActionFilters.cs`, Application events, `AUDIT-SAMPLE.json`, and `scripts/GenerateAuditSample.ps1` | Web integration tests verify filter metadata. The script runs a local Development host, signs in, creates and deactivates a product, then exports only the safe audit records. |
| Persistence and seed data | `ApexPost.Infrastructure/Repositories/JsonMutationCoordinator.cs`, `JsonCollectionStore.cs`, and `DemoDataSeeder.cs` | `JsonPersistenceReliabilityTests.cs` covers missing/malformed files, write failure, concurrent appends, and repeatable seed data. `JsonMutationCoordinatorTests.cs` covers multiple coordinator instances, multi-file commit/rollback, reader blocking, and recovery from a prepared journal. |
| Architecture decisions | `docs/adr/` plus `DESIGN.md` | ADRs include context, decision, alternatives, and consequences. ADR-0030 covers recoverable JSON mutations; ADR-0031 the configurable courier contract; ADR-0032 cached attribute validation and measurement. |

## Seed data counts

Development seeding creates five branches, 20 staff accounts across all four roles, 30 products, ten shipping zones, Standard/Express/Registered rates for the seeded zones, and 40 historical counter transactions (36 Paid, two Voided, two Refunded). It also seeds ten money orders and five successful identity verification records for the auditor view. The seed is conditional and deterministic; `Development_seed_can_run_twice_without_duplicates` verifies repeated seeding.

## Persistence behavior

The JSON coordinator uses a shared OS file lock for repository reads and an exclusive lock for mutations. Business read-modify-write paths stage collection changes, retain backups, write a prepared journal, replace affected files, and mark the transaction committed. Startup recovery restores files for a prepared transaction or clears a completed journal. Tests use separate coordinator instances and verify that readers cannot observe a staged multi-file change before commit. Local filesystem lock behavior and same-volume rename are required; this does not provide distributed locking or database durability. Reports do not take one cross-collection point-in-time snapshot if a commit happens between their individual reads.

## Performance sample

`ApexPost.Benchmarks` is a dependency-free local timing harness. On 2026-10-30, .NET 10.0.13 on Windows 11 with four logical processors, 500 reads across five rounds of a 1,000-product collection gave a median of 2,814.280 ms for JSON read/deserialization and 140.684 ms for warm cached catalogue reads (20.00x ratio). Twelve synthetic 40 ms courier calls took 568.009 ms at concurrency one and 184.323 ms at concurrency three. The OS file cache was warm after warm-up; these timings are illustrative and do not represent a production deployment or real courier.

The singleton catalogue cache checks a small version marker so another local process can invalidate a stale snapshot. `Catalogue_reads_are_cached_and_mutations_refresh_the_snapshot` verifies cache invalidation. Login and partner requests use separate rate-limit policies. `HttpCourierTrackingClient` tests cover the configurable REST response, missing records, timeout versus caller cancellation, and bounded concurrency; the simulator remains the default without provider configuration.

## Reproducible checks

Verified on 2026-10-30: `Directory.Build.props` sets .NET 10, C# 14, and nullable reference types. `dotnet build ApexPost.slnx` succeeded with zero warnings; `dotnet test ApexPost.slnx` passed all 85 tests.

From the repository root:

```powershell
dotnet build ApexPost.slnx
dotnet test ApexPost.slnx
dotnet run --project ApexPost.Benchmarks --no-build
.\scripts\GenerateAuditSample.ps1
```

The audit sample script uses an isolated temporary JSON directory and temporary Development password, and removes both after its local MVC session. It does not change the project's user-secrets store. Regular automated tests use their own temporary directories and do not require provider credentials.
