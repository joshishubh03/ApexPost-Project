# 0043: Which operations are stored procedures, and why

- Status: Accepted (supersedes 0035)
- Date: 2026-11-03

## Decision

Three procedures, created by migration `Week3SchemaCompliance` and kept in `sql/stored-procedures.sql`. All use
`CREATE OR ALTER`, `SET NOCOUNT ON`, validate every parameter and raise numbered errors with `THROW`
(50001-50013; 50004 "branch not found" is mapped to HTTP 404). They are called from EF Core only through
`Database.SqlQuery<T>($"EXEC ... {param}")` - the interpolated values become `SqlParameter`s, never text.

| Procedure | Why a procedure and not LINQ ("faster" is not the reason) |
|---|---|
| `usp_DailyBranchSales(@BranchCode, @Date)` | It is a **shared contract** used by the MVC report, the JSON API and by head-office tools in SSMS/Excel that do not run .NET. Keeping the retail/service classification (by product category) in one place guarantees every consumer gets the same totals; the day window is written SARGably once. |
| `usp_LowStockReport(@BranchCode = NULL, @Threshold)` | The optional-parameter "catch-all" shape is exactly where a cached plan goes wrong (one branch wants a seek, all branches want a scan). A procedure lets us pin the plan policy explicitly with `OPTION (RECOMPILE)` and document it; LINQ would produce a different SQL text per filter combination, scattered across callers. |
| `usp_AdjustStock(@BranchCode, @Sku, @Delta, @Reason, @ChangedBy)` | A **batch / back-office operation** (deliveries, stock counts) that must be atomic and safe without the app: guarded `UPDATE ... WHERE QuantityOnHand + @Delta >= 0` plus the `StockAdjustments` history row in one `TRY...CATCH` / `XACT_ABORT` transaction. It joins an outer transaction when called from EF Core (`@@TRANCOUNT` check), so the app can combine it with `ExecuteUpdate` and `SaveChanges` (SqlStockAdjustmentCommand). The app login only needs EXECUTE (ownership chaining). |

## OPTION (RECOMPILE) vs alternatives for `usp_LowStockReport`

- **Chosen: `OPTION (RECOMPILE)`** - the report runs a few times a day; a compile of a 3-table join costs
  ~1 ms of CPU, while a plan sniffed for "one branch" and reused for "all branches" (or the reverse) costs
  thousands of reads. RECOMPILE also embeds the parameter values so `@BranchCode IS NULL OR ...` collapses.
- Rejected: `OPTIMIZE FOR UNKNOWN` (one average plan, wrong for both cases); two procedures / IF-ELSE branches
  (duplicated SQL); dynamic SQL with `sp_executesql` (more moving parts and permissions for no gain at this call rate).

## Consequences

Reports that are plain projections (shipping volume, money orders per branch, branches with no sales) stay in
LINQ query services (ADR 0044) - they have a single consumer and need no special plan handling.
