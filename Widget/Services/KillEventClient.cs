using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Core;

namespace TestXboxGameBar.Services
{
    public enum KillEventConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    public sealed class KillEvent
    {
        public int KillCount { get; set; }
        public bool IsHeadshot { get; set; }
        public bool IsKnifeKill { get; set; }
        public bool IsFirstKill { get; set; }
        public bool IsLastKill { get; set; }
        public bool PlayMainAnimation { get; set; }
        public string AnimationKey { get; set; }
        public string PlayerName { get; set; }
        public string SteamId { get; set; }
    }

    public sealed class KillEventClient : IDisposable
    {
        private static readonly Uri EventsUri = new Uri("ws://127.0.0.1:3000/events");

        private readonly CoreDispatcher _dispatcher;
        private MessageWebSocket _socket;
        private ThreadPoolTimer _reconnectTimer;
        private bool _started;
        private bool _connecting;
        private bool _disposed;
        private KillEventConnectionState _connectionState = KillEventConnectionState.Disconnected;

        public KillEventClient(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public event EventHandler<KillEvent> KillReceived;
        public event EventHandler<KillEventConnectionState> ConnectionStateChanged;

        public KillEventConnectionState ConnectionState => _connectionState;

        public void Start()
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _ = ConnectAsync();
        }

        public void Dispose()
        {
            _disposed = true;
            _started = false;
            _reconnectTimer?.Cancel();
            CleanupSocket();
            SetConnectionState(KillEventConnectionState.Disconnected);
        }

        private async Task ConnectAsync()
        {
            if (_disposed || !_started || _connecting)
            {
                return;
            }

            _connecting = true;
            SetConnectionState(KillEventConnectionState.Connecting);
            CleanupSocket();

            var socket = new MessageWebSocket();
            socket.Control.MessageType = SocketMessageType.Utf8;
            socket.MessageReceived += OnMessageReceived;
            socket.Closed += OnClosed;
            _socket = socket;

            try
            {
                await socket.ConnectAsync(EventsUri);
                SetConnectionState(KillEventConnectionState.Connected);
            }
            catch
            {
                CleanupSocket();
                SetConnectionState(KillEventConnectionState.Disconnected);
                ScheduleReconnect();
            }
            finally
            {
                _connecting = false;
            }
        }

        private async void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            string message;
            using (DataReader reader = args.GetDataReader())
            {
                reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                message = reader.ReadString(reader.UnconsumedBufferLength);
            }

            KillEvent killEvent;
            try
            {
                JsonObject json = JsonObject.Parse(message);
                killEvent = new KillEvent
                {
                    KillCount = (int)json.GetNamedNumber("kill_count", 0),
                    IsHeadshot = json.GetNamedBoolean("is_headshot", false),
                    IsKnifeKill = json.GetNamedBoolean("is_knife_kill", false),
                    IsFirstKill = json.GetNamedBoolean("is_first_kill", false),
                    IsLastKill = json.GetNamedBoolean("is_last_kill", false),
                    PlayMainAnimation = json.GetNamedBoolean("play_main_animation", true),
                    AnimationKey = json.GetNamedString("animation_key", string.Empty),
                    PlayerName = json.GetNamedString("player_name", string.Empty),
                    SteamId = json.GetNamedString("steamid", string.Empty)
                };
            }
            catch
            {
                return;
            }

            if (_dispatcher == null)
            {
                KillReceived?.Invoke(this, killEvent);
                return;
            }

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                KillReceived?.Invoke(this, killEvent);
            });
        }

        private void OnClosed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            CleanupSocket();
            if (!_connecting)
            {
                SetConnectionState(KillEventConnectionState.Disconnected);
            }
            ScheduleReconnect();
        }

        private void ScheduleReconnect()
        {
            if (_disposed || !_started)
            {
                return;
            }

            _reconnectTimer?.Cancel();
            _reconnectTimer = ThreadPoolTimer.CreateTimer(timer =>
            {
                var ignored = ConnectAsync();
            }, TimeSpan.FromSeconds(1));
        }

        private void CleanupSocket()
        {
            if (_socket == null)
            {
                return;
            }

            _socket.MessageReceived -= OnMessageReceived;
            _socket.Closed -= OnClosed;
            _socket.Dispose();
            _socket = null;
        }

        private void SetConnectionState(KillEventConnectionState state)
        {
            if (_connectionState == state)
            {
                return;
            }

            _connectionState = state;

            if (_dispatcher == null || _dispatcher.HasThreadAccess)
            {
                ConnectionStateChanged?.Invoke(this, state);
                return;
            }

            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ConnectionStateChanged?.Invoke(this, state);
            });
        }
    }
}
