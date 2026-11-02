# Week 3 brief -> evidence map

Every requirement in the Week 3 brief (SQL Server + EF Core 10), with the file or test that proves it.
Couchbase / offline synchronisation is excluded from this assignment (it was listed in the brief by mistake), so it is not implemented.

## Part A - Relational schema (15)

| Requirement | Evidence |
|---|---|
| Own database `ApexPostDb` | README section 2; the tests use a throw-away `ApexPostDb_Test_<guid>` |
| 3NF tables (Branches, Counters, Staff, Roles, Products, ProductCategories, BranchStock, StockAdjustments, ShippingZones, ShippingRates, Shipments, MoneyOrders, IdentityVerifications, CounterTransactions, TransactionLines, Payments, AuditEntries) | `Persistence/Configurations/*.cs`; `sql/ApexPostDb_schema.sql`; ADR 0041 |
| PK/FK + delete behaviour and why | DESIGN.md section 2 "Delete behaviours"; ADR 0041 |
| UNIQUE (BranchCode, Sku, staff email) + CHECK (qty >= 0, Amount > 0, Status values, weight <= 30 kg) + DEFAULT + audit columns | Configurations; `sql/assignment3-compliance.sql` lists them from the catalog; `ApexPostDbContextModelTests` |
| rowversion on BranchStock and CounterTransactions | `AuditShadowProperties.ConfigureRowVersion`; also on Products |
| decimal(18,2), datetime2/datetimeoffset UTC, varchar codes / nvarchar names | Configurations; ADR 0041 section 8 |
| Identity documents masked only, in domain and schema | `IdentityDocumentMask` (domain), `varchar(16)` + CHECK `LIKE '****%'`; test `Identity_documents_can_only_be_recorded_masked` |
| ER diagram, three anomalies, idempotent script | DESIGN.md section 2; `sql/ApexPostDb_schema.sql` |

## Part B - SQL (10)

| Requirement | Evidence |
|---|---|
| Five reports with INNER/LEFT JOIN, GROUP BY, HAVING | `sql/reporting-queries.sql` (1 daily retail vs services, 2 branches with no sales LEFT JOIN, 3 top-10 per region, 4 zone x service, 5 money orders + ID checks, 5b HAVING) |
| T-SQL transaction with TRY/CATCH, XACT_ABORT, XACT_STATE, THROW | `sql/stock-sale-transaction.sql`; docs/sql-transactions-and-concurrency.md section 1 |
| Blocking demo with observation + isolation level | `sql/blocking-demo.sql`, `scripts/run-blocking-demo.ps1`, `sql/performance-evidence/blocking-demo-output.txt`; doc section 2-3 |
| Deadlock avoidance | doc section 4; ADR 0046 |

## Part C - Indexes (15)

| Requirement | Evidence |
|---|---|
| 4 workloads, baseline actual plan + IO/TIME, candidate index with key-order/INCLUDE reasoning, re-measure at volume, write overhead + storage | PERFORMANCE.md; `sql/performance-evidence/*.sqlplan` + `results.json`; `scripts/measure-performance.ps1` |
| Non-SARGable fix with plans | PERFORMANCE.md "SARGability"; `sargable-before/after.sqlplan` |
| Estimated vs actual investigation | PERFORMANCE.md "Statistics"; `stats-*.sqlplan` |
| Accepted indexes in the schema | migration `Week3MeasuredIndexes`; ADR 0042 |

## Part D - Stored procedures (10)

| Requirement | Evidence |
|---|---|
| `usp_DailyBranchSales`, `usp_LowStockReport` (optional params, RECOMPILE reasoning), `usp_AdjustStock` (TRY/CATCH, XACT_ABORT, history row) | `Persistence/Migrations/StoredProcedureSql.cs`, `sql/stored-procedures.sql` |
| Called with parameterized APIs only | `SqlServerStoredProcedureService` (`SqlQuery`/`ExecuteSql` with interpolation -> parameters); test `Stored_procedures_return_the_expected_results` |
| Least-privilege note | `sql/least-privilege.sql`; ADR 0043 |
| ADR on why each is a procedure | ADR 0043 |

## Part E - EF Core (15)

| Requirement | Evidence |
|---|---|
| DbContext, IEntityTypeConfiguration per entity, converters (Money, Sku, BranchCode, Weight), owned types | `ApexPostDbContext`, `Configurations/`, `Conversions/ValueObjectConverters.cs`, `ShippingAddress`/`ParcelDimensions` owned |
| Migrations: initial + follow-ups, reviewed, idempotent script, no EnsureCreated in app code | `Persistence/Migrations/` (`Week3SchemaCompliance` hand-reviewed data move; `Week3MeasuredIndexes`); `EnsureCreated` is not used anywhere |
| Seed: 5 branches, 20 staff, 30 products, 10 zones/rates, 200+ transactions | `DemoDataSeeder` (240 transactions); test `Seeded_database_meets_the_assignment_volumes` |
| AsNoTracking projections, Include only for aggregates, AsSplitQuery, no N+1 (logging) | `EfReportingQueries`, `EfCounterTransactionRepository.AggregateQuery`; `appsettings.Development.json` logs `Database.Command`; test `Reading_recent_transactions_uses_a_fixed_number_of_commands_not_one_per_row` |
| Sale = one SaveChanges; ExecuteUpdate price revision | `EfUnitOfWork`, `CounterTransactionService`; `EfPriceRevisionService`; tests `Counter_sale_posted_through_mvc_is_persisted_in_sql_server`, `Price_revision_is_set_based_and_audited_in_one_transaction` |
| Concurrency: last unit cannot go negative, 409 | ADR 0046; tests `Two_clerks_selling_the_last_unit_cannot_drive_stock_negative`, `Concurrency_conflict_on_stock_returns_409` |
| Explicit transaction + execution strategy when mixing SaveChanges with procs/ExecuteUpdate | `SqlStockAdjustmentCommand`, `EfPriceRevisionService`, `EfUnitOfWork.ExecuteInTransactionAsync` |
| ToQueryString for 3+ queries in PERFORMANCE.md | PERFORMANCE.md "Generated SQL"; `docs/generated-sql-samples.md`; test `Generated_sql_for_the_main_queries_is_captured_with_ToQueryString` |

## Part F - Patterns (20)

| Requirement | Evidence |
|---|---|
| Dependency direction | DESIGN.md section 1; project references |
| Specific repositories + IAuditStore; stage only; no IQueryable; IUnitOfWork with ExecuteInTransactionAsync; one commit per use case; all scoped | `Application/Abstractions/I*Repository.cs`, `IAuditStore`, `IUnitOfWork`; `Persistence/Repositories/`; `AddApexPostSqlServer`; `UnitOfWorkSpyTests` (7 tests: commit once / never on failure) |
| Rich domain, shadow properties for rowversion/audit | `Domain/Entities`; `AuditShadowProperties`; ADR 0045 |
| Separate persistence entity + mapper with ADR | `IdentityVerificationMapper`; ADR 0045 |
| Request DTOs with validation, view-model DTOs, explicit mapping, over-posting proof | `Web/Models/*InputModel.cs` (`[Bind]`), `*PageModel.cs`; test `Over_posting_status_id_and_audit_fields_has_no_effect`; ADR 0048 |
| Query/command separation, no separate DB (stated) | `IReportingQueries` vs services + UoW; ADR 0048 |
| Week 2 business-rule tests pass unchanged | `BusinessRuleTests.cs`, `CounterTransactionReversalTests.cs` run in the full suite |

## Part G - Integration

| Requirement | Evidence |
|---|---|
| MVC screens on SQL Server (dashboard, catalogue/stock, transactions/voids/refunds, money orders/ID, reports) | Controllers + `AddApexPostSqlServer`; `SqlServerWebTests` |
| Reports via query services / procedures (4 reports) | `ReportsController.Operations`, `ReportsApiController`, `EfReportingQueries` |
| Audit filter -> AuditEntries via IAuditStore, does not break the transaction | `MvcAuditActionFilter`, `EfAuditStore` (own context); ADR 0047 |
| Problem Details 409/409/404/400 | `SafeProblemDetailsExceptionHandler`; test `Errors_are_returned_as_problem_details` |
| Connection string from user-secrets; no credentials committed | README section 1; `appsettings*.json` contain none |

## Part H - Tests (15, with docs/ADRs/Git)

| Requirement | Evidence |
|---|---|
| Unit: stock, ID verification, weight, voids/refunds; services with fakes + spy UoW | `BusinessRuleTests`, `CounterTransactionReversalTests`, `ValueObjectTests`, `UnitOfWorkSpyTests` |
| SQL integration on a disposable DB: mapping, rollback, 409, procs | `SqlServerIntegrationTests` + `SqlServerTestDatabase` fixture |
| WebApplicationFactory on a test DB: sale persists, void needs manager, report totals, Problem Details | `SqlServerWebTests` |
| 7+ new ADRs | 0041-0049 (nine) |
| Git hygiene (feature branches, PRs, meaningful commits) | **To do by the student:** push this work from a feature branch and merge it with a PR (see the final summary for commands) |
