# Offline design - courier device store (Week 4 Part A)

The courier device keeps every shipment scan in an embedded Couchbase Lite database, `apexpost_courier`
(default collection). Each scan is a JSON document with the ID `scan::{Guid}`:

```json
{
  "docType": "shipmentScan",
  "trackingNumber": "APX-10000001",
  "scanType": "Pickup",
  "courierId": "courier-007",
  "location": "Maplewood Depot",
  "scannedAtUtc": "2026-11-09T08:00:00.000Z",
  "notes": null,
  "signatureName": null,
  "syncStatus": "Pending",
  "syncAttempts": 0,
  "lastSyncAttemptUtc": null,
  "lastSyncError": null
}
```

`ApexPost.Courier.Offline` contains the domain record (`ShipmentScan`), the `IScanRepository` port, the Couchbase
adapter (`CourierDatabase`, `CouchbaseScanRepository`), the outbox (`OutboxService`) and the change-listener
monitor (`PendingScanMonitor`). Only the `Storage` folder references Couchbase; `ArchitectureTests` fails the build
if a domain or abstraction type ever exposes a Couchbase type.

## Why the app saves locally first

A courier's network coverage drops in lifts, basements and rural roads, but the pickup or delivery has already
happened and must not be lost or re-typed. So every scan is written to the local database first, with
`syncStatus = Pending`, and the screen confirms it immediately. Upload is a separate step.

- **No data loss.** The scan is on disk before anything touches the network. If the app crashes, the phone
  restarts or the battery dies, the scan is still there.
- **No waiting.** Saving takes milliseconds offline or online, so the courier can scan the next parcel straight
  away.
- **One upload path.** The outbox (`OutboxService.SyncPendingAsync`) reads Pending scans with a SQL++ query
  (using the `syncStatus` value index), uploads them in batches of 20 and marks each one:
  - `Synced` when the server accepts it, or reports it as a duplicate it already has;
  - `Failed` when the server rejects it for good, for example an invalid status transition, with the reason kept
    in `lastSyncError`.
  
  A transient error (offline, timeout, server down) leaves the whole batch `Pending` with the attempt counted, and
  the next run retries it. Uploads are idempotent because every scan carries its own `scan::{Guid}`: the server
  de-duplicates by that ID (Part B), so a retry after a lost response can never create a second scan.
- **Live feedback.** A collection change listener logs the IDs of the changed documents and updates the live
  "pending scans" count shown by the harness and the MAUI app. `CourierDatabase` removes every listener token in
  `Dispose()` before closing the database.

## How conflicts would be handled

A scan is immutable once it has been taken: it describes something that happened at a place and a time. The only
field that changes on the device is the sync bookkeeping. So real conflicts happen at the **shipment** level
(two couriers, or a courier and the office, change the same parcel), not at the scan level.

| Approach | How it works | Good for | Risk |
| --- | --- | --- | --- |
| **Last-write-wins** | The newest write, by timestamp or revision, replaces the other one. | Device-local data with a single writer, such as the sync bookkeeping on one phone. This store saves with `ConcurrencyControl.LastWriteWins`. | Device clocks drift. A late upload from a phone that was offline for hours could overwrite a newer office decision, such as "Delivered" overwriting "Returned to sender". Data is silently lost. |
| **Server decides (chosen)** | The device sends *facts* (scans) rather than the new state. The server applies them through its business rules: allowed status transitions, ordering by `scannedAtUtc`, de-duplication by scan ID. It answers accepted, duplicate or rejected with a reason. | Shared, rule-heavy data such as shipment status. | Needs a round trip, and the device must show rejections (`Failed` + reason) so a human can fix them. |

ApexPost uses **server-decides** for shipment state. Scans are append-only facts, and the API (Part B) owns the
status machine `Created -> PickedUp -> InTransit -> OutForDelivery -> Delivered / Failed`. A scan that would
break it is rejected with a reason instead of overwriting newer data. Last-write-wins is only used for
single-writer, device-local data.

## Why an embedded database must not be shared by several processes

Couchbase Lite is an *embedded* database: a library that runs inside the app's process and works on local files.
It is built on SQLite. It is not a server that brokers access for several clients.

- **Locking and caches are per process.** Each process keeps its own in-memory state: open file handles,
  write-ahead log position, query and index caches, and document revision tracking. A second process writing to
  the same files bypasses all of that. The result is lock errors (`SQLITE_BUSY`), stale reads, or a corrupted
  database.
- **Change listeners only see their own process.** The listener that updates the pending count fires for writes
  made through *this* `Database` instance. Writes by another process would never be announced, so the UI and the
  outbox would act on stale state.
- **Mobile sandboxes enforce it anyway.** On Android and iOS each app has a private data folder, so sharing
  across apps isn't even possible.

The rule in this code: **open the database once per process and share that one `CourierDatabase` instance**. It is
a singleton in the MAUI app, and the harness, tests and app each use their own folder. If another process needs the
data, it should go through an API or Couchbase's replication (Sync Gateway), never through the files directly.
