# ADR 0014: Manager-controlled money-order refunds

- Status: Accepted
- Date: 2026-10-25

## Context

Money orders need a controlled refund path consistent with branch roles and the requirement that financial operations remain traceable. A refunded order must not be refunded again, and reports must not count its value as an active issued order.

## Decision

Only an active Branch Manager assigned to the open branch can refund an issued money order. A refund requires a reason, stores the manager, timestamp, and reason, and transitions the order from Issued to Refunded. The service publishes a correlated audit event. Reporting totals include only issued money orders and show refunded counts separately.

## Consequences

- The order remains in JSON history and cannot be refunded twice.
- Counter Clerks and Administrators cannot use the manager refund operation.
- The money-order collection write and audit append are separate JSON operations; there is no cross-file transaction.

## Alternatives considered

- Delete a refunded order: rejected because the auditor needs the original order and its resolution history.
- Permit refund by any counter clerk: rejected because the assignment reserves voids and refunds for Branch Managers.
- Permit refunding linked orders independently: rejected because the associated counter receipt and money-order status could diverge.
