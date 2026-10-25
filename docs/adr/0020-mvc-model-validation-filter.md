# ADR 0020: MVC model validation filter

- Status: Accepted
- Date: 2026-10-25

## Context

The MVC controllers validate bound form models and many actions redisplay their typed view when input is invalid. JSON clients also need a consistent validation response. Middleware cannot inspect MVC model state because model binding happens after the middleware pipeline.

## Decision

Register `ModelValidationActionFilter` globally. It short-circuits invalid JSON requests with a `400` `ValidationProblemDetails` response. For HTML requests it lets the action use the existing typed redisplay behavior; if an invalid model does not produce a view, the filter returns the same validation problem response with status 400.

## Consequences

- Validation responses are consistent for JSON clients and invalid HTML forms continue to show their existing model errors.
- The filter runs after model binding, while middleware remains responsible for transport-level checks.
- Domain invariants remain enforced in application/domain services and are not replaced by MVC validation.

## Alternatives considered

- Validate only in middleware: rejected because middleware runs before MVC model binding and cannot inspect `ModelState`.
- Repeat validation response logic in every controller: rejected because it creates inconsistent handling across HTML and JSON actions.
- Replace domain validation with model annotations: rejected because HTTP input validation must not be the only protection for business invariants.
