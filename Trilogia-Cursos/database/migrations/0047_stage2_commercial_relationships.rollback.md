0047 introduces explicit seller–customer (`SC`) and supervisor–seller (`SS`)
relationships. Installation creates no assignments and grants no profile or SQL
principal privileges. Management needs `RELACIONES_COMERCIALES_GESTIONAR` explicitly;
the Administrator name provides no exception. Eligibility is rechecked from active
users/profiles and canonical capabilities, not inferred from HR assignments.

Rehearse against a verified, separate restored copy before principal installation.
Preserve a current CHECKSUM backup, original ledger/module hashes and authorization
evidence. Provision only Options/List/SetAssignment EXECUTE to the application.
RequireAssignment is an internal transaction-only dependency for future order/quote
contracts and must not receive a direct application EXECUTE grant. This migration
does not release the current commercial containment guard or integrate orders.

Each effective change increments the immutable relationship key's revision and
appends an audit entry with its original login, actor and reason. Revocation keeps
the row. Regrant increments again, so an earlier quote/context cannot be revived by
matching users alone. Repeated identical state with the current revision is a no-op;
a stale revision is a conflict. Expiration never renews itself.

On failure before commit, roll back the transaction. After successful installation,
disable the management route or revoke only its new EXECUTE/capability grants; keep
relationships, audits, ledger and all old data. Do not drop tables, rewrite revisions,
remove audit records or restore principal automatically. Real assignments require
an authorized person selecting the actual participants; no test may infer them.
