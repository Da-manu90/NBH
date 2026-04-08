# NBH DB Migration Series - Reviewed

Stand: 2026-04-07

Diese Serie ist die bereinigte, ausfuehrungssichere Ableitung aus
[SQL_MIGRATION_KOMPLETT.sql](/C:/Data/NBH/SQL_MIGRATION_KOMPLETT.sql).

Ziel:
- sichere DB-Haertung ohne den aktuellen Client unnoetig zu brechen
- klare Trennung zwischen sofort ausfuehrbaren Schritten und clientabhaengigen Schritten
- saubere Reihenfolge mit Tests zwischen den Bloecken

## Vor jedem Block

1. Vollstaendiges Supabase/Postgres Backup erstellen
2. Aktuelle Policy-Uebersicht sichern
3. Nach jedem Block kurz testen

## Empfohlene Reihenfolge

1. [01_preflight_snapshot.sql](/C:/Data/NBH/db_migrations_reviewed/01_preflight_snapshot.sql)
2. [02_safe_cleanup.sql](/C:/Data/NBH/db_migrations_reviewed/02_safe_cleanup.sql)
3. [03_indexes_core.sql](/C:/Data/NBH/db_migrations_reviewed/03_indexes_core.sql)
4. [04_rls_phase_a_safe_now.sql](/C:/Data/NBH/db_migrations_reviewed/04_rls_phase_a_safe_now.sql)
5. [05_additive_functions_safe.sql](/C:/Data/NBH/db_migrations_reviewed/05_additive_functions_safe.sql)

Diese Dateien noch NICHT ausfuehren, bevor der Client angepasst ist:

6. [06_client_dependent_rls_later.sql](/C:/Data/NBH/db_migrations_reviewed/06_client_dependent_rls_later.sql)
7. [07_constraints_and_legacy_later.sql](/C:/Data/NBH/db_migrations_reviewed/07_constraints_and_legacy_later.sql)

## Warum diese Aufteilung?

- Der Client schreibt In-App-Notifications nur noch per RPC `create_system_notification` (siehe `08_system_notifications_safe.sql`). Nach Ausfuehrung von **08** (mit `set_config('row_security', 'off', true)` im Funktionskoerper) kann **10_notifications_phase_b_rls.sql** die direkten INSERT-Policies entfernen, ohne Chat-Benachrichtigungen zu brechen.
- `chat_reviews` und `event_rating` duerfen aktuell noch nicht auf "nur Beteiligte" verengt werden, weil `ProfilePageViewModel` und `UserProfilePageViewModel` noch Rohdaten fuer Aggregation lesen.
- `get_entries_paged` darf aktuell nicht auf neues JSON-Format umgestellt werden, weil `AddPageViewModel` noch direkt `List<EntryItem>` erwartet.
- Typ-Migration `text -> uuid` ist aus dem alten Plan ueberholt, weil die aktuelle DB diese Spalten bereits als `uuid` hat.

## Tests nach jedem Block

- Login
- AddPage laden
- DetailPage laden
- Chat oeffnen
- Profilseite laden
- Notification Popup oeffnen

