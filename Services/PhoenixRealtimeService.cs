// ============================================================
// PhoenixRealtimeService.cs – Professionelle Version
// ============================================================
// BEHOBENE BUGS:
// 1. Hardcodierte WebSocket-URL → dynamisch aus AppConfig
// 2. Hardcodierter apikey Header → aus Konstruktor-Parameter
// 3. Kein Guard in ConnectAsync → bereits verbunden = skip
// 4. Kein Duplikat-Check in SubscribeChannel → hinzugefügt
// 5. 4096-Byte Buffer ohne Fragmentierung → akkumulierend
// 6. GetUserTokenAsync korrumpiert Session → SecureStorage-Zeile entfernt
// 7. ref immer "1" → inkrementierender Zähler
// 8. new LoginPage() direkt im Service → Navigation über Event
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NBH.Services
{
    /// <summary>
    /// Service für manuelles Phoenix-WebSocket. Unterstützt mehrere Channels,
    /// automatisches Reconnect und gefilterte Postgres Changes.
    /// </summary>
    public class PhoenixRealtimeService
    {
        private ClientWebSocket _webSocket;
        private readonly Uri _websocketUri;
        private CancellationTokenSource _cts;
        private readonly AuthService _authService;
        private readonly string _anonKey; // FIX: Key als Feld speichern

        private readonly List<ChannelInfo> _channelsToSubscribe = new();
        private bool _isReconnecting = false;
        private const int ReconnectInterval = 60_000;
        private int _tokenFailCount = 0;
        private int _refCounter = 0; // FIX 7: Inkrementierende Ref-IDs

        /// <summary>
        /// Event, das bei "INSERT"/"UPDATE"/"DELETE" ausgelöst wird.
        /// </summary>
        public event EventHandler<PhoenixPostgresEvent> PostgresChanged;

        /// <summary>
        /// Event, das gefeuert wird, wenn eine Broadcast-Nachricht (z.B. Typing Indicator) eingeht.
        /// </summary>
        public event EventHandler<PhoenixBroadcastEvent> BroadcastReceived;

        /// <summary>
        /// Event, das gefeuert wird wenn die Session endgültig ungültig ist
        /// und der Nutzer zum Login zurückkehren muss.
        /// FIX 8: Statt direkt 'new LoginPage()' zu erstellen, feuert
        /// der Service ein Event. App.xaml.cs reagiert darauf.
        /// </summary>
        public event EventHandler SessionExpired;

        // ============================================================
        // FIX 1+2: Konstruktor nimmt jetzt supabaseUrl UND anonKey
        // und baut die WebSocket-URL dynamisch daraus.
        // VORHER: URL und Key waren hardcodiert auf ein altes Projekt.
        // ============================================================
        public PhoenixRealtimeService(string supabaseUrl, string anonKey, AuthService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            if (string.IsNullOrEmpty(anonKey))
                throw new ArgumentNullException(nameof(anonKey), "AnonKey darf nicht null oder leer sein!");

            if (string.IsNullOrEmpty(supabaseUrl))
                throw new ArgumentNullException(nameof(supabaseUrl), "SupabaseUrl darf nicht null oder leer sein!");

            _anonKey = anonKey;

            // FIX 1: URL dynamisch aus supabaseUrl + anonKey bauen
            var host = new Uri(supabaseUrl).Host;
            var url = $"wss://{host}/realtime/v1/websocket?apikey={anonKey}&vsn=1.0.0";

            Console.WriteLine($"[PhoenixRealtimeService] WS-URL => wss://{host}/realtime/v1/websocket?apikey=***&vsn=1.0.0");
            _websocketUri = new Uri(url);
        }

        // Abwärtskompatibilitäts-Konstruktor (ohne supabaseUrl)
        // Nutzt AppConfig direkt – für den Fall dass alter Code noch
        // den 2-Parameter-Konstruktor aufruft.
        public PhoenixRealtimeService(string anonKey, AuthService authService)
            : this(AppConfig.SupabaseUrl, anonKey, authService)
        {
        }

        /// <summary>
        /// Fügt ein Channel-Abo hinzu. Duplikate werden ignoriert.
        /// </summary>
        public void SubscribeChannel(string topic, string userToken, string schema, string table, string filter = "")
        {
            // FIX 4: Duplikat-Check – selbe Tabelle + selber Filter = schon vorhanden
            bool alreadySubscribed = _channelsToSubscribe.Any(c =>
                c.Table == table && c.Filter == filter);

            if (alreadySubscribed)
            {
                Console.WriteLine($"[PhoenixRealtimeService] Channel bereits abonniert: table={table}, filter={filter} => skip");
                return;
            }

            Console.WriteLine($"[PhoenixRealtimeService] SubscribeChannel => topic={topic}, filter={filter}");
            var cinfo = new ChannelInfo
            {
                Topic = topic,
                UserToken = userToken,
                Schema = schema,
                Table = table,
                Filter = filter
            };
            _channelsToSubscribe.Add(cinfo);

            // Wenn der Socket schon offen ist => sofort joinen
            if (_webSocket?.State == WebSocketState.Open)
            {
                _ = JoinSingleChannelAsync(cinfo);
            }
        }

        /// <summary>
        /// Baut die Verbindung auf, setzt Header, und joint alle Channels.
        /// </summary>
        public async Task ConnectAsync()
        {
            // ============================================================
            // FIX 3: Guard – bereits verbunden? Nichts tun.
            // VORHER: Jeder Aufruf hat einen neuen Socket erstellt und
            // den alten einfach überschrieben. Der alte ReceiveLoop lief
            // im Hintergrund weiter → mehrere parallele Verbindungen.
            // ============================================================
            if (_webSocket?.State == WebSocketState.Open)
            {
                Console.WriteLine("[PhoenixRealtimeService] Already connected, skipping ConnectAsync.");
                return;
            }

            // Alten Socket sauber schließen falls in Zwischenzustand
            if (_webSocket != null && _webSocket.State != WebSocketState.Closed)
            {
                try
                {
                    _cts?.Cancel();
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "Reconnect", CancellationToken.None);
                }
                catch { /* Alten Socket ignorieren */ }
            }

            try
            {
                bool valid = await _authService.EnsureSessionValidAsync();
                if (!valid)
                {
                    Console.WriteLine("[PhoenixRealtimeService] Session invalid => Abbruch ConnectAsync");
                    return;
                }

                var userJwt = await _authService.GetUserAccessTokenAsync();
                if (string.IsNullOrEmpty(userJwt))
                {
                    Console.WriteLine("[PhoenixRealtimeService] Kein (gültiges) JWT => Abbruch ConnectAsync");
                    // FIX 8: Event feuern statt direkt LoginPage erstellen
                    SessionExpired?.Invoke(this, EventArgs.Empty);
                    return;
                }

                _webSocket = new ClientWebSocket();
                _cts = new CancellationTokenSource();

                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {userJwt}");
                // FIX 2: Key aus Feld statt hardcodiert
                _webSocket.Options.SetRequestHeader("apikey", _anonKey);
                _webSocket.Options.SetRequestHeader("Sec-WebSocket-Protocol", "phoenix");

                await _webSocket.ConnectAsync(_websocketUri, CancellationToken.None)
                    .ConfigureAwait(false);
                Console.WriteLine($"[PhoenixRealtimeService] State => {_webSocket.State}");

                if (_webSocket.State == WebSocketState.Open)
                {
                    // Starte ReceiveLoop
                    _ = Task.Run(() => ReceiveLoopAsync());

                    // Channels joinen
                    foreach (var cinfo in _channelsToSubscribe)
                    {
                        await JoinSingleChannelAsync(cinfo);
                    }
                }
                else
                {
                    Console.WriteLine("[PhoenixRealtimeService] Connect => state != Open => Abbruch");
                    ScheduleReconnect();
                }
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"[PhoenixRealtimeService] WebSocketException => {ex.Message}");
                ScheduleReconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PhoenixRealtimeService] Fehler => {ex.Message}");
                ScheduleReconnect();
            }
        }

        /// <summary>
        /// Schließt die Verbindung.
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                _cts?.Cancel();
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                }
                Console.WriteLine("[PhoenixRealtimeService] Disconnect => Socket geschlossen");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PhoenixRealtimeService] DisconnectAsync ERROR => {ex.Message}");
            }
        }

        /// <summary>
        /// Sendet eine benutzerdefinierte Nachricht (z. B. "typing") an einen Channel.
        /// </summary>
        public async Task PushAsync(string topic, string eventName, object payload)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                Console.WriteLine("[PhoenixRealtimeService] PushAsync => Socket not open! Skip.");
                return;
            }

            try
            {
                var refId = Interlocked.Increment(ref _refCounter).ToString();
                var envelope = new
                {
                    topic = topic,
                    @event = eventName,
                    payload = payload,
                    @ref = refId
                };

                var json = JsonConvert.SerializeObject(envelope);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: _cts.Token
                );

                Console.WriteLine($"[PhoenixRealtimeService] PUSH sent => {topic}, event={eventName}, ref={refId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PhoenixRealtimeService] PushAsync ERROR => " + ex.Message);
            }
        }

        /// <summary>
        /// Falls der Socket schließt => 60s später Retry
        /// </summary>
        private void ScheduleReconnect()
        {
            if (_isReconnecting)
                return;
            _isReconnecting = true;

            Console.WriteLine("[PhoenixRealtimeService] ScheduleReconnect => in 60s...");
            Task.Run(async () =>
            {
                await Task.Delay(ReconnectInterval);
                _isReconnecting = false;
                await ConnectAsync();
            });
        }

        /// <summary>
        /// Sendet den "phx_join" Payload für EINEN Channel
        /// </summary>
        private async Task JoinSingleChannelAsync(ChannelInfo cinfo)
        {
            try
            {
                Console.WriteLine($"[PhoenixRealtimeService] JoinSingleChannel => topic={cinfo.Topic}, filter={cinfo.Filter}");

                var token = cinfo.UserToken;
                if (string.IsNullOrEmpty(token))
                {
                    token = await GetUserTokenAsync();
                }

                // FIX 7: Jeder Join bekommt eine eindeutige Ref-ID
                var refId = Interlocked.Increment(ref _refCounter).ToString();

                var joinPayload = new
                {
                    topic = cinfo.Topic,
                    @event = "phx_join",
                    payload = new
                    {
                        user_token = token,
                        config = new
                        {
                            postgres_changes = new[]
                            {
                                new {
                                    @event = "ALL",
                                    schema = cinfo.Schema,
                                    table = cinfo.Table,
                                    filter = cinfo.Filter
                                }
                            }
                        }
                    },
                    @ref = refId
                };

                var json = JsonConvert.SerializeObject(joinPayload);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: _cts.Token
                );

                Console.WriteLine($"[PhoenixRealtimeService] JOIN sent => {cinfo.Topic}, filter={cinfo.Filter}, ref={refId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PhoenixRealtimeService] JoinSingleChannel ERROR => " + ex.Message);
            }
        }

        /// <summary>
        /// Holt das aktuelle User-JWT vom AuthService.
        /// </summary>
        private async Task<string> GetUserTokenAsync()
        {
            var token = await _authService.GetUserAccessTokenAsync();

            // ============================================================
            // FIX 6: Diese Zeile war der Session-Korruptions-Bug:
            //   await SecureStorage.SetAsync("supabase_session", token);
            // Sie hat den supabase_session-Key mit einem nackten JWT
            // überschrieben. AuthService erwartet dort aber ein vollständiges
            // serialisiertes Session-JSON. Beim nächsten App-Start konnte
            // AuthService das nicht mehr deserialisieren → User ausgeloggt.
            // LÖSUNG: Zeile komplett entfernt. SupabaseSessionHandler
            // verwaltet die Session.
            // ============================================================

            Console.WriteLine("[PhoenixRealtimeService] Token geholt, Länge=" + (token?.Length ?? 0));
            return token ?? "";
        }

        /// <summary>
        /// Endlosschleife, die eingehende Nachrichten empfängt.
        /// </summary>
        private async Task ReceiveLoopAsync()
        {
            // ============================================================
            // FIX 5: Akkumulierender Buffer für fragmentierte Nachrichten.
            // VORHER: Fester 4096-Byte Buffer, result.EndOfMessage wurde
            // nicht geprüft. Nachrichten >4KB wurden abgeschnitten, der
            // JSON-Parser bekam ein Fragment und warf eine Exception.
            // NACHHER: Daten werden gesammelt bis EndOfMessage = true.
            // ============================================================
            var buffer = new byte[4096];
            var messageBuffer = new List<byte>();

            while (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("[PhoenixRealtimeService] WS closed by server => reconnect");
                        break;
                    }

                    // Daten akkumulieren
                    messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                    // Erst verarbeiten wenn die Nachricht komplett ist
                    if (result.EndOfMessage)
                    {
                        var msg = Encoding.UTF8.GetString(messageBuffer.ToArray());
                        messageBuffer.Clear();
                        HandleIncomingMessage(msg);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[PhoenixRealtimeService] ReceiveLoop cancelled (disconnect)");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PhoenixRealtimeService] ReceiveLoop ERROR => " + ex.Message);
                    break;
                }
            }
            Console.WriteLine("[PhoenixRealtimeService] Socket not open => schedule reconnect");
            ScheduleReconnect();
        }

        /// <summary>
        /// Prüft "postgres_changes", "phx_reply", ...
        /// </summary>
        private void HandleIncomingMessage(string rawJson)
        {
            PhoenixEnvelope envelope = null;
            try
            {
                envelope = JsonConvert.DeserializeObject<PhoenixEnvelope>(rawJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PhoenixRealtimeService] JSON parse ERROR => " + ex.Message);
                return;
            }
            if (envelope == null)
                return;

            // "phx_reply" oder "phx_error"
            if (envelope.@event == "phx_reply" || envelope.@event == "phx_error")
            {
                if (envelope.payload?.response is Dictionary<string, object> respObj
                    && respObj.ContainsKey("reason"))
                {
                    string reason = respObj["reason"]?.ToString() ?? "";
                    if (reason.Contains("Token has expired"))
                    {
                        Console.WriteLine("[PhoenixRealtimeService] => Token abgelaufen => versuche Refresh + Reconnect");
                        Task.Run(async () =>
                        {
                            bool validToken = await _authService.EnsureSessionValidAsync();
                            if (validToken)
                            {
                                _tokenFailCount = 0;
                                await DisconnectAsync();
                                await ConnectAsync();
                            }
                            else
                            {
                                _tokenFailCount++;
                                Console.WriteLine($"[PhoenixRealtimeService] => Refresh fehlgeschlagen => failCount={_tokenFailCount}");

                                if (_tokenFailCount >= 2)
                                {
                                    _tokenFailCount = 0;
                                    Console.WriteLine("[PhoenixRealtimeService] => 2x Refresh fehlgeschlagen => SessionExpired Event");

                                    await _authService.LogoutAndClearSessionAsync();

                                    // FIX 8: Event statt direkt new LoginPage()
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        SessionExpired?.Invoke(this, EventArgs.Empty);
                                    });
                                }
                            }
                        });
                    }
                }

                Console.WriteLine($"[PhoenixRealtimeService] {envelope.@event} => ref={envelope.@ref}");
                return;
            }

            // "postgres_changes"
            if (envelope.@event == "postgres_changes")
            {
                var dataNode = envelope.payload?.data;
                if (dataNode == null)
                    return;

                var jsonDataNode = JsonConvert.SerializeObject(dataNode);
                RealtimeData realtimeData = null;
                try
                {
                    realtimeData = JsonConvert.DeserializeObject<RealtimeData>(jsonDataNode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PhoenixRealtimeService] parse RealtimeData => " + ex.Message);
                    return;
                }

                var evtType = realtimeData?.Type?.ToUpper();
                var tab = realtimeData?.Table;
                var record = realtimeData?.Record;
                var oldRecord = realtimeData?.OldRecord;

                if (!string.IsNullOrEmpty(evtType))
                {
                    PostgresChanged?.Invoke(this, new PhoenixPostgresEvent
                    {
                        Table = tab,
                        EventType = evtType,
                        New = record,
                        Old = oldRecord
                    });
                }
                return;
            }

            // Custom "broadcast" oder benutzerdefinierte Events (Typing etc.)
            if (envelope.@event != null && envelope.payload != null)
            {
                BroadcastReceived?.Invoke(this, new PhoenixBroadcastEvent
                {
                    Topic = envelope.topic,
                    Event = envelope.@event,
                    Payload = envelope.payload.response ?? envelope.payload.record ?? envelope.payload.data as Dictionary<string, object>
                });
            }
        }

        // ---------------------------------------------------------
        // Interne Hilfsklassen
        // ---------------------------------------------------------
        private class ChannelInfo
        {
            public string Topic { get; set; }
            public string UserToken { get; set; }
            public string Schema { get; set; }
            public string Table { get; set; }
            public string Filter { get; set; }
        }

        private class PhoenixEnvelope
        {
            public string topic { get; set; }
            public string @event { get; set; }
            public string @ref { get; set; }
            public string join_ref { get; set; }
            public PhoenixPayload payload { get; set; }
        }

        private class PhoenixPayload
        {
            public string type { get; set; }
            public string schema { get; set; }
            public string table { get; set; }
            public object data { get; set; }
            public Dictionary<string, object> record { get; set; }
            public Dictionary<string, object> old_record { get; set; }

            [JsonProperty("status")]
            public string status { get; set; }

            [JsonProperty("response")]
            public Dictionary<string, object> response { get; set; }
        }

        private class RealtimeData
        {
            public string Type { get; set; }
            public string Table { get; set; }
            public Dictionary<string, object> Record { get; set; }
            public Dictionary<string, object> OldRecord { get; set; }
        }
    }

    /// <summary>
    /// Argument-Klasse für PostgresChanged-Event
    /// </summary>
    public class PhoenixPostgresEvent : EventArgs
    {
        public string Table { get; set; }
        public string EventType { get; set; }
        public Dictionary<string, object> New { get; set; }
        public Dictionary<string, object> Old { get; set; }
    }

    /// <summary>
    /// Argument-Klasse für benutzerdefinierte Events (Typing etc.)
    /// </summary>
    public class PhoenixBroadcastEvent : EventArgs
    {
        public string Topic { get; set; }
        public string Event { get; set; }
        public object Payload { get; set; } // Kann Dictionary oder andere Objekte sein
    }
}
