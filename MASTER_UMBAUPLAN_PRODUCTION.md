# NBH Care Community - Finaler Umbauplan fuer Client und Datenbank

Stand: 2026-04-07

Dieses Dokument ist der konsolidierte Masterplan fuer den Umbau der NBH-App auf eine performante, moderne, stabile, sichere und produktionsreife Architektur.

Der Plan basiert auf:

- der historischen Analyse von `NBH-10.zip`
- der Analyse von `NBH-latest`
- der Analyse von `NBH-latestv4`
- der Debug-/Build-Analyse von `NBH-latest`
- der direkten Codeanalyse des aktuellen Workspace
- der direkten Codeanalyse von `NBH-latestv3.zip`
- dem gelieferten aktuellen RLS-/Policy-Stand
- den CSV-Exports zu Spalten, Constraints, Indizes, RLS, Policies, Views, Funktionen, Triggern und Row-Counts
- offiziellen Quellen zu .NET MAUI 10, CommunityToolkit MVVM, Supabase/PostgreSQL RLS und OneSignal

## 1. Zielbild

Nach Abschluss aller Phasen soll die App:

- auf einer konsistenten, dokumentierten und abgesicherten Datenbankstruktur laufen
- ein klares RLS-Modell ohne permissive Policy-Leaks besitzen
- Kernlogik ueber RPCs/Views statt ueber clientseitige N+1- und Mehrfachqueries abbilden
- eine saubere Client-Architektur mit klaren Feature-Grenzen besitzen
- einen deterministischen Startup-, Session- und Navigationsfluss haben
- stabile Realtime-, Push- und Notification-Pfade nutzen
- reproduzierbar gebaut, getestet und in Etappen ausgerollt werden koennen

## 2. Konsolidiertes Ist-Bild

### 2.1 Client

Aktueller App-Stand:

- .NET MAUI 10 (`Microsoft.Maui.Controls 10.0.41`)
- `CommunityToolkit.Maui 14.0.1`
- `CommunityToolkit.Mvvm 8.4.0`
- `Supabase 1.0.0`, `Supabase.Postgrest 4.0.3`, `Supabase.Realtime 7.0.1`
- `OneSignalSDK.DotNet 6.1.3`
- `SixLabors.ImageSharp 3.1.7`
- `Microsoft.AppCenter 5.0.7`

Aktuelle fachliche Features:

- Auth inkl. Apple-Login
- Profile und Fremdprofile
- Entries / Events / Gruppen-Events
- Chat / Notification / Realtime
- Ratings / Reviews
- FAQ
- Settings / Moderation / Feedback
- Maps

### 2.2 Datenbank

Aktueller DB-Stand laut CSV:

- `users`: 24 Spalten
- `entries`: 18 Spalten
- `chats`: 10 Spalten
- `chat_messages`: 5 Spalten
- `chat_reviews`: 8 Spalten
- `event_rating`: 8 Spalten
- `notifications`: 7 Spalten (`action_data` = `jsonb`)
- `reports`: 10 Spalten
- `faq`: 7 Spalten
- `group_event_roles`: 5 Spalten
- `group_event_participants`: 7 Spalten
- `user_badges`: 9 Spalten
- zusaetzlich vorhanden:
  - `entry_helpers`
  - `entry_helper_signups`
  - `entry_participants`
  - `group_chats`
  - `group_chat_members`
  - `group_chat_messages`
  - View `v_event_participants`

### 2.3 Datenvolumen

Aktuelle Row-Counts:

- `users`: 7
- `entries`: 22
- `chats`: 6
- `chat_messages`: 164
- `chat_reviews`: 6
- `event_rating`: 11
- `notifications`: 222
- `reports`: 0
- `group_event_roles`: 0
- `group_event_participants`: 0
- `user_badges`: 0

Fazit:

- Das System ist produktnah, aber noch klein genug fuer migrationssichere Sanierung.
- Jetzt ist der beste Zeitpunkt fuer strukturellen Umbau.

## 3. Konsolidierte Hauptprobleme

### 3.1 Datenbank / RLS

1. RLS ist vorhanden, aber in Teilen zu offen.
2. Mehrere permissive Policies neutralisieren strengere Policies durch OR-Verhalten.
3. `chat_messages.allow_select_realtime` ist aktuell sicherheitskritisch.
4. `entries`-Policies modellieren Fachlogik ueber zu breite UPDATE-Rechte.
5. `notifications`-Insert ist zu offen fuer normale Clients.
6. `storage.objects` und `storage.buckets` sind zu breit modelliert.
7. `users SELECT true` fuer `authenticated` ist fuer sensible Felder zu weit.
8. Trigger sind doppelt oder ueberlappend:
   - doppelter Auth-Sync-Trigger
   - doppelter `update_last_message`-Trigger
9. Es existieren Drift-/Altlaststrukturen:
   - `entry_helpers`
   - `entry_helper_signups`
   - `entry_participants`
   - `group_chat_*`

### 3.2 Client-Architektur

1. Startup und Navigation sind nicht zentral orchestriert.
2. `MainPage` wird mehrfach direkt gesetzt.
3. Sessionhandling blockiert auf async Storage.
4. Realtime-Subscriptions haben Lifecycle-Risiken.
5. DB-Modelle und UI-ViewState sind vermischt.
6. Es gibt weiterhin rohe REST-Schreibpfade ausserhalb des Supabase-/RPC-Service-Layers.
7. Routing ist inkonsistent zwischen Shell-Namen und Magic-Strings.

### 3.3 Performance

1. Map ist klarer Haupt-Hotspot.
2. Chat-Inbox hat weiterhin N+1-Last.
3. Detail/Gruppen-Event-Logik ist clientseitig zu query-lastig.
4. Notification-Mark-All-Read arbeitet einzeln statt gebatched.
5. Startup/Resume hat unnoetige Parallelitaet und Main-Thread-Last.

### 3.4 Build / Dependency / Tooling

1. AppCenter ist veraltet und abloesebeduerftig.
2. `SixLabors.ImageSharp 3.1.7` ist als Sicherheits-/Qualitaetsrisiko markiert.
3. AndroidX-Drift wurde bereits im Build-Log sichtbar.
4. Fehlende bzw. inkonsistente Assets:
   - `trash.png`
   - `placeholder_image.png`
   - `settings.png` existiert, muss aber packaging-seitig sauber bleiben

## 4. Datei- und Codeanker: identifizierte Umbaupunkte

Die folgende Liste ist die aktuell identifizierte Arbeitsbasis. Sie ist bewusst praktisch formuliert: pro Datei, mit konkreten Startpunkten fuer den Umbau.

### 4.1 App-Start, Routing, MainPage, DeepLinks

- [App.xaml.cs](C:/Data/NBH/App.xaml.cs)
  - `45-47`: fruehe OneSignal-Initialisierung und Permission-Request
  - `59`: DeepLink-Navigation auf `detail?entryId=...`
  - `82`, `105`: `MainPage = new SplashPage()`
  - `117ff`: `OnResume`
  - `144`, `166`, `174`, `181`: direkte `Application.Current.MainPage = ...`
  - `213`: `WeakReferenceMessenger.Default.Send(...)`
  - `274ff`: Notification-Init im Social-Login-Pfad

- [Routes.cs](C:/Data/NBH/Routes.cs)
  - relative Route `DetailPage = "detail"` ist nicht konsistent zur Shell-Registrierung

- [AppShell.xaml.cs](C:/Data/NBH/AppShell.xaml.cs)
  - `30-41`: Routenregistrierung via `nameof(...)`

- [MauiProgram.cs](C:/Data/NBH/MauiProgram.cs)
  - `114-120`: `AuthService`-Init via `Task.Run(...)`
  - `211-218`: zusätzliche Routenregistrierung
  - `226`: `AppCenter.Start(...)`

### 4.2 Session, Token, Storage

- [SupabaseSessionHandler.cs](C:/Data/NBH/SupabaseSessionHandler.cs)
  - `26`: `Task.Run(...).Wait()`
  - `55`: `.Result`
  - `41`, `68`: Legacy-Preferences-Migration

- [ProfilePageViewModel.cs](C:/Data/NBH/Resources/ViewModels/ProfilePageViewModel.cs)
  - `403`: `SecureStorage.Remove("supabase_session")` - Legacy-Key

- [CropImagePage.xaml.cs](C:/Data/NBH/CropImagePage.xaml.cs)
  - `242`, `246`: direkter `jwt_token`-Pfad

- [ChatReviewMopup.xaml.cs](C:/Data/NBH/Resources/Views/ChatReviewMopup.xaml.cs)
  - `62`, `66`: direkter `jwt_token`-Pfad

- [EventRatingMopup.xaml.cs](C:/Data/NBH/Resources/Views/EventRatingMopup.xaml.cs)
  - `60`, `64`: direkter `jwt_token`-Pfad

### 4.3 Notification- und Push-Achse

- [NotificationModel.cs](C:/Data/NBH/Resources/Models/NotificationModel.cs)
  - `32`: `ActionData` als `string`, obwohl DB `jsonb` ist
  - `43`: parser erwartet `hasRatingButton`, Daten sind inkonsistent (`HasRatingButton`/`hasRatingButton`)

- [NotificationService.cs](C:/Data/NBH/Services/NotificationService.cs)
  - `58`: `OneSignal.Login(currentUserId)`
  - `88-96`: Client-seitiges Notification-Insert
  - `116-127`: Laden ueber direkten Tabellenzugriff
  - `224`: `DeserializeObject<NotificationModel>`

- [NotificationListViewModel.cs](C:/Data/NBH/Resources/ViewModels/NotificationListViewModel.cs)
  - `78`: `JObject.Parse(notif.ActionData)`
  - `94ff`: `MarkAllAsReadAsync()` als Einzelupdates
  - `140`: Logging bestaetigt Einzelupdates

- [BellControl.xaml.cs](C:/Data/NBH/Resources/Controls/BellControl.xaml.cs)
  - `23-26`: Event-Registrierung ohne echten Lifecycle-Cleanup
  - `36`, `43`: `async void`

- [LoginPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/LoginPageViewModel.cs)
  - `216-217`: `OneSignal.User.Changed`-Lifecycle
  - `230`: fire-and-forget UpdateOneSignalSubscription

- [MyPushDelegate.cs](C:/Data/NBH/Services/MyPushDelegate.cs)
  - Notification-/Subscription-Achse gegen neuen RLS-/RPC-Ansatz pruefen

### 4.4 Entries, Map, Detail

- [EntryItem.cs](C:/Data/NBH/Resources/Models/EntryItem.cs)
  - `31`: `user_id` noch `string`, DB ist `uuid`
  - `50`: `event_type`
  - `entries.is_group_event` fehlt im Client-Modell, DB hat redundantes Feld

- [AddPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/AddPageViewModel.cs)
  - `82`, `101`, `116`: fire-and-forget Filter-Reloads
  - `180`: `LoadEntriesFromDatabaseAsync`
  - `187`: `Application.Current.MainPage = new LoginPage()`
  - `205`: `Rpc("get_entries_paged", ...)`
  - `272`: `async void OpenDetail`

- [MapPage.xaml.cs](C:/Data/NBH/MapPage.xaml.cs)
  - `52`: `LoadPinsFromDatabaseAsync`
  - `58`: Vollabfrage `From<EntryItem>().Get()`
  - `68`: clientseitiges Geocoding pro Entry
  - `124`: Pin-Append im UI-Flow
  - `152`, `219`: Such-Geocoding und `MoveToRegion`

- [DetailPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/DetailPageViewModel.cs)
  - `372-382`: Chat-Existenzpruefung in 2 Queries
  - `445`: `Guid.TryParse(SelectedEntry.user_id, ...)`
  - `754`: `Guid.Parse(SelectedEntry.user_id)`
  - `760`, `820`: inkonsistente `actionData`-Keys fuer Notifications
  - `926`: `get_user_rating_stats`
  - `979`, `985`: `DisplayActionSheet`

- [DetailPageViewModel.GroupEvents.cs](C:/Data/NBH/Resources/ViewModels/DetailPageViewModel.GroupEvents.cs)
  - `98-99`: `.Result.Models`
  - `103`: User-Fetch nach Rollen/Teilnehmern

### 4.5 Chat und Realtime

- [PublicChat.cs](C:/Data/NBH/Resources/Models/PublicChat.cs)
  - `17`: `UserId` noch `string`, DB ist `uuid`
  - `21`: `PartnerId` noch `string`, DB ist `uuid`

- [ChatViewPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/ChatViewPageViewModel.cs)
  - `88`: N+1-Aufruf `GetPartnerDataAsync`
  - `190`: Partnerdaten-Helfer
  - `203`: User-Fetch pro Chat

- [ChatPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/ChatPageViewModel.cs)
  - `148-149`: Service-Events werden registriert
  - `165`: `async void SubscribeToRealtimeMessages`
  - `463`, `514`: `Task.Run(async () => ...)` fuer DB-Laden
  - `507`: `LoadMoreMessagesAsync`
  - `599`: weiterer User-Fetch

- [PhoenixRealtimeService.cs](C:/Data/NBH/Services/PhoenixRealtimeService.cs)
  - `190`, `286`, `457`: Fire-and-forget-/Reconnect-Pfade
  - insgesamt Kandidat fuer Lifecycle-/State-Maschine statt ad hoc Reconnect

### 4.6 Reviews, Settings, UI

- [ChatReviewMopup.xaml.cs](C:/Data/NBH/Resources/Views/ChatReviewMopup.xaml.cs)
  - `45`: direkte `rest/v1`-BaseAddress
  - `181`: `PostAsync("chat_reviews", ...)`

- [EventRatingMopup.xaml.cs](C:/Data/NBH/Resources/Views/EventRatingMopup.xaml.cs)
  - `45`: direkte `rest/v1`-BaseAddress
  - `191`: `PostAsync("event_rating", ...)`

- [SettingsPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/SettingsPageViewModel.cs)
  - `196`: `DisplayAlert`

- [UserProfilePageViewModel.cs](C:/Data/NBH/Resources/ViewModels/UserProfilePageViewModel.cs)
  - `376`, `382`: `DisplayActionSheet`

- [NBH.csproj](C:/Data/NBH/NBH.csproj)
  - Assetliste pruefen gegen reale Referenzen
  - AppCenter entfernen
  - OneSignal, ImageSharp, AndroidX-Drift adressieren

## 5. Datenbankzielbild

### 5.1 Tabellenzielbild

Beibehalten und haerten:

- `users`
- `entries`
- `chats`
- `chat_messages`
- `chat_reviews`
- `event_rating`
- `notifications`
- `reports`
- `faq`
- `group_event_roles`
- `group_event_participants`
- `user_badges`

Neu einordnen:

- `group_chats`
- `group_chat_members`
- `group_chat_messages`

Auf Status pruefen:

- `entry_helpers`
- `entry_helper_signups`
- `entry_participants`

### 5.2 Redundanzen und Drift

Zu bereinigen:

- `entries.is_group_event` vs. `entries.event_type`
- `notifications.action_data` (`jsonb`) vs. Client-Stringmodell
- Client-Modelle mit `string`-User-IDs vs. DB-`uuid`
- Trigger-Duplikate
- Index-Duplikate / Ueberlappungen

## 6. Build-/Test-Gates pro Phase

Zwischen jeder Phase wird auf einem stabilen Zwischenstand gebaut und geprueft.

### 6.1 Client Build Gate

Pflicht:

```powershell
dotnet build C:\Data\NBH\NBH.csproj -f net10.0-android
```

Optional, wenn Plattformen verfuegbar:

```powershell
dotnet build C:\Data\NBH\NBH.csproj -f net10.0-ios
dotnet build C:\Data\NBH\NBH.csproj -f net10.0-windows10.0.19041.0
```

### 6.2 Laufzeit-Smoketests

Pflicht-Screens:

1. App-Start
2. Login
3. Profile
4. AddPage
5. DetailPage
6. ChatViewPage
7. ChatPage
8. Notifications Popup
9. CreateEventPage
10. FAQ

### 6.3 DB Gate

Nach jeder DB-Phase:

- alle Funktionen kompilieren
- `pg_policies` validieren
- `pg_indexes` validieren
- kritische CRUD-Tests mit authentifiziertem User
- Negativtests:
  - fremde Notifications lesen
  - fremde Chats lesen
  - fremde Entries manipulieren
  - unerlaubte Rollen/Teilnehmer manipulieren

## 7. Phasenplan

## Phase 0 - Baseline, Freeze, Sicherheits- und Wahrheitscheck

### Ziel

Produktionsnahe Ausgangsbasis einfrieren, Risiken sichtbar machen, vor dem Umbau keine ungeprueften Bewegungen mehr.

### Arbeiten

1. Git-Tag / Baseline-Snapshot setzen.
2. Vollstaendige DB-Struktur, Policies, Funktionen und Trigger archivieren.
3. Dokumentieren, welche Tabellen/Funktionen wirklich aktiv genutzt werden.
4. Secrets und externe Integrationen pruefen:
   - OneSignal
   - Supabase API Keys
   - REST-Fallbacks
5. Asset-Inventar gegen XAML-Referenzen pruefen.

### Betroffene Bereiche

- [NBH.csproj](C:/Data/NBH/NBH.csproj)
- [AppConfig.cs](C:/Data/NBH/AppConfig.cs)
- [ChatReviewMopup.xaml.cs](C:/Data/NBH/Resources/Views/ChatReviewMopup.xaml.cs)
- [EventRatingMopup.xaml.cs](C:/Data/NBH/Resources/Views/EventRatingMopup.xaml.cs)

### Gate

- Client-Build erfolgreich
- dokumentierter Baseline-Snapshot vorhanden

## Phase 1 - RLS- und Policy-Sanierung

### Ziel

Aus dem vorhandenen RLS-Set ein konsistentes, enges, performantes und verstehbares Sicherheitsmodell machen.

### Arbeiten

1. `chat_messages`:
   - `allow_select_realtime` entfernen oder durch enge membership-basierte Realtime-SELECT-Regel ersetzen.
   - nur Mitglieder des Chats duerfen lesen.

2. `entries`:
   - offene INSERT-Policies konsolidieren.
   - `Banned users cannot insert` nicht als zweite permissive Zusatzregel, sondern in die eigentliche erlaubende Policy integrieren.
   - breite UPDATE-Policy ersetzen.
   - Partner-/Accept-Flow in RPC verlagern.

3. `users`:
   - `select_users_for_auth USING (true)` durch Public-Profile-Sicht ersetzen.
   - Self-Full-Access explizit beibehalten.
   - private Felder nur fuer Self oder Admin.

4. `notifications`:
   - Insert fuer normale Clients schliessen.
   - Lesen/Updaten/Loeschen nur fuer eigene Notifications.
   - System-Inserts ueber Trigger/RPC/SECURITY DEFINER.

5. `group_event_roles`:
   - SELECT offen oder bewusst oeffentlich lassen.
   - INSERT/UPDATE/DELETE nur Owner.

6. `group_event_participants`:
   - sichtbar nur wenn fachlich gewollt; sonst auf Owner + betroffenen User einschränken.
   - Status-Updates nur Owner ueber definierte Funktion.

7. `storage`:
   - Bucket-spezifische Insert-/Read-Regeln
   - keine global breite Upload-Policy
   - Bucket-Creation nicht fuer normale Clients

### DB-Objekte

- `pg_policies` fuer:
  - `chat_messages`
  - `entries`
  - `users`
  - `notifications`
  - `group_event_roles`
  - `group_event_participants`
  - `storage.objects`
  - `storage.buckets`

### Gate

- SQL-Migrationsskript ausfuehrbar
- Positivtests fuer erlaubte CRUD-Faelle
- Negativtests fuer unberechtigte Zugriffe
- Client-Build erfolgreich

## Phase 2 - Schema-Konsolidierung

### Ziel

Schema-Drift und Altlasten abbauen, damit Client und DB wieder dieselbe Fachwahrheit haben.

### Arbeiten

1. `notifications.action_data`
   - Client auf JSON-Objektmodell heben
   - Keys normieren: `hasRatingButton`, `entry_id`, `event_id`, `chat_id`, `sender_id`
   - Altformate weiter lesbar machen

2. `entries`
   - `is_group_event` gegen `event_type` konsolidieren
   - `location default 'NULL'::text` entfernen
   - fachliche Checks fuer Datum/Enddatum ergaenzen

3. `users`
   - `is_completed` fachlich pruefen und ggf. entfernen
   - `subscription_id`-Semantik dokumentieren und harmonisieren

4. Trigger-Bereinigung
   - doppelten `auth.users`-Insert-Trigger entfernen
   - doppelten `update_last_message`-Trigger entfernen

5. Index-Bereinigung
   - ueberlappende `entry_helper_signups`-Indizes bereinigen
   - fehlende Policy-/Query-Indizes ergaenzen

### Betroffene Client-Dateien

- [NotificationModel.cs](C:/Data/NBH/Resources/Models/NotificationModel.cs)
- [NotificationService.cs](C:/Data/NBH/Services/NotificationService.cs)
- [NotificationListViewModel.cs](C:/Data/NBH/Resources/ViewModels/NotificationListViewModel.cs)
- [EntryItem.cs](C:/Data/NBH/Resources/Models/EntryItem.cs)
- [PublicChat.cs](C:/Data/NBH/Resources/Models/PublicChat.cs)

### Gate

- Migration erfolgreich
- DTO-/Model-Compile erfolgreich
- Notification-Smoketest erfolgreich

## Phase 3 - Backend-API-Schicht ueber RPCs/Views

### Ziel

Mehrtabellen- und Fachzustandslogik zentral in der DB abbilden, damit der Client nur noch konsumiert und schlank orchestriert.

### Arbeiten

1. Bestehende Funktionen produktiv nutzen:
   - `get_entries_paged`
   - `get_user_rating_stats`
   - `get_user_chats_v2`
   - `create_event_with_roles`

2. Neue/erweiterte Funktionen:
   - `get_entry_detail_bundle(entry_id uuid, viewer_id uuid)`
   - `get_user_profile_summary(user_id uuid)`
   - `mark_notifications_read(user_id uuid)`
   - `get_unread_notification_count(user_id uuid)`
   - `find_or_create_chat(user_a uuid, user_b uuid)`
   - `set_group_event_participant_status(...)`
   - optional `get_map_entries(bounds...)`

3. Review-/Notification-Writes kapseln:
   - `submit_chat_review(...)`
   - `submit_event_review(...)`
   - `create_notification(...)` nur systemisch

4. Public Views:
   - `public_user_profiles`
   - `v_user_rating_summary`
   - optional `v_chat_overview`

### Betroffene Client-Dateien

- [AddPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/AddPageViewModel.cs)
- [DetailPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/DetailPageViewModel.cs)
- [DetailPageViewModel.GroupEvents.cs](C:/Data/NBH/Resources/ViewModels/DetailPageViewModel.GroupEvents.cs)
- [ChatViewPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/ChatViewPageViewModel.cs)
- [NotificationService.cs](C:/Data/NBH/Services/NotificationService.cs)
- [ChatReviewMopup.xaml.cs](C:/Data/NBH/Resources/Views/ChatReviewMopup.xaml.cs)
- [EventRatingMopup.xaml.cs](C:/Data/NBH/Resources/Views/EventRatingMopup.xaml.cs)

### Gate

- neue RPCs testbar
- AddPage, DetailPage, ChatInbox, Notifications laufen gegen RPC/View-Pfade

## Phase 4 - Client-Datenzugriff und Service-Layer

### Ziel

DB-Zugriffe ueber klaren Service-Layer und DTOs statt direkt aus ViewModels und Popups.

### Arbeiten

1. Entities, DTOs und ViewState trennen.
2. Service-Layer aufbauen:
   - `EntryService`
   - `ChatService`
   - `ProfileService`
   - `ReviewService`
   - `NotificationQueryService`
3. Full-Object-Updates auf gezielte Patch-Operationen umstellen.
4. Popup-REST-Pfade entfernen.
5. `UserRatingService` auf RPC-only reduzieren.

### Betroffene Dateien

- [ChatReviewMopup.xaml.cs](C:/Data/NBH/Resources/Views/ChatReviewMopup.xaml.cs)
- [EventRatingMopup.xaml.cs](C:/Data/NBH/Resources/Views/EventRatingMopup.xaml.cs)
- [AddPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/AddPageViewModel.cs)
- [ChatViewPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/ChatViewPageViewModel.cs)
- [DetailPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/DetailPageViewModel.cs)
- [UserRatingService.cs](C:/Data/NBH/Services/UserRatingService.cs)

### Gate

- kein roher REST-Write-Pfad mehr
- keine Full-Object-Updates mehr auf Kernobjekten

## Phase 5 - Startup, Session und Navigation

### Ziel

Deterministischer App-Lifecycle ohne Race Conditions und ohne direkte MainPage-Sonderpfade.

### Arbeiten

1. `AppStartupService` einfuehren.
2. `Task.Run(async () => await authService.InitializeAsync())` entfernen.
3. `SupabaseSessionHandler` entblocken.
4. `MainPage = new SplashPage()`-Pfade ueber DI/Router ersetzen.
5. `Routes.cs`, `AppShell.xaml.cs`, `App.xaml.cs` auf eine einzige Routing-Wahrheit bringen.
6. Resume-/Timeout-/DeepLink-Pfad zentralisieren.

### Betroffene Dateien

- [App.xaml.cs](C:/Data/NBH/App.xaml.cs)
- [MauiProgram.cs](C:/Data/NBH/MauiProgram.cs)
- [SupabaseSessionHandler.cs](C:/Data/NBH/SupabaseSessionHandler.cs)
- [Routes.cs](C:/Data/NBH/Routes.cs)
- [AppShell.xaml.cs](C:/Data/NBH/AppShell.xaml.cs)
- [AddPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/AddPageViewModel.cs)

### Gate

- Start/Login/Resume/Logout/Deeplink-Smoketests gruen
- keine direkten `new LoginPage()`/`new SplashPage()`-Businesspfade mehr

## Phase 6 - Realtime, Push und Notification-Lifecycle

### Ziel

Stabile Push-/Realtime-Achse ohne doppelte Handler, ohne fragile Init-Reihenfolge und ohne JSON-Drift.

### Arbeiten

1. OneSignal-Init konsolidieren.
2. `OneSignal.User.Changed`-Lifecycle sauber machen.
3. `NotificationService` nur fuer lesen/synchronisieren und lokale Events zuständig machen.
4. `BellControl` event- und resource-sicher machen.
5. `PhoenixRealtimeService` als echte State-Machine haerten.
6. Realtime-Abmeldungen / Dispose-Pfade fuer Chat und Notifications definieren.

### Betroffene Dateien

- [App.xaml.cs](C:/Data/NBH/App.xaml.cs)
- [NotificationService.cs](C:/Data/NBH/Services/NotificationService.cs)
- [BellControl.xaml.cs](C:/Data/NBH/Resources/Controls/BellControl.xaml.cs)
- [LoginPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/LoginPageViewModel.cs)
- [ChatPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/ChatPageViewModel.cs)
- [PhoenixRealtimeService.cs](C:/Data/NBH/Services/PhoenixRealtimeService.cs)

### Gate

- Notification-Liste laedt stabil
- Bell-Badge korrekt
- Chat-Realtime doppelt nicht mehr
- Push-Navigation korrekt

## Phase 7 - Performance-Umbau

### Ziel

Die schwersten Hotspots gezielt abbauen.

### Arbeiten

1. Map:
   - `latitude/longitude` serverseitig persistieren
   - Geocoding aus dem Client entfernen
   - Bounding Box / Viewport Queries

2. Chat-Inbox:
   - `get_user_chats_v2` statt N+1

3. Detail/Gruppen-Event:
   - bundlebasierte Datenlieferung
   - `.Result`-Reststellen entfernen

4. Notifications:
   - Count + Batch-Mark-Read ueber RPC
   - Pagination / Limit

5. AddPage:
   - Filter-Reentry entkoppeln
   - echte `total_count`-Pagination

### Betroffene Dateien

- [MapPage.xaml.cs](C:/Data/NBH/MapPage.xaml.cs)
- [ChatViewPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/ChatViewPageViewModel.cs)
- [DetailPageViewModel.GroupEvents.cs](C:/Data/NBH/Resources/ViewModels/DetailPageViewModel.GroupEvents.cs)
- [NotificationListViewModel.cs](C:/Data/NBH/Resources/ViewModels/NotificationListViewModel.cs)
- [AddPageViewModel.cs](C:/Data/NBH/Resources/ViewModels/AddPageViewModel.cs)

### Gate

- merklich weniger Frame-Drops
- weniger Queries pro Screen
- Debug-Logs ohne aggressive Reload-/LoadMore-Spikes

## Phase 8 - Struktur, MAUI-10-Haertung, Tooling

### Ziel

Aus der gewachsenen Codebasis eine wartbare Produktbasis machen.

### Arbeiten

1. Feature-Struktur einfuehren:
   - `Features/Auth`
   - `Features/Profile`
   - `Features/Entries`
   - `Features/Chat`
   - `Features/Notifications`
   - `Features/Settings`
   - `Features/FAQ`
   - `Shared/...`

2. Compiled Bindings konsequent ausrollen.
3. Strikte XAML-Compilation aktivieren.
4. Asset-Inventar bereinigen.
5. AppCenter ablösen.
6. Buildwarnungen und Paketdrift aufraeumen.

### Betroffene Dateien

- [NBH.csproj](C:/Data/NBH/NBH.csproj)
- XAML-Dateien ohne/mit unvollstaendigen compiled bindings
- Struktur des gesamten Projekts

### Gate

- alle Buildziele gruen
- weniger Warnungen
- Build-/Analyse-Pipeline stabil

## 8. Reihenfolge fuer die Agentenbearbeitung

Exakt diese Reihenfolge verwenden:

1. RLS-/Policy-Modul
2. Schema-/Trigger-/Index-Modul
3. RPC-/View-Modul
4. Client-Service-/DTO-Modul
5. Startup-/Session-/Navigation-Modul
6. Realtime-/Push-/Notification-Modul
7. Performance-Modul
8. Struktur-/Cleanup-/Tooling-Modul

## 9. Nicht vorziehen

Diese Dinge erst nach Fundamentarbeit anfassen:

- groesseres Design-/UX-Redesign
- Group-Chat-Feature voll aktivieren
- Soft Delete / Audit / Geo Search / pg_cron
- groessere neue Produktfeatures

## 10. Akzeptanzkriterien fuer "produktionsreif"

Die App gilt erst dann als produktionsreif, wenn:

- RLS fachlich konsistent und negativgetestet ist
- keine rohen REST-Write-Pfade mehr existieren
- `notifications.action_data` robust als JSON gehandhabt wird
- Startup/Resume ohne direkte MainPage-Sonderpfade laeuft
- Map keine clientseitige Voll-Geocoding-Last mehr erzeugt
- Chat-Inbox ohne N+1 laeuft
- Push/Realtime ohne doppelte Eventeffekte stabil laeuft
- Buildwarnungen und kritische Paketdrifts bereinigt sind
- die App in klaren Phasen mit erfolgreichen Zwischen-Builds umgebaut wurde

## 11. Naechster praktischer Schritt

Der naechste operative Schritt nach diesem Dokument ist:

1. Phase 1 als konkrete SQL-/Policy-Migrationsarbeitspakete ausformulieren
2. Reihenfolge so definieren, dass keine offene Policy waehrend des Umbaus produktiv schlimmer wird
3. danach direkt mit dem ersten umsetzbaren Paket beginnen

Dieses Dokument ist bewusst so geschrieben, dass es im Agentenmodus Schritt fuer Schritt abgearbeitet werden kann.
