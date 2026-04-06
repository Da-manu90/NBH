-- FAQ Migration Script for NBH App
-- Table definition
CREATE TABLE IF NOT EXISTS public.faqs (
    id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
    category text NOT NULL,
    subcategory text,
    question text NOT NULL,
    answer text NOT NULL,
    image_url text,
    order_index int NOT NULL DEFAULT 0,
    created_at timestamptz DEFAULT now()
);

-- Enable RLS
ALTER TABLE public.faqs ENABLE ROW LEVEL SECURITY;

-- Allow public read access
CREATE POLICY "Allow public read access" ON public.faqs FOR SELECT USING (true);

-- Insert Data
INSERT INTO public.faqs (category, subcategory, question, answer, order_index) VALUES
('Neuigkeiten', NULL, 'Neuigkeiten', 'Hier soll eine Timeline (kurz) mit den neusten Neuigkeiten enthalten sein.', 10),
('Feedback', NULL, 'Feedback', 'Möglichkeit zum Feedback / Kontaktformular', 20),
('Spenden', NULL, 'Spenden', 'Hier soll eine Möglichkeit zum Spenden (Paypal) integriert werden.', 30),

-- App Funktionen
('App Funktionen', 'Bewertungen', 'Bewertungen', 'Hier folgen Infos zu den Bewertungen...', 40),
('App Funktionen', 'Profil Angaben', 'Profil Seite Übersicht', 'In der ProfilePage (Profil Seite) haben Sie die Möglichkeit, Ihre Benutzerinformationen anzupassen. Die Sterne-Bewertung zeigt den Durchschnittswert Ihrer Bewertungen (Chat 1/3, Event 2/3).', 50),
('App Funktionen', 'AddPage', 'Events hinzufügen', 'Auf der AddPage können Sie Anfragen und Angebote für Events hinzufügen. Diese werden nach Datum sortiert gelistet.', 60),
('App Funktionen', 'Detail Seite', 'Event Details', 'Auf der Detail Seite sieht man alle Informationen eines Events, einschließlich Benutzerinfos des Erstellers und Status-Buttons.', 70),
('App Funktionen', 'Chat Funktion', 'Nachrichten austauschen', 'Im Chat haben Sie die Möglichkeit Nachrichten mit anderen Benutzern auszutauschen. Achtung: Chats sind unverschlüsselt zur Missbrauchsprävention.', 80),

-- Sicherheit
('Sicherheit', 'Safety first', 'Sicherheit Grundlagen', 'Grundlagen zur Sicherheit bei der Nutzung der Plattform.', 90),
('Sicherheit', 'Kennenlernen', 'Umgang beim Kennenlernen', 'Tipps und Hinweise für den sicheren Umgang beim ersten Treffen.', 100),
('Sicherheit', 'Bewertungssystem', 'Wie funktioniert das Rating?', 'Informationen zum Bewertungssystem zur Steigerung der Sicherheit.', 110),
('Sicherheit', 'Notsituationen', 'Verhalten im Notfall', 'Verhalten in Notsituationen während eines Events.', 120),

-- Dienstleistungen
('Dienstleistungen', 'Ehrenamtlich', 'Was ist Ehrenamt?', 'Ehrenamtliche Dienstleistungen sind Tätigkeiten, die in der Regel unentgeltlich erbracht werden (z.B. Nachhilfe ohne Honorar).', 130),
('Dienstleistungen', 'Finanziell', 'Finanzielle Leistungen', 'Dienstleistungen mit Gewinnerzielungsabsicht können als gewerbliche Tätigkeit eingestuft werden.', 140),
('Dienstleistungen', 'Versicherung', 'Versicherungsschutz', 'Es ist wichtig, an passenden Versicherungsschutz (Haftpflicht, Unfall) zu denken.', 150),
('Dienstleistungen', 'Haftung', 'Haftungsausschluss', 'Informationen zum Haftungsausschluss der Plattform.', 160),
('Dienstleistungen', 'Pflege', 'Pflege-Infos', 'Informationen zur Pflege im Kontext der Community-Hilfe.', 170),

-- Global
('Regeln', NULL, 'Verhaltensregeln', 'Hier werden die Regeln angezeigt, die im Bezug auf die App gewünscht oder unerwünscht sind.', 180),
('Datenschutz', NULL, 'Datenschutzerklärung', 'Hier wird die Datenschutzerklärung angezeigt, die jeder bestätigen muss.', 190);
