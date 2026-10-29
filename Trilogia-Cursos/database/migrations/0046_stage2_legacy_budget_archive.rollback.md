0046 adds an empty read-only monthly account archive and one guarded reader. It does
not install obsolete expense writers, seed accounts, assign capabilities, convert
annual budgets, or modify the modern `GastosOperativos` table.

Before installation, preserve and restore a CHECKSUM backup to a separate owned
destination; compare the modern table/module metadata before and after rehearsal.
Reconcile any pre-existing monthly archive explicitly: this migration stops instead
of selecting a schema or overwriting a reader.

On failure, roll back the installation transaction. After successful installation,
remove only the new reader's application EXECUTE grant or restore the prior MVC
release to disable this route. Keep the table, any subsequently authorized imports,
ledger and audits. Do not drop the table, remove ledger rows, seed balances, enable
the historical writers, or restore over principal automatically. Any future import
requires its source, approval and reconciliation; the annual budget stays authoritative.
