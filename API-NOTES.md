# ApexPost.Api - endpoints and status codes

Base paths are `/api/v1` (deprecated, Part B contract) and `/api/v2`. Both versions expose the same endpoints;
only the shipment response shape differs (see VERSIONING.md). The unversioned Part B path `/api/shipments` is
rewritten to v1. All errors are `application/problem+json` (RFC 9457 Problem Details) with a `traceId`.

| Verb | Route | Success | Error codes | Description |
|---|---|---|---|---|
| GET | `/api/v{v}/shipments?page=&pageSize=&status=&city=` | 200 | 400 (page < 1, pageSize > 50, unknown status), 500 | Paged, filtered list, newest first. `pageSize` defaults to 20 (max 50). |
| GET | `/api/v{v}/shipments/{trackingNumber}` | 200 | 404 (unknown or malformed tracking number), 500 | One shipment. |
| POST | `/api/v{v}/shipments` | 201 + `Location` | 400 (validation), 409 (duplicate tracking number), 415 (not JSON), 500 | Creates a shipment in status `Created`. |
| PUT | `/api/v{v}/shipments/{trackingNumber}` | 200 | 400, 404, 409 (Delivered/Failed, or concurrent change), 415, 500 | Replaces the editable details (weight and recipient). |
| PATCH | `/api/v{v}/shipments/{trackingNumber}/status` | 200 | 400 (missing or unknown status), 404, 409 (invalid transition), 415, 500 | Status only. Allowed: Created -> PickedUp -> InTransit -> OutForDelivery -> Delivered / Failed. |
| DELETE | `/api/v{v}/shipments/{trackingNumber}` | 204 | 404, 409 (status is not Created), 500 | Deletes a shipment that has not been picked up. |
| POST | `/api/v{v}/shipments/{trackingNumber}/scans` | 200 | 400 (validation: future time, Delivered without signature, bad scan ID), 404, 415, 500 | Idempotent bulk upload of offline scans. Returns `accepted`, `duplicatesSkipped` and `rejected[]` with reasons. |
| GET | `/api/v{v}/shipments/{trackingNumber}/scans` | 200 | 404, 500 | Scan history, oldest first. |
| GET | `/openapi/v1.json`, `/openapi/v2.json` | 200 | 404 outside Development | Version-specific OpenAPI documents (Development only). |
| GET | `/swagger` | 200 | 404 outside Development | Swagger UI with a "v2" / "v1 (deprecated)" dropdown (Development only). |

## Status code rules

| Code | When |
|---|---|
| 200 OK | Successful read or update. |
| 201 Created | Successful create. The `Location` header comes from `CreatedAtAction` and points at the versioned GET route. |
| 204 No Content | Successful delete. |
| 400 Bad Request | `ValidationProblemDetails` from `[ApiController]` automatic validation, which includes the custom `[NotInFuture]` and `IValidatableObject` rules. |
| 404 Not Found | Unknown tracking number (`ShipmentNotFoundException`), or a tracking number that does not match the route constraint. |
| 409 Conflict | Duplicate tracking number, invalid status transition, delete outside `Created`, edit after Delivered/Failed, or a rowversion conflict. |
| 415 Unsupported Media Type | Body is not `application/json` (`[Consumes]`). |
| 500 Internal Server Error | `ApiExceptionHandler` (`IExceptionHandler`). Generic message plus `traceId`; the exception and stack trace are included only in Development. |
