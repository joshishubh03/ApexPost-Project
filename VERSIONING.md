# API versioning - ApexPost Logistics API

- **Scheme:** URL segment, `/api/v{version}/shipments` (Asp.Versioning, `UrlSegmentApiVersionReader`).
- **Reporting:** `ReportApiVersions = true`, so every response carries `api-supported-versions: 2.0` and
  `api-deprecated-versions: 1.0`. v1 responses also carry a `Sunset` header (RFC 8594) with a link to the
  migration guide.
- **One implementation:** both versions call the same `CourierShipmentService`. Only the DTOs and the mapping
  differ (`ShipmentResponse` for v1, `ShipmentResponseV2` for v2). Scans did not change, so one `ScansController`
  serves both versions.
- **Contracts:** `contracts/v1.json` and `contracts/v2.json` are the generated OpenAPI documents. The test
  `OpenApiDocumentTests.Committed_contract_matches_the_generated_document` fails if the API drifts from them.
  After an intentional change, run `scripts/update-contracts.ps1`, review the diff, and add a row below.

## Change log

| Version | Change | Breaking? | Why |
|---|---|---|---|
| v1 (= Part B) | Endpoints moved from `/api/shipments` to `/api/v1/shipments`. The old path is rewritten to v1 server-side. | No | Existing clients keep working until the sunset date: the rewrite serves the same v1 contract. |
| v1 | Route names / operationIds gained a `V1` suffix (`ListShipmentsV1`, ...). | No on the wire | Route names must be unique. The clean names (`ListShipments`, ...) go to v2, the version the SDK is generated from. |
| v1 | Every v1 operation is marked `deprecated: true`. | No | Signals the migration to v2 in Swagger UI and generated clients. |
| v2 | `recipientName`, `recipientPhone`, `recipientAddress` and `city` are replaced by a nested `recipient { name, phone, address, city }` object. | **Yes** | Removing and renaming response fields breaks any client that reads them. |
| v2 | `status` is returned as a string enum (`"PickedUp"`) instead of a numeric code (`1`). | **Yes** | The JSON type of an existing field changes from number to string. |
| v2 | New `latestScan { scanType, location, scannedAtUtc }` summary (null before the first scan). | No (additive) | Clients that ignore unknown fields are unaffected. It is shipped with the breaking changes. |
| v2 | Paged list returns `ShipmentPageResponseV2` (items are `ShipmentResponseV2`). | **Yes** | Follows from the item shape change. |
| v1 + v2 | Request bodies (`CreateShipmentRequest`, `UpdateShipmentRequest`, `UpdateStatusRequest`, `ScanUploadRequest`) are unchanged. | No | Only the response shape changed, so request compatibility is kept. |
| v1 + v2 | Scan endpoints are identical in both versions. | No | No reason to fork them. |
| v1 + v2 (Part D) | Endpoints now require a bearer token (401/403 documented; `Bearer` security scheme in both documents). | **Yes** for anonymous callers | Security requirement. Released together with v2 before any external client went live. |
| v2 (Part D) | New read-only `GET /api/v2/partner/shipments/{trackingNumber}` (X-Api-Key). | No (additive) | New endpoint for partner systems. |

A change is **breaking** when an existing, correctly written client can fail. That covers removing or renaming a
field, changing a field's type or meaning, making an optional input required, removing an endpoint, or changing a
status code clients rely on. Adding an optional request field, a response field, or a new endpoint is
**non-breaking**.

## v1 deprecation and sunset plan

| Date | Step |
|---|---|
| 2026-11-09 | v2 released. v1 is marked `Deprecated = true`: `api-deprecated-versions: 1.0` on every response, `deprecated: true` in `/openapi/v1.json`, and "v1 (deprecated)" in the Swagger UI dropdown. The generated SDK targets v2 only. |
| 2026-11 to 2027-01 | Partners are told by e-mail and in the developer portal. The `Sunset: Wed, 31 Mar 2027 00:00:00 GMT` header and its `Link` to the migration guide appear on every v1 response. v1 traffic is monitored per client in the request logs. |
| 2027-02 | Reminder to clients that still call v1, using the monitoring data. |
| **2027-03-31** | **Sunset.** v1 controllers and the `/api/shipments` rewrite are removed, and v1 requests get `404 Not Found` (Problem Details). `contracts/v1.json` is kept in Git as history. |

Migration from v1 to v2:
- Change the path segment `v1` to `v2`.
- Read `recipient.name` and the other recipient fields from the nested object instead of the flat fields.
- Compare `status` with names (`"Delivered"`) instead of numbers.
- Regenerate clients from `contracts/v2.json`.
