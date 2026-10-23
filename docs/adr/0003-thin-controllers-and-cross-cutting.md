# ADR-0003: Keep MVC controllers thin

- Status: Accepted
- Date: 2026-10-23

## Context

The assignment evaluates separation between HTTP-wide middleware concerns, MVC action filters, and business behavior.

## Decision

Controllers bind and validate transport input, call Application services, and select a response. Pricing, inventory, authorization decisions, transaction workflows, and report calculations remain in Domain or Application. Cross-cutting request behavior belongs in middleware; action-specific auditing and validation behavior belongs in filters where appropriate.

## Consequences

Use-case services can be tested without an HTTP server. Web-specific response models may be needed to keep transport concerns outside the domain.
