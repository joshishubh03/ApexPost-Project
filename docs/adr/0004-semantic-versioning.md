# ADR-0004: Record an explicit initial version and use Semantic Versioning

- Status: Accepted
- Date: 2026-10-23

## Context

The application will grow through staged feature work and needs traceable releases. Early interfaces may change as the assignment is implemented.

## Decision

Start at version 0.1.0, maintain version metadata centrally in Directory.Build.props, and tag releases in Git with a v prefix. Use 0.y.z during development and reserve 1.0.0 for the first stable, assignment-complete release.

## Consequences

A release tag identifies the source state. Any release process must keep the central build version aligned with the tag. Feature branches do not receive release versions.

## Alternatives considered

- Omit version metadata until the first stable release: rejected because assignment changes and demo builds would be harder to identify.
- Increment a version for every feature commit: rejected because commits are not releases and would create noisy release numbers.
- Use Semantic Versioning from `0.1.0`: selected because it distinguishes compatible changes from future breaking changes while the API is still evolving.
