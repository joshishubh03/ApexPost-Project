# ADR 0008: Branch-scoped reporting and deterministic demo history

- Status: Accepted
- Date: 2026-10-25

## Context

The assessment requires branch dashboards, stock and shipping summaries, money-order and identity-verification reporting, and at least 40 historical counter transactions for a reproducible demonstration.

## Decision

Keep report calculations in an Application service. The service reads independent JSON repositories concurrently with `Task.WhenAll`, applies branch scope before aggregating, and returns a report model to the MVC layer. Development seeding creates 40 date-spread transactions only when transaction and inventory collections are both empty; seeded receipts and stock movements are generated deterministically.

## Consequences

Reports are consistent with the records available at request time, but concurrent JSON writes do not provide a cross-repository snapshot. Seeded inventory starts with ample opening quantities and then records the historical sales as stock movements. Existing populated data is preserved.
