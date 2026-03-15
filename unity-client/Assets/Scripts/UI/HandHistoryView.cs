using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HijackPoker.Models;
using HijackPoker.Utils;

namespace HijackPoker.UI
{
    /// <summary>
    /// Collapsible log panel showing step-by-step actions, hand summaries,
    /// and running stack changes. Right side of screen, collapsed by default.
    /// </summary>
    public class HandHistoryView : MonoBehaviour
    {
        private const int MaxEntries = 200;

        private RectTransform _content;
        private ScrollRect _scrollRect;
        private readonly List<TextMeshProUGUI> _entries = new();
        private bool _scrollPending;

        // Track stacks per player across hands
        private readonly Dictionary<int, float> _startOfHandStacks = new();
        // Track previous player actions for diffing
        private readonly Dictionary<int, string> _prevPlayerActions = new();
        private int _lastGameNo = -1;

        // Collapse state
        private RectTransform _panelRt;
        private GameObject _panelBody;
        private TextMeshProUGUI _toggleLabel;
        private bool _isExpanded = false;

        public static HandHistoryView Create(Transform parent)
        {
            bool portrait = LayoutConfig.IsPortrait;

            // Toggle tab (always visible)
            var toggleGo = new GameObject("HandHistoryToggle", typeof(RectTransform));
            toggleGo.transform.SetParent(parent, false);
            var toggleRt = toggleGo.GetComponent<RectTransform>();

            if (portrait)
            {
                // Portrait: horizontal tab above controls bar, right side
                toggleRt.anchorMin = new Vector2(1f, 0f);
                toggleRt.anchorMax = new Vector2(1f, 0f);
                toggleRt.pivot = new Vector2(1f, 0f);
                toggleRt.sizeDelta = new Vector2(90, 34);
                toggleRt.anchoredPosition = new Vector2(0, LayoutConfig.ControlsBarHeight);
            }
            else
            {
                // Landscape: vertical tab on right edge
                toggleRt.anchorMin = new Vector2(1f, 0.54f);
                toggleRt.anchorMax = new Vector2(1f, 0.54f);
                toggleRt.pivot = new Vector2(1f, 0.5f);
                toggleRt.sizeDelta = new Vector2(44, 100);
                toggleRt.anchoredPosition = Vector2.zero;
            }

            var toggleBg = toggleGo.AddComponent<Image>();
            toggleBg.color = UIFactory.ToggleBg;
            toggleBg.sprite = TextureGenerator.GetRoundedRect(90, 34, 8);
            toggleBg.type = Image.Type.Sliced;
            toggleBg.raycastTarget = true;

            var view = toggleGo.AddComponent<HandHistoryView>();

            var toggleBtn = toggleGo.AddComponent<Button>();
            toggleBtn.targetGraphic = toggleBg;
            toggleBtn.onClick.AddListener(view.TogglePanel);

            view._toggleLabel = UIFactory.CreateText("ToggleText", toggleGo.transform,
                portrait ? "Log" : "Log >",
                13f, UIFactory.AccentCyan, TextAlignmentOptions.Center, FontStyles.Bold);
            var tlRt = view._toggleLabel.GetComponent<RectTransform>();
            UIFactory.StretchFill(tlRt);
            view._toggleLabel.enableWordWrapping = false;
            view._toggleLabel.overflowMode = TextOverflowModes.Overflow;
            if (!portrait)
                tlRt.localEulerAngles = new Vector3(0, 0, 90);

            // Panel (collapsible body)
            var go = new GameObject("HandHistoryPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            view._panelRt = rt;

            if (portrait)
            {
                // Portrait: bottom overlay above controls
                float barNorm = LayoutConfig.ControlsBarHeight / LayoutConfig.ReferenceResolution.y;
                rt.anchorMin = new Vector2(0.02f, barNorm + 0.02f);
                rt.anchorMax = new Vector2(0.98f, 0.55f);
            }
            else
            {
                // Landscape: right side panel
                rt.anchorMin = new Vector2(0.82f, 0.08f);
                rt.anchorMax = new Vector2(0.995f, 0.92f);
            }
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Semi-transparent background
            var bg = go.AddComponent<Image>();
            bg.color = UIFactory.PanelDarkSolid;
            bg.sprite = TextureGenerator.GetRoundedRect(128, 128, 8);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            view._panelBody = go;
            view.BuildUI(go.transform);

            // Start collapsed
            go.SetActive(false);

            // Ensure toggle renders on top of the panel
            toggleGo.transform.SetAsLastSibling();

            return view;
        }

        private void TogglePanel()
        {
            _isExpanded = !_isExpanded;
            _panelBody.SetActive(_isExpanded);
            if (LayoutConfig.IsPortrait)
                _toggleLabel.text = _isExpanded ? "Close" : "Log";
            else
                _toggleLabel.text = _isExpanded ? "< Log" : "Log >";
        }

        private void BuildUI(Transform panelTransform)
        {
            // Header bar
            var headerBar = UIFactory.CreatePanel("HeaderBar", panelTransform,
                UIFactory.HeaderBarBg);
            var headerBarRt = headerBar.GetComponent<RectTransform>();
            headerBarRt.anchorMin = new Vector2(0, 1);
            headerBarRt.anchorMax = new Vector2(1, 1);
            headerBarRt.pivot = new Vector2(0.5f, 1);
            headerBarRt.sizeDelta = new Vector2(0, 30);

            var header = UIFactory.CreateText("Header", headerBar, "Hand History",
                14f, UIFactory.AccentCyan, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.StretchFill(header.GetComponent<RectTransform>());

            // Scroll viewport
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(panelTransform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(6, 6);
            scrollRt.offsetMax = new Vector2(-6, -34);

            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = Color.clear;
            scrollGo.AddComponent<RectMask2D>();

            _scrollRect = scrollGo.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.scrollSensitivity = 20f;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Content container
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.sizeDelta = new Vector2(0, 0);

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 3;
            layout.padding = new RectOffset(4, 4, 2, 2);

            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.content = _content;
        }

        public void LogStateChange(TableResponse oldState, TableResponse newState)
        {
            if (newState?.Game == null) return;

            var game = newState.Game;
            int step = game.HandStep;

            // Hand transition separator
            if (_lastGameNo >= 0 && game.GameNo != _lastGameNo)
            {
                AddHandSeparator(oldState);
                RecordStartStacks(newState);
            }
            else if (_lastGameNo < 0)
            {
                RecordStartStacks(newState);
            }
            _lastGameNo = game.GameNo;

            // Generate entry
            string label = PhaseLabels.GetLabel(step);
            string detail = GenerateDetail(step, newState);
            string entry = string.IsNullOrEmpty(detail)
                ? $"[Step {step}] {label}"
                : $"[Step {step}] {label}\n  {detail}";

            AddEntry(entry, GetStepColor(step));

            // Log individual player actions during betting rounds (steps 5, 7, 9, 11)
            if (step == 5 || step == 7 || step == 9 || step == 11)
                LogPlayerActionDiffs(oldState, newState);
        }

        public void LogError(string message)
        {
            AddEntry($"ERROR: {message}", UIFactory.HexColor("#f44336"));
        }

        public void Clear()
        {
            foreach (var entry in _entries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            _entries.Clear();
            _lastGameNo = -1;
            _startOfHandStacks.Clear();
            _prevPlayerActions.Clear();
        }

        private void LogPlayerActionDiffs(TableResponse oldState, TableResponse newState)
        {
            if (newState?.Players == null) return;

            // Build lookup of old actions
            var oldActions = new Dictionary<int, string>();
            if (oldState?.Players != null)
            {
                foreach (var p in oldState.Players)
                    oldActions[p.Seat] = p.Action ?? "";
            }

            foreach (var player in newState.Players)
            {
                if (string.IsNullOrEmpty(player.Action)) continue;

                oldActions.TryGetValue(player.Seat, out string prevAction);
                _prevPlayerActions.TryGetValue(player.Seat, out string trackedAction);

                // Only log if action changed from what we last logged
                if (player.Action != (trackedAction ?? ""))
                {
                    string betInfo = player.Bet > 0
                        ? $" {MoneyFormatter.Format(player.Bet)}"
                        : "";
                    string actionDisplay = player.Action.ToLower() switch
                    {
                        "fold" => "folds",
                        "check" => "checks",
                        "call" => $"calls{betInfo}",
                        "bet" => $"bets{betInfo}",
                        "raise" => $"raises{betInfo}",
                        "allin" => $"goes all-in{betInfo}",
                        _ => player.Action
                    };
                    AddEntry($"  {player.Username} {actionDisplay}", UIFactory.TextSecondary);
                    _prevPlayerActions[player.Seat] = player.Action;
                }
            }
        }

        private string GenerateDetail(int step, TableResponse state)
        {
            var game = state.Game;

            switch (step)
            {
                case 0:
                    return $"Hand #{game.GameNo}";

                case 1:
                    return $"Dealer: Seat {game.DealerSeat}";

                case 2:
                    var sbPlayer = FindPlayerBySeat(state, game.SmallBlindSeat);
                    return sbPlayer != null
                        ? $"{sbPlayer.Username} posts {MoneyFormatter.Format(game.SmallBlind)}"
                        : "";

                case 3:
                    var bbPlayer = FindPlayerBySeat(state, game.BigBlindSeat);
                    return bbPlayer != null
                        ? $"{bbPlayer.Username} posts {MoneyFormatter.Format(game.BigBlind)}"
                        : "";

                case 4:
                    return "Cards dealt to all players";

                case 6: // Flop
                    if (game.CommunityCards?.Count >= 3)
                        return FormatCards(game.CommunityCards, 0, 3);
                    return "";

                case 8: // Turn
                    if (game.CommunityCards?.Count >= 4)
                        return FormatCard(game.CommunityCards[3]);
                    return "";

                case 10: // River
                    if (game.CommunityCards?.Count >= 5)
                        return FormatCard(game.CommunityCards[4]);
                    return "";

                case 5: case 7: case 9: case 11: // Betting rounds
                    return $"Pot: {MoneyFormatter.Format(game.Pot)}";

                case 12:
                    return "Cards revealed";

                case 13: // Evaluating hands
                    return GenerateWinnerDetail(state);

                case 14: // Paying winners
                    return GeneratePayoutDetail(state);

                default:
                    return "";
            }
        }

        private string GenerateWinnerDetail(TableResponse state)
        {
            if (state?.Players == null) return "";

            var parts = new List<string>();
            foreach (var p in state.Players)
            {
                if (p.IsWinner)
                {
                    string rankPart = string.IsNullOrEmpty(p.HandRank) ? "" : $" ({p.HandRank})";
                    parts.Add($"{p.Username}{rankPart}");
                }
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "";
        }

        private string GeneratePayoutDetail(TableResponse state)
        {
            if (state?.Players == null) return "";

            var parts = new List<string>();
            foreach (var p in state.Players)
            {
                if (p.Winnings > 0)
                {
                    string rankPart = string.IsNullOrEmpty(p.HandRank) ? "" : $" ({p.HandRank})";
                    parts.Add($"{p.Username} +{MoneyFormatter.Format(p.Winnings)}{rankPart}");
                }
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "";
        }

        private void AddHandSeparator(TableResponse oldState)
        {
            if (oldState?.Game == null) return;

            // Find winner info from last state
            string winnerStr = "";
            if (oldState.Players != null)
            {
                foreach (var p in oldState.Players)
                {
                    if (p.IsWinner)
                    {
                        winnerStr = $"{p.Username} ({MoneyFormatter.Format(p.Winnings)})";
                        break;
                    }
                }
            }

            string separator = string.IsNullOrEmpty(winnerStr)
                ? $"--- Hand #{oldState.Game.GameNo} Complete ---"
                : $"--- Hand #{oldState.Game.GameNo} -- Winner: {winnerStr} ---";

            AddEntry(separator, UIFactory.AccentGold);

            // Stack deltas
            AddStackDeltas(oldState);
        }

        private void AddStackDeltas(TableResponse state)
        {
            if (state?.Players == null || _startOfHandStacks.Count == 0) return;

            var parts = new List<string>();
            foreach (var p in state.Players)
            {
                if (_startOfHandStacks.TryGetValue(p.PlayerId, out float startStack))
                {
                    float delta = p.Stack - startStack;
                    if (Mathf.Abs(delta) > 0.01f)
                    {
                        string sign = delta > 0 ? "+" : "";
                        parts.Add($"  {p.Username}: {sign}{MoneyFormatter.Format(delta)}");
                    }
                }
            }

            if (parts.Count > 0)
                AddEntry(string.Join("\n", parts), UIFactory.TextSecondary);
        }

        private void RecordStartStacks(TableResponse state)
        {
            _startOfHandStacks.Clear();
            if (state?.Players == null) return;
            foreach (var p in state.Players)
                _startOfHandStacks[p.PlayerId] = p.Stack;
        }

        private void AddEntry(string text, Color color)
        {
            var tmp = UIFactory.CreateText("Entry", _content, text,
                13f, color, TextAlignmentOptions.TopLeft);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;

            var le = tmp.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 14;

            _entries.Add(tmp);

            // Entry pruning: cap at MaxEntries, destroy oldest when exceeded
            while (_entries.Count > MaxEntries)
            {
                var oldest = _entries[0];
                _entries.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            _scrollPending = true;
        }

        private void LateUpdate()
        {
            if (_scrollPending && _scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 0f;
                _scrollPending = false;
            }
        }

        private static Color GetStepColor(int step)
        {
            if (step <= 1) return UIFactory.TextMuted;                 // Setup (gray)
            if (step == 4 || step == 6 || step == 8 || step == 10)
                return UIFactory.AccentCyan;                            // Dealing (cyan)
            if (step >= 13) return UIFactory.AccentGold;                // Winners (gold)
            return UIFactory.TextPrimary;                               // Betting (white)
        }

        private static PlayerState FindPlayerBySeat(TableResponse state, int seat)
        {
            if (state?.Players == null) return null;
            foreach (var p in state.Players)
                if (p.Seat == seat) return p;
            return null;
        }

        private static string FormatCard(string card)
        {
            try
            {
                var parsed = CardUtils.Parse(card);
                return parsed.Display;
            }
            catch
            {
                return card;
            }
        }

        private static string FormatCards(List<string> cards, int start, int count)
        {
            var parts = new List<string>();
            for (int i = start; i < start + count && i < cards.Count; i++)
                parts.Add(FormatCard(cards[i]));
            return string.Join(" ", parts);
        }
    }
}
