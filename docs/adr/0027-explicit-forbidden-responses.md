# ADR 0027: Explicit forbidden responses

- Status: Accepted
- Date: 2026-10-25

## Context

Cookie authentication redirected both unauthenticated users and signed-in users without permission. The required MVC behavior preserves a login redirect for an unauthenticated staff page, but a Counter Clerk attempting a manager-only reversal must receive 403. Partner IP restrictions also need an integration-level check proving a blocked source is denied before key validation.

## Decision

Keep the cookie challenge redirect to `/Account/Login`, and configure the cookie access-denied event to set status 403 without redirecting. Add integration coverage for a clerk posting a reversal and for a partner request from an IP outside the integration allowlist. The partner test supplies a valid key so the result demonstrates that the IP restriction is applied first.

## Consequences

- Browser clients retain the login flow for unauthenticated requests.
- Authenticated users and blocked partner clients receive a clear 403 response.
- The tests cover both required response contracts without weakening role or IP checks.
