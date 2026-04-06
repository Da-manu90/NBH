# NBH App – Gruppen Events: Implementierungsplan

> **Erstellt:** 06.04.2026
> **App-Stack:** .NET MAUI 10 · Supabase (PostgreSQL + Realtime) · MVVM
> **Ziel:** Erweiterung der bestehenden 1:1-Event-Logik um Gruppen-Events mit Rollen, Kapazitätsbalken, Avatar-Stack und Genehmigungsflow

---

## Inhaltsverzeichnis

1. [Ist-Zustand Analyse](#1-ist-zustand-analyse)
2. [Design-Recherche Erkenntnisse](#2-design-recherche-erkenntnisse)
3. [Architektur-Überblick der Änderungen](#3-architektur-überblick-der-änderungen)
4. [Phase 1 – Datenbank & Datenmodelle](#phase-1--datenbank--datenmodelle)
5. [Phase 2 – AddPage Umbau (Liste + Erstellen trennen)](#phase-2--addpage-umbau-liste--erstellen-trennen)
6. [Phase 3 – Event-Erstellen Formular (Einzel + Gruppen)](#phase-3--event-erstellen-formular-einzel--gruppen)
7. [Phase 4 – DetailPage Gruppen-Event Erweiterung](#phase-4--detailpage-gruppen-event-erweiterung)
8. [Phase 5 – Approval-Flow & Benachrichtigungen](#phase-5--approval-flow--benachrichtigungen)
9. [Phase 6 – UI-Polish & Qualitätssicherung](#phase-6--ui-polish--qualitätssicherung)
10. [Datei-Änderungsmatrix](#datei-änderungsmatrix)

---

## 1. Ist-Zustand Analyse

### Aktuelle Architektur

Die App ist ein **.NET MAUI** Projekt (CareCommunity / NBH) mit folgenden Kern-Bausteinen:

| Schicht | Dateien |
|---------|---------|
| **Pages** | AddPage, DetailPage, ProfilePage, ChatPage, … |
| **ViewModels** | AddPageViewModel, DetailPageViewModel, … |
| **Models** | EntryItem, User, EventRating, NotificationModel, … |
| **Services** | AuthService, PhoenixRealtimeService, NotificationService, NavigationService |
| **Controls** | CustomTabBar, EntryCard, BellControl, StarRatingControl |
| **Backend** | Supabase PostgreSQL + Realtime WebSocket |

### Aktuelles EntryItem-Schema (Supabase Tabelle `entries`)

```
id                UUID         PK
title             TEXT
description       TEXT
category          TEXT         ("Anfrage" | "Angebot")
location          TEXT
created_date      TIMESTAMPTZ
user_id           UUID         FK → users.id
event_partner_id  UUID?        FK → users.id  (wer angenommen hat)
is_confirmed      BOOL         (Event bestätigt?)
auto_complete_at  TIMESTAMPTZ? (48h Timer)
is_completed      BOOL         (archiviert)
```

### Aktueller Event-Lifecycle (1:1)

```
Erstellen → Annehmen → Bestätigen → Bewerten → Abschließen
(AddPage)  (DetailPage) (Owner)    (Beide)    (auto/48h)
```

### Aktueller AddPage Aufbau

- **Oben:** Formular zum Erstellen (Titel, Beschreibung, Kategorie, Ort, Bild)
- **Unten:** Liste aller `EntryItem`-Einträge mit Paginierung (10 pro Seite)
- **ViewModel:** `AddPageViewModel.cs` verwaltet Entries + Pagination

### Aktuelle Probleme / Limitierungen

- 1:1-Event-Logik: nur ein `event_partner_id` Feld → kein Gruppen-Support
- AddPage vermischt Erstellen und Anzeigen in einer Ansicht
- Keine Rollen, keine Kapazitätsverwaltung, keine Gruppenbestätigung

---

## 2. Design-Recherche Erkenntnisse

### Analysierte Apps (Recherche-Dokument: `Event_UX_Design_Research.docx`)

| App | Stärke | Für NBH übernehmen |
|-----|--------|-------------------|
| **Nebenan.de** | Community-Kontext, einfaches 5-Feld-Formular, Nachbarschafts-Fokus | Formular-Einfachheit, Tonalität |
| **Meetup.com** | Rollensystem (Organizer/Helper), Warteliste, Kapazität | Rollen-Konzept, Approval-Flow |
| **Facebook Events** | Facepile Avatar-Stack, 3 RSVP-Zustände | Avatar-Stack Design |
| **Eventbrite** | Ticket-Types = Rollen-Buckets, Sold-Out-Handling | Kapazitäts-Buckets pro Rolle |
| **SignUpGenius** | Slot-basierte Rollen mit Auto-Schließen | Slot-Mechanik pro Rolle |

### Wichtigste UX-Erkenntnisse

1. **Trennung: Liste vs. Erstellen** → FAB-Button oder Top-Button leitet zur Erstellung weiter (kein Formular direkt auf der Liste)
2. **Formular-Einfachheit** → Einzel/Gruppen Unterscheidung via simplem Segment/Switch, nicht via Tab-Wechsel
3. **Rollen müssen frei benennbar sein** → Freitext + Kapazität pro Rolle, nicht vordefinierte Kategorien
4. **Progress Bar + Avatar Stack** → Kombiniert, nicht getrennt; Zahl + Bar + Profilbilder übereinander
5. **6 Teilnehmer-Zustände** → Nicht beigetreten / Angefragt / Bestätigt / Warteliste / Abgelehnt / Zurückgezogen
6. **Approval muss sichtbar sein** → Ersteller sieht Bewerbungsliste mit Annehmen/Ablehnen

---

## 3. Architektur-Überblick der Änderungen

```
┌─────────────────────────────────────────────────────────────────┐
│  NEUE NAVIGATION-FLOW                                           │
│                                                                 │
│  AddPage (Liste)                                                │
│    └── [Button "Event erstellen"] ──→ CreateEventPage (NEU)    │
│                                            ├── Einzelevent     │
│                                            └── Gruppenevent    │
│                                                  ├── Kapazität │
│                                                  └── Rollen    │
│                                                                 │
│  EntryCard (in Liste) ──→ DetailPage                           │
│                               ├── Einzel: wie bisher           │
│                               └── Gruppe: NEU                  │
│                                     ├── Progress Bar           │
│                                     ├── Avatar Stack           │
│                                     ├── Rolle bewerben         │
│                                     └── Allg. teilnehmen       │
└─────────────────────────────────────────────────────────────────┘

NEUE SUPABASE TABELLEN:
  group_event_roles        → Rollen pro Event
  group_event_participants → Teilnehmer/Bewerber-Zustand
```

---

## Phase 1 – Datenbank & Datenmodelle

**Ziel:** Datenbankschema erweitern und neue C#-Modelle anlegen, ohne bestehende Funktionalität zu berühren.

---

### 1.1 Supabase – `entries` Tabelle erweitern

Neue Spalten zur `entries`-Tabelle hinzufügen:

```sql
ALTER TABLE entries ADD COLUMN event_type        TEXT DEFAULT 'single';
ALTER TABLE entries ADD COLUMN max_participants   INTEGER;
ALTER TABLE entries ADD COLUMN event_date         TIMESTAMPTZ;
ALTER TABLE entries ADD COLUMN event_end_date     TIMESTAMPTZ;
ALTER TABLE entries ADD COLUMN event_image_url    TEXT;
```

| Spalte | Typ | Beschreibung |
|--------|-----|-------------|
| `event_type` | TEXT | `'single'` (Standard, 1:1) oder `'group'` |
| `max_participants` | INTEGER? | Max. allgemeine Teilnehmer (nur für Gruppen-Events) |
| `event_date` | TIMESTAMPTZ? | Start-Datum/-Zeit des Events |
| `event_end_date` | TIMESTAMPTZ? | End-Datum/-Zeit des Events |

**Betroffene Datei:** Kein C#-Code – nur Supabase SQL-Migration

---

### 1.2 Neue Supabase-Tabelle: `group_event_roles`

```sql
CREATE TABLE group_event_roles (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entry_id        UUID NOT NULL REFERENCES entries(id) ON DELETE CASCADE,
    role_name       TEXT NOT NULL,          -- z.B. "Helfer", "Aufbau"
    max_slots       INTEGER NOT NULL,       -- Max. Personen für diese Rolle
    created_at      TIMESTAMPTZ DEFAULT now()
);
```

**Beschreibung:** Eine Rolle ist ein benannter Slot mit maximaler Anzahl. Ein Gruppen-Event kann 0 bis N Rollen haben. Hat es keine Rollen, gibt es nur allgemeine Teilnehmer.

---

### 1.3 Neue Supabase-Tabelle: `group_event_participants`

```sql
CREATE TABLE group_event_participants (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entry_id        UUID NOT NULL REFERENCES entries(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id         UUID REFERENCES group_event_roles(id) ON DELETE SET NULL,
    -- NULL = allgemeiner Teilnehmer, UUID = Bewerber für spezifische Rolle
    status          TEXT NOT NULL DEFAULT 'pending',
    -- 'pending' | 'confirmed' | 'rejected' | 'withdrawn'
    applied_at      TIMESTAMPTZ DEFAULT now(),
    confirmed_at    TIMESTAMPTZ,
    UNIQUE(entry_id, user_id, role_id)
);
```

**Status-Maschine:**

```
[Bewerben] → pending
[Owner bestätigt] → confirmed
[Owner ablehnt] → rejected
[User zieht zurück] → withdrawn
```

---

### 1.4 Supabase Row-Level Security (RLS) Policies

```sql
-- group_event_roles: Jeder kann lesen, nur Ersteller kann schreiben
ALTER TABLE group_event_roles ENABLE ROW LEVEL SECURITY;
CREATE POLICY "Read roles" ON group_event_roles FOR SELECT USING (true);
CREATE POLICY "Owner manage roles" ON group_event_roles FOR ALL
    USING (EXISTS (SELECT 1 FROM entries WHERE id = entry_id AND user_id = auth.uid()));

-- group_event_participants: Eigene Rows + Owner des Events
ALTER TABLE group_event_participants ENABLE ROW LEVEL SECURITY;
CREATE POLICY "Read participants" ON group_event_participants FOR SELECT USING (true);
CREATE POLICY "Own participant row" ON group_event_participants FOR ALL
    USING (user_id = auth.uid() OR
           EXISTS (SELECT 1 FROM entries WHERE id = entry_id AND user_id = auth.uid()));
```

---

### 1.5 Neue C# Modellklassen

**Neue Datei:** `Resources/Models/GroupEventRole.cs`

```
Klasse: GroupEventRole
Tabelle: group_event_roles
Attribute:
  [PrimaryKey("id")] Guid Id
  [Column("entry_id")] Guid EntryId
  [Column("role_name")] string RoleName
  [Column("max_slots")] int MaxSlots
  [Column("created_at")] DateTime CreatedAt

Nicht-persistente Properties (berechnet):
  int ConfirmedCount    → Anzahl bestätigter Teilnehmer für diese Rolle
  int PendingCount      → Anzahl ausstehender Bewerbungen
  bool IsFull           → ConfirmedCount >= MaxSlots
  double FillPercentage → (double)ConfirmedCount / MaxSlots * 100
```

**Neue Datei:** `Resources/Models/GroupEventParticipant.cs`

```
Klasse: GroupEventParticipant
Tabelle: group_event_participants
Attribute:
  [PrimaryKey("id")] Guid Id
  [Column("entry_id")] Guid EntryId
  [Column("user_id")] Guid UserId
  [Column("role_id")] Guid? RoleId
  [Column("status")] string Status
  [Column("applied_at")] DateTime AppliedAt
  [Column("confirmed_at")] DateTime? ConfirmedAt

Nicht-persistente Properties (Join-Daten):
  string Username        → Aus users-Tabelle geladen
  string ProfileImageUrl → Aus users-Tabelle geladen
  string RoleName        → Aus GroupEventRole geladen
```

**Bestehende Datei ändern:** `Resources/Models/EntryItem.cs`

```
Neue Felder hinzufügen:
  [Column("event_type")] string EventType = "single"
  [Column("max_participants")] int? MaxParticipants
  [Column("event_date")] DateTime? EventDate
  [Column("event_end_date")] DateTime? EventEndDate

Nicht-persistente Listen (nach Laden befüllt):
  List<GroupEventRole> Roles
  List<GroupEventParticipant> Participants
  int ConfirmedParticipantCount
  double GeneralFillPercentage

Hilfseigenschaften:
  bool IsGroupEvent → EventType == "group"
  bool HasRoles     → Roles?.Count > 0
```

---

### 1.6 Notification-Typen erweitern

**Bestehende Datei:** `Resources/Models/NotificationModel.cs`

Neue `type`-Werte dokumentieren (keine Code-Änderung nötig, da `type` ein freier Text-String ist):

```
Bestehend:
  "event_accepted"    → Partner hat angenommen
  "event_rejected"    → Partner wurde abgelehnt
  "event_confirmed"   → Event wurde bestätigt

Neu hinzufügen:
  "group_apply"       → Jemand hat sich beworben (an Owner)
  "group_confirmed"   → Bewerbung bestätigt (an Bewerber)
  "group_rejected"    → Bewerbung abgelehnt (an Bewerber)
  "group_withdrawn"   → Bewerber hat Bewerbung zurückgezogen (an Owner)
```

---

## Phase 2 – AddPage Umbau (Liste + Erstellen trennen)

**Ziel:** AddPage zeigt nur noch die Entries-Liste. Ein neuer "Event erstellen"-Button führt zu einer separaten `CreateEventPage`.

---

### 2.1 Neue Page: `CreateEventPage.xaml` + `CreateEventPage.xaml.cs`

**Beschreibung:** Vollständig neue Seite für die Event-Erstellung. Ersetzt den oberen Formular-Bereich der aktuellen `AddPage`.

**UI-Struktur (von oben nach unten):**

```
┌─────────────────────────────────────────┐
│  ← Zurück        Event erstellen        │
├─────────────────────────────────────────┤
│  [Bild hinzufügen]  (Tap zum Auswählen) │
├─────────────────────────────────────────┤
│  Titel *                                │
│  ┌───────────────────────────────────┐  │
│  │ Kurzer prägnanter Titel...        │  │
│  └───────────────────────────────────┘  │
├─────────────────────────────────────────┤
│  Beschreibung *                         │
│  ┌───────────────────────────────────┐  │
│  │ Was wird gemacht? Wer wird        │  │
│  │ gebraucht?...                     │  │
│  └───────────────────────────────────┘  │
├─────────────────────────────────────────┤
│  Kategorie                              │
│  ┌──────────┐  ┌──────────┐            │
│  │ Anfrage  │  │  Angebot │            │
│  └──────────┘  └──────────┘            │
├─────────────────────────────────────────┤
│  Ort *                                  │
│  ┌───────────────────────────────────┐  │
│  │ Adresse oder Beschreibung...      │  │
│  └───────────────────────────────────┘  │
├─────────────────────────────────────────┤
│  Datum & Uhrzeit (optional)             │
│  [Von: Datum/Zeit] [Bis: Datum/Zeit]    │
├─────────────────────────────────────────┤
│  Event-Typ                              │
│  ┌──────────────┐  ┌────────────────┐   │
│  │ Einzel-Event │  │ Gruppen-Event  │   │
│  └──────────────┘  └────────────────┘   │
├─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
│  [Nur sichtbar wenn Gruppen-Event]      │
│                                         │
│  Maximale Teilnehmerzahl                │
│  ┌───────────────────────────────────┐  │
│  │  10                           ▲▼  │  │
│  └───────────────────────────────────┘  │
│                                         │
│  Rollen (optional)           [+ Rolle]  │
│  ┌────────────────────────────────────┐ │
│  │ Rolle: Helfer              Anz: 5  │ │
│  │                             [🗑️]   │ │
│  ├────────────────────────────────────┤ │
│  │ Rolle: Aufbau              Anz: 3  │ │
│  │                             [🗑️]   │ │
│  └────────────────────────────────────┘ │
├─────────────────────────────────────────┤
│                                         │
│  ┌─────────────────────────────────┐    │
│  │      Event veröffentlichen      │    │
│  └─────────────────────────────────┘    │
└─────────────────────────────────────────┘
```

---

### 2.2 AddPage.xaml – Formular-Bereich entfernen

**Betroffene Datei:** `AddPage.xaml`

**Änderungen:**
- Den gesamten oberen Grid/StackLayout mit dem Erstellungsformular entfernen
- Stattdessen einen "Event erstellen"-Button oben in der Page platzieren:

```
Neues Layout oben:
┌─────────────────────────────────────────┐
│              NBH Events                 │
│  [Alle] [Anfragen] [Angebote]           │
│                     [+ Event erstellen] │  ← Neuer Button
├─────────────────────────────────────────┤
│  EntryItem Liste (CollectionView)       │
│  ...                                    │
│  [← Zurück] Seite 1 von 5 [Weiter →]   │
└─────────────────────────────────────────┘
```

Der Button "Event erstellen" navigiert zu `CreateEventPage`.

---

### 2.3 AddPage.xaml.cs – Navigations-Handler

**Betroffene Datei:** `AddPage.xaml.cs`

**Änderung:** Event-Handler für den "Event erstellen"-Button hinzufügen, der `Shell.Current.GoToAsync(Routes.CreateEvent)` aufruft.

---

### 2.4 AddPageViewModel.cs – Erstell-Logik auslagern

**Betroffene Datei:** `Resources/ViewModels/AddPageViewModel.cs`

**Änderungen:**
- `AddEntryCommand` und alle zugehörigen Properties (Title, Description, Category, Location, ImageSource) aus dem ViewModel entfernen
- Diese Logik wandert in das neue `CreateEventPageViewModel`
- Die Properties für die Entry-Liste, Pagination und Filter bleiben unverändert

**Properties die bleiben:**
- `Entries`, `FilteredEntries`, `CurrentPage`, `TotalPages`
- `FilterCommand`, `NextPageCommand`, `PrevPageCommand`
- `OpenDetailCommand`, `RefreshCommand`, `CompleteEntryCommand`
- `LoadEntriesFromDatabaseAsync()`

**Properties die wandern:**
- `Title`, `Description`, `Category`, `Location`, `ImageSource`
- `AddEntryCommand`, `PickImageCommand`

---

### 2.5 Neues ViewModel: `CreateEventPageViewModel.cs`

**Neue Datei:** `Resources/ViewModels/CreateEventPageViewModel.cs`

**Properties:**
```
string Title
string Description
string Category           ("Anfrage" | "Angebot")
string Location
ImageSource ImageSource
DateTime? EventDate
DateTime? EventEndDate
string EventType          ("single" | "group")
bool IsGroupEvent         → EventType == "group"
int? MaxParticipants
ObservableCollection<RoleInputModel> Roles  → Eingabe-Rollen
```

**Commands:**
```
PickImageCommand          → Bild aus Galerie wählen (bestehende Logik übernehmen)
AddRoleCommand            → Neue Rolle zur Roles-Liste hinzufügen
RemoveRoleCommand         → Rolle aus der Liste entfernen
CreateEventCommand        → Validierung + Speichern in Supabase + Navigieren zurück
```

**Hilfklasse (inline oder separat):**
```
RoleInputModel:
  string RoleName
  int MaxSlots
```

**Speicher-Logik `CreateEventCommand`:**
1. Validierung (Titel und Location required)
2. Bild zu Supabase Storage hochladen (falls vorhanden)
3. `EntryItem` erstellen mit `event_type = EventType`
4. Falls Gruppen-Event: für jede Rolle einen `GroupEventRole`-Datensatz erstellen
5. Zurück zur AddPage navigieren mit `Shell.Current.GoToAsync("..")`
6. AddPage refresht automatisch via `RefreshCommand` oder Realtime

---

### 2.6 Routes.cs – Neue Route registrieren

**Betroffene Datei:** `Routes.cs`

**Änderung:** Konstante für neue Seite hinzufügen:
```csharp
public const string CreateEvent = "CreateEventPage";
```

---

### 2.7 AppShell.xaml – Route registrieren

**Betroffene Datei:** `AppShell.xaml`

**Änderung:** Route zur `CreateEventPage` als `ShellContent` oder via `Routing.RegisterRoute()` in `AppShell.xaml.cs` registrieren.

---

### 2.8 MauiProgram.cs – DI-Registrierung

**Betroffene Datei:** `MauiProgram.cs`

**Änderung:** `CreateEventPage` und `CreateEventPageViewModel` in den DI-Container registrieren:
```csharp
builder.Services.AddTransient<CreateEventPage>();
builder.Services.AddTransient<CreateEventPageViewModel>();
```

---

## Phase 3 – Event-Erstellen Formular (Einzel + Gruppen)

**Ziel:** Das Formular in `CreateEventPage` smart und intuitiv gestalten. Gruppen-Felder erscheinen nur wenn "Gruppen-Event" ausgewählt ist.

---

### 3.1 Segment Control für Event-Typ

**Betroffene Datei:** `CreateEventPage.xaml`

Die Auswahl "Einzel-Event" / "Gruppen-Event" wird mit zwei abgerundeten Buttons umgesetzt (ähnlich Nebenan.de / Meetup-Tonalität):

```xml
<!-- Pseudo-XAML-Struktur -->
<Grid ColumnDefinitions="*,*">
    <Button Text="Einzel-Event"
            Command="{Binding SetEventTypeCommand}"
            CommandParameter="single"
            Style="{Binding SingleEventButtonStyle}" />
    <Button Text="Gruppen-Event"
            Command="{Binding SetEventTypeCommand}"
            CommandParameter="group"
            Style="{Binding GroupEventButtonStyle}"
            Grid.Column="1" />
</Grid>
```

Der Button-Style wechselt zwischen "aktiv" (gefüllte Hintergrundfarbe) und "inaktiv" (Outline) je nach `EventType`.

---

### 3.2 Conditional Visibility für Gruppen-Felder

**Betroffene Datei:** `CreateEventPage.xaml`

```xml
<!-- Gruppen-Felder mit IsVisible Binding -->
<StackLayout IsVisible="{Binding IsGroupEvent}">
    <!-- Maximale Teilnehmer -->
    <!-- Rollen-Sektion -->
</StackLayout>
```

Die Felder werden weich ein-/ausgeblendet (AnimationExtensions oder IsVisible-Übergang). Kein hartes Erscheinen/Verschwinden.

---

### 3.3 Rollen-Eingabe (dynamische Liste)

**Betroffene Datei:** `CreateEventPage.xaml`

Rollen werden als `CollectionView` oder `BindableLayout` mit einem Template dargestellt:

```
Jede Rolle:
┌────────────────────────────────────────┐
│ [Rollenname eingeben...]    [5] [🗑️]  │
│ Textfeld für Name           ▲▼ Anzahl  │
└────────────────────────────────────────┘
```

- Rollenname: Freitext-Entry
- Anzahl: Stepper oder Entry (Zahl)
- Löschen: Trash-Icon-Button

**"+ Rolle hinzufügen"-Button** fügt eine neue leere `RoleInputModel` zur `Roles`-ObservableCollection hinzu.

---

### 3.4 Validierungs-Feedback

**Betroffene Datei:** `CreateEventPageViewModel.cs`

Validierungsregeln:
- `Title` muss ausgefüllt sein
- `Location` muss ausgefüllt sein
- Bei Gruppen-Event: `MaxParticipants` > 0 ODER mindestens eine Rolle mit Name + Slots > 0
- Rollen-Name darf nicht leer sein wenn Slot > 0

Fehleranzeige als dezente rote Hinweistexte unterhalb der Felder (kein Popup).

---

### 3.5 Bild-Upload Logik (aus AddPage übernehmen)

**Betroffene Datei:** `CreateEventPageViewModel.cs`

Die bestehende `PickImageCommand`-Logik aus `AddPageViewModel.cs` 1:1 übernehmen. Keine Neuentwicklung nötig – nur in das neue ViewModel verschieben.

---

## Phase 4 – DetailPage Gruppen-Event Erweiterung

**Ziel:** Die `DetailPage` erkennt ob ein Entry ein Gruppen-Event ist und zeigt entsprechend erweiterte UI-Elemente an.

---

### 4.1 Daten-Laden in DetailPageViewModel erweitern

**Betroffene Datei:** `Resources/ViewModels/DetailPageViewModel.cs`

**Neue Methode:** `LoadGroupEventDataAsync(Guid entryId)`

```
Lädt:
1. Alle Rollen: FROM group_event_roles WHERE entry_id = entryId
2. Alle Teilnehmer: FROM group_event_participants WHERE entry_id = entryId
3. Für jeden Teilnehmer: Username + ProfileImageUrl aus users-Tabelle
4. Status des aktuellen Users: Hat sich der aktuelle User bereits beworben?
```

**Neue Properties:**
```
List<GroupEventRole> EventRoles
List<GroupEventParticipant> ConfirmedParticipants
List<GroupEventParticipant> PendingApplicants    (nur für Owner sichtbar)
int TotalConfirmedCount
double GeneralFillPercentage
bool IsCurrentUserParticipant
string CurrentUserStatus      ("none" | "pending" | "confirmed" | "rejected")
bool CanApply                 → !IsCurrentUserParticipant && !IsFull
bool IsOwner                  → Entry.UserId == CurrentUserId
bool IsGroupEvent             → Entry.EventType == "group"
```

---

### 4.2 DetailPage.xaml – Gruppen-Event UI-Sektionen

**Betroffene Datei:** `DetailPage.xaml`

Alle neuen UI-Sektionen werden mit `IsVisible="{Binding IsGroupEvent}"` gesteuert. Einzel-Events sehen die bestehende UI unverändert.

#### 4.2.1 Kapazitäts-Übersicht (allgemeine Teilnehmer)

```
┌─────────────────────────────────────────┐
│  Teilnehmer                             │
│  ████████░░░░░░░░░░░  2 von 10         │  ← Progress Bar
│                                         │
│  [😊] [😊] [😊] +4 weitere            │  ← Avatar Stack
└─────────────────────────────────────────┘
```

**Progress Bar Spezifikation:**
- Breite: 100% der Card
- Höhe: 8dp (Pill-Form, border-radius = 4dp)
- Hintergrundfarbe: Grau (20% Opazität der Primärfarbe)
- Füllfarbe: Primärfarbe der App; bei ≥90% Füllung → Orange/Rot
- Label: "X von Y Teilnehmern" rechts oder darunter
- Keine Animation beim ersten Laden nötig, nur beim Aktualisieren

**Avatar Stack Spezifikation:**
- Größe: 32x32dp pro Avatar, circular (border-radius = 16dp)
- Überlappung: -8dp (benachbarte Avatare überlappen sich)
- Maximale Anzeige: 5 Avatare
- Überlauf: "+N" Label in gleichem Kreis-Format, Hintergrundfarbe Grau
- Rand: 2dp weißer Rand um jeden Avatar (hebt sich ab)
- Fallback: Initialen auf farbigem Hintergrund wenn kein Profilbild

#### 4.2.2 Rollen-Sektion

Für jede Rolle ein eigenes Karten-Element:

```
┌─────────────────────────────────────────┐
│  Helfer                      3 von 5   │
│  ███████████░░░░░░░░░░░░░░░░            │  ← Role Progress Bar
│  [😊] [😊] [😊]                        │  ← Bestätigte Teilnehmer
│                                         │
│  ┌─────────────────────────────────┐   │
│  │    Als Helfer bewerben          │   │  ← Apply-Button
│  └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

**Button-Zustände pro Rolle:**
- `none`: "Als [Rollenname] bewerben" (aktiver Button)
- `pending`: "Bewerbung ausstehend" (deaktiviert, anders gefärbt)
- `confirmed`: "Du bist [Rollenname]" (grün, kein Klick)
- `rejected`: "Abgelehnt" (grau)
- `full`: "Rolle besetzt" (deaktiviert)

#### 4.2.3 Allgemeiner Teilnehmen-Button (ohne Rolle)

Falls das Event Rollen hat, wird dieser Bereich nur angezeigt wenn `MaxParticipants` gesetzt ist:

```
┌─────────────────────────────────────────┐
│  Allgemein teilnehmen      2 von 10    │
│  ██░░░░░░░░░░░░░░░░░░░░░               │
│  [😊] [😊]                             │
│  ┌──────────────────────────────────┐  │
│  │    Als Teilnehmer bewerben       │  │
│  └──────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

#### 4.2.4 Owner-Ansicht: Bewerbungsliste

Nur sichtbar wenn `IsOwner == true` und `PendingApplicants.Count > 0`:

```
┌─────────────────────────────────────────┐
│  Bewerbungen (3)                        │
│  ┌─────────────────────────────────┐   │
│  │ 😊 MaxMuster   →  Helfer        │   │
│  │              [✓ Annehmen] [✗]   │   │
│  ├─────────────────────────────────┤   │
│  │ 😊 AnnaB       →  Aufbau        │   │
│  │              [✓ Annehmen] [✗]   │   │
│  └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

---

### 4.3 DetailPageViewModel.cs – Neue Commands

**Betroffene Datei:** `Resources/ViewModels/DetailPageViewModel.cs`

**Neue Commands:**

```
ApplyAsParticipantCommand(Guid? roleId)
  → Erstellt GroupEventParticipant mit status='pending', role_id=roleId
  → Schickt Benachrichtigung an Entry-Ersteller (type="group_apply")
  → Aktualisiert CurrentUserStatus lokal

ConfirmApplicantCommand(Guid participantId)
  → Setzt status='confirmed', confirmed_at=now()
  → Prüft ob MaxSlots/MaxParticipants überschritten
  → Schickt Benachrichtigung an Bewerber (type="group_confirmed")
  → Aktualisiert PendingApplicants + ConfirmedParticipants lokal

RejectApplicantCommand(Guid participantId)
  → Setzt status='rejected'
  → Schickt Benachrichtigung an Bewerber (type="group_rejected")
  → Entfernt aus PendingApplicants

WithdrawApplicationCommand()
  → Setzt eigene Bewerbung auf status='withdrawn'
  → Schickt Benachrichtigung an Owner (type="group_withdrawn")
  → Setzt CurrentUserStatus auf "none"
```

**Bestehende Commands bleiben unverändert** für Einzel-Events.

---

### 4.4 Realtime-Integration für Gruppen-Events

**Betroffene Datei:** `Services/PhoenixRealtimeService.cs`

**Neue Subscriptions:**
```
Tabelle: group_event_participants
Filter: entry_id=eq.[currentEntryId]
Events: INSERT, UPDATE, DELETE

Tabelle: group_event_roles
Filter: entry_id=eq.[currentEntryId]
Events: INSERT, UPDATE
```

Beim `PostgresChanged`-Event der DetailPage: `LoadGroupEventDataAsync()` erneut aufrufen → UI aktualisiert sich automatisch.

---

## Phase 5 – Approval-Flow & Benachrichtigungen

**Ziel:** Sicherstellen dass der komplette Approval-Kreislauf (Bewerben → Benachrichtigen → Bestätigen/Ablehnen → Benachrichtigen) funktioniert und in der NotificationService-Logik abgebildet ist.

---

### 5.1 NotificationService.cs – Neue Notification-Typen

**Betroffene Datei:** `Services/NotificationService.cs`

Die bestehende `AddNotificationAsync()`-Methode wird mit neuen `type`-Werten aufgerufen:

```
Trigger "group_apply":
  → Wenn User sich bewirbt
  → Empfänger: Entry.UserId (Owner)
  → Message: "{username} möchte an deinem Event teilnehmen"
    (oder "{username} möchte als {Rollenname} mithelfen" wenn Rolle)
  → action_data: {"entry_id": "...", "participant_id": "..."}

Trigger "group_confirmed":
  → Wenn Owner bestätigt
  → Empfänger: Bewerber UserId
  → Message: "Deine Teilnahme bei '{EventTitle}' wurde bestätigt!"

Trigger "group_rejected":
  → Wenn Owner ablehnt
  → Empfänger: Bewerber UserId
  → Message: "Deine Bewerbung bei '{EventTitle}' wurde nicht angenommen."

Trigger "group_withdrawn":
  → Wenn Bewerber zurückzieht
  → Empfänger: Entry.UserId (Owner)
  → Message: "{username} hat seine Teilnahme zurückgezogen."
```

---

### 5.2 App.xaml.cs – DeepLink für Gruppen-Notifications

**Betroffene Datei:** `App.xaml.cs`

**Bestehende DeepLink-Logik erweitern:**

Die bestehende `myapp://detailpage?entryId=...` Logik funktioniert für Gruppen-Events bereits, da die DetailPage das Entry lädt und dann den Gruppen-Modus erkennt.

Optionaler Zusatz: `myapp://detailpage?entryId=...&highlight=applicants` → Bewerbungs-Sektion direkt scrollen.

**Betroffene Datei:** `DetailPage.xaml.cs`

Parameter `highlight` auslesen und zur entsprechenden Sektion scrollen wenn gesetzt.

---

### 5.3 BellControl.xaml – Neue Notification-Icons

**Betroffene Datei:** `Resources/Controls/BellControl.xaml(.cs)`

Neue Notification-Typen in der Badge-Logik berücksichtigen (falls Typen gefiltert werden). Wahrscheinlich keine Änderung nötig wenn Badge nur "ungelesen gesamt" zählt.

**Betroffene Datei:** (optional) Notification-Liste UI

Falls eine Notification-List-Page existiert: Icons/Farben für neue Typen hinzufügen.

---

### 5.4 Kapazitäts-Schutz (Server-seitig absichern)

**Supabase – PostgreSQL Trigger oder RLS:**

Sicherstellen dass nicht mehr Teilnehmer bestätigt werden können als `max_slots` (für Rollen) oder `max_participants` (allgemein) erlaubt:

```sql
-- Beispiel: Trigger der verhindert dass max_slots überschritten wird
CREATE OR REPLACE FUNCTION check_role_capacity()
RETURNS TRIGGER AS $$
DECLARE
  current_count INT;
  max_count INT;
BEGIN
  IF NEW.status = 'confirmed' AND NEW.role_id IS NOT NULL THEN
    SELECT COUNT(*) INTO current_count
    FROM group_event_participants
    WHERE role_id = NEW.role_id AND status = 'confirmed';

    SELECT max_slots INTO max_count
    FROM group_event_roles WHERE id = NEW.role_id;

    IF current_count >= max_count THEN
      RAISE EXCEPTION 'Role capacity exceeded';
    END IF;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER enforce_role_capacity
  BEFORE UPDATE ON group_event_participants
  FOR EACH ROW EXECUTE FUNCTION check_role_capacity();
```

Dieses SQL wird in Supabase direkt ausgeführt (kein C#-Code).

---

## Phase 6 – UI-Polish & Qualitätssicherung

**Ziel:** Konsistentes visuelles Design, Edge Cases abdecken, Performance sicherstellen.

---

### 6.1 EntryCard.xaml – Gruppen-Event Badge

**Betroffene Datei:** `Resources/Controls/EntryCard.xaml(.cs)`

Für Gruppen-Events ein kleines Badge-Label hinzufügen:

```
┌─────────────────────────────────────────┐
│ [Bild]  Titel des Events         GRUPPE │  ← Badge "GRUPPE"
│         Ort · Datum                     │
│         2 von 10 Teilnehmern ████░░░░░  │  ← Mini-Bar in der Card
└─────────────────────────────────────────┘
```

**Änderungen:**
- Badge-Label mit `IsVisible="{Binding IsGroupEvent}"`
- Mini Progress Bar (schmaler, ~4dp) unter dem Beschreibungs-Text
- Mini Avatar Stack (3-4 Avatare, 24dp) in der Card

---

### 6.2 Colors.xaml / Styles.xaml – Neue Style-Definitionen

**Betroffene Datei:** `Resources/Styles/Colors.xaml`

Neue Farbkonstanten:

```xml
<Color x:Key="ProgressBarBackground">#1A000000</Color>  <!-- 10% schwarz -->
<Color x:Key="ProgressBarFill">...</Color>               <!-- Primärfarbe -->
<Color x:Key="ProgressBarWarning">#FF8C00</Color>        <!-- >80% -->
<Color x:Key="ProgressBarFull">#DC3545</Color>           <!-- 100% -->
<Color x:Key="AvatarBorder">White</Color>
<Color x:Key="StatusPending">#FFA500</Color>
<Color x:Key="StatusConfirmed">#28A745</Color>
<Color x:Key="StatusRejected">#6C757D</Color>
```

**Betroffene Datei:** `Resources/Styles/Styles.xaml`

Neue Style-Definitionen:

```xml
<Style x:Key="ProgressBarStyle" TargetType="ProgressBar">
    <!-- Höhe 8dp, gerundete Ecken via BoxView-Ansatz -->

<Style x:Key="RoleCardStyle" TargetType="Frame">
    <!-- Card mit Shadow, gerundete Ecken, Padding -->

<Style x:Key="ApplyButtonStyle" TargetType="Button">
    <!-- Outlined Button für "Bewerben" -->

<Style x:Key="ApplyButtonActiveStyle" TargetType="Button">
    <!-- Gefüllter Button wenn "Bestätigt" -->

<Style x:Key="EventTypeSelectorActive" TargetType="Button">
    <!-- Aktiver Segment-Button -->

<Style x:Key="EventTypeSelectorInactive" TargetType="Button">
    <!-- Inaktiver Segment-Button -->
```

---

### 6.3 Neues Control: `CapacityBarControl.xaml(.cs)`

**Neue Datei:** `Resources/Controls/CapacityBarControl.xaml`
**Neue Datei:** `Resources/Controls/CapacityBarControl.xaml.cs`

Wiederverwendbares Control das den Kapazitäts-Balken + Avatar-Stack kombiniert:

```
Bindable Properties:
  int CurrentCount
  int MaxCount
  ObservableCollection<GroupEventParticipant> Participants
  string Label         → z.B. "Teilnehmer" oder "Helfer"

Berechnet:
  double FillPercentage
  string CountLabel    → "X von Y"
  Color BarColor       → Je nach FillPercentage
  List<Participant> DisplayAvatars  → Erste 5
  int OverflowCount   → Alle weitere
```

Dieses Control wird in `DetailPage.xaml` und `EntryCard.xaml` eingebunden.

---

### 6.4 Neues Control: `AvatarStackControl.xaml(.cs)`

**Neue Datei:** `Resources/Controls/AvatarStackControl.xaml`
**Neue Datei:** `Resources/Controls/AvatarStackControl.xaml.cs`

```
Bindable Properties:
  List<string> ImageUrls     → Profilbild-URLs
  List<string> Initials      → Fallback wenn kein Bild
  int MaxVisible             → Default: 5
  int AvatarSize             → Default: 32

Verhalten:
  → Rendert max. MaxVisible Kreise mit Überlappung (-8dp)
  → Falls mehr als MaxVisible: Letzter Kreis zeigt "+N"
  → FFImageLoading für effizientes Bild-Laden mit Caching
```

---

### 6.5 Performance-Optimierungen

**Betroffene Datei:** `Resources/ViewModels/DetailPageViewModel.cs`

- Gruppen-Event-Daten parallel laden (Task.WhenAll für Rollen + Teilnehmer)
- Avatar-Bilder werden gecacht (FFImageLoading übernimmt das automatisch)
- Realtime-Updates nur für die aktuell geöffnete `entry_id` subscriben

**Betroffene Datei:** `Resources/ViewModels/AddPageViewModel.cs`

- `group_event_roles` und `group_event_participants` werden in der Liste **nicht** vollständig geladen
- Nur `event_type` und ein `participant_count` (aggregierter Wert) werden in der EntryCard angezeigt
- Für `participant_count` kann eine PostgreSQL View oder Supabase Function genutzt werden:

```sql
CREATE OR REPLACE VIEW entries_with_counts AS
SELECT
    e.*,
    COALESCE(p.confirmed_count, 0) as confirmed_participant_count
FROM entries e
LEFT JOIN (
    SELECT entry_id, COUNT(*) as confirmed_count
    FROM group_event_participants
    WHERE status = 'confirmed'
    GROUP BY entry_id
) p ON e.id = p.entry_id;
```

---

### 6.6 Edge Cases & Fehlerbehandlung

| Situation | Verhalten |
|-----------|-----------|
| Event bereits voll wenn User bestätigt wird | Fehlermeldung "Kapazität überschritten", Button wird deaktiviert |
| User bewirbt sich mehrfach (Race Condition) | UNIQUE constraint in DB fängt das ab, UI zeigt Toast-Fehler |
| Event-Ersteller verlässt die App während Bewerbung pending | Bewerbung bleibt bis Ersteller reagiert |
| Bewerbung zurückziehen nach Bestätigung | Wird wie ein neues "Withdrawn" behandelt, Slot wird wieder frei |
| Gruppen-Event ohne Rollen UND ohne MaxParticipants | Validierung verhindert das beim Erstellen |
| 0 Rollen mit MaxParticipants=0 | Nicht erlaubt, Fehlermeldung beim Erstellen |

---

## Datei-Änderungsmatrix

| Datei | Aktion | Phase | Art der Änderung |
|-------|--------|-------|-----------------|
| **Supabase SQL** | Ändern | 1 | ALTER TABLE entries + 2 neue Tabellen + Trigger |
| `Resources/Models/EntryItem.cs` | Erweitern | 1 | 5 neue Felder + berechnete Properties |
| `Resources/Models/GroupEventRole.cs` | Neu | 1 | Komplett neue Datei |
| `Resources/Models/GroupEventParticipant.cs` | Neu | 1 | Komplett neue Datei |
| `Resources/Models/NotificationModel.cs` | Dokumentation | 1 | Neue type-Strings dokumentieren |
| `AddPage.xaml` | Umbau | 2 | Formular entfernen, Liste + Button behalten |
| `AddPage.xaml.cs` | Umbau | 2 | CreateEvent-Navigation hinzufügen |
| `Resources/ViewModels/AddPageViewModel.cs` | Reduzieren | 2 | Erstell-Logik auslagern |
| `CreateEventPage.xaml` | Neu | 2+3 | Komplett neue Seite |
| `CreateEventPage.xaml.cs` | Neu | 2+3 | Code-Behind der neuen Seite |
| `Resources/ViewModels/CreateEventPageViewModel.cs` | Neu | 2+3 | Komplett neues ViewModel |
| `Routes.cs` | Erweitern | 2 | Neue Route-Konstante |
| `AppShell.xaml(.cs)` | Erweitern | 2 | Route registrieren |
| `MauiProgram.cs` | Erweitern | 2 | DI-Registrierung |
| `Resources/ViewModels/DetailPageViewModel.cs` | Erweitern | 4 | Neue Properties + 4 neue Commands |
| `DetailPage.xaml` | Erweitern | 4 | 4 neue UI-Sektionen (Gruppen-Modus) |
| `DetailPage.xaml.cs` | Erweitern | 4+5 | highlight-Parameter + Scroll-Logik |
| `Services/PhoenixRealtimeService.cs` | Erweitern | 4 | 2 neue Realtime-Subscriptions |
| `Services/NotificationService.cs` | Erweitern | 5 | 4 neue Notification-Trigger |
| `App.xaml.cs` | Erweitern | 5 | DeepLink-Parameter erweitern |
| `Resources/Controls/EntryCard.xaml(.cs)` | Erweitern | 6 | Gruppen-Badge + Mini-Bar |
| `Resources/Styles/Colors.xaml` | Erweitern | 6 | ~8 neue Farb-Konstanten |
| `Resources/Styles/Styles.xaml` | Erweitern | 6 | ~6 neue Style-Definitionen |
| `Resources/Controls/CapacityBarControl.xaml(.cs)` | Neu | 6 | Wiederverwendbares Control |
| `Resources/Controls/AvatarStackControl.xaml(.cs)` | Neu | 6 | Wiederverwendbares Control |

**Summe:**
- 4 Dateien komplett neu (Models)
- 4 Dateien komplett neu (Pages/ViewModels)
- 2 Dateien komplett neu (Controls)
- 2 Dateien stark umgebaut (AddPage, AddPageViewModel)
- 6 Dateien erweitert (Detail, Services, App, Shell)
- 4 Dateien leicht erweitert (Routes, MauiProgram, Styles, Colors)
- 1 Supabase SQL-Migration (kein C#)

---

## Phasen-Zusammenfassung

| Phase | Kern-Deliverables | Abhängigkeiten |
|-------|-------------------|----------------|
| **Phase 1** | DB-Schema + neue C# Modelle | Keine |
| **Phase 2** | AddPage → Liste + CreateEventPage | Phase 1 |
| **Phase 3** | Formular mit Einzel/Gruppen-Logik | Phase 2 |
| **Phase 4** | DetailPage Gruppen-Ansicht | Phase 1, 3 |
| **Phase 5** | Notifications + Approval-Flow | Phase 4 |
| **Phase 6** | UI-Polish + Controls + Performance | Phase 1–5 |

Jede Phase ist in sich abgeschlossen und testbar. Phase 1–3 und Phase 4–5 können nach Phase 1 parallel bearbeitet werden.

---

*Dokument basierend auf Analyse des NBH-Projekts (Stand 06.04.2026) und Design-Recherche (Nebenan.de, Meetup, Facebook Events, Eventbrite, SignUpGenius).*
