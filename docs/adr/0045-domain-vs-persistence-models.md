# 0045: Where domain and persistence models are separated (and shadow properties)

- Status: Accepted (supersedes 0039, domain/persistence part)
- Date: 2026-11-03

## Decision

- The **domain** keeps rich, immutable records with behaviour and invariants: `InventoryItem.ApplyChange`
  (stock can never go negative), `CounterTransaction.Void/Refund`, `MoneyOrder.Refund`, value objects `Money`,
  `Sku`, `Weight` (30 kg limit), `BranchCode`, and `IdentityDocumentMask` (only masked identifiers are valid).
  Properties are get-only - stricter than private setters.
- **Persistence rows** (`Persistence/Entities/SqlRows.cs`) are separate, mapped with one
  `IEntityTypeConfiguration<T>` class per entity (`Persistence/Configurations`). Repositories map rows <-> domain.
- **Shadow properties**: `RowVersion` (BranchStock, CounterTransactions, Products) and the audit columns
  `CreatedAtUtc / CreatedBy / ModifiedAtUtc / ModifiedBy` exist only in the EF model and the table - neither
  the row classes nor the domain have them. `AuditColumnsInterceptor` stamps the timestamps; repositories set
  `CreatedBy/ModifiedBy` via `context.Entry(row).Property(...)`.
- **Value converters** for `Sku`, `BranchCode`, `Money` (shipping rate prices) and `Weight` (rate maximum,
  shipment weight). Reading a row re-validates the value object, so bad data fails fast.
- **Owned types**: `ParcelDimensions` and `ShippingAddress` on `Shipments` (same table, all-or-nothing).
- **Identity verification has a dedicated persistence entity + explicit mapper** (`IdentityVerificationRow`,
  `IdentityVerificationMapper`). The row has *only* `MaskedDocumentIdentifier varchar(16)`; there is no property
  that could hold a full document number, and the mapper re-checks the mask before staging. This aggregate is
  separated deliberately because a mapping mistake would be a data-protection breach, not just a bug.

## Why separate rows everywhere rather than mapping the domain records directly

The domain records are immutable positional records with validating constructors (e.g. a `CounterTransaction`
cannot exist without consistent totals); EF Core would need parameterless constructors and setters, weakening
those invariants. The cost is explicit mapping code in the repositories, kept next to each repository and covered
by the round-trip integration test.

## Consequences

Over-posting is impossible by construction: MVC binds request DTOs, the service builds the domain record, the
repository builds the row - `Id`, `Status`, totals and audit columns are never bound from a request
(test `Over_posting_status_id_and_audit_fields_has_no_effect`).
