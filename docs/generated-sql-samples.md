# Generated SQL samples (EF Core `ToQueryString()`)

Captured by the integration test `Generated_sql_for_the_main_queries_is_captured_with_ToQueryString` (written to
`ApexPost.Tests/bin/<config>/net10.0/generated-sql.md` on every test run). Every value is a parameter; date windows are
half-open ranges on the raw column (SARGable).

### Shipping volume by zone and service level

```sql
DECLARE @fromUtc datetimeoffset = '2026-10-03T00:00:00.0000000+00:00';
DECLARE @toUtc datetimeoffset = '2026-11-03T00:00:00.0000000+00:00';

SELECT [s1].[Code], [s1].[Name], [s0].[ServiceLevel], COUNT(*), COALESCE(SUM([c].[ShippingCharge]), 0.0)
FROM [Shipments] AS [s]
INNER JOIN [CounterTransactions] AS [c] ON [s].[CounterTransactionId] = [c].[Id]
INNER JOIN [ShippingRates] AS [s0] ON [s].[ShippingRateId] = [s0].[Id]
INNER JOIN [ShippingZones] AS [s1] ON [s0].[ZoneId] = [s1].[Id]
WHERE [c].[OccurredAt] >= @fromUtc AND [c].[OccurredAt] < @toUtc AND [s].[Status] = 'Booked'
GROUP BY [s1].[Code], [s1].[Name], [s0].[ServiceLevel]
ORDER BY [s1].[Code], [s0].[ServiceLevel]
```

### Money orders and identity verifications per branch

```sql
DECLARE @fromUtc datetimeoffset = '2026-10-03T00:00:00.0000000+00:00';
DECLARE @toUtc datetimeoffset = '2026-11-03T00:00:00.0000000+00:00';

SELECT [b].[Code], [b].[Name], (
    SELECT COUNT(*)
    FROM [MoneyOrders] AS [m]
    WHERE [m].[BranchId] = [b].[Id] AND [m].[OccurredAt] >= @fromUtc AND [m].[OccurredAt] < @toUtc AND [m].[Status] = 'Issued'), (
    SELECT COUNT(*)
    FROM [MoneyOrders] AS [m0]
    WHERE [m0].[BranchId] = [b].[Id] AND [m0].[OccurredAt] >= @fromUtc AND [m0].[OccurredAt] < @toUtc AND [m0].[Status] = 'Refunded'), COALESCE((
    SELECT COALESCE(SUM([m1].[Amount]), 0.0)
    FROM [MoneyOrders] AS [m1]
    WHERE [m1].[BranchId] = [b].[Id] AND [m1].[OccurredAt] >= @fromUtc AND [m1].[OccurredAt] < @toUtc), 0.0), (
    SELECT COUNT(*)
    FROM [IdentityVerifications] AS [i]
    WHERE [i].[BranchId] = [b].[Id] AND [i].[OccurredAt] >= @fromUtc AND [i].[OccurredAt] < @toUtc AND [i].[Status] = 'Verified'), (
    SELECT COUNT(*)
    FROM [IdentityVerifications] AS [i0]
    WHERE [i0].[BranchId] = [b].[Id] AND [i0].[OccurredAt] >= @fromUtc AND [i0].[OccurredAt] < @toUtc AND [i0].[Status] = 'Failed')
FROM [Branches] AS [b]
ORDER BY [b].[Code]
```

### Branches with no sales on a day (NOT EXISTS / anti-join)

```sql
DECLARE @fromUtc datetimeoffset = '2026-11-02T00:00:00.0000000+00:00';
DECLARE @toUtc datetimeoffset = '2026-11-03T00:00:00.0000000+00:00';

SELECT [b].[Code], [b].[Name], [b].[Region]
FROM [Branches] AS [b]
WHERE NOT EXISTS (
    SELECT 1
    FROM [CounterTransactions] AS [c]
    WHERE [c].[BranchId] = [b].[Id] AND [c].[OccurredAt] >= @fromUtc AND [c].[OccurredAt] < @toUtc AND [c].[Status] IN ('Paid', 'Completed'))
ORDER BY [b].[Code]
```

### Recent counter transactions for a branch (split query, first statement)

```sql
DECLARE @p int = 20;
DECLARE @branchId uniqueIdentifier = '00000000-0000-0000-0000-000000000000';

SELECT [c0].[Id], [c0].[BranchId], [c0].[CreatedAtUtc], [c0].[CreatedBy], [c0].[ModifiedAtUtc], [c0].[ModifiedBy], [c0].[MoneyOrderAmount], [c0].[MoneyOrderFee], [c0].[MoneyOrderId], [c0].[OccurredAt], [c0].[ResolutionReason], [c0].[ResolvedAt], [c0].[ResolvedByStaffId], [c0].[RowVersion], [c0].[ShippingCharge], [c0].[ShippingTaxAmount], [c0].[ShippingTaxRatePercent], [c0].[StaffId], [c0].[Status], [c0].[Subtotal], [c0].[TaxTotal], [c0].[TerminalId], [c0].[TotalAmount], [c0].[TransactionNumber], [p].[Id], [s].[Id], [p].[Amount], [p].[CounterTransactionId], [p].[CreatedAtUtc], [p].[CreatedBy], [p].[Method], [p].[ModifiedAtUtc], [p].[ModifiedBy], [p].[PaidAt], [s].[CounterTransactionId], [s].[CreatedAtUtc], [s].[CreatedBy], [s].[ModifiedAtUtc], [s].[ModifiedBy], [s].[ShipmentType], [s].[ShippingRateId], [s].[Status], [s].[WeightKilograms], [s].[AddressLine], [s].[RecipientName], [s].[HeightCentimeters], [s].[LengthCentimeters], [s].[WidthCentimeters]
FROM (
    SELECT TOP(@p) [c].[Id], [c].[BranchId], [c].[CreatedAtUtc], [c].[CreatedBy], [c].[ModifiedAtUtc], [c].[ModifiedBy], [c].[MoneyOrderAmount], [c].[MoneyOrderFee], [c].[MoneyOrderId], [c].[OccurredAt], [c].[ResolutionReason], [c].[ResolvedAt], [c].[ResolvedByStaffId], [c].[RowVersion], [c].[ShippingCharge], [c].[ShippingTaxAmount], [c].[ShippingTaxRatePercent], [c].[StaffId], [c].[Status], [c].[Subtotal], [c].[TaxTotal], [c].[TerminalId], [c].[TotalAmount], [c].[TransactionNumber]
    FROM [CounterTransactions] AS [c]
    WHERE [c].[BranchId] = @branchId
    ORDER BY [c].[OccurredAt] DESC
) AS [c0]
LEFT JOIN [Payments] AS [p] ON [c0].[Id] = [p].[CounterTransactionId]
LEFT JOIN [Shipments] AS [s] ON [c0].[Id] = [s].[CounterTransactionId]
ORDER BY [c0].[OccurredAt] DESC, [c0].[Id], [p].[Id], [s].[Id]

This LINQ query is being executed in split-query mode, and the SQL shown is for the first query to be executed. Additional queries may also be executed depending on the results of the first query.
```
