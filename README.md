# ApexPost Counter Platform

Assignment 2 implementation of a secure retail and postal counter platform using .NET 10, C# 14, and ASP.NET Core MVC.

## Current stage

The staff-auth slice adds branch and staff domain records, JSON demo data, and cookie-based staff sign-in. The branch-staff-management slice adds Administrator-only branch and staff management. The product-catalogue slice adds product search and management, with 30 seeded demo products. The inventory slice tracks branch-specific quantities and a reasoned movement history. The shipping-rates slice defines destination zones, service levels, and weight-based quotes for parcel and letter shipments. Unified counter baskets support products, shipment services, and money orders. Branch Managers can void or fully refund completed receipts with a reason. Branch reports summarize sales, stock, shipping, money orders, identity checks, audit events, and recent operational notifications. Simulated notifications are emitted for low-stock threshold crossings, high-value money orders, and every void or refund. The money-order slice adds configurable fees, high-value identity verification, and masked document recording. Development seeding supplies 40 historical transactions.

## Solution projects

- ApexPost.Domain — business entities, value objects, calculations, and invariants.
- ApexPost.Application — use cases, service contracts, and orchestration.
- ApexPost.Infrastructure — JSON repositories, audit storage, seed data, and simulated integrations.
- ApexPost.Web — MVC controllers, Razor views, authentication, middleware, and filters.
- ApexPost.Tests — unit and integration tests.
- docs — design documentation and architecture decision records.
- data — deterministic, non-sensitive JSON demo data created at runtime.

## Prerequisites

- .NET 10 SDK

## Restore, build, test, and run

PowerShell commands:

    dotnet restore ApexPost.slnx
    dotnet build ApexPost.slnx
    dotnet test ApexPost.slnx
    dotnet run --project ApexPost.Web --urls https://localhost:5001

The demo data is seeded on first run in Development. The seed creates five branches, two active counter terminals per branch, at least 20 staff, 30 products, 10 shipping zones with Standard, Express, and Registered rates, opening inventory, and 40 historical transactions when both the inventory and transaction files are empty. All seeded accounts use the password CounterDemo!2026. Example accounts:

- admin@apexpost.local
- manager1@apexpost.local
- clerk11@apexpost.local
- auditor1@apexpost.local

The demo password is public training data and must never be reused outside this local demo. Password hashes, not plaintext passwords, are stored in JSON.

## Branch and staff administration

Sign in as the seeded Administrator to use the **Branches** and **Staff** links. Administrators can create branches with opening and closing times, change a branch's operational status, add or deactivate branch counter terminals, create staff accounts, edit staff names, email addresses, roles and home branches, and activate or deactivate accounts. Staff profile edits preserve the current password hash. New passwords are hashed before they are saved. Non-administrator staff must be assigned to an existing branch, and the last active Administrator cannot be deactivated or reassigned to a non-administrator role.

The MVC administration controller calls `StaffManagementService` in the Application layer. That service uses repository interfaces; Infrastructure implements those interfaces with the JSON files in `data/`. This keeps web form handling, business rules, and persistence in their respective layers.

## Product catalogue

All signed-in staff can search active products by SKU, name, category, or description. Administrators and Branch Managers can create products and activate or deactivate them. Product SKUs are normalized to uppercase and must be unique; prices are non-negative and support up to two decimal places. Products carry a tax category (Standard by default). Development seeding creates 30 sample catalogue products in `data/products.json` when the file is empty or missing.

## Inventory

Staff can view active-product stock for their assigned branch. Administrators can select a branch; Administrators and Branch Managers can record stock changes with a reason and reorder level. Each record stores the current quantity and recent movements. A stock change cannot reduce quantity below zero. Inventory data is written to `data/inventory.json` when the first adjustment is saved.

## Shipping zones and rates

Signed-in staff can view the shipping schedule and calculate a quote. Administrators can add zones and rates. Each rate has a service level (Standard, Express, or Registered), base charge, per-kilogram charge, and maximum weight. Counter staff can ship parcels with recipient, destination address, weight, and dimensions, or letters with recipient, destination address, and weight without parcel dimensions. Both use the configured rate and weight limit, appear in tracking, and have a branch-scoped printable label preview. Development seeding provides 10 zones and all three service levels in `data/shipping-zones.json` and `data/shipping-rates.json`.

## Counter transactions

Counter Clerks, Branch Managers, and Administrators can record sales for their assigned branch. A transaction can combine catalogue products, parcel or letter shipping, and an optional money order in one basket. Checkout requires an active terminal assigned to the branch and snapshots its ID on the receipt. Completing a sale checks active staff, branch, terminal, and available stock; stores price and service snapshots; records the payment method; and decreases inventory. Money orders in a basket still use the same identity-verification rule and are linked to the unified receipt. Line and shipping tax rates and amounts are snapshotted on the receipt. Configure category rates in `ApexPost.Web/appsettings.json` under `Taxation:Rates`; the demo defaults are 0% for Standard and Shipping because the assignment provides no approved tax values. Unknown categories fail closed. Payment methods are recorded for the training workflow; no external payment is processed. Transactions are stored in `data/counter-transactions.json`.

### Voids and refunds

Branch Managers can void or fully refund a completed counter transaction for their assigned branch. Both actions require a reason, are recorded with the manager and timestamp, restore the transaction's product quantities to branch inventory, and produce an audit event with the request correlation ID. Managers can also refund a standalone issued money order once, with a reason and correlated audit event. Money orders in a counter basket must be refunded through that receipt so the receipt and instrument are resolved together. Partial counter-transaction refunds are not supported. Inventory, transaction, and money order status are stored in separate JSON files, so a process or file failure between writes can require a retry or reconciliation; this training adapter does not provide cross-file atomicity.

## Branch reports

Administrators can select a branch; Branch Managers and Auditors are restricted to their assigned branch. The report summarizes completed sales, daily retail and service totals, voids and refunds, low-stock active products, shipping volume by zone and service, money orders, failed identity checks, the latest 20 operational notifications, and the latest 50 audit events. Service totals include Postage, Shipping, and Money Order catalogue lines, receipt shipping and money-order amounts, and standalone issued money orders. Daily grouping follows the server's local calendar date; rows distinguish completed receipts from standalone money orders. Independent JSON reads are loaded concurrently with `Task.WhenAll`. Notifications are simulated operational records in `data/notifications.json`; they do not send email, SMS, or external messages.

## Security and partner tracking

The web pipeline adds security headers, an optional maintenance response (`503`, with `/health` left available), loopback-only default access to administrative mutation routes and partner integration endpoints, a five-attempt-per-minute login limit, and a 30-request-per-minute partner API limit partitioned by configured partner ID. Unexpected server errors return safe `application/problem+json` responses with trace and correlation IDs; exception details remain in server logs. Configure each partner key through user-secrets or the environment before using `/api/partner/tracking/{transactionNumber}`. The legacy single `PartnerApi__ApiKey` setting remains supported as the `default` partner. Set `PartnerApi__AllowedCorsOrigins__0` to the approved tracking-widget origin; CORS does not replace the API key. Set `Security__AdminAllowedIpAddresses__0` and `Security__IntegrationAllowedIpAddresses__0` to the approved administrator and partner egress IPs. Both lists default to loopback only, so deployment addresses must be configured explicitly. When deploying behind a reverse proxy, configure trusted forwarded headers before relying on client IP restrictions. Missing or invalid partner keys receive 401; requests from unapproved IPs receive 403; if no partner keys are configured, the API returns 500.

Example local configuration and request:

    dotnet user-secrets init --project ApexPost.Web
    dotnet user-secrets set "PartnerApi:Partners:CourierNorth" "replace-with-a-local-secret" --project ApexPost.Web
    dotnet user-secrets set "PartnerApi:Partners:CourierSouth" "replace-with-another-local-secret" --project ApexPost.Web
    $env:PartnerApi__AllowedCorsOrigins__0 = "https://widget.example"
    dotnet run --project ApexPost.Web
    $trackingNumber = (Get-Content data/counter-transactions.json | ConvertFrom-Json | Where-Object { $_.shippingRateId } | Select-Object -First 1).transactionNumber
    curl.exe -H "X-API-Key: replace-with-a-local-secret" "https://localhost:5001/api/partner/tracking/$trackingNumber"

The tracking response contains receipt-level shipment type, service, zone, weight, status, and accepted time only; it does not expose customer names or identity data. `AUDIT-SAMPLE.json` contains synthetic event and MVC-filter audit entries. MVC action filters validate bound model state, audit configuration changes, sales, reversals, money-order issue/refund and identity-verification requests, and log slow actions at the configured `Diagnostics:SlowActionThresholdMilliseconds` threshold. Audit entries record actor, role, branch, safe route IDs, HTTP result status, duration, and correlation ID without serializing request bodies or sensitive form values. JSON requests with invalid models receive a validation problem response; HTML forms retain their controller-specific redisplay behavior. Transaction services publish events for committed sales, reversals, money orders, and identity verification outcomes.

## Money orders and identity verification

Counter Clerks, Branch Managers, and Administrators can issue money orders for an open branch. The flat fee and identity verification threshold are configured in `ApexPost.Web/appsettings.json`; the included values (`50.00` and `10000.00`) are demo defaults that can be replaced with trainer-approved rules. Amounts above the threshold require a successful identity check before an order is saved. In Development, the local simulated verifier accepts synthetic identifiers beginning with `DEMO-VALID-`; for example, `DEMO-VALID-1234`. Other values fail verification. Outside Development, verification fails closed until a real provider is configured. Raw identifiers are never persisted. The verification JSON records store the document type, outcome, and masked identifier; issued orders link to successful verification records. Replace the simulated verifier before any real use.

Never commit real secrets or production personal data.

## Versioning

The application starts at version 0.1.0 during development. Git tags use a v prefix (for example, v0.1.0). Increment the patch number for compatible fixes, the minor number for backward-compatible features, and the major number for incompatible changes. The first stable release is v1.0.0.

Architecture decisions are recorded in DESIGN.md and docs/adr/.
