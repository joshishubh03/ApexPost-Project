# 0051: Couchbase Lite offline store for courier scans

- Status: Accepted
- Date: 2026-11-09

## Context

Couriers record pickups and deliveries where there is no network. Scans must be stored on the device, queryable
offline, and uploaded later without duplicates.

## Decision

- New class library `ApexPost.Courier.Offline`. Couchbase Lite (`Couchbase.Lite` 3.2.x) is referenced only here.
- Database `apexpost_courier`, default collection, JSON documents with ID `scan::{Guid}`.
- `IScanRepository` (create, get by ID, update, delete, list all, plus three SQL++ queries). The queries use
  named parameters only, backed by value indexes on `trackingNumber`, `syncStatus` and `courierId`.
- Outbox: scans are saved locally as `Pending`. `OutboxService` uploads them in batches of 20 through
  `IShipmentUploader` and marks each one Synced or Failed. Transient errors leave scans Pending. Part A uses
  `FakeShipmentUploader`, and Part D swaps in the real API client.
- `PendingScanMonitor` registers a collection change listener. `CourierDatabase.Dispose()` removes all listener
  tokens before closing the database.

## Consequences

- The Week 3 SQL Server / EF Core code is untouched.
- Native Couchbase Lite binaries are needed at run time. They come with the NuGet package for Windows, macOS,
  Linux, Android and iOS.
- Only one process may open a database folder (see OFFLINE-DESIGN.md).
