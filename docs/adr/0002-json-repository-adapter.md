# ADR-0002: JSON persistence behind repository interfaces

- Status: Accepted
- Date: 2026-10-23

## Context

This assignment explicitly disallows a database or ORM, while requiring next week's database implementation to replace persistence without changing business logic.

## Decision

Define repository abstractions in Application and implement them in Infrastructure using JSON files. Domain and Application will not depend on file paths or JSON serializer APIs.

## Consequences

JSON DTOs and mapping may differ from domain types. The infrastructure must handle malformed or missing files safely. Collection replacement uses a per-file semaphore and temporary-file replacement. Application mutation gates hold across the full read/modify/write workflows for counter sales, reversals, inventory adjustments, and money orders so concurrent requests in one process do not overwrite each other's snapshots. JSON storage remains a teaching-stage adapter: gates do not coordinate multiple processes, writes across separate collections are not atomic, and administrative collections need their own coordination if concurrent updates are introduced.

## Alternatives considered

- Add a database or ORM now: rejected because this week's constraints prohibit both.
- Read and write JSON directly in Application services: rejected because it would couple use cases to file paths and serialization.
- Keep one unstructured JSON document for the whole application: rejected because repository interfaces and separate collections make the planned database replacement clearer, while the remaining cross-file atomicity limit is documented.
