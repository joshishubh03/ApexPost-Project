# ApexPost Counter Platform

Assignment 2 implementation of a secure retail and postal counter platform using .NET 10, C# 14, and ASP.NET Core MVC.

## Current stage

The staff-auth slice adds branch and staff domain records, JSON demo data, and cookie-based staff sign-in. The branch-staff-management slice adds Administrator-only branch and staff management. The product-catalogue slice adds product search and management, with 30 seeded demo products. Business capabilities continue in short-lived feature branches and merge into main through pull requests.

## Solution projects

- ApexPost.Domain — business entities, value objects, calculations, and invariants.
- ApexPost.Application — use cases, service contracts, and orchestration.
- ApexPost.Infrastructure — JSON repositories, audit storage, seed data, and simulated integrations.
- ApexPost.Web — MVC controllers, Razor views, authentication, middleware, and filters.
- ApexPost.Tests — unit and integration tests.
- docs — design documentation and architecture decision records.
- data — deterministic, non-sensitive JSON demo data created at runtime.

## Prerequisites

- .NET 10 SDK

## Restore, build, test, and run

PowerShell commands:

    dotnet restore ApexPost.slnx
    dotnet build ApexPost.slnx
    dotnet test ApexPost.slnx
    dotnet run --project ApexPost.Web

The demo data is seeded on first run in Development. All seeded accounts use the password CounterDemo!2026. Example accounts:

- admin@apexpost.local
- manager1@apexpost.local
- clerk11@apexpost.local
- auditor1@apexpost.local

The demo password is public training data and must never be reused outside this local demo. Password hashes, not plaintext passwords, are stored in JSON.

## Branch and staff administration

Sign in as the seeded Administrator to use the **Branches** and **Staff** links. Administrators can create branches, create staff accounts, and activate or deactivate accounts. New passwords are hashed before they are saved. Non-administrator staff must be assigned to an existing branch, and the last active Administrator cannot be deactivated.

The MVC administration controller calls `StaffManagementService` in the Application layer. That service uses repository interfaces; Infrastructure implements those interfaces with the JSON files in `data/`. This keeps web form handling, business rules, and persistence in their respective layers.

## Product catalogue

All signed-in staff can search active products by SKU, name, category, or description. Administrators and Branch Managers can create products and activate or deactivate them. Product SKUs are normalized to uppercase and must be unique; prices are non-negative and support up to two decimal places. Development seeding creates 30 sample catalogue products in `data/products.json` when the file is empty or missing.

Never commit real secrets or production personal data.

## Versioning

The application starts at version 0.1.0 during development. Git tags use a v prefix (for example, v0.1.0). Increment the patch number for compatible fixes, the minor number for backward-compatible features, and the major number for incompatible changes. The first stable release is v1.0.0.

Architecture decisions are recorded in DESIGN.md and docs/adr/.
