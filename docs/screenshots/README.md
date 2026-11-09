# Week 4 - how to reproduce the evidence

Each result the assignment asks to show can be reproduced locally with the commands below. Fictional data only.
The automated tests (`dotnet test ApexPost.Server.slnf`) check the same behaviour.

## Part A - Couchbase Lite (no network needed)

`dotnet run --project ApexPost.Courier.Offline.Harness`

| Result | Menu option | Covered by test |
|---|---|---|
| Scans for `APX-10000001`, ordered by `scannedAtUtc` | 2 Find by Tracking Number | `CouchbaseScanRepositoryTests` |
| Pending scans | 3 List Pending | `CouchbaseScanRepositoryTests` |
| Number of scans per scan type for `courier-007` | 7 | `CouchbaseScanRepositoryTests` |
| `[listener] ... changed ... pending scans: N` log lines | 1 Add Scan, then 4 Simulate Sync | `ChangeListenerTests` |

## Parts B and C - API and Swagger UI

`dotnet run --project ApexPost.Api --launch-profile https`, then open `https://localhost:7301/swagger`.
Get a token from `https://localhost:7301/dev/token?roles=Admin,Courier&courierId=courier-007` and paste it into
**Authorize**. The same requests are in `ApexPost.Api/ApexPost.Api.http`.

| Result | Request | Covered by test |
|---|---|---|
| Swagger dropdown "v2" and "v1 (deprecated)" | open `/swagger` | `OpenApiDocumentTests` |
| 400 `ValidationProblemDetails` | POST `/api/v2/shipments` with tracking number `BAD-1` | `ShipmentsApiTests` |
| 409 Conflict | POST the same shipment twice | `ShipmentsApiTests` |
| 201 Created with `Location` | POST a new shipment | `ShipmentsApiTests` |

## Part D - security and the courier app

The 401, 403 and partner API key requests are in the "SECURITY" section of `ApexPost.Api/ApexPost.Api.http`
(`SecurityApiTests`). The offline sync, retry and circuit-breaker walkthrough is the "Demo script" in `README.md`
(`OfflineSyncTests`, `ResilienceTests`).
