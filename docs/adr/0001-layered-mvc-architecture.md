# ADR-0001: Layered solution and MVC presentation

- Status: Accepted
- Date: 2026-10-23

## Context

The assignment requires an ASP.NET Core MVC application and separates domain rules, use cases, and infrastructure. The previous assignment used a console application and a different business domain.

## Decision

Create separate Domain, Application, Infrastructure, Web, and Tests projects. Keep controllers focused on HTTP concerns and place enforceable business rules in Domain and Application. The Web project uses MVC rather than carrying forward the old console UI or Razor Pages scaffold.

## Consequences

The project boundaries make the counter domain independently testable and allow infrastructure implementations to change. Cross-layer changes may require more files, so vertical feature branches should keep each use case coherent.
