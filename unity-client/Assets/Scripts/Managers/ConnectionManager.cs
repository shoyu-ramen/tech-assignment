using System;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using HijackPoker.Api;
using HijackPoker.Models;

namespace HijackPoker.Managers
{
    public enum ConnectionState
    {
        Connecting,
        ConnectedWebSocket,
        ConnectedRest,
        Disconnected,
        Error
    }

    /// <summary>
    /// Orchestrates health check, WebSocket connection, REST fallback,
    /// and reconnection with exponential backoff.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        private PokerApiClient _apiClient;
#if UNITY_WEBGL && !UNITY_EDITOR
        private WebGLWebSocketClient _wsClient;
#else
        private WebSocketClient _wsClient;
#endif
        private ConnectionState _state = ConnectionState.Connecting;
        private int _tableId;
        private TaskCompletionSource<TableResponse> _pendingWsState;
        private bool _reconnecting;
        private bool _destroyed;

        public event Action<ConnectionState, string> OnConnectionStateChanged;
        public event Action<TableResponse> OnWebSocketStateReceived;
        public ConnectionState State => _state;
        public bool IsWebSocketMode => _state == ConnectionState.ConnectedWebSocket;
        public int TableId => _tableId;

        public bool IsConnected =>
            _state == ConnectionState.ConnectedWebSocket ||
            _state == ConnectionState.ConnectedRest;

        public void Initialize(PokerApiClient apiClient, int tableId)
        {
            _apiClient = apiClient;
            _tableId = tableId;
        }

        private void Update()
        {
            if (_wsClient == null) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            _wsClient.PollMessages();

            // In WebGL, ConnectAsync returns before the socket is open.
            // Detect the connection becoming ready here.
            if (_wsClient.IsConnected && _state == ConnectionState.Connecting)
            {
                SetState(ConnectionState.ConnectedWebSocket, "Connected");
            }
#endif

            // Process incoming WebSocket messages on main thread
            while (_wsClient.TryDequeueMessage(out var json))
            {
                try
                {
                    var state = JsonConvert.DeserializeObject<TableResponse>(json);
                    if (state?.Game != null)
                    {
                        _pendingWsState?.TrySetResult(state);
                        OnWebSocketStateReceived?.Invoke(state);
                    }
                }
                catch (JsonException)
                {
                    Debug.Log($"WS non-state message: {json.Substring(0, Math.Min(100, json.Length))}");
                }
            }

            // Check for disconnect
            if (_wsClient.WasDisconnected && _state == ConnectionState.ConnectedWebSocket)
            {
                _wsClient.ClearDisconnectFlag();
                SetState(ConnectionState.ConnectedRest, "Connected (REST)");
                StartReconnect();
            }
        }

        /// <summary>
        /// Full startup sequence: health check with retry, then WebSocket connect.
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            SetState(ConnectionState.Connecting, "Connecting to server...");

            bool healthy = await HealthCheckWithRetry();
            if (!healthy)
            {
                SetState(ConnectionState.Disconnected,
                    "Server unavailable - is Docker running?");
                return false;
            }

            await TryConnectWebSocket();
            return true;
        }

        private async Task TryConnectWebSocket()
        {
            CleanupWsClient();

#if UNITY_WEBGL && !UNITY_EDITOR
            _wsClient = new WebGLWebSocketClient();
#else
            _wsClient = new WebSocketClient();
#endif

            try
            {
                await _wsClient.ConnectAsync(_tableId);
                SetState(ConnectionState.ConnectedWebSocket, "Connected");
            }
            catch (Exception ex)
            {
                Debug.Log($"WebSocket connect failed: {ex.Message}");
                SetState(ConnectionState.ConnectedRest, "Connected (REST)");
            }
        }

        private async Task<bool> HealthCheckWithRetry()
        {
            // First attempt is fast — if Docker is down, fall to mock immediately
            var health = await _apiClient.GetHealthAsync();
            if (health != null) return true;
            if (_destroyed) return false;

            // One retry after a short delay
            Debug.Log("Health check failed, retrying in 1s...");
            await Task.Delay(1000);
            if (_destroyed) return false;

            health = await _apiClient.GetHealthAsync();
            return health != null;
        }

        /// <summary>
        /// POST /process, then get state via WS (with timeout) or REST fallback.
        /// </summary>
        public async Task<TableResponse> AdvanceStepAsync()
        {
            var processResult = await _apiClient.ProcessStepAsync(_tableId);
            if (processResult == null) return null;

            if (IsWebSocketMode)
            {
                // Cancel any prior pending state request
                _pendingWsState?.TrySetCanceled();

                // Wait for WS to deliver state, with 3s timeout
                _pendingWsState = new TaskCompletionSource<TableResponse>();
                var wsTask = _pendingWsState.Task;
                var completed = await Task.WhenAny(wsTask, Task.Delay(3000));

                if (completed == wsTask && !wsTask.IsCanceled)
                {
                    var result = wsTask.Result;
                    _pendingWsState = null;
                    return result;
                }

                Debug.LogWarning("WS state delivery timed out, falling back to REST");
                _pendingWsState = null;
            }

            // REST fallback
            return await _apiClient.GetTableStateAsync(_tableId);
        }

        /// <summary>
        /// Fetch current state via REST (used for initial load and reset).
        /// </summary>
        public async Task<TableResponse> GetTableStateAsync()
        {
            return await _apiClient.GetTableStateAsync(_tableId);
        }

        /// <summary>
        /// Switch to a different table. Disconnects WS and reconnects.
        /// </summary>
        public async Task SwitchTableAsync(int newTableId)
        {
            _tableId = newTableId;
            _reconnecting = false;

            CleanupWsClient();

            // Health check first
            var health = await _apiClient.GetHealthAsync();
            if (health == null)
            {
                SetState(ConnectionState.Disconnected,
                    "Server unavailable - is Docker running?");
                return;
            }

            await TryConnectWebSocket();
        }

        private async void StartReconnect()
        {
            if (_reconnecting || _destroyed) return;
            _reconnecting = true;

            float delay = 1f;

            try
            {
                while (_state == ConnectionState.ConnectedRest && !_destroyed)
                {
                    await Task.Delay((int)(delay * 1000));
                    if (_state != ConnectionState.ConnectedRest || _destroyed) break;

#if UNITY_WEBGL && !UNITY_EDITOR
                    var ws = new WebGLWebSocketClient();
#else
                    var ws = new WebSocketClient();
#endif
                    try
                    {
                        await ws.ConnectAsync(_tableId);
                        if (_destroyed)
                        {
                            await ws.DisconnectAsync();
                            break;
                        }
                        CleanupWsClient();
                        _wsClient = ws;
                        SetState(ConnectionState.ConnectedWebSocket, "Connected");
                        break;
                    }
                    catch
                    {
                        await ws.DisconnectAsync();
                        if (_destroyed) break;
                        delay = Mathf.Min(delay * 2f, 30f);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Reconnect loop error: {ex.Message}");
            }

            _reconnecting = false;
        }

        private void CleanupWsClient()
        {
            if (_wsClient != null)
            {
                _ = _wsClient.DisconnectAsync();
                _wsClient = null;
            }
        }

        private void SetState(ConnectionState state, string message)
        {
            _state = state;
            OnConnectionStateChanged?.Invoke(state, message);
        }

        public void Disconnect()
        {
            _reconnecting = false;
            CleanupWsClient();
        }

        private void OnDestroy()
        {
            _destroyed = true;
            Disconnect();
        }
    }
}
