# 0035: Stored procedures for reports and transactional stock adjustments

- Status: Superseded by 0043
- Date: 2026-11-01

## Context

The relational database is intended to be the system of record for the branch platform, but the application still needs a few carefully scoped database routines that are easier and safer to execute in SQL Server than by composing a large LINQ query or by scattering transactional logic through multiple application services. Reports such as daily branch sales and low-stock alerts are naturally set-based operations, while stock adjustments must be atomic and fully auditable.

## Decision

We keep read reports and a small set of transactional writes behind stored procedures with `CREATE OR ALTER` scripts and parameter validation. The targeted routines are:

- `dbo.usp_DailyBranchSales (@BranchCode, @Date)`
- `dbo.usp_LowStockReport (@BranchCode = NULL, @Threshold)`
- `dbo.usp_AdjustStock (@BranchCode, @Sku, @Delta, @Reason, @ChangedBy)`

Each procedure uses `SET NOCOUNT ON`, validates inputs before execution, and raises explicit `THROW` errors for invalid parameters or business violations. The stock-adjustment procedure wraps the update and audit-row insert in `BEGIN TRAN`/`TRY...CATCH` with `SET XACT_ABORT ON` so the stock decrement and the adjustment history remain consistent even when the transaction fails.

## Why these are procedures, and not LINQ queries

1. Reporting logic is set-based and naturally belongs in the database engine. A single query is easier to optimize, parameterize, and deploy as a versioned SQL artifact than to re-create in application code.
2. The low-stock report is sensitive to cardinality and optional branch filtering. `OPTION (RECOMPILE)` is chosen specifically for that workload because the branch filter can change the shape of the plan and the query is expected to be highly parameter-sensitive.
3. Stock adjustment is a business transaction that updates inventory and writes a stock-adjustment history row. The database is the correct place to guarantee atomicity and consistent validation instead of splitting the logic across multiple application writes.
4. The procedures remain parameterized and are invoked through EF Core using raw SQL or command APIs only; no string concatenation is used by the application.

## Consequences

- The database exposes stable, versioned SQL routines that can be reviewed, tested, and tuned separately from the .NET code.
- Report and adjustment logic is portable to SQL Server and easier to optimize with execution plans and indexing evidence.
- The application can still stay behind a repository and unit-of-work boundary while using stored procedures for specific high-value operations.
- The procedures add SQL Server-specific deployment requirements and should be treated as part of the assignment artifacts that accompany the application code.
