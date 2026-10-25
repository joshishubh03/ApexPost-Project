# ADR 0015: Money orders in the unified checkout basket

- Status: Accepted
- Date: 2026-10-25

## Context

The counter transaction screen already combines retail goods and parcel shipping. The assignment also calls for financial services to be available in the unified basket. A money order must continue to use the configured fee and identity-verification policy, while the customer receives one receipt and supplies one payment method.

## Decision

The counter basket can optionally include a money order. Checkout issues it through `MoneyOrderService`, which retains the identity verification and authorization rules. The counter receipt stores the money-order ID, principal, and fee and includes both amounts in its total. Reversing such a receipt requires a refund, which also refunds the linked money order; voiding is rejected for a basket containing a money order. Standalone money-order refunds are blocked when the order is linked to a counter receipt, and the linked refund operation is idempotent so it can be retried if a later JSON write fails.

The training adapter persists inventory, counter receipts, money orders, and audit records in separate JSON operations. There is no cross-file transaction; failures during a mixed checkout or reversal may need reconciliation.

## Consequences

- Retail products, shipping, and a money order can be paid for in one counter basket.
- The linked money order remains independently reportable and identity-verifiable.
- A unified receipt and money-order status can temporarily diverge if one JSON write fails after another succeeds.
