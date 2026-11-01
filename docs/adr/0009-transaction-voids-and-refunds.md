# ADR 0009: Manager-controlled transaction reversals

- Status: Accepted; persistence extended by ADR-0030
- Date: 2026-10-25

## Context

The assignment requires controlled voids and refunds, with Branch Manager permission and a recorded reason.

## Decision

Represent transaction status in the domain and permit a one-way transition from Completed to Voided or Refunded. Require a non-empty reason and retain the manager ID and timestamp. The Application service rechecks active staff role and branch assignment, restores sold inventory, and publishes a correlated audit event. The current training slice supports full refunds only.

## Consequences

The user interface and authorization attributes provide convenient access control, while the service and domain enforce the business rule independently. Inventory and transaction status remain separate JSON collections and are committed together through the recoverable local JSON coordinator in ADR-0030; this is not equivalent to a database transaction.

## Alternatives considered

- Delete a receipt when it is voided: rejected because it would remove the history required for audit and reporting.
- Allow the clerk who made the sale to reverse it: rejected because the assignment explicitly requires Branch Manager approval.
- Support partial refunds in this training slice: deferred because it requires line-level quantity and payment settlement behavior beyond the specified full reversal workflow.
