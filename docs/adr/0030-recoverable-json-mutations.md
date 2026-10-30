# ADR-0030: Coordinate and recover JSON mutations

- Status: Accepted
- Date: 2026-10-30

## Context

The application stores related counter, inventory, money-order, audit, and notification records in separate JSON files. In-process locks do not protect against a second application process, and replacing multiple files directly can leave a partial business operation after a process crash.

## Decision

Keep repository interfaces and JSON storage, but coordinate mutations through `IApplicationMutationCoordinator`. Use an OS exclusive file lock for each mutation and a compatible shared lock for reads. Stage modified JSON files, write backups and a prepared journal, replace the files, then record the committed state. Recover prepared transactions during startup by restoring their backups; retain committed data and remove journal artifacts. Run business read-modify-write operations and repeatable seeding through the coordinator. Coordinate standalone audit and notification appends as well.

## Consequences

- Local application processes serialize conflicting mutations and do not overwrite one another's read snapshots.
- Related files are committed as a recoverable unit, and readers do not observe an in-progress replacement group.
- A crash can leave temporary data and a journal that startup recovery resolves.
- The guarantee assumes a local filesystem with correct OS file-share locking and same-volume rename behavior. This is not a distributed transaction, a database, or a guarantee for network shares.
- Corrupt journals and filesystem failures fail closed and require operator repair rather than silently discarding data.

## Alternatives considered

- Use only per-process `SemaphoreSlim`: rejected because independent web processes can still lose updates.
- Write each file independently and compensate on exceptions: rejected because compensation cannot run after abrupt process termination.
- Add SQLite or another database: rejected because the assignment prohibits a database and ORM in this week.
