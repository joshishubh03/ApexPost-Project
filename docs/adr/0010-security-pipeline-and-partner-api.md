# ADR 0010: Security pipeline and partner tracking access

- Status: Accepted
- Date: 2026-10-25

## Context

The assessment requires request-wide security middleware, protected partner integration endpoints, origin restrictions, rate limiting, maintenance handling, and MVC action filters for auditing and performance.

## Decision

Use built-in ASP.NET Core rate limiting and CORS policies. Apply security headers and maintenance handling globally. Restrict administrative mutation routes by an exact IP allowlist, and protect `/api/partner` with an environment-provided API key checked in constant time. Keep the partner tracking origin list configurable. Leave `/health` available during maintenance. Use action filters only for MVC action concerns: slow-action timing and successful configuration-operation auditing. Keep transaction audit events in the Application/Infrastructure event flow.

## Alternatives considered

- Third-party rate-limiting and CORS packages were unnecessary because ASP.NET Core provides the required primitives and the assignment limits dependencies.
- CORS alone was rejected as partner authentication because non-browser clients can call an endpoint regardless of browser-origin policy.
- A fixed API key shared by every courier was replaced by unique per-partner keys so rate limits and key revocation can be isolated by partner.

## Consequences

The training app has explicit defaults and configuration points. The admin allowlist defaults to loopback and must be set to deployment IPs. The partner API returns only shipment receipt metadata. CORS limits browser origins but is not authentication. Reverse-proxy deployments need trusted forwarded-header configuration before IP allowlists can identify the original client.
