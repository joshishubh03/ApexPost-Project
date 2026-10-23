# ApexPost Counter Platform

Assignment 2 implementation of a secure retail and postal counter platform using .NET 10, C# 14, and ASP.NET Core MVC.

## Current stage

The feature/solution-foundation branch establishes the layered solution and MVC host. Business functionality will be delivered in short-lived feature branches and merged into main through pull requests.

## Solution projects

- ApexPost.Domain — business entities, value objects, calculations, and invariants.
- ApexPost.Application — use cases, service contracts, and orchestration.
- ApexPost.Infrastructure — JSON repositories, audit storage, seed data, and simulated integrations.
- ApexPost.Web — MVC controllers, Razor views, authentication, middleware, and filters.
- ApexPost.Tests — unit and integration tests.
- docs — design documentation and architecture decision records.
- data — deterministic, non-sensitive JSON demo data.

## Prerequisites

- .NET 10 SDK

## Build and run

PowerShell commands:

    dotnet restore ApexPost.slnx
    dotnet build ApexPost.slnx
    dotnet run --project ApexPost.Web

JSON persistence and demo accounts will be documented as those features are implemented. Never commit real secrets or production personal data.

## Versioning

The application starts at version 0.1.0 during development. Git tags use a v prefix (for example, v0.1.0). Increment the patch number for compatible fixes, the minor number for backward-compatible features, and the major number for incompatible changes. The first stable release is v1.0.0.

Architecture decisions are recorded in DESIGN.md and docs/adr/.
