# ADR-0005: Treat branch and role checks as server-side authorization

- Status: Accepted
- Date: 2026-10-23

## Context

The assignment requires staff to be restricted by both role and branch, and states that business rules must not be bypassable by calling an endpoint directly.

## Decision

Use ASP.NET Core authentication and authorization for access to MVC endpoints, and enforce branch and role constraints in Application/Domain use cases for business operations. UI visibility is not an authorization boundary.

## Alternatives considered

- Hiding restricted buttons in Razor views alone was rejected because callers can invoke controller routes directly.
- Trusting branch IDs submitted by forms was rejected; application use cases check the requested entity against the authenticated staff member's branch.
- ASP.NET Core Identity could provide more account-management features, but the assignment forbids it this week; the project uses cookie authentication and server-side claims instead.

## Consequences

Authorization policies and user context must be available to use cases. Tests should cover both HTTP access and direct application-service calls for critical business rules.
