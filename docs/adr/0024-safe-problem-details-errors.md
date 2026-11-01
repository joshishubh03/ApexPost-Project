# ADR 0024: Safe Problem Details for unhandled errors

- Status: Accepted
- Date: 2026-10-25

## Context

The global production exception route returned an HTML view. The assignment requires a safe Problem Details response with a trace ID, while internal exception details must not reach callers.

## Decision

Register `SafeProblemDetailsExceptionHandler` with ASP.NET Core's exception-handler middleware. It returns `application/problem+json` with a generic title and detail, the request path, a trace ID, and the existing correlation ID. It clears any uncommitted partial response before writing and restores the correlation header. It does not serialize exception messages or stack traces. `RequestExceptionLoggingMiddleware` remains inside the global handler and records the exception for operators.

## Consequences

- MVC and integration callers receive a consistent machine-readable 500 response.
- Callers can provide the trace or correlation ID to support without learning internal exception details.
- An exception raised after the response has started cannot be replaced with a Problem Details document; the handler leaves that response to ASP.NET Core's exception handling behavior.

## Alternatives considered

- Return an HTML error view from every unhandled exception: rejected because integration clients need a consistent machine-readable response.
- Include exception messages and stack traces in the response: rejected because they can reveal paths, implementation details, or sensitive values.
- Return generic Problem Details with trace and correlation identifiers: selected so callers can report an error without seeing internal details.
