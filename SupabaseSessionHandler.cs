// ============================================================
// SupabaseSessionHandler.cs – Session-Persistenz im SecureStorage
// ============================================================

using Newtonsoft.Json;
using Microsoft.Maui.Storage;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using System.Diagnostics;

namespace NBH
{
    internal class SupabaseSessionHandler
        : IGotrueSessionPersistence<Session>
    {
        private const string Key = "supabase_session_secure_v1";

        public void SaveSession(Session session)
        {
            if (session == null) return;

            try
            {
                var json = JsonConvert.SerializeObject(session);
                // SecureStorage ist asynchron, wir nutzen Task.Run um den synchronen Interface-Call zu bedienen
                Task.Run(async () => await SecureStorage.Default.SetAsync(Key, json).ConfigureAwait(false))
                    .GetAwaiter()
                    .GetResult();
                Debug.WriteLine("[SupabaseSessionHandler] Session erfolgreich im SecureStorage gespeichert.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseSessionHandler] Fehler beim Speichern der Session: {ex.Message}");
            }
        }

        public void DestroySession()
        {
            try
            {
                SecureStorage.Default.Remove(Key);
                // Optional: Alten Preferences-Key ebenfalls aufräumen
                Preferences.Remove("supabase_session_v2");
                Debug.WriteLine("[SupabaseSessionHandler] Session aus SecureStorage gelöscht.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseSessionHandler] Fehler beim Löschen der Session: {ex.Message}");
            }
        }

        public Session LoadSession()
        {
            try
            {
                // Versuche zuerst SecureStorage
                var json = Task.Run(async () => await SecureStorage.Default.GetAsync(Key).ConfigureAwait(false))
                    .GetAwaiter()
                    .GetResult();
                
                // Fallback auf Preferences (Migration)
                if (string.IsNullOrEmpty(json))
                {
                    json = Preferences.Get("supabase_session_v2", null);
                    if (!string.IsNullOrEmpty(json))
                    {
                        Debug.WriteLine("[SupabaseSessionHandler] Migration von Preferences zu SecureStorage...");
                        var session = JsonConvert.DeserializeObject<Session>(json);
                        if (session != null)
                        {
                            SaveSession(session); // In SecureStorage verschieben
                            Preferences.Remove("supabase_session_v2");
                            return session;
                        }
                    }
                }

                if (string.IsNullOrEmpty(json))
                    return null;

                return JsonConvert.DeserializeObject<Session>(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SupabaseSessionHandler] Fehler beim Laden der Session: {ex.Message}");
                return null;
            }
        }
    }
}
