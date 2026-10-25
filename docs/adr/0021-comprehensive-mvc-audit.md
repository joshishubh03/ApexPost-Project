# ADR 0021: Comprehensive MVC audit context

- Status: Accepted
- Date: 2026-10-25

## Context

The original MVC audit filter covered successful configuration changes but did not record action duration, result status, role, or whitelisted route identifiers. Important business requests also need an MVC audit trail, while committed business events already have separate event observers that write domain outcomes.

## Decision

Expand `MvcAuditActionFilter` to record configuration changes, counter sale/reversal actions, money-order issue/refund actions, and inventory adjustments. Each filter record stores the authenticated actor, role, branch, controller/action, safe route identifiers, HTTP result status, duration, and correlation ID. The filter never serializes bound models, request bodies, or arbitrary route values. Existing application event handlers remain responsible for committed business outcomes.

Record identity verification outcomes through a dedicated application event after the verification attempt is persisted. The audit event includes the verification ID, actor, branch, document type, result, amount, and correlation ID; it never includes the masked or full document identifier.

## Consequences

- Audit entries distinguish an MVC request from a committed business event and can be correlated using the request ID.
- Failed or rejected requests are traceable by their result status, while the domain event records whether a business operation committed.
- Audit entries do not contain passwords, identity numbers, API keys, card data, or free-form request models.
- Audit appends remain best-effort after operations have committed; failures are logged for operators.
