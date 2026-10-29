# 0059 rollback and recovery

0059 changes route dispatch semantics and records durable dispatch/notification
ownership. It intentionally has no destructive down script. Dispatch headers,
items, notification requests, claim attempts, and audit events must not be
deleted or rewritten; doing so would discard ownership, retry, or operational
evidence. Dispatch also changes historical route state through the already
installed `dbo.sp_Rutas_Dispatch`; those transitions are not reversible by this
migration.

## Before installation

Take and verify a database backup, restore it separately, record the exact 0058
ledger/hash, and preserve the pre-install definition hash of
`dbo.sp_Stage2_RouteOperation`. Apply 0059 only from its exact SHA256 bytes and
run `0059_stage2_route_dispatch_notifications.verify.sql`. Grant no identities
from this file; the independently reviewed provisioning step must grant the
MVC identity only `RouteDispatch.Dispatch` and `RouteDispatch.GetOwned`, and
grant the notification authority only `Request`, `Claim`, `Complete`, and
`Receipt` plus the minimum required ledger metadata.

## Operational disable / recovery

On a failed rollout, disable the application feature and stop callers first.
Do not run the historical `sp_Rutas_Dispatch` through the old wrapper and do
not manually update route/order status. Preserve the migration ledger and all
RouteDispatch rows. Diagnose using metadata and sanitized errors. Recover the
database from the verified pre-install backup only into a separate recovery
database, validate integrity and application compatibility there, then prepare
a separately reviewed forward repair. Replacing the live principal with a
restored copy or deleting 0059 objects is not an approved rollback.

Because SMTP acceptance cannot be atomically coordinated with SQL,
`Sending`/`Unknown` are terminal for automatic processing. Resolve uncertain
mail delivery operationally; never reset an attempt to `Pending` or replay it.
`SmtpAccepted` means the SMTP server accepted the message, not that a mailbox
received it.
