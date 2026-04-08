using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shiny;
using Shiny.Push;
using OneSignalSDK.DotNet;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Controls;
using Supabase;
using NBH.Resources.Models;
using System.Diagnostics;
using static Supabase.Postgrest.Constants;

namespace NBH.Services
{
    public class MyPushDelegate : IPushDelegate
    {
        private readonly Supabase.Client _supabaseClient;

        public MyPushDelegate(Supabase.Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
        }

        /// <summary>
        /// Automatische Speicherung der OneSignal Subscription ID nach Login.
        /// </summary>
        public async Task SaveSubscriptionId()
        {
            Console.WriteLine("[MyPushDelegate] SaveSubscriptionId() gestartet...");

            try
            {
                // 🔥 OneSignal Subscription ID abrufen
                var subscriptionId = OneSignal.User.PushSubscription.Id;

                if (!string.IsNullOrEmpty(subscriptionId))
                {
                    Console.WriteLine($"[OneSignal] Gefundene Subscription ID: {subscriptionId}");
                    await SaveSubscriptionIdToDatabase(subscriptionId);
                }
                else
                {
                    Console.WriteLine("[OneSignal] Keine Subscription ID gefunden!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OneSignal] Fehler beim Abrufen der Subscription ID: {ex.Message}");
            }
        }

        /// <summary>
        /// Speichert die Subscription ID in der Supabase 'users' Tabelle.
        /// </summary>
        private async Task SaveSubscriptionIdToDatabase(string subscriptionId)
        {
            Console.WriteLine("[MyPushDelegate] SaveSubscriptionIdToDatabase() aufgerufen...");

            var currentUser = _supabaseClient.Auth.CurrentUser;
            if (currentUser == null)
            {
                Console.WriteLine("[MyPushDelegate] Kein User eingeloggt. Abbruch.");
                return;
            }

            Console.WriteLine($"[MyPushDelegate] Aktueller UserId: {currentUser.Id}");

            try
            {
                var userResponse = await _supabaseClient
                    .From<User>()
                    .Filter("id", Operator.Equals, currentUser.Id)
                    .Single();

                if (userResponse == null)
                {
                    Console.WriteLine("[MyPushDelegate] Kein User in 'users' gefunden.");
                    return;
                }

                if (string.IsNullOrEmpty(userResponse.SubscriptionId) ||
                    userResponse.SubscriptionId != subscriptionId)
                {
                    Console.WriteLine("[MyPushDelegate] Subscription ID ist leer oder veraltet. Update in DB...");
                    userResponse.SubscriptionId = subscriptionId;

                    var result = await _supabaseClient.From<User>().Update(userResponse);
                    Console.WriteLine($"[MyPushDelegate] Subscription ID erfolgreich gespeichert: {subscriptionId}");
                }
                else
                {
                    Console.WriteLine("[MyPushDelegate] Subscription ID ist bereits aktuell. Kein Update nötig.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MyPushDelegate] Fehler beim Speichern: {ex.Message}");
            }
        }

        // -----------------------------------------
        // IPushDelegate-Methoden für OneSignal
        // -----------------------------------------

        public Task OnNewToken(string token)
        {
            Console.WriteLine($"[MyPushDelegate] OnNewToken: {token}");
            return Task.CompletedTask;
        }

        public Task OnReceived(PushNotification notification)
        {
            Console.WriteLine("[MyPushDelegate] OnReceived() - Nachricht im Vordergrund empfangen.");
            return Task.CompletedTask;
        }

        public async Task OnEntry(PushNotification notification)
        {
            Console.WriteLine("[MyPushDelegate] OnEntry() - Benutzer hat Push angetippt.");
            if (notification.Data != null && notification.Data.ContainsKey("entryId"))
            {
                var entryId = notification.Data["entryId"];
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.GoToAsync($"{Routes.DetailPage}?entryId={entryId}");
                });
            }
        }

        public Task OnUnRegistered(string token)
        {
            Console.WriteLine($"[MyPushDelegate] OnUnRegistered() - Token: {token}");
            return Task.CompletedTask;
        }
    }
}
