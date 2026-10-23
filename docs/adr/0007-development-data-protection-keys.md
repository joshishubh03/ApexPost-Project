# ADR-0007: Use ephemeral data-protection keys for local Development

- Status: Accepted
- Date: 2026-10-23

## Context

ASP.NET Core cookie authentication and antiforgery tokens rely on data protection. Persisted keys are machine/user specific and can prevent local training runs from starting when a checkout is moved between Windows accounts or restricted environments.

## Decision

Use an ephemeral data-protection provider only in the Development environment. Production keeps the platform's normal key-management behavior. Development cookies are invalidated when the process restarts.

## Consequences

Local development does not depend on user-profile DPAPI files or store key material in the repository. Production deployments must configure durable, access-controlled key storage when running multiple instances or when cookies must survive restarts.
