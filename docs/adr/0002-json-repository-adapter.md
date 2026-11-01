# ADR-0002: JSON persistence behind repository interfaces

- Status: Accepted; extended by ADR-0030
- Date: 2026-10-23

## Context

This assignment explicitly disallows a database or ORM, while requiring next week's database implementation to replace persistence without changing business logic.

## Decision

Define repository abstractions in Application and implement them in Infrastructure using JSON files. Domain and Application will not depend on file paths or JSON serializer APIs.

## Consequences

JSON DTOs and mapping may differ from domain types. The infrastructure must handle malformed or missing files safely. Collection replacement uses a same-directory temporary file. Cross-process locking, journaled multi-file commit, and startup recovery are documented in ADR-0030. JSON remains a teaching-stage adapter: the coordinator depends on local filesystem lock and rename semantics and does not provide distributed transactions or database-level durability.

## Alternatives considered

- Add a database or ORM now: rejected because this week's constraints prohibit both.
- Read and write JSON directly in Application services: rejected because it would couple use cases to file paths and serialization.
- Keep one unstructured JSON document for the whole application: rejected because repository interfaces and separate collections make the planned database replacement clearer, while the coordinator handles the current local cross-file consistency requirement.
