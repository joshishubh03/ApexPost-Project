# 0033: SQL Server persistence for the Assignment 3 data layer

- Status: Accepted
- Date: 2026-11-01

## Context

The Week 2 JSON-backed platform works for local demos, but it cannot scale to branch-wide concurrency, transaction integrity, or head-office reporting. ApexPost needs a durable relational system of record with explicit branch, stock, shipping, money-order, transaction, and audit data. The assignment requires SQL Server and EF Core 10 behind the existing repository interfaces without rewriting the business rules.

## Decision

We keep the same repository abstractions and application contracts, and add a SQL Server implementation behind them in Infrastructure. The application detects a configured connection string (`ConnectionStrings:ApexPostDb` or `APEXPOST_DB_CONNECTION`) and wires the EF Core `DbContext` and repositories only when a database is available. The JSON repositories remain as a safe fallback for local or no-database scenarios.

## Consequences

- The application keeps a stable contract and the Week 2 business logic remains the same.
- Reporting and transaction work can move to SQL Server and EF Core without a broad rewrite of the UI or services.
- Local development remains usable even without a SQL Server install, which helps the team keep working while still supporting the assignment database work.
- The database-backed path requires explicit migration and connection-string setup before the full relational features are active.
