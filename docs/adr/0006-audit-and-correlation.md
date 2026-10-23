# ADR-0006: Make important operations auditable with correlation context

- Status: Accepted
- Date: 2026-10-23

## Context

Financial, inventory, identity-verification, and administrative actions must be traceable to a user, branch, timestamp, and correlation ID.

## Decision

Model audit records explicitly and write them through an Infrastructure adapter. Establish correlation context at the HTTP boundary and pass the relevant actor and branch context into application use cases. Store only masked identity document identifiers.

## Consequences

Audit writes must be coordinated with the business operation and failures need an explicit policy. The implementation must not log raw identity numbers or secrets. The consistency policy will be finalized with the transaction workflow design.
