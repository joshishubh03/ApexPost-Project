# ADR 0018: Registered shipping service

- Status: Accepted
- Date: 2026-10-25

## Context

The assignment requires Standard, Express, and Registered shipping service levels. The current rate model already supports configurable per-zone service levels, but only Standard and Express are available in the enum and demo data.

## Decision

Add Registered to `ShippingServiceLevel`. Use the existing rate creation, quote, counter checkout, label, tracking, and branch reporting paths without introducing a separate pricing or fulfillment model. Development seeding provides illustrative Registered rates for each seeded zone and appends missing Registered rates to existing seeded zone data without overwriting current rates.

## Consequences

- Administrators can configure Registered rates through the existing rate form.
- Staff can quote and select Registered for parcel and letter shipments; receipts, labels, partner tracking, and branch reports retain the selected level.
- Seeded Registered prices are demo values and should be replaced with approved tariffs before operational use.
