# ADR 0013: Branch hours and counter terminals

- Status: Accepted
- Date: 2026-10-25

## Context

Branch management must include operating hours and terminals. Transactions also need to identify the physical counter that processed a sale. The assignment provides no per-branch schedule or terminal inventory to import.

## Decision

Branches store local opening and closing times, defaulting to 09:00–18:00 for existing/demo data. The existing `IsOpen` operational status continues to gate transactions; the configured times are shown to staff and administrators and are not interpreted against server time because no branch timezone or day-of-week schedule was specified.

Counter terminals are separate domain records persisted in `counter-terminals.json` behind `ICounterTerminalRepository`. Administrators can add terminals and activate/deactivate them. Branch operational status can be opened or closed from the admin page. Development seeds two terminals per branch. A sale must reference an active terminal assigned to the selected branch; the terminal ID is stored on the receipt.

## Consequences

- Receipts identify the counter terminal for audit and operational review.
- Terminals can be retired without deleting historical receipt references.
- A later schedule policy can add timezones and weekly/holiday hours without inferring them from the demo's simple daily times.
