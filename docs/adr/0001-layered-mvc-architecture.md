# ADR-0001: Layered solution and MVC presentation

- Status: Accepted
- Date: 2026-10-23

## Context

The assignment requires an ASP.NET Core MVC application and separates domain rules, use cases, and infrastructure. The previous assignment used a console application and a different business domain.

## Decision

Create separate Domain, Application, Infrastructure, Web, and Tests projects. Keep controllers focused on HTTP concerns and place enforceable business rules in Domain and Application. The Web project uses MVC rather than carrying forward the old console UI or Razor Pages scaffold.

## Alternatives considered

- A single MVC project would reduce the initial file count, but it would mix HTTP handling, business rules, and persistence in the same project.
- Razor Pages would support the required forms, but MVC is explicitly required by the assignment and better matches its controller/action-filter exercises.
- Microservices would add deployment and network complexity without a real scaling or ownership boundary in this single training application.

## Consequences

The project boundaries make the counter domain independently testable and allow infrastructure implementations to change. Cross-layer changes may require more files, so vertical feature branches should keep each use case coherent.
