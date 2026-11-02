# 0034: Relational schema design and 3NF normalization

- Status: Superseded by 0041
- Date: 2026-11-01

## Context

The Assignment 3 database design must support branch stock integrity, transaction snapshots, identity verification, shipping, and operational auditing without repeating business data behind each transaction or branch record. The system is branch-centric and must be capable of reliable reporting and multi-step sales updates.

## Decision

We keep a normalized relational model in which each root entity is stored once, and branch-specific facts such as stock, terminal assignments, and transaction history are modeled as separate tables keyed by branch and business identifier. Product pricing and shipping metadata are snapshotted onto transactions to preserve report integrity even when master data changes.

## Consequences

- 3NF reduces update anomalies and keeps the schema understandable for reporting and EF Core mapping.
- Branch stock and transaction records can be updated atomically while preserving historical snapshots.
- The relational design remains aligned with the EF Core `DbContext` and repository abstractions used by the application.
- The schema must be validated with foreign keys, check constraints, and rowversion fields to preserve correct concurrency and data integrity.
