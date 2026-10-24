# Design overview

## Architecture

The solution uses a layered design with dependencies pointing toward the domain:

    ApexPost.Web -> ApexPost.Application -> ApexPost.Domain
    ApexPost.Web -> ApexPost.Infrastructure -> ApexPost.Application
    ApexPost.Infrastructure -> ApexPost.Domain
    ApexPost.Tests -> projects under test

MVC controllers translate HTTP input into application use cases and responses. Business rules belong in Domain and Application code. Infrastructure implements repository and integration interfaces.

## Staff authentication slice

- Staff and branch records are modeled in Domain.
- Application authentication checks credentials, rejects inactive staff, and resolves assigned branch context.
- Infrastructure persists branches and staff in JSON files under the root data directory.
- Passwords are hashed with ASP.NET Core's PasswordHasher; plaintext demo passwords are not written to JSON.
- The MVC host uses secure cookie authentication, a fallback authenticated-user policy, and antiforgery validation for unsafe MVC actions. Development uses ephemeral data-protection keys; production must configure durable protected keys for multi-instance deployments.
- Role, staff ID, and assigned branch are emitted as claims after successful authentication.

## Branch and staff administration slice

The Administrator-only MVC area lists branches and staff, creates branches and staff accounts, and activates or deactivates staff accounts. `AdministrationController` handles web forms and authorization. `StaffManagementService` checks duplicate branch codes and email addresses, validates branch assignment, delegates password hashing, and prevents deactivating the last active Administrator. The service depends on Application repository abstractions; Infrastructure provides the JSON-backed implementations. This JSON read-modify-write workflow is intended for the single-process training application and does not provide transactional multi-user concurrency guarantees.

## Product catalogue slice

`Product` owns SKU and price invariants. `ProductCatalogueService` searches the repository and handles create/activation operations. `CatalogController` makes catalogue browsing available to signed-in staff and restricts product changes to Administrators and Branch Managers. The repository abstraction is implemented by Infrastructure using `data/products.json`; the development seeder supplies 30 products when no product data exists. Prices are stored as decimal amounts with two fractional digits and displayed as INR in the MVC view. Inventory quantities and stock movements belong to a later feature and are not part of this catalogue record.

## Inventory slice

`InventoryItem` is scoped to one branch and product. It stores quantity on hand, a reorder level, and its stock movement history together so each update is written through one JSON collection replacement. `InventoryManagementService` joins active catalogue products with branch stock, validates branch/product state, prevents negative stock, and appends the signed-in staff member and reason to each movement. `InventoryController` lets signed-in staff view only their assigned branch; Administrators can select a branch, and only Administrators and Branch Managers can change stock. The JSON repository is suitable for this single-process training application and does not provide multi-process transaction guarantees.

## Shipping rates slice

`ShippingZone` models a destination group, while `ShippingRate` defines the service level, base charge, per-kilogram charge, and maximum supported weight. The Domain rate calculates a quote and enforces non-negative two-decimal prices and valid parcel weight. `ShippingRateService` coordinates zone/rate repositories, prevents duplicate zone codes and duplicate service levels within a zone, and calculates quotes. Infrastructure stores the two collections separately and development seeding creates 10 zones with Standard and Express rates. `ShippingController` allows all signed-in staff to view and quote; only Administrators can add zones and rates. Quote calculation is kept as a separate operation so a later counter transaction can use the same pricing rules without duplicating them.

## Counter transactions slice

`CounterTransaction` stores a completed receipt with line-item price snapshots, optional shipping quote details, payment method, branch, staff member, and timestamp. `CounterTransactionService` checks branch/product availability, calculates shipping using the existing service, deducts stock with a linked inventory movement, and persists the receipt. `CounterTransactionsController` enforces branch scope from the authenticated claims and allows Clerks, Managers, and Administrators to create transactions; Auditors can review. The training JSON implementation writes inventory and receipt collections separately, so it does not provide a cross-file database transaction or external payment settlement.

## Money orders and identity verification

`MoneyOrderService` verifies that the branch is open and the current staff record is active, authorized, and assigned to the selected branch before issuing an order. The configured flat fee is added to the order amount. When the amount exceeds the configured threshold, the service calls `IIdentityVerificationService`, stores a verification attempt with a masked identifier and result, and issues the money order only for a successful result. The order stores a reference to that verification record. Development uses a simulated adapter that accepts only synthetic identifiers with the configured `DEMO-VALID-` prefix; other environments fail closed until a real provider is configured. Money orders and verification attempts use separate JSON repositories, so they do not have cross-file transaction guarantees.

## Persistence

Assignment 2 requires JSON file persistence behind interfaces. The JSON implementation remains in Infrastructure so a later database adapter can replace it without changing business rules. JSON writes use a semaphore and a temporary file replacement; this protects a single process from overlapping writes but does not provide multi-process transactions.

## Security and audit

Authentication, role and branch authorization, correlation middleware, request exception logging, and audit events are in place. Counter sales and money order issuance publish application events that Infrastructure handlers append to `audit-records.json`. Audit failure is logged without making a committed sale or order appear unsuccessful; the JSON implementation therefore has an acknowledged audit consistency gap. Sensitive identity values are used transiently by the simulated verifier and masked before persistence. API-key/IP restrictions, rate limiting, security headers, maintenance mode, CORS policy, and action filters remain future slices.

## Decisions

See docs/adr/ for accepted architecture decision records. This document will expand with domain diagrams, middleware ordering, filter-versus-middleware guidance, security and audit design, and updated SOLID mapping as those features land.
