# ADR-0032: Cache currency validation metadata and measure catalogue reads

- Status: Accepted
- Date: 2026-10-30

## Context

MVC model validation uses data annotations, but currency-specific constraints were duplicated across monetary form fields. Reflection-based validation was absent, and the catalogue cache was justified without local measurements.

## Decision

Add a reusable `[CurrencyAmount]` attribute for non-negative, two-decimal currency values. `CachedAttributeValidator` discovers annotated properties once per model type, compiles property accessors, and caches the metadata in a `ConcurrentDictionary`; `ModelValidationActionFilter` adds its failures to MVC model state. Add a dependency-free console benchmark that compares repository JSON deserialization with warm catalogue reads and separately reports the synthetic courier concurrency sample.

## Consequences

- Monetary model rules share a small implementation and the metadata discovery cost is paid once per type.
- The validator is used for server-side MVC model validation; it does not replace domain constructors or application-service checks.
- Benchmark output is reproducible evidence for this development machine, but the small local timing is not a production capacity claim.
- The benchmark creates data in a unique temporary directory and removes it at completion.

## Alternatives considered

- Duplicate `Range` and decimal-place checks on each view model: rejected because similar monetary constraints could drift apart.
- Reflect and compile property accessors on every request: rejected because repeated metadata work adds avoidable overhead.
- Add a benchmarking framework package: deferred because a small no-dependency console measurement is sufficient for this assignment's evidence.
