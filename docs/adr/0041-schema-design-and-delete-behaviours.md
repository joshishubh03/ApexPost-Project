# 0041: Relational schema design, constraints and delete behaviours

- Status: Accepted (supersedes 0034)
- Date: 2026-11-03

## Context

Week 3 replaces JSON files with SQL Server as the system of record. The schema must be in 3NF, enforce the
business rules that the domain also enforces (defence in depth), and protect financial history from accidental
deletes. The previous model kept shipping data inside `CounterTransactions` (a shipment's zone name, service
level, weight, recipient and dimensions), had no `Status` CHECK constraints and named audit columns
inconsistently.

## Decision

1. **Tables** (all created by EF Core migrations, see `sql/ApexPostDb_schema.sql`): Branches, CounterTerminals
   (the "Counters"), Staff, StaffRoles, Products, ProductCategories, TaxCategories, BranchStock, StockAdjustments,
   ShippingZones, ShippingRates, **Shipments** (new), MoneyOrders, IdentityVerifications, CounterTransactions,
   TransactionLines, Payments, AuditEntries, OperationalNotifications, OutboxMessages.
2. **Shipments is its own table** (0..1 per transaction, unique FK). Zone name and service level are *not*
   copied - they are reached through `ShippingRateId -> ShippingRates -> ShippingZones`, removing a transitive
   dependency. Migration `Week3SchemaCompliance` moves existing shipping data before dropping the old columns.
3. **Keys**: surrogate `uniqueidentifier` PKs; natural keys for lookups (`StaffRoles.Code`,
   `ProductCategories.Code`); composite PK `(BranchId, ProductId)` for BranchStock.
4. **UNIQUE**: `Branches.Code`, `Products.Sku`, `Staff.Email`, `ShippingZones.Code`, `(ZoneId, ServiceLevel)`,
   `(BranchId, Code)` for terminals, `TransactionNumber`, `MoneyOrderNumber`, `(CounterTransactionId, LineNumber)`,
   one payment and one shipment per transaction.
5. **CHECK**: quantities >= 0 (`BranchStock`), non-zero adjustments, `Amount > 0` (payments, money orders,
   identity checks), totals >= 0, allowed status values (`CounterTransactions.Status`, `MoneyOrders.Status`,
   `VerificationStatus`, `Payments.Method`, `Shipments.Status/ShipmentType`, `ShippingRates.ServiceLevel`,
   `StaffRoles.Code`), weight limits (`Shipments.WeightKilograms` and `ShippingRates.MaximumWeightKilograms`
   in (0, 30] - mirrored by the `Weight` value object), reversal and refund consistency, letters have no
   dimensions, and masked identity documents (`LIKE '****%'`, at most 4 visible characters, `varchar(16)`).
6. **DEFAULT + audit columns**: every master/operational table has `CreatedAtUtc datetime2(3) DEFAULT
   SYSUTCDATETIME()`, `CreatedBy`, `ModifiedAtUtc`, `ModifiedBy` (EF Core shadow properties, ADR 0045);
   `Shipments.Status DEFAULT 'Booked'`, `OperationalNotifications.IsAcknowledged DEFAULT 0`,
   `OutboxMessages.AttemptCount DEFAULT 0`, `AuditEntries.OccurredAt DEFAULT SYSDATETIMEOFFSET()`.
7. **rowversion** on `BranchStock`, `CounterTransactions` (required by the brief) and `Products` (catalogue cache).
8. **Types**: `decimal(18,2)` money, `decimal(9,3)` weights, `datetime2(3)` / `datetimeoffset(3)` UTC instants,
   `varchar` for codes / SKUs / e-mails / status values, `nvarchar` for names, reasons and addresses.

## Delete behaviours (and why)

| Relationship | ON DELETE | Reason |
|---|---|---|
| CounterTransactions -> TransactionLines, Payments, Shipments | CASCADE | Parts of one aggregate; they never exist without the receipt. |
| Everything -> Branches, Staff, Products, ShippingZones/Rates, StaffRoles, categories | RESTRICT (NO ACTION) | Master data is referenced by financial history. It is deactivated (`IsActive`/`IsOpen`), never deleted. |
| MoneyOrders -> CounterTransactions / IdentityVerifications | RESTRICT | A money order must keep its originating receipt and its verification. |
| AuditEntries, StockAdjustments, OperationalNotifications -> * | RESTRICT | Append-only history; the app login is also DENY DELETE (`sql/least-privilege.sql`). |

## Consequences

- Hard deletes of master data fail with a FK error by design; the UI offers deactivation instead.
- The `Shipments` split costs one join in shipment reports, measured in PERFORMANCE.md (workload 3).
- Constraint names are stable (`CK_<Table>_<Rule>`) so evidence queries in `sql/assignment3-compliance.sql`
  can list them.
