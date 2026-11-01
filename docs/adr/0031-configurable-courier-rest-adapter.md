# ADR-0031: Use a configurable generic courier REST adapter

- Status: Accepted
- Date: 2026-10-30

## Context

The partner tracking route initially used only simulated receipt lookups. The assignment needs bounded asynchronous external-style work, but no courier, endpoint, authentication scheme, or credentials were supplied.

## Decision

Define `ICourierTrackingClient` and keep the simulator as the default when `CourierTracking:ProviderBaseUrl` is empty. When a provider URL is configured, use an HTTPS GET to `{base}/{TrackingPath}/{escaped tracking number}`, send `X-API-Key` from secret configuration, map 404 to no result, and parse the documented `ShipmentTrackingSummary` JSON response. Validate that the response matches the requested tracking number. Apply a configured timeout and `SemaphoreSlim` concurrency limit to both adapters.

## Consequences

- The project can exercise a real HTTP integration contract without a provider-specific SDK or committed credentials.
- A provider can be selected by configuration and must adapt its response to the agreed fields and enum names.
- The provider receives the API key in a request header; logs must not include the request headers.
- Provider-specific resilience, authentication formats, retries, and rate policies remain adapter extensions when an actual courier is selected.

## Alternatives considered

- Continue with simulation only: rejected because it leaves the external HTTP contract unimplemented.
- Add a named provider SDK: rejected because no provider was identified and a provider dependency would make the project less replaceable.
- Send provider configuration and credentials in source-controlled settings: rejected because credentials must remain in user-secrets or deployment secret storage.
