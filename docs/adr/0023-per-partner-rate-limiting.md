# ADR 0023: Per-partner API rate limiting

- Status: Accepted
- Date: 2026-10-25

## Context

The partner API limiter partitioned requests by client IP. The assignment requires a per-partner limit, and trusted partner identity is available only after validating the API key.

## Decision

Configure partner keys under `PartnerApi:Partners:{partnerId}`. Validate each `X-API-Key` value against precomputed SHA-256 hashes using fixed-time comparison, then place only the configured partner ID in `HttpContext.Items`. Run key validation before endpoint rate limiting so the limiter uses that partner ID. Keep `PartnerApi:ApiKey` as a compatibility setting for the `default` partner. Return 401 for missing or invalid keys and 500 when no partner key is configured.

## Consequences

- Each configured partner receives an independent 30-request-per-minute bucket, independent of its current IP address.
- The rate limiter partitions only on configured partner IDs; raw keys are not partition keys or log values.
- CORS preflight requests remain exempt from key validation.
- Partner secrets belong in user-secrets or deployment secret configuration, not appsettings or source control.
