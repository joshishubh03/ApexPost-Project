# ADR-0006: Make important operations auditable with correlation context

- Status: Accepted; storage behavior extended by ADR-0030
- Date: 2026-10-23

## Context

Financial, inventory, identity-verification, and administrative actions must be traceable to a user, branch, timestamp, and correlation ID.

## Decision

Model audit records explicitly and write them through an Infrastructure adapter. Establish correlation context at the HTTP boundary and pass the relevant actor and branch context into application use cases. Store only masked identity document identifiers.

Counter transaction completion is published as an application event after the stock and transaction JSON writes succeed. Its handler appends a structured audit record containing actor, branch, timestamp, action, amount, transaction ID, and correlation ID. Do not store payment credentials or identity document values in audit records.

Money order issuance follows the same event-to-audit path. Identity verification attempts are stored separately with their result, actor, branch, timestamp, correlation ID, document type, and masked identifier; a failed attempt is recorded but does not issue an order.

## Consequences

Business audit events written inside an application mutation are staged with the corresponding JSON business files by the coordinator in ADR-0030. MVC filter audit records are appended after the action and remain a separate recoverable write; failures are logged with the correlation ID. Local JSON is not tamper-evident. Never log raw identity numbers or secrets.

## Alternatives considered

- Rely only on request logs: rejected because request logs do not capture durable business outcomes such as a completed sale or refund.
- Persist full action models or request bodies: rejected because they may contain passwords, identity document values, or other sensitive input.
- Require a transactional database outbox now: deferred because the assignment prohibits a database; the local journal provides recoverable JSON commits but not database durability or tamper evidence.
