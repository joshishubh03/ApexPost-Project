# ADR 0010: Security pipeline and partner tracking access

- Status: Accepted
- Date: 2026-10-25

## Context

The assessment requires request-wide security middleware, protected partner integration endpoints, origin restrictions, rate limiting, maintenance handling, and MVC action filters for auditing and performance.

## Decision

Use built-in ASP.NET Core rate limiting and CORS policies. Apply security headers and maintenance handling globally. Restrict administrative mutation routes by an exact IP allowlist, and protect `/api/partner` with an environment-provided API key checked in constant time. Keep the partner tracking origin list configurable. Leave `/health` available during maintenance. Use action filters only for MVC action concerns: slow-action timing and successful configuration-operation auditing. Keep transaction audit events in the Application/Infrastructure event flow.

## Consequences

The training app has explicit defaults and configuration points. The admin allowlist defaults to loopback and must be set to deployment IPs. The partner API returns only shipment receipt metadata. CORS limits browser origins but is not authentication. Reverse-proxy deployments need trusted forwarded-header configuration before IP allowlists can identify the original client.

## Alternatives considered

- Perform IP checks only inside individual controllers: rejected because it is easy to omit a route and duplicates transport-level policy.
- Trust every `X-Forwarded-For` value: rejected because a direct caller could spoof the address used by the allowlist.
- Delegate API keys, rate limits, and origin policy to an external gateway: deferred because the assignment requires the application pipeline to demonstrate these controls and no gateway is part of the local lab setup.
