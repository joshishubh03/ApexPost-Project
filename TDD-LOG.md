# TDD log: stale scans are rejected

One new business rule was built test-first, in three commits on `week5-testing`:

| Commit | Message | State of the tests |
|---|---|---|
| 1 | `RED: stale scans older than 7 days must be rejected` | New tests compile and **5 of 9 fail** (the 4 "accepted" rows pass because a stub that accepts everything is correct for them) |
| 2 | `GREEN: reject scans older than 7 days in the upload use case` | All tests pass |
| 3 | `REFACTOR: stale-scan rule lives in CourierShipment.ApplyScan; test helpers` | All tests still pass; no test changed its assertions |

## 1. The requirement

> **A scan taken more than 7 days before it reaches the server is rejected as stale. Exactly 7 days is still
> accepted. The other scans in the same upload are not affected.**

Why the business wants it: a phone that was offline (or had a wrong clock) for more than a week should not
silently rewrite a parcel's history. A late "Pickup" could, for example, appear after the office already
recorded a return. The scan is not lost: the server rejects it with a reason, the phone marks it **Failed** and
shows the reason (Week 4 Part A outbox), and a supervisor decides what to do.

Rules that follow from the sentence, each one a test:

| Rule | Test |
|---|---|
| 0 s, 1 min, 7 days minus 1 s, exactly 7 days old: accepted | `CheckScanAge_ScanAtMostSevenDaysOld_IsAccepted` (4 rows) |
| 7 days plus 1 s, 8 days, 30 days old: rejected with a reason | `CheckScanAge_ScanOlderThanSevenDays_IsRejectedWithAReason` (3 rows) |
| A stale scan in a batch is rejected; the fresh one is accepted and stored | `UploadScansAsync_ScanOlderThanSevenDays_IsRejectedAndTheRestOfTheBatchIsAccepted` |
| A stale scan never moves the shipment | `UploadScansAsync_OnlyAStaleScan_LeavesTheShipmentStatusUnchanged` |

The boundary (exactly 7 days vs one second more) is the part most likely to be wrong (`>` vs `>=`), so it gets
its own rows on both sides.

## 2. RED: the first failing tests

File: `ApexPost.UnitTests/Courier/StaleScanRuleTests.cs`.

The first test written was the boundary on the rejecting side:

```csharp
[Theory]
[InlineData(7 * 86_400 + 1)]             // one second over the limit
[InlineData(8 * 86_400)]
[InlineData(30 * 86_400)]
public void CheckScanAge_ScanOlderThanSevenDays_IsRejectedWithAReason(int secondsOld)
{
    // Arrange
    var scannedAt = ReceivedAt.AddSeconds(-secondsOld);

    // Act
    var reason = CourierShipment.CheckScanAge(scannedAt, ReceivedAt);

    // Assert
    Assert.NotNull(reason);
    Assert.Contains("older than 7 days", reason, StringComparison.Ordinal);
}
```

The solution builds with **zero warnings and warnings as errors**, so a test that does not compile cannot be
committed. The RED commit therefore adds the smallest production stub that compiles, and nothing more:

```csharp
public static readonly TimeSpan MaximumScanAge = TimeSpan.FromDays(7);

public static string? CheckScanAge(DateTimeOffset scannedAtUtc, DateTimeOffset receivedAtUtc) => null;
```

Result of `dotnet test ApexPost.UnitTests --filter "FullyQualifiedName~StaleScanRuleTests"` at this commit:

- **Failed: 5.** The three rejection rows fail on `Assert.NotNull(reason)`. The two service tests fail because
  the stale scan is accepted (`Accepted` is 2 instead of 1, and the status moves to `PickedUp`).
- **Passed: 4.** The acceptance rows pass, because "accept everything" is right for them. That is expected: they
  pin the boundary from the other side so GREEN cannot over-correct with `>=`.

## 3. GREEN: the minimal implementation

Two small changes, nothing speculative:

```csharp
// CourierShipment (domain)
public static string? CheckScanAge(DateTimeOffset scannedAtUtc, DateTimeOffset receivedAtUtc) =>
    receivedAtUtc - scannedAtUtc > MaximumScanAge
        ? "Scan is older than 7 days and can no longer be accepted."
        : null;

// CourierShipmentService.UploadScansAsync (application)
var reason = CourierShipment.CheckScanAge(scan.ScannedAtUtc, receivedAt)
    ?? shipment.ApplyScan(scan.ScanType, receivedAt);
```

`>` (not `>=`) is what makes "exactly 7 days" pass. All 9 new tests pass, and so does the rest of the suite.
Existing scans in other tests are minutes old, so none of them was affected.

## 4. REFACTOR: what changed while the tests stayed green

1. **One place for scan acceptance.** In GREEN, the service had to know that the age check runs before the status
   machine (`?? shipment.ApplyScan(...)`). That ordering is a domain rule, so it moved into the domain: a new
   overload `ApplyScan(scanType, scannedAtUtc, receivedAtUtc)` checks the age first, then the status machine.
   The service now makes a single call: `shipment.ApplyScan(scan.ScanType, scan.ScannedAtUtc, receivedAt)`.
   The old `ApplyScan(scanType, now)` stays and delegates with `scannedAtUtc = receivedAtUtc = now`, so existing
   callers and tests behave exactly as before.
2. **No magic number in the message.** The text is built from `MaximumScanAge` (and formatted with the invariant
   culture). It now also says when the scan was taken and received, which helps the supervisor who reviews a
   Failed scan on the phone.
3. **Test code.** The two service tests duplicated the set-up. It moved into `ServiceWithShipmentAsync(...)` and
   `PickupTaken(TimeSpan age)`. `PickupTaken(TimeSpan.FromDays(8))` reads as the requirement does.

No assertion changed in this commit; the whole suite stayed green.

## 5. Production code changed to make it testable

| Change | Why |
|---|---|
| **None needed for the clock**: `CourierShipmentService` already takes a `TimeProvider` (added in Week 4 for exactly this reason). The tests pass `FixedClock` (9 Nov 2026 09:00 UTC) | A rule about "7 days ago" that read `DateTime.UtcNow` would make the boundary tests flaky: they would depend on how long the test run takes and on when it runs |
| **The domain rule is a pure function**: `CheckScanAge(scannedAtUtc, receivedAtUtc)` takes both instants as parameters instead of reading a clock | The rule can be tested with plain values, at exact boundaries, in microseconds. The domain project stays free of infrastructure (no clock, no `TimeProvider` dependency) |
| **Test doubles shared, not copied**: `InMemoryCourierShipmentRepository` and `FixedClock` were moved out of a test class into `ApexPost.UnitTests/TestDoubles/` (done while reorganising the projects, before the RED commit) | The TDD tests, the existing service tests and the BDD scan-sync scenarios all use the same fakes, so a fix to a fake fixes it everywhere |

## 6. Where else the rule shows up

- **API** (`POST /api/v{1,2}/shipments/{trackingNumber}/scans`): a stale scan appears in `rejected` with its reason;
  the rest of the batch is accepted. No contract change: `rejected[].reason` already existed.
- **Phone** (Week 4 outbox): a rejected scan becomes `Failed` with `lastSyncError`, and it is never retried
  automatically. This is the same path as an invalid status transition.
