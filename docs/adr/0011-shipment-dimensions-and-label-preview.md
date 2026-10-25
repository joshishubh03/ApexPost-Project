# ADR 0011: Shipment snapshots and printable label preview

- Status: Accepted
- Date: 2026-10-25

## Context

The assignment requires shipment capture for recipient, destination, parcel weight and dimensions, plus a shipping label preview. The current counter receipt already snapshots the rate, zone, service, weight and price.

## Decision

Store recipient name, destination address, and positive two-decimal parcel dimensions on the counter transaction when a shipping rate is selected. Require the complete shipment data in the Application workflow, and expose a printable label only after checking the receipt against the caller's permitted branch. Older shipping receipts with no address snapshot remain readable but have no label preview.

## Consequences

The receipt contains the shipping information needed to render the training label without a separate mutable label store. Address validation checks presence and length but does not invent a destination schema or dimension ceiling; the selected shipping rate remains the source of truth for maximum weight. Production label generation, barcode format, and courier integration remain future work.
