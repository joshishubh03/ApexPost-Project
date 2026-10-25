# ADR 0025: Partner integration IP allowlist

- Status: Accepted
- Date: 2026-10-25

## Context

Partner integration endpoints validate API keys and apply per-partner rate limits, but the configured IP restriction covered administrative routes only. The assignment requires the integration surface to have an IP allowlist as well.

## Decision

Use `EndpointIpRestrictionMiddleware` for administrative mutations and `/api/partner` requests. Admin and integration clients have separate exact-address lists under `Security:AdminAllowedIpAddresses` and `Security:IntegrationAllowedIpAddresses`. Empty or missing lists deny access. Both lists ship with loopback-only local defaults; deployments must configure their approved egress addresses. IPv4-mapped IPv6 addresses are normalized before comparison. The existing pipeline order keeps endpoint CORS ahead of this middleware and applies the partner API-key check afterward.

## Consequences

- A partner request from an unapproved address receives 403 before API-key validation.
- Approved partners still need a valid API key and remain subject to their own rate limit.
- Deployments behind proxies must configure trusted forwarded headers before the application can safely use the original client address; trusting arbitrary forwarded headers would defeat the allowlist.
