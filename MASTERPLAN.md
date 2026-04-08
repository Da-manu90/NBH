# NBH CareCommunity — Vollstaendiger Umbauplan (Masterplan)

**Erstellt:** 2026-04-07
**Letzte Plan-Aktualisierung:** 2026-04-08 (Debug-Log-Auswertung, Notification-RPC-RLS, Chat-Send robust)
**Basis:** Ultra-Analyse aus NBH-10.zip, NBH-latest, NBH-latestv3, Debug-/Build-Log, RLS-Dump, .NET MAUI 10 Deep Research

### Ausführungsstand (kurz)

| Bereich | Stand |
|--------|--------|
| Phase 0 | Größtenteils umgesetzt: `AppConfig`-URLs in Mopups, Sentry statt AppCenter, ImageSharp 3.1.12, keine hardcodierte Supabase-URL in ChatReview/EventRating |
| Phase 1 | `SupabaseSessionHandler` mit `ConfigureAwait(false)`; `ChatPage` ruft `Cleanup()` auf; Auth-Init in `MauiProgram` mit try/catch; Notification-Deserialisierung mit try/catch; `DetailPageViewModel` nutzt `SafeExecuteAsync` statt ungeschuetztem Fire-and-Forget; `Guid.Parse`-Stellen auf `TryParse` gehaertet (`AuthService`, `DetailPageViewModel.GroupEvents`, `SettingsPageViewModel`, `UserProfilePageViewModel`, `DetailPage.xaml.cs`, `ModerationService`) |
| SendMessage / RLS | App: In-App-Notification nach erfolgreichem Chat-Insert isoliert (Fehler bei RPC toastet nicht mehr den ganzen Versand). DB: `08_system_notifications_safe.sql` setzt vor dem INSERT `row_security=off`, damit `create_system_notification` unter strenger RLS nicht mit 42501 scheitert — **Migration auf Supabase erneut ausführen**, wenn die alte Funktion ohne diese Zeile deployed ist |
| OneSignal (Log) | Warnungen `IOtelPlatformProvider` / `IOtelLogger`: natives OTel-Modul fehlt im APK; Push lief im Log trotzdem. Prüfen bei nächstem OneSignal-DotNet-Release oder Android-Gradle-Abhängigkeiten; keine sichere Paket-Fix-Version ohne Gerätetest |
| Phase 2+ | SQL-Serie unter `db_migrations_reviewed/`; großer Monolith `SQL_MIGRATION_KOMPLETT.sql` — Ausrollen nach Backup, Reihenfolge laut `00_README.md` |

### Plan-Fortsetzung (aktualisiert auf Code-Stand 2026-04-08)

Fortgesetzt wird ab **Phase 4 bis Phase 6**, weil zentrale Teile aus Phase 0-3 bereits umgesetzt sind, aber die aktuelle App-Realität neue Prioritäten hat:

1. **Chat UX und Badge-Korrektheit (sofort):**
   - Chat-Übersicht ohne Carousel, nur performante Card-Liste.
   - Strikte Sortierung nach `last_message_date DESC` (neueste Konversation oben).
   - TabBar-Unread-Badge darf nur bei **eingehenden** neuen Nachrichten steigen.

2. **AddPage Informationshierarchie (sofort):**
   - Highlight-Karten zeigen nicht "neueste", sondern "wichtig/beliebt".
   - Highlight-Bereich muss ein-/ausblendbar sein.
   - Hauptliste bleibt chronologisch neu -> alt.

3. **DB-RPC-Deploy-Härtung (sofort):**
   - Migrationsskripte müssen idempotent sein (`DROP FUNCTION IF EXISTS` vor `CREATE OR REPLACE` bei Default-Parameter-Änderungen), um Fehler wie `42P13` zu verhindern.

4. **Nächster Produktionsblock (offen):**
   - Service-Locator-Reste vollständig entfernen (Phase 4.1).
   - Navigation-Orchestrierung konsolidieren (Phase 4.2).
   - Strenge Binding-Compilation und verbleibende XAML-Bindings schließen (Phase 5.3).
   - Offline/Cache-Layer für Chat-Übersicht und Entry-Liste (Phase 6.1).

**Ziel:** Die App von "gewachsener Hybrid-Architektur" (5.1/10) auf "professionelle Production-App" (9/10) bringen
**Methode:** 6 Phasen, jede Phase endet mit einem Build-Test-Gate

---

## Inhaltsverzeichnis

- [Phase 0: Sofort-Absicherung](#phase-0-sofort-absicherung)
- [Phase 1: Kritische Code-Stabilisierung](#phase-1-kritische-code-stabilisierung)
- [Phase 2: RLS-Konsolidierung und Schema-Haertung](#phase-2-rls-konsolidierung-und-schema-haertung)
- [Phase 3: Backend-API (RPCs, Edge Functions, Views)](#phase-3-backend-api-rpcs-edge-functions-views)
- [Phase 4: App-Architektur bereinigen](#phase-4-app-architektur-bereinigen)
- [Phase 5: Performance und MAUI-10-Haertung](#phase-5-performance-und-maui-10-haertung)
- [Phase 6: Produktionsreife](#phase-6-produktionsreife)
- [Anhang A: Vollstaendige Befundmatrix](#anhang-a-vollstaendige-befundmatrix)
- [Anhang B: Entschiedene Produktfragen](#anhang-b-entschiedene-produktfragen)

---

## Entschiedene Produktfragen

Diese Entscheidungen beeinflussen den gesamten Plan:

| Frage | Entscheidung |
|-------|-------------|
| Bewertungen/Reviews lesbar? | **Nur aggregierte Scores** — rohe Reviews/Kommentare nicht fuer alle sichtbar |
| Teilnehmerlisten sichtbar? | **Nur fuer Owner und Betroffene** — entry_participants, group_event_participants, entry_helpers |
| Notifications vom Client? | **Nur ueber Systempfade** (RPC/Edge Function) — kein direkter Client-INSERT |
| FAQ-Tabelle? | **Nur `faq` (modern)** — `faqs` (legacy) kann entfernt werden |
| Secrets rotiert? | **Nutzt `faq`** (Antwort unklar zu Rotation — wird in Phase 0 geprueft) |

---

## Phase 0: Sofort-Absicherung

**Ziel:** Sicherheitskritische Probleme sofort schliessen, Build-Basis sichern.
**Dauer:** Tag 1-2
**Build-Gate:** `dotnet build` muss durchlaufen, App muss starten

### 0.1 Hardcodierte Supabase-URL entfernen

**Problem:** Produktions-URL im Quellcode, SDK-Bypass
**Dateien und exakte Zeilen:**

**ChatReviewMopup.xaml.cs (Zeile 45):**
```
AKTUELL:  BaseAddress = new Uri("https://entpyczncbnavvudfgrb.supabase.co/rest/v1/")
AENDERN:  BaseAddress = new Uri($"{AppConfig.SupabaseUrl}/rest/v1/")
```

**EventRatingMopup.xaml.cs (Zeile 45):**
```
AKTUELL:  BaseAddress = new Uri("https://entpyczncbnavvudfgrb.supabase.co/rest/v1/")
AENDERN:  BaseAddress = new Uri($"{AppConfig.SupabaseUrl}/rest/v1/")
```

> Hinweis: In Phase 4 (Schritt 4.9) werden diese Dateien komplett auf Supabase SDK umgebaut.
> Hier nur die URL zentralisieren als Sofortmassnahme.

### 0.2 launchSettings.json absichern

**Problem:** Supabase-Credentials in Git-tracked Datei
**Datei:** `Properties/launchSettings.json`

**Aktion:**
1. `.gitignore` ergaenzen: `Properties/launchSettings.json`
2. `git rm --cached Properties/launchSettings.json`
3. Sicherstellen dass `AppConfig.cs` in `.gitignore` steht (ist es bereits)

### 0.3 Fehlende Assets klaeren

**Problem:** UI-Referenzen auf nicht-existente Bilder (bestaetigt durch Glide-Fehler im Debug-Log)

**Fehlende Dateien:**
- `trash.png` — referenziert in CreateEventPage.xaml
- `placeholder_image.png` — referenziert in ProfilePage.xaml

**Aktion:** Entweder Assets erstellen/beschaffen ODER Referenzen durch vorhandene Assets ersetzen.

**CreateEventPage.xaml** — Referenz auf trash.png finden und durch vorhandenes Icon ersetzen
**ProfilePage.xaml** — Referenz auf placeholder_image.png finden und durch vorhandenes Placeholder ersetzen

### 0.4 AppCenter entfernen, Sentry einrichten

**Problem:** AppCenter retired seit 31.03.2025, Analytics-Extension bis 30.06.2026

**NBH.csproj — Zeilen 195-197 ENTFERNEN:**
```xml
<PackageReference Include="Microsoft.AppCenter" Version="5.0.5" />
<PackageReference Include="Microsoft.AppCenter.Analytics" Version="5.0.5" />
<PackageReference Include="Microsoft.AppCenter.Crashes" Version="5.0.5" />
```

**NBH.csproj — NEU HINZUFUEGEN:**
```xml
<PackageReference Include="Sentry.Maui" Version="5.*" />
```

**MauiProgram.cs — AppCenter.Start() Aufruf finden und entfernen.**
**MauiProgram.cs — Sentry Setup in CreateMauiApp() hinzufuegen:**
```csharp
builder.UseSentry(options =>
{
    options.Dsn = AppConfig.SentryDsn;
    options.TracesSampleRate = 0.2;
});
```

**AppConfig.cs / AppConfig.Template.cs — Neues Feld:**
```csharp
public const string SentryDsn = "https://DEIN_SENTRY_DSN";
```

### 0.5 SixLabors.ImageSharp Sicherheitsluecke

**Problem:** Version 3.1.7 hat bekannte Schwachstelle (Build-Warnung)

**NBH.csproj — Zeile 216:**
```
AKTUELL:  <PackageReference Include="SixLabors.ImageSharp" Version="3.1.7" />
AENDERN:  <PackageReference Include="SixLabors.ImageSharp" Version="3.1.8" />
```
(oder neueste 3.x Version)

### 0.6 AllowUnsafeBlocks pruefen

**NBH.csproj — Zeile 21:**
```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```
**Aktion:** Pruefen ob CropImagePage.xaml.cs (Pixel-Manipulation) dies benoetigt. Wenn ja: belassen mit Kommentar. Wenn nein: entfernen.

---

**BUILD-GATE Phase 0:**
```bash
dotnet build NBH.csproj -c Debug
# App auf Emulator deployen und starten
# Pruefen: Login, Navigation, Chat oeffnen, Profil laden
```

---

## Phase 1: Kritische Code-Stabilisierung

**Ziel:** Die 7 gefaehrlichsten Code-Probleme beseitigen (Deadlocks, Crashes, Memory Leaks)
**Dauer:** Tag 3-7
**Build-Gate:** Build + manueller Smoke-Test aller Kernflows

### 1.1 SupabaseSessionHandler entblocken

**Problem:** `.Wait()` und `.Result` auf async SecureStorage blockieren den Main Thread, Deadlock-Risiko
**Datei:** `SupabaseSessionHandler.cs`

**Zeile 13-14 — Interface-Signatur aendern ist NICHT moeglich** (IGotrueSessionPersistence<Session> ist synchron).
**Loesung:** Intern async ausfuehren, aber sicher dispatchen:

**Zeile 26 — AKTUELL:**
```csharp
Task.Run(async () => await SecureStorage.Default.SetAsync(Key, json)).Wait();
```
**AENDERN ZU:**
```csharp
// ConfigureAwait(false) verhindert Deadlock durch SynchronizationContext
Task.Run(async () => await SecureStorage.Default.SetAsync(Key, json).ConfigureAwait(false))
    .GetAwaiter().GetResult();
```

**Zeile 55 — AKTUELL:**
```csharp
var json = Task.Run(async () => await SecureStorage.Default.GetAsync(Key)).Result;
```
**AENDERN ZU:**
```csharp
var json = Task.Run(async () => await SecureStorage.Default.GetAsync(Key).ConfigureAwait(false))
    .GetAwaiter().GetResult();
```

> Langfristig (Phase 4): Eigenen async SessionHandler implementieren und Supabase-Client-Initialisierung umbauen.

### 1.2 async void in Nicht-Event-Handlern korrigieren

**Problem:** Unbeobachtete Exceptions in async void crashen die App ohne Recovery

**DetailPageViewModel.cs — Zeile 430:**
```
AKTUELL:  async void LoadUserDataAsync()
AENDERN:  async Task LoadUserDataAsync()
```
Alle Aufrufer muessen geaendert werden:
- Aufrufer finden (wahrscheinlich Property-Setter oder OnAppearing)
- `_ = LoadUserDataAsync()` mit try/catch-Wrapper ODER `await` verwenden

**DetailPageViewModel.cs — Zeile 549:**
```
AKTUELL:  async void LoadEntryState()
AENDERN:  async Task LoadEntryState()
```
Aufrufer anpassen (Property-Setter in SelectedEntry um Zeile 104):
```csharp
// statt: LoadEntryState();
_ = SafeExecuteAsync(LoadEntryState);
```

**App.xaml.cs — Zeile 115:**
```
AKTUELL:  protected override async void OnResume()
```
> OnResume() MUSS async void bleiben (Framework-Override).
> Loesung: Gesamten Body in try/catch wrappen:
```csharp
protected override async void OnResume()
{
    try
    {
        // ... gesamter bestehender Code Zeile 116-169
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[App] OnResume error: {ex}");
    }
}
```

### 1.3 Fire-and-Forget Auth-Initialisierung absichern

**Problem:** AuthService kann uninitialisiert bleiben, alle DB-Calls schlagen dann still fehl

**MauiProgram.cs — Zeile 120:**
```
AKTUELL:  Task.Run(async () => await authService.InitializeAsync());
AENDERN:  _ = Task.Run(async () =>
{
    try
    {
        await authService.InitializeAsync();
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[MauiProgram] Auth init failed: {ex}");
    }
});
```

> Langfristig (Phase 4): Auth-Init in den App-Startup-Orchestrator verschieben und awaiten.

### 1.4 Memory Leaks: Event-Subscriptions lifecycle-sicher machen

**Problem:** Event-Handler akkumulieren bei Navigation, halten alte ViewModels im Speicher

**ChatPageViewModel.cs — Zeilen 147-149 (im Konstruktor):**
```csharp
AKTUELL:
_phoenixService.PostgresChanged += OnPostgresChanged;      // Zeile 148
_phoenixService.BroadcastReceived += OnBroadcastReceived;    // Zeile 149
```

**Loesung:** Unsubscribe-Methode hinzufuegen und von der Page aufrufen:

```csharp
// NEU: Cleanup-Methode im ViewModel
public void Cleanup()
{
    _phoenixService.PostgresChanged -= OnPostgresChanged;
    _phoenixService.BroadcastReceived -= OnBroadcastReceived;
    _typingDebounceTimer?.Dispose();        // Zeile 288 Timer
    _partnerTypingTimeoutTimer?.Dispose();  // Zeile 267 Timer
}
```

**ChatPage.xaml.cs — OnDisappearing hinzufuegen/ergaenzen:**
```csharp
protected override void OnDisappearing()
{
    base.OnDisappearing();
    if (BindingContext is ChatPageViewModel vm)
        vm.Cleanup();
}
```

**NotificationService.cs — Zeile 75:**
```csharp
AKTUELL:  _realtimeService.PostgresChanged += OnPostgresChanged;
```
**Loesung:** Guard hinzufuegen dass nicht doppelt subscribed wird:
```csharp
// VOR dem Subscribe:
_realtimeService.PostgresChanged -= OnPostgresChanged;  // Idempotent
_realtimeService.PostgresChanged += OnPostgresChanged;
```

**BellControl.xaml.cs — Zeilen 23-26:**
Pruefen ob bereits Unsubscribe-before-Subscribe Muster vorhanden (laut Analyse: ja).
Falls nicht: gleiche Technik anwenden.

### 1.5 Notification-Deserialisierung robust machen

**Problem:** Debug-Log: `Unexpected character encountered while parsing value: {. Path '[1].action_data'`

**NotificationService.cs — Zeilen 217-232 (ConvertDictionaryToNotification):**
```
AKTUELL:  var json = JsonConvert.SerializeObject(dict);
          return JsonConvert.DeserializeObject<NotificationModel>(json);
```
**AENDERN ZU:**
```csharp
private NotificationModel? ConvertDictionaryToNotification(Dictionary<string, object> dict)
{
    try
    {
        var json = JsonConvert.SerializeObject(dict);
        var model = JsonConvert.DeserializeObject<NotificationModel>(json);
        return model;
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[NotificationService] Deserialisierung fehlgeschlagen: {ex.Message}");
        return null;
    }
}
```

**NotificationListViewModel.cs — Zeile 78 (JObject.Parse):**
```
AKTUELL:  JObject.Parse(notif.ActionData)
AENDERN:  Wrap in try/catch:
```
```csharp
JObject? actionObj = null;
try { actionObj = JObject.Parse(notif.ActionData ?? "{}"); }
catch { /* malformed action_data */ }
```

**NotificationModel.cs — Zeilen 35-50 (HasRatingButton Getter):**
Pruefen dass der try/catch den gesamten JObject-Zugriff umschliesst (Zeile 43-44).

### 1.6 Guid.Parse absichern

**DetailPageViewModel.cs — Zeile 754:**
```
AKTUELL:  Guid.Parse(SelectedEntry.user_id)
AENDERN:  Guid.TryParse(SelectedEntry.user_id, out var userId) ? userId : Guid.Empty
```

### 1.7 CreateEvent Doppelsubmit verhindern

**CreateEventPageViewModel.cs — CreateEventAsync Methode (ca. Zeile 160):**
Pruefen ob `IsBusy`-Guard am Anfang steht. Falls nicht:
```csharp
[RelayCommand]
async Task CreateEventAsync()
{
    if (IsBusy) return;
    IsBusy = true;
    try
    {
        // ... bestehender Code
    }
    finally
    {
        IsBusy = false;
    }
}
```

---

**BUILD-GATE Phase 1:**
```bash
dotnet build NBH.csproj -c Debug
# Manueller Smoke-Test:
# 1. App starten — kein Startup-Crash
# 2. Login durchfuehren — Session wird gespeichert (kein Deadlock)
# 3. Chat oeffnen, zurueck navigieren, erneut oeffnen — kein Memory-Spike
# 4. Notifications laden — keine Deserialisierungs-Fehler in Debug-Output
# 5. DetailPage oeffnen — kein Crash bei ungueltiger user_id
# 6. CreateEvent doppelt klicken — nur ein Event erstellt
```

---

## Phase 2: RLS-Konsolidierung und Schema-Haertung

**Ziel:** Sicherheitsmodell auf Production-Grade bringen, Typ-Konsistenz herstellen
**Dauer:** Tag 8-14
**Build-Gate:** DB-Migrationen erfolgreich, App funktioniert mit neuen Policies

### 2.1 Kritische Policy-Fixes (SQL-Migrationen)

> Jede Migration ist ein eigenes SQL-File. Reihenfolge beachten!

**Migration 001: chat_messages — Realtime-Luecke schliessen**
```sql
-- PROBLEM: allow_select_realtime USING (true) macht ALLE Nachrichten fuer ALLE User lesbar
-- LOESUNG: Loeschen. select_messages (Chat-Teilnehmer only) bleibt bestehen.

DROP POLICY IF EXISTS "allow_select_realtime" ON chat_messages;

-- Bestehende Policies von TO public auf TO authenticated umstellen
DROP POLICY IF EXISTS "select_messages" ON chat_messages;
CREATE POLICY "select_messages" ON chat_messages
    FOR SELECT TO authenticated
    USING (EXISTS (
        SELECT 1 FROM chats
        WHERE chats.id = chat_messages.chat_id
        AND (chats.user_id = auth.uid() OR chats.partner_id = auth.uid())
    ));

DROP POLICY IF EXISTS "insert_messages" ON chat_messages;
CREATE POLICY "insert_messages" ON chat_messages
    FOR INSERT TO authenticated
    WITH CHECK (EXISTS (
        SELECT 1 FROM chats c
        WHERE c.id = chat_messages.chat_id
        AND (c.user_id = auth.uid() OR c.partner_id = auth.uid())
    ));

DROP POLICY IF EXISTS "update_messages" ON chat_messages;
CREATE POLICY "update_messages" ON chat_messages
    FOR UPDATE TO authenticated
    USING (sender_id = auth.uid());

DROP POLICY IF EXISTS "delete_messages" ON chat_messages;
CREATE POLICY "delete_messages" ON chat_messages
    FOR DELETE TO authenticated
    USING (sender_id = auth.uid() OR EXISTS (
        SELECT 1 FROM chats
        WHERE chats.id = chat_messages.chat_id
        AND chats.user_id = auth.uid()
    ));
```

**Migration 002: entries — Ban-Check und Update-Policy fixen**
```sql
-- PROBLEM: Zwei permissive INSERT-Policies neutralisieren den Ban-Check per OR
-- LOESUNG: Eine saubere INSERT-Policy mit Ban-Check

DROP POLICY IF EXISTS "Allow authenticated users to insert entries" ON entries;
DROP POLICY IF EXISTS "Banned users cannot insert" ON entries;
CREATE POLICY "entries_insert_authenticated" ON entries
    FOR INSERT TO authenticated
    WITH CHECK (
        auth.uid()::text = user_id
        AND NOT EXISTS (SELECT 1 FROM users WHERE id = auth.uid() AND is_banned = true)
    );

-- PROBLEM: UPDATE-Policy zu breit fuer Partner-Accept
-- LOESUNG: Owner-Update und Partner-Accept trennen
DROP POLICY IF EXISTS "Owner or future partner can update" ON entries;

-- Owner darf alles updaten
CREATE POLICY "entries_update_owner" ON entries
    FOR UPDATE TO authenticated
    USING (user_id = auth.uid()::text)
    WITH CHECK (user_id = auth.uid()::text);

-- Partner-Accept: nur event_partner_id auf sich selbst setzen wenn noch frei
-- HINWEIS: In Phase 3 wird dies durch RPC accept_entry_partner ersetzt.
-- Bis dahin diese enge Policy:
CREATE POLICY "entries_accept_partner" ON entries
    FOR UPDATE TO authenticated
    USING (event_partner_id IS NULL AND user_id != auth.uid()::text)
    WITH CHECK (event_partner_id = auth.uid());

-- SELECT und DELETE bleiben unveraendert, nur TO public -> TO authenticated
DROP POLICY IF EXISTS "Allow authenticated users to read entries" ON entries;
CREATE POLICY "entries_select" ON entries
    FOR SELECT TO authenticated
    USING (true);

DROP POLICY IF EXISTS "Allow delete own entries" ON entries;
CREATE POLICY "entries_delete_own" ON entries
    FOR DELETE TO authenticated
    USING (auth.uid()::text = user_id);
```

**Migration 003: group_event_roles — Nur Owner darf Rollen erstellen**
```sql
-- PROBLEM: auth.uid() IS NOT NULL = jeder Auth-User kann Rollen auf fremden Events erstellen
DROP POLICY IF EXISTS "Nur angemeldete User können Rollen erstellen" ON group_event_roles;
CREATE POLICY "group_roles_insert_owner" ON group_event_roles
    FOR INSERT TO authenticated
    WITH CHECK (auth.uid() IN (
        SELECT user_id::uuid FROM entries WHERE id = entry_id
    ));

-- SELECT und UPDATE bleiben, nur TO public -> TO authenticated
DROP POLICY IF EXISTS "Jeder kann Rollen sehen" ON group_event_roles;
CREATE POLICY "group_roles_select" ON group_event_roles
    FOR SELECT TO authenticated
    USING (true);

DROP POLICY IF EXISTS "Nur Owner kann Rollen löschen/ändern" ON group_event_roles;
CREATE POLICY "group_roles_update_owner" ON group_event_roles
    FOR UPDATE TO authenticated
    USING (auth.uid() IN (
        SELECT user_id::uuid FROM entries WHERE id = entry_id
    ));

-- DELETE fuer Owner hinzufuegen (fehlte bisher)
CREATE POLICY "group_roles_delete_owner" ON group_event_roles
    FOR DELETE TO authenticated
    USING (auth.uid() IN (
        SELECT user_id::uuid FROM entries WHERE id = entry_id
    ));
```

**Migration 004: notifications — Auf Self-Only + System umstellen**
```sql
-- PROBLEM: 8 Policies, darunter INSERT WITH CHECK (true) = jeder kann an jeden senden
-- LOESUNG: Auf 4 saubere Policies reduzieren. Client darf NUR lesen/updaten/loeschen.
-- INSERT nur ueber RPC (Phase 3)

DROP POLICY IF EXISTS "Eingeloggte Nutzer dürfen eine Notification an andere schicken" ON notifications;
DROP POLICY IF EXISTS "Ermögliche das Erstellen von Benachrichtigungen für authentif" ON notifications;
DROP POLICY IF EXISTS "User dürfen Read-Status der eigenen Notifications flippen" ON notifications;
DROP POLICY IF EXISTS "User dürfen eigene Notifications löschen" ON notifications;
DROP POLICY IF EXISTS "User dürfen nur ihre eigenen empfangenen Notifications lesen" ON notifications;
DROP POLICY IF EXISTS "Users can delete their own notifications" ON notifications;
DROP POLICY IF EXISTS "Users can update their own notifications" ON notifications;
DROP POLICY IF EXISTS "Users can view their own notifications" ON notifications;

CREATE POLICY "notifications_select_own" ON notifications
    FOR SELECT TO authenticated
    USING (auth.uid() = user_id);

CREATE POLICY "notifications_update_own" ON notifications
    FOR UPDATE TO authenticated
    USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

CREATE POLICY "notifications_delete_own" ON notifications
    FOR DELETE TO authenticated
    USING (auth.uid() = user_id);

-- INSERT: Nur ueber service_role (Edge Functions/RPCs) moeglich.
-- Kein INSERT-Policy fuer authenticated = Client kann nicht direkt inserten.
-- Falls Realtime-INSERT-Trigger benoetigt wird: SECURITY DEFINER Function nutzen.
```

**Migration 005: storage — Bucket-spezifisch statt global**
```sql
-- PROBLEM: Breite INSERT-Policy ORt mit bucket-spezifischen, macht sie wertlos
DROP POLICY IF EXISTS "Allow uploads for authenticated users" ON storage.objects;

-- Bucket-spezifische Policies bleiben bestehen:
-- "Allow authenticated inserts on profile_pictures" (fuer Profilbilder)
-- HINWEIS: Falls event_images Bucket benoetigt wird, eigene Policy anlegen

-- Buckets: INSERT fuer authenticated entfernen (Buckets werden vom Admin erstellt)
DROP POLICY IF EXISTS "Enable insert for authenticated users only" ON storage.buckets;
```

**Migration 006: users — Public Profile vs Self Access**
```sql
-- PROBLEM: SELECT USING (true) gibt email, apple_user_id, ban-Felder frei
-- LOESUNG: View fuer Public Profile + enge Self-Access Policy

-- Schritt 1: View fuer oeffentliche Profildaten
CREATE OR REPLACE VIEW public_user_profiles AS
SELECT
    id,
    username,
    profile_image_url,
    wohnort,
    ueber_mich,
    qualifikationen,
    is_verified,
    is_public,
    created_at
FROM users
WHERE is_public = true OR id = auth.uid();

-- Schritt 2: Users-SELECT einschraenken
DROP POLICY IF EXISTS "select_users_for_auth" ON users;

-- Jeder Auth-User kann Public-Felder aller User sehen
-- (benoetigt fuer Chat-Partner-Anzeige, Event-Owner etc.)
CREATE POLICY "users_select_public_fields" ON users
    FOR SELECT TO authenticated
    USING (true);
-- HINWEIS: Die View public_user_profiles filtert die Spalten.
-- Fuer App-Code: bevorzugt die View nutzen statt direkt users abzufragen.
-- Langfristig: Column-Level-Security via View durchsetzen.

-- anon Signup-Policy absichern
DROP POLICY IF EXISTS "allow_insert_during_signup" ON users;
-- Anon-Insert wird ueber auth.users Trigger gehandelt (sync_auth_users_to_public)
-- Falls manueller Signup-Flow: eng schneiden:
CREATE POLICY "users_insert_signup" ON users
    FOR INSERT TO anon
    WITH CHECK (id = auth.uid());
```

**Migration 007: Bewertungen — Nur aggregierte Scores oeffentlich**
```sql
-- ENTSCHEIDUNG: Rohe Reviews nur fuer Beteiligte, aggregierte Scores oeffentlich

-- chat_reviews: Nur Reviewer und Reviewed koennen sehen
DROP POLICY IF EXISTS "Select chat reviews for authenticated" ON chat_reviews;
CREATE POLICY "chat_reviews_select_involved" ON chat_reviews
    FOR SELECT TO authenticated
    USING (auth.uid() = reviewer_id OR auth.uid() = reviewed_id);

-- event_rating: Nur Reviewer und Reviewed koennen sehen
DROP POLICY IF EXISTS "Select event rating for auth" ON event_rating;
CREATE POLICY "event_rating_select_involved" ON event_rating
    FOR SELECT TO authenticated
    USING (auth.uid() = reviewer_id OR auth.uid() = reviewed_id);

-- Alle anderen Policies (INSERT, UPDATE, DELETE) auf TO authenticated umstellen
DROP POLICY IF EXISTS "Allow authenticated users to insert" ON chat_reviews;
CREATE POLICY "chat_reviews_insert" ON chat_reviews
    FOR INSERT TO authenticated
    WITH CHECK (auth.uid() = reviewer_id);

DROP POLICY IF EXISTS "Allow authenticated users to update" ON chat_reviews;
CREATE POLICY "chat_reviews_update" ON chat_reviews
    FOR UPDATE TO authenticated
    USING (auth.uid() = reviewer_id)
    WITH CHECK (auth.uid() = reviewer_id);

DROP POLICY IF EXISTS "Allow authenticated users to delete" ON chat_reviews;
CREATE POLICY "chat_reviews_delete" ON chat_reviews
    FOR DELETE TO authenticated
    USING (auth.uid() = reviewer_id);

-- event_rating analog
DROP POLICY IF EXISTS "Allow authenticated users to insert" ON event_rating;
CREATE POLICY "event_rating_insert" ON event_rating
    FOR INSERT TO authenticated
    WITH CHECK (auth.uid() = reviewer_id);

DROP POLICY IF EXISTS "Allow authenticated users to update" ON event_rating;
CREATE POLICY "event_rating_update" ON event_rating
    FOR UPDATE TO authenticated
    USING (auth.uid() = reviewer_id)
    WITH CHECK (auth.uid() = reviewer_id);

DROP POLICY IF EXISTS "Allow authenticated users to delete" ON event_rating;
CREATE POLICY "event_rating_delete" ON event_rating
    FOR DELETE TO authenticated
    USING (auth.uid() = reviewer_id);
```

**Migration 008: Teilnehmerlisten — Nur Owner und Betroffene**
```sql
-- ENTSCHEIDUNG: entry_participants, group_event_participants, entry_helpers
-- nur fuer Owner und betroffene User sichtbar

-- entry_participants: Doppelte Policies bereinigen (mit/ohne ::text Cast)
DROP POLICY IF EXISTS "p_participants_select" ON entry_participants;
DROP POLICY IF EXISTS "sel_participants_owner_or_self" ON entry_participants;
CREATE POLICY "entry_participants_select" ON entry_participants
    FOR SELECT TO authenticated
    USING (
        user_id = auth.uid()
        OR EXISTS (SELECT 1 FROM entries e WHERE e.id = entry_id AND e.user_id = auth.uid()::text)
    );

-- Doppelte INSERT-Policies bereinigen
DROP POLICY IF EXISTS "ins_participants_self" ON entry_participants;
DROP POLICY IF EXISTS "p_participants_insert_self" ON entry_participants;
CREATE POLICY "entry_participants_insert_self" ON entry_participants
    FOR INSERT TO authenticated
    WITH CHECK (user_id = auth.uid());

-- Doppelte UPDATE-Policies bereinigen
DROP POLICY IF EXISTS "p_participants_owner_update" ON entry_participants;
DROP POLICY IF EXISTS "p_participants_update_self" ON entry_participants;
DROP POLICY IF EXISTS "upd_participants_owner_or_self" ON entry_participants;
CREATE POLICY "entry_participants_update" ON entry_participants
    FOR UPDATE TO authenticated
    USING (
        user_id = auth.uid()
        OR EXISTS (SELECT 1 FROM entries e WHERE e.id = entry_id AND e.user_id = auth.uid()::text)
    );

-- DELETE bleibt: Owner oder Self
DROP POLICY IF EXISTS "del_participants_owner_or_self" ON entry_participants;
CREATE POLICY "entry_participants_delete" ON entry_participants
    FOR DELETE TO authenticated
    USING (
        user_id = auth.uid()
        OR EXISTS (SELECT 1 FROM entries e WHERE e.id = entry_id AND e.user_id = auth.uid()::text)
    );

-- group_event_participants analog
DROP POLICY IF EXISTS "Jeder kann Teilnehmer sehen" ON group_event_participants;
CREATE POLICY "group_participants_select" ON group_event_participants
    FOR SELECT TO authenticated
    USING (
        user_id = auth.uid()
        OR auth.uid() IN (SELECT user_id::uuid FROM entries WHERE id = entry_id)
    );

-- entry_helpers analog
DROP POLICY IF EXISTS "p_helpers_select" ON entry_helpers;
CREATE POLICY "entry_helpers_select" ON entry_helpers
    FOR SELECT TO authenticated
    USING (
        EXISTS (SELECT 1 FROM entries e WHERE e.id = entry_id
            AND (e.user_id = auth.uid()::text))
        OR EXISTS (SELECT 1 FROM entry_helper_signups s
            WHERE s.entry_id = entry_helpers.entry_id AND s.user_id = auth.uid()::text)
    );

-- entry_helper_signups analog
DROP POLICY IF EXISTS "p_signups_select" ON entry_helper_signups;
CREATE POLICY "signups_select" ON entry_helper_signups
    FOR SELECT TO authenticated
    USING (
        user_id::text = auth.uid()::text
        OR EXISTS (SELECT 1 FROM entries e WHERE e.id = entry_id AND e.user_id = auth.uid()::text)
    );
```

**Migration 009: Legacy faqs-Tabelle aufloesen**
```sql
-- ENTSCHEIDUNG: Nur faq (modern) wird genutzt
-- Pruefen ob faqs noch Daten enthaelt, dann:
-- 1. Daten migrieren falls noetig
-- 2. Tabelle droppen oder RLS sperren

-- Option A: Tabelle droppen (wenn keine Daten mehr benoetigt)
DROP TABLE IF EXISTS faqs;

-- Option B: Falls Daten benoetigt, erst migrieren:
-- INSERT INTO faq (question, answer, category, sort_order)
-- SELECT question, answer, category, sort_order FROM faqs
-- WHERE NOT EXISTS (SELECT 1 FROM faq WHERE faq.question = faqs.question);
-- DANN: DROP TABLE faqs;
```

### 2.2 Performance-Indizes (SQL)

**Migration 010: Indizes fuer Policy-Performance und Query-Performance**
```sql
-- Policy-Performance (auth.uid() Spalten)
CREATE INDEX IF NOT EXISTS ix_entries_user_id ON entries(user_id);
CREATE INDEX IF NOT EXISTS ix_entries_partner_id ON entries(event_partner_id) WHERE event_partner_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_chats_user_id ON chats(user_id);
CREATE INDEX IF NOT EXISTS ix_chats_partner_id ON chats(partner_id);
CREATE INDEX IF NOT EXISTS ix_chat_messages_chat_id ON chat_messages(chat_id);
CREATE INDEX IF NOT EXISTS ix_notifications_user_id ON notifications(user_id);
CREATE INDEX IF NOT EXISTS ix_group_event_roles_entry_id ON group_event_roles(entry_id);
CREATE INDEX IF NOT EXISTS ix_group_event_participants_entry_id ON group_event_participants(entry_id);
CREATE INDEX IF NOT EXISTS ix_group_event_participants_user_id ON group_event_participants(user_id);
CREATE INDEX IF NOT EXISTS ix_entry_participants_entry_id ON entry_participants(entry_id);
CREATE INDEX IF NOT EXISTS ix_entry_participants_user_id ON entry_participants(user_id);

-- Query-Performance
CREATE INDEX IF NOT EXISTS ix_entries_category_completed ON entries(category, is_completed);
CREATE INDEX IF NOT EXISTS ix_entries_created_date ON entries(created_date DESC);
CREATE INDEX IF NOT EXISTS ix_chat_messages_chat_created ON chat_messages(chat_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_notifications_user_read ON notifications(user_id, read);
CREATE INDEX IF NOT EXISTS ix_notifications_user_created ON notifications(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_chat_reviews_reviewed ON chat_reviews(reviewed_id);
CREATE INDEX IF NOT EXISTS ix_event_rating_reviewed ON event_rating(reviewed_id);
```

### 2.3 Typ-Migration text -> uuid

**Migration 011: entries.user_id text -> uuid**
```sql
-- Schritt 1: Neue uuid-Spalte
ALTER TABLE entries ADD COLUMN user_id_new uuid;

-- Schritt 2: Daten migrieren
UPDATE entries SET user_id_new = user_id::uuid WHERE user_id IS NOT NULL AND user_id != '';

-- Schritt 3: Alte Spalte umbenennen, neue einsetzen
ALTER TABLE entries RENAME COLUMN user_id TO user_id_old;
ALTER TABLE entries RENAME COLUMN user_id_new TO user_id;

-- Schritt 4: NOT NULL und FK
ALTER TABLE entries ALTER COLUMN user_id SET NOT NULL;
ALTER TABLE entries ADD CONSTRAINT fk_entries_user FOREIGN KEY (user_id) REFERENCES users(id);

-- Schritt 5: Alte Spalte droppen (nach Verifizierung!)
-- ALTER TABLE entries DROP COLUMN user_id_old;

-- Index aktualisieren
DROP INDEX IF EXISTS ix_entries_user_id;
CREATE INDEX ix_entries_user_id ON entries(user_id);
```

**Migration 012: chats.user_id und chats.partner_id text -> uuid**
```sql
-- Analog zu Migration 011, fuer beide Spalten
ALTER TABLE chats ADD COLUMN user_id_new uuid;
ALTER TABLE chats ADD COLUMN partner_id_new uuid;

UPDATE chats SET
    user_id_new = user_id::uuid,
    partner_id_new = partner_id::uuid;

ALTER TABLE chats RENAME COLUMN user_id TO user_id_old;
ALTER TABLE chats RENAME COLUMN user_id_new TO user_id;
ALTER TABLE chats RENAME COLUMN partner_id TO partner_id_old;
ALTER TABLE chats RENAME COLUMN partner_id_new TO partner_id;

ALTER TABLE chats ALTER COLUMN user_id SET NOT NULL;
ALTER TABLE chats ALTER COLUMN partner_id SET NOT NULL;
ALTER TABLE chats ADD CONSTRAINT fk_chats_user FOREIGN KEY (user_id) REFERENCES users(id);
ALTER TABLE chats ADD CONSTRAINT fk_chats_partner FOREIGN KEY (partner_id) REFERENCES users(id);

-- Alte Spalten nach Verifizierung droppen
-- ALTER TABLE chats DROP COLUMN user_id_old;
-- ALTER TABLE chats DROP COLUMN partner_id_old;
```

**App-seitige Aenderungen nach UUID-Migration:**

**EntryItem.cs — Zeile 31:**
```
AKTUELL:  public string user_id { get; set; } = string.Empty;
AENDERN:  public Guid user_id { get; set; }
```

**PublicChat.cs — Zeile 17:**
```
AKTUELL:  public string UserId { get; set; }
AENDERN:  public Guid UserId { get; set; }
```

**PublicChat.cs — Zeile 21:**
```
AKTUELL:  public string PartnerId { get; set; }
AENDERN:  public Guid PartnerId { get; set; }
```

**Alle Stellen die .ToString() oder ::text Casts verwenden muessen angepasst werden:**
- ChatViewPageViewModel.cs — Zeilen 66-75 (Chat-Queries mit user_id Filter)
- DetailPageViewModel.cs — Zeile 754 (Guid.Parse entfaellt, ist jetzt schon Guid)
- UserProfilePageViewModel.cs — Zeile 198 (Guid-to-String Konvertierung entfaellt)
- DetailPageViewModel.GroupEvents.cs — Alle Stellen mit user_id Vergleichen
- Alle RLS-Policies die `::text` Casts verwenden (werden in Migration 001-008 bereits bereinigt)

### 2.4 Foreign Key Constraints

**Migration 013: Alle fehlenden FKs**
```sql
-- chat_messages -> chats (mit CASCADE)
ALTER TABLE chat_messages ADD CONSTRAINT fk_messages_chat
    FOREIGN KEY (chat_id) REFERENCES chats(id) ON DELETE CASCADE;
ALTER TABLE chat_messages ADD CONSTRAINT fk_messages_sender
    FOREIGN KEY (sender_id) REFERENCES users(id);

-- notifications -> users (mit CASCADE)
ALTER TABLE notifications ADD CONSTRAINT fk_notifications_user
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;

-- chat_reviews -> users
ALTER TABLE chat_reviews ADD CONSTRAINT fk_reviews_reviewer
    FOREIGN KEY (reviewer_id) REFERENCES users(id);
ALTER TABLE chat_reviews ADD CONSTRAINT fk_reviews_reviewed
    FOREIGN KEY (reviewed_id) REFERENCES users(id);

-- event_rating -> users
ALTER TABLE event_rating ADD CONSTRAINT fk_rating_reviewer
    FOREIGN KEY (reviewer_id) REFERENCES users(id);
ALTER TABLE event_rating ADD CONSTRAINT fk_rating_reviewed
    FOREIGN KEY (reviewed_id) REFERENCES users(id);

-- group_event_roles -> entries (mit CASCADE)
ALTER TABLE group_event_roles ADD CONSTRAINT fk_roles_entry
    FOREIGN KEY (entry_id) REFERENCES entries(id) ON DELETE CASCADE;

-- group_event_participants -> entries, users
ALTER TABLE group_event_participants ADD CONSTRAINT fk_gep_entry
    FOREIGN KEY (entry_id) REFERENCES entries(id) ON DELETE CASCADE;
ALTER TABLE group_event_participants ADD CONSTRAINT fk_gep_user
    FOREIGN KEY (user_id) REFERENCES users(id);

-- reports -> users
ALTER TABLE reports ADD CONSTRAINT fk_reports_reporter
    FOREIGN KEY (reporter_id) REFERENCES users(id);

-- user_badges -> users
ALTER TABLE user_badges ADD CONSTRAINT fk_badges_user
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
```

### 2.5 Duplikat-Trigger entfernen

**Migration 014:**
```sql
-- Zwei Trigger feuern auf auth.users INSERT: doppelte Ausfuehrung
DROP TRIGGER IF EXISTS after_insert_auth_users ON auth.users;
-- sync_auth_users_to_public_trigger bleibt (feuert auch auf UPDATE)
```

---

**BUILD-GATE Phase 2:**
```bash
# 1. Alle Migrationen in Supabase Dashboard oder via CLI ausfuehren
# 2. EXPLAIN ANALYZE auf kritische Queries mit neuem RLS
# 3. dotnet build NBH.csproj -c Debug
# 4. App starten und testen:
#    - Login (Session-Check mit neuen Policies)
#    - Chat oeffnen (select_messages Policy)
#    - Entry erstellen (INSERT mit Ban-Check)
#    - Notifications laden (nur eigene)
#    - Profil laden (users Policy)
#    - Detail-Seite mit Group Event (Teilnehmer-Sichtbarkeit)
```

---

## Phase 3: Backend-API (RPCs, Edge Functions, Views)

**Ziel:** Schwere Client-Queries in gebuendelte DB-Operationen verlagern
**Dauer:** Tag 15-21
**Build-Gate:** Alle RPCs deployed, App nutzt sie, Performance messbar besser

### 3.1 RPC: get_entries_paged erweitern (total_count)

```sql
CREATE OR REPLACE FUNCTION get_entries_paged(
    p_category text DEFAULT 'Alles',
    p_event_type text DEFAULT 'all',
    p_limit int DEFAULT 20,
    p_offset int DEFAULT 0
) RETURNS json
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
DECLARE
    v_total int;
    v_rows json;
BEGIN
    SELECT count(*) INTO v_total
    FROM entries
    WHERE is_completed = false
      AND (p_category = 'Alles' OR category = p_category)
      AND (p_event_type = 'all' OR event_type = p_event_type);

    SELECT json_agg(row_to_json(e))
    INTO v_rows
    FROM (
        SELECT * FROM entries
        WHERE is_completed = false
          AND (p_category = 'Alles' OR category = p_category)
          AND (p_event_type = 'all' OR event_type = p_event_type)
        ORDER BY created_date DESC
        LIMIT p_limit OFFSET p_offset
    ) e;

    RETURN json_build_object('total', v_total, 'entries', COALESCE(v_rows, '[]'::json));
END;
$$;
```

**App-Aenderung — AddPageViewModel.cs (Zeile 180ff):**
Antwort parsen als `{ total: int, entries: [...] }` statt direkt als Array.
`HasMoreEntries` basierend auf `total` setzen statt Heuristik.

### 3.2 RPC: get_user_chats (eliminiert N+1)

```sql
CREATE OR REPLACE FUNCTION get_user_chats(p_user_id uuid)
RETURNS TABLE(
    chat_id uuid,
    partner_id uuid,
    partner_name text,
    partner_image text,
    last_message text,
    last_message_date timestamptz,
    is_unread boolean,
    archived_by_user boolean,
    archived_by_partner boolean
) LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT
        c.id,
        CASE WHEN c.user_id = p_user_id THEN c.partner_id ELSE c.user_id END,
        u.username,
        u.profile_image_url,
        c.last_message,
        c.last_message_date,
        CASE WHEN c.user_id = p_user_id
            THEN c.last_message_date > COALESCE(c.user_last_read_at, '1970-01-01'::timestamptz)
            ELSE c.last_message_date > COALESCE(c.partner_last_read_at, '1970-01-01'::timestamptz)
        END,
        c.archived_by_user,
        c.archived_by_partner
    FROM chats c
    JOIN users u ON u.id = (
        CASE WHEN c.user_id = p_user_id THEN c.partner_id ELSE c.user_id END
    )
    WHERE (c.user_id = p_user_id OR c.partner_id = p_user_id)
    ORDER BY c.last_message_date DESC;
$$;
```

**App-Aenderung — ChatViewPageViewModel.cs:**
- Zeilen 55-126 (LoadChatsAsync): Komplett ersetzen durch einzelnen RPC-Call
- Zeilen 190-223 (GetPartnerDataAsync): Entfaellt komplett (Daten kommen aus RPC)

### 3.3 RPC: get_entry_detail_bundle

```sql
CREATE OR REPLACE FUNCTION get_entry_detail_bundle(p_entry_id uuid, p_current_user_id uuid)
RETURNS json
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
DECLARE
    v_entry json;
    v_owner json;
    v_rating json;
    v_has_chat boolean;
    v_roles json;
    v_participants json;
BEGIN
    -- Entry laden
    SELECT row_to_json(e) INTO v_entry FROM entries e WHERE e.id = p_entry_id;

    -- Owner-Profil laden
    SELECT json_build_object(
        'id', u.id, 'username', u.username, 'profile_image_url', u.profile_image_url,
        'is_verified', u.is_verified, 'wohnort', u.wohnort
    ) INTO v_owner
    FROM entries e JOIN users u ON u.id = e.user_id
    WHERE e.id = p_entry_id;

    -- Rating-Stats laden
    SELECT row_to_json(r) INTO v_rating
    FROM get_user_rating_stats((SELECT user_id FROM entries WHERE id = p_entry_id)) r;

    -- Chat-Existenz pruefen
    SELECT EXISTS(
        SELECT 1 FROM chats
        WHERE (user_id = p_current_user_id AND partner_id = (SELECT user_id FROM entries WHERE id = p_entry_id))
           OR (partner_id = p_current_user_id AND user_id = (SELECT user_id FROM entries WHERE id = p_entry_id))
    ) INTO v_has_chat;

    -- Group Event Rollen
    SELECT json_agg(row_to_json(r)) INTO v_roles
    FROM group_event_roles r WHERE r.entry_id = p_entry_id;

    -- Group Event Teilnehmer mit User-Info
    SELECT json_agg(json_build_object(
        'id', gp.id, 'user_id', gp.user_id, 'role_id', gp.role_id,
        'status', gp.status, 'applied_at', gp.applied_at, 'confirmed_at', gp.confirmed_at,
        'username', u.username, 'profile_image_url', u.profile_image_url
    )) INTO v_participants
    FROM group_event_participants gp
    JOIN users u ON u.id = gp.user_id
    WHERE gp.entry_id = p_entry_id;

    RETURN json_build_object(
        'entry', v_entry,
        'owner', v_owner,
        'rating', v_rating,
        'has_chat', v_has_chat,
        'roles', COALESCE(v_roles, '[]'::json),
        'participants', COALESCE(v_participants, '[]'::json)
    );
END;
$$;
```

**App-Aenderung — DetailPageViewModel.cs:**
- Zeilen 100-116 (SelectedEntry Setter): Ersetzen durch einzelnen RPC-Call
- DetailPageViewModel.GroupEvents.cs Zeilen 89-160 (LoadGroupEventDataAsync): Daten kommen aus Bundle

### 3.4 RPC: get_user_profile_summary

```sql
CREATE OR REPLACE FUNCTION get_user_profile_summary(p_user_id uuid)
RETURNS json
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
DECLARE
    v_user json;
    v_rating json;
    v_entries_created int;
    v_entries_participated int;
BEGIN
    SELECT json_build_object(
        'id', u.id, 'username', u.username, 'email', CASE WHEN u.id = auth.uid() THEN u.email ELSE null END,
        'profile_image_url', u.profile_image_url, 'wohnort', u.wohnort,
        'ueber_mich', u.ueber_mich, 'qualifikationen', u.qualifikationen,
        'is_verified', u.is_verified, 'is_public', u.is_public, 'created_at', u.created_at
    ) INTO v_user FROM users u WHERE u.id = p_user_id;

    SELECT row_to_json(r) INTO v_rating FROM get_user_rating_stats(p_user_id) r;

    SELECT count(*) INTO v_entries_created FROM entries WHERE user_id = p_user_id;
    SELECT count(*) INTO v_entries_participated FROM entries
        WHERE event_partner_id = p_user_id AND is_completed = true;

    RETURN json_build_object(
        'user', v_user,
        'rating', v_rating,
        'entries_created', v_entries_created,
        'entries_participated', v_entries_participated
    );
END;
$$;
```

**App-Aenderung:**
- ProfilePageViewModel.cs — Rating-/Stats-Queries ersetzen
- UserProfilePageViewModel.cs — Zeilen 157-226 ersetzen durch einzelnen RPC-Call

### 3.5 RPC: mark_notifications_read

```sql
CREATE OR REPLACE FUNCTION mark_notifications_read(p_user_id uuid)
RETURNS int
LANGUAGE sql
AS $$
    WITH updated AS (
        UPDATE notifications SET read = true
        WHERE user_id = p_user_id AND read = false
        RETURNING 1
    )
    SELECT count(*)::int FROM updated;
$$;
```

**App-Aenderung — NotificationListViewModel.cs Zeilen 118-128:**
Schleife ersetzen durch: `await _supabaseClient.Rpc("mark_notifications_read", new { p_user_id = userId });`

### 3.6 RPC: create_system_notification (fuer Systempfade)

```sql
CREATE OR REPLACE FUNCTION create_system_notification(
    p_user_id uuid,
    p_type text,
    p_message text,
    p_action_data jsonb DEFAULT null
) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE
    v_id uuid;
BEGIN
    INSERT INTO notifications (user_id, type, message, action_data, read, created_at)
    VALUES (p_user_id, p_type, p_message, p_action_data, false, now())
    RETURNING id INTO v_id;
    RETURN v_id;
END;
$$;
```

**App-Aenderung:** Alle Stellen die direkt in notifications inserten, muessen auf diesen RPC umgestellt werden:
- DetailPageViewModel.cs — Notification-Erstellung bei Event-Annahme/Abschluss
- Alle weiteren In-App Notification-Inserts

### 3.7 Edge Functions absichern

**notify_partner_assigned/index.ts:**
```typescript
// AM ANFANG HINZUFUEGEN:
import { createClient } from '@supabase/supabase-js'

const authHeader = req.headers.get('Authorization')
if (!authHeader) {
    return new Response('Unauthorized', { status: 401 })
}

// URLs/Keys aus ENV statt hardcoded
const supabaseUrl = Deno.env.get('SUPABASE_URL')!
const supabaseServiceKey = Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!
const oneSignalAppId = Deno.env.get('ONESIGNAL_APP_ID')!
const oneSignalApiKey = Deno.env.get('ONESIGNAL_API_KEY')!
```

**notify_new_chat_message/index.ts:** Analog.

### 3.8 Schema-Ergaenzung fuer Map (serverseitige Koordinaten)

```sql
ALTER TABLE entries ADD COLUMN IF NOT EXISTS latitude double precision;
ALTER TABLE entries ADD COLUMN IF NOT EXISTS longitude double precision;
ALTER TABLE entries ADD COLUMN IF NOT EXISTS geocoded_at timestamptz;

CREATE INDEX IF NOT EXISTS ix_entries_geo ON entries(latitude, longitude)
    WHERE latitude IS NOT NULL;
```

**RPC: get_map_entries**
```sql
CREATE OR REPLACE FUNCTION get_map_entries(
    p_min_lat double precision DEFAULT -90,
    p_max_lat double precision DEFAULT 90,
    p_min_lng double precision DEFAULT -180,
    p_max_lng double precision DEFAULT 180,
    p_category text DEFAULT 'Alles'
) RETURNS TABLE(
    id uuid, title text, category text, location text,
    latitude double precision, longitude double precision,
    event_type text, user_id uuid
) LANGUAGE sql STABLE
AS $$
    SELECT id, title, category, location, latitude, longitude, event_type, user_id
    FROM entries
    WHERE is_completed = false
      AND latitude IS NOT NULL
      AND latitude BETWEEN p_min_lat AND p_max_lat
      AND longitude BETWEEN p_min_lng AND p_max_lng
      AND (p_category = 'Alles' OR category = p_category);
$$;
```

---

**BUILD-GATE Phase 3:**
```bash
# 1. Alle RPCs in Supabase deployen
# 2. Testen via Supabase SQL Editor:
#    SELECT * FROM get_user_chats('user-uuid-hier');
#    SELECT * FROM get_entry_detail_bundle('entry-uuid', 'user-uuid');
#    SELECT * FROM mark_notifications_read('user-uuid');
# 3. Edge Functions deployen und testen
# 4. dotnet build NBH.csproj -c Debug
# 5. App-seitige RPC-Integration testen (Chat-Liste, Detail, Notifications)
```

---

## Phase 4: App-Architektur bereinigen

**Ziel:** Service Locator eliminieren, Navigation vereinheitlichen, Lifecycle-Sicherheit
**Dauer:** Tag 22-28
**Build-Gate:** Keine MauiProgram.Services-Zugriffe mehr, einheitliche Navigation

### 4.1 Service Locator eliminieren (15+ Stellen)

**Jede Stelle muss durch Konstruktor-Injektion ersetzt werden:**

| # | Datei | Zeile | Aktuell | Aenderung |
|---|-------|-------|---------|-----------|
| 4.1a | App.xaml.cs | 57 | `MauiProgram.Services.GetRequiredService<AppShell>()` | AppShell per Konstruktor-Parameter in App injizieren |
| 4.1b | App.xaml.cs | 138 | `MauiProgram.Services.GetRequiredService<AppShell>()` | Gespeicherte Referenz nutzen |
| 4.1c | App.xaml.cs | 151 | `MauiProgram.Services.GetRequiredService<AppShell>()` | Gespeicherte Referenz nutzen |
| 4.1d | App.xaml.cs | 159 | `MauiProgram.Services.GetRequiredService<AppShell>()` | Gespeicherte Referenz nutzen |
| 4.1e | App.xaml.cs | 166 | `MauiProgram.Services.GetRequiredService<AppShell>()` | Gespeicherte Referenz nutzen |
| 4.1f | App.xaml.cs | 224 | `MauiProgram.Services` | Injizierte Services nutzen |
| 4.1g | App.xaml.cs | 283, 291, 314 | `MauiProgram.Services.GetRequiredService<AppShell>()` | Gespeicherte Referenz nutzen |
| 4.1h | SplashPage.xaml.cs | 141 | `MauiProgram.Services.GetRequiredService<AppShell>()` | Per Konstruktor injizieren |
| 4.1i | NavigationService.cs | 41 | `MauiProgram.Services.GetRequiredService<AppShell>()` | Per Konstruktor injizieren |
| 4.1j | LoginPage.xaml.cs | 34 | `MauiProgram.Services.GetRequiredService<LoginPageViewModel>()` | DI-Konstruktor nutzen (Zeile 42-53 existiert bereits) |
| 4.1k | RegisterPage.xaml.cs | 22-23 | `MauiProgram.Services.GetRequiredService<>()` | Per Konstruktor injizieren |
| 4.1l | BellControl.xaml.cs | 22, 99-102 | `MauiProgram.Services.GetRequiredService<>()` | Per Konstruktor injizieren oder BindableProperty |
| 4.1m | AddPage.xaml.cs | 31 | `MauiProgram.Services` | Per Konstruktor injizieren |
| 4.1n | AddPageViewModel.cs | 187 | `new LoginPage()` | Shell-Navigation nutzen: `Shell.Current.GoToAsync(Routes.LoginPage)` |
| 4.1o | ProfilePageViewModel.cs | 406 | `MauiProgram.Services.GetRequiredService<AppShell>()` | Injizierte Navigation nutzen |

**Nach Abschluss: `MauiProgram.Services` static Property (Zeile 28) entfernen.**

### 4.2 MainPage-Reset-Kaskaden eliminieren

**Ziel:** Ein einziger Ort steuert den Login/App-Wechsel.

**Neuer Service: NavigationOrchestrator.cs**
```csharp
public class NavigationOrchestrator
{
    private readonly IServiceProvider _services;

    public NavigationOrchestrator(IServiceProvider services)
    {
        _services = services;
    }

    public void ShowApp()
    {
        if (Application.Current?.MainPage is AppShell) return;
        Application.Current!.MainPage = _services.GetRequiredService<AppShell>();
    }

    public void ShowLogin()
    {
        Application.Current!.MainPage = _services.GetRequiredService<LoginPage>();
    }
}
```

**Alle MainPage-Zuweisungen ersetzen:**

| Datei | Zeile(n) | Ersetzen durch |
|-------|----------|----------------|
| App.xaml.cs | 57, 138, 151, 159, 166, 282-283, 291-292, 313-314 | `_orchestrator.ShowApp()` |
| App.xaml.cs | 81, 103 | `MainPage = new SplashPage(...)` (Startup — bleibt als Ausnahme) |
| SplashPage.xaml.cs | 141 | `_orchestrator.ShowApp()` |
| NavigationService.cs | 41 | `_orchestrator.ShowApp()` |
| AddPageViewModel.cs | 187 | `_orchestrator.ShowLogin()` ODER Shell-Navigation |
| ProfilePageViewModel.cs | 406 | `_orchestrator.ShowApp()` |

### 4.3 Routing vereinheitlichen

**AppShell.xaml.cs — Zeilen 30-41: Route-Registrierungen ENTFERNEN**
(Sind Duplikate von MauiProgram.cs Zeilen 213-219)

**Routes.cs — Alle Konstanten verifizieren:**
```
Zeile 14: MainPage
Zeile 16: LoginPage
Zeile 18: ProfilePage (absoluter Pfad)
Zeile 20: DetailPage = "detail"
Zeile 22: ChatPage = "ChatPage"
Zeile 24: UserProfilePage
Zeile 26: CreateEvent = "CreateEventPage"
Zeile 28: RatingPage
Zeile 29: SettingsPage
```

**Sicherstellen:** MyPushDelegate.cs (Zeile 129) nutzt `Routes.DetailPage` statt hardcoded String.

### 4.4 LoginPage duale Konstruktoren bereinigen

**LoginPage.xaml.cs — Zeilen 33-37 (parameterloser Konstruktor) ENTFERNEN.**
Nur DI-Konstruktor (Zeilen 42-53) behalten.

### 4.5 CropImagePage Environment-Variablen ersetzen

**CropImagePage.xaml.cs — Zeilen 37-38:**
```
AKTUELL:
static readonly string SupabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
static readonly string SupabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY");

AENDERN:
static readonly string SupabaseUrl = AppConfig.SupabaseUrl;
static readonly string SupabaseKey = AppConfig.SupabaseAnonKey;
```

### 4.6 Notification-Limit einfuehren

**NotificationService.cs — Zeilen 126-130 (LoadNotificationsFromDatabaseAsync):**
```
AKTUELL:  .Order("created_at", Ordering.Descending).Get()
AENDERN:  .Order("created_at", Ordering.Descending).Limit(50).Get()
```

### 4.7 Ban-Check cachen

**AuthService.cs — Zeile 318-338 (EnsureSessionValidAsync):**
```csharp
// Neues Feld in AuthService:
private DateTime _lastBanCheckAt = DateTime.MinValue;
private const int BanCheckCooldownMinutes = 5;

// In EnsureSessionValidAsync, VOR dem Ban-Check:
if ((DateTime.UtcNow - _lastBanCheckAt).TotalMinutes < BanCheckCooldownMinutes)
    return true; // Gecachtes Ergebnis nutzen

// Nach erfolgreichem Ban-Check:
_lastBanCheckAt = DateTime.UtcNow;
```

### 4.8 PhoenixRealtimeService: Exponentieller Backoff

**PhoenixRealtimeService.cs — Zeilen 279-293 (ScheduleReconnect):**
```csharp
// Neues Feld:
private int _reconnectAttempts = 0;
private const int MaxReconnectAttempts = 10;

// In ScheduleReconnect:
if (_reconnectAttempts >= MaxReconnectAttempts)
{
    Debug.WriteLine("[Realtime] Max reconnect attempts reached");
    SessionExpired?.Invoke(this, EventArgs.Empty);
    return;
}
_reconnectAttempts++;
var delay = Math.Min(60000, 1000 * Math.Pow(2, _reconnectAttempts)); // Exponential Backoff
await Task.Delay((int)delay);
```

### 4.9 Mopup-Dateien auf Supabase SDK umstellen

**ChatReviewMopup.xaml.cs:**
- Zeile 43: HttpClient-Erstellung ENTFERNEN
- Zeile 45: Hardcodierte URL ENTFERNEN
- Zeilen 74, 181: GetAsync/PostAsync ersetzen durch:
```csharp
// Statt HttpClient.GetAsync:
var response = await _supabaseClient.From<ChatReview>()
    .Filter("chat_id", Operator.Equals, chatId)
    .Filter("reviewer_id", Operator.Equals, reviewerId)
    .Get();

// Statt HttpClient.PostAsync:
await _supabaseClient.From<ChatReview>().Insert(newReview);
```

**EventRatingMopup.xaml.cs:** Analog (Zeilen 43, 45, 72, 191).

Beide Dateien muessen Supabase-Client per Konstruktor-Injektion erhalten.

### 4.10 Stubs fertigstellen oder entfernen

**SettingsPageViewModel.cs — Zeile 35:**
```
AKTUELL:  CurrentUsername = "Max Mustermann"
AENDERN:  CurrentUsername laden aus AuthService/UserProfile
```

**SettingsPageViewModel.cs — Zeilen 146-149 (NavigateToFaqAsync):**
```
AENDERN:  await Shell.Current.GoToAsync(nameof(FAQPage));
```

**RatingPageViewModel.cs — Zeilen 22-28:**
```
AENDERN:  LoadDataAsync implementieren mit get_user_rating_stats RPC
```

**CreateEventPage.xaml — Zeilen 144 und 157:**
```
AKTUELL:  MinimumDate="01/01/2025"
AENDERN:  MinimumDate="{x:Static sys:DateTime.Today}"
```
(Mit `xmlns:sys="clr-namespace:System;assembly=netstandard"`)

---

**BUILD-GATE Phase 4:**
```bash
dotnet build NBH.csproj -c Debug
# Grep pruefen: keine MauiProgram.Services Referenzen mehr
grep -r "MauiProgram.Services" --include="*.cs" .
# App auf Emulator:
# 1. Login/Logout-Zyklus (NavigationOrchestrator)
# 2. Push-Notification -> Deep Link (Routes)
# 3. Chat oeffnen/schliessen/erneut oeffnen (Memory Leak Test)
# 4. Notifications laden (Limit 50)
# 5. Bewertung abgeben (Mopup mit SDK)
# 6. Event erstellen (Datum-Locale)
```

---

## Phase 5: Performance und MAUI-10-Haertung

**Ziel:** Frame-Drops eliminieren, Compiled Bindings flaechendeckend, alle MAUI 10 Features nutzen
**Dauer:** Tag 29-35
**Build-Gate:** Keine Frame-Drops > 100 Frames, strenge XAML-Kompilierung aktiv

### 5.1 MapPage MVVM-konform umbauen

**Neues File: Resources/ViewModels/MapPageViewModel.cs**
- Gesamte DB-/Geocoding-Logik aus MapPage.xaml.cs (Zeilen 52-91) auslagern
- `get_map_entries` RPC nutzen statt Client-Geocoding
- ObservableCollection<MapPin> als Property

**MapPage.xaml.cs — Zeilen 52-91 (LoadPinsFromDatabaseAsync):**
KOMPLETT ERSETZEN durch ViewModel-Binding.
Code-Behind reduziert sich auf Map-spezifische UI-Events.

**MapPage.xaml.cs — Zeilen 64-80 (Geocoding-Schleife):**
KOMPLETT ENTFERNEN. Koordinaten kommen aus DB (Phase 3.8).

### 5.2 ChatViewPage auf RPC umstellen

**ChatViewPageViewModel.cs — Zeilen 55-126 (LoadChatsAsync):**
ERSETZEN durch:
```csharp
var result = await _supabaseClient.Rpc("get_user_chats", new { p_user_id = currentUserId });
// Ergebnis in ObservableCollection<ChatOverviewItem> mappen
```

**ChatViewPageViewModel.cs — Zeilen 190-223 (GetPartnerDataAsync):**
ENTFERNEN (Daten kommen aus RPC).

### 5.3 Compiled Bindings flaechendeckend aktivieren

**NBH.csproj — HINZUFUEGEN:**
```xml
<MauiStrictXamlCompilation>true</MauiStrictXamlCompilation>
```

**XAML-Dateien OHNE x:DataType — muessen ergaenzt werden:**

| Datei | Aenderung |
|-------|-----------|
| RatingPage.xaml | `x:DataType="viewModels:RatingPageViewModel"` hinzufuegen |
| MapPage.xaml | `x:DataType="viewModels:MapPageViewModel"` hinzufuegen (nach 5.1) |
| CreateEventPage.xaml | `x:DataType="viewModels:CreateEventPageViewModel"` hinzufuegen |
| SplashPage.xaml | `x:DataType` nicht noetig (kein ViewModel-Binding) |
| CropImagePage.xaml | `x:DataType` oder Code-Behind-Bindings (kein ViewModel) |
| FAQPage.xaml | Root `x:DataType="viewModels:FAQPageViewModel"` hinzufuegen |

**Controls OHNE x:DataType (pruefen ob Bindings vorhanden):**
- BellControl.xaml — Falls Bindings: x:DataType hinzufuegen
- CapacityBarControl.xaml — Falls Bindings: x:DataType hinzufuegen
- StarRatingControl.xaml — Falls Bindings: x:DataType hinzufuegen
- EmptyStateView.xaml — Falls Bindings: x:DataType hinzufuegen
- InlineLoadingButton.xaml — Falls Bindings: x:DataType hinzufuegen
- LoadingOverlay.xaml — Falls Bindings: x:DataType hinzufuegen
- TypingIndicatorControl.xaml — Falls Bindings: x:DataType hinzufuegen

### 5.4 OneSignal-Setup bereinigen

**App.xaml.cs — Zeilen 45-47 (OneSignal Init):**
- Sicherstellen dass `OneSignal.Initialize(AppConfig.OneSignalAppId)` nur EINMAL aufgerufen wird
- `RequestPermission` NICHT im App-Konstruktor, sondern nach Login/Onboarding
- Otel-Klassenproblem: Pruefen ob OneSignalSDK.DotNet 6.1.3 das Otel-Paket optional benoetigt

**NBH.csproj — Zeile 211:**
```
AKTUELL:  <PackageReference Include="OneSignalSDK.DotNet" Version="6.1.3" />
PRUEFEN:  Auf neueste Version aktualisieren falls Otel-Bug behoben
```

### 5.5 AndroidX-Versionskonflikte aufloesen

**NBH.csproj — Package-Versionen harmonisieren:**
Explizite Versionen fuer konfliktbehaftete Pakete hinzufuegen:
```xml
<PackageReference Include="Xamarin.AndroidX.Lifecycle.LiveData" Version="2.9.0.1" />
<PackageReference Include="Xamarin.AndroidX.Fragment.Ktx" Version="1.8.6.1" />
<!-- Weitere je nach Build-Warnungen -->
```

### 5.6 WeakReferenceMessenger konsistent nutzen

**Pruefen und umstellen:**
- Alle Event-basierten Cross-ViewModel-Kommunikation auf WeakReferenceMessenger
- Besonders: Notification-Updates zwischen NotificationService und BellControl
- Besonders: Entry-Updates zwischen AddPageViewModel und DetailPageViewModel

### 5.7 Layout-Performance messen

**NBH.csproj — HINZUFUEGEN fuer Debug-Builds:**
```xml
<MauiEnableLayoutPerformanceTracking Condition="'$(Configuration)'=='Debug'">true</MauiEnableLayoutPerformanceTracking>
```

Startup-, Map- und Chat-Performance mit MAUI Diagnostics messen.
Zielwerte: Cold Start < 3s, Seitenwechsel < 500ms, keine Skipped Frames > 50.

---

**BUILD-GATE Phase 5:**
```bash
dotnet build NBH.csproj -c Debug
# ERWARTUNG: Keine XAML-Binding-Warnungen (MauiStrictXamlCompilation)
# App auf Emulator:
# 1. Karte oeffnen — Pins erscheinen sofort (keine Geocoding-Wartezeit)
# 2. Chat-Liste oeffnen — schnelles Laden (RPC statt N+1)
# 3. Memory-Profiling: 5x Chat oeffnen/schliessen — kein Speicherwachstum
# 4. Push-Notification empfangen — korrekte Navigation
# 5. Debug-Output pruefen: keine Skipped-Frames > 100
```

---

## Phase 6: Produktionsreife

**Ziel:** Store-Release-Qualitaet
**Dauer:** Tag 36+
**Build-Gate:** Release-Build, Store-Compliance, Performance-Benchmarks

### 6.1 Offline-/Cache-Strategie

- SQLite-Cache fuer Profile (5min TTL), Entry-Listen (2min TTL), Chat-Overview (1min TTL)
- Optimistic UI fuer Chat-Nachrichten (sofort anzeigen, spaeter bestaetigen)
- Connectivity-Check: bei fehlender Verbindung aus Cache laden

### 6.2 Dateistruktur auf Feature-Folders umstellen

```
Features/
  Auth/       LoginPage, RegisterPage, LoginPageViewModel, RegisterPageViewModel, AuthService
  Profile/    ProfilePage, CropImagePage, ProfilePageViewModel, UserProfilePage, UserProfilePageViewModel
  Entries/    AddPage, DetailPage, CreateEventPage, AddPageViewModel, DetailPageViewModel, CreateEventPageViewModel
  Chat/       ChatPage, ChatViewPage, ChatPageViewModel, ChatViewPageViewModel, ChatReviewMopup
  Map/        MapPage, MapPageViewModel
  Notifications/ BellControl, NotificationListViewModel, NotificationService, NotificationPopup
  Rating/     RatingPage, RatingPageViewModel, EventRatingMopup
  Settings/   SettingsPage, SettingsPageViewModel, FAQPage, FAQPageViewModel
  Moderation/ ModerationService, Reports
```

### 6.3 Storage bucketisieren

- `profile_pictures` — Profilbilder (existiert)
- `event_images` — Event-Bilder (NEU)
- `documents` — Qualifikationsdateien (NEU)
- Jeder Bucket mit eigenen Storage-Policies (Ownership-basiert)

### 6.4 CI/CD Pipeline

```yaml
# .github/workflows/build.yml
name: NBH Build & Test
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet build NBH.csproj -c Release
      - run: dotnet test (wenn Tests existieren)
```

### 6.5 Weitere Produktionsfeatures (nach Bedarf)

| Feature | DB-Aenderung | Prioritaet |
|---------|-------------|-----------|
| Dark Mode | DynamicResource-Theming | MITTEL |
| Soft Delete | `deleted_at` Spalte + RLS-Filter | MITTEL |
| Audit Trail | `audit_log` Tabelle + Trigger | NIEDRIG |
| pg_cron | Auto-Complete Events nach 48h | NIEDRIG |
| CDN/Image-Transformation | Supabase Image Transformation | NIEDRIG |
| PostGIS Geo-Search | geography Column + Radius-Query | NIEDRIG |
| Chat-Verschluesselung (E2E) | encrypted_message Spalte + Key-Exchange | NIEDRIG |

---

## Anhang A: Vollstaendige Befundmatrix

### Sicherheit

| # | Befund | Datei | Zeile | Schwere | Phase |
|---|--------|-------|-------|---------|-------|
| S1 | Hardcodierte Supabase-URL | ChatReviewMopup.xaml.cs | 45 | KRITISCH | 0.1 |
| S2 | Hardcodierte Supabase-URL | EventRatingMopup.xaml.cs | 45 | KRITISCH | 0.1 |
| S3 | chat_messages SELECT USING (true) | RLS Policy | - | KRITISCH | 2.1 |
| S4 | notifications INSERT WITH CHECK (true) | RLS Policy | - | KRITISCH | 2.1 |
| S5 | storage.objects breite INSERT | RLS Policy | - | KRITISCH | 2.1 |
| S6 | users SELECT USING (true) — alle Felder | RLS Policy | - | HOCH | 2.1 |
| S7 | entries doppelte INSERT neutralisiert Ban | RLS Policy | - | HOCH | 2.1 |
| S8 | group_event_roles INSERT zu offen | RLS Policy | - | HOCH | 2.1 |
| S9 | launchSettings.json in Git | Properties/launchSettings.json | - | HOCH | 0.2 |
| S10 | CropImagePage ENV-Variablen statt AppConfig | CropImagePage.xaml.cs | 37-38 | MITTEL | 4.5 |
| S11 | ImageSharp Sicherheitsluecke | NBH.csproj | 216 | HOCH | 0.5 |
| S12 | Edge Functions ohne Auth | notify_partner_assigned, notify_new_chat_message | - | HOCH | 3.7 |
| S13 | Duplikat-Trigger doppelte Ausfuehrung | auth.users | - | MITTEL | 2.5 |

### Stabilitaet

| # | Befund | Datei | Zeile | Schwere | Phase |
|---|--------|-------|-------|---------|-------|
| C1 | .Wait() blockiert Thread | SupabaseSessionHandler.cs | 26 | KRITISCH | 1.1 |
| C2 | .Result blockiert Thread | SupabaseSessionHandler.cs | 55 | KRITISCH | 1.1 |
| C3 | async void LoadUserDataAsync | DetailPageViewModel.cs | 430 | KRITISCH | 1.2 |
| C4 | async void LoadEntryState | DetailPageViewModel.cs | 549 | KRITISCH | 1.2 |
| C5 | async void OnResume | App.xaml.cs | 115 | KRITISCH | 1.2 |
| C6 | Fire-and-forget Auth-Init | MauiProgram.cs | 120 | HOCH | 1.3 |
| C7 | Notification-Deserialisierung | NotificationService.cs | 217-232 | KRITISCH | 1.5 |
| C8 | JObject.Parse ohne try/catch | NotificationListViewModel.cs | 78 | HOCH | 1.5 |
| C9 | Guid.Parse ohne TryParse | DetailPageViewModel.cs | 754 | HOCH | 1.6 |
| C10 | CreateEvent Doppelsubmit | CreateEventPageViewModel.cs | ~160 | MITTEL | 1.7 |
| C11 | OneSignal teil-initialisiert | App.xaml.cs | 45-47 | KRITISCH | 5.4 |
| C12 | Kultur-sensitives Datum | CreateEventPage.xaml | 144, 157 | MITTEL | 4.10 |

### Memory Leaks

| # | Befund | Datei | Zeile | Schwere | Phase |
|---|--------|-------|-------|---------|-------|
| M1 | PostgresChanged nie abgemeldet | ChatPageViewModel.cs | 148 | HOCH | 1.4 |
| M2 | BroadcastReceived nie abgemeldet | ChatPageViewModel.cs | 149 | HOCH | 1.4 |
| M3 | PostgresChanged nie abgemeldet | NotificationService.cs | 75 | HOCH | 1.4 |
| M4 | Timer ohne Dispose-Garantie | ChatPageViewModel.cs | 267, 288 | MITTEL | 1.4 |
| M5 | ViewModels ohne IDisposable | Alle ViewModels | - | MITTEL | 4 |

### Performance

| # | Befund | Datei | Zeile | Schwere | Phase |
|---|--------|-------|-------|---------|-------|
| P1 | Client-Geocoding in Schleife | MapPage.xaml.cs | 64-80 | KRITISCH | 5.1 |
| P2 | N+1 Queries (2+N) | ChatViewPageViewModel.cs | 85-96 | HOCH | 5.2 |
| P3 | Notifications kein LIMIT | NotificationService.cs | 126-130 | HOCH | 4.6 |
| P4 | Mark-All-Read N Updates | NotificationListViewModel.cs | 118-128 | HOCH | 3.5 |
| P5 | Ban-Check ohne Cache | AuthService.cs | 318-338 | MITTEL | 4.7 |
| P6 | Detail 3-7 sequentielle Queries | DetailPageViewModel.cs | 100-116 | MITTEL | 3.3 |
| P7 | GroupEvents N+1 Users | DetailPageViewModel.GroupEvents.cs | 89-160 | MITTEL | 3.3 |
| P8 | Rating doppelte Berechnung | UserProfilePageViewModel.cs | 194-226 | MITTEL | 3.4 |
| P9 | AddPage aggressive Reloads | AddPageViewModel.cs | 82, 101, 116 | MITTEL | 4 |

### Architektur

| # | Befund | Datei | Zeile | Schwere | Phase |
|---|--------|-------|-------|---------|-------|
| A1 | Service Locator (15+ Stellen) | Diverse | Diverse | HOCH | 4.1 |
| A2 | 9+ MainPage-Resets | App.xaml.cs + Andere | Diverse | HOCH | 4.2 |
| A3 | MapPage ohne ViewModel | MapPage.xaml.cs | - | HOCH | 5.1 |
| A4 | Doppelte Route-Registrierung | AppShell.xaml.cs | 30-41 | MITTEL | 4.3 |
| A5 | REST-Bypass in Mopups | ChatReviewMopup.xaml.cs, EventRatingMopup.xaml.cs | 43-45 | HOCH | 4.9 |
| A6 | String-IDs statt UUID | EntryItem.cs:31, PublicChat.cs:17,21 | - | HOCH | 2.3 |
| A7 | Duale Konstruktoren | LoginPage.xaml.cs | 33-37, 42-53 | MITTEL | 4.4 |
| A8 | Kein x:DataType (5 Pages) | RatingPage, MapPage, CreateEventPage, SplashPage, CropImagePage | - | MITTEL | 5.3 |
| A9 | Kein x:DataType (10 Controls) | Diverse Controls | - | NIEDRIG | 5.3 |
| A10 | AppCenter retired | NBH.csproj | 195-197 | HOCH | 0.4 |
| A11 | PhoenixRealtime: kein Backoff | PhoenixRealtimeService.cs | 279-293 | MITTEL | 4.8 |
| A12 | "Max Mustermann" hardcoded | SettingsPageViewModel.cs | 35 | NIEDRIG | 4.10 |
| A13 | NavigateToFaqAsync leer | SettingsPageViewModel.cs | 146-149 | NIEDRIG | 4.10 |
| A14 | RatingPage LoadData auskommentiert | RatingPageViewModel.cs | 22-28 | NIEDRIG | 4.10 |

---

## Anhang B: Entschiedene Produktfragen

| # | Frage | Entscheidung | Betroffene Phase |
|---|-------|-------------|-----------------|
| PF1 | Rohe Bewertungen sichtbar? | Nur aggregierte Scores | Phase 2 (Migration 007) |
| PF2 | Teilnehmerlisten sichtbar? | Nur Owner und Betroffene | Phase 2 (Migration 008) |
| PF3 | Notifications vom Client? | Nur Systempfade (RPC/Edge Function) | Phase 2 (Migration 004), Phase 3 (3.6) |
| PF4 | FAQ-Tabelle? | Nur `faq` (modern), `faqs` entfernen | Phase 2 (Migration 009) |
| PF5 | Secrets rotiert? | Muss geprueft werden | Phase 0 |

---

## Anhang C: Verifizierte Quellen

| Thema | Quelle |
|-------|--------|
| Compiled Bindings MAUI 10 | https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/compiled-bindings?view=net-maui-10.0 |
| XAML Compilation | https://learn.microsoft.com/en-us/dotnet/maui/xaml/xamlc?view=net-maui-10.0 |
| Supabase RLS Best Practices | https://supabase.com/docs/guides/database/postgres/row-level-security |
| Supabase RLS Performance | https://supabase.com/docs/guides/troubleshooting/rls-performance-and-best-practices-Z5Jjwv |
| Supabase Edge Functions Auth | https://supabase.com/docs/guides/functions/auth |
| Supabase API Keys | https://supabase.com/docs/guides/api/api-keys |
| PostgreSQL RLS Performance | https://github.com/orgs/supabase/discussions/14576 |
| WeakReferenceMessenger | https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/messenger |
| Shell Navigation MAUI 10 | https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/navigation?view=net-maui-10.0 |
| Sentry MAUI | https://docs.sentry.io/platforms/dotnet/guides/maui/ |
| AppCenter Retirement | https://github.com/microsoft/appcenter/issues/2625 |
| OneSignal .NET SDK | https://documentation.onesignal.com/docs/en/net-sdk-setup |
| MAUI Memory Management | https://markaicode.com/maui-memory-leaks-2025/ |
| MAUI DI Best Practices | https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/dependency-injection?view=net-maui-10.0 |

---

*Ende des Masterplans. Jede Phase ist eigenstaendig testbar. Empfohlene Reihenfolge: Phase 0 -> 1 -> 2 -> 3 -> 4 -> 5 -> 6.*
