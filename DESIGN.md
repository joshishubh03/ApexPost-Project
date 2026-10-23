# Design overview

## Architecture

The solution uses a layered design with dependencies pointing toward the domain:

    ApexPost.Web -> ApexPost.Application -> ApexPost.Domain
    ApexPost.Web -> ApexPost.Infrastructure -> ApexPost.Application
    ApexPost.Infrastructure -> ApexPost.Domain
    ApexPost.Tests -> projects under test

MVC controllers will translate HTTP input into application use cases and responses. Business rules belong in Domain and Application code. Infrastructure will implement repository and integration interfaces.

## Persistence

Assignment 2 requires JSON file persistence behind interfaces. The JSON implementation will remain in Infrastructure so a later database adapter can replace it without changing business rules. File write coordination and its process-local limits will be documented before implementing transactions.

## Security and audit

Authentication, role and branch authorization, request middleware, action filters, and audit event shape will be decided and documented in feature branches before implementation. Sensitive identity values must be masked before persistence.

## Decisions

See docs/adr/ for accepted architecture decision records. This document will expand with domain diagrams, middleware ordering, filter-versus-middleware guidance, security and audit design, and updated SOLID mapping as those features land.
