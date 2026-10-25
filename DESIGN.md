# Design overview

## Architecture

The solution uses a layered design with dependencies pointing toward the domain:

    ApexPost.Web -> ApexPost.Application -> ApexPost.Domain
    ApexPost.Web -> ApexPost.Infrastructure -> ApexPost.Application
    ApexPost.Infrastructure -> ApexPost.Domain
    ApexPost.Tests -> projects under test

MVC controllers translate HTTP input into application use cases and responses. Business rules belong in Domain and Application code. Infrastructure implements repository and integration interfaces.

```mermaid
flowchart LR
    Browser[Staff browser] --> Web[ApexPost.Web MVC]
    Partner[Tracking widget] -->|API key and CORS| Web
    Web --> App[ApexPost.Application]
    App --> Domain[ApexPost.Domain]
    App --> Ports[Repository and service interfaces]
    Ports --> Infra[ApexPost.Infrastructure]
    Infra --> Json[(JSON files)]
    Infra --> Audit[(Audit JSON)]
    Tests[ApexPost.Tests] --> Web
    Tests --> App
    Tests --> Domain
```

The following domain diagram shows the main entities and their ID-based associations. The JSON records store identifiers rather than object navigation properties.

```mermaid
classDiagram
    direction LR
    class Branch {
        +Guid Id
        +string Code
        +bool IsOpen
        +TimeOnly OpeningTime
        +TimeOnly ClosingTime
    }
    class StaffMember {
        +Guid Id
        +StaffRole Role
        +Guid? BranchId
        +bool IsActive
    }
    class CounterTerminal {
        +Guid Id
        +Guid BranchId
        +bool IsActive
    }
    class Product {
        +Guid Id
        +string Sku
        +string Category
        +decimal Price
        +string TaxCategory
    }
    class InventoryItem {
        +Guid BranchId
        +Guid ProductId
        +int QuantityOnHand
        +int ReorderLevel
    }
    class InventoryMovement {
        +Guid StaffId
        +int QuantityChange
        +DateTimeOffset OccurredAt
    }
    class CounterTransaction {
        +Guid Id
        +Guid BranchId
        +Guid StaffId
        +Guid? TerminalId
        +Guid? MoneyOrderId
        +Guid? ShippingRateId
        +TransactionStatus Status
        +decimal TotalAmount
    }
    class TransactionLine {
        +Guid ProductId
        +int Quantity
        +decimal UnitPrice
        +decimal TaxAmount
    }
    class ShippingZone {
        +Guid Id
        +string Code
        +string Destination
    }
    class ShippingRate {
        +Guid Id
        +Guid ZoneId
        +ShippingServiceLevel ServiceLevel
        +decimal MaximumWeightKilograms
    }
    class MoneyOrder {
        +Guid Id
        +Guid BranchId
        +Guid StaffId
        +Guid? VerificationRecordId
        +decimal Amount
        +MoneyOrderStatus Status
    }
    class IdentityVerificationRecord {
        +Guid Id
        +Guid BranchId
        +Guid StaffId
        +IdentityVerificationStatus Status
        +string MaskedDocumentIdentifier
    }
    Branch "1" --> "0..*" StaffMember : home branch
    Branch "1" --> "0..*" CounterTerminal : has
    Branch "1" --> "0..*" InventoryItem : stocks
    Product "1" --> "0..*" InventoryItem : stocked as
    InventoryItem "1" *-- "0..*" InventoryMovement : history
    StaffMember "1" --> "0..*" InventoryMovement : records
    Branch "1" --> "0..*" CounterTransaction : accepts
    StaffMember "1" --> "0..*" CounterTransaction : processes
    CounterTerminal "0..1" --> "0..*" CounterTransaction : used for
    CounterTransaction "1" *-- "0..*" TransactionLine : contains
    Product "1" --> "0..*" TransactionLine : snapshotted as
    ShippingZone "1" --> "0..*" ShippingRate : offers
    ShippingRate "0..1" --> "0..*" CounterTransaction : selected by
    MoneyOrder "0..1" --> "0..1" CounterTransaction : included in
    Branch "1" --> "0..*" MoneyOrder : issues
    StaffMember "1" --> "0..*" MoneyOrder : issues
    MoneyOrder "0..1" --> "0..1" IdentityVerificationRecord : verified by
    Branch "1" --> "0..*" IdentityVerificationRecord : records
    StaffMember "1" --> "0..*" IdentityVerificationRecord : performs
```

## Middleware and MVC filters

The HTTP pipeline is ordered as follows so cross-cutting checks happen before endpoint execution:

```text
Correlation ID
  -> Structured request logging and timing
  -> Global exception handler (Problem Details)
  -> HSTS (outside Development)
  -> Exception logging
  -> Security headers
  -> Maintenance gate (/health bypass)
  -> HTTPS redirection
  -> Routing
  -> Endpoint CORS
  -> Admin and integration IP allowlists
  -> Partner API key
  -> Rate limiter
  -> Authentication
  -> Authorization
  -> MVC or partner endpoint
```

Middleware handles concerns that apply to the HTTP request regardless of which MVC action runs: correlation, structured request timing, safe error responses, error logging, headers, maintenance, CORS, rate limits, IP checks, and partner keys. `RequestLoggingMiddleware` logs method, path, final status, and elapsed milliseconds in a `finally` block, so failed requests are timed too. It runs outside the global exception handler and shares the correlation ID logging scope. The exception handler emits an `application/problem+json` response with a safe message, trace ID, and correlation ID; it never serializes exception details. MVC action filters run around controller actions for model validation, slow-action timing, and auditing. Domain and Application services remain responsible for business rules; filters do not decide whether a stock change, sale, or refund is valid.

| Concern | Mechanism | Why |
|---|---|---|
| Correlation, request logging/timing, Problem Details exception handling, error logging, security headers, maintenance, CORS, rate limits, IP allowlists, partner keys | Middleware | These checks apply at the HTTP request boundary before MVC action selection or model binding. Request logging wraps the exception handler to capture its final status and total duration. The exception handler returns safe RFC-style problem responses with trace and correlation IDs; the partner key middleware sets a configured partner identity before the endpoint rate limiter partitions requests. |
| Antiforgery tokens | Built-in MVC filter | The check uses MVC form and endpoint metadata and applies to unsafe MVC actions. |
| Invalid model state | `ModelValidationActionFilter` | It runs after MVC model binding. JSON callers receive a consistent `400` validation problem; HTML forms retain the controller's correctly typed view and validation messages. |
| Audit of configuration and business actions | `MvcAuditActionFilter` | It needs controller/action identity, the authenticated staff principal, safe route keys, the MVC result, and action duration. Application events separately record committed sales, reversals, money-order issuance/refunds, and identity verification outcomes. |
| Slow-action warnings | `RequestTimingActionFilter` | It measures controller/action execution and reports an MVC action name with the elapsed duration. |

## Domain flow

```mermaid
sequenceDiagram
    actor Staff
    participant MVC as MVC Controller
    participant App as Application Service
    participant Domain as Domain Rules
    participant Repo as JSON Repository
    participant Audit as Audit Event Handler
    Staff->>MVC: Submit counter operation
    MVC->>App: Pass branch, actor, values, correlation ID
    App->>Repo: Load branch, staff, stock, and rates
    App->>Domain: Validate and calculate
    Domain-->>App: Valid receipt or rejection
    App->>Repo: Save receipt and stock movement
    App->>Audit: Publish completed-operation event
    MVC-->>Staff: Confirmation or safe error
```

## Staff authentication slice

- Staff and branch records are modeled in Domain.
- Application authentication checks credentials, rejects inactive staff, and resolves assigned branch context.
- Infrastructure persists branches and staff in JSON files under the root data directory.
- Passwords are hashed with ASP.NET Core's PasswordHasher; plaintext demo passwords are not written to JSON.
- The MVC host uses secure cookie authentication, a fallback authenticated-user policy, and antiforgery validation for unsafe MVC actions. Development uses ephemeral data-protection keys; production must configure durable protected keys for multi-instance deployments.
- Role, staff ID, and assigned branch are emitted as claims after successful authentication.

## Branch and staff administration slice

The Administrator-only MVC area lists branches and staff, creates branches with local opening/closing times, manages branch terminals, creates and edits staff accounts, and activates or deactivates staff accounts. `AdministrationController` handles web forms and authorization. `StaffManagementService` checks duplicate branch codes, terminal codes, and email addresses, validates branch assignment, delegates password hashing, and prevents deactivating or reassigning the last active Administrator. Staff profile edits preserve the existing password hash and staff ID. Each sale must use an active terminal assigned to its branch, and the terminal ID is retained on the receipt. Terminals are stored behind `ICounterTerminalRepository` in `counter-terminals.json`. The service depends on Application repository abstractions; Infrastructure provides the JSON-backed implementations. This JSON read-modify-write workflow is intended for the single-process training application and does not provide transactional multi-user concurrency guarantees.

## Product catalogue slice

`Product` owns SKU and price invariants. `ProductCatalogueService` searches the repository and handles create/activation operations. `CatalogController` makes catalogue browsing available to signed-in staff and restricts product changes to Administrators and Branch Managers. The repository abstraction is implemented by Infrastructure using `data/products.json`; the development seeder supplies 30 products when no product data exists. Prices are stored as decimal amounts with two fractional digits and displayed as INR in the MVC view. Inventory quantities and stock movements belong to a later feature and are not part of this catalogue record.

## Inventory slice

`InventoryItem` is scoped to one branch and product. It stores quantity on hand, a reorder level, and its stock movement history together so each update is written through one JSON collection replacement. `InventoryManagementService` joins active catalogue products with branch stock, validates branch/product state, prevents negative stock, and appends the signed-in staff member and reason to each movement. `InventoryController` lets signed-in staff view only their assigned branch; Administrators can select a branch, and only Administrators and Branch Managers can change stock. The JSON repository is suitable for this single-process training application and does not provide multi-process transaction guarantees.

## Shipping rates slice

`ShippingZone` models a destination group, while `ShippingRate` defines the service level (Standard, Express, or Registered), base charge, per-kilogram charge, and maximum supported weight. The Domain rate calculates a quote and enforces non-negative two-decimal prices and configured weight limits. `ShippingRateService` coordinates zone/rate repositories, prevents duplicate zone codes and duplicate service levels within a zone, and calculates quotes. A parcel captures recipient name, destination address, weight, and three positive dimensions; a letter captures recipient, destination, and weight without parcel dimensions. Both use the configured rate and weight limit, and the shipment type and selected service are retained on the receipt and tracking response. The label preview checks that the receipt belongs to the caller's branch and renders printable shipping metadata without exposing unrelated branch data. Infrastructure stores the two collections separately and development seeding creates 10 zones with all three service levels, adding any missing Registered rates to existing seeded data. `ShippingController` allows all signed-in staff to view and quote; only Administrators can add zones and rates.

## Counter transactions slice

`CounterTransaction` stores a completed receipt with terminal ID, line-item price, tax category, tax rate, and tax amount snapshots, optional shipping quote and shipping tax details, optional money-order ID/principal/fee, payment method, branch, staff member, and timestamp. A basket can combine products, parcel shipping, and a money order; the money-order principal and configured fee contribute to the same receipt total. The money order still goes through `MoneyOrderService` identity checks and is linked to the receipt. Configured tax category rates are applied at checkout; unconfigured categories fail closed. The demo configuration uses 0% Standard and Shipping rates until approved rates are supplied. `CounterTransactionService` checks active staff and branch/product/terminal availability, calculates shipping using the existing service, deducts stock with a linked inventory movement, and persists the receipt. `CounterTransactionsController` enforces branch scope from the authenticated claims and allows Clerks, Managers, and Administrators to create transactions; Auditors can review. The training JSON implementation writes inventory, receipts, and money orders separately, so it does not provide a cross-file database transaction or external payment settlement.

Branch Managers can void or fully refund a completed counter transaction at their assigned open branch. The domain transition requires a non-empty reason, records the manager and timestamp, and prevents a second reversal. The application service checks the live staff record and branch assignment, restores the product quantities, changes the stored transaction status, and publishes an audit event. Managers can also refund an issued money order once; the order records its refund actor/time/reason and an event is written to the audit log. Partial counter-transaction refunds are not supported. Since the JSON adapter has no cross-file transaction, the receipt service compensates a failed inventory write by restoring the original receipt status; money order status and audit persistence remain separate file operations.

## Branch reports and demo history

`ReportingService` concurrently reads branch, sales, inventory, catalogue, money-order, identity-verification, audit, and notification data using `Task.WhenAll`. It filters every measure by branch and calculates completed sales, daily retail/service sales, low-stock active products, completed shipping volume by zone and service, money-order totals, failed identity checks, reversals, the 20 newest operational notifications, and the 50 newest audit events. Daily summaries include completed receipts and standalone issued money orders, include associated tax, and group by the server's local calendar date. Catalogue lines in Postage, Shipping, and Money Order categories are classified as services; other catalogue lines are retail, while receipt shipping and money-order amounts also count as services. Linked money orders are not counted twice. Administrators can select a branch; Branch Managers and Auditors are constrained to their assigned branch in the controller. Development seeding creates 40 deterministic historical transactions and matching opening stock movements when transaction and inventory collections are both empty.

## Simulated operational notifications

Application events signal a stock transition from above the reorder threshold to below it, a money order whose principal exceeds the configured identity-verification threshold, and every counter-transaction void or refund. Standalone money-order refunds also emit a notification; a money order refunded as part of a counter receipt is represented by the receipt reversal notification to avoid duplicate alerts. `OperationalNotificationHandler` observes these events and appends branch-scoped records to `notifications.json`. Persistence failures are logged and do not turn an already committed sale or refund into a failed response. The branch report shows the 20 most recent records with time, type, message, and correlation ID. This is a simulated in-app record; no external notification provider is called.

## Money orders and identity verification

`MoneyOrderService` verifies that the branch is open and the current staff record is active, authorized, and assigned to the selected branch before issuing an order. The configured flat fee is added to the order amount. When the amount exceeds the configured threshold, the service calls `IIdentityVerificationService`, stores a verification attempt with a masked identifier and result, and issues the money order only for a successful result. The order stores a reference to that verification record. Development uses a simulated adapter that accepts only synthetic identifiers with the configured `DEMO-VALID-` prefix; other environments fail closed until a real provider is configured. Money orders and verification attempts use separate JSON repositories, so they do not have cross-file transaction guarantees.

## Persistence

Assignment 2 requires JSON file persistence behind interfaces. The JSON implementation remains in Infrastructure so a later database adapter can replace it without changing business rules. JSON writes use a semaphore and a temporary file replacement; this protects a single process from overlapping writes but does not provide multi-process transactions.

## Security and audit

Cookie authentication, server-side role and branch checks, antiforgery validation, correlation IDs, structured request logging and timing, safe Problem Details exception responses, request exception logging, security headers, maintenance mode, IP restrictions, rate limiting, partner API keys, and a restricted tracking-widget CORS policy are in place. Middleware order is: correlation ID; request logging and timing; global exception handler; HSTS outside Development; exception logging; security headers; maintenance gate; HTTPS redirection; routing; endpoint CORS; admin and integration IP allowlists; partner API-key validation; rate limiter; authentication; authorization; endpoints. Each request log records method, path, final status, and elapsed milliseconds, including failed requests; correlation ID is included in the logger scope. Admin mutations and `/api/partner` require exact IP matches from separate configuration lists; a missing or empty list denies access. Unexpected errors return `application/problem+json` with a safe detail, trace ID, and correlation ID; stack traces and exception messages stay in server logs. Partner keys are configured by partner ID, compared using fixed-time hashes, and never written to logs. `/health` is anonymous and remains available during maintenance. Missing or invalid keys return 401; requests from unapproved IPs return 403; an unconfigured partner API returns 500.

MVC action filters log slow actions and append request audit records for branch status/hours, terminals, staff, catalogue, inventory, shipping rates, sales, reversals, money-order issue/refund, and identity verification requests. Each MVC audit entry records actor, role, branch, action, whitelisted IDs, result status, duration, and correlation ID. The filter never serializes action models or request bodies. Application events separately append committed sales, reversals, money-order issuance/refunds, and identity verification outcomes to `audit-records.json`; identity audit events contain document type/result but no document identifier. Audit failures are logged without making a committed operation appear unsuccessful; JSON audit storage therefore has an acknowledged consistency gap. Sensitive identity values are used transiently by the simulated verifier and masked before persistence. Partner API keys are supplied through secret configuration and compared in constant time. The default admin allowlist is loopback-only; deployments behind proxies must configure trusted forwarded headers before IP restrictions can identify the original client.

| Threat | Mitigation | Remaining limitation |
|---|---|---|
| Password guessing | Login is limited to five attempts per minute per client IP; passwords are stored as ASP.NET Core password hashes and login errors do not reveal which credential failed. | IP-based limits depend on correctly configured forwarded headers behind a proxy. |
| Unauthorized staff or cross-branch access | Cookie claims, role authorization, branch-scoped controllers, and application-service checks enforce staff and branch boundaries. | A signed-in user's cookie claims remain a snapshot until the cookie is renewed or expires. |
| Cross-site form submission | Global antiforgery validation protects unsafe MVC actions. | External clients still need authentication and authorization; antiforgery is not an API credential. |
| Partner API key theft or guessing | Unique partner keys live in user-secrets or deployment secret configuration, are compared using fixed-time hash comparisons, and are never written to logs. Each validated partner gets an independent request limit. | CORS only controls browser behavior; it does not authenticate a partner or prevent direct HTTP clients. |
| API overload | Partner requests are limited to 30 per minute per configured partner; login requests use a separate limit. | The fixed-window policy can allow bursts around a window boundary. |
| Forged client IP behind a proxy | Admin and partner integration routes use separate configured IP allowlists; forwarded headers must be restricted to trusted proxies before the app relies on them. | Trusting arbitrary forwarded headers can let clients spoof their IP and bypass an allowlist. |
| Exposure of personal or payment information | Identity document numbers are masked before persistence; audit filters whitelist identifiers and never serialize form bodies; partner tracking omits recipient and identity information. | Local JSON files still require appropriate filesystem access controls and backups. |
| Audit loss or alteration | Business events and MVC requests write append-only JSON audit entries with actor, branch, time, and correlation context; write failures are logged. | JSON audit storage is best-effort and is not tamper-evident or multi-process transactional. |
| Concurrent or partial JSON writes | Shared writes use `SemaphoreSlim` and temporary-file replacement. | The guarantee is single-process; it does not coordinate multiple application instances. |
| Script injection, framing, or MIME confusion | Content Security Policy, frame restrictions, `nosniff`, and referrer headers are set centrally. | Policy directives need review when new scripts, integrations, or external origins are added. |

## SOLID mapping and advanced C# usage

| Principle or feature | ApexPost example | Reason |
|---|---|---|
| Single Responsibility | Controllers bind HTTP input; Application services coordinate use cases; Infrastructure owns JSON storage | Each layer has one reason to change. |
| Open/Closed | `IInventoryRepository`, `ICounterTransactionRepository`, and `IIdentityVerificationService` | A database or provider adapter can be substituted without rewriting business rules. |
| Liskov Substitution | JSON adapters and test doubles implement the same repository contracts | Callers depend on behavior expressed by the interface, not concrete storage. |
| Interface Segregation | Separate branch, staff, inventory, shipping, and transaction repository interfaces | Consumers receive only the operations they use. |
| Dependency Inversion | Application services accept repository and publisher abstractions | High-level business workflows do not depend on JSON implementation details. |
| Records | Domain entities, request events, report models, and audit records | Immutable snapshots make business state and event data explicit. |
| Pattern matching | Enum `switch` expressions and `is`/`is not` checks in workflow validation | Branching rules stay readable and exhaustive. |
| Generics | `JsonCollectionStore<T>` and `IApplicationEventHandler<T>` | Shared persistence and event dispatch avoid duplicated type-specific plumbing. |
| Delegates and `Func` | `LowStockRule` accepts a `Func<int, int, bool>` predicate used by branch reporting | The low-stock comparison is an explicit policy that can be replaced and tested without rewriting report orchestration. |
| Events and observers | Counter completion, money-order issuance, and reversal events | Audit observers are decoupled from the business operation. |
| Async and cancellation | Repository contracts accept `CancellationToken` and use asynchronous file I/O | Request cancellation can flow through the storage boundary. |
| `Task.WhenAll` | `ReportingService` loads independent branch report collections concurrently | The report waits on the slowest read rather than serializing unrelated reads. |
| `SemaphoreSlim` | JSON collection writes and audit appends | A single process serializes writes to each shared JSON collection. |

The implementation uses eight requested C# techniques: records, pattern matching, generics, delegates/`Func`, events, async/cancellation, `Task.WhenAll`, and `SemaphoreSlim`. `ValueTask`, cache layers, custom-attribute reflection, extension members, and a `Money`/`Weight` value-object hierarchy are not used because the current workload does not justify their complexity.

## Decisions

See docs/adr/ for accepted architecture decision records. The domain, middleware, filter, security, audit, performance, and SOLID notes above describe the current implementation and its known limitations.
