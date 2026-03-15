using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HijackPoker.Animation;
using HijackPoker.Models;
using HijackPoker.Utils;

namespace HijackPoker.UI
{
    /// <summary>
    /// Heads-up display: phase label (top-center), hand number, blinds info.
    /// Pot is created separately via CreatePot() into the center game row.
    /// Animates phase shimmer and pot tweens on state changes.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        private TextMeshProUGUI _phaseLabel;
        private TextMeshProUGUI _handNumber;
        private TextMeshProUGUI _activePlayerText;
        private TextMeshProUGUI _potText;
        private TextMeshProUGUI _blindsText;
        private TextMeshProUGUI _statusText;
        private RectTransform _rt;
        private RectTransform _potRt;

        // Shimmer
        private RectTransform _shimmerContainer;
        private Image _shimmerBar;

        // Animation state
        private int _prevStep = -1;
        private float _prevPot;
        private bool _hasRenderedOnce;

        public AnimationController AnimController { get; set; }
        public TextMeshProUGUI StatusText => _statusText;
        public RectTransform PotTransform => _potRt;

        public static HudView Create(Transform parent)
        {
            var go = new GameObject("HUD", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();

            var view = go.AddComponent<HudView>();
            view._rt = rt;
            view.BuildUI();
            return view;
        }

        private void BuildUI()
        {
            var phaseSize = LayoutConfig.PhaseLabelContainerSize;

            // HUD uses a vertical layout to stack elements
            var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2;
            vlg.padding = new RectOffset(0, 0, 4, 0);

            // Row 1: Connection status + Phase label + Blinds (horizontal)
            var topRow = new GameObject("TopRow", typeof(RectTransform));
            topRow.transform.SetParent(transform, false);
            var topRowLE = topRow.AddComponent<LayoutElement>();
            topRowLE.preferredHeight = phaseSize.y + 4;
            topRowLE.preferredWidth = 0;
            topRowLE.flexibleWidth = 1;

            var topHlg = topRow.AddComponent<HorizontalLayoutGroup>();
            topHlg.childAlignment = TextAnchor.MiddleCenter;
            topHlg.childControlWidth = false;
            topHlg.childControlHeight = false;
            topHlg.childForceExpandWidth = true;
            topHlg.childForceExpandHeight = false;
            topHlg.spacing = 8;

            // Blinds info — left side
            _blindsText = UIFactory.CreateText("Blinds", topRow.transform, "",
                13f, UIFactory.TextMuted, TextAlignmentOptions.MidlineLeft);
            var blindsLE = _blindsText.gameObject.AddComponent<LayoutElement>();
            blindsLE.preferredWidth = LayoutConfig.BlindsWidth;
            blindsLE.preferredHeight = 22;

            // Phase label container (with mask for shimmer sweep) — center
            var containerGo = new GameObject("PhaseLabelContainer", typeof(RectTransform));
            containerGo.transform.SetParent(topRow.transform, false);
            _shimmerContainer = containerGo.GetComponent<RectTransform>();
            _shimmerContainer.sizeDelta = phaseSize;
            containerGo.AddComponent<RectMask2D>();
            var phaseLE = containerGo.AddComponent<LayoutElement>();
            phaseLE.preferredWidth = phaseSize.x;
            phaseLE.preferredHeight = phaseSize.y;

            _phaseLabel = UIFactory.CreateText("PhaseLabel", _shimmerContainer, "Waiting...",
                LayoutConfig.PhaseFontSize, UIFactory.AccentCyan, TextAlignmentOptions.Center,
                FontStyles.Bold);
            UIFactory.StretchFill(_phaseLabel.GetComponent<RectTransform>());

            // Shimmer bar
            float shimmerRange = phaseSize.x / 2f + 15f;
            _shimmerBar = UIFactory.CreateImage("ShimmerBar", _shimmerContainer,
                UIFactory.ShimmerColor, new Vector2(60, phaseSize.y - 4));
            var shimmerRt = _shimmerBar.GetComponent<RectTransform>();
            shimmerRt.anchoredPosition = new Vector2(-shimmerRange, 0);
            _shimmerBar.gameObject.SetActive(false);

            // Spacer for right side balance
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(topRow.transform, false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.preferredWidth = LayoutConfig.BlindsWidth;
            spacerLE.preferredHeight = 22;

            // Row 2: Hand number + active player
            _handNumber = UIFactory.CreateText("HandNumber", transform, "",
                14f, UIFactory.TextMuted, TextAlignmentOptions.Center);
            var handLE = _handNumber.gameObject.AddComponent<LayoutElement>();
            handLE.preferredWidth = LayoutConfig.HandNumberWidth;
            handLE.preferredHeight = 22;

            _activePlayerText = _handNumber;

            // Status text — below hand number
            _statusText = UIFactory.CreateText("Status", transform, "",
                14f, UIFactory.TextMuted, TextAlignmentOptions.Center);
            var statusLE = _statusText.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredWidth = LayoutConfig.StatusWidth;
            statusLE.preferredHeight = 24;
        }

        /// <summary>
        /// Creates the pot display as a child of the center game row,
        /// separate from the HUD's own layout.
        /// </summary>
        public void CreatePot(Transform centerRow)
        {
            var potSize = LayoutConfig.PotBgSize;
            var potBg = UIFactory.CreatePanel("PotBg", centerRow,
                UIFactory.PanelDark, potSize);
            var potBgImg = potBg.GetComponent<Image>();
            if (potBgImg == null) potBgImg = potBg.gameObject.AddComponent<Image>();
            potBgImg.sprite = TextureGenerator.GetRoundedRect((int)potSize.x, (int)potSize.y, 6);
            potBgImg.type = Image.Type.Sliced;

            var potLE = potBg.gameObject.AddComponent<LayoutElement>();
            potLE.preferredWidth = potSize.x;
            potLE.preferredHeight = potSize.y;

            _potRt = potBg;

            _potText = UIFactory.CreateText("Pot", potBg, "",
                LayoutConfig.PotFontSize, UIFactory.AccentGold, TextAlignmentOptions.Center,
                FontStyles.Bold);
            UIFactory.StretchFill(_potText.GetComponent<RectTransform>());
        }

        public void UpdateFromState(TableResponse state)
        {
            if (state?.Game == null)
            {
                _phaseLabel.text = "Waiting for data...";
                _handNumber.text = "";
                if (_potText != null) _potText.text = "";
                _blindsText.text = "";
                return;
            }

            var game = state.Game;
            bool animate = _hasRenderedOnce && AnimController != null;

            // Phase label
            _phaseLabel.text = PhaseLabels.GetLabel(game.HandStep);

            // Phase shimmer on step change
            if (animate && game.HandStep != _prevStep)
            {
                PlayShimmer();
            }
            _prevStep = game.HandStep;

            // Hand number + active player (merged into one line)
            string activeInfo = "";
            if (game.Move > 0 && state.Players != null)
            {
                foreach (var p in state.Players)
                {
                    if (p.Seat == game.Move)
                    {
                        activeInfo = $"  \u00B7  {p.Username} to act";
                        break;
                    }
                }
            }
            _handNumber.text = $"Hand #{game.GameNo}{activeInfo}";

            // Pot (with tween)
            if (_potText != null)
            {
                float newPot = game.Pot;
                string sidePotStr = BuildSidePotString(game);

                if (animate && Mathf.Abs(newPot - _prevPot) > 0.01f && newPot > 0)
                {
                    float fromPot = _prevPot;
                    AnimController.Play(Tweener.TweenFloat(fromPot, newPot, 0.4f,
                        v => _potText.text = $"Pot: {MoneyFormatter.Format(v)}{sidePotStr}"));
                }
                else
                {
                    _potText.text = newPot > 0
                        ? $"Pot: {MoneyFormatter.Format(newPot)}{sidePotStr}"
                        : "";
                }
                _prevPot = newPot;
            }

            // Blinds
            _blindsText.text = $"Blinds: {MoneyFormatter.Format(game.SmallBlind)} / {MoneyFormatter.Format(game.BigBlind)}";

            _hasRenderedOnce = true;
        }

        private string BuildSidePotString(GameState game)
        {
            if (game.SidePots == null || game.SidePots.Count == 0)
                return "";

            string result = "";
            for (int i = 0; i < game.SidePots.Count; i++)
            {
                result += $"\nSide Pot {i + 1}: {MoneyFormatter.Format(game.SidePots[i].Amount)}";
            }
            return result;
        }

        private void PlayShimmer()
        {
            _shimmerBar.gameObject.SetActive(true);
            var shimmerRt = _shimmerBar.GetComponent<RectTransform>();
            float range = _shimmerContainer.sizeDelta.x / 2f + 15f;

            var handle = AnimController.Play(Tweener.TweenFloat(-range, range, 0.5f,
                x => { if (shimmerRt != null) shimmerRt.anchoredPosition = new Vector2(x, 0); },
                EaseType.Linear));

            handle.SnapToFinal = () =>
            {
                if (_shimmerBar != null) _shimmerBar.gameObject.SetActive(false);
            };

            handle.OnComplete(() =>
            {
                if (_shimmerBar != null) _shimmerBar.gameObject.SetActive(false);
            });
        }

        public void SetStatus(string message)
        {
            _statusText.text = message;
        }
    }
}
