# NBH App – UX-Verbesserungsplan: Animationen, Ladebildschirme & Benutzer-Feedback

> **Erstellt:** 06.04.2026
> **Basis:** Vollanalyse aller ViewModels, Pages, Code-Behind und XAML-Dateien
> **Kernproblem:** Die App hat einen vollständigen `AnimationHelper.cs` der NIRGENDWO genutzt wird, `IsLoading`-Properties die NICHT visuell gebunden sind, und kritische Aktionen (Event erstellen, Nachricht senden) die KEIN Benutzer-Feedback geben.

---

## Inhaltsverzeichnis

1. [Research: Moderne App-UX-Patterns](#1-research-moderne-app-ux-patterns)
2. [IST-Zustand der App](#2-ist-zustand-der-app)
3. [Fehlende Feedback-Stellen (Vollständige Tabelle)](#3-fehlende-feedback-stellen)
4. [Neue Infrastruktur-Komponenten](#4-neue-infrastruktur-komponenten)
5. [Phase A – Toast/Snackbar-System](#phase-a)
6. [Phase B – Loading-Overlays & Skeleton Screens](#phase-b)
7. [Phase C – Erfolgs- & Fehler-Animationen](#phase-c)
8. [Phase D – Seitenübergänge & Micro-Animations](#phase-d)
9. [Phase E – Seiten-spezifische Implementierung](#phase-e)
10. [Build-Test Checkliste](#build-test-checkliste)

---

## 1. Research: Moderne App-UX-Patterns

### 1.1 Lade-Zustände – Was moderne Apps nutzen

| Pattern | Wann verwenden | Beispiel |
|---------|---------------|----------|
| **Skeleton Screen (Shimmer)** | Wenn Inhaltsstruktur bekannt ist (Listen, Karten) | Instagram Feed, LinkedIn Posts |
| **ActivityIndicator (Spinner)** | Bei kurzen, unbestimmten Wartezeiten (<3s) | "Nachricht wird gesendet..." |
| **Progress Bar (bestimmt)** | Wenn Fortschritt messbar ist (Upload, Download) | Profilbild-Upload mit %-Anzeige |
| **Pull-to-Refresh** | Manuelle Aktualisierung von Listen | WhatsApp Chat-Liste |
| **Loading Overlay** | Blockierende Aktion, UI soll nicht bedienbar sein | "Account wird geloescht..." |
| **Inline-Spinner** | Innerhalb eines Buttons waehrend der Aktion | "Speichern..." im Button-Text |
| **Lottie-Animation** | Premium-Feeling, Marken-Animation | Airbnb Buchungsbestaetigung |
| **Pulsierender Platzhalter** | Bild noch nicht geladen | Profilbild-Platzhalter pulsiert |

### 1.2 Erfolgs-/Fehler-Feedback – Moderne Alternativen zu DisplayAlert

| Pattern | Wann verwenden | Vorteil gegenueber DisplayAlert |
|---------|---------------|---------------------------------|
| **Toast (oben/unten einblenden)** | Kurze Bestaetigungen ("Gespeichert!") | Nicht-blockierend, verschwindet automatisch |
| **Snackbar (mit Aktion)** | Info + Moeglichkeit ("Geloescht" + "Rueckgaengig") | User kann reagieren oder ignorieren |
| **Bottom Sheet** | Mehr Info + Optionen | Eleganter als Alert, kann Bilder enthalten |
| **Inline-Feedback** | Direkt am Eingabefeld | Roter Rand, Fehlermeldung unter dem Feld |
| **Konfetti/Partikel** | Grosser Erfolg (Event erstellt, Registrierung) | Emotionale Wirkung, Freude |
| **Shake-Animation** | Eingabefehler, falsches Passwort | Sofort sichtbar, intuitiv |
| **Checkmark-Animation** | Erfolg bei kurzen Aktionen | Professionell, schnell |

### 1.3 Micro-Animationen – Wo moderne Apps sie einsetzen

| Stelle | Animation | Effekt |
|--------|-----------|--------|
| **Button-Tap** | Scale 0.95 -> 1.0 (Spring) | "Der Button reagiert" |
| **Seiten-Uebergang** | Slide-in von rechts / Fade | Raeumliche Orientierung |
| **Listen-Items erscheinen** | Gestaffeltes Fade+Translate von unten | "Die Daten werden geladen" |
| **Pull-to-Refresh** | Lottie-Animation statt Standard-Spinner | Marken-Erlebnis |
| **Nachricht gesendet** | Bubble fliegt nach oben + Checkmark | "Nachricht ist unterwegs" |
| **Toggle-Switch** | Farb-Uebergang + leichter Bounce | "Einstellung geaendert" |
| **Neues Element in Liste** | Highlight-Flash (gelbes Aufleuchten) | "Das ist neu" |
| **Tab-Wechsel** | Crossfade zwischen Inhalten | Weicher Uebergang |
| **Karte aufklappen** | Expand-Animation mit Rotation des Chevrons | Raeumliche Logik |
| **Swipe-to-Delete** | Element gleitet raus + Luecke schliesst sich | Physik-Gefuehl |

### 1.4 .NET MAUI Verfuegbare Werkzeuge

| Werkzeug | Verfuegbarkeit | Nutzbar fuer |
|----------|---------------|-------------|
| `VisualElement.FadeTo/ScaleTo/TranslateTo/RotateTo` | Built-in | Alle Basis-Animationen |
| `CommunityToolkit.Maui` `Snackbar` | NuGet-Paket (bereits im Projekt) | Toast & Snackbar |
| `CommunityToolkit.Maui` `AnimationBehavior` | NuGet-Paket | XAML-deklarative Animationen |
| `AnimationHelper.cs` | **BEREITS IM PROJEKT** (ungenutzt!) | PageEntrance, Shake, Pulse, ChatBubble |
| `Mopups` (Mopup Service) | **BEREITS IM PROJEKT** | Modale Dialoge, Custom Popups |
| `ShimmerView` | **BEREITS IM PROJEKT** (eigenes Control) | Skeleton Loading auf AddPage |
| FFImageLoading `CachedImage` | **BEREITS IM PROJEKT** | Bild-Loading mit Platzhalter |

---

## 2. IST-Zustand der App

### 2.1 Vorhandene Infrastruktur (UNGENUTZT)

```
Resources/Animations/AnimationHelper.cs  <-- VOLLSTAENDIG, NICHT VERWENDET
   - PageEntranceAsync()      – Seiten-Eingangs-Animation
   - ButtonPressAsync()       – Button-Tap-Feedback
   - ShakeAsync()             – Fehler-Schuettel-Animation
   - PulseSuccessAsync()      – Erfolgs-Puls
   - StaggerListAsync()       – Gestaffeltes Listen-Laden
   - ChatBubbleEnterAsync()   – Chat-Nachrichten Animation
   - StartPulse()             – Endlos-Pulsieren waehrend Laden
   - SlideInFromBottomAsync() – Modal hereinfahren
   - SlideOutToBottomAsync()  – Modal herausfahren
```

### 2.2 Vorhandene IsLoading-Properties (NICHT VISUELL GEBUNDEN)

| ViewModel | Property | XAML-Binding vorhanden? |
|-----------|----------|------------------------|
| LoginPageViewModel | `IsBusy` | **NEIN** – Button wird disabled, aber kein Spinner |
| RegisterPageViewModel | `IsBusy` | **NEIN** |
| AddPageViewModel | `IsLoading` | **JA** – ShimmerView gebunden |
| ChatPageViewModel | `IsLoading` | **JA** – ActivityIndicator (Phase 7 hinzugefuegt) |
| FAQPageViewModel | `IsLoading` | **NEIN** – Property existiert, kein XAML-Binding |
| ProfilePageViewModel | `IsUploading`, `UploadProgress` | **NEIN** – Kein visuelles Element |
| CreateEventPageViewModel | – | **FEHLT KOMPLETT** |
| DetailPageViewModel | – | **FEHLT KOMPLETT** |
| SettingsPageViewModel | – | **FEHLT KOMPLETT** |

### 2.3 Aktuelle Fehler-Behandlung

**Muster das ueberall verwendet wird:**
```csharp
// Variante A: DisplayAlert (BLOCKIEREND)
await Application.Current.MainPage.DisplayAlert("Fehler", ex.Message, "OK");

// Variante B: Still (NUR Console, User erfaehrt NICHTS)
Console.WriteLine($"[ERROR] {ex.Message}");

// Variante C: LoginPage hat ShowError() mit Inline-Label (GUT, aber einzige Stelle)
ShowError("Login fehlgeschlagen. Bitte E-Mail und Passwort pruefen.");
```

---

## 3. Fehlende Feedback-Stellen (Vollstaendige Tabelle)

### KRITISCH (User weiss nicht ob Aktion erfolgreich war)

| Seite | Aktion | Aktuell | Fehlt |
|-------|--------|---------|-------|
| **CreateEventPage** | Event erstellen (RPC) | DisplayAlert "Erfolg" | Lade-Overlay, Erfolgs-Animation, benutzerfreundliche Fehlermeldung |
| **ChatPage** | Nachricht senden | NICHTS (nur Console.Log) | Sende-Animation, Fehler-Toast, Button-Disabled-State |
| **DetailPage** | Eintrag annehmen/ablehnen/bestaetigen | NICHTS oder DisplayAlert | Lade-Spinner im Button, Erfolgs-Animation, Fehler-Toast |
| **DetailPage** | Fuer Gruppen-Event bewerben | DisplayAlert | Lade-Spinner, Erfolgs-Feedback |
| **DetailPage** | Chat starten | NICHTS bei Fehler | Lade-Overlay, Fehler-Toast |
| **ProfilePage** | Profil speichern | DisplayAlert "Erfolg" | Lade-Spinner, Erfolgs-Animation statt Alert |
| **ProfilePage** | Profilbild hochladen | NICHTS (kein Progress) | Progress-Bar, Erfolgs-Feedback |

### HOCH (Schlechte UX aber nicht kritisch)

| Seite | Aktion | Aktuell | Fehlt |
|-------|--------|---------|-------|
| **LoginPage** | Login ausfuehren | `IsBusy` (Button disabled) | Spinner im Button oder Overlay |
| **RegisterPage** | Registrierung | `IsBusy` (Button disabled) | Spinner, Erfolgs-Animation |
| **SettingsPage** | Account loeschen | DisplayAlert-Kette | Lade-Overlay waehrend Loeschung |
| **SettingsPage** | Passwort aendern | DisplayAlert | Toast statt Alert |
| **SettingsPage** | Toggle-Switches | Nur Haptic | Toast "Einstellung gespeichert" |
| **ChatViewPage** | Chat archivieren | NICHTS | Swipe-Animation + Toast |
| **AddPage** | Pagination-Buttons | NICHTS | Kurzer Lade-Spinner |
| **MapPage** | Standort laden | NICHTS | Lade-Overlay, Fehler bei Berechtigung |

### MITTEL (Nice-to-have fuer Premium-Feeling)

| Seite | Animation | Aktuell | Gewuenscht |
|-------|-----------|---------|------------|
| **Alle Seiten** | Seiten-Eingang | Kein Uebergang | `PageEntranceAsync()` nutzen |
| **AddPage** | Listen-Items erscheinen | Sofort sichtbar | `StaggerListAsync()` |
| **ChatPage** | Chat-Bubble erscheint | Sofort sichtbar | `ChatBubbleEnterAsync()` |
| **Alle Buttons** | Tap-Feedback | Eigene ScaleTo-Calls | Einheitlich `ButtonPressAsync()` |
| **FAQPage** | Expander oeffnen/schliessen | Standard-Behavior | Smooth Expand-Animation |
| **SplashPage** | App-Start | Logo Fade+Scale (GUT) | + Lade-Text "Sitzung wird geprueft..." |

---

## 4. Neue Infrastruktur-Komponenten

### 4.1 `Services/ToastService.cs` – Globaler Toast/Snackbar-Service

**Zweck:** Einheitliche, nicht-blockierende Benachrichtigungen ueberall in der App.
**Technologie:** `CommunityToolkit.Maui` Snackbar (bereits als NuGet im Projekt).

```csharp
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace NBH.Services
{
    public static class ToastService
    {
        // --------------------------------------------------------
        // ERFOLG: Gruener Toast oben (3s sichtbar)
        // Aufruf: await ToastService.ShowSuccessAsync("Profil gespeichert!");
        // --------------------------------------------------------
        public static async Task ShowSuccessAsync(string message)
        {
            var snackbar = Snackbar.Make(
                message,
                duration: TimeSpan.FromSeconds(3),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#2E7D32"),  // Dunkelgruen
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15, FontWeight.Medium),
                    CharacterSpacing = 0.3
                });
            await snackbar.Show();
        }

        // --------------------------------------------------------
        // FEHLER: Roter Toast (5s sichtbar, laenger weil wichtig)
        // Aufruf: await ToastService.ShowErrorAsync("Verbindung fehlgeschlagen.");
        // --------------------------------------------------------
        public static async Task ShowErrorAsync(string message)
        {
            var snackbar = Snackbar.Make(
                message,
                duration: TimeSpan.FromSeconds(5),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#C62828"),  // Dunkelrot
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15, FontWeight.Medium),
                    CharacterSpacing = 0.3
                });
            await snackbar.Show();
        }

        // --------------------------------------------------------
        // INFO: Anthrazit Toast (3s sichtbar)
        // Aufruf: await ToastService.ShowInfoAsync("Einstellung gespeichert.");
        // --------------------------------------------------------
        public static async Task ShowInfoAsync(string message)
        {
            var snackbar = Snackbar.Make(
                message,
                duration: TimeSpan.FromSeconds(3),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#4b4b4b"),  // Anthrazit
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15, FontWeight.Medium),
                    CharacterSpacing = 0.3
                });
            await snackbar.Show();
        }

        // --------------------------------------------------------
        // SNACKBAR MIT AKTION: z.B. "Chat archiviert" + "Rueckgaengig"
        // --------------------------------------------------------
        public static async Task ShowWithActionAsync(
            string message,
            string actionText,
            Action onAction)
        {
            var snackbar = Snackbar.Make(
                message,
                actionButtonText: actionText,
                action: () => { onAction?.Invoke(); return Task.CompletedTask; },
                duration: TimeSpan.FromSeconds(5),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#4b4b4b"),
                    TextColor = Colors.White,
                    ActionButtonTextColor = Color.FromArgb("#00d2a8"),  // Teal Akzent
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15, FontWeight.Medium),
                });
            await snackbar.Show();
        }
    }
}
```

**Benutzerfreundliche Fehlermeldungen (KEIN IT-Jargon):**

```csharp
// Fehlermeldungs-Mapping fuer haeufige Exceptions:
public static class UserFriendlyErrors
{
    public static string GetMessage(Exception ex)
    {
        return ex switch
        {
            HttpRequestException => "Keine Internetverbindung. Pruefe bitte deine Verbindung und versuche es erneut.",
            TaskCanceledException => "Die Anfrage hat zu lange gedauert. Bitte versuche es erneut.",
            UnauthorizedAccessException => "Deine Sitzung ist abgelaufen. Bitte melde dich erneut an.",
            _ when ex.Message.Contains("duplicate") => "Dieser Eintrag existiert bereits.",
            _ when ex.Message.Contains("violates") => "Die eingegebenen Daten sind ungueltig. Bitte pruefe deine Eingaben.",
            _ when ex.Message.Contains("timeout") => "Der Server antwortet nicht. Bitte versuche es in ein paar Minuten erneut.",
            _ when ex.Message.Contains("network") => "Netzwerkfehler. Bist du mit dem Internet verbunden?",
            _ => "Etwas ist schiefgelaufen. Bitte versuche es erneut."
        };
    }
}
```

---

### 4.2 `Resources/Controls/LoadingOverlay.xaml` – Blockierendes Lade-Overlay

**Zweck:** Wenn der User WARTEN MUSS und die UI nicht bedienbar sein soll (Event erstellen, Account loeschen).

```xml
<!-- NEUE DATEI: Resources/Controls/LoadingOverlay.xaml -->
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="NBH.Resources.Controls.LoadingOverlay"
             IsVisible="False"
             InputTransparent="False"
             BackgroundColor="#80000000">
    <!-- Halbtransparenter schwarzer Hintergrund -->

    <Border HorizontalOptions="Center"
            VerticalOptions="Center"
            WidthRequest="260"
            HeightRequest="160"
            BackgroundColor="White"
            Padding="24"
            StrokeThickness="0">
        <Border.StrokeShape>
            <RoundRectangle CornerRadius="20"/>
        </Border.StrokeShape>
        <Border.Shadow>
            <Shadow Brush="Black" Opacity="0.2" Radius="20" Offset="0,4"/>
        </Border.Shadow>

        <VerticalStackLayout Spacing="16"
                             HorizontalOptions="Center"
                             VerticalOptions="Center">
            <!-- Spinner -->
            <ActivityIndicator x:Name="Spinner"
                               IsRunning="True"
                               Color="#00897b"
                               WidthRequest="40"
                               HeightRequest="40"
                               HorizontalOptions="Center"/>

            <!-- Lade-Text (bindbar) -->
            <Label x:Name="LoadingLabel"
                   Text="{Binding LoadingText, FallbackValue='Bitte warten...'}"
                   FontSize="15"
                   TextColor="#4b4b4b"
                   HorizontalTextAlignment="Center"
                   HorizontalOptions="Center"/>
        </VerticalStackLayout>
    </Border>
</ContentView>
```

```csharp
// NEUE DATEI: Resources/Controls/LoadingOverlay.xaml.cs
public partial class LoadingOverlay : ContentView
{
    public static readonly BindableProperty LoadingTextProperty =
        BindableProperty.Create(nameof(LoadingText), typeof(string), typeof(LoadingOverlay), "Bitte warten...");

    public string LoadingText
    {
        get => (string)GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    public LoadingOverlay()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public async Task ShowAsync(string text = "Bitte warten...")
    {
        LoadingText = text;
        IsVisible = true;
        Opacity = 0;
        await this.FadeTo(1, 200, Easing.CubicOut);
    }

    public async Task HideAsync()
    {
        await this.FadeTo(0, 200, Easing.CubicIn);
        IsVisible = false;
    }
}
```

---

### 4.3 `Resources/Controls/InlineLoadingButton.xaml` – Button mit integriertem Spinner

**Zweck:** Button zeigt waehrend der Aktion einen Spinner anstelle des Textes.

```xml
<!-- NEUE DATEI: Resources/Controls/InlineLoadingButton.xaml -->
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="NBH.Resources.Controls.InlineLoadingButton">

    <Border x:Name="ButtonBorder"
            Padding="16,12"
            StrokeThickness="0"
            HorizontalOptions="Center"
            VerticalOptions="Center">
        <Border.StrokeShape>
            <RoundRectangle CornerRadius="25"/>
        </Border.StrokeShape>
        <Border.GestureRecognizers>
            <TapGestureRecognizer Tapped="OnTapped"/>
        </Border.GestureRecognizers>

        <Grid>
            <!-- Normaler Text (sichtbar wenn NICHT loading) -->
            <Label x:Name="ButtonLabel"
                   Text="{Binding ButtonText}"
                   TextColor="White"
                   FontSize="16"
                   FontAttributes="Bold"
                   HorizontalOptions="Center"
                   VerticalOptions="Center"/>

            <!-- Spinner (sichtbar wenn loading) -->
            <ActivityIndicator x:Name="ButtonSpinner"
                               IsVisible="False"
                               IsRunning="False"
                               Color="White"
                               WidthRequest="24"
                               HeightRequest="24"
                               HorizontalOptions="Center"/>
        </Grid>
    </Border>
</ContentView>
```

---

## Phase A – Toast/Snackbar-System (Prioritaet: KRITISCH)

### A.1 `Services/ToastService.cs` erstellen
(Code siehe Abschnitt 4.1 oben)

### A.2 `Services/UserFriendlyErrors.cs` erstellen
(Code siehe Abschnitt 4.1 oben)

### A.3 Alle `DisplayAlert("Erfolg", ...)` durch `ToastService.ShowSuccessAsync()` ersetzen

**Betroffene Stellen:**

| Datei | Zeile | Alt | Neu |
|-------|-------|-----|-----|
| CreateEventPageViewModel.cs | 231 | `DisplayAlert("Erfolg", "Event wurde erstellt!", "OK")` | `await ToastService.ShowSuccessAsync("Event erfolgreich erstellt!")` |
| ProfilePageViewModel.cs | 291 | `DisplayAlert("Erfolg", "Profil gespeichert.", "OK")` | `await ToastService.ShowSuccessAsync("Profil gespeichert!")` |
| ProfilePageViewModel.cs | 299 | `DisplayAlert("Info", "Keine Aenderung.", "OK")` | `await ToastService.ShowInfoAsync("Keine Aenderungen erkannt.")` |
| SettingsPageViewModel.cs | 171 | `DisplayAlert("E-Mail versandt", ..., "OK")` | `await ToastService.ShowSuccessAsync("Passwort-Reset E-Mail gesendet!")` |

### A.4 Alle `DisplayAlert("Fehler", ex.Message, ...)` durch benutzerfreundliche Meldungen ersetzen

| Datei | Zeile | Alt | Neu |
|-------|-------|-----|-----|
| CreateEventPageViewModel.cs | 239-240 | `DisplayAlert("Fehler", $"...Details: {ex.Message}", "OK")` | `await ToastService.ShowErrorAsync(UserFriendlyErrors.GetMessage(ex))` |
| ProfilePageViewModel.cs | 308 | `DisplayAlert("Fehler", ex.Message, "OK")` | `await ToastService.ShowErrorAsync(UserFriendlyErrors.GetMessage(ex))` |
| SettingsPageViewModel.cs | 175 | `DisplayAlert("Fehler", "E-Mail konnte nicht...", "OK")` | `await ToastService.ShowErrorAsync("E-Mail konnte leider nicht gesendet werden. Pruefe deine E-Mail-Adresse.")` |
| SettingsPageViewModel.cs | 242 | `DisplayAlert("Fehler", "Account-Loeschung...", "OK")` | `await ToastService.ShowErrorAsync("Account-Loeschung konnte nicht angefordert werden. Bitte versuche es spaeter erneut.")` |

### A.5 Stille Fehler (Console.Log only) durch Toast ersetzen

| Datei | Methode | Aktuell | Neu |
|-------|---------|---------|-----|
| ChatPageViewModel.cs | SendMessageAsync() catch | `Console.WriteLine("[ERROR]...")` | `await ToastService.ShowErrorAsync("Nachricht konnte nicht gesendet werden.")` |
| ChatPageViewModel.cs | LoadMessagesAsync() catch | `Console.WriteLine("[ERROR]...")` | `await ToastService.ShowErrorAsync("Nachrichten konnten nicht geladen werden.")` |
| ChatViewPageViewModel.cs | ArchiveChatAsync() catch | `_logger.LogError(...)` | + `await ToastService.ShowErrorAsync("Chat konnte nicht archiviert werden.")` |
| DetailPageViewModel.cs | CreateNewChatAsync() catch | Nur Logger | + `await ToastService.ShowErrorAsync("Chat konnte nicht gestartet werden.")` |
| DetailPageViewModel.cs | AcceptEntryAsync() catch | Nur Logger | + `await ToastService.ShowErrorAsync("Aktion fehlgeschlagen. Bitte erneut versuchen.")` |

---

## Phase B – Loading-Overlays & Skeleton Screens (Prioritaet: HOCH)

### B.1 LoadingOverlay Control erstellen
(Code siehe Abschnitt 4.2 oben)

### B.2 LoadingOverlay in kritische Pages einbauen

**CreateEventPage.xaml:**
```xml
<!-- Ganz am Ende des aeusseren Grids (ueberlagert alles): -->
<controls:LoadingOverlay x:Name="LoadingOverlay"
                         Grid.RowSpan="99"
                         LoadingText="Event wird erstellt..."
                         IsVisible="{Binding IsBusy}"/>
```

**CreateEventPageViewModel.cs – IsLoading hinzufuegen:**
```csharp
[ObservableProperty] private bool _isBusy;

[RelayCommand]
private async Task CreateEventAsync()
{
    // ... Validierung ...
    IsBusy = true;
    try
    {
        var response = await _client.Rpc("create_event_with_roles", rpcParams);
        await ToastService.ShowSuccessAsync("Event erfolgreich erstellt!");
        await Shell.Current.GoToAsync("..");
    }
    catch (Exception ex)
    {
        await ToastService.ShowErrorAsync(UserFriendlyErrors.GetMessage(ex));
    }
    finally
    {
        IsBusy = false;
    }
}
```

**Betroffene Seiten:**

| Seite | LoadingText | Ausloeser |
|-------|-------------|-----------|
| CreateEventPage | "Event wird erstellt..." | CreateEventAsync |
| SettingsPage | "Account wird geloescht..." | DeleteAccountAsync |
| ProfilePage | "Profil wird gespeichert..." | SaveProfileAsync |
| ProfilePage | "Bild wird hochgeladen..." | SelectAndCropImageAsync |
| DetailPage | "Chat wird gestartet..." | CreateNewChatAsync |

### B.3 ActivityIndicator zu LoginPage und RegisterPage

**LoginPage.xaml – Spinner ueber dem Login-Button:**
```xml
<ActivityIndicator IsRunning="{Binding IsBusy}"
                   IsVisible="{Binding IsBusy}"
                   Color="#00897b"
                   WidthRequest="30" HeightRequest="30"
                   HorizontalOptions="Center"
                   Margin="0,10,0,0"/>
```

### B.4 Skeleton/Shimmer fuer ChatViewPage & UserProfilePage

**ChatViewPage.xaml – Shimmer waehrend Chats laden:**
```xml
<!-- Vor dem CollectionView: -->
<controls:ShimmerView IsShimmering="{Binding IsLoading}"
                      IsVisible="{Binding IsLoading}"
                      HeightRequest="400"
                      HorizontalOptions="FillAndExpand"/>
```

**IsLoading hinzufuegen in ChatViewPageViewModel:**
```csharp
private bool _isLoading;
public bool IsLoading
{
    get => _isLoading;
    set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsNotLoading)); }
}
public bool IsNotLoading => !IsLoading;

public async Task LoadChatsAsync()
{
    IsLoading = true;
    try { ... }
    finally { IsLoading = false; }
}
```

### B.5 Progress-Bar fuer Profilbild-Upload

**ProfilePage.xaml – Upload-Fortschrittsanzeige:**
```xml
<!-- Unter dem Profilbild: -->
<ProgressBar x:Name="UploadProgressBar"
             Progress="{Binding UploadProgress}"
             IsVisible="{Binding IsUploading}"
             ProgressColor="#00897b"
             HeightRequest="4"
             Margin="30,0"/>
<Label Text="Bild wird hochgeladen..."
       IsVisible="{Binding IsUploading}"
       FontSize="12"
       TextColor="#999999"
       HorizontalOptions="Center"/>
```

### B.6 SplashPage verbessern – Lade-Text hinzufuegen

```xml
<!-- Unter dem Logo: -->
<Label x:Name="statusLabel"
       Text="Sitzung wird geprueft..."
       TextColor="#999999"
       FontSize="13"
       Opacity="0"
       HorizontalOptions="Center"
       VerticalOptions="Center"
       Margin="0,120,0,0"/>
```

```csharp
// In SplashPage.xaml.cs OnAppearing():
// Nach Logo-Animation:
await statusLabel.FadeTo(1, 500, Easing.CubicOut);
statusLabel.Text = "Sitzung wird geprueft...";
bool sessionValid = await _authService.EnsureSessionValidAsync();
statusLabel.Text = sessionValid ? "Willkommen zurueck!" : "Bitte anmelden...";
await Task.Delay(500);
```

---

## Phase C – Erfolgs- & Fehler-Animationen (Prioritaet: MITTEL)

### C.1 Shake-Animation bei Validierungsfehlern

**LoginPage.xaml.cs – Fehlerhaftes Passwort:**
```csharp
// Im SignInCommand-Handler, wenn Fehler:
await AnimationHelper.ShakeAsync(PasswordEntry);
```

**RegisterPage.xaml.cs – Pflichtfelder:**
```csharp
// Bei Validierungsfehler:
if (string.IsNullOrWhiteSpace(Username))
    await AnimationHelper.ShakeAsync(UsernameEntry);
```

**CreateEventPage.xaml.cs – Titel oder Ort leer:**
```csharp
if (string.IsNullOrWhiteSpace(Title))
    await AnimationHelper.ShakeAsync(TitleEntry);
if (string.IsNullOrWhiteSpace(Location))
    await AnimationHelper.ShakeAsync(LocationEntry);
```

### C.2 Erfolgs-Puls bei abgeschlossenen Aktionen

**ProfilePage.xaml.cs – Nach Profil-Speichern:**
```csharp
// Statt DisplayAlert "Erfolg":
await AnimationHelper.PulseSuccessAsync(SaveButton);
await ToastService.ShowSuccessAsync("Profil gespeichert!");
```

**ChatPage.xaml.cs – Nach Nachricht senden:**
```csharp
// Nach erfolgreichem SendMessageAsync:
await AnimationHelper.PulseSuccessAsync(SendButton);
```

### C.3 Button-Disabled-State waehrend Async

**Alle Action-Buttons sollen waehrend der Aktion deaktiviert sein:**

```csharp
// In DetailPageViewModel:
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(AcceptEntryCommand))]
private bool _isProcessing;

private bool CanAcceptEntry() => !IsProcessing;

[RelayCommand(CanExecute = nameof(CanAcceptEntry))]
private async Task AcceptEntryAsync()
{
    IsProcessing = true;
    try { ... }
    finally { IsProcessing = false; }
}
```

---

## Phase D – Seitenuebergaenge & Micro-Animations (Prioritaet: NIEDRIG)

### D.1 PageEntranceAsync in allen OnAppearing

**Alle Seiten mit Karten/Listen:**

```csharp
// ProfilePage.xaml.cs OnAppearing:
await AnimationHelper.PageEntranceAsync(ProfileCard, StatsCard, SettingsCard);

// DetailPage.xaml.cs OnAppearing:
await AnimationHelper.PageEntranceAsync(HeaderSection, ContentCard, ActionButtons);

// AddPage.xaml.cs OnAppearing:
await AnimationHelper.PageEntranceAsync(HeaderFrame);
```

### D.2 Einheitliches Button-Press-Feedback

**Alle TapGestureRecognizer-Handler:**
```csharp
// STATT eigener ScaleTo-Calls:
private async void OnButtonTapped(object sender, TappedEventArgs e)
{
    if (sender is VisualElement el)
        await AnimationHelper.ButtonPressAsync(el);
    // ... restliche Logik
}
```

### D.3 Chat-Bubble-Animation

**ChatPage – Neue Nachrichten animiert einblenden:**
```csharp
// In Messages_CollectionChanged:
if (e.Action == NotifyCollectionChangedAction.Add)
{
    // Letzte hinzugefuegte Bubble animieren:
    var lastItem = MessagesCollectionView.GetVisualElement(ViewModel.Messages.Count - 1);
    if (lastItem != null)
        await AnimationHelper.ChatBubbleEnterAsync(lastItem, isOwnMessage);
}
```

### D.4 Toggle-Switch Feedback in SettingsPage

```csharp
// Nach Toggle aendern:
set
{
    SetProperty(ref _pushNotificationsEnabled, value);
    Preferences.Set("push_enabled", value);
    HapticFeedback.Perform(HapticFeedbackType.Click);
    _ = ToastService.ShowInfoAsync(value ? "Push-Benachrichtigungen aktiviert" : "Push-Benachrichtigungen deaktiviert");
}
```

---

## Phase E – Seiten-spezifische Implementierung

### E.1 CreateEventPage (HOECHSTE PRIORITAET)

**Aktuell:** DisplayAlert fuer Erfolg/Fehler, KEIN Ladezustand
**Neu:**
1. `IsBusy`-Property im ViewModel
2. `LoadingOverlay` mit "Event wird erstellt..."
3. Bei Validierungsfehler: `ShakeAsync()` am fehlerhaften Feld
4. Bei Erfolg: `ToastService.ShowSuccessAsync("Event erfolgreich erstellt!")`
5. Bei Fehler: `ToastService.ShowErrorAsync(UserFriendlyErrors.GetMessage(ex))`
6. Button disabled waehrend IsBusy
7. Fehlermeldungen benutzerfreundlich:
   - "Bitte gib einen Titel fuer dein Event an."
   - "Bitte gib einen Ort an."
   - "Gruppen-Events brauchen eine maximale Teilnehmerzahl."

### E.2 ChatPage

**Aktuell:** Senden hat KEIN Feedback
**Neu:**
1. Send-Button pulsiert waehrend Senden (`StartPulse`)
2. Send-Button wird disabled waehrend `SendMessageAsync()`
3. Bei Erfolg: Kurzes `PulseSuccessAsync()` am Button
4. Bei Fehler: `ToastService.ShowErrorAsync("Nachricht konnte nicht gesendet werden. Pruefe deine Internetverbindung.")`
5. Chat-Bubbles erscheinen mit `ChatBubbleEnterAsync()`
6. ActivityIndicator beim Nachladen aelterer Nachrichten (bereits da, gut)

### E.3 DetailPage

**Aktuell:** Aktionen (Annehmen, Ablehnen, Bewerben) haben kaum Feedback
**Neu:**
1. `IsProcessing`-Property fuer alle Aktions-Buttons
2. Jeder Button zeigt waehrend der Aktion einen Spinner
3. Erfolgs-Feedback:
   - Annehmen: Toast "Du hilfst jetzt bei diesem Event!" + PulseSuccess
   - Ablehnen: Toast "Anfrage abgelehnt."
   - Bewerben: Toast "Bewerbung abgeschickt!" + PulseSuccess
   - Chat starten: LoadingOverlay "Chat wird gestartet..."
4. Fehlermeldungen:
   - "Aktion konnte nicht durchgefuehrt werden. Bitte versuche es erneut."

### E.4 ProfilePage

**Aktuell:** DisplayAlert fuer alles
**Neu:**
1. Speichern: LoadingOverlay "Profil wird gespeichert..." + Toast bei Erfolg
2. Bild-Upload: ProgressBar unter Profilbild + Toast bei Erfolg
3. Logout: LoadingOverlay "Du wirst abgemeldet..."
4. Passwort-Reset: Toast "E-Mail zum Zuruecksetzen gesendet!"
5. PageEntranceAsync fuer Profil-Karten

### E.5 LoginPage & RegisterPage

**Aktuell:** IsBusy existiert, kein visueller Spinner
**Neu:**
1. ActivityIndicator ueber dem Login-Button (gebunden an IsBusy)
2. Bei Fehler: `ShakeAsync()` am Passwort-Feld + inline Fehlermeldung (bereits vorhanden, gut)
3. Bei Erfolg: Kurze Erfolgs-Animation vor Navigation
4. Apple-Login: Overlay "Anmeldung mit Apple..." (IsBusy-Binding)

### E.6 SettingsPage

**Aktuell:** DisplayAlert, kein Ladezustand
**Neu:**
1. Account loeschen: LoadingOverlay "Account wird vorgemerkt fuer Loeschung..."
2. Passwort aendern: Toast statt DisplayAlert
3. Toggle-Switches: Toast "Einstellung gespeichert"

### E.7 SplashPage

**Aktuell:** Logo-Animation (GUT), kein Status-Text
**Neu:**
1. Status-Label unter dem Logo: "Sitzung wird geprueft..."
2. Wechselt zu "Willkommen zurueck!" oder "Bitte anmelden..."
3. Sanftes Fade-In des Status-Texts

### E.8 MapPage

**Aktuell:** Kein Ladezustand waehrend Standort/Pins laden
**Neu:**
1. ActivityIndicator waehrend Standort geladen wird
2. Toast bei Standort-Berechtigung verweigert: "Standort-Zugriff verweigert. Gehe zu Einstellungen um ihn zu aktivieren."
3. Toast bei Pin-Lade-Fehler: "Eintraege konnten nicht geladen werden."

---

## Zusammenfassung: Neue Dateien

| Datei | Typ | Phase |
|-------|-----|-------|
| `Services/ToastService.cs` | Service (statisch) | A |
| `Services/UserFriendlyErrors.cs` | Helper (statisch) | A |
| `Resources/Controls/LoadingOverlay.xaml` | Control | B |
| `Resources/Controls/LoadingOverlay.xaml.cs` | Code-Behind | B |

## Zusammenfassung: Zu aendernde Dateien

| Datei | Aenderung | Phase |
|-------|-----------|-------|
| CreateEventPageViewModel.cs | IsBusy, Toast, Fehlermeldungen | A+B |
| CreateEventPage.xaml | LoadingOverlay hinzufuegen | B |
| ChatPageViewModel.cs | Toast bei Fehler, Button-State | A+C |
| ChatPage.xaml.cs | PulseSuccess, ButtonPress | C |
| DetailPageViewModel.cs | IsProcessing, Toast, Button-State | A+B+C |
| DetailPageViewModel.GroupEvents.cs | IsProcessing, Toast | A+C |
| DetailPage.xaml | LoadingOverlay fuer Chat-Start | B |
| ProfilePageViewModel.cs | Toast statt DisplayAlert, LoadingOverlay | A+B |
| ProfilePage.xaml | LoadingOverlay, ProgressBar | B |
| ProfilePage.xaml.cs | AnimationHelper Aufrufe | C+D |
| SettingsPageViewModel.cs | Toast, LoadingOverlay | A+B |
| SettingsPage.xaml | LoadingOverlay | B |
| LoginPage.xaml | ActivityIndicator | B |
| LoginPage.xaml.cs | ShakeAsync | C |
| RegisterPage.xaml | ActivityIndicator | B |
| SplashPage.xaml | Status-Label | B |
| SplashPage.xaml.cs | Status-Text Logik | B |
| ChatViewPageViewModel.cs | IsLoading, Toast | A+B |
| ChatViewPage.xaml | ShimmerView | B |
| AddPage.xaml.cs | PageEntrance | D |
| MapPage.xaml.cs | Toast fuer Fehler | A |
| UserProfilePageViewModel.cs | Toast, IsLoading | A+B |
| Alle .xaml.cs mit ScaleTo | AnimationHelper.ButtonPressAsync | D |

---

## Phasen-Uebersicht & Zeitschaetzung

| Phase | Inhalt | Risiko | Geschaetzt |
|-------|--------|--------|-----------|
| **A** | Toast/Snackbar-System + UserFriendlyErrors | Niedrig | 2-3h |
| **B** | Loading-Overlays, ActivityIndicators, Shimmer, ProgressBar | Mittel | 4-6h |
| **C** | Shake, PulseSuccess, Button-Disabled-States | Niedrig | 2-3h |
| **D** | PageEntrance, ButtonPress, ChatBubble, Toggle-Toasts | Niedrig | 2-3h |
| **E** | Seiten-spezifische Integration (alles zusammen) | Mittel | 4-6h |

**Empfohlene Reihenfolge:**
```
Phase A (ToastService + Errors) → BUILD →
Phase B (LoadingOverlay + Spinner) → BUILD →
Phase C (Animationen) → BUILD →
Phase D (Micro-Animations) → BUILD →
Phase E (Integration) → BUILD →
ABSCHLUSS-QA
```

---

## Build-Test Checkliste

### Phase A Tests
- [ ] Event erstellen mit fehlendem Titel → Benutzerfreundliche Fehlermeldung (kein IT-Jargon)
- [ ] Event erstellen erfolgreich → Gruener Toast "Event erfolgreich erstellt!"
- [ ] Profil speichern → Toast statt DisplayAlert
- [ ] Internet ausschalten + Aktion ausfuehren → "Keine Internetverbindung..."
- [ ] Chat-Nachricht senden bei Fehler → Roter Toast erscheint

### Phase B Tests
- [ ] Event erstellen → LoadingOverlay "Event wird erstellt..." erscheint
- [ ] Account loeschen → LoadingOverlay waehrend Loeschung
- [ ] Login-Button klicken → Spinner sichtbar waehrend Login
- [ ] ChatViewPage oeffnen → Shimmer waehrend Chats laden
- [ ] Profilbild hochladen → ProgressBar unter Bild sichtbar
- [ ] SplashPage → "Sitzung wird geprueft..." Text sichtbar

### Phase C Tests
- [ ] Falsches Passwort eingeben → Passwort-Feld wackelt (ShakeAsync)
- [ ] Event erstellen ohne Titel → Titel-Feld wackelt
- [ ] Profil speichern erfolgreich → Speichern-Button pulsiert kurz
- [ ] Nachricht senden → Senden-Button pulsiert bei Erfolg

### Phase D Tests
- [ ] Zu ProfilePage navigieren → Karten gleiten von unten herein
- [ ] Button tippen → Einheitliches "Einfedern" bei allen Buttons
- [ ] Neue Chat-Nachricht → Bubble gleitet von der Seite herein
- [ ] Settings-Toggle umschalten → Toast "Einstellung gespeichert"

---

*Analyse basiert auf Vollaudit aller 25+ ViewModels, 20+ Pages und 15+ Controls (Stand 06.04.2026).*
*Vorhandener AnimationHelper.cs wird erstmalig in die App integriert.*
