# ADR-0029: Bound simulated courier tracking lookups

- Status: Accepted
- Date: 2026-10-30

## Context

The partner tracking endpoint needs to demonstrate timeout and cancellation handling without calling an external courier. Unbounded slow lookups could occupy server work unnecessarily, and caller cancellation must remain distinct from a service timeout.

## Decision

Use a singleton `SimulatedCourierTrackingClient` with a configurable `SemaphoreSlim` concurrency limit and a linked cancellation timeout. A caller cancellation propagates as cancellation; expiration of the configured lookup timeout becomes a `503 Service Unavailable` response. The simulated latency exists to exercise the behavior in tests and is not presented as a production courier integration.

## Consequences

- Simultaneous simulated lookups have a configured upper bound, and tests can verify the timeout and cancellation paths.
- The API reports a temporary service failure when its own timeout expires.
- A production courier adapter must supply real network cancellation, timeout, resilience, and provider-specific error handling; this simulation makes no throughput claim.

## Alternatives considered

- Wait indefinitely for every lookup: rejected because a slow provider could tie up requests without a defined response deadline.
- Use `Task.Run` to move the lookup to a worker thread: rejected because it does not bound network concurrency and wastes threads for asynchronous work.
- Add a real courier SDK: deferred because this assignment asks for simulated tracking and provides no provider credentials or contract.
