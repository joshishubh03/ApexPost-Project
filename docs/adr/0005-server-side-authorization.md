# ADR-0005: Treat branch and role checks as server-side authorization

- Status: Accepted
- Date: 2026-10-23

## Context

The assignment requires staff to be restricted by both role and branch, and states that business rules must not be bypassable by calling an endpoint directly.

## Decision

Use ASP.NET Core authentication and authorization for access to MVC endpoints, and enforce branch and role constraints in Application/Domain use cases for business operations. UI visibility is not an authorization boundary.

## Consequences

Authorization policies and user context must be available to use cases. Tests should cover both HTTP access and direct application-service calls for critical business rules.
