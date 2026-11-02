# 0048: DTOs, explicit mapping, over-posting and query/command separation

- Status: Accepted
- Date: 2026-11-03

## Decision

- **Request DTOs** for every POST (`Create*InputModel`, `StockAdjustmentInputModel`, `RevisePricesInputModel`,
  `ReverseCounterTransactionInputModel`, `OperationsReportQuery` for GET filters) with DataAnnotations and the
  global `ModelValidationActionFilter`. They contain only what a user may supply - no `Id`, `Status`, totals or
  audit fields - and `RevisePrices` additionally uses `[Bind]`.
- **Explicit mapping chain**: input model -> application service arguments (the command) -> domain record
  (validating constructor) -> persistence row (repository). No AutoMapper.
- **Response / view-model DTOs** for every screen (`*PageModel`, `HomeDashboardViewModel`, `ConflictViewModel`)
  and query DTOs in `Application/Queries` (`DailyBranchSalesDto`, `LowStockDto`, `ShippingVolumeDto`,
  `MoneyOrderBranchSummaryDto`, `DashboardSummaryDto`).
- **Query/command separation**: reports and dashboard reads go through `IReportingQueries` (AsNoTracking
  projections or procedures); writes through application services + repositories + unit of work. Same database,
  no read replica - stated explicitly because CQS here is about code paths, not storage.

## Evidence

`Over_posting_status_id_and_audit_fields_has_no_effect` posts `Id`, `Status=Voided`, `TotalAmount`, `StaffId`,
`CreatedAtUtc` and `CreatedBy` with a sale; the stored transaction has a server-generated id, `Paid` status,
the authenticated clerk, the computed total and the real creation time.
