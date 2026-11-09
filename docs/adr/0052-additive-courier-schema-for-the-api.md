# 0052: Additive courier schema for the Week 4 API

- Status: Accepted
- Date: 2026-11-09

## Context

Parts B-D say: "Do not modify the Week 3 data layer except to add new service methods." The shipments API needs
things the Week 3 schema does not have:
- APX-######## tracking numbers and the courier status machine (Created -> ... -> Delivered/Failed);
- recipient phone and city;
- a scan history with the device scan ID as an idempotency key.

The Week 3 `Shipments` table belongs to counter transactions (Booked/Cancelled, 30 kg limit) and cannot hold these
without changing existing columns, constraints and behaviour.

## Decision

Extend the data layer **additively**, without changing any Week 3 table, column, constraint, repository or service:

- New tables `CourierShipments` and `ShipmentScans` with their own CHECK constraints, a unique tracking number,
  a rowversion, and `ShipmentScans.Id` = device scan GUID. They come from migration `Week4CourierShipmentsApi`
  plus two new `DbSet`s on the same `ApexPostDbContext`.
- New `ICourierShipmentRepository` / `EfCourierShipmentRepository` and `CourierShipmentService`, registered by
  `AddApexPostCourierShipments()`. The Week 3 `AddApexPostSqlServer()` composition (DbContext, unit of work,
  retry strategy) is reused unchanged.
- `sql/least-privilege.sql` gets per-table grants for the two new tables. `CourierShipments` gets the only DELETE
  grant, because the API may delete a shipment only while it is Created. The matching assertion in the Week 3
  `SqlScriptTests` allows exactly that exception.

## Consequences

- All Week 3 code paths, migrations and tests are unchanged in behaviour. The model test
  `Migrations_and_snapshot_cover_the_whole_model` guards the new migration and snapshot.
- The API reuses the Week 3 DbContext, unit of work and execution strategy, as Part B requires.
