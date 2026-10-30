# ADR 0022: Staff profile updates

- Status: Accepted
- Date: 2026-10-25

## Context

The staff administration workflow supported account creation and activation changes, but the assignment also requires updating staff details.

## Decision

Add an Administrator-only edit page for staff name, email, role, and home branch. The application service validates email uniqueness and branch assignment, preserves the existing password hash and active state, and prevents changing the role of the last active Administrator to a non-administrator role.

## Consequences

- Administrators can correct staff profile and branch assignment data without replacing credentials.
- Profile edits keep the same staff ID so historical transactions, audit events, and verification records retain their actor reference.
- Email addresses remain unique, and non-administrator roles require an existing home branch.

## Alternatives considered

- Deactivate and recreate a staff account for profile corrections: rejected because historical references use the staff ID.
- Replace the entire staff record from form input: rejected because it could overwrite the stored password hash or active flag.
- Update only the editable fields while preserving ID and credentials: selected to keep historical references and sign-in behavior stable.
