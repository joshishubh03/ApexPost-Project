# ApexPost Logistics - Week 3: SQL Server + EF Core 10

ApexPost is a branch counter system: product sales, stock, shipping, money orders with identity checks, and
reporting. In Week 3, SQL Server becomes the system of record:

- EF Core 10 maps a 3NF schema (20 tables) with migrations.
- Three stored procedures handle the set-based work.
- The application runs on Repository + Unit of Work with optimistic concurrency.
- Index choices are backed by measured execution plans.

Couchbase is out of scope for this assignment and is not used anywhere.

| Document | What it covers |
|---|---|
| [DESIGN.md](DESIGN.md) | Layers, ER diagram, normalization, delete behaviours, data access rules, SOLID |
| [PERFORMANCE.md](PERFORMANCE.md) | Workloads, indexes, before/after plans and numbers, write overhead, SARGability, statistics |
| [docs/sql-transactions-and-concurrency.md](docs/sql-transactions-and-concurrency.md) | T-SQL transaction, blocking demo output, isolation level, deadlock avoidance |
| [docs/generated-sql-samples.md](docs/generated-sql-samples.md) | SQL that EF Core generated for the main queries (`ToQueryString`) |
| [docs/adr/](docs/adr/) | Architecture decisions; 0041-0048 are the Week 3 ones |

## Solution layout

```
ApexPost.Domain          entities, value objects (Sku, BranchCode, Money, Weight, IdentityDocumentMask), invariants
ApexPost.Application     use-case services, ports (IUnitOfWork, repositories, IReportingQueries, IAuditStore), DTOs
ApexPost.Infrastructure  Persistence/ (ApexPostDbContext, Configurations/, Migrations/, Repositories/, Queries/,
                         StoredProcedures/, EfUnitOfWork, EfAuditStore), Seed/, JSON adapter (Weeks 1-2)
ApexPost.Web             MVC controllers + views, reports API, Problem Details, audit filter, composition root
ApexPost.Tests           unit, unit-of-work spy, SQL Server integration and WebApplicationFactory tests
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
dotnet test ApexPost.slnx
```

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

## Persistence modes

`Persistence:Provider` is `SqlServer` (default) or `Json`. The JSON adapter from Weeks 1-2 is kept for offline
demos; it has no atomic commit, so services use their compensation path (ADR 0030).
