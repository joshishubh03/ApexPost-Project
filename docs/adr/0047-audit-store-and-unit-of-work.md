# 0047: How the audit store interacts with the unit of work

- Status: Accepted
- Date: 2026-11-03

## Decision

There are two kinds of audit rows, both in `AuditEntries`, with different transaction rules:

1. **Business-event audit** (`CounterTransactionCompleted`, `MoneyOrderIssued`, refunds, identity checks...) is
   written by domain-event handlers through `IAuditLogRepository`, which **stages the row in the same unit of
   work**. It commits atomically with the sale: there is never a sale without its audit row, nor an audit row for
   a sale that rolled back.
2. **Request audit** (`MvcAuditActionFilter`, one row per audited MVC action with role, route values, status
   code and duration) goes through **`IAuditStore`**, implemented by `EfAuditStore` with **its own short-lived
   DbContext and transaction**. It runs after the action, so:
   - it can never roll back (or be rolled back by) the business change;
   - failed or forbidden attempts are still audited (409/403 status codes are recorded);
   - an audit-store failure is logged and swallowed by the filter - auditing must not break the counter.

## Alternatives considered

- Sharing the unit of work for the filter: rejected - the filter runs after commit, and a failed action would
  lose its audit row together with the rolled-back business data, which is exactly the record auditors want.
- Outbox for audit: unnecessary for a same-database write; the `OutboxMessages` table stays available for
  external notifications.

## Consequences

Identity documents stay masked in audit rows: handlers only receive the masked value and route values are
filtered to a safe allow-list.
