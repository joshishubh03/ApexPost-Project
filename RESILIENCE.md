# Resilience - courier app to API (Week 4 Part D)

The typed `ApexApiClient` is registered with `AddHttpClient<ApexApiClient>()` (IHttpClientFactory, so there is
never a `new HttpClient()` per call). The message handler chain is, outermost first:

```
ApexApiClient
  -> AuthHeaderHandler            (bearer token from SecureStorage; 401 -> clear token, go to Login)
  -> Polly pipeline "apexpost-api" (Microsoft.Extensions.Http.Resilience, Polly v8)
       1. total request timeout   45 s
       2. retry                   3 retries, exponential back-off 1 s / 2 s / 4 s + jitter
       3. circuit breaker         opens at >= 50 % failures, >= 4 calls in 30 s; stays open 20 s
       4. per-attempt timeout     10 s
  -> HttpClientHandler            (DEBUG: dev-certificate bypass for localhost / 10.0.2.2 only)
```

The values are in `ApexPost.Courier/appsettings.json` (`Api:Resilience`) and `CourierResilienceOptions`. They are
unit-tested in `ApexPost.Courier.Core.Tests/ResilienceTests.cs`.

## Retry

| Setting | Value | Why |
|---|---|---|
| Attempts | 1 + **3 retries** | Mobile drop-outs are short. Three retries with back-off cover about 7 s of trouble without making the courier wait long, and the outbox retries later anyway. |
| Back-off | **Exponential** from 1 s (1, 2, 4 s) | Gives a struggling server room instead of hammering it. |
| Jitter | **On** | Hundreds of couriers reconnect at the same moment, for example when a depot's Wi-Fi comes back. Jitter spreads their retries so they don't arrive as one synchronized burst. |
| Retried | network errors (`HttpRequestException`), timeouts (`TimeoutRejectedException`), **408**, **5xx** | These are transient: the same request may succeed a moment later. |
| Never retried | every other **4xx** (400, 401, 403, 404, 409, 415, 429 ...) and an open circuit | A 4xx means the request itself is wrong or not allowed, so repeating it cannot help. 401 goes to Login. An open circuit fails fast by design. |
| Only idempotent requests | GET/PUT/DELETE by default; POST only when flagged with `CourierResilience.IdempotentRequest` | Repeating a non-idempotent POST could create something twice. |

Every retry is logged: `Retry {attempt}/3 in {delay} ms after HTTP 503` (or the exception type).

### Why the scan upload (a POST) is safe to retry

`POST /api/v2/shipments/{tn}/scans` is idempotent by design:
1. Every scan carries its device ID `scan::{Guid}`, created once when the scan is saved locally (Part A) and never
   regenerated.
2. The server stores that GUID as the **primary key** of `ShipmentScans` and skips any ID it already has
   (Part B). Repeats are reported as `duplicatesSkipped`, never stored twice.
3. A retry after a lost response, for example a timeout after the server committed, therefore produces the same
   end state. The app treats `duplicatesSkipped` as Synced.

So `ApexApiClient.UploadScansAsync` flags the request as idempotent and the retry strategy may repeat it.

## Circuit breaker

| Setting | Value | Why |
|---|---|---|
| Failure ratio | **50 %** | Opens only when the server is clearly failing, not on one unlucky call. |
| Minimum throughput | **4 calls** in the window | Avoids opening on one or two calls (one sync batch with retries already makes 4 attempts). |
| Sampling window | **30 s** | Long enough to see a pattern on a phone that makes few calls. |
| Break duration | **20 s** | Gives the API time to recover. After that, one trial call (half-open) closes the circuit if it succeeds. |
| Counted failures | the same transient set as retry | A 4xx is the client's problem, not a server outage. |

While the circuit is **open**, calls fail immediately with `BrokenCircuitException`, which becomes
`ApiUnavailableException`. Every page shows the banner **"Server unavailable – working offline"**. Scans are still
saved locally as Pending, and the next sync after the circuit closes uploads them. Every state change is logged
(`Circuit OPEN for 20 s ...`, `HALF-OPEN`, `CLOSED`) and shown on the Sync Status page.

## Timeouts

| Setting | Value | Why |
|---|---|---|
| Per attempt | **10 s** | A healthy API answers in well under a second. 10 s allows for slow 3G without letting one stuck connection block the batch. |
| Overall request | **45 s** | Caps the whole retry sequence (4 x 10 s worst case plus back-off), so the courier is never stuck on a spinner. The outbox simply tries again later. |

## How it fits the offline sync

- New scans are always saved locally first as **Pending** (Part A outbox).
- `SyncCoordinator` runs the outbox when connectivity returns (`Connectivity.ConnectivityChanged`), after each new
  scan, and on "Sync now". It uploads **batches of 20** through `ApiShipmentUploader`, which replaces Part A's fake.
- Results:

  | Result | Scan becomes |
  |---|---|
  | Accepted or duplicate | **Synced** |
  | Server rule rejected it (status machine, 400, 403, 404) | **Failed**, with the reason |
  | Transient failure (offline, timeout, 5xx, circuit open, 401) | Stays **Pending** for the next run |

- The Part A change listener (`PendingScanMonitor`) keeps the on-screen pending count live.
