# ADR-0029: Bound simulated courier tracking lookups

- Status: Accepted; extended by ADR-0031
- Date: 2026-10-30

## Context

The partner tracking endpoint originally used a simulated lookup to demonstrate timeout and cancellation handling without a known external courier contract. ADR-0031 extends this boundary with an opt-in configurable REST adapter.

## Decision

Use `ICourierTrackingClient` with the simulator as the default and the generic HTTPS REST client when `CourierTracking:ProviderBaseUrl` is configured. Both implementations use the same concurrency limit and timeout behavior. A caller cancellation propagates as cancellation; expiration of the configured lookup timeout becomes a `503 Service Unavailable` response.

## Consequences

- Simultaneous simulated or provider lookups have a configured upper bound, and tests verify timeout and cancellation paths.
- The API reports a temporary service failure when its own timeout expires.
- The generic REST contract expects the documented shipment response shape; provider-specific authentication or resilience can be added to the adapter.

## Alternatives considered

- Wait indefinitely for every lookup: rejected because a slow provider could tie up requests without a defined response deadline.
- Use `Task.Run` to move the lookup to a worker thread: rejected because it does not bound network concurrency and wastes threads for asynchronous work.
- Add a named courier SDK: rejected because the assignment provides no provider or contract; the generic REST adapter is configurable without a provider-specific dependency.
