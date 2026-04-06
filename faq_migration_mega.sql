-- ============================================================
-- FAQ MEGA MIGRATION (Vollständige Inhalts-Extraktion)
-- ============================================================

-- Sicherstellen, dass die Tabelle existiert (passend zum C# Model FAQRow.cs)
CREATE TABLE IF NOT EXISTS public.faq (
    id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
    category_title text NOT NULL,
    subtitle text,
    content text NOT NULL,
    sort_order int NOT NULL DEFAULT 0,
    image_url text, -- Optional, falls ein separates Bild benötigt wird
    created_at timestamptz DEFAULT now()
);

-- Alle alten Einträge löschen (optional, für sauberen Start)
TRUNCATE TABLE public.faq;

-- EINTRÄGE EINFÜGEN
INSERT INTO public.faq (category_title, subtitle, content, sort_order) VALUES

-- 1) Neuigkeiten
('Allgemein', 'Neuigkeiten', 
'### Was gibt es Neues?

Hier finden Sie eine Übersicht über die neuesten Entwicklungen der Care Community App. Wir halten Sie über Feature-Updates und Community-Events auf dem Laufenden.

*   **Tipp:** Schauen Sie regelmäßig vorbei, um keine Neuigkeiten zu verpassen!', 10),

-- 2) Feedback
('Allgemein', 'Feedback', 
'### Wie kann ich Feedback geben?

Wir freuen uns über Ihre Meinung! Nutzen Sie unser Kontaktformular oder senden Sie uns direkt eine Nachricht im Chat an den Administrator, um Verbesserungsvorschläge oder Lob zu teilen.

Ihre Rückmeldung hilft uns, die Care Community stetig zu verbessern.', 20),

-- 3) Spenden
('Allgemein', 'Spenden', 
'### Wie kann ich das Projekt unterstützen?

Care Community ist ein Herzensprojekt. Wir freuen uns über jede Unterstützung via PayPal, um die Serverkosten und die Weiterentwicklung decken zu können.

Jeder Beitrag zählt!', 30),

-- 4) App Funktionen - Bewertungen
('App Funktionen', 'Bewertungen', 
'### Wie funktioniert das Bewertungssystem?

Das Bewertungssystem sorgt für Vertrauen in der Community. 

**Gewichtung:**
* **1/3 Gewichtung:** Chat-Bewertungen (Freundlichkeit, Antwortzeit).
* **2/3 Gewichtung:** Event-Bewertungen (Verlässlichkeit, Umsetzung).

Sie können Sterne (1-5) vergeben und einen kurzen Text hinterlassen.', 40),

-- 4) App Funktionen - Profil Angaben
('App Funktionen', 'Profil Angaben', 
'### Wie gestalte ich mein Profil?

In der ProfilePage (Profil Seite) passen Sie Ihre Informationen an, damit andere Benutzer einen ersten Eindruck von Ihnen bekommen.

![Profil Übersicht](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/profile_page1.png)

**1. Profilbild:**
* a) Ihr aktuelles Bild.
* b) Tippen Sie auf das Bild, um ein neues auszuwählen. Speichern nicht vergessen!

**2. Benutzername:**
Ihr bei der Registrierung gewählter Name.

**3. Sterne-Bewertung:**
Zeigt Ihren Durchschnittswert. Tippen Sie darauf, um Details zu sehen.

---

![Wohnort](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/profile_page2.png)
**Wohnort:** Geben Sie Ihren ungefähren Wohnort an. **ZU IHREM SCHUTZ:** Geben Sie niemals Ihre genaue Adresse öffentlich an!

---

![Passwort](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/profile_page3.png)
**Passwort ändern:** Hier können Sie einen Link zum Zurücksetzen anfordern.

---

![Beschreibung](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/profile_page4.png)
**Über mich:** Erzählen Sie der Community etwas über sich (Hobbys, Alter, Motivation).

---

![Abzeichen](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/profile_page5.png)
**Abzeichen:** Werden für besondere Leistungen oder Funktionen (z.B. Administrator) verliehen.

---

![Qualifikationen](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/profile_page6.png)
**Qualifikationen:** Hinterlegen Sie hier, worin Sie ausgebildet sind oder was Sie gut können (z.B. "Erzieher", "Handwerker").

---

![Speichern](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/profile_page7.png)
**WICHTIG:** Änderungen müssen über den **"Profil speichern"** Button bestätigt werden!

---

![Verifizierung](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/profile_page8.png)
**Verifizierung:** Über diesen Button können Sie Nachweise für Ihre Qualifikationen einreichen, um ein "VERIFIZIERT" Abzeichen zu erhalten.', 50),

-- 4) App Funktionen - AddPage
('App Funktionen', 'Hinzufügen (AddPage)', 
'### Wie erstelle ich ein Event?

Auf der "AddPage" erstellen Sie Anfragen (Hilfe gesucht) oder Angebote (Hilfe angeboten).

![Add Icon](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/add_icon.png)
Nutzen Sie das Plus-Icon in der Tabbar, um die Seite zu öffnen.

**Schritte zum Erstellen:**

1. ![Titel](ap_titel.png) **Titel:** Kurz und prägnant (z.B. "Gartenhilfe gesucht").
2. ![Beschreibung](ap_beschreibung.png) **Beschreibung:** Beschreiben Sie Details. Je genauer, desto besser wird später Ihre Bewertung für die Beschreibung ausfallen!
3. ![Kategorie](ap_kategorie.png) **Kategorie:** Wählen Sie "Angebot" oder "Anfrage".
4. ![Hinzufügen](ap_hinzufuegen.png) **Veröffentlichen:** Tippen Sie auf "Hinzufügen", um das Event für alle sichtbar zu machen.

**Filter nutzen:**
![Filter](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/ap_filter.png)
Sie können die Liste nach Angeboten, Anfragen oder "Alles" filtern.', 60),

-- 4) App Funktionen - Detail Seite
('App Funktionen', 'Detail Seite', 
'### Was bedeuten die Buttons auf der Detailseite?

Die Detailseite zeigt alle Infos eines Events und ermöglicht die Interaktion.

![User Info](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/dp_benutzerinformationen.png)
**Oben:** Infos zum Ersteller (Bewertungen, Name). Ein Tipp auf die Sterne öffnet das detaillierte Bewertungsprofil.

**Status-Buttons:**
* ![Warten](dp_warten_auf_partner.png) **Warten auf Partner:** Das Event ist neu und wartet auf Interessenten.
* ![Annehmen](dp_annehmen.png) **Annehmen:** (Für andere Sichtbar) Melden Sie sich für dieses Event an.
* ![Status](dp_warten_auf_bestaetigung.png) **Warten auf Bestätigung:** Sie haben sich angemeldet, der Ersteller muss Sie noch bestätigen.
* ![Aktion](dp_event_bestaetigen_ablehnen.png) **Bestätigen/Ablehnen:** Der Ersteller wählt seinen Partner aus.

**Interaktion:**
![Kontakt](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/dp_kontakt.png)
**WICHTIG:** Kontaktieren Sie den Ersteller immer erst via Chat, bevor Sie eine feste Zusage machen (Button "Kontakt").', 70),

-- 4) App Funktionen - Chat
('App Funktionen', 'Chat Funktion', 
'### Wie sicher ist der Chat?

Der Chat dient zur Absprache von Event-Details.

**Sicherheitshinweis:**
> [!WARNING]
> Ihre Chat-Nachrichten sind **unverschlüsselt** und für Administratoren einsehbar, um Missbrauch und Betrug vorzubeugen. Geben Sie sensible private Daten erst heraus, wenn Sie Vertrauen aufgebaut haben.

![Chat Übersicht](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/chatviewpage.png)
In der Übersicht sehen Sie alle aktiven Konversationen mit Profilbild, Name und der letzten Nachricht.

![Konversation](https://wyapzbqmdijffygbiyie.supabase.co/storage/v1/object/public/faq_bilder/nachrichten.png)
Blau = Ihre Nachrichten. Grau = Nachrichten des Partners.', 80),

-- 5) Sicherheit
('Sicherheit', 'Grundlagen', 
'### Safety First - Worauf muss ich achten?

Ihre Sicherheit steht an erster Stelle!

1. **Öffentliche Orte:** Treffen Sie sich beim ersten Mal an einem neutralen, öffentlichen Ort.
2. **Bezugspersonen:** Informieren Sie Freunde oder Familie über Ihre Treffen.
3. **Bauchgefühl:** Wenn Ihnen etwas komisch vorkommt, brechen Sie den Kontakt ab.
4. **Keine Vorkasse:** Zahlen Sie niemals Geld vorab an Personen, die Sie nicht kennen.', 90),

-- 5) Sicherheit - Umgang
('Sicherheit', 'Kennenlernen', 
'### Umgang beim Kennenlernen

Wenn Sie Kontakt zu neuen Personen aufnehmen:

*   Nutzen Sie den Chat für die erste Abstimmung.
*   Fragen Sie nach Referenzen oder Bewertungen anderer Nutzer.
*   Bleiben Sie freundlich, aber bestimmt bei Ihren Grenzen.', 95),

-- 5) Sicherheit - Notsituationen
('Sicherheit', 'Notsituationen', 
'### Was tun im Ernstfall?

Sollte es zu Problemen kommen:

* **Polizei:** Rufen Sie im Notfall sofort die 110.
* **Melden:** Nutzen Sie die "Melden"-Funktion im Profil des Benutzers, damit wir ihn sperren können.
* **Blockieren:** Sie können Nutzer jederzeit blockieren, um keine Nachrichten mehr zu erhalten.', 100),

-- 6) Dienstleistungen
('Dienstleistungen', 'Versicherung', 
'### Bin ich versichert?

Beim Helfen können Unfälle passieren.

**(1) Haftpflichtversicherung:**
Deckt Schäden ab, die Sie Dritten versehentlich zufügen (z.B. ein teures Gerät beschädigt). Prüfen Sie, ob Ihre private Haftpflicht ehrenamtliche Tätigkeiten abdeckt.

**(2) Unfallversicherung:**
Schützt Sie selbst bei Verletzungen während der Tätigkeit.

> [!NOTE]
> Care Community stellt lediglich die Plattform bereit und bietet keinen eigenen Versicherungsschutz an. Jeder Nutzer handelt auf eigene Verantwortung.', 110),

-- 7) Verhaltensregeln
('Regeln', 'Community-Richtlinien', 
'### Welche Regeln gelten?

Um eine harmonische Care-Community zu gewährleisten, bitten wir um Einhaltung folgender Regeln:

* **Respekt:** Behandeln Sie jeden Nutzer so, wie Sie selbst behandelt werden möchten.
* **Ehrlichkeit:** Machen Sie korrekte Angaben in Ihrem Profil und Ihren Events.
* **Zuverlässigkeit:** Erscheinen Sie pünktlich zu vereinbarten Treffen oder sagen Sie rechtzeitig ab.
* **Kein Spam:** Nutzen Sie die Plattform nicht für Werbung oder Belästigung.
* **Keine Gewalt/Hassrede:** Solche Inhalte führen zur sofortigen Sperrung.', 120),

-- 8) Datenschutz
('Rechtliches', 'Datenschutz', 
'### Wie werden meine Daten geschützt?

Wir nehmen den Schutz Ihrer Daten ernst.

* **Speicherung:** Ihre Daten werden sicher auf Supabase-Servern gespeichert.
* **Sichtbarkeit:** Nur die von Ihnen freigegebenen Infos (Profil) sind für andere sichtbar.
* **Löschung:** Sie können Ihr Konto jederzeit löschen, wodurch alle personenbezogenen Daten entfernt werden.
* **Chat:** Wie erwähnt, sind Chats zur Sicherheit einsehbar, werden aber nicht für Werbezwecke ausgewertet.', 130);
