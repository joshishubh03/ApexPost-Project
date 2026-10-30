# ADR 0015: Money orders in the unified checkout basket

- Status: Accepted; persistence extended by ADR-0030
- Date: 2026-10-25

## Context

The counter transaction screen already combines retail goods and parcel shipping. The assignment also calls for financial services to be available in the unified basket. A money order must continue to use the configured fee and identity-verification policy, while the customer receives one receipt and supplies one payment method.

## Decision

The counter basket can optionally include a money order. Checkout issues it through `MoneyOrderService`, which retains the identity verification and authorization rules. The counter receipt stores the money-order ID, principal, and fee and includes both amounts in its total. Reversing such a receipt requires a refund, which also refunds the linked money order; voiding is rejected for a basket containing a money order. Standalone money-order refunds are blocked when the order is linked to a counter receipt, and the linked refund operation is idempotent so it can be retried if a later JSON write fails.

The initial training adapter persisted inventory, receipts, and money orders in separate JSON operations. ADR-0030 adds a recoverable local mutation journal for current mixed checkout and reversal workflows.

## Consequences

- Retail products, shipping, and a money order can be paid for in one counter basket.
- The linked money order remains independently reportable and identity-verifiable.
- The local JSON journal restores consistency after a process interruption; filesystem limits remain and there is no database-level durability.

## Alternatives considered

- Require a separate payment and receipt for each basket component: rejected because it would not model the assignment's mixed counter transaction.
- Treat the money order only as a generic product line: rejected because identity verification, principal, fee, and instrument status have separate business rules.
- Save only one unified JSON object: rejected because repository interfaces remain replaceable; the local journal coordinates current cross-file mutations.
