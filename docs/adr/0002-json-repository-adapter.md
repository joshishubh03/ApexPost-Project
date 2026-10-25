# ADR-0002: JSON persistence behind repository interfaces

- Status: Accepted
- Date: 2026-10-23

## Context

This assignment explicitly disallows a database or ORM, while requiring next week's database implementation to replace persistence without changing business logic.

## Decision

Define repository abstractions in Application and implement them in Infrastructure using JSON files. Domain and Application will not depend on file paths or JSON serializer APIs.

## Consequences

JSON DTOs and mapping may differ from domain types. The infrastructure must handle malformed or missing files safely. JSON storage is a teaching-stage adapter and does not provide database-grade multi-process transactions.
