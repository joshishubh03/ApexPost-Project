# ADR 0017: Simulated operational notifications

## Status

Accepted

## Context

Branch staff need visibility into low inventory, high-value money orders, and transaction reversals. The assignment requires simulated notifications and the application already publishes operation events to decoupled observers for audit persistence.

## Decision

Add an Infrastructure event handler for low-stock threshold crossings, money orders above the configured identity-verification threshold, and counter transaction voids and refunds. Standalone money-order refunds also produce a notification; refunds performed as part of a counter transaction are represented by the counter reversal event to avoid duplicate alerts. Store notifications in `notifications.json` through an Application repository abstraction and show the latest 20 records on the branch report. Notifications include branch, actor, timestamp, correlation ID, and a reference to the affected product, instrument, or receipt.

Notification appends use the cross-process mutation coordinator. Notifications emitted during a business mutation are staged with that operation, so the business records and notification are recovered together. The records are simulated and remain local to the application; no email, SMS, or external notification integration is implied.

## Consequences

- Event observers remain separate from business services and JSON details remain in Infrastructure.
- Branch reports provide recent operational alerts alongside audit history.
- JSON appends coordinate separate application processes and can recover from a process interruption, subject to the local filesystem assumptions in ADR-0030.
- Notification persistence is local and not tamper-evident; filesystem failures are logged and prevent the related mutation from committing.

## Alternatives considered

- Send email or SMS from the business service: rejected because the assignment requests simulated notifications and no provider is configured.
- Write notification files directly from each use case: rejected because it would couple core workflows to one notification format.
- Publish application events to an observer that appends JSON: selected because it keeps notification handling out of transaction rules and remains replaceable.
