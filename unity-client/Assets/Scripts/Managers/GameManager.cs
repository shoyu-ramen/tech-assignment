using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using HijackPoker.Animation;
using HijackPoker.Api;
using HijackPoker.Models;
using HijackPoker.UI;
using HijackPoker.Utils;

namespace HijackPoker.Managers
{
    /// <summary>
    /// Singleton that bootstraps the entire poker client.
    /// Creates Canvas, all views, and orchestrates state updates.
    /// Manages ConnectionManager for WS/REST, auto-play, and hand history.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance => _instance;

        private PokerApiClient _apiClient;
        private TableStateManager _stateManager;
        private AnimationController _animController;
        private ConnectionManager _connectionManager;
        private InputHandler _inputHandler;
        private int _tableId = 1;
        private bool _isProcessing;
        private bool _lastIsPortrait;

        // Auto-play
        private bool _autoPlaying;
        private bool _autoPlayStopRequested;

        // Views
        private GameObject _canvasGo;
        private Transform _canvasTransform;
        private TableView _tableView;
        private SeatView[] _seats;
        private CommunityCardsView _communityCards;
        private HudView _hud;
        private ControlsView _controls;
        private ConnectionStatusView _connectionStatus;
        private HandHistoryView _handHistory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("GameManager");
            _instance = go.AddComponent<GameManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            // Wait one frame for screen orientation to settle on mobile
            // before building UI, then continue with async initialization.
            StartCoroutine(DelayedStart());
        }

        private IEnumerator DelayedStart()
        {
            yield return null;
            UI.LayoutConfig.ResetOrientationCache();
            StartAsync();
        }

        private async void StartAsync()
        {
            _stateManager = new TableStateManager();
            _stateManager.OnStateChanged += HandleStateChanged;
            _animController = new AnimationController();
            AudioManager.Initialize(gameObject);

            BuildUI();
            _lastIsPortrait = LayoutConfig.IsPortrait;

            _apiClient = gameObject.AddComponent<PokerApiClient>();
            _connectionManager = gameObject.AddComponent<ConnectionManager>();
            _connectionManager.Initialize(_apiClient, _tableId);
            _connectionManager.OnConnectionStateChanged += HandleConnectionStateChanged;

            _controls.SetInteractable(false);

            bool connected = await _connectionManager.ConnectAsync();
            if (connected)
            {
                await FetchInitialState();
            }
            else
            {
                _stateManager.UpdateState(MockStateFactory.CreateMockState());
                _controls.SetInteractable(true);
            }
        }

        private void BuildUI()
        {
            // Canvas
            _canvasGo = new GameObject("Canvas");
            var canvasGo = _canvasGo;
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UI.LayoutConfig.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = UI.LayoutConfig.CanvasMatch;

            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem — required for button clicks
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.transform.SetParent(transform, false);
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            var canvasRt = canvasGo.GetComponent<RectTransform>();

            // Full-screen background (fills entire screen including behind notch/home indicator)
            var fullBgRt = UIFactory.CreatePanel("FullScreenBg", canvasRt, UIFactory.Background);
            UIFactory.StretchFill(fullBgRt);

            // Safe area container — constrains all interactive content within Screen.safeArea
            var safeAreaGo = new GameObject("SafeArea", typeof(RectTransform));
            safeAreaGo.transform.SetParent(canvasRt, false);
            var safeAreaRt = safeAreaGo.GetComponent<RectTransform>();
            UIFactory.StretchFill(safeAreaRt);
            safeAreaGo.AddComponent<UI.SafeAreaPanel>();

            _canvasTransform = safeAreaRt;
            UI.LayoutConfig.SetContentRoot(safeAreaRt);

            // Table background + surface (stretch-fills behind everything)
            _tableView = TableView.Create(safeAreaRt);

            // ── Main vertical grid (3 rows: HUD, Game Area, Controls) ──
            var mainGrid = new GameObject("MainGrid", typeof(RectTransform));
            mainGrid.transform.SetParent(safeAreaRt, false);
            var mainGridRt = mainGrid.GetComponent<RectTransform>();
            UIFactory.StretchFill(mainGridRt);

            var mainVlg = mainGrid.AddComponent<VerticalLayoutGroup>();
            mainVlg.childAlignment = TextAnchor.UpperCenter;
            mainVlg.childControlWidth = true;
            mainVlg.childControlHeight = true;
            mainVlg.childForceExpandWidth = true;
            mainVlg.childForceExpandHeight = false;
            mainVlg.spacing = 0;

            // ── Row 0: HUD (fixed height) ──────────────────────────────
            _hud = HudView.Create(mainGrid.transform);
            _hud.AnimController = _animController;
            var hudLE = _hud.gameObject.AddComponent<LayoutElement>();
            hudLE.preferredHeight = LayoutConfig.HudRowHeight;
            hudLE.flexibleWidth = 1;

            // ── Row 1: Game Area (flexible, fills remaining space) ──────
            var gameArea = new GameObject("GameArea", typeof(RectTransform));
            gameArea.transform.SetParent(mainGrid.transform, false);
            var gameAreaLE = gameArea.AddComponent<LayoutElement>();
            gameAreaLE.flexibleHeight = 1;
            gameAreaLE.flexibleWidth = 1;

            // Game area uses a nested vertical layout for seat rows
            var gameVlg = gameArea.AddComponent<VerticalLayoutGroup>();
            gameVlg.childAlignment = TextAnchor.MiddleCenter;
            gameVlg.childControlWidth = true;
            gameVlg.childControlHeight = true;
            gameVlg.childForceExpandWidth = true;
            gameVlg.childForceExpandHeight = false;
            gameVlg.spacing = LayoutConfig.GameAreaSpacing;
            gameVlg.padding = new RectOffset(4, 4, 0, 4);

            // ── Top seats row (Seats 3, 4, 5) ──────────────────────────
            var topSeatsRow = CreateSeatRow("TopSeats", gameArea.transform,
                LayoutConfig.TopSeatsRowFlex);

            // ── Center row (community cards + pot) ──────────────────────
            var centerRow = new GameObject("CenterRow", typeof(RectTransform));
            centerRow.transform.SetParent(gameArea.transform, false);
            var centerRowLE = centerRow.AddComponent<LayoutElement>();
            centerRowLE.flexibleHeight = LayoutConfig.CenterRowFlex;
            centerRowLE.flexibleWidth = 1;

            var centerVlg = centerRow.AddComponent<VerticalLayoutGroup>();
            centerVlg.childAlignment = TextAnchor.MiddleCenter;
            centerVlg.childControlWidth = false;
            centerVlg.childControlHeight = false;
            centerVlg.childForceExpandWidth = false;
            centerVlg.childForceExpandHeight = false;
            centerVlg.spacing = 6;

            _communityCards = CommunityCardsView.Create(centerRow.transform);
            _communityCards.AnimController = _animController;

            _hud.CreatePot(centerRow.transform);

            // ── Bottom seats row (Seats 2, 1, 6) ───────────────────────
            var bottomSeatsRow = CreateSeatRow("BottomSeats", gameArea.transform,
                LayoutConfig.BottomSeatsRowFlex);

            // Create seats into their rows
            _seats = new SeatView[LayoutConfig.MaxSeats + 1]; // index 0 unused
            // Top row: seats 3, 4, 5
            _seats[3] = SeatView.Create(3, topSeatsRow);
            _seats[4] = SeatView.Create(4, topSeatsRow);
            _seats[5] = SeatView.Create(5, topSeatsRow);
            // Bottom row: seats 2, 1, 6
            _seats[2] = SeatView.Create(2, bottomSeatsRow);
            _seats[1] = SeatView.Create(1, bottomSeatsRow);
            _seats[6] = SeatView.Create(6, bottomSeatsRow);

            for (int i = 1; i <= LayoutConfig.MaxSeats; i++)
                _seats[i].AnimController = _animController;

            // ── Row 2: Controls (fixed height) ─────────────────────────
            _controls = ControlsView.Create(mainGrid.transform);
            _controls.OnNextStep += HandleNextStep;
            _controls.OnReset += HandleReset;
            _controls.OnAutoPlayToggle += HandleAutoPlayToggle;
            _controls.OnTableConnect += HandleTableConnect;

            // Connection status (overlaid top-left of safe area)
            _connectionStatus = ConnectionStatusView.Create(safeAreaRt);

            // Hand history (overlaid right side of safe area)
            _handHistory = HandHistoryView.Create(safeAreaRt);

            // Keyboard shortcuts (desktop/WebGL) — only create once
            if (_inputHandler == null)
            {
                _inputHandler = gameObject.AddComponent<InputHandler>();
                _inputHandler.OnNextStep += HandleNextStep;
                _inputHandler.OnReset += HandleReset;
                _inputHandler.OnAutoPlayToggle += HandleAutoPlayToggle;
            }
        }

        private Transform CreateSeatRow(string name, Transform parent, float flexHeight)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var le = row.AddComponent<LayoutElement>();
            le.flexibleHeight = flexHeight;
            le.flexibleWidth = 1;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.spacing = LayoutConfig.SeatRowSpacing;
            hlg.padding = new RectOffset(8, 8, 0, 0);

            return row.transform;
        }

        // ── Connection ───────────────────────────────────────────────

        private void HandleConnectionStateChanged(ConnectionState state, string message)
        {
            _connectionStatus.UpdateStatus(state, message);
            _hud.SetStatus(
                state == ConnectionState.Disconnected || state == ConnectionState.Error
                    ? message : "");

            bool canInteract = _connectionManager.IsConnected;
            _controls.SetInteractable(canInteract && !_isProcessing);

            // Stop auto-play on disconnect
            if (!canInteract && _autoPlaying)
                StopAutoPlay();
        }

        // ── State ────────────────────────────────────────────────────

        private void HandleStateChanged(TableResponse oldState, TableResponse newState)
        {
            int oldStep = oldState?.Game?.HandStep ?? -1;
            int newStep = newState?.Game?.HandStep ?? -1;
            int oldGameNo = oldState?.Game?.GameNo ?? -1;
            int newGameNo = newState?.Game?.GameNo ?? -1;

            bool isHandTransition = oldState != null && newGameNo > 0 && oldGameNo != newGameNo;
            bool isDealCards = oldState != null && oldStep < 4 && newStep >= 4
                && !isHandTransition;
            bool isFindWinners = oldState != null && oldStep < 13 && newStep >= 13;
            bool isPayWinners = oldState != null && oldStep < 14 && newStep >= 14;

            if (isFindWinners)
                AudioManager.Instance?.Play(SoundType.WinnerFanfare);

            // Defer stack tweens for winners before rendering (pot will fly in first)
            if (isPayWinners && newState?.Players != null)
            {
                foreach (var player in newState.Players)
                {
                    if (player.Seat < 1 || player.Seat > LayoutConfig.MaxSeats) continue;
                    if (player.IsWinner && player.Winnings > 0)
                        _seats[player.Seat].DeferNextStackTween();
                }
            }

            RenderState(newState);

            // Log to hand history
            _handHistory.LogStateChange(oldState, newState);

            // Play pot-to-winner fly animations after render
            if (isPayWinners)
                PlayPotDistribution(oldState, newState);

            // Shuffle animation on hand transition
            if (isHandTransition)
                ShuffleAnimator.PlayShuffle(_animController, _canvasTransform,
                    _seats, oldState, newGameNo);

            // Deal animation when cards are first dealt
            if (isDealCards)
                DealAnimator.PlayDeal(_animController, _canvasTransform,
                    newState.Game.DealerSeat, _seats, newState.Players);
        }

        private void RenderState(TableResponse state)
        {
            if (state == null) return;

            _hud.UpdateFromState(state);
            _communityCards.UpdateFromState(state);
            UpdateSeats(state);
        }

        private void UpdateSeats(TableResponse state)
        {
            if (state?.Game == null) return;

            var playerBySeat = new Dictionary<int, PlayerState>();
            if (state.Players != null)
            {
                foreach (var player in state.Players)
                {
                    if (player.Seat < 1 || player.Seat > LayoutConfig.MaxSeats) continue;
                    playerBySeat[player.Seat] = player;
                }
            }

            for (int i = 1; i <= LayoutConfig.MaxSeats; i++)
            {
                playerBySeat.TryGetValue(i, out var player);
                _seats[i].UpdateFromState(player, state.Game);
            }
        }

        // ── Data Fetching ────────────────────────────────────────────

        private async Task FetchInitialState()
        {
            try
            {
                var state = await _connectionManager.GetTableStateAsync();
                if (state != null)
                {
                    _stateManager.UpdateState(state);
                    _controls.SetInteractable(true);
                }
                else
                {
                    _hud.SetStatus("Failed to fetch table state");
                    _stateManager.UpdateState(MockStateFactory.CreateMockState());
                    _controls.SetInteractable(true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error fetching state: {ex.Message}");
                _hud.SetStatus("Server unavailable - is Docker running?");
                _stateManager.UpdateState(MockStateFactory.CreateMockState());
                _controls.SetInteractable(true);
            }
        }

        // ── Step Processing ──────────────────────────────────────────

        private async void HandleNextStep()
        {
            // Manual click stops auto-play
            if (_autoPlaying)
            {
                StopAutoPlay();
                return;
            }

            if (_isProcessing) return;
            await ProcessStep();
        }

        private async Task ProcessStep()
        {
            if (_isProcessing) return;

            // Cancel any running animations and snap to final states
            _animController.CancelAll();
            ResetSeatContinuousTweens();

            _isProcessing = true;
            if (!_autoPlaying)
                _controls.SetInteractable(false);

            try
            {
                var state = await _connectionManager.AdvanceStepAsync();
                if (state != null)
                {
                    _stateManager.UpdateState(state);
                    _hud.SetStatus("");
                }
                else
                {
                    _hud.SetStatus("Failed to process step");
                    _handHistory.LogError("Failed to process step");
                    if (_autoPlaying) StopAutoPlay();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing step: {ex.Message}");
                _hud.SetStatus($"Error: {ex.Message}");
                _handHistory.LogError(ex.Message);
                if (_autoPlaying) StopAutoPlay();
            }
            finally
            {
                _isProcessing = false;
                if (!_autoPlaying)
                {
                    _controls.SetInteractable(_connectionManager.IsConnected);
                }
            }
        }

        private async void HandleReset()
        {
            if (_isProcessing) return;

            if (_autoPlaying)
                StopAutoPlay();

            _animController.CancelAll();
            ResetSeatContinuousTweens();

            _isProcessing = true;
            _controls.SetInteractable(false);

            try
            {
                var state = await _connectionManager.GetTableStateAsync();
                if (state != null)
                {
                    _stateManager.UpdateState(state);
                    _hud.SetStatus("");
                }
                else
                {
                    _hud.SetStatus("Failed to fetch table state");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error resetting: {ex.Message}");
                _hud.SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                _controls.SetInteractable(_connectionManager.IsConnected);
            }

            _handHistory.Clear();
        }

        // ── Auto-Play ────────────────────────────────────────────────

        private void HandleAutoPlayToggle()
        {
            if (_autoPlaying)
                StopAutoPlay();
            else
                StartAutoPlay();
        }

        private Coroutine _autoPlayCoroutine;

        private void StartAutoPlay()
        {
            if (_autoPlaying || _isProcessing) return;
            _autoPlaying = true;
            _autoPlayStopRequested = false;
            _controls.SetAutoPlayActive(true);
            Tweener.SpeedMultiplier = Mathf.Max(1f, 1f / _controls.CurrentSpeed);
            _autoPlayCoroutine = StartCoroutine(AutoPlayLoop());
        }

        private IEnumerator AutoPlayLoop()
        {
            while (_autoPlaying && !_autoPlayStopRequested)
            {
                var stepTask = ProcessStep();
                while (!stepTask.IsCompleted) yield return null;

                if (!_autoPlaying || _autoPlayStopRequested) break;

                yield return new WaitForSeconds(_controls.CurrentSpeed);
            }

            _autoPlaying = false;
            _controls.SetAutoPlayActive(false);
        }

        private void StopAutoPlay()
        {
            _autoPlayStopRequested = true;
            _autoPlaying = false;
            Tweener.SpeedMultiplier = 1f;
            if (_autoPlayCoroutine != null)
            {
                StopCoroutine(_autoPlayCoroutine);
                _autoPlayCoroutine = null;
            }
            _controls.SetAutoPlayActive(false);
        }

        private async void HandleTableConnect(int tableId)
        {
            if (_isProcessing) return;

            if (_autoPlaying)
                StopAutoPlay();

            _animController.CancelAll();
            ResetSeatContinuousTweens();

            _tableId = tableId;
            _controls.SetInteractable(false);
            _handHistory.Clear();

            try
            {
                await _connectionManager.SwitchTableAsync(tableId);
                await FetchInitialState();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error switching table: {ex.Message}");
                _hud.SetStatus($"Error: {ex.Message}");
                _controls.SetInteractable(_connectionManager.IsConnected);
            }
        }

        private void Update()
        {
            // Detect runtime orientation change
            LayoutConfig.ResetOrientationCache();
            bool currentPortrait = LayoutConfig.IsPortrait;
            if (currentPortrait != _lastIsPortrait)
            {
                _lastIsPortrait = currentPortrait;
                RebuildUI();
            }
        }

        private void RebuildUI()
        {
            // Stop auto-play and cancel animations
            StopAutoPlay();
            _animController?.CancelAll();

            // Destroy existing canvas
            if (_canvasGo != null)
                Destroy(_canvasGo);

            LayoutConfig.ResetOrientationCache();
            BuildUI();

            // Re-render current state
            var currentState = _stateManager?.CurrentState;
            if (currentState != null)
                RenderState(currentState);
        }

        private void OnDestroy()
        {
            StopAutoPlay();
            _animController?.CancelAll();
            if (_stateManager != null)
                _stateManager.OnStateChanged -= HandleStateChanged;
            if (_connectionManager != null)
                _connectionManager.OnConnectionStateChanged -= HandleConnectionStateChanged;
            if (_instance == this)
                _instance = null;
        }

        private void ResetSeatContinuousTweens()
        {
            for (int i = 1; i <= LayoutConfig.MaxSeats; i++)
                _seats[i].ResetContinuousTweens();
        }

        // ── Pot Distribution (delegated to PotDistributionAnimator) ──

        private void PlayPotDistribution(TableResponse oldState, TableResponse newState)
        {
            PotDistributionAnimator.PlayPotDistribution(
                _animController, _canvasTransform, _seats, oldState, newState);
        }
    }
}
