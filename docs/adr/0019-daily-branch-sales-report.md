# ADR 0019: Daily branch sales report

- Status: Accepted
- Date: 2026-10-25

## Context

The branch report currently shows a single completed-sales total. The assignment requires daily branch sales split between retail and services.

## Decision

Group completed counter receipts by the server's local calendar date. Classify catalogue lines in the Postage, Shipping, and Money Order categories as services; classify other catalogue lines as retail. Include line tax in the relevant amount, and count shipping charges/tax plus linked money-order principal/fee as services. Include standalone issued money orders in service sales on their issue date and show their count separately so linked money orders are not counted twice. Show daily receipt count, standalone money-order count, retail sales, service sales, and total sales in the branch report.

## Consequences

- Daily totals include receipt sales and standalone issued money orders; refunded orders are excluded.
- The current product catalogue category determines the classification for historical product lines; categories are not currently editable after creation.
- Branches do not store a timezone, so the server's local calendar date is used for daily grouping.
