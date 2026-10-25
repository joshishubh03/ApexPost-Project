# ADR-0006: Make important operations auditable with correlation context

- Status: Accepted
- Date: 2026-10-23

## Context

Financial, inventory, identity-verification, and administrative actions must be traceable to a user, branch, timestamp, and correlation ID.

## Decision

Model audit records explicitly and write them through an Infrastructure adapter. Establish correlation context at the HTTP boundary and pass the relevant actor and branch context into application use cases. Store only masked identity document identifiers.

Counter transaction completion is published as an application event after the stock and transaction JSON writes succeed. Its handler appends a structured audit record containing actor, branch, timestamp, action, amount, transaction ID, and correlation ID. Do not store payment credentials or identity document values in audit records.

Money order issuance follows the same event-to-audit path. Identity verification attempts are stored separately with their result, actor, branch, timestamp, correlation ID, document type, and masked identifier; a failed attempt is recorded but does not issue an order.

## Alternatives considered

- General application logs alone were rejected because they do not provide a stable, queryable business-action record with actor and branch fields.
- Writing audit entries before a sale commits could record an operation that never happened; committed business events are therefore appended after the business writes succeed.
- A transactional outbox would make audit publication atomic, but it requires database transactions outside this JSON-only assignment.

## Consequences

Audit persistence is best-effort after the business write: a failed audit append is logged with the correlation ID and does not make the completed sale appear to fail (which could cause a duplicate retry). This is an acknowledged consistency gap in the JSON implementation; a database-backed transactional outbox is the intended production direction. JSON persistence cannot atomically commit inventory, a transaction, and an audit event together. Never log raw identity numbers or secrets.
