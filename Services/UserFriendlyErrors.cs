namespace NBH.Services
{
    /// <summary>
    /// Wandelt technische Exception-Meldungen in benutzerfreundliche Texte um.
    /// Kein IT-Jargon – klare, verstaendliche Sprache fuer alle Nutzer.
    ///
    /// Verwendung:
    ///   catch (Exception ex) {
    ///       await ToastService.ShowErrorAsync(UserFriendlyErrors.GetMessage(ex));
    ///   }
    /// </summary>
    public static class UserFriendlyErrors
    {
        public static string GetMessage(Exception ex)
        {
            if (ex == null)
                return "Etwas ist schiefgelaufen. Bitte versuche es erneut.";

            return ex switch
            {
                HttpRequestException => "Keine Internetverbindung. Bitte prüfe deine Verbindung und versuche es erneut.",
                TaskCanceledException => "Die Anfrage hat zu lange gedauert. Bitte versuche es erneut.",
                TimeoutException => "Der Server antwortet nicht. Bitte versuche es in ein paar Minuten erneut.",
                UnauthorizedAccessException => "Deine Sitzung ist abgelaufen. Bitte melde dich erneut an.",
                OperationCanceledException => "Die Aktion wurde abgebrochen.",
                _ when ex.Message.Contains("duplicate key") || ex.Message.Contains("duplicate") =>
                    "Dieser Eintrag existiert bereits.",
                _ when ex.Message.Contains("violates") || ex.Message.Contains("constraint") =>
                    "Die eingegebenen Daten sind ungültig. Bitte prüfe deine Eingaben.",
                _ when ex.Message.Contains("timeout") =>
                    "Der Server antwortet nicht. Bitte versuche es in ein paar Minuten erneut.",
                _ when ex.Message.Contains("network") || ex.Message.Contains("Network") =>
                    "Netzwerkfehler. Bist du mit dem Internet verbunden?",
                _ when ex.Message.Contains("unauthorized") || ex.Message.Contains("401") =>
                    "Deine Sitzung ist abgelaufen. Bitte melde dich erneut an.",
                _ when ex.Message.Contains("not found") || ex.Message.Contains("404") =>
                    "Der Inhalt wurde nicht gefunden. Möglicherweise wurde er gelöscht.",
                _ when ex.Message.Contains("storage") || ex.Message.Contains("Storage") =>
                    "Bild konnte nicht hochgeladen werden. Bitte versuche es erneut.",
                _ => "Etwas ist schiefgelaufen. Bitte versuche es erneut."
            };
        }

        // Vordefinierte benutzerfreundliche Meldungen fuer haeufige Aktionen
        public static class Messages
        {
            // Login / Auth
            public const string LoginFailed = "Login fehlgeschlagen. Bitte prüfe deine E-Mail-Adresse und dein Passwort.";
            public const string SessionExpired = "Deine Sitzung ist abgelaufen. Bitte melde dich erneut an.";
            public const string NoInternetConnection = "Keine Internetverbindung. Bitte prüfe deine Verbindung.";

            // Profil
            public const string ProfileSaved = "Profil erfolgreich gespeichert!";
            public const string ProfileSaveError = "Profil konnte nicht gespeichert werden. Bitte versuche es erneut.";
            public const string NoChanges = "Keine Änderungen erkannt.";
            public const string ImageUploadError = "Bild konnte nicht hochgeladen werden. Bitte versuche es erneut.";
            public const string ImageProcessingError = "Bildverarbeitung fehlgeschlagen. Bitte wähle ein anderes Bild.";
            public const string InvalidImage = "Ungültiges Bildformat. Bitte wähle ein JPG oder PNG.";

            // Events
            public const string EventCreated = "Event erfolgreich erstellt! 🎉";
            public const string EventCreateError = "Event konnte nicht erstellt werden. Bitte versuche es erneut.";
            public const string EventLoadError = "Events konnten nicht geladen werden. Bitte neu laden.";
            public const string FillTitleAndLocation = "Bitte gib einen Titel und einen Ort an.";
            public const string FillParticipants = "Bitte gib eine maximale Teilnehmerzahl an oder erstelle mindestens eine Rolle.";
            public const string FillRoleNames = "Alle Rollen müssen einen Namen haben.";

            // Chat
            public const string MessageSendError = "Nachricht konnte nicht gesendet werden. Bitte versuche es erneut.";
            public const string ChatLoadError = "Nachrichten konnten nicht geladen werden.";
            public const string ChatArchiveError = "Chat konnte nicht archiviert werden.";
            public const string ChatStartError = "Chat konnte nicht gestartet werden. Bitte versuche es erneut.";
            public const string SelfChatWarning = "Du kannst keinen Chat mit dir selbst starten.";

            // Aktionen
            public const string ActionFailed = "Aktion fehlgeschlagen. Bitte versuche es erneut.";
            public const string ApplicationSent = "Bewerbung erfolgreich versendet!";
            public const string ApplicationError = "Bewerbung konnte nicht versendet werden. Bitte versuche es erneut.";
            public const string ReportSent = "Vielen Dank! Deine Meldung wurde übermittelt.";
            public const string ReportError = "Meldung konnte leider nicht gesendet werden.";

            // Account
            public const string PasswordResetSent = "Passwort-Reset E-Mail wurde gesendet! Bitte prüfe dein Postfach.";
            public const string PasswordResetError = "E-Mail konnte leider nicht gesendet werden. Bitte prüfe deine E-Mail-Adresse.";
            public const string AccountDeleteSuccess = "Dein Account wird in 30 Tagen gelöscht. Du wirst automatisch abgemeldet.";
            public const string AccountDeleteError = "Account-Löschung konnte nicht angefordert werden. Bitte versuche es später erneut.";
            public const string WrongPassword = "Das eingegebene Passwort ist nicht korrekt.";
        }
    }
}
