# ADR 0012: Configurable tax rates and receipt snapshots

- Status: Accepted
- Date: 2026-10-25

## Context

The counter workflow needs to calculate tax for product categories and shipping. The assignment materials do not specify approved tax rates, and rates may vary by deployment. A receipt must remain auditable if configuration changes after a sale.

## Decision

Configure rates under `Taxation:Rates` in application settings. Products identify a tax category, defaulting to `Standard`. Checkout snapshots the category, rate, and rounded amount for every line, as well as the shipping rate and amount when shipping is present. Unknown categories fail closed so a missing configuration cannot silently create a tax-free sale.

The demo configuration uses 0% for `Standard` and `Shipping` until approved rates are provided. These are explicit demo values, not a claim about legally applicable rates.

## Consequences

- Historical receipt totals retain the rate and tax amount applied at checkout.
- Product categories can use deployment-specific rates without changing application code.
- Operators must provide approved rates for any additional categories before using them in sales.
- Tax rounding is performed per line and for shipping, to two decimal places.
