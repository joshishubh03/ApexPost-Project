# ADR 0026: Structured request logging and timing

- Status: Accepted
- Date: 2026-10-25

## Context

MVC action timing reports slow controller actions, and `RequestExceptionLoggingMiddleware` records unhandled exceptions. The platform also needs a request-wide record of method, path, final response status, and elapsed time, including failures and non-MVC endpoints.

## Decision

Add `RequestLoggingMiddleware` outside the global exception handler. It measures the full downstream pipeline and emits one structured information log in a `finally` block. The log includes method, path without query string, final status, and elapsed milliseconds. If an exception escapes the exception handler, the log records status 500 and rethrows it. Correlation middleware remains outermost so the log is inside the correlation logger scope.

## Consequences

- Requests through MVC, health, and partner endpoints receive consistent timing records.
- Handled exceptions are logged with their final 500 status after Problem Details generation; exception details remain in the existing error log.
- The MVC slow-action filter remains useful for identifying a specific controller action, while this middleware measures the whole HTTP pipeline.
