# ADR 0017: Simulated operational notifications

## Status

Accepted

## Context

Branch staff need visibility into low inventory, high-value money orders, and transaction reversals. The assignment requires simulated notifications and the application already publishes operation events to decoupled observers for audit persistence.

## Decision

Add an Infrastructure event handler for low-stock threshold crossings, money orders above the configured identity-verification threshold, and counter transaction voids and refunds. Standalone money-order refunds also produce a notification; refunds performed as part of a counter transaction are represented by the counter reversal event to avoid duplicate alerts. Store notifications in `notifications.json` through an Application repository abstraction and show the latest 20 records on the branch report. Notifications include branch, actor, timestamp, correlation ID, and a reference to the affected product, instrument, or receipt.

Notification storage failures are logged and do not fail an operation that has already been committed. The records are simulated and remain local to the application; no email, SMS, or external notification integration is implied.

## Consequences

- Event observers remain separate from business services and JSON details remain in Infrastructure.
- Branch reports provide recent operational alerts alongside audit history.
- JSON persistence is suitable for the single-process training application and has the same concurrency and durability limits as the other JSON repositories.
- Notification delivery is not guaranteed if local persistence fails; the failure is logged for operators.

## Alternatives considered

- Send email or SMS from the business service: rejected because the assignment requests simulated notifications and no provider is configured.
- Write notification files directly from each use case: rejected because it would couple core workflows to one notification format.
- Publish application events to an observer that appends JSON: selected because it keeps notification handling out of transaction rules and remains replaceable.
