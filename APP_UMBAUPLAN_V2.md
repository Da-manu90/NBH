# NBH App – Umbauplan V2: Design, UX & Feature-Erweiterungen

> **Erstellt:** 06.04.2026
> **Basis:** Intensiv-Analyse aller ~105 Quell-Dateien
> **Methode:** 6 aufeinanderfolgende Phasen mit Build-Test-Checkpoint zwischen jeder Phase
> **Wichtig:** Keine Datei wird direkt umgebaut – dieser Plan beschreibt ausschließlich WAS und WIE

---

## Inhaltsverzeichnis

- [Ist-Zustand Zusammenfassung](#ist-zustand-zusammenfassung)
- [Neue Dateien (Übersicht)](#neue-dateien-übersicht)
- [Phase 1 – Globale Styles & Hintergrund-Vereinheitlichung + Typografie](#phase-1--globale-styles--hintergrund-vereinheitlichung--typografie)
- [Phase 2 – Frame→Border Migration & Star Rating Fix](#phase-2--frameborder-migration--star-rating-fix)
- [Phase 3 – MapPage Redesign, Empty States & Performance](#phase-3--mappage-redesign-empty-states--performance)
- [Phase 4 – Settings Page, In-App Feedback, Dark Mode](#phase-4--settings-page-in-app-feedback-dark-mode)
- [Phase 5 – Sicherheit, Vertrauen & Moderations-System](#phase-5--sicherheit-vertrauen--moderations-system)
- [Phase 6 – DSGVO, Account-Management, Haptic & Final Polish](#phase-6--dsgvo-account-management-haptic--final-polish)
- [Datei-Änderungsmatrix (Gesamt)](#datei-änderungsmatrix-gesamt)
- [Supabase SQL-Migrationen (Übersicht)](#supabase-sql-migrationen-übersicht)

---

## Ist-Zustand Zusammenfassung

### Was gut ist und bleibt
- Konsistentes Mint/Teal-Farbschema (`#00897b` als Primärfarbe, `#00d2a8` als Akzent)
- Modernes Card-Design mit `CornerRadius="20"` als Standard
- Animierter Custom-TabBar mit Glow-Effekten
- Gut strukturierte MVVM-Architektur mit DI-Container
- Phoenix Realtime WebSocket funktioniert
- Haptic Feedback ist teilweise vorhanden

### Aktuelle Probleme (analysiert)

| Problem | Betroffene Dateien | Schwere |
|---------|-------------------|---------|
| Jede Page definiert eigene Hintergrund-Gradienten inline | Alle 10 Page-XAML-Dateien | Mittel |
| FontSizes hardcoded (12, 13, 15, 16, 18, 20, 22, 26px) | Alle XAML-Dateien | Niedrig |
| Star-Rating auf 3 Seiten handgemacht statt StarRatingControl | ProfilePage, UserProfilePage, RatingPage | Hoch |
| Frame-Elemente statt Border in visuellen Containern | RegisterPage, RatingPage, BellControl | Niedrig |
| MapPage: schwarzes Suchfeld, kein Titel, keine Navigation | MapPage.xaml | Mittel |
| Keine Settings-Seite | – (fehlt komplett) | Hoch |
| Kein In-App Feedback | – (fehlt komplett) | Mittel |
| Kein Dark Mode (AppThemeBinding unvollständig) | Colors.xaml, alle XAML | Hoch |
| Kein Verified-Badge System | User.cs, ProfilePage | Mittel |
| Kein Report/Melde-System | – (fehlt komplett) | Hoch |
| Keine Account-Löschung | – (fehlt komplett) | Kritisch (DSGVO) |
| Kein is_banned Feld | users-Tabelle, AuthService | Kritisch |
| Haptic Feedback unvollständig | Mehrere Pages | Niedrig |
| Empty States fehlen in Listen | AddPage, ChatViewPage | Mittel |
| Bildkomprimierung nicht bei allen Uploads | ProfilePageViewModel | Mittel |
| Kein AGB-Akzeptanz Timestamp | RegisterPage, users-Tabelle | Kritisch (DSGVO) |
| Bewertungskommentare nicht sichtbar | RatingPage, DetailPage | Mittel |

---

## Neue Dateien (Übersicht)

| Datei | Typ | Phase |
|-------|-----|-------|
| `Pages/SettingsPage.xaml` | XAML Page | 4 |
| `Pages/SettingsPage.xaml.cs` | Code-Behind | 4 |
| `Resources/ViewModels/SettingsPageViewModel.cs` | ViewModel | 4 |
| `Resources/Controls/EmptyStateView.xaml` | ContentView Control | 3 |
| `Resources/Controls/EmptyStateView.xaml.cs` | Code-Behind | 3 |
| `Resources/Controls/VerifiedBadge.xaml` | ContentView Control | 5 |
| `Resources/Controls/VerifiedBadge.xaml.cs` | Code-Behind | 5 |
| `Resources/Models/Report.cs` | Datenmodell | 5 |
| `Services/FeedbackService.cs` | Service | 4 |
| `Services/ModerationService.cs` | Service | 5 |

---

## Phase 1 – Globale Styles & Hintergrund-Vereinheitlichung + Typografie

**Ziel:** Einheitliches visuelles Fundament schaffen. Alle Seiten teilen denselben Hintergrund. Typografie-Skala ist definiert und wird konsequent genutzt.
**Risiko:** Niedrig – nur visuelle Änderungen, keine Logik
**Geschätzte Dateien:** 14

---

### 1.1 `Resources/Styles/Colors.xaml` – Neue Design-Tokens

**Was hinzugefügt wird:**

```xml
<!-- Hintergrund-Tokens (Light / Dark) -->
<Color x:Key="PageBackgroundStart">#b7f0e9</Color>
<Color x:Key="PageBackgroundMid">#15be9e</Color>
<Color x:Key="PageBackgroundEnd">#b7f0e9</Color>

<!-- Dark Mode Gegenstücke -->
<Color x:Key="PageBackgroundStartDark">#0d2e2a</Color>
<Color x:Key="PageBackgroundMidDark">#0a4a3d</Color>
<Color x:Key="PageBackgroundEndDark">#0d2e2a</Color>

<!-- Surface Colors (Karten, Inputs) -->
<Color x:Key="SurfaceLight">#FFFFFF</Color>
<Color x:Key="SurfaceDark">#1E2A28</Color>
<Color x:Key="SurfaceElevatedLight">#F8FFFE</Color>
<Color x:Key="SurfaceElevatedDark">#243330</Color>

<!-- Text Colors -->
<Color x:Key="TextPrimaryLight">#1A1A1A</Color>
<Color x:Key="TextPrimaryDark">#F0F0F0</Color>
<Color x:Key="TextSecondaryLight">#666666</Color>
<Color x:Key="TextSecondaryDark">#A0A0A0</Color>
<Color x:Key="TextMutedLight">#999999</Color>
<Color x:Key="TextMutedDark">#707070</Color>

<!-- Semantische Farben (bereits vorhanden, nur dokumentieren) -->
<!-- ErrorColor: #E53935 | SuccessColor: #43A047 | WarningColor: #FB8C00 -->
```

**Warum:** Anstatt Farben direkt in XAML-Dateien hardcoden, werden alle Werte über benannte Tokens gesteuert. Das macht Dark-Mode-Implementierung in Phase 4 möglich ohne alle Dateien wieder anfassen zu müssen.

---

### 1.2 `Resources/Styles/Styles.xaml` – Globaler Seiten-Stil & Typografie-Erweiterung

**Abschnitt A: Globaler ContentPage-Style**

```xml
<Style TargetType="ContentPage" ApplyToDerivedTypes="True">
    <Setter Property="Background">
        <Setter.Value>
            <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                <GradientStop Color="{StaticResource PageBackgroundStart}" Offset="0.0"/>
                <GradientStop Color="{StaticResource PageBackgroundMid}"   Offset="0.5"/>
                <GradientStop Color="{StaticResource PageBackgroundEnd}"   Offset="1.0"/>
            </LinearGradientBrush>
        </Setter.Value>
    </Setter>
</Style>
```

**Wichtig:** `ApplyToDerivedTypes="True"` sorgt dafür dass alle ContentPage-Ableitungen automatisch diesen Hintergrund erhalten. Seiten die einen anderen Hintergrund brauchen (z.B. LoginPage weiß) können ihn lokal überschreiben.

**Abschnitt B: Vollständige Typografie-Skala**

Bereits vorhanden in `Styles_Polished.xaml`:
- `HeadingStyle` (22px, Bold)
- `SubheadingStyle` (14px)
- `FieldLabelStyle` (13px, Bold, Grau)

Neu hinzufügen in `Styles.xaml`:

```xml
<!-- DisplayStyle: Große Hero-Texte (z.B. Login-Titel "Willkommen zurück!") -->
<Style x:Key="DisplayStyle" TargetType="Label">
    <Setter Property="FontSize" Value="26"/>
    <Setter Property="FontAttributes" Value="Bold"/>
    <Setter Property="LineHeight" Value="1.2"/>
</Style>

<!-- TitleLargeStyle: Seiten-Titel (z.B. "Chats", "NBH Events") -->
<Style x:Key="TitleLargeStyle" TargetType="Label">
    <Setter Property="FontSize" Value="22"/>
    <Setter Property="FontAttributes" Value="Bold"/>
</Style>

<!-- TitleStyle: Karten-Titel, Abschnitte -->
<Style x:Key="TitleStyle" TargetType="Label">
    <Setter Property="FontSize" Value="18"/>
    <Setter Property="FontAttributes" Value="Bold"/>
</Style>

<!-- BodyLargeStyle: Wichtige Body-Texte -->
<Style x:Key="BodyLargeStyle" TargetType="Label">
    <Setter Property="FontSize" Value="16"/>
</Style>

<!-- BodyStyle: Standard-Fließtext (bereits als Default 14px) -->
<!-- BodySmallStyle: Sekundäre Infos -->
<Style x:Key="BodySmallStyle" TargetType="Label">
    <Setter Property="FontSize" Value="13"/>
</Style>

<!-- CaptionStyle: Zeitstempel, Metadaten, Ortsangaben -->
<Style x:Key="CaptionStyle" TargetType="Label">
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="TextColor" Value="{StaticResource TextMutedLight}"/>
</Style>

<!-- BadgeStyle: Chips, Tags -->
<Style x:Key="BadgeStyle" TargetType="Label">
    <Setter Property="FontSize" Value="11"/>
    <Setter Property="FontAttributes" Value="Bold"/>
</Style>
```

---

### 1.3 Individuelle Hintergrund-Gradienten entfernen

**Betrifft alle folgenden XAML-Dateien** – jeweils nur den `Background`-Block des `ContentPage`-Elements entfernen:

#### `Pages/ProfilePage.xaml`
**Was entfernt wird** (ca. Zeilen 12–17):
```xml
<!-- ENTFERNEN: -->
<ContentPage.Background>
    <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
        <GradientStop Color="#b7f0e9" Offset="0.0"/>
        <GradientStop Color="#15be9e" Offset="0.5"/>
        <GradientStop Color="#b7f0e9" Offset="1.0"/>
    </LinearGradientBrush>
</ContentPage.Background>
```
**Was bleibt:** Alles andere. Der globale Style aus Styles.xaml übernimmt automatisch.

#### `Pages/AddPage.xaml`
Identischer Gradient (`#b7f0e9 → #15be9e`) – wird genauso entfernt.

#### `Pages/DetailPage.xaml`
Identischer Gradient – wird entfernt.

#### `Pages/UserProfilePage.xaml`
Identischer Gradient (Zeilen 13–18) – wird entfernt.

#### `Pages/LoginPage.xaml`
**Sonderfall:** LoginPage hat ebenfalls den Mint-Gradient. Hier wird der Background-Block entfernt – der globale Style greift automatisch. ✓ Kein Problem, gleiches Design.

#### `Pages/RegisterPage.xaml`
**Sonderfall:** RegisterPage hat einen anderen Gradient (`White → #6bffea`). Da die RegisterPage inhaltlich zur Login-Welt gehört, wird dieser ebenfalls durch den globalen Gradient ersetzt. Der Kontrast zu LoginPage verschwindet, was Einheit schafft.

#### `Pages/ChatViewPage.xaml`
Gradient `#52c8da → white → #52c8da` – entfernen, globaler Style greift.

#### `Pages/ChatPage.xaml`
Hat eigenen Gradient – entfernen.

#### `Pages/FAQPage.xaml`
Gradient `#52d8ff → white` – entfernen. (FAQPage Header-Border behält seinen eigenen Gradient, nur der Seiten-Hintergrund wird vereinheitlicht.)

#### `Pages/MapPage.xaml`
MapPage hat die Map als Vollbild-Hintergrund. Hier ist kein ContentPage-Background nötig – wenn vorhanden, entfernen, ansonsten nichts tun.

---

### 1.4 Typografie anwenden

**`Pages/LoginPage.xaml`**
| Aktuell | Ersetzen durch |
|---------|---------------|
| `FontSize="26"` (Willkommen-Titel) | `Style="{StaticResource DisplayStyle}"` |
| `FontSize="14"` (Untertitel) | `Style="{StaticResource SubheadingStyle}"` |

**`Pages/RegisterPage.xaml`**
| Aktuell | Ersetzen durch |
|---------|---------------|
| Verschiedene FontSizes auf Labels | Entsprechende Style-Keys aus Tabelle |

**`Pages/ProfilePage.xaml`**
| Aktuell | Ersetzen durch |
|---------|---------------|
| `FontSize="20"` (Username) | `Style="{StaticResource TitleLargeStyle}"` |
| `FontSize="13"` (Karten-Titel) | `Style="{StaticResource FieldLabelStyle}"` |
| `FontSize="12"` (Rating-Count) | `Style="{StaticResource CaptionStyle}"` |

**`Pages/UserProfilePage.xaml`**
| Aktuell | Ersetzen durch |
|---------|---------------|
| `FontSize="20"` (Username) | `Style="{StaticResource TitleLargeStyle}"` |
| `FontSize="12"` (Sektion-Header) | `Style="{StaticResource CaptionStyle}"` |
| `FontSize="15"` (Inhaltstext) | `Style="{StaticResource BodyStyle}"` (default 14px) |

**`Pages/ChatViewPage.xaml`**
| Aktuell | Ersetzen durch |
|---------|---------------|
| `FontSize="30"` (Chats-Titel) | `Style="{StaticResource TitleLargeStyle}"` + ggf. manuell 28px |

**`Pages/RatingPage.xaml`**
| Aktuell | Ersetzen durch |
|---------|---------------|
| `FontSize="20"` (Sektion-Header) | `Style="{StaticResource TitleStyle}"` |
| `FontSize="18"` (Bewertungs-Labels) | `Style="{StaticResource BodyLargeStyle}"` |
| `FontSize="18"` (Username) | `Style="{StaticResource TitleStyle}"` |

**`Pages/AddPage.xaml`**
| Aktuell | Ersetzen durch |
|---------|---------------|
| `FontSize="Large"` (NBH Events-Titel) | `Style="{StaticResource TitleLargeStyle}"` |

---

### ✅ Phase 1 Build-Test Checkpoint

**Was zu prüfen ist:**
1. App kompiliert ohne Fehler
2. Alle Seiten zeigen denselben Mint/Teal-Gradient-Hintergrund
3. Keine Seite ist komplett weiß oder transparent
4. Schriftgrößen sind konsistent und nichts ist zu groß/zu klein
5. LoginPage und RegisterPage sehen korrekt aus

**Häufige Fehler:**
- `StaticResource not found`: Sicherstellen dass Colors.xaml **vor** Styles.xaml in App.xaml geladen wird
- Seite überschreibt den globalen Style lokal: Prüfen ob noch lokale `Background`-Definitionen übrig sind
- Typografie-Key falsch geschrieben: Case-sensitive prüfen

---

## Phase 2 – Frame→Border Migration & Star Rating Fix

**Ziel:** Modernere API für visuelle Container nutzen. Fehleranfällige handgemachte Stern-Implementierungen durch das vorhandene `StarRatingControl` ersetzen.
**Risiko:** Mittel – Star Rating betrifft Code-Behind-Logik
**Geschätzte Dateien:** 8

---

### 2.1 Hybrid-Strategie: Frame vs. Border – Entscheidungsmatrix

**Inventar aller Frame-Elemente (aus Analyse):**

| Datei | Element-Name | Hat TapGesture? | Aktion |
|-------|-------------|----------------|--------|
| `BellControl.xaml` | `NotificationBadgeFrame` | Nein | → **Border** |
| `ProfilePage.xaml` | `SaveProfileFrame` (Zeile 289) | **JA** (`OnSaveProfileTapped`) | ✋ Frame **behalten** |
| `ProfilePage.xaml` | `VerifyQualificationsFrame` (Zeile 343) | **JA** (`OnVerifyQualificationsTapped`) | ✋ Frame **behalten** |
| `ProfilePage.xaml` | ProgressBar-Frame (Zeile 418) | Nein | → **Border** |
| `RatingPage.xaml` | Chat-Rating-Frame (Zeile 73) | Nein | → **Border** |
| `RatingPage.xaml` | Event-Rating-Frame (Zeile 265) | Nein | → **Border** |
| `RatingPage.xaml` | Comment-Item-Frame (Zeile 454) | Nein | → **Border** |
| `RegisterPage.xaml` | Input-Feld-Frames (CornerRadius="25") | Nein | → **Border** |
| `App.xaml` | `ButtonFrameStyle` | Als Button-Wrapper | Überprüfen bei Verwendung |

**Daumenregel für die Umsetzung:**
```
Frame hat OnXxxTapped oder TapGestureRecognizer → BEHALTEN
Frame ist nur visuelle Hülle → ERSETZEN durch Border
```

---

### 2.2 `Resources/Controls/BellControl.xaml` – Badge-Frame konvertieren

**Aktuell (Zeile 76):**
```xml
<Frame x:Name="NotificationBadgeFrame"
       BackgroundColor="Red"
       CornerRadius="10"
       Padding="3"
       IsVisible="False"
       ...>
    <Label x:Name="BadgeLabel" FontSize="12" .../>
</Frame>
```

**Neu:**
```xml
<Border x:Name="NotificationBadgeFrame"
        BackgroundColor="Red"
        IsVisible="False"
        ...>
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="10"/>
    </Border.StrokeShape>
    <Border.Padding>
        <Thickness Left="3" Top="3" Right="3" Bottom="3"/>
    </Border.Padding>
    <Label x:Name="BadgeLabel" FontSize="12" .../>
</Border>
```

**Wichtig:** Der `x:Name` bleibt identisch damit `BellControl.xaml.cs` nicht geändert werden muss.

---

### 2.3 `Pages/ProfilePage.xaml` – ProgressBar-Frame konvertieren

**Zeile 418 – Aktuell:**
```xml
<Frame CornerRadius="20" Padding="15">
    <ProgressBar .../>
</Frame>
```

**Neu:**
```xml
<Border Padding="15">
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="20"/>
    </Border.StrokeShape>
    <ProgressBar .../>
</Border>
```

**SaveProfileFrame (Zeile 289) und VerifyQualificationsFrame (Zeile 343) bleiben unverändert als Frame!**

---

### 2.4 `Pages/RatingPage.xaml` – 3 Frames konvertieren

**Chat-Rating-Frame (Zeile 73):**
```xml
<!-- ALT: -->
<Frame CornerRadius="10" Padding="15" BackgroundColor="White">

<!-- NEU: -->
<Border Padding="15">
    <Border.StrokeShape><RoundRectangle CornerRadius="10"/></Border.StrokeShape>
    <Border.Background><SolidColorBrush Color="White"/></Border.Background>
```

Gleiche Konvertierung für Event-Rating-Frame (Zeile 265) und Comment-Item-Frame (Zeile 454).

---

### 2.5 `Pages/RegisterPage.xaml` – Input-Feld-Frames konvertieren

Input-Felder mit `Frame CornerRadius="25"` werden zu:
```xml
<Border>
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="25"/>
    </Border.StrokeShape>
    <Border.Stroke>
        <SolidColorBrush Color="#DDDDDD"/>
    </Border.Stroke>
    <!-- Entry/Editor bleibt unverändert drin -->
</Border>
```

---

### 2.6 Star Rating Fix – Analyse des Problems

**Aktueller Code (ProfilePage.xaml – Stern-Implementierung):**

In `ProfilePage.xaml` existieren 5 Grid-Container (`Star1Container` bis `Star5Container`), jeder enthält:
- Ein leeres Stern-Bild: `Star{n}Empty` (Image, 22x22, `star_empty32.png`)
- Ein gefülltes Stern-Bild: `Star{n}Fill` (Image, 22x22, `star_filled32.png`, Opacity=0)

In `ProfilePage.xaml.cs` wird `UpdateStarsDetailed(averageRating, totalCount)` aufgerufen:
- Berechnet für jeden Stern einen `starValue` (0.0–1.0)
- Ruft `ClipStarFill(fillImage, starValue)` auf
- `ClipStarFill` erstellt eine `RectangleGeometry` und schneidet das Bild clip-basiert ab

**Gleiches Muster in:**
- `UserProfilePage.xaml` + `UserProfilePage.xaml.cs`
- `RatingPage.xaml` + `RatingPage.xaml.cs`

**Das vorhandene `StarRatingControl`** (in `Resources/Controls/`) hat:
- BindableProperty `Rating` (double, TwoWay)
- BindableProperty `RatingCount` (int, TwoWay)
- `UpdateStars()` intern implementiert
- "Keine Bewertungen" Label bei Count=0

**Problem:** StarRatingControl verwendet `ImageButton` für interaktive Sternbewertung (Nutzer kann Sterne setzen). Auf ProfilePage, UserProfilePage und RatingPage sind die Sterne jedoch **nur zur Anzeige** – kein interaktiver Input.

**Lösung:** `StarRatingControl` um einen `IsInteractive`-Parameter erweitern (oder ein separates `StarDisplayControl` erstellen) das die gleiche visuelle Darstellung hat aber keine Touch-Events hat.

---

### 2.7 `Resources/Controls/StarRatingControl.xaml` – IsInteractive Property hinzufügen

**Was hinzukommt:**

In `StarRatingControl.xaml.cs`:
```csharp
// Neues BindableProperty:
public static readonly BindableProperty IsInteractiveProperty =
    BindableProperty.Create(nameof(IsInteractive), typeof(bool), typeof(StarRatingControl), defaultValue: true);

public bool IsInteractive
{
    get => (bool)GetValue(IsInteractiveProperty);
    set => SetValue(IsInteractiveProperty, value);
}
```

In `StarRatingControl.xaml`:
```xml
<!-- Alle ImageButtons bekommen IsEnabled Binding: -->
<ImageButton IsEnabled="{Binding IsInteractive, Source={RelativeSource AncestorType={x:Type controls:StarRatingControl}}}" .../>
```

Alternativ: Bei `IsInteractive=False` werden die ImageButtons durch normale Images ersetzt (bessere Performance, kein Tap-Feedback auf nicht-interaktiven Sternen).

---

### 2.8 `Pages/ProfilePage.xaml` – Handgemachte Sterne ersetzen

**Was entfernt wird** (die 5 Grid-Container + 10 Image-Elemente für Sterne):
```xml
<!-- ENTFERNEN: Star1Container bis Star5Container mit je 2 Images -->
<Grid x:Name="Star1Container" ...>
    <Image x:Name="Star1Empty" Source="star_empty32.png" .../>
    <Image x:Name="Star1Fill"  Source="star_filled32.png" .../>
</Grid>
<!-- ... x5 -->
```

**Was eingefügt wird:**
```xml
<controls:StarRatingControl
    x:Name="ProfileStarRating"
    Rating="{Binding FinalRating}"
    RatingCount="{Binding FinalRatingCount}"
    IsInteractive="False"
    HeightRequest="30"
    HorizontalOptions="Center"/>
```

Zusätzlich bleibt das `RatingContainer`-Grid mit `TapGestureRecognizer` (das zu RatingPage navigiert) – es bekommt das StarRatingControl als Kind-Element.

---

### 2.9 `Pages/ProfilePage.xaml.cs` – UpdateStarsDetailed entfernen

**Was entfernt wird:**
```csharp
// ENTFERNEN:
private void UpdateStarsDetailed(double averageRating, int totalCount) { ... }
private void ClipStarFill(Image fillImage, double value) { ... }

// ENTFERNEN aus ViewModelOnPropertyChanged:
case nameof(viewModel.FinalRating):
case nameof(viewModel.FinalRatingCount):
    UpdateStarsDetailed(viewModel.FinalRating, viewModel.FinalRatingCount);
    break;
```

**Was bleibt:** Der `PropertyChanged`-Handler bleibt, aber nur für andere Properties. Das StarRatingControl aktualisiert sich selbst via Data Binding.

---

### 2.10 `Pages/UserProfilePage.xaml` + `UserProfilePage.xaml.cs` – Identische Änderungen

Gleiches Muster wie ProfilePage:
1. 5 Grid-Container mit 10 Images in UserProfilePage.xaml entfernen
2. `StarRatingControl` mit `IsInteractive="False"` einfügen
3. `UpdateStarsDetailed()` und `ClipStarFill()` aus `UserProfilePage.xaml.cs` entfernen

---

### 2.11 `Pages/RatingPage.xaml` + `RatingPage.xaml.cs` – Identische Änderungen

Die RatingPage zeigt **mehrere** Stern-Anzeigen (Chat- und Event-Kategorien), jeweils mehrere Sterne für verschiedene Dimensionen (Freundlichkeit, Zuverlässigkeit, etc.).

Für jede Kategorie:
1. Die 5 Grid-Container entfernen
2. Je ein `StarRatingControl` mit `IsInteractive="False"` einfügen
3. `UpdateStars()` und `DoClip()` aus `RatingPage.xaml.cs` entfernen

---

### ✅ Phase 2 Build-Test Checkpoint

**Was zu prüfen ist:**
1. App kompiliert ohne `x:Name not found`-Fehler
2. Sternebewertungen auf ProfilePage, UserProfilePage und RatingPage werden korrekt dargestellt
3. Teilweise Sternfüllungen (z.B. 3.67 Sterne) werden noch korrekt angezeigt
4. Bewerbungs-Frame-Buttons auf ProfilePage funktionieren noch (OnSaveProfileTapped, OnVerifyQualificationsTapped)
5. NotificationBadge (roter Kreis) im BellControl ist noch sichtbar
6. RegisterPage-Eingabefelder sind weiterhin benutzbar

**Häufige Fehler:**
- `IsInteractive` BindableProperty nicht korrekt deklariert → Build-Fehler
- StarRatingControl-Namespace nicht in XAML importiert → `controls:StarRatingControl` nicht gefunden
- `x:Name`-Referenzen in .cs-Dateien auf gelöschte Star-Images → Kompiler-Fehler (alle solche Referenzen müssen entfernt werden)

---

## Phase 3 – MapPage Redesign, Empty States & Performance

**Ziel:** MapPage erhält eine professionelle visuelle Hierarchie. Leere Listen sehen durchdacht aus statt kaputt. Bilder werden komprimiert bevor sie hochgeladen werden.
**Risiko:** Niedrig–Mittel
**Geschätzte Dateien:** 7

---

### 3.1 `Pages/MapPage.xaml` – Visuelles Konzept

**Aktueller Zustand (analysiert):**
- `Maps.Map` füllt den gesamten Bildschirm ✓
- Suchfeld: `Frame` mit `BackgroundColor="Black"`, schwarzem Rahmen, unten links platziert
- Suchfeld-Button: Gradient `#00f3ff → #0066a5` vertikal
- Kein Titel, kein Zurück-Button, keine visuelle Hierarchie

**Neues Layout-Konzept:**

```
┌─────────────────────────────────────────┐
│         [vollbild Map]                  │
│                                         │
│  ┌──────────────────────────────────┐  │ ← Glasmorphic Header (oben)
│  │ 📍 Karte       [Standort-Button] │  │   Blur/Semi-transparent
│  └──────────────────────────────────┘  │
│                                         │
│  (Map-Inhalt)                           │
│                                         │
│  ┌──────────────────────────────────┐  │ ← Suchfeld (unten, floating)
│  │ 🔍 Ort suchen...    [Suchen]     │  │   Weiß/hellgrau, abgerundet
│  └──────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

**Spezifische XAML-Änderungen in `MapPage.xaml`:**

**A) Grid-Struktur:** Die Seite bleibt ein Grid, die Map nimmt `RowSpan="2"` ein (füllt alles).

**B) Oberer Header-Bereich (absolut positioniert oder Grid Row 0):**
```xml
<!-- Statt keinem Header: -->
<Border BackgroundColor="#CCFFFFFF"  <!-- Semi-transparent weiß -->
        VerticalOptions="Start"
        Margin="12,52,12,0"          <!-- SafeArea oben -->
        Padding="16,12">
    <Border.StrokeShape><RoundRectangle CornerRadius="20"/></Border.StrokeShape>
    <Border.Shadow>
        <Shadow Brush="Black" Offset="0,4" Radius="12" Opacity="0.12"/>
    </Border.Shadow>
    <Grid ColumnDefinitions="*,Auto">
        <Label Text="📍 Karte in der Nähe"
               Style="{StaticResource TitleStyle}"
               VerticalOptions="Center"/>
        <Button Grid.Column="1"
                Text="⊕"
                Command="{Binding LoadCurrentLocationCommand}"
                WidthRequest="40" HeightRequest="40"
                CornerRadius="20"
                BackgroundColor="#00897b"
                TextColor="White"
                FontSize="18"/>
    </Grid>
</Border>
```

**C) Suchfeld (unten, statt schwarz → hell/glasmorphisch):**
```xml
<!-- ALT: schwarzes Frame mit schwarzem Rand -->
<!-- NEU: -->
<Border BackgroundColor="#F0FFFFFF"
        VerticalOptions="End"
        HorizontalOptions="Fill"
        Margin="12,0,12,100"         <!-- Über CustomTabBar -->
        Padding="12,8">
    <Border.StrokeShape><RoundRectangle CornerRadius="28"/></Border.StrokeShape>
    <Border.Stroke><SolidColorBrush Color="#20000000"/></Border.Stroke>
    <Border.Shadow>
        <Shadow Brush="Black" Offset="0,4" Radius="16" Opacity="0.15"/>
    </Border.Shadow>
    <Grid ColumnDefinitions="*,Auto">
        <Entry x:Name="SearchEntry"
               Placeholder="Ort suchen..."
               PlaceholderColor="#80000000"
               TextColor="#1A1A1A"
               BackgroundColor="Transparent"
               FontSize="15"/>
        <Button Grid.Column="1"
                Text="Suchen"
                Command="{Binding SearchCommand}"
                CornerRadius="18"
                HeightRequest="36"
                BackgroundColor="#00897b"
                TextColor="White"
                FontSize="13"
                Padding="14,0"/>
    </Grid>
</Border>
```

---

### 3.2 `Pages/MapPage.xaml.cs` – Anpassungen

**Was geändert wird:**
- Den `OnSearchClicked`-Handler anpassen falls der Entry-Name sich geändert hat (gleich lassen: `SearchEntry`)
- `LoadCurrentLocationCommand` als neues Command in ViewModel oder direkt als Button-Handler implementieren (falls nicht vorhanden)

---

### 3.3 Neues Control: `Resources/Controls/EmptyStateView.xaml`

**Neue Datei erstellen.**

Universell wiederverwendbarer Empty State, der in jeder Liste eingebunden werden kann.

**XAML-Struktur:**
```xml
<ContentView xmlns="..."
             xmlns:x="..."
             x:Class="NBH.Resources.Controls.EmptyStateView">

    <StackLayout Spacing="16" Padding="40,32" HorizontalOptions="Center" VerticalOptions="Center">

        <!-- Illustration / Icon -->
        <Image x:Name="StateIcon"
               HeightRequest="120"
               WidthRequest="120"
               HorizontalOptions="Center"
               Opacity="0.6"/>

        <!-- Titel -->
        <Label x:Name="StateTitle"
               HorizontalTextAlignment="Center"
               Style="{StaticResource TitleStyle}"
               Opacity="0.8"/>

        <!-- Beschreibung -->
        <Label x:Name="StateDescription"
               HorizontalTextAlignment="Center"
               Style="{StaticResource SubheadingStyle}"
               TextColor="#888888"
               MaxLines="3"
               LineBreakMode="WordWrap"/>

        <!-- CTA-Button (optional) -->
        <Button x:Name="StateActionButton"
                IsVisible="False"
                CornerRadius="22"
                HeightRequest="44"
                BackgroundColor="#00897b"
                TextColor="White"
                FontAttributes="Bold"
                FontSize="14"
                Margin="0,8,0,0"/>
    </StackLayout>
</ContentView>
```

**`Resources/Controls/EmptyStateView.xaml.cs` – BindableProperties:**
```csharp
// BindableProperties:
IconSource         (ImageSource) - Illustration oder Emoji-Icon
Title              (string)      - Haupttitel
Description        (string)      - Beschreibungstext
ActionButtonText   (string)      - CTA-Button-Text (wenn leer → Button unsichtbar)
ActionCommand      (ICommand)    - Command für CTA-Button
IsVisible          (bool)        - Standard-MAUI, geerbt
```

---

### 3.4 `Pages/AddPage.xaml` – Empty State einbinden

**Aktuell:** Wenn `FilteredEntries` leer ist, zeigt die CollectionView nichts → weißer/leerer Bereich.

**Änderung:** In der CollectionView `EmptyView` Property nutzen:

```xml
<CollectionView ItemsSource="{Binding FilteredEntries}"
                ...>
    <CollectionView.EmptyView>
        <controls:EmptyStateView
            IconSource="no_entries_illustration.png"
            Title="Noch keine Einträge"
            Description="In deiner Nähe gibt es noch keine Anfragen oder Angebote. Sei der Erste!"
            ActionButtonText="Jetzt erstellen"
            ActionCommand="{Binding NavigateToCreateCommand}"/>
    </CollectionView.EmptyView>
    <!-- ... ItemTemplate bleibt -->
</CollectionView>
```

**Neue Bild-Ressource:** Eine einfache Illustration oder ein passendes Icon (`no_entries_illustration.png`) muss in `Resources/Images/` hinzugefügt werden. Alternativ kann ein Unicode-Emoji als Image-Text dienen (z.B. über einen Label-Placeholder in EmptyStateView).

---

### 3.5 `Pages/ChatViewPage.xaml` – Empty State einbinden

```xml
<CollectionView ItemsSource="{Binding Chats}">
    <CollectionView.EmptyView>
        <controls:EmptyStateView
            IconSource="no_chats_illustration.png"
            Title="Noch keine Chats"
            Description="Wenn du einen Eintrag annimmst oder jemand deinen Eintrag annimmt, startet hier ein Chat."/>
    </CollectionView.EmptyView>
</CollectionView>
```

---

### 3.6 `Resources/ViewModels/ProfilePageViewModel.cs` – Bildkomprimierung für alle Uploads

**Aktuell:** Profilbild wird auf 600x600 verkleinert via ImageSharp – das ist bereits vorhanden.

**Problem:** Das Bild wird nach dem Crop noch mal als voller PNG-Stream hochgeladen. PNGs können sehr groß sein.

**Änderung in `SelectAndCropImageAsync()` (nach der 600x600 Skalierung):**

```csharp
// NACH der bestehenden 600x600 Skalierung, VOR dem Upload:
// JPEG mit 80% Qualität exportieren statt PNG:
var encoder = new JpegEncoder { Quality = 80 };
image.Save(outputStream, encoder);
// outputStream wird danach zum Upload verwendet
```

**Warum:** PNG bei 600x600 = 500KB–1.5MB. JPEG bei 80% = 60–150KB. Ladezeiten in der Liste drastisch reduziert.

**Zusatz:** Falls andere Upload-Pfade existieren (Event-Bild-Upload in CreateEventPage) – dort ebenfalls JPEG-Compression sicherstellen.

---

### 3.7 Anti-Flickern: Bestehende Liste behalten bis neue Daten da sind

**`Resources/ViewModels/AddPageViewModel.cs` – `LoadEntriesFromDatabaseAsync()`**

**Aktuell (wahrscheinlicher Ablauf):**
```csharp
Entries.Clear();  // ← Hier wird Liste geleert → kurzer leerer Zustand
// DB-Abfrage...
foreach (var entry in results) Entries.Add(entry);
```

**Optimierter Ablauf:**
```csharp
// NEU:
var newEntries = await _supabaseClient.From<EntryItem>()...Get();
// Erst wenn neue Daten da sind, Liste tauschen:
MainThread.BeginInvokeOnMainThread(() => {
    Entries.Clear();
    foreach (var entry in newEntries.Models) Entries.Add(entry);
});
// Alternativ: ObservableCollection direkt ersetzen via SetProperty
```

**Shimmer während Laden:** ShimmerView bereits vorhanden – in AddPage während `IsLoading=true` anzeigen:

In `AddPage.xaml`:
```xml
<controls:ShimmerView IsShimmering="{Binding IsLoading}"
                      IsVisible="{Binding IsLoading}"
                      HeightRequest="200"/>
<CollectionView IsVisible="{Binding IsNotLoading}" .../>
```

Neue Property `IsLoading` und `IsNotLoading` (invertiert) im AddPageViewModel.

---

### ✅ Phase 3 Build-Test Checkpoint

**Was zu prüfen ist:**
1. MapPage zeigt glasmorphisches Suchfeld (heller Hintergrund, nicht schwarz)
2. MapPage hat oberen Header-Bereich mit Titel
3. MapPage Suche funktioniert noch korrekt
4. AddPage zeigt Empty State wenn keine Einträge vorhanden (zum Test: Filter so setzen dass nichts angezeigt wird)
5. ChatViewPage zeigt Empty State wenn keine Chats vorhanden (frischer Test-Account)
6. Profilbild-Upload funktioniert noch (Compression-Änderung)
7. Keine Flickering wenn AddPage refresht wird (visuell prüfen)

---

## Phase 4 – Settings Page, In-App Feedback, Dark Mode

**Ziel:** Die App wirkt komplett und professionell. Nutzer können ihre Account-Einstellungen verwalten, Feedback geben und die App im Dark Mode nutzen.
**Risiko:** Mittel – neue Seiten + Dark-Mode-Bindings
**Geschätzte Dateien:** 12

---

### 4.1 Neue Datei: `Pages/SettingsPage.xaml`

**Vollständige UI-Struktur:**

```
┌─────────────────────────────────────────┐
│  ← Zurück        Einstellungen          │
├─────────────────────────────────────────┤
│  ┌──────────────────────────────────┐  │
│  │  👤 Max Mustermann               │  │
│  │  max@example.com    ✓ Verifiziert│  │
│  └──────────────────────────────────┘  │
├─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┤
│  BENACHRICHTIGUNGEN                     │
│  ┌──────────────────────────────────┐  │
│  │  Push-Benachrichtigungen  [----] │  │  ← Toggle
│  │  E-Mail Benachrichtigungen [---] │  │  ← Toggle
│  │  Neue Events in der Nähe  [----] │  │  ← Toggle
│  └──────────────────────────────────┘  │
├─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┤
│  ERSCHEINUNGSBILD                       │
│  ┌──────────────────────────────────┐  │
│  │  Dark Mode                [----] │  │  ← Toggle (Application.RequestedTheme)
│  └──────────────────────────────────┘  │
├─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┤
│  DATENSCHUTZ & SICHERHEIT               │
│  ┌──────────────────────────────────┐  │
│  │  Datenschutzerklärung      →     │  │  ← Navigation zu WebView/Browser
│  │  Nutzungsbedingungen       →     │  │
│  │  So schützen wir deine Daten →   │  │  ← Inline-Sektion (expandierbar)
│  │  Profil für andere sichtbar [--] │  │  ← Toggle
│  └──────────────────────────────────┘  │
├─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┤
│  SUPPORT & FEEDBACK                     │
│  ┌──────────────────────────────────┐  │
│  │  Feedback geben            →     │  │  ← Öffnet FeedbackSheet
│  │  App bewerten              →     │  │  ← Store-Bewertung
│  │  FAQ                       →     │  │  ← Navigation zu FAQPage
│  └──────────────────────────────────┘  │
├─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┤
│  VERSION                                │
│  App-Version: 1.0.0 (Build 42)         │  ← Aus Assembly-Info
├─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┤
│  ACCOUNT                                │
│  ┌──────────────────────────────────┐  │
│  │  Passwort ändern           →     │  │  ← Aus ProfilePage übernehmen
│  │  Account deaktivieren      →     │  │  ← Confirmation-Dialog
│  │  ⚠️ Account löschen         →     │  │  ← Multi-Step Löschflow (Phase 6)
│  └──────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

**XAML-Implementierung:** Die Seite nutzt `ScrollView → VerticalStackLayout` mit `Border`-Cards für jede Sektion. Jede Einstellungs-Zeile ist ein `Grid` mit Label links und Control (Switch, Button oder Chevron-Label) rechts.

Wiederverwendbares Zeilen-Template (als DataTemplate oder inline):
```xml
<Grid ColumnDefinitions="*,Auto" Padding="16,14" MinimumHeightRequest="52">
    <Label Text="Einstellungs-Name" VerticalOptions="Center"
           Style="{StaticResource BodyLargeStyle}"/>
    <Switch Grid.Column="1" VerticalOptions="Center"
            IsToggled="{Binding SettingValue}"
            OnColor="#00897b"/>
</Grid>
```

---

### 4.2 Neue Datei: `Resources/ViewModels/SettingsPageViewModel.cs`

**Observable Properties:**
```csharp
bool PushNotificationsEnabled      // → Preferences.Set/Get("push_enabled", true)
bool EmailNotificationsEnabled     // → Preferences.Set/Get("email_enabled", true)
bool NearbyEventsEnabled           // → Preferences.Set/Get("nearby_enabled", true)
bool IsDarkModeEnabled             // → Application.Current.RequestedTheme
bool IsProfilePublic               // → Supabase users.is_public (neue Spalte)
string AppVersion                  // → AppInfo.VersionString + " (Build " + AppInfo.BuildString + ")"
string CurrentUserEmail            // Anzeige
string CurrentUsername             // Anzeige
bool IsVerified                    // Anzeige des Verifikations-Status
```

**Commands:**
```csharp
OpenPrivacyPolicyCommand       // → Browser.OpenAsync("https://...")
OpenTermsCommand               // → Browser.OpenAsync("https://...")
OpenFeedbackCommand            // → Navigation zu FeedbackSheet
OpenAppStoreRatingCommand      // → AppReviewRequest oder Store-URL
NavigateToFaqCommand           // → Shell.Current.GoToAsync(Routes.FaqPage)
ChangePasswordCommand          // → AuthService.SendPasswordResetEmail() (aus ProfilePage übernehmen)
DeactivateAccountCommand       // → Confirmation + Supabase update
DeleteAccountCommand           // → Navigation zu DeleteAccountPage (Phase 6)
GoBackCommand                  // → Shell.Current.GoToAsync("..")
ToggleDarkModeCommand          // → Application.Current.UserAppTheme = ...
```

---

### 4.3 In-App Feedback – Bottom Sheet Ansatz

**Konzept:** Kein separates Fenster – ein Mopup (bereits im Projekt verfügbar) zeigt ein einfaches Feedback-Formular.

**`Services/FeedbackService.cs` (neue Datei):**

```csharp
public class FeedbackService
{
    public async Task SubmitFeedbackAsync(string message, string category, Guid userId)
    {
        // Option A: E-Mail öffnen
        await Email.ComposeAsync(new EmailMessage {
            To = new List<string> { "feedback@nbh-app.de" },
            Subject = $"NBH Feedback – {category}",
            Body = $"Von: {userId}\n\n{message}"
        });

        // Option B: In Supabase-Tabelle speichern (Tabelle 'feedback' erstellen)
        // await _supabase.From<FeedbackModel>().Insert(new FeedbackModel { ... });
    }
}
```

**Feedback-Sheet UI (Mopup):**
```
┌────────────────────────────────────┐
│  Feedback geben              [×]   │
├────────────────────────────────────┤
│  Was möchtest du mitteilen?        │
│  ┌──────────────────────────────┐  │
│  │ ○ Fehler melden              │  │
│  │ ○ Funktion vorschlagen       │  │
│  │ ○ Lob & Kritik               │  │
│  └──────────────────────────────┘  │
│                                    │
│  Beschreibung:                     │
│  ┌──────────────────────────────┐  │
│  │ Dein Feedback...             │  │
│  │ (min. 20 Zeichen)            │  │
│  └──────────────────────────────┘  │
│                                    │
│  [    Feedback senden    ]         │
└────────────────────────────────────┘
```

---

### 4.4 Versionsnummer anzeigen

In `SettingsPageViewModel.cs`:
```csharp
public string AppVersion => $"Version {AppInfo.VersionString} (Build {AppInfo.BuildString})";
```

In `SettingsPage.xaml`:
```xml
<Label Text="{Binding AppVersion}"
       Style="{StaticResource CaptionStyle}"
       HorizontalTextAlignment="Center"
       Margin="0,8"/>
```

**Für iOS und Android:** `AppInfo.VersionString` liest die Versionsnummer aus dem jeweiligen nativen Projekt (CFBundleShortVersionString / android:versionName).

---

### 4.5 Settings-Navigation von ProfilePage aus

**`Pages/ProfilePage.xaml`** – Einstellungs-Button hinzufügen:
```xml
<!-- Im Header-Bereich oben rechts: -->
<ImageButton Source="settings_icon.png"
             Command="{Binding NavigateToSettingsCommand}"
             WidthRequest="28" HeightRequest="28"
             Margin="0,0,16,0"
             HorizontalOptions="End"
             VerticalOptions="Start"
             Opacity="0.8"/>
```

**`Resources/ViewModels/ProfilePageViewModel.cs`** – Command hinzufügen:
```csharp
public ICommand NavigateToSettingsCommand => new Command(async () =>
    await Shell.Current.GoToAsync(Routes.SettingsPage));
```

Den `ResetPasswordCommand` und den zugehörigen Code aus ProfilePageViewModel in SettingsPageViewModel **duplizieren** (nicht entfernen aus ProfilePage – Entscheidung nach der Implementierung).

---

### 4.6 Dark Mode – `Resources/Styles/Colors.xaml`

**Aktuell:** Colors.xaml definiert statische Farben. `AppThemeBinding` wird punktuell genutzt.

**Ziel:** Alle Hintergrund-, Text- und Oberflächen-Farben via `AppThemeBinding` umschalten.

**In `Styles.xaml` den globalen ContentPage-Style auf AppThemeBinding umstellen:**

```xml
<Style TargetType="ContentPage" ApplyToDerivedTypes="True">
    <Setter Property="Background">
        <Setter.Value>
            <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                <GradientStop Offset="0.0">
                    <GradientStop.Color>
                        <AppThemeBinding Light="#b7f0e9" Dark="#0d2e2a"/>
                    </GradientStop.Color>
                </GradientStop>
                <GradientStop Offset="0.5">
                    <GradientStop.Color>
                        <AppThemeBinding Light="#15be9e" Dark="#0a4a3d"/>
                    </GradientStop.Color>
                </GradientStop>
                <GradientStop Offset="1.0">
                    <GradientStop.Color>
                        <AppThemeBinding Light="#b7f0e9" Dark="#0d2e2a"/>
                    </GradientStop.Color>
                </GradientStop>
            </LinearGradientBrush>
        </Setter.Value>
    </Setter>
</Style>
```

**Karten-Hintergrund mit AppThemeBinding:**
```xml
<!-- In Styles_Polished.xaml, CardStyle: -->
<Setter Property="BackgroundColor">
    <Setter.Value>
        <AppThemeBinding Light="White" Dark="#1E2A28"/>
    </Setter.Value>
</Setter>
```

**Label-Farben:**
```xml
<!-- Standard-Label (global): -->
<Style TargetType="Label">
    <Setter Property="TextColor">
        <AppThemeBinding Light="{StaticResource TextPrimaryLight}"
                         Dark="{StaticResource TextPrimaryDark}"/>
    </Setter>
</Style>
```

**Dark Mode Toggle in SettingsPageViewModel:**
```csharp
private void ExecuteToggleDarkMode()
{
    Application.Current.UserAppTheme = IsDarkModeEnabled
        ? AppTheme.Dark
        : AppTheme.Light;
    Preferences.Set("dark_mode", IsDarkModeEnabled);
}

// Beim App-Start in MauiProgram.cs oder App.xaml.cs:
var isDark = Preferences.Get("dark_mode", false);
Application.Current.UserAppTheme = isDark ? AppTheme.Dark : AppTheme.Light;
```

---

### 4.7 `Routes.cs` – Neue Route

```csharp
public const string SettingsPage = "SettingsPage";
```

### 4.8 `AppShell.xaml.cs` – Route registrieren

```csharp
Routing.RegisterRoute(Routes.SettingsPage, typeof(SettingsPage));
```

### 4.9 `MauiProgram.cs` – DI-Registrierung

```csharp
builder.Services.AddTransient<SettingsPage>();
builder.Services.AddTransient<SettingsPageViewModel>();
builder.Services.AddSingleton<FeedbackService>();
```

---

### ✅ Phase 4 Build-Test Checkpoint

**Was zu prüfen ist:**
1. SettingsPage öffnet sich von ProfilePage aus
2. Alle Toggle-Switches speichern/laden korrekt via `Preferences`
3. Dark Mode Toggle schaltet das gesamte App-Design um
4. App-Version wird korrekt angezeigt (nicht "0.0.0")
5. Feedback-Sheet öffnet sich und E-Mail-Compose wird gestartet
6. Zurück-Navigation aus SettingsPage funktioniert
7. Dark Mode bleibt nach App-Neustart erhalten (Preferences persistiert)

**Häufige Fehler:**
- `AppInfo.VersionString` gibt `""` zurück auf Simulator → Mit echtem Gerät oder konfigurierten Build-Nummern testen
- `Application.Current.UserAppTheme` funktioniert nur ab MAUI 7+ → Version prüfen (aktuell MAUI 10 ✓)
- Feedback-E-Mail funktioniert nicht auf iOS-Simulator → Mit echtem Gerät testen
- Dark Mode zeigt Karten noch als weiß → `AppThemeBinding` in `CardStyle` fehlt

---

## Phase 5 – Sicherheit, Vertrauen & Moderations-System

**Ziel:** Nutzer fühlen sich sicher. Verifizierten Accounts werden vertraut. Schlechte Akteure können gemeldet und gesperrt werden. Bewertungen sind transparent und glaubwürdig.
**Risiko:** Mittel–Hoch (neue DB-Tabellen, neue Services)
**Geschätzte Dateien:** 11 + 2 neue Supabase-Tabellen

---

### 5.1 Supabase: `users`-Tabelle erweitern

```sql
ALTER TABLE users ADD COLUMN is_verified    BOOLEAN DEFAULT FALSE;
ALTER TABLE users ADD COLUMN is_public      BOOLEAN DEFAULT TRUE;
ALTER TABLE users ADD COLUMN is_banned      BOOLEAN DEFAULT FALSE;
ALTER TABLE users ADD COLUMN banned_reason  TEXT;
ALTER TABLE users ADD COLUMN banned_at      TIMESTAMPTZ;
```

**`is_verified`:** Wird manuell durch Admin gesetzt (Supabase Dashboard) oder per automatischer E-Mail-Verifizierung. Start: immer `false`.
**`is_public`:** Profil für andere sichtbar (aus SettingsPage steuerbar).
**`is_banned`:** Admin setzt auf `true` wenn Nutzer gegen Regeln verstößt.

---

### 5.2 Supabase: Neue Tabelle `reports`

```sql
CREATE TABLE reports (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reporter_id     UUID NOT NULL REFERENCES users(id),
    reported_user_id UUID REFERENCES users(id),  -- NULL wenn Entry gemeldet
    reported_entry_id UUID REFERENCES entries(id), -- NULL wenn User gemeldet
    reason          TEXT NOT NULL,
    -- 'spam' | 'inappropriate' | 'fake' | 'harassment' | 'other'
    description     TEXT,
    status          TEXT DEFAULT 'pending',
    -- 'pending' | 'reviewed' | 'resolved' | 'dismissed'
    created_at      TIMESTAMPTZ DEFAULT now(),
    reviewed_at     TIMESTAMPTZ,
    admin_note      TEXT
);

-- RLS: Jeder kann reporten, nur Admin kann Status lesen/ändern
ALTER TABLE reports ENABLE ROW LEVEL SECURITY;
CREATE POLICY "Anyone can report" ON reports FOR INSERT WITH CHECK (reporter_id = auth.uid());
CREATE POLICY "Own reports readable" ON reports FOR SELECT USING (reporter_id = auth.uid());
```

---

### 5.3 Supabase: RLS für gebannte Nutzer

```sql
-- Gebannte Nutzer können keine neuen Entries erstellen:
CREATE POLICY "Banned users cannot insert" ON entries
    FOR INSERT WITH CHECK (
        NOT EXISTS (SELECT 1 FROM users WHERE id = auth.uid() AND is_banned = true)
    );

-- Gebannte Nutzer können sich nicht mehr in Supabase Auth einloggen:
-- Dafür in AuthService.cs nach dem Login den user.is_banned-Status prüfen
```

---

### 5.4 `Resources/Models/User.cs` – Neue Felder

```csharp
[Column("is_verified")]
public bool IsVerified { get; set; }

[Column("is_public")]
public bool IsPublic { get; set; } = true;

[Column("is_banned")]
public bool IsBanned { get; set; }
```

---

### 5.5 `Resources/Models/Report.cs` – Neues Modell

```csharp
[Table("reports")]
public class Report : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("reporter_id")]
    public Guid ReporterId { get; set; }

    [Column("reported_user_id")]
    public Guid? ReportedUserId { get; set; }

    [Column("reported_entry_id")]
    public Guid? ReportedEntryId { get; set; }

    [Column("reason")]
    public string Reason { get; set; } = "";

    [Column("description")]
    public string? Description { get; set; }

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
```

---

### 5.6 `Services/ModerationService.cs` – Neuer Service

```csharp
public class ModerationService
{
    private readonly Supabase.Client _supabase;

    // Nutzer melden
    public async Task<bool> ReportUserAsync(Guid reportedUserId, string reason, string? description)

    // Eintrag melden
    public async Task<bool> ReportEntryAsync(Guid reportedEntryId, string reason, string? description)

    // Nutzer blockieren (lokal, kein DB-Eintrag nötig – Preferences)
    public void BlockUser(Guid blockedUserId)

    // Ist Nutzer blockiert?
    public bool IsUserBlocked(Guid userId)

    // Alle blockierten User abrufen
    public List<Guid> GetBlockedUsers()

    // Blockierung aufheben
    public void UnblockUser(Guid userId)
}
```

**Blockier-Mechanismus:** Lokal via `Preferences.Set("blocked_users", JSON-Array)`. Blockierte Nutzer werden in der Entries-Liste gefiltert (im AddPageViewModel).

---

### 5.7 Neues Control: `Resources/Controls/VerifiedBadge.xaml`

```xml
<ContentView ...>
    <HorizontalStackLayout Spacing="4">
        <Image Source="verified_check.png"
               WidthRequest="14" HeightRequest="14"
               VerticalOptions="Center"
               IsVisible="{Binding IsVerified}"/>
        <Label Text="Verifiziert"
               FontSize="11"
               FontAttributes="Bold"
               TextColor="#00897b"
               VerticalOptions="Center"
               IsVisible="{Binding IsVerified}"/>
    </HorizontalStackLayout>
</ContentView>
```

**BindableProperty:** `IsVerified` (bool)

**Bild-Ressource:** `verified_check.png` – grünes Häkchen-Icon (16x16, in Resources/Images hinzufügen)

---

### 5.8 `Pages/UserProfilePage.xaml` – Verified Badge + Report Button

**Verified Badge unter dem Nutzernamen einfügen:**
```xml
<!-- Nach Username-Label (Zeile ~83): -->
<controls:VerifiedBadge
    IsVerified="{Binding IsVerified}"
    HorizontalOptions="Center"
    Margin="0,2,0,0"/>
```

**Report-Button als Drei-Punkte-Menü oben rechts:**
```xml
<!-- Im Header-Bereich: -->
<ImageButton Source="more_options.png"
             VerticalOptions="Start"
             HorizontalOptions="End"
             WidthRequest="24" HeightRequest="24"
             Margin="0,16,16,0"
             Command="{Binding ShowOptionsCommand}"/>
```

`ShowOptionsCommand` öffnet ein `ActionSheet`:
```csharp
var action = await Shell.Current.DisplayActionSheet(
    "Optionen", "Abbrechen", null,
    "Nutzer melden", "Nutzer blockieren");

if (action == "Nutzer melden") { /* Report-Flow */ }
if (action == "Nutzer blockieren") { /* Block lokal */ }
```

---

### 5.9 `Pages/DetailPage.xaml` – Report-Button für Einträge

**In der oberen Ecke des Entry-Headers oder als Kontext-Menu:**
```xml
<!-- Drei-Punkte Button im Header -->
<ImageButton Source="more_options.png"
             Command="{Binding ShowEntryOptionsCommand}"
             IsVisible="{Binding IsNotOwner}"  <!-- Eigene Einträge nicht melden -->
             .../>
```

**`DetailPageViewModel.cs` – ShowEntryOptionsCommand:**
```csharp
var action = await Shell.Current.DisplayActionSheet(
    "Optionen", "Abbrechen", null,
    "Eintrag melden");

if (action == "Eintrag melden") {
    // Report-Grund-Auswahl öffnen
    var reason = await Shell.Current.DisplayActionSheet(
        "Grund wählen", "Abbrechen", null,
        "Spam", "Unangemessener Inhalt", "Falsche Informationen", "Anderes");
    if (reason != null && reason != "Abbrechen") {
        await _moderationService.ReportEntryAsync(SelectedEntry.Id, reason, null);
        await Shell.Current.DisplayAlert("Danke", "Deine Meldung wurde übermittelt.", "OK");
    }
}
```

---

### 5.10 `Services/AuthService.cs` – Gebannte Nutzer abfangen

**Nach erfolgreichem Login:**
```csharp
// Nach Session-Validierung:
var user = await _supabase.From<User>().Where(u => u.Id == currentUserId).Single();
if (user.IsBanned) {
    await _supabase.Auth.SignOut();
    throw new Exception("Dieser Account wurde gesperrt. Bitte kontaktiere den Support.");
}
```

---

### 5.11 `Resources/ViewModels/UserProfilePageViewModel.cs` – IsVerified Property

```csharp
[ObservableProperty]
private bool _isVerified;

// In LoadUserProfileAsync(), nach dem Laden:
IsVerified = foundUser.IsVerified;
```

---

### 5.12 Bewertungs-Transparenz: Kommentare auf RatingPage anzeigen

**`Pages/RatingPage.xaml`** – Kommentare-Sektion erweitern:

Aktuell: Kommentare sind in einem Frame mit `CornerRadius="30"` – das wurde aus der Analyse erkannt.

**Sicherstellen dass der Kommentartext gut lesbar ist:**
```xml
<!-- Kommentar-Item (innerhalb der CollectionView für Ratings): -->
<Border Padding="16" Margin="0,6">
    <Border.StrokeShape><RoundRectangle CornerRadius="16"/></Border.StrokeShape>
    <Grid RowDefinitions="Auto,Auto,Auto">
        <!-- Nutzername + Datum -->
        <Grid ColumnDefinitions="*,Auto">
            <Label Text="{Binding ReviewerName}"
                   Style="{StaticResource BodyLargeStyle}"
                   FontAttributes="Bold"/>
            <Label Grid.Column="1"
                   Text="{Binding CreatedAt, StringFormat='{0:dd.MM.yyyy}'}"
                   Style="{StaticResource CaptionStyle}"/>
        </Grid>
        <!-- Sternebewertung (mini) -->
        <controls:StarRatingControl Grid.Row="1"
                                    Rating="{Binding AverageScore}"
                                    IsInteractive="False"
                                    HeightRequest="20"
                                    HorizontalOptions="Start"/>
        <!-- Kommentartext -->
        <Label Grid.Row="2"
               Text="{Binding Comment}"
               Style="{StaticResource BodyStyle}"
               TextColor="#555555"
               Margin="0,8,0,0"
               IsVisible="{Binding HasComment}"/>
    </Grid>
</Border>
```

**`RatingPageViewModel.cs`** – Kommentare in die `LoadDataAsync()` einbeziehen:
Die Methode muss `EventRating.Comment` und `ChatReview` mit Kommentaren laden und in der UI-Collection verfügbar machen.

---

### ✅ Phase 5 Build-Test Checkpoint

**Was zu prüfen ist:**
1. Verified Badge erscheint bei Nutzern mit `is_verified=true` (manuell in Supabase setzen)
2. Drei-Punkte-Menü öffnet sich auf UserProfilePage
3. Report-Meldung wird in Supabase `reports`-Tabelle gespeichert
4. Report-Button auf DetailPage funktioniert für andere Nutzer (nicht für eigene Einträge)
5. Gesperrter Nutzer kann sich nicht einloggen (is_banned=true in Supabase testen)
6. Bewertungskommentare werden auf RatingPage angezeigt
7. ModerationService korrekt in DI registriert

**Häufige Fehler:**
- `User.is_verified` Spalte fehlt in Supabase → SQL-Migration vergessen
- `DisplayActionSheet` gibt Lokalisierungs-Probleme auf verschiedenen Geräten → explizite String-Prüfung statt `==` verwenden
- Blocked users in Preferences werden nicht korrekt als List<Guid> serialisiert → JSON-Serialisierung explizit implementieren

---

## Phase 6 – DSGVO, Account-Management, Haptic & Final Polish

**Ziel:** Gesetzliche Anforderungen (DSGVO) erfüllen. Haptic Feedback ist konsequent überall. Alle verbleibenden UX-Lücken schließen.
**Risiko:** Hoch (Account-Löschung ist irreversibel – erfordert besondere Sorgfalt)
**Geschätzte Dateien:** 9

---

### 6.1 Supabase: `users`-Tabelle – DSGVO-Felder

```sql
ALTER TABLE users ADD COLUMN terms_accepted_at    TIMESTAMPTZ;
ALTER TABLE users ADD COLUMN privacy_accepted_at  TIMESTAMPTZ;
ALTER TABLE users ADD COLUMN terms_version        TEXT DEFAULT '1.0';
ALTER TABLE users ADD COLUMN deletion_requested_at TIMESTAMPTZ;
ALTER TABLE users ADD COLUMN deletion_scheduled_at TIMESTAMPTZ;
```

**Logik:**
- `terms_accepted_at` wird beim Registrieren gesetzt
- `deletion_requested_at` wird gesetzt wenn Nutzer Account-Löschung anfordert
- `deletion_scheduled_at` = `deletion_requested_at` + 30 Tage (Widerrufsfrist)

---

### 6.2 `Pages/RegisterPage.xaml` – AGB + Datenschutz Checkbox

**Unterhalb des "Registrieren"-Buttons hinzufügen:**
```xml
<HorizontalStackLayout Spacing="10" Margin="0,8">
    <CheckBox x:Name="TermsCheckBox"
              Color="#00897b"
              IsChecked="{Binding HasAcceptedTerms}"/>
    <Label VerticalOptions="Center">
        <Label.FormattedText>
            <FormattedString>
                <Span Text="Ich stimme den "/>
                <Span Text="Nutzungsbedingungen"
                      TextColor="#00897b"
                      TextDecorations="Underline">
                    <Span.GestureRecognizers>
                        <TapGestureRecognizer Command="{Binding OpenTermsCommand}"/>
                    </Span.GestureRecognizers>
                </Span>
                <Span Text=" und der "/>
                <Span Text="Datenschutzerklärung"
                      TextColor="#00897b"
                      TextDecorations="Underline">
                    <Span.GestureRecognizers>
                        <TapGestureRecognizer Command="{Binding OpenPrivacyCommand}"/>
                    </Span.GestureRecognizers>
                </Span>
                <Span Text=" zu."/>
            </FormattedString>
        </Label.FormattedText>
    </Label>
</HorizontalStackLayout>

<!-- Hinweis wenn nicht akzeptiert: -->
<Label Text="Bitte stimme den Bedingungen zu um fortzufahren."
       Style="{StaticResource CaptionStyle}"
       TextColor="{StaticResource ErrorColor}"
       IsVisible="{Binding ShowTermsError}"/>
```

**`Resources/ViewModels/RegisterPageViewModel.cs` – Änderungen:**
```csharp
[ObservableProperty]
private bool _hasAcceptedTerms = false;

[ObservableProperty]
private bool _showTermsError = false;

// In RegisterCommand (CanExecute erweitern):
// _hasAcceptedTerms muss true sein

// Nach erfolgreicher Registrierung:
await _supabase.From<User>().Where(u => u.Id == newUserId)
    .Set(u => u.TermsAcceptedAt, DateTime.UtcNow)
    .Set(u => u.PrivacyAcceptedAt, DateTime.UtcNow)
    .Set(u => u.TermsVersion, "1.0")
    .Update();
```

---

### 6.3 Account-Löschung – Multi-Step Flow

**Ablauf (3 Schritte, kein Popup-Overload):**

```
Schritt 1: Warnung & Erklärung
  "Wenn du deinen Account löschst, werden alle deine Daten
   unwiderruflich entfernt: Profil, Einträge, Chats, Bewertungen."
  [Weiter →]  [Abbrechen]

Schritt 2: Bestätigung durch Passwort-Eingabe
  "Gib dein aktuelles Passwort ein um die Löschung zu bestätigen:"
  [Passwort-Feld]
  [Account löschen]  [Abbrechen]

Schritt 3: Löschung gestartet
  "Dein Account wird in 30 Tagen gelöscht.
   Bis dahin kannst du dich jederzeit wieder einloggen
   um die Löschung rückgängig zu machen."
  [Verstanden]
```

**Implementierung über SettingsPage → AccountDeletionPage (optionale neue Page oder Mopup-Flow):**

Alternativ als 3-Screen-Mopup mit Supabase-Seitennavigation.

**`SettingsPageViewModel.cs` – DeleteAccountCommand:**
```csharp
private async Task ExecuteDeleteAccountAsync()
{
    // Schritt 1: Warnung
    var proceed = await Shell.Current.DisplayAlert(
        "Account löschen",
        "Wenn du deinen Account löschst, werden alle deine Daten " +
        "unwiderruflich entfernt: Profil, Einträge, Chats und Bewertungen.\n\n" +
        "Du hast 30 Tage Zeit, dies rückgängig zu machen.",
        "Weiter", "Abbrechen");

    if (!proceed) return;

    // Schritt 2: Passwort-Bestätigung
    var password = await Shell.Current.DisplayPromptAsync(
        "Bestätigung",
        "Gib dein Passwort ein um die Löschung zu bestätigen:",
        placeholder: "Passwort",
        maxLength: 100,
        keyboard: Keyboard.Text);

    if (string.IsNullOrEmpty(password)) return;

    // Passwort validieren via Re-Authentifizierung
    var isValid = await _authService.ValidatePasswordAsync(password);
    if (!isValid) {
        await Shell.Current.DisplayAlert("Fehler", "Ungültiges Passwort.", "OK");
        return;
    }

    // Löschung vormerken
    await _supabase.From<User>()
        .Where(u => u.Id == currentUserId)
        .Set(u => u.DeletionRequestedAt, DateTime.UtcNow)
        .Set(u => u.DeletionScheduledAt, DateTime.UtcNow.AddDays(30))
        .Update();

    // Ausloggen
    await _authService.LogoutAsync();
    await Shell.Current.GoToAsync("//LoginPage");

    // Erfolgs-Meldung
    await Shell.Current.DisplayAlert(
        "Anfrage eingegangen",
        "Dein Account wird in 30 Tagen gelöscht. " +
        "Du kannst dich noch bis dahin einloggen um die Löschung zu stornieren.",
        "OK");
}
```

---

### 6.4 Supabase: Datenlöschungs-Funktion (PostgreSQL)

```sql
-- Edge Function oder Scheduled Task in Supabase:
CREATE OR REPLACE FUNCTION delete_user_data(target_user_id UUID)
RETURNS void AS $$
BEGIN
    -- 1. Profilbild aus Storage löschen
    -- (Über Supabase Storage API in Edge Function)

    -- 2. Chat-Nachrichten anonymisieren
    UPDATE chat_messages
    SET content = '[Nachricht gelöscht]', sender_id = NULL
    WHERE sender_id = target_user_id;

    -- 3. Chats löschen wo Nutzer Teilnehmer war
    DELETE FROM chats WHERE user_id = target_user_id OR partner_id = target_user_id;

    -- 4. Einträge als completed markieren (nicht löschen für Partnerkontinuität)
    UPDATE entries SET is_completed = true WHERE user_id = target_user_id;

    -- 5. Bewertungen anonymisieren
    UPDATE event_rating SET reviewer_id = NULL WHERE reviewer_id = target_user_id;
    UPDATE chat_reviews SET reviewer_id = NULL WHERE reviewer_id = target_user_id;

    -- 6. Notifications löschen
    DELETE FROM notifications WHERE user_id = target_user_id;

    -- 7. User-Datensatz anonymisieren (nicht löschen für referentielle Integrität)
    UPDATE users SET
        username = 'Gelöschter Nutzer',
        email = NULL,
        full_name = NULL,
        profile_image_url = NULL,
        wohnort = NULL,
        ueber_mich = NULL,
        qualifikationen = NULL,
        apple_user_id = NULL,
        is_completed = true   -- neue Spalte als Soft-Delete
    WHERE id = target_user_id;

    -- 8. Auth-User löschen (via Supabase Admin API in Edge Function)
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;
```

**Supabase Edge Function:** `delete-user` – wird täglich via Scheduled Function aufgerufen, löscht alle Nutzer deren `deletion_scheduled_at` in der Vergangenheit liegt.

---

### 6.5 Konsequentes Haptic Feedback

**Analyse der aktuell fehlenden Haptic-Calls:**

Überall wo ein Button angetippt wird sollte:
```csharp
#if IOS
    iOSHapticFeedback.TriggerHeavyImpact();
#endif
HapticFeedback.Perform(HapticFeedbackType.Click);
```

**Fehlende Stellen (aus der Code-Analyse identifiziert):**

| Datei | Handler/Method | Typ |
|-------|---------------|-----|
| `AddPage.xaml.cs` | Pagination-Buttons (Zurück/Weiter/Anfang/Ende) | Click |
| `AddPage.xaml.cs` | Filter-Änderung | LongPress (light) |
| `DetailPage.xaml.cs` | `SingleActionButton_Tapped` | Click |
| `DetailPage.xaml.cs` | `DoubleActionButton_Tapped` | Click |
| `DetailPage.xaml.cs` | `RejectButton_Tapped` | Click |
| `DetailPage.xaml.cs` | `ContactButton_Tapped` | Click |
| `ChatPage.xaml.cs` | Nachricht senden | Click |
| `ChatViewPage.xaml.cs` | Chat-Item antippen | Click |
| `FAQPage.xaml.cs` | FAQ-Header antippen (Expander) | LongPress (light) |
| `SettingsPage.xaml.cs` | Toggle-Switches | LongPress (light) |
| `SettingsPage.xaml.cs` | Jeder Navigations-Tap | Click |

**Für Fehler/Fehlzustände:**
```csharp
// Wenn Validierung fehlschlägt / Fehler auftritt:
#if IOS
    iOSHapticFeedback.TriggerErrorFeedback();
#endif
HapticFeedback.Perform(HapticFeedbackType.LongPress);
```

**`Platforms/iOS/iOSHapticFeedback.cs`** – Neue Methode hinzufügen:
```csharp
public static void TriggerLightImpact()
{
    var generator = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Light);
    generator.Prepare();
    generator.ImpactOccurred();
}
```

---

### 6.6 Optimistische UI-Updates für Eintrag-Erstellung

**`Resources/ViewModels/AddPageViewModel.cs`** – `CreateEventCommand`:

```csharp
// Statt: erst speichern, dann liste neu laden
// NEU: Temporären Eintrag sofort hinzufügen

var tempEntry = new EntryItem {
    Id = Guid.NewGuid(),  // Temporäre ID
    Title = Title,
    Description = Description,
    // ... andere Properties
    IsTemporary = true    // Neue non-persistent Property
};

// Sofort in die Liste einfügen (oben):
MainThread.BeginInvokeOnMainThread(() => Entries.Insert(0, tempEntry));

try {
    // Echten Eintrag in DB speichern:
    var result = await _supabase.From<EntryItem>().Insert(tempEntry);
    var savedEntry = result.Model;

    // Temporären durch echten ersetzen:
    MainThread.BeginInvokeOnMainThread(() => {
        var index = Entries.IndexOf(tempEntry);
        if (index >= 0) Entries[index] = savedEntry;
    });
} catch {
    // Bei Fehler: temporären Eintrag entfernen
    MainThread.BeginInvokeOnMainThread(() => Entries.Remove(tempEntry));
    // Fehlermeldung anzeigen
}
```

---

### 6.7 `Pages/ProfilePage.xaml` – Datenschutz-Sektion hinzufügen

Kurzer "So schützen wir deine Daten"-Abschnitt auf der Profilseite (ausklappbar via Expander):

```xml
<toolkit:Expander Margin="0,8">
    <toolkit:Expander.Header>
        <Border Padding="16" BackgroundColor="Transparent">
            <Grid ColumnDefinitions="*,Auto">
                <Label Text="🔒  Datenschutz"
                       Style="{StaticResource FieldLabelStyle}"
                       VerticalOptions="Center"/>
                <Label Grid.Column="1" Text="›"
                       Style="{StaticResource CaptionStyle}"
                       VerticalOptions="Center"/>
            </Grid>
        </Border>
    </toolkit:Expander.Header>
    <VerticalStackLayout Padding="16,0,16,16" Spacing="8">
        <Label Style="{StaticResource BodySmallStyle}" TextColor="#555555">
            <Label.Text>
                • Deine Daten werden nur für die Vermittlung von Nachbarschaftshilfe genutzt.
                • Wir geben keine Daten an Dritte weiter.
                • Du kannst deine Daten jederzeit löschen.
            </Label.Text>
        </Label>
        <Label Text="Vollständige Datenschutzerklärung →"
               TextColor="#00897b"
               TextDecorations="Underline"
               FontSize="13">
            <Label.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding OpenPrivacyPolicyCommand}"/>
            </Label.GestureRecognizers>
        </Label>
    </VerticalStackLayout>
</toolkit:Expander>
```

---

### 6.8 `MauiProgram.cs` – Finale DI-Registrierungen

```csharp
builder.Services.AddSingleton<ModerationService>();
builder.Services.AddSingleton<FeedbackService>();
builder.Services.AddTransient<SettingsPage>();
builder.Services.AddTransient<SettingsPageViewModel>();
```

---

### ✅ Phase 6 Build-Test Checkpoint (FINAL)

**Was zu prüfen ist:**
1. AGB-Checkbox auf RegisterPage verhindert Registrierung wenn nicht angehakt
2. Account-Löschungs-Flow zeigt alle 3 Schritte korrekt
3. `deletion_requested_at` wird in Supabase gesetzt nach Bestätigung
4. Alle Haptic-Feedback-Calls funktionieren auf echtem iOS-Gerät
5. Haptic-Fehler-Feedback bei ungültigem Login spürbar
6. Datenschutz-Expander auf ProfilePage öffnet/schließt korrekt
7. Optimistische UI: Neuer Eintrag erscheint sofort in der Liste (vor Server-Antwort)
8. Optimistische UI: Bei Netzwerk-Fehler verschwindet temporärer Eintrag wieder

**DSGVO-Pflicht-Test:**
- Neuen Test-Account registrieren → Prüfen ob `terms_accepted_at` in Supabase gesetzt ist
- Account-Löschung anfordern → Prüfen ob `deletion_requested_at` korrekt gesetzt ist
- `delete_user_data()` Funktion manuell mit Test-User-ID aufrufen und Ergebnis prüfen

---

## Datei-Änderungsmatrix (Gesamt)

| Datei | Art | Phase | Priorität |
|-------|-----|-------|-----------|
| `Resources/Styles/Colors.xaml` | Erweitern | 1 | Hoch |
| `Resources/Styles/Styles.xaml` | Erweitern | 1+4 | Hoch |
| `Resources/Styles/Styles_Polished.xaml` | Erweitern | 1+4 | Mittel |
| `Pages/ProfilePage.xaml` | Umbau | 1+2+6 | Hoch |
| `Pages/ProfilePage.xaml.cs` | Bereinigen | 2+6 | Hoch |
| `Pages/AddPage.xaml` | Erweitern | 1+3 | Mittel |
| `Pages/DetailPage.xaml` | Erweitern | 1+5 | Mittel |
| `Pages/UserProfilePage.xaml` | Umbau | 1+2+5 | Hoch |
| `Pages/UserProfilePage.xaml.cs` | Bereinigen | 2 | Hoch |
| `Pages/RatingPage.xaml` | Umbau | 1+2 | Mittel |
| `Pages/RatingPage.xaml.cs` | Bereinigen | 2 | Mittel |
| `Pages/LoginPage.xaml` | Anpassen | 1 | Niedrig |
| `Pages/RegisterPage.xaml` | Umbau | 1+2+6 | Hoch |
| `Pages/ChatViewPage.xaml` | Anpassen | 1+3 | Niedrig |
| `Pages/ChatPage.xaml` | Anpassen | 1 | Niedrig |
| `Pages/FAQPage.xaml` | Anpassen | 1 | Niedrig |
| `Pages/MapPage.xaml` | Umbau | 3 | Mittel |
| `Pages/MapPage.xaml.cs` | Anpassen | 3 | Niedrig |
| `Pages/SettingsPage.xaml` | **NEU** | 4 | Hoch |
| `Pages/SettingsPage.xaml.cs` | **NEU** | 4 | Hoch |
| `Resources/ViewModels/SettingsPageViewModel.cs` | **NEU** | 4 | Hoch |
| `Resources/ViewModels/ProfilePageViewModel.cs` | Erweitern | 4+6 | Mittel |
| `Resources/ViewModels/RegisterPageViewModel.cs` | Erweitern | 6 | Kritisch |
| `Resources/ViewModels/UserProfilePageViewModel.cs` | Erweitern | 5 | Mittel |
| `Resources/ViewModels/DetailPageViewModel.cs` | Erweitern | 5+6 | Mittel |
| `Resources/ViewModels/AddPageViewModel.cs` | Erweitern | 3+6 | Mittel |
| `Resources/Controls/StarRatingControl.xaml` | Erweitern | 2 | Hoch |
| `Resources/Controls/StarRatingControl.xaml.cs` | Erweitern | 2 | Hoch |
| `Resources/Controls/BellControl.xaml` | Umbau | 2 | Niedrig |
| `Resources/Controls/EmptyStateView.xaml` | **NEU** | 3 | Mittel |
| `Resources/Controls/EmptyStateView.xaml.cs` | **NEU** | 3 | Mittel |
| `Resources/Controls/VerifiedBadge.xaml` | **NEU** | 5 | Mittel |
| `Resources/Controls/VerifiedBadge.xaml.cs` | **NEU** | 5 | Mittel |
| `Resources/Models/User.cs` | Erweitern | 5+6 | Kritisch |
| `Resources/Models/Report.cs` | **NEU** | 5 | Hoch |
| `Services/AuthService.cs` | Erweitern | 5+6 | Kritisch |
| `Services/FeedbackService.cs` | **NEU** | 4 | Mittel |
| `Services/ModerationService.cs` | **NEU** | 5 | Hoch |
| `Services/NotificationService.cs` | Anpassen | – | Niedrig |
| `Routes.cs` | Erweitern | 4 | Hoch |
| `AppShell.xaml.cs` | Erweitern | 4 | Hoch |
| `MauiProgram.cs` | Erweitern | 4+5 | Hoch |
| `App.xaml.cs` | Anpassen | 4 | Mittel |
| `Platforms/iOS/iOSHapticFeedback.cs` | Erweitern | 6 | Niedrig |

**Summe:**
- **10 neue Dateien** (Pages, ViewModels, Controls, Models, Services)
- **33 bestehende Dateien** bearbeitet
- **Kritische DSGVO-Dateien:** RegisterPage, User.cs, AuthService.cs

---

## Supabase SQL-Migrationen (Übersicht)

| Migration | Phase | Tabelle | Änderung |
|-----------|-------|---------|---------|
| 001_add_user_verification | 5 | `users` | `is_verified`, `is_public`, `is_banned`, `banned_reason`, `banned_at` |
| 002_create_reports | 5 | `reports` | Neue Tabelle mit RLS |
| 003_ban_rls_policy | 5 | `entries` | Policy: Gebannte Nutzer keine Inserts |
| 004_gdpr_fields | 6 | `users` | `terms_accepted_at`, `privacy_accepted_at`, `deletion_requested_at` |
| 005_delete_user_function | 6 | – | PostgreSQL-Funktion `delete_user_data()` |
| 006_soft_delete_user | 6 | `users` | `is_completed` Feld für Soft-Delete |

---

## Phasen-Übersicht & Zeitschätzung

| Phase | Inhalt | Risiko | Dateien | Geschätzt |
|-------|--------|--------|---------|-----------|
| **1** | Styles, Hintergrund, Typografie | Niedrig | 14 | 2–3h |
| **2** | Frame→Border, Star Rating | Mittel | 8 | 2–3h |
| **3** | MapPage, Empty States, Performance | Niedrig | 7 | 2–3h |
| **4** | Settings, Feedback, Dark Mode | Mittel | 12 | 4–6h |
| **5** | Security, Trust, Moderation | Hoch | 11 | 4–6h |
| **6** | DSGVO, Haptic, Final Polish | Hoch | 9 | 4–6h |
| **7** | FAQ dynamisch, ChatPage Lazy Loading, AddPage Redesign, ChatViewPage Fix | Mittel | 10 | 3–5h |
| **8** | App-Performance, Dead Locks, Code-Qualität (LETZTE PHASE) | Hoch | 15+ | 6–8h |

**Empfohlene Reihenfolge:** Phase 1 → Build → Phase 2 → Build → Phase 3 → Build → Phase 4 → Build → Phase 5 → Build → Phase 6 → Build → Phase 7 → Build → Phase 8 → Build → Abschluss-QA

---

*Dokument basierend auf Vollanalyse aller NBH-Quelldateien (Stand 06.04.2026). Keine Datei wurde durch diese Planung verändert.*

---

---

# ERWEITERUNGEN – Ergänzungsplan (06.04.2026)

> **Basis:** Zweite Analyse-Runde mit Fokus auf ChatPage, ChatViewPage, FAQPage, EntryCard, CustomTabBar und AddPage
> **Neue Themen:** Globales Anthrazit-Farbkonzept, CustomTabBar-Anpassungen, AddPage Redesign, ChatViewPage Ecken-Fix, ChatPage Lazy Loading, FAQ dynamisch, Performance-Phase

---

## Neue Probleme – Ergänzung zur Ist-Zustand-Tabelle

| Problem | Betroffene Datei(en) | Schwere |
|---------|---------------------|---------|
| Farbkonzept inkonsistent: Mint vs. Türkis auf gleicher Seite | AddPage.xaml, ChatPage.xaml, ChatViewPage.xaml | Hoch |
| CustomTabBar Icons zu klein (22px), Hintergrund zu dunkel | CustomTabBar.xaml | Mittel |
| FAQ Icon in CustomTabBar lila statt harmonisch | CustomTabBar.xaml, CustomTabBar.xaml.cs | Niedrig |
| ProfilePage: „Passwort"-Button-Text unpräzise | ProfilePage.xaml | Niedrig |
| ChatViewPage: Graue Ecken hinter abgerundeten Chat-Karten | ChatViewPage.xaml | Hoch |
| ChatPage: Bulk-Loading aller Nachrichten auf Main Thread → App friert ein | ChatPageViewModel.cs | Kritisch |
| ChatPage: Nach dem Laden nicht automatisch ans Ende gescrollt | ChatPage.xaml.cs | Mittel |
| FAQPage: 1405 Zeilen XAML mit hardcoded Inhalten trotz vorhandenem ViewModel | FAQPage.xaml | Hoch |
| EntryCard: Bild rechts statt links, kein Platzhalter-Bild wenn kein Event-Bild | EntryCard.xaml | Mittel |
| AddPage Header-Titel „NBH Events" statt „CareCommunity / Events" | AddPage.xaml | Niedrig |
| AddPage: Filter unterscheidet nicht zwischen Einzel- und Gruppen-Events | AddPage.xaml, AddPageViewModel.cs | Mittel |
| Gesamte App: Fire-and-Forget async Calls können zu Dead Locks führen | Alle ViewModels | Kritisch |
| Gesamte App: Unnötige StackLayouts statt Grid/FlexLayout | Mehrere XAML-Dateien | Mittel |
| FAQPage: kein CommunityToolkit-Expander-Update (bereits verwendet, aber prüfen) | FAQPage.xaml | Niedrig |

---

## Neue Dateien – Ergänzung zur Übersicht

| Datei | Typ | Phase |
|-------|-----|-------|
| `Resources/Models/FAQCategory.cs` | Datenmodell (View-Wrapper) | 7 |
| `Services/FAQService.cs` | Service | 7 |
| `Services/PerformanceService.cs` | Service (Logging/Monitoring) | 8 |

---

## Ergänzungen zu Phase 1 – Anthrazit-Farbkonzept (GLOBAL)

**Ziel:** Das bisherige Mint/Türkis-Farbschema wird durch ein modernes Anthrazit-basiertes Design ersetzt. `#4b4b4b` (Anthrazit) wird die neue Primärfarbe der Oberfläche, `#e4f8f5` (helles Eisgrün) wird als heller Akzent verwendet.

---

### E1.1 `Resources/Styles/Colors.xaml` – Vollständige Farbpaletten-Revision

**Was entfernt / ersetzt wird:**

| Alter Token | Alter Wert | Neuer Token | Neuer Wert | Verwendung |
|-------------|-----------|-------------|-----------|-----------|
| `PageBackgroundStart` | `#b7f0e9` (Mint) | `PageBackgroundStart` | `#e4f8f5` | Seiten-Gradient oben |
| `PageBackgroundMid` | `#15be9e` (Türkis) | `PageBackgroundMid` | `#6b6b6b` | Seiten-Gradient Mitte |
| `PageBackgroundEnd` | `#b7f0e9` (Mint) | `PageBackgroundEnd` | `#4b4b4b` | Seiten-Gradient unten |
| `PageBackgroundStartDark` | `#0d2e2a` | `PageBackgroundStartDark` | `#2a2a2a` | Dark-Mode oben |
| `PageBackgroundMidDark` | `#0a4a3d` | `PageBackgroundMidDark` | `#1e1e1e` | Dark-Mode Mitte |
| `PageBackgroundEndDark` | `#0d2e2a` | `PageBackgroundEndDark` | `#121212` | Dark-Mode unten |

**Neue Anthrazit-Skala (Helligkeitsstufen):**
```xml
<!-- Anthrazit-Skala: von hell nach dunkel -->
<Color x:Key="AnthrazitUltraLight">#f0f0f0</Color>   <!-- Fast weiß, für Karten auf dunklem BG -->
<Color x:Key="AnthrazitLight">#8a8a8a</Color>          <!-- Helles Grau -->
<Color x:Key="AnthrazitMid">#6b6b6b</Color>            <!-- Mittelton für Gradient -->
<Color x:Key="AnthrazitBase">#4b4b4b</Color>           <!-- Kern-Anthrazit -->
<Color x:Key="AnthrazitDark">#333333</Color>           <!-- Dunkleres Anthrazit -->
<Color x:Key="AnthrazitDeep">#1e1e1e</Color>           <!-- Fast Schwarz -->

<!-- Akzent-Farbe (ersetzt Mint/Türkis als Highlight) -->
<Color x:Key="AccentLight">#e4f8f5</Color>             <!-- Heller Eisgrün-Akzent -->
<Color x:Key="AccentMid">#a8e6df</Color>               <!-- Mittlerer Eisgrün-Akzent -->
<Color x:Key="AccentPrimary">#00897b</Color>           <!-- Primärfarbe bleibt (Teal) -->
<Color x:Key="AccentBright">#00d2a8</Color>            <!-- Heller Teal-Akzent -->

<!-- Oberflächen-Farben (angepasst auf Anthrazit-Basis) -->
<Color x:Key="SurfaceLight">#FFFFFF</Color>
<Color x:Key="SurfaceDark">#2d2d2d</Color>
<Color x:Key="SurfaceElevatedLight">#f7f7f7</Color>
<Color x:Key="SurfaceElevatedDark">#383838</Color>

<!-- Card-Hintergrund auf Anthrazit-Seite -->
<Color x:Key="CardOnAnthrazit">#FFFFFF</Color>         <!-- Weiße Karten auf Anthrazit -->
<Color x:Key="CardOnAnthrazitDark">#2d2d2d</Color>    <!-- Dunkel-Karten im Dark Mode -->
```

---

### E1.2 `Resources/Styles/Styles.xaml` – Seiten-Gradient mit neuem Farbkonzept

**Der globale ContentPage-Style (aus Phase 1.2) wird auf das Anthrazit-Schema aktualisiert:**

```xml
<Style TargetType="ContentPage" ApplyToDerivedTypes="True">
    <Setter Property="Background">
        <Setter.Value>
            <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                <!-- Hell oben (Eisgrün), dann Übergang zu Anthrazit -->
                <GradientStop Color="{AppThemeBinding Light={StaticResource AccentLight},
                                                      Dark={StaticResource AnthrazitDeep}}"
                              Offset="0.0"/>
                <GradientStop Color="{AppThemeBinding Light={StaticResource AnthrazitMid},
                                                      Dark={StaticResource AnthrazitDark}}"
                              Offset="0.55"/>
                <GradientStop Color="{AppThemeBinding Light={StaticResource AnthrazitBase},
                                                      Dark={StaticResource AnthrazitDeep}}"
                              Offset="1.0"/>
            </LinearGradientBrush>
        </Setter.Value>
    </Setter>
</Style>
```

**Visuelles Ergebnis:**
- Oben: Zartes Eisgrün `#e4f8f5` → geht fließend über
- Mitte: Mittelgrau `#6b6b6b`
- Unten: Anthrazit `#4b4b4b`
- Karten (weiß) heben sich stark vom Anthrazit-Hintergrund ab → Hoher Kontrast, professionelle Optik

**Button-Primärfarbe bleibt grün** (`#00897b` / `#00d2a8`) – das ist ein bewusstes Kontrast-Element auf dem Anthrazit-Hintergrund.

---

### E1.3 Alle Pages – Inline-Gradienten auf neue Farben aktualisieren

Alle individuellen Page-Gradienten werden **in Phase 1.3 entfernt** (bereits geplant). Falls noch Seiten-spezifische Farben existieren, folgen sie der neuen Anthrazit-Skala:

| Seite | Aktuell | Neu |
|-------|---------|-----|
| `ChatViewPage.xaml` | `#52c8da → White → #52c8da` | **Global Style** (Anthrazit) |
| `ChatPage.xaml` | `#b7f0e9 → #15be9e` | **Global Style** |
| `FAQPage.xaml` | `#52d8ff → White` | **Global Style** |
| `ChatPage.xaml` Header-Bar | `#676767 → #a9a9a9` | `AnthrazitDark → AnthrazitMid` |
| `ChatPage.xaml` Send-Button | `#35c4d8 → #5a9ca5` | `AccentBright → AccentPrimary` |
| `FAQPage.xaml` Header-Border | `#0036af → #00aeff` | `AnthrazitBase → AnthrazitMid` (oder Teal behalten) |

---

## Ergänzungen zu Phase 1 – CustomTabBar Anpassungen

---

### E1.4 `Resources/Controls/CustomTabBar.xaml` – Icons vergrößern

**Aktuell:** Alle Icons: `HeightRequest="22"` + `WidthRequest="22"`

**Neu:** Alle Icons: `HeightRequest="28"` + `WidthRequest="28"`

**Betroffene Zeilen:** Alle 5 `<Image HeightRequest="22" WidthRequest="22">` Elemente im `TabItemsGrid`.

**Anpassung der TabBar-Höhe:** Da Icons größer werden, die Gesamt-TabBar-Höhe von `64px` auf `72px` erhöhen:
```xml
<!-- ALT: -->
<Border HeightRequest="64" ...>

<!-- NEU: -->
<Border HeightRequest="72" ...>
```

Und den Gesamt-Grid entsprechend: von `HeightRequest="88"` auf `HeightRequest="96"`.

---

### E1.5 `Resources/Controls/CustomTabBar.xaml` – Hintergrundfarbe Anthrazit

**Aktuell:**
```xml
Background Color="#F0121628"  <!-- Sehr dunkles Blau-Schwarz, kaum sichtbar -->
Stroke "#15FFFFFF"
```

**Neu (Anthrazit mit Transparenz):**
```xml
Background Color="#CC4b4b4b"   <!-- Anthrazit mit 80% Opazität = satte Anthrazit-TabBar -->
Stroke Color="#334b4b4b"       <!-- Subtiler Rand, leicht heller als Hintergrund -->
```

**Visuell:** Die TabBar wird zu einem klaren, modernen Anthrazit-Balken, der sich als eigenständiges Element vom Seiten-Gradient absetzt. Der Glasmorphismus-Effekt bleibt erhalten durch die leichte Transparenz (`CC` = 80%).

---

### E1.6 `Resources/Controls/CustomTabBar.xaml` – Slider Farbe aktualisieren

**Aktuell:** Slider hat Teal-Glow (`#1F00D4AA`, `#4000D4AA`).

**Neu:** Slider nimmt Akzentfarbe Eisgrün:
```xml
<!-- Slider Background: -->
Background Color="#3Ae4f8f5"  <!-- AccentLight mit 23% Alpha -->
Stroke "#80e4f8f5"            <!-- AccentLight mit 50% Alpha -->
```

**ActiveDot:**
```xml
<!-- ALT: Color="#00D4AA" (Teal) -->
<!-- NEU: -->
Color="{StaticResource AccentLight}"  <!-- #e4f8f5 -->
```

---

### E1.7 `Resources/Controls/CustomTabBar.xaml.cs` – FAQ Icon Farbe & InactiveColor anpassen

**Aktuell:**
```csharp
private static readonly Color[] TabColors = new[]
{
    Color.FromArgb("#00D4AA"),  // Tab 0: Profil  – Teal
    Color.FromArgb("#378ADD"),  // Tab 1: Karte   – Blau
    Color.FromArgb("#EF9F27"),  // Tab 2: Neu     – Amber
    Color.FromArgb("#D4537E"),  // Tab 3: Chat    – Pink
    Color.FromArgb("#7F77DD"),  // Tab 4: FAQ     – Lila ← PROBLEM
};
private const string InactiveColor = "#4A5178";  // Blau-Grau
```

**Neu – FAQ Icon harmonisch in die Farbpalette integrieren:**
```csharp
private static readonly Color[] TabColors = new[]
{
    Color.FromArgb("#e4f8f5"),  // Tab 0: Profil  – Eisgrün (neuer Akzent)
    Color.FromArgb("#378ADD"),  // Tab 1: Karte   – Blau (bleibt)
    Color.FromArgb("#00d2a8"),  // Tab 2: Neu     – Teal-Grün
    Color.FromArgb("#D4537E"),  // Tab 3: Chat    – Pink (bleibt)
    Color.FromArgb("#00897b"),  // Tab 4: FAQ     – Dunkles Teal (harmonisch mit Primärfarbe)
};
private const string InactiveColor = "#8a8a8a";  // Anthrazit-Hellton statt Blau-Grau
```

**Begründung:** Das FAQ-Icon war mit Lila (`#7F77DD`) ein Ausreißer aus der Teal/Eisgrün-Farbwelt. Das neue Dunkelteal `#00897b` (die Primärfarbe der App) verbindet alle Icons mit der Markenfarbe.

---

## Ergänzungen zu Phase 2 – ProfilePage Passwort-Button

---

### E2.1 `Pages/ProfilePage.xaml` – Passwort-Button Text präzisieren

**Aktuell:** Der Button auf der ProfilePage (analysiert als `VerifyQualificationsFrame` und ein weiterer Button für Passwort) hat möglicherweise keinen klaren Text.

**Betroffene Zeile:** Der Ghost-Button für Passwort-Änderung (analysiert als zweiter Ghost-Button neben „Verifizieren").

**Was geändert wird:**
```xml
<!-- ALT (unklarer Button-Text): -->
<Label Text="Passwort" .../>  <!-- oder ähnlich vage -->

<!-- NEU (eindeutig): -->
<Label Text="🔑  Passwort ändern" .../>
```

**Zusätzlich:** Der Button-Text wird mit einem Schloss-Icon versehen. Das Icon macht sofort klar was die Funktion ist, auch ohne den Text vollständig zu lesen. In MAUI kann das als Unicode-Emoji (`🔑`) oder als `ImageButton` mit `Source="lock_icon.png"` umgesetzt werden.

**Zugehöriger Command:** `ResetPasswordCommand` in `ProfilePageViewModel.cs` bleibt unverändert – nur der Button-Text in der XAML ändert sich.

---

## Ergänzungen zu Phase 2 – ChatViewPage graue Ecken Fix

---

### E2.2 `ChatViewPage.xaml` – Analyse der grauen Ecken

**Ursache (aus der Analyse ermittelt):**

Das Chat-Item-Layout ist:
```xml
<Frame BorderColor="Black" Padding="7" CornerRadius="20" HasShadow="False">
    <Grid>
        <Grid.Background>
            <LinearGradientBrush>
                <GradientStop Color="#e3e3e3" Offset="0"/>
                <GradientStop Color="White" Offset="0.5"/>
                <GradientStop Color="#e3e3e3" Offset="1"/>
            </LinearGradientBrush>
        </Grid.Background>
        <!-- Profilbild (Frame CornerRadius="35") -->
        <!-- Textinhalte -->
    </Grid>
</Frame>
```

**Das Problem:** Der äußere `Frame` hat `BackgroundColor` nicht explizit gesetzt → MAUI setzt einen Default (Weiß oder Hell-Grau je nach Theme). Der innere `Grid` hat einen Gradient-Background. Die abgerundeten Ecken des `Frame` klippen den Inhalt, aber **da der Frame selbst einen Hintergrund hat, sieht man in den Ecken den Frame-Hintergrund statt das Gradient** – das erzeugt die grauen/weißen Ecken.

**Zweites Problem:** Das Profilbild-Frame (`Frame CornerRadius="35"`) sitzt direkt neben dem Text. Wenn das Bild nicht circular ist oder der Frame es nicht korrekt klipt, sieht man eckige Artefakte.

**Fix A – Frame durch Border ersetzen und Gradient direkt auf Border:**

```xml
<!-- ALT: -->
<Frame BorderColor="Black" CornerRadius="20" HasShadow="False" Padding="7">
    <Grid>
        <Grid.Background>...</Grid.Background>
    </Grid>
</Frame>

<!-- NEU: -->
<Border Padding="7"
        Stroke="#4b4b4b"
        StrokeThickness="1">
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="20"/>
    </Border.StrokeShape>
    <Border.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
            <!-- Angepasste Farben auf Anthrazit-Schema: -->
            <GradientStop Color="{StaticResource AnthrazitUltraLight}" Offset="0"/>
            <GradientStop Color="White" Offset="0.5"/>
            <GradientStop Color="{StaticResource AnthrazitUltraLight}" Offset="1"/>
        </LinearGradientBrush>
    </Border.Background>
    <!-- Grid ohne eigenes Background (Border übernimmt es) -->
    <Grid>
        <!-- Inhalte -->
    </Grid>
</Border>
```

**Warum das den Fehler behebt:** `Border` wendet den `Background` mit `StrokeShape` zusammen an – die Ecken werden korrekt gemäß der `RoundRectangle` Form abgeschnitten. Es gibt keinen separaten Frame-Hintergrund mehr, der in den Ecken durchscheinen könnte.

**Fix B – Profilbild circular machen:**

Das Profilbild ist aktuell in `Frame CornerRadius="35"` (70x70px). Dieser Frame muss zu `Border` konvertiert werden:

```xml
<!-- ALT: -->
<Frame WidthRequest="70" HeightRequest="70" CornerRadius="35"
       Padding="0" IsClippedToBounds="True">
    <ff:CachedImage Source="{Binding PartnerImageUrl}" .../>
</Frame>

<!-- NEU: -->
<Border WidthRequest="70" HeightRequest="70"
        Padding="0"
        StrokeThickness="2"
        Stroke="{StaticResource AccentLight}">
    <Border.StrokeShape>
        <Ellipse/>  <!-- Perfekter Kreis -->
    </Border.StrokeShape>
    <ff:CachedImage Source="{Binding PartnerImageUrl}"
                    Aspect="AspectFill"
                    WidthRequest="70" HeightRequest="70"/>
</Border>
```

**`Ellipse` als StrokeShape** statt `RoundRectangle CornerRadius="35"` – das garantiert einen perfekten Kreis ohne Eckartefakte auf allen Plattformen.

---

## Phase 7 – FAQ Dynamisch, ChatPage Lazy Loading, AddPage Redesign

**Ziel:** FAQ-Inhalte aus der Datenbank laden (nicht mehr 1405 Zeilen hardcoded XAML). ChatPage lädt Nachrichten asynchron im Hintergrund und scrollt danach ans Ende. AddPage bekommt ein überarbeitetes Layout mit zwei-zeiliger Überschrift, verbesserter EntryCard und erweitertem Filter.
**Risiko:** Mittel
**Geschätzte Dateien:** 10

---

### 7.1 `FAQPage.xaml` – Von hardcoded Expanders zu dynamischer CollectionView

**Ist-Zustand (analysiert):**
- `FAQPage.xaml`: 1405 Zeilen XAML mit statischen `toolkit:Expander`-Elementen, verschachtelt in fixen Kategorien
- `FAQPageViewModel.cs`: **Bereits vorhanden!** Lädt FAQ-Items aus Supabase-Tabelle `faq` via `LoadFAQCommand`, gruppiert sie in `FAQCategory`-Objekte nach `category_title`
- `FAQRow.cs`: Supabase-Mapping auf `faq`-Tabelle (id, category_title, subtitle, content)
- **Problem:** Das ViewModel existiert und funktioniert, aber die View ignoriert es und zeigt stattdessen den hartcodierten XAML-Content

**Lösung:** `FAQPage.xaml` wird komplett neu aufgebaut – nur die äußere Shell (CustomTabBar, Page-Background, Header) bleibt. Der gesamte Inhaltsbereich wird durch eine dynamische CollectionView mit Expander-Template ersetzt.

**Neue Struktur `FAQPage.xaml`:**

```xml
<ContentPage xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"
             ...>
    <Grid RowDefinitions="Auto,*,Auto">

        <!-- Row 0: Header (bleibt erhalten, ggf. Farbe auf Anthrazit angepasst) -->
        <Border Grid.Row="0" HeightRequest="150" Padding="20,40,20,20">
            <Border.Background>
                <LinearGradientBrush>
                    <GradientStop Color="{StaticResource AnthrazitBase}" Offset="0"/>
                    <GradientStop Color="{StaticResource AnthrazitDark}" Offset="1"/>
                </LinearGradientBrush>
            </Border.Background>
            <Label Text="FAQ" TextColor="White"
                   Style="{StaticResource DisplayStyle}"
                   HorizontalOptions="Center" VerticalOptions="Center"/>
        </Border>

        <!-- Row 1: Dynamische FAQ-Liste -->
        <CollectionView Grid.Row="1"
                        ItemsSource="{Binding Categories}"
                        Margin="16,8">

            <!-- Empty State wenn FAQ noch lädt oder leer -->
            <CollectionView.EmptyView>
                <controls:EmptyStateView
                    Title="FAQ wird geladen..."
                    IconSource="faq_placeholder.png"/>
            </CollectionView.EmptyView>

            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="models:FAQCategory">

                    <!-- Kategorie-Container -->
                    <Border Margin="0,6" Padding="0">
                        <Border.StrokeShape>
                            <RoundRectangle CornerRadius="20"/>
                        </Border.StrokeShape>

                        <!-- Expander pro Kategorie -->
                        <toolkit:Expander>
                            <toolkit:Expander.Header>
                                <Grid ColumnDefinitions="*,Auto" Padding="18,16">
                                    <Label Text="{Binding CategoryTitle}"
                                           Style="{StaticResource TitleStyle}"
                                           VerticalOptions="Center"/>
                                    <!-- Rotierendes Chevron-Icon -->
                                    <Image Grid.Column="1"
                                           Source="chevron_down.png"
                                           WidthRequest="20" HeightRequest="20"
                                           VerticalOptions="Center">
                                        <Image.Triggers>
                                            <DataTrigger TargetType="Image"
                                                         Binding="{Binding Source={RelativeSource AncestorType={x:Type toolkit:Expander}}, Path=IsExpanded}"
                                                         Value="True">
                                                <Setter Property="Rotation" Value="180"/>
                                            </DataTrigger>
                                        </Image.Triggers>
                                    </Image>
                                </Grid>
                            </toolkit:Expander.Header>

                            <!-- FAQ-Items innerhalb einer Kategorie -->
                            <VerticalStackLayout Padding="0,0,0,8">
                                <CollectionView ItemsSource="{Binding Items}">
                                    <CollectionView.ItemTemplate>
                                        <DataTemplate x:DataType="models:FAQRow">
                                            <!-- Sub-Expander pro FAQ-Item -->
                                            <toolkit:Expander Margin="12,4">
                                                <toolkit:Expander.Header>
                                                    <Grid ColumnDefinitions="*,Auto" Padding="12,12">
                                                        <Label Text="{Binding Subtitle}"
                                                               Style="{StaticResource BodyLargeStyle}"
                                                               FontAttributes="Bold"
                                                               VerticalOptions="Center"/>
                                                        <Label Grid.Column="1" Text="+"
                                                               Style="{StaticResource TitleStyle}"
                                                               TextColor="{StaticResource AccentPrimary}"
                                                               VerticalOptions="Center"/>
                                                    </Grid>
                                                </toolkit:Expander.Header>
                                                <Label Text="{Binding Content}"
                                                       Style="{StaticResource BodyStyle}"
                                                       Padding="12,4,12,16"
                                                       TextColor="#555555"
                                                       LineBreakMode="WordWrap"/>
                                            </toolkit:Expander>
                                        </DataTemplate>
                                    </CollectionView.ItemTemplate>
                                </CollectionView>
                            </VerticalStackLayout>
                        </toolkit:Expander>
                    </Border>

                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>

        <!-- Row 2: CustomTabBar -->
        <controls:CustomTabBar Grid.Row="2" SelectedIndex="4"/>
    </Grid>
</ContentPage>
```

**Vorteile:**
- FAQ-Inhalt wird von Supabase geladen → Admin kann Inhalte jederzeit ohne App-Update ändern
- Von 1405 Zeilen XAML auf ~80 Zeilen Template-XAML
- Neue Kategorien/Fragen erscheinen automatisch nach Server-Update
- Kein App Store Update für FAQ-Änderungen nötig

---

### 7.2 `Resources/Models/FAQCategory.cs` – Neue Datei (View-Wrapper)

Das vorhandene ViewModel gruppiert bereits – der `FAQCategory`-Wrapper fehlt jedoch als formale Klasse:

```csharp
// NEUE DATEI: Resources/Models/FAQCategory.cs
public class FAQCategory
{
    public string CategoryTitle { get; set; } = "";
    public List<FAQRow> Items { get; set; } = new();
}
```

**Hinweis:** Diese Klasse existiert möglicherweise bereits als anonymer Typ im ViewModel. Falls ja, wird sie in eine eigene Datei extrahiert für saubere Typisierung im XAML-DataTemplate (`x:DataType="models:FAQCategory"`).

---

### 7.3 `Resources/ViewModels/FAQPageViewModel.cs` – Verfeinern

**Aktuell:** Lädt FAQ-Rows, gruppiert zu Categories. Kein Ladezustand.

**Ergänzungen:**
```csharp
[ObservableProperty]
private bool _isLoading = false;

[ObservableProperty]
private string _errorMessage = "";

// In LoadFAQAsync():
IsLoading = true;
try {
    var response = await _supabaseClient.From<FAQRow>()
        .Order(f => f.CategoryTitle, Ordering.Ascending)
        .Get();
    // Gruppierung (bestehende Logik bleibt)
    // WICHTIG: ConfigureAwait(false) für Performance (→ Phase 8)
} catch (Exception ex) {
    ErrorMessage = "FAQ konnte nicht geladen werden.";
} finally {
    IsLoading = false;
}
```

---

### 7.4 `FAQPage.xaml.cs` – Code-Behind stark vereinfachen

**Aktuell:** 110 Zeilen mit manueller Expander-Steuerung (`OnMainExpanderExpanded`, `OnSubExpanderExpanded`, `FindAncestorLayoutWithExpanders`).

**Nach dem Umbau:** Da die Expander jetzt in einer CollectionView mit DataTemplate sitzen, entfällt die manuelle Steuerung. Das gegenseitige Schließen von Expandern muss entweder:
- **Option A:** Im ViewModel über eine `SelectedCategory`-Property + `IsExpanded`-BindableProperty am Expander gelöst werden
- **Option B:** `CommunityToolkit.Maui`'s `Expander` hat keine eingebaute Gegenseitig-Schließen-Logik für CollectionView-Kontext → Einfachste Lösung: Mehrere Expander dürfen gleichzeitig offen sein (Nutzer-freundlicher, weniger Code)

**Empfehlung:** Option B – Das gleichzeitige Öffnen mehrerer Expander ist intuitiver und entfernt komplexen Code. Code-Behind wird auf:
```csharp
public partial class FAQPage : ContentPage
{
    public FAQPage(FAQPageViewModel vm) {
        InitializeComponent();
        BindingContext = vm;
    }
    protected override void OnAppearing() {
        base.OnAppearing();
        if (BindingContext is FAQPageViewModel vm)
            vm.LoadFAQCommand.Execute(null);
    }
}
```

---

### 7.5 Supabase: `faq`-Tabelle um `sort_order`-Spalte erweitern

```sql
ALTER TABLE faq ADD COLUMN sort_order INTEGER DEFAULT 0;
-- Damit FAQ-Items in definierter Reihenfolge angezeigt werden
-- Admin kann sort_order im Supabase Dashboard setzen
```

```csharp
// In FAQPageViewModel.cs – Sortierung nach sort_order:
.Order(f => f.SortOrder, Ordering.Ascending)
```

---

### 7.6 `ChatPageViewModel.cs` – Lazy Loading & Background Thread

**Aktuell (Analyse-Ergebnis):**
```csharp
public async Task LoadMessagesAsync(Guid chatId)
{
    var msgResp = await _supabaseClient
        .From<ChatMessage>()
        .Where(m => m.ChatId == chatId)
        .Order(m => m.CreatedAt, Ordering.Ascending)
        .Get();  // ← Bulk-Fetch ALLER Nachrichten

    Messages.Clear();
    foreach (var msg in msgResp.Models)
        Messages.Add(msg);  // ← Sequenzielles Hinzufügen auf Main Thread
}
```

**Probleme:**
1. Alle Nachrichten werden auf einmal geladen (keine Paginierung)
2. Das `foreach`-Loop mit `Messages.Add()` ist teuer bei vielen Nachrichten (jedes Add löst PropertyChanged aus)
3. Kein explizites Scrollen ans Ende nach dem Laden

**Neue Implementierung:**

```csharp
// Konstante für initiale Ladegröße:
private const int InitialMessageCount = 40;
private const int PageSize = 20;
private bool _isLoadingMore = false;
private int _currentOffset = 0;

public async Task LoadMessagesAsync(Guid chatId)
{
    IsLoading = true;

    // ► Auf Background-Thread ausführen (nicht Main Thread blockieren):
    var messages = await Task.Run(async () =>
    {
        var response = await _supabaseClient
            .From<ChatMessage>()
            .Where(m => m.ChatId == chatId)
            .Order(m => m.CreatedAt, Ordering.Descending)  // Neueste zuerst
            .Limit(InitialMessageCount)                      // Nur die letzten 40
            .Get()
            .ConfigureAwait(false);

        // Reihenfolge umkehren für chronologische Anzeige:
        return response.Models?.OrderBy(m => m.CreatedAt).ToList()
               ?? new List<ChatMessage>();
    });

    // ► UI-Update auf Main Thread, aber als Bulk-Replace (nur ein PropertyChanged):
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
        Messages.Clear();
        foreach (var msg in messages)
            Messages.Add(msg);

        IsLoading = false;
        _currentOffset = messages.Count;
    });
}

// ► Ältere Nachrichten nachladen (Lazy Loading nach oben scrollen):
public async Task LoadMoreMessagesAsync()
{
    if (_isLoadingMore) return;
    _isLoadingMore = true;

    var olderMessages = await Task.Run(async () =>
    {
        return await _supabaseClient
            .From<ChatMessage>()
            .Where(m => m.ChatId == ChatId)
            .Order(m => m.CreatedAt, Ordering.Descending)
            .Range(_currentOffset, _currentOffset + PageSize - 1)
            .Get()
            .ConfigureAwait(false);
    });

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
        // Alte Nachrichten oben einfügen:
        foreach (var msg in olderMessages.Models.OrderBy(m => m.CreatedAt))
            Messages.Insert(0, msg);
        _currentOffset += olderMessages.Models.Count;
        _isLoadingMore = false;
    });
}
```

---

### 7.7 `ChatPage.xaml.cs` – Scroll-to-Bottom nach Laden

**Aktuell:** `Messages.CollectionChanged` scrollt bei `Add`-Events – aber nur wenn eine neue Nachricht hinzukommt, nicht nach dem initialen Bulk-Laden.

**Ergänzung in `ChatPage.xaml.cs`:**

```csharp
// Nach dem Laden: automatisch ans Ende scrollen
protected override async void OnAppearing()
{
    base.OnAppearing();
    // Kurze Verzögerung damit CollectionView rendern kann:
    await Task.Delay(150);
    ScrollToLastMessage();
}

private void ScrollToLastMessage()
{
    var vm = BindingContext as ChatPageViewModel;
    if (vm?.Messages?.Count > 0)
    {
        var lastMessage = vm.Messages[^1];  // C# 8+ Index-Syntax
        MessagesCollectionView.ScrollTo(
            lastMessage,
            position: ScrollToPosition.End,
            animate: false);  // Kein animate für initialen Scroll
    }
}
```

**Load-More beim Scrollen nach oben:**

```xml
<!-- In ChatPage.xaml, CollectionView: -->
<CollectionView x:Name="MessagesCollectionView"
                ItemsSource="{Binding Messages}"
                RemainingItemsThreshold="5"
                RemainingItemsThresholdReachedCommand="{Binding LoadMoreCommand}">
```

Das `RemainingItemsThreshold="5"` triggert `LoadMoreCommand` wenn der Nutzer auf 5 Items vom oberen Ende scrollt. Die neuen alten Nachrichten werden oben eingefügt ohne die aktuelle Scrollposition zu verlieren.

---

### 7.8 `ChatPage.xaml` – Ladezustand anzeigen

```xml
<!-- Über dem CollectionView, sichtbar während IsLoading: -->
<ActivityIndicator IsRunning="{Binding IsLoading}"
                   IsVisible="{Binding IsLoading}"
                   Color="{StaticResource AccentPrimary}"
                   VerticalOptions="Center"
                   HorizontalOptions="Center"/>
```

---

### 7.9 `AddPage.xaml` – Zwei-zeilige Überschrift

**Aktuell:**
```xml
<Label Text="NBH Events" FontSize="Large" .../>
```

**Neu:**
```xml
<VerticalStackLayout Spacing="0" HorizontalOptions="Start" VerticalOptions="Center">
    <Label Text="CareCommunity"
           Style="{StaticResource CaptionStyle}"
           TextColor="{StaticResource AccentLight}"
           LetterSpacing="2"
           CharacterSpacing="2"/>
    <Label Text="Events"
           Style="{StaticResource TitleLargeStyle}"
           TextColor="White"
           FontAttributes="Bold"/>
</VerticalStackLayout>
```

**Visuell:** „CareCommunity" klein und in AccentLight darüber, „Events" groß und weiß darunter – wie ein Marken-Logo-Effekt.

---

### 7.10 `AddPage.xaml` – Button-Farben auf Anthrazit-Schema

**Aktuell:** Alle Buttons (Event erstellen, Paginierung) haben Gradient `#00f3ff → #0066a5` (Cyan-Blau).

**Neu:** Buttons nutzen die neue Primärfarbe:
```xml
<!-- Event-erstellen Button: -->
<Border.Background>
    <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
        <GradientStop Color="{StaticResource AccentBright}" Offset="0"/>   <!-- #00d2a8 -->
        <GradientStop Color="{StaticResource AccentPrimary}" Offset="1"/> <!-- #00897b -->
    </LinearGradientBrush>
</Border.Background>

<!-- Paginierungs-Buttons: gleicher Gradient, weniger prominent (kleinere Buttons) -->
```

---

### 7.11 `AddPage.xaml` – Filter um Einzel/Gruppen erweitern

**Aktuell:** Ein `Picker` mit Optionen: „Alles", „Anfrage", „Angebot".

**Neu:** Zwei separate Filter-Zeilen oder ein erweiterter Picker:

**Option A – Zwei Picker (einfach):**
```xml
<Grid ColumnDefinitions="*,*" ColumnSpacing="8">
    <!-- Filter 1: Kategorie -->
    <Border Grid.Column="0">
        <Picker Title="Kategorie"
                ItemsSource="{Binding CategoryFilters}"
                SelectedItem="{Binding SelectedCategoryFilter}"/>
    </Border>
    <!-- Filter 2: Event-Typ -->
    <Border Grid.Column="1">
        <Picker Title="Typ"
                ItemsSource="{Binding EventTypeFilters}"
                SelectedItem="{Binding SelectedEventTypeFilter}"/>
    </Border>
</Grid>
```

**Option B – Chip-Filter (modern, empfohlen):**
```xml
<FlexLayout Direction="Row" Wrap="Wrap" JustifyContent="Start" Gap="8">
    <!-- Kategorie-Chips -->
    <Border x:Name="ChipAlle" ...><Label Text="Alle"/></Border>
    <Border x:Name="ChipAnfrage" ...><Label Text="Anfragen"/></Border>
    <Border x:Name="ChipAngebot" ...><Label Text="Angebote"/></Border>
    <!-- Separator -->
    <BoxView WidthRequest="1" BackgroundColor="#50FFFFFF" Margin="4,4"/>
    <!-- Event-Typ Chips -->
    <Border x:Name="ChipEinzel" ...><Label Text="Einzel"/></Border>
    <Border x:Name="ChipGruppe" ...><Label Text="Gruppe"/></Border>
</FlexLayout>
```

**`AddPageViewModel.cs` – Neue Filter-Properties:**
```csharp
[ObservableProperty]
private string _selectedEventTypeFilter = "Alle";  // "Alle" | "single" | "group"

// Filter-Anpassung in ApplyFilter():
private IEnumerable<EntryItem> ApplyFilter(IEnumerable<EntryItem> entries)
{
    // Bestehender Kategorie-Filter:
    if (SelectedCategoryFilter != "Alles")
        entries = entries.Where(e => e.Category == SelectedCategoryFilter);

    // NEU: Event-Typ Filter:
    if (SelectedEventTypeFilter == "single")
        entries = entries.Where(e => e.EventType == "single" || e.EventType == null);
    else if (SelectedEventTypeFilter == "group")
        entries = entries.Where(e => e.EventType == "group");

    return entries;
}
```

---

### 7.12 `Resources/Controls/EntryCard.xaml` – Bild auf die linke Seite

**Aktuell (Grid-Layout analysiert):**
```xml
<Grid ColumnDefinitions="6,*,Auto">
    <!-- Column 0: AccentBar (6px) -->
    <!-- Column 1: Content (Kategorie, Titel, Beschreibung, Ort) -->
    <!-- Column 2: Bild (80x80, rechts) -->
```

**Neu (Bild links, Inhalt nach rechts verschoben):**
```xml
<Grid ColumnDefinitions="Auto,6,*">
    <!-- Column 0: Bild (80x80, links) ODER Platzhalter -->
    <!-- Column 1: AccentBar (6px, jetzt in der Mitte als Trennlinie) -->
    <!-- Column 2: Content (nach rechts verschoben) -->
```

**Bild-Bereich (Column 0):**
```xml
<Border Grid.Column="0"
        WidthRequest="80" HeightRequest="80"
        Margin="10,10,0,10"
        StrokeThickness="0">
    <Border.StrokeShape>
        <RoundRectangle CornerRadius="12"/>
    </Border.StrokeShape>
    <!-- Bild wenn vorhanden: -->
    <ff:CachedImage
        Source="{Binding event_image_url}"
        Aspect="AspectFill"
        IsVisible="{Binding event_image_url,
                            Converter={StaticResource IsNotNullOrEmptyConverter}}"/>
    <!-- Platzhalter wenn KEIN Bild: -->
    <Image
        Source="placeholder_event.png"
        Aspect="AspectFill"
        IsVisible="{Binding event_image_url,
                            Converter={StaticResource IsNullOrEmptyConverter}}"/>
</Border>
```

**Platzhalter-Bild:** `placeholder_event.png` – bereits im Projekt vorhanden (das generische Platzhalter-Bild das auch für Profilbilder verwendet wird). In `Resources/Images/` liegt es – gleiche Datei kann wiederverwendet werden.

**Content-Bereich (Column 2):** Bleibt inhaltlich identisch, liegt jetzt in `Grid.Column="2"` statt `Grid.Column="1"`.

**AccentBar (Column 1):** Bleibt als vertikale 6px-Linie zwischen Bild und Text.

---

### ✅ Phase 7 Build-Test Checkpoint

**Was zu prüfen ist:**
1. FAQPage lädt Inhalte aus Supabase (nicht mehr hardcoded) – neuen FAQ-Eintrag in Supabase DB einfügen und prüfen ob er erscheint
2. FAQPage zeigt leere/Lade-Ansicht wenn keine FAQ-Daten vorhanden
3. ChatPage friert bei vielen Nachrichten nicht mehr ein
4. ChatPage scrollt nach dem Öffnen automatisch ans Ende
5. Ältere Nachrichten laden beim Scrollen nach oben nach
6. AddPage zeigt „CareCommunity / Events" als Titel
7. AddPage-Filter funktioniert für Kategorie UND Event-Typ
8. EntryCard zeigt Bild links mit Platzhalter wenn kein Bild vorhanden
9. ChatViewPage hat keine grauen Ecken mehr
10. Passwort-Button auf ProfilePage zeigt „🔑 Passwort ändern"

**Häufige Fehler:**
- `x:DataType="models:FAQCategory"` in DataTemplate → Namespace `models:` muss in FAQPage.xaml importiert sein
- `ConfigureAwait(false)` auf iOS: Bei Supabase-SDK sicherstellen dass der Client Thread-Safe ist
- `RemainingItemsThreshold` auf CollectionView: Funktioniert nur wenn die CollectionView scrollbar ist (genug Items vorhanden)
- Platzhalter-Bild lädt nicht: Prüfen ob `IsNullOrEmptyConverter` im ResourceDictionary registriert ist

---

## Phase 8 – App Performance, Dead Locks & Code-Qualität (LETZTE PHASE)

**Ziel:** Die gesamte App wird auf Performance, Threading-Sicherheit und Code-Qualität geprüft. Fire-and-Forget-Calls werden eliminiert. Unnötige Layout-Container werden durch effizientere Alternativen ersetzt. Der gesamte async-Code wird modernisiert und gegen Dead Locks abgesichert.
**Risiko:** Hoch – Änderungen an Core-Logik in allen ViewModels und Services
**Geschätzte Dateien:** 15+

---

### 8.1 Problem: Fire-and-Forget async Calls (Dead Lock Gefahr)

**Was ist Fire-and-Forget?**

```csharp
// GEFÄHRLICH – typisches Muster in der App:
async void OnButtonTapped(object sender, EventArgs e)
{
    await SomeOperationAsync();  // async void = Fire-and-Forget
}
```

`async void` Methoden sind Event-Handler-kompatibel, aber:
- Exceptions werden **nicht gefangen** und crashen die App unbemerkt
- Kein `await` von außen möglich → kein Error-Handling
- Kann zu Dead Locks führen wenn `SynchronizationContext` blockiert wird

**Wo in der App betroffen (typische Stellen):**

| Datei | Methode | Problem |
|-------|---------|---------|
| Alle XAML.cs | `OnButtonTapped(async void)` | Fire-and-Forget |
| `ChatPage.xaml.cs` | `OnSendButtonTapped` | Kein Exception-Handling |
| `ProfilePage.xaml.cs` | `OnSaveProfileTapped` | Exception schluckt Fehler |
| `AddPage.xaml.cs` | Pagination-Handler | Unbehandelter Fehlerfall |
| `NotificationService.cs` | Realtime-Callback | Async Void in Event |
| `PhoenixRealtimeService.cs` | `OnPostgresChanged` | Async in Event-Handler |

**Fix-Strategie:**

```csharp
// STATT async void:
private async void OnButtonTapped(object sender, EventArgs e)
{
    await SomeOperationAsync();  // Crash wenn Exception
}

// BESSER: Wrapper mit Try-Catch:
private async void OnButtonTapped(object sender, EventArgs e)
{
    try {
        await SomeOperationAsync();
    } catch (Exception ex) {
        // Logging + User-Feedback
        await Shell.Current.DisplayAlert("Fehler", "Aktion fehlgeschlagen.", "OK");
        System.Diagnostics.Debug.WriteLine($"[ERROR] {ex.Message}");
    }
}

// OPTIMAL: Via Command (Commands haben eingebautes Error-Handling):
// Statt Event-Handler → Command in ViewModel, Command hat CanExecute und Error-Propagation
```

**Globaler Exception-Handler in `App.xaml.cs`:**
```csharp
// Im App-Konstruktor:
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    var ex = e.ExceptionObject as Exception;
    System.Diagnostics.Debug.WriteLine($"[UNHANDLED] {ex?.Message}");
    // AppCenter.TrackError(ex);  // Bereits vorhanden
};

TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    System.Diagnostics.Debug.WriteLine($"[UNOBSERVED TASK] {e.Exception?.Message}");
    e.SetObserved();  // Verhindert App-Crash
};
```

---

### 8.2 Problem: `ConfigureAwait(false)` fehlt bei nicht-UI-Methoden

**Aktuell:** Die meisten async-Methoden in ViewModels und Services laufen auf dem Main Thread weiter, auch wenn sie keine UI-Arbeit machen.

**Regel:**
- **Service-Layer (AuthService, NotificationService, etc.):** Immer `ConfigureAwait(false)` verwenden
- **ViewModel-Methoden die keine UI-Properties setzen:** `ConfigureAwait(false)`
- **UI-Updates:** `MainThread.BeginInvokeOnMainThread()` oder `MainThread.InvokeOnMainThreadAsync()`

**Betroffene Services (alle Methoden):**

`Services/AuthService.cs`:
```csharp
// ALT:
var user = await _supabase.From<User>().Where(...).Get();
// NEU:
var user = await _supabase.From<User>().Where(...).Get().ConfigureAwait(false);
```

`Services/NotificationService.cs`:
```csharp
// ALT:
var notifications = await _supabaseClient.From<NotificationModel>().Get();
// NEU:
var notifications = await _supabaseClient.From<NotificationModel>().Get()
    .ConfigureAwait(false);
```

`Services/PhoenixRealtimeService.cs`:
```csharp
// ALT:
await _webSocket.ConnectAsync(_websocketUri, CancellationToken.None);
// NEU:
await _webSocket.ConnectAsync(_websocketUri, CancellationToken.None)
    .ConfigureAwait(false);
```

**ViewModels – nur für Datenbankabfragen, NICHT für Properties:**
```csharp
// ViewModel-Methoden:
var result = await _supabase.From<EntryItem>().Get().ConfigureAwait(false);
// Dann UI-Update:
await MainThread.InvokeOnMainThreadAsync(() => {
    Entries.Clear();
    foreach (var e in result.Models) Entries.Add(e);
});
```

---

### 8.3 Problem: Unnötige StackLayouts → Grid-Migration

**Warum StackLayouts teuer sind:**
- `VerticalStackLayout` und `HorizontalStackLayout` machen je einen Measure-Pass pro Kind-Element
- Bei verschachtelten Layouts multipliziert sich der Aufwand
- `Grid` mit fixen Definitionen ist O(1) beim Messen, StackLayout ist O(n)

**Betroffene Stellen (häufiges Muster):**

```xml
<!-- TEUER: -->
<VerticalStackLayout>
    <HorizontalStackLayout>
        <Image.../> <Label .../>
    </HorizontalStackLayout>
    <Label .../>
    <HorizontalStackLayout>
        <Label.../> <Label .../>
    </HorizontalStackLayout>
</VerticalStackLayout>

<!-- BESSER: -->
<Grid RowDefinitions="Auto,Auto,Auto"
      ColumnDefinitions="Auto,*">
    <Image Grid.Row="0" Grid.Column="0" .../>
    <Label Grid.Row="0" Grid.Column="1" .../>
    <Label Grid.Row="1" Grid.ColumnSpan="2" .../>
    <Label Grid.Row="2" Grid.Column="0" .../>
    <Label Grid.Row="2" Grid.Column="1" .../>
</Grid>
```

**Zu prüfende Dateien:**

| Datei | Verdächtige Stellen | Aufwand |
|-------|-------------------|---------|
| `ProfilePage.xaml` | Alle Karten-Inhalte | Mittel |
| `UserProfilePage.xaml` | Info-Sektion | Niedrig |
| `ChatViewPage.xaml` | Chat-Item innen | Niedrig |
| `RatingPage.xaml` | Bewertungs-Zeilen | Mittel |
| `FAQPage.xaml` | Nach Umbau (Phase 7) minimal | Gering |
| `DetailPage.xaml` | Aktions-Buttons-Bereich | Niedrig |

**Regel:** Nur bei verschachtelten Layouts mit mehr als 2 Ebenen migrieren. Einfache StackLayouts bleiben wenn sie klar sind.

---

### 8.4 `ChatViewPageViewModel.cs` – Sequential Partner-Data-Loading parallelisieren

**Aktuell (wahrscheinliches Muster):**
```csharp
foreach (var chat in chats)
{
    var partner = await GetPartnerDataAsync(chat.PartnerId);  // SEQUENZIELL!
    // 10 Chats = 10 sequenzielle DB-Abfragen
}
```

**Neu (paralleles Laden):**
```csharp
// Alle Partner-Daten parallel laden:
var partnerTasks = chats.Select(chat => GetPartnerDataAsync(chat.PartnerId));
var partnerData = await Task.WhenAll(partnerTasks).ConfigureAwait(false);

// Dann UI-Update:
await MainThread.InvokeOnMainThreadAsync(() => {
    for (int i = 0; i < chats.Count; i++)
    {
        chats[i].PartnerName = partnerData[i].Username;
        chats[i].PartnerImageUrl = partnerData[i].ProfileImageUrl;
    }
    Chats = new ObservableCollection<PublicChat>(chats);
});
```

**Vorteil:** Bei 10 Chats von ~2s sequenzieller Ladezeit auf ~200ms parallele Ladezeit.

---

### 8.5 `AddPageViewModel.cs` – Seiten-Loading optimieren

**Aktuell:** Paginierung lädt alle Entries aus DB und filtert/paginiert dann client-seitig.

**Problem:** Bei 1000+ Entries werden alle geladen, gefiltert und dann nur 10 angezeigt.

**Besser: Server-seitige Paginierung:**
```csharp
public async Task LoadEntriesPagedAsync(int page, int pageSize = 10, string? category = null, string? eventType = null)
{
    var query = _supabaseClient
        .From<EntryItem>()
        .Where(e => e.IsCompleted == false)
        .Order(e => e.CreatedDate, Ordering.Descending);

    // Server-seitige Filterung:
    if (category != null && category != "Alles")
        query = query.Where(e => e.Category == category);
    if (eventType != null && eventType != "Alle")
        query = query.Where(e => e.EventType == eventType);

    // Server-seitige Paginierung:
    var skip = (page - 1) * pageSize;
    query = query.Range(skip, skip + pageSize - 1);

    var result = await query.Get().ConfigureAwait(false);

    await MainThread.InvokeOnMainThreadAsync(() => {
        FilteredEntries.Clear();
        foreach (var entry in result.Models)
            FilteredEntries.Add(entry);
    });
}
```

**Vorteil:** Statt 1000 Entries zu laden nur 10 pro Seite → ~100x weniger Datentransfer.

---

### 8.6 `PhoenixRealtimeService.cs` – CancellationToken Support

**Aktuell:** Der WebSocket-Loop läuft ohne Cancellation-Möglichkeit.

**Problem:** Wenn der Nutzer sich ausloggt oder die App in den Hintergrund geht, läuft der WebSocket weiter → Ressourcen-Verschwendung, mögliche Exceptions.

**Verbesserung:**
```csharp
private CancellationTokenSource _cts = new();

public async Task ConnectAsync()
{
    _cts?.Cancel();
    _cts = new CancellationTokenSource();

    await _webSocket.ConnectAsync(_websocketUri, _cts.Token).ConfigureAwait(false);
    // Receive-Loop mit Token:
    _ = ReceiveLoopAsync(_cts.Token);  // Background Task (bewusster Fire-and-Forget mit Token)
}

public void Disconnect()
{
    _cts?.Cancel();
    _webSocket?.Dispose();
}

// Im App.xaml.cs beim Logout:
_realtimeService.Disconnect();
```

---

### 8.7 `App.xaml.cs` – SplashPage-Navigation absichern

**Aktuell (Muster):** `SplashPage` navigiert zu `LoginPage` oder `MainTabs` nach Session-Check.

**Problem:** Wenn Session-Check fehlschlägt und eine Exception geworfen wird, kann die App auf der SplashPage hängenbleiben.

**Verbesserung:**
```csharp
// In SplashPage oder App.xaml.cs:
try {
    var isValid = await _authService.EnsureSessionValidAsync()
        .ConfigureAwait(false);

    await MainThread.InvokeOnMainThreadAsync(async () => {
        if (isValid)
            await Shell.Current.GoToAsync(Routes.AddPage);
        else
            await Shell.Current.GoToAsync(Routes.LoginPage);
    });
} catch (Exception ex) {
    // Immer zur LoginPage bei Fehler – nie hängenbleiben:
    System.Diagnostics.Debug.WriteLine($"[SPLASH ERROR] {ex.Message}");
    await MainThread.InvokeOnMainThreadAsync(async () =>
        await Shell.Current.GoToAsync(Routes.LoginPage));
}
```

---

### 8.8 `MauiProgram.cs` – Scoped vs. Singleton vs. Transient Audit

**Aktuell (analysiert):**
- `ProfilePage` ist als **Singleton** registriert → Das bedeutet das ViewModel wird **nicht** neu erstellt beim Navigation-Return → Daten können veraltet sein
- `AddPage` ist als **Transient** → Korrekt (frische Instanz bei jedem Navigate)
- `ChatViewPageViewModel` ist als **Singleton** → Kann zu alten Daten führen

**Korrekturen:**
```csharp
// VORHER (problematisch):
builder.Services.AddSingleton<ProfilePage>();
builder.Services.AddSingleton<ChatViewPageViewModel>();

// NACHHER (korrekt):
builder.Services.AddTransient<ProfilePage>();           // Frisch bei jedem Besuch
builder.Services.AddTransient<ProfilePageViewModel>();  // Frisch bei jedem Besuch
builder.Services.AddTransient<ChatViewPageViewModel>(); // Frisch laden

// BLEIBEN als Singleton (globale Services):
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<PhoenixRealtimeService>();
builder.Services.AddSingleton<NotificationService>();
```

**Vorsicht:** Bei Transient-Pages muss sichergestellt sein dass Realtime-Subscriptions beim `OnDisappearing` wieder abgemeldet werden (`Dispose`), sonst akkumulieren sich Subscriptions.

---

### 8.9 Image Caching-Strategie mit FFImageLoading

**Aktuell:** FFImageLoading wird in `BellControl` für das GIF verwendet. Standard-Images überall nutzen `<Image Source="...">` ohne Caching.

**Verbesserung:** Alle Netzwerk-Bilder (Profilbilder, Event-Bilder) durch `ff:CachedImage` ersetzen:

```xml
<!-- ALT (kein Caching): -->
<Image Source="{Binding ProfileImageUrl}" WidthRequest="70" HeightRequest="70"/>

<!-- NEU (mit Caching + Loading-Placeholder): -->
<ff:CachedImage
    Source="{Binding ProfileImageUrl}"
    WidthRequest="70" HeightRequest="70"
    CacheDuration="7"        <!-- 7 Tage cachen -->
    LoadingPlaceholder="placeholder_profile.png"
    ErrorPlaceholder="placeholder_profile.png"
    Aspect="AspectFill"
    DownsampleToViewSize="True"/>  <!-- Automatisch auf View-Größe skalieren -->
```

**`DownsampleToViewSize="True"`** ist entscheidend: FFImageLoading lädt das Bild nur in der Auflösung die für die View nötig ist. Ein 2MB Profilbild wird für einen 70x70 Avatar auf ~5KB downsampled bevor es im Memory landet.

**Betroffene Stellen:**
- `EntryCard.xaml`: Event-Bild (80x80)
- `ChatViewPage.xaml`: Partner-Profilbild (70x70)
- `ProfilePage.xaml`: Eigenes Profilbild (90x90)
- `UserProfilePage.xaml`: Fremd-Profilbild (90x90)
- `DetailPage.xaml`: Autor-Profilbild

---

### 8.10 Performance: `ObservableCollection` Bulk-Updates

**Problem:** `ObservableCollection.Add()` in einem Loop triggert für jeden Item ein `CollectionChanged`-Event → bei 40 Nachrichten = 40 UI-Redraws.

**Lösung 1 – `ObservableRangeCollection` (CommunityToolkit):**
```csharp
// CommunityToolkit.Mvvm bietet ObservableCollection<T> mit AddRange
// Oder eigene Implementation:
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items) Items.Add(item);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }
}
```

**Lösung 2 – Direkt CollectionView.ItemsSource tauschen:**
```csharp
// Statt foreach+Add:
var newList = response.Models;
await MainThread.InvokeOnMainThreadAsync(() =>
    Messages = new ObservableCollection<ChatMessage>(newList));
// SetProperty löst nur ein PropertyChanged aus, nicht 40
```

---

### 8.11 Performance: `Shell.Current.GoToAsync` Pre-Warm

**Problem:** Bei der ersten Navigation zu einer Seite (z.B. DetailPage) gibt es eine spürbare Verzögerung weil MAUI die Page-Instanz erstellen und initialisieren muss.

**Lösung:** Die häufig besuchten Seiten beim App-Start im Hintergrund „aufwärmen":
```csharp
// In App.xaml.cs, nach der Initialisierung (im Background):
_ = Task.Run(async () => {
    // DI-Container vorwarmen (Services instantiieren):
    await Task.Delay(2000);  // Warten bis Main-UI geladen
    _ = _serviceProvider.GetService<DetailPageViewModel>();
    _ = _serviceProvider.GetService<ChatPageViewModel>();
});
```

---

### 8.12 Neue Supabase Tabelle: `faq` – `sort_order` Migration

```sql
-- Bereits in Phase 7 geplant, hier Performance-relevante Ergänzung:
-- Index für schnelles Sortieren:
CREATE INDEX idx_faq_sort_order ON faq(sort_order, category_title);
-- Index für häufige Filterung:
CREATE INDEX idx_entries_completed_type
    ON entries(is_completed, event_type, created_date DESC)
    WHERE is_completed = false;
-- Damit LoadEntriesPagedAsync() nicht Full-Table-Scan macht
```

---

### 8.13 `Services/PerformanceService.cs` – Neue Datei (Logging-Helper)

```csharp
// Einfacher Performance-Helper für Debug-Messungen:
public static class PerformanceService
{
    [Conditional("DEBUG")]
    public static IDisposable MeasureOperation(string operationName)
    {
        var sw = Stopwatch.StartNew();
        return new DisposableAction(() => {
            sw.Stop();
            Debug.WriteLine($"[PERF] {operationName}: {sw.ElapsedMilliseconds}ms");
        });
    }
}

// Verwendung:
using (PerformanceService.MeasureOperation("LoadEntries"))
{
    await LoadEntriesFromDatabaseAsync();
}
// Output: [PERF] LoadEntries: 342ms
```

---

### ✅ Phase 8 Build-Test Checkpoint (FINALER QUALITÄTS-TEST)

**Performance-Tests:**
1. ChatPage mit 200+ Nachrichten öffnen → App darf nicht mehr als 500ms hängen
2. ChatPage scrollen → Ältere Nachrichten laden nach ohne Pause
3. AddPage mit 100+ Entries öffnen → Paginierung lädt korrekt
4. ChatView mit 15 Chats öffnen → Partner-Daten laden parallel (spürbar schneller)
5. App kalt starten → SplashPage darf maximal 2s anzeigen

**Threading-Tests:**
6. App in den Hintergrund und zurück → Realtime-Subscriptions bleiben aktiv oder reconnecten
7. Ausloggen → Alle WebSocket-Verbindungen werden geschlossen (kein Memory Leak)
8. Schnell mehrfach zwischen Tabs wechseln → Keine „Collection was modified"-Exception

**Error-Handling Tests:**
9. Flugzeugmodus aktivieren, dann Button tippen → Fehler-Meldung erscheint (kein App-Crash)
10. Ungültiger Login → `TriggerErrorFeedback` spürbar auf iOS, Fehlermeldung sichtbar

**Code-Qualitäts-Tests:**
11. Alle neuen `async void` Event-Handler haben `try-catch`
12. `ProfilePage` öffnen, schließen, wieder öffnen → Frische Daten (kein Singleton-State-Problem)
13. `TaskScheduler.UnobservedTaskException` wird global abgefangen (Breakpoint setzen)

**Memory-Tests:**
14. 20x DetailPage öffnen und schließen → Memory steigt nicht kontinuierlich
15. Profilbilder cachen → Beim zweiten Besuch sofort sichtbar ohne Netzwerk-Request

---

## Aktualisierte Datei-Änderungsmatrix (Gesamt nach Erweiterung)

**Neue Einträge (Ergänzung zur bestehenden Matrix):**

| Datei | Art | Phase | Priorität |
|-------|-----|-------|-----------|
| `Resources/Styles/Colors.xaml` | **Vollständig überarbeiten** | 1+E1 | Kritisch |
| `Resources/Controls/CustomTabBar.xaml` | Umbau | E1 | Hoch |
| `Resources/Controls/CustomTabBar.xaml.cs` | Anpassen | E1 | Hoch |
| `Pages/ProfilePage.xaml` | Ergänzen (Passwort-Button-Text) | E2 | Niedrig |
| `ChatViewPage.xaml` | Umbau (Frame→Border, Ecken-Fix) | E2+7 | Hoch |
| `ChatPage.xaml` | Erweitern (ActivityIndicator, LoadMore) | 7 | Hoch |
| `Resources/ViewModels/ChatPageViewModel.cs` | **Umbau** (Lazy Loading, Background Thread) | 7 | Kritisch |
| `ChatPage.xaml.cs` | Erweitern (Scroll-to-Bottom) | 7 | Mittel |
| `FAQPage.xaml` | **Komplett neu** (dynamisch statt hardcoded) | 7 | Hoch |
| `FAQPage.xaml.cs` | Stark vereinfachen | 7 | Hoch |
| `Resources/ViewModels/FAQPageViewModel.cs` | Erweitern (IsLoading, ConfigureAwait) | 7 | Mittel |
| `Resources/Models/FAQCategory.cs` | **NEU** | 7 | Mittel |
| `AddPage.xaml` | Erweitern (Titel, Filter, Button-Farben) | 7 | Mittel |
| `Resources/ViewModels/AddPageViewModel.cs` | Erweitern (EventType-Filter, Server-Paginierung) | 7+8 | Hoch |
| `Resources/Controls/EntryCard.xaml` | Umbau (Bild links) | 7 | Mittel |
| `App.xaml.cs` | Erweitern (Global Exception Handler, CancellationToken) | 8 | Kritisch |
| `MauiProgram.cs` | Anpassen (Singleton→Transient Audit) | 8 | Hoch |
| `Services/AuthService.cs` | Erweitern (ConfigureAwait, CancellationToken) | 8 | Hoch |
| `Services/NotificationService.cs` | Erweitern (ConfigureAwait) | 8 | Mittel |
| `Services/PhoenixRealtimeService.cs` | Erweitern (CancellationToken, Disconnect) | 8 | Kritisch |
| `Services/PerformanceService.cs` | **NEU** | 8 | Niedrig |
| `Resources/ViewModels/ChatViewPageViewModel.cs` | Erweitern (Task.WhenAll) | 8 | Hoch |
| `Resources/ViewModels/ProfilePageViewModel.cs` | Erweitern (ConfigureAwait) | 8 | Mittel |
| Alle XAML.cs Event-Handler | Erweitern (try-catch) | 8 | Kritisch |
| Alle Image-Tags in XAML | Umbau (ff:CachedImage) | 8 | Mittel |

**Gesamt-Ergänzung:**
- **2 neue Dateien** (FAQCategory.cs, PerformanceService.cs)
- **22 weitere bestehende Dateien** angepasst
- **1 SQL-Migration** (faq.sort_order + Indices)

---

## Aktualisierte Phasen-Übersicht (Gesamt)

| Phase | Inhalt | Risiko | Geschätzt |
|-------|--------|--------|-----------|
| **1** | Styles, Anthrazit-Farbkonzept, Typografie | Niedrig | 3–4h |
| **1E** | CustomTabBar Icons, Anthrazit-Hintergrund, FAQ-Icon-Farbe | Niedrig | 1h |
| **2** | Frame→Border, Star Rating, Passwort-Button-Text | Mittel | 2–3h |
| **2E** | ChatViewPage Ecken-Fix (Frame→Border Profilbild + Card) | Niedrig | 1h |
| **3** | MapPage, Empty States, Performance | Niedrig | 2–3h |
| **4** | Settings, Feedback, Dark Mode | Mittel | 4–6h |
| **5** | Security, Trust, Moderation | Hoch | 4–6h |
| **6** | DSGVO, Haptic, Final Polish | Hoch | 4–6h |
| **7** | FAQ dynamisch, ChatPage Lazy Loading, AddPage Redesign | Mittel | 3–5h |
| **8** | Performance, Dead Locks, Code-Qualität (**LETZTE PHASE**) | Hoch | 6–8h |

**Empfohlene Reihenfolge mit Build-Tests:**
```
Phase 1 → ✅ BUILD → Phase 1E → ✅ BUILD →
Phase 2 → ✅ BUILD → Phase 2E → ✅ BUILD →
Phase 3 → ✅ BUILD → Phase 4 → ✅ BUILD →
Phase 5 → ✅ BUILD → Phase 6 → ✅ BUILD →
Phase 7 → ✅ BUILD → Phase 8 → ✅ BUILD →
ABSCHLUSS-QA (Device-Tests iOS + Android)
```

---

*Erweiterungen hinzugefügt am 06.04.2026. Basis: Zweite Analyse-Runde aller Chat-, FAQ-, TabBar- und EntryCard-Quelldateien.*
