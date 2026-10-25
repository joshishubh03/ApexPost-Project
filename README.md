# ApexPost Counter Platform

Assignment 2 implementation of a secure retail and postal counter platform using .NET 10, C# 14, and ASP.NET Core MVC.

## Current stage

The staff-auth slice adds branch and staff domain records, JSON demo data, and cookie-based staff sign-in. The branch-staff-management slice adds Administrator-only branch and staff management. The product-catalogue slice adds product search and management, with 30 seeded demo products. The inventory slice tracks branch-specific quantities and a reasoned movement history. The shipping-rates slice defines destination zones, service levels, and weight-based quotes. Counter transactions record product sales, optional shipping, payment method, and inventory deductions. Branch Managers can void or fully refund completed receipts with a reason. Branch reports summarize sales, stock, shipping, money orders, identity checks, and audit events. The money-order slice adds configurable fees, high-value identity verification, and masked document recording. Development seeding supplies 40 historical transactions.

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

The demo data is seeded on first run in Development. The seed creates five branches, at least 20 staff, 30 products, 10 shipping zones with Standard and Express rates, opening inventory, and 40 historical transactions when both the inventory and transaction files are empty. All seeded accounts use the password CounterDemo!2026. Example accounts:

- admin@apexpost.local
- manager1@apexpost.local
- clerk11@apexpost.local
- auditor1@apexpost.local

The demo password is public training data and must never be reused outside this local demo. Password hashes, not plaintext passwords, are stored in JSON.

## Branch and staff administration

Sign in as the seeded Administrator to use the **Branches** and **Staff** links. Administrators can create branches, create staff accounts, and activate or deactivate accounts. New passwords are hashed before they are saved. Non-administrator staff must be assigned to an existing branch, and the last active Administrator cannot be deactivated.

The MVC administration controller calls `StaffManagementService` in the Application layer. That service uses repository interfaces; Infrastructure implements those interfaces with the JSON files in `data/`. This keeps web form handling, business rules, and persistence in their respective layers.

## Product catalogue

All signed-in staff can search active products by SKU, name, category, or description. Administrators and Branch Managers can create products and activate or deactivate them. Product SKUs are normalized to uppercase and must be unique; prices are non-negative and support up to two decimal places. Development seeding creates 30 sample catalogue products in `data/products.json` when the file is empty or missing.

## Inventory

Staff can view active-product stock for their assigned branch. Administrators can select a branch; Administrators and Branch Managers can record stock changes with a reason and reorder level. Each record stores the current quantity and recent movements. A stock change cannot reduce quantity below zero. Inventory data is written to `data/inventory.json` when the first adjustment is saved.

## Shipping zones and rates

Signed-in staff can view the shipping schedule and calculate a quote. Administrators can add zones and rates. Each rate has a service level, base charge, per-kilogram charge, and maximum weight. Development seeding provides 10 zones and Standard/Express rates in `data/shipping-zones.json` and `data/shipping-rates.json`.

## Counter transactions

Counter Clerks, Branch Managers, and Administrators can record sales for their assigned branch. A transaction can contain multiple catalogue products and an optional shipping service. Completing a sale checks available stock, stores price and product snapshots, records the payment method, and decreases inventory. Payment methods are recorded for the training workflow; no external payment is processed. Transactions are stored in `data/counter-transactions.json`.

### Voids and refunds

Branch Managers can void or fully refund a completed counter transaction for their assigned branch. Both actions require a reason, are recorded with the manager and timestamp, restore the transaction's product quantities to branch inventory, and produce an audit event with the request correlation ID. A transaction can be reversed only once. Partial refunds and money-order refunds are not implemented yet. Inventory and transaction status are stored in separate JSON files, so a process or file failure between writes can leave them out of sync; this training adapter does not provide cross-file atomicity.

## Branch reports

Administrators can select a branch; Branch Managers and Auditors are restricted to their assigned branch. The report summarizes completed sales, voids and refunds, low-stock active products, shipping volume by zone and service, money orders, failed identity checks, and the latest 50 audit events. Independent JSON reads are loaded concurrently with `Task.WhenAll`.

## Security and partner tracking

The web pipeline adds security headers, an optional maintenance response (`503`, with `/health` left available), loopback-only default access to `/Administration`, a five-attempt-per-minute login limit, and a 30-request-per-minute partner API limit. Set `PartnerApi__ApiKey` through the environment or a secret store before using `/api/partner/tracking/{transactionNumber}`. Set `PartnerApi__AllowedCorsOrigins__0` to the approved tracking-widget origin; CORS does not replace the API key. Configure `Security__AdminAllowedIpAddresses__0` for the deployment's admin IPs. When deploying behind a reverse proxy, configure trusted forwarded headers before relying on client IP restrictions.

Example local configuration and request:

    $env:PartnerApi__ApiKey = "replace-with-a-local-secret"
    $env:PartnerApi__AllowedCorsOrigins__0 = "https://widget.example"
    dotnet run --project ApexPost.Web
    curl.exe -H "X-API-Key: replace-with-a-local-secret" https://localhost:5001/api/partner/tracking/APT-20261020-DEMO0001

The tracking response contains receipt-level service, zone, weight, status, and accepted time only; it does not expose customer names or identity data. `AUDIT-SAMPLE.json` contains synthetic audit entries. MVC action filters audit successful branch, staff, product, stock, and shipping configuration changes; transaction services publish events for sales, reversals, and money orders. Slow MVC actions are logged at the configured `Diagnostics:SlowActionThresholdMilliseconds` threshold.

## Money orders and identity verification

Counter Clerks, Branch Managers, and Administrators can issue money orders for an open branch. The flat fee and identity verification threshold are configured in `ApexPost.Web/appsettings.json`; the included values (`50.00` and `10000.00`) are demo defaults that can be replaced with trainer-approved rules. Amounts above the threshold require a successful identity check before an order is saved. In Development, the local simulated verifier accepts synthetic identifiers beginning with `DEMO-VALID-`; for example, `DEMO-VALID-1234`. Other values fail verification. Outside Development, verification fails closed until a real provider is configured. Raw identifiers are never persisted. The verification JSON records store the document type, outcome, and masked identifier; issued orders link to successful verification records. Replace the simulated verifier before any real use.

Never commit real secrets or production personal data.

## Versioning

The application starts at version 0.1.0 during development. Git tags use a v prefix (for example, v0.1.0). Increment the patch number for compatible fixes, the minor number for backward-compatible features, and the major number for incompatible changes. The first stable release is v1.0.0.

Architecture decisions are recorded in DESIGN.md and docs/adr/.
