# ADR 0016: Weight-rated letter shipments

- Status: Accepted
- Date: 2026-10-25

## Context

The shipping counter must handle both parcels and letters. The existing configurable rates are weight-based, but parcel labels also require dimensions that do not apply to letters.

## Decision

Persist a nullable shipment type on counter receipts to preserve older JSON data. New shipping receipts must select Parcel or Letter and provide destination and weight. Parcels require three positive dimensions; letters reject parcel dimensions. Both use the selected zone/service rate and its configured maximum-weight rule. Labels and partner tracking include the shipment type.

## Consequences

- Letter shipments can use the existing zone and service pricing without a parcel-only form requirement.
- Existing shipment receipts with no shipment type remain readable and display as parcels in the label preview.
- Letter pricing remains weight-based because no separate letter tariff was supplied.
