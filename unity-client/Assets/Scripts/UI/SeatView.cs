using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HijackPoker.Animation;
using HijackPoker.Managers;
using HijackPoker.Models;
using HijackPoker.Utils;

namespace HijackPoker.UI
{
    /// <summary>
    /// Renders a single player seat: avatar, name, stack, hole cards, bet,
    /// position badge, action badge, hand rank, and winnings.
    /// Animates transitions: stack/bet tweens, card flips, glow pulses, badge pops.
    /// </summary>
    public class SeatView : MonoBehaviour
    {
        // Seat positions are now determined by layout groups, not absolute anchors.

        private int _seatNumber;
        private RectTransform _rt;
        private CanvasGroup _canvasGroup;

        // UI elements
        private Image _avatarBg;
        private TextMeshProUGUI _avatarText;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _stackText;
        private CardView _card1;
        private CardView _card2;
        private TextMeshProUGUI _betText;
        private RectTransform _positionBadge;
        private TextMeshProUGUI _positionBadgeText;
        private RectTransform _actionBadge;
        private TextMeshProUGUI _actionBadgeText;
        private Image _actionBadgeBg;
        private RectTransform _allInBadge;
        private TextMeshProUGUI _handRankText;
        private TextMeshProUGUI _winningsText;
        private Image _activeGlow;
        private CanvasGroup _actionBadgeCg;
        private CanvasGroup _betCg;

        // ── Animation state ─────────────────────────────────────────
        private bool _hasRenderedOnce;
        private float _prevStack;
        private float _prevBet;
        private bool _wasActive;
        private bool _wasWinner;
        private bool _wasFolded;
        private string _prevAction;
        private bool _prevHadWinnings;
        private bool _prevCardsWereFaceDown;
        private float? _deferredStack;

        // Active continuous tweens (cancelled on state change)
        private TweenHandle _activeGlowTween;
        private TweenHandle _winnerGlowTween;

        // In-flight card animation handles
        private TweenHandle _flipHandle1;
        private TweenHandle _flipHandle2;
        private TweenHandle _foldTiltHandle;
        private TweenHandle _foldFadeHandle;

        public int SeatNumber => _seatNumber;
        public RectTransform RectTransform => _rt;
        public CardView Card1 => _card1;
        public CardView Card2 => _card2;
        public AnimationController AnimController { get; set; }

        public static SeatView Create(int seatNumber, Transform parent)
        {
            var go = new GameObject($"Seat_{seatNumber}", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            var seatSize = LayoutConfig.SeatSize;
            rt.sizeDelta = seatSize;

            // Participate in parent HorizontalLayoutGroup via LayoutElement
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = seatSize.x;
            le.preferredHeight = seatSize.y;

            var view = go.AddComponent<SeatView>();
            view._seatNumber = seatNumber;
            view._rt = rt;
            view._canvasGroup = go.AddComponent<CanvasGroup>();
            view.BuildUI();
            view.SetVisible(false);

            return view;
        }

        private void BuildUI()
        {
            float avatarSize = LayoutConfig.AvatarSize;
            float avatarRingSize = LayoutConfig.AvatarRingSize;
            var infoSz = LayoutConfig.SeatInfoSize;
            float cardSpacing = LayoutConfig.SeatCardSpacing;
            float cardScale = LayoutConfig.SeatCardScale;
            var cardSz = LayoutConfig.CardSize;
            float scaledCardH = cardSz.y * cardScale;
            float spacing = LayoutConfig.SeatElementSpacing;
            float nameW = LayoutConfig.SeatNameWidth;

            // Active glow (stretch fills seat bounds, behind everything)
            _activeGlow = UIFactory.CreateImage("ActiveGlow", transform,
                new Color(UIFactory.ActiveGlowCyan.r, UIFactory.ActiveGlowCyan.g,
                    UIFactory.ActiveGlowCyan.b, 0f));
            var glowRt = _activeGlow.GetComponent<RectTransform>();
            UIFactory.StretchFill(glowRt);

            // ── Main column (VerticalLayoutGroup) ─────────────────────────
            var columnGo = new GameObject("Column", typeof(RectTransform));
            columnGo.transform.SetParent(transform, false);
            var columnRt = columnGo.GetComponent<RectTransform>();
            UIFactory.StretchFill(columnRt);

            var vlg = columnGo.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(10, 10, 8, 8);

            // ── Section 1: Avatar ─────────────────────────────────────────
            var avatarSection = new GameObject("AvatarSection", typeof(RectTransform));
            avatarSection.transform.SetParent(columnGo.transform, false);
            var avatarSectionRt = avatarSection.GetComponent<RectTransform>();
            var avatarLE = avatarSection.AddComponent<LayoutElement>();
            avatarLE.preferredWidth = avatarRingSize;
            avatarLE.preferredHeight = avatarRingSize;

            // Avatar ring (centered in section)
            var avatarRingGo = new GameObject("AvatarRing", typeof(RectTransform));
            avatarRingGo.transform.SetParent(avatarSection.transform, false);
            var avatarRingRt = avatarRingGo.GetComponent<RectTransform>();
            UIFactory.SetAnchor(avatarRingRt, 0.5f, 0.5f);
            avatarRingRt.sizeDelta = new Vector2(avatarRingSize, avatarRingSize);
            var ringImg = avatarRingGo.AddComponent<Image>();
            ringImg.color = UIFactory.AvatarRing;
            ringImg.sprite = TextureGenerator.GetCircle((int)avatarRingSize);
            ringImg.raycastTarget = false;

            var avatarGo = new GameObject("Avatar", typeof(RectTransform));
            avatarGo.transform.SetParent(avatarRingGo.transform, false);
            var avatarRt = avatarGo.GetComponent<RectTransform>();
            avatarRt.anchoredPosition = Vector2.zero;
            avatarRt.sizeDelta = new Vector2(avatarSize, avatarSize);
            _avatarBg = avatarGo.AddComponent<Image>();
            _avatarBg.color = UIFactory.AccentCyan;
            _avatarBg.sprite = TextureGenerator.GetCircle((int)avatarSize);
            _avatarBg.raycastTarget = false;

            _avatarText = UIFactory.CreateText("Initials", avatarGo.transform, "",
                LayoutConfig.AvatarFontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.StretchFill(_avatarText.GetComponent<RectTransform>());

            // Position badge — anchored above seat, outside the glow
            var posBadgeSz = LayoutConfig.SeatPositionBadgeSize;
            _positionBadge = UIFactory.CreatePanel("PosBadge", transform,
                UIFactory.PositionBadgeBg, posBadgeSz);
            _positionBadge.anchorMin = new Vector2(0.5f, 1f);
            _positionBadge.anchorMax = new Vector2(0.5f, 1f);
            _positionBadge.pivot = new Vector2(0.5f, 0f);
            _positionBadge.anchoredPosition = new Vector2(0, 2f);
            var posBadgeImg = _positionBadge.GetComponent<Image>();
            if (posBadgeImg == null) posBadgeImg = _positionBadge.gameObject.AddComponent<Image>();
            posBadgeImg.sprite = TextureGenerator.GetRoundedRect((int)posBadgeSz.x, (int)posBadgeSz.y, 6);
            posBadgeImg.type = Image.Type.Sliced;
            _positionBadgeText = UIFactory.CreateText("PosBadgeText", _positionBadge,
                "", 12f, UIFactory.AccentGold, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.StretchFill(_positionBadgeText.GetComponent<RectTransform>());
            _positionBadge.gameObject.SetActive(false);

            // All-in badge — anchored above avatar section
            var allInSz = LayoutConfig.SeatAllInBadgeSize;
            _allInBadge = UIFactory.CreatePanel("AllInBadge", avatarSection.transform,
                null, allInSz);
            _allInBadge.anchorMin = new Vector2(0.5f, 1f);
            _allInBadge.anchorMax = new Vector2(0.5f, 1f);
            _allInBadge.pivot = new Vector2(0.5f, 0f);
            _allInBadge.anchoredPosition = Vector2.zero;
            var allInBg = _allInBadge.gameObject.AddComponent<Image>();
            allInBg.color = UIFactory.AccentMagenta;
            allInBg.sprite = TextureGenerator.GetRoundedRect((int)allInSz.x, (int)allInSz.y, 6);
            allInBg.type = Image.Type.Sliced;
            allInBg.raycastTarget = false;
            var allInText = UIFactory.CreateText("AllInText", _allInBadge, "ALL IN",
                11f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.StretchFill(allInText.GetComponent<RectTransform>());
            _allInBadge.gameObject.SetActive(false);

            // ── Section 2: Info panel (name + stack with backdrop) ─────────
            var infoSection = new GameObject("InfoSection", typeof(RectTransform));
            infoSection.transform.SetParent(columnGo.transform, false);
            var infoSectionRt = infoSection.GetComponent<RectTransform>();
            var infoLE = infoSection.AddComponent<LayoutElement>();
            infoLE.preferredWidth = infoSz.x;
            infoLE.preferredHeight = infoSz.y;

            // Backdrop background on info section
            var backdropImg = infoSection.AddComponent<Image>();
            backdropImg.color = UIFactory.PanelDarkSolid;
            backdropImg.sprite = TextureGenerator.GetRoundedRect(64, 50, 6);
            backdropImg.type = Image.Type.Sliced;

            // Name (upper half of info panel)
            float nameFontMax = LayoutConfig.SeatNameFontSize;
            _nameText = UIFactory.CreateText("Name", infoSection.transform, "",
                nameFontMax, UIFactory.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            var nameRt = _nameText.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(4, 0);
            nameRt.offsetMax = new Vector2(-4, -2);
            _nameText.overflowMode = TextOverflowModes.Ellipsis;
            _nameText.enableAutoSizing = true;
            _nameText.fontSizeMin = 10f;
            _nameText.fontSizeMax = nameFontMax;

            // Stack (lower half of info panel)
            float stackFontMax = LayoutConfig.SeatStackFontSize;
            _stackText = UIFactory.CreateText("Stack", infoSection.transform, "",
                stackFontMax, UIFactory.TextSecondary, TextAlignmentOptions.Center);
            var stackRt = _stackText.GetComponent<RectTransform>();
            stackRt.anchorMin = new Vector2(0f, 0f);
            stackRt.anchorMax = new Vector2(1f, 0.5f);
            stackRt.offsetMin = new Vector2(4, 2);
            stackRt.offsetMax = new Vector2(-4, 0);
            _stackText.overflowMode = TextOverflowModes.Ellipsis;
            _stackText.enableAutoSizing = true;
            _stackText.fontSizeMin = 10f;
            _stackText.fontSizeMax = stackFontMax;

            // ── Section 3: Cards ──────────────────────────────────────────
            float cardsRowW = cardSpacing * 2 + cardSz.x;
            var cardsSection = new GameObject("CardsSection", typeof(RectTransform));
            cardsSection.transform.SetParent(columnGo.transform, false);
            var cardsSectionRt = cardsSection.GetComponent<RectTransform>();
            var cardsLE = cardsSection.AddComponent<LayoutElement>();
            cardsLE.preferredWidth = cardsRowW;
            cardsLE.preferredHeight = scaledCardH;

            _card1 = CardView.Create("Card1", cardsSection.transform);
            _card1.RectTransform.anchoredPosition = new Vector2(-cardSpacing, 0);
            _card1.RectTransform.localScale = Vector3.one * cardScale;

            _card2 = CardView.Create("Card2", cardsSection.transform);
            _card2.RectTransform.anchoredPosition = new Vector2(cardSpacing, 0);
            _card2.RectTransform.localScale = Vector3.one * cardScale;

            // ── Bet amount ───────────────────────────────────────────────
            // Always active in layout (prevents column resize / jump).
            // Visibility toggled via CanvasGroup alpha.
            _betText = UIFactory.CreateText("Bet", columnGo.transform, "",
                stackFontMax, UIFactory.AccentCyan, TextAlignmentOptions.Center, FontStyles.Bold);
            var betLE = _betText.gameObject.AddComponent<LayoutElement>();
            betLE.preferredWidth = LayoutConfig.SeatBetWidth;
            betLE.preferredHeight = 22;
            _betCg = _betText.gameObject.AddComponent<CanvasGroup>();
            _betCg.alpha = 0f;

            // ── Section 4: Action badge ──────────────────────────────────
            // Always active in layout (prevents column resize / jump).
            // Visibility toggled via CanvasGroup alpha.
            var actionSz = LayoutConfig.SeatActionBadgeSize;
            _actionBadge = UIFactory.CreatePanel("ActionBadge", columnGo.transform, null, actionSz);
            var actionLE = _actionBadge.gameObject.AddComponent<LayoutElement>();
            actionLE.preferredWidth = actionSz.x;
            actionLE.preferredHeight = actionSz.y;
            _actionBadgeBg = _actionBadge.gameObject.AddComponent<Image>();
            _actionBadgeBg.color = UIFactory.ActionCall;
            _actionBadgeBg.sprite = TextureGenerator.GetRoundedRect((int)actionSz.x, (int)actionSz.y, 6);
            _actionBadgeBg.type = Image.Type.Sliced;
            _actionBadgeBg.raycastTarget = false;
            _actionBadgeText = UIFactory.CreateText("ActionText", _actionBadge, "",
                13f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.StretchFill(_actionBadgeText.GetComponent<RectTransform>());
            _actionBadgeCg = _actionBadge.gameObject.AddComponent<CanvasGroup>();
            _actionBadgeCg.alpha = 0f;

            // ── Section 5: Hand rank (shown at showdown) ──────────────────
            _handRankText = UIFactory.CreateText("HandRank", columnGo.transform, "",
                13f, UIFactory.AccentGold, TextAlignmentOptions.Center,
                FontStyles.Italic);
            var hrLE = _handRankText.gameObject.AddComponent<LayoutElement>();
            hrLE.preferredWidth = nameW;
            hrLE.preferredHeight = 20;
            _handRankText.gameObject.SetActive(false);

            // ── Winnings (shown at payout) ────────────────────────────────
            _winningsText = UIFactory.CreateText("Winnings", columnGo.transform, "",
                18f, UIFactory.AccentGold, TextAlignmentOptions.Center,
                FontStyles.Bold);
            var wLE = _winningsText.gameObject.AddComponent<LayoutElement>();
            wLE.preferredWidth = nameW;
            wLE.preferredHeight = 26;
            _winningsText.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            // Keep the GameObject always active so it occupies its slot in the
            // HorizontalLayoutGroup.  Hiding via CanvasGroup.alpha avoids
            // layout reflow that makes other seats jump when players join/leave.
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }

        public void UpdateFromState(PlayerState player, GameState game)
        {
            if (player == null)
            {
                SetVisible(false);
                ResetAnimState();
                return;
            }

            SetVisible(true);
            bool animate = _hasRenderedOnce && AnimController != null;

            // Avatar
            _avatarBg.color = UIFactory.GetAvatarColor(player.PlayerId);
            _avatarText.text = UIFactory.GetInitials(player.Username);

            // Name
            _nameText.text = player.Username ?? "";
            if (player.IsAllIn)
                _nameText.color = UIFactory.AccentMagenta;
            else if (player.IsFolded)
                _nameText.color = UIFactory.FoldedTextColor;
            else
                _nameText.color = UIFactory.TextPrimary;

            // Stack (animated tween, deferred when pot fly-in is active)
            float newStack = player.Stack;
            if (_deferredStack.HasValue)
            {
                _stackText.text = MoneyFormatter.Format(_deferredStack.Value);
            }
            else if (animate && Mathf.Abs(newStack - _prevStack) > 0.01f)
            {
                float fromStack = _prevStack;
                AnimController.Play(Tweener.TweenFloat(fromStack, newStack, 0.5f,
                    v => _stackText.text = MoneyFormatter.Format(v)));
            }
            else
            {
                _stackText.text = MoneyFormatter.Format(newStack);
            }
            _prevStack = newStack;

            // Opacity for folded players
            _canvasGroup.alpha = player.IsFolded ? 0.4f : 1f;

            // Hole cards (with fold animation and flip animation)
            UpdateCardsAnimated(player, game, animate);

            // Bet (animated tween)
            UpdateBetAnimated(player, animate);

            // Position badge
            UpdatePositionBadge(player.Seat, game);

            // Action badge (with scale pop)
            UpdateActionBadgeAnimated(player, animate);

            // Persistent ALL-IN badge
            _allInBadge.gameObject.SetActive(player.IsAllIn);

            // Active glow (breathing pulse)
            UpdateActiveGlow(player, game, animate);

            // Winner state (pulsing gold glow)
            UpdateWinnerState(player, game, animate);

            // Collapse invisible bet/action from layout when showing
            // end-of-hand elements, so they don't squish the content.
            bool endOfHand = _handRankText.gameObject.activeSelf
                || _winningsText.gameObject.activeSelf;
            _betText.gameObject.SetActive(!endOfHand);
            _actionBadge.gameObject.SetActive(!endOfHand);

            _hasRenderedOnce = true;
        }

        /// <summary>
        /// Called by GameManager after CancelAll() to ensure continuous tweens
        /// (glow pulses) are restarted on the next render.
        /// </summary>
        public void ResetContinuousTweens()
        {
            _activeGlowTween = null;
            _winnerGlowTween = null;
            _deferredStack = null;
            // Reset transition flags so glows restart on next UpdateFromState
            _wasActive = false;
            _wasWinner = false;
        }

        /// <summary>
        /// Defers the next stack tween — stack text holds old value until
        /// AnimateDeferredStack() is called (after pot fly-in arrives).
        /// </summary>
        public void DeferNextStackTween()
        {
            _deferredStack = _prevStack;
        }

        /// <summary>
        /// Tweens stack from old deferred value to the actual current value.
        /// Called by GameManager after pot fly-in animation arrives at this seat.
        /// </summary>
        public void AnimateDeferredStack()
        {
            if (!_deferredStack.HasValue) return;
            float from = _deferredStack.Value;
            float to = _prevStack;
            _deferredStack = null;

            if (AnimController != null && Mathf.Abs(to - from) > 0.01f)
            {
                AnimController.Play(Tweener.TweenFloat(from, to, 0.5f,
                    v => _stackText.text = MoneyFormatter.Format(v)));
            }
            else
            {
                _stackText.text = MoneyFormatter.Format(to);
            }
        }

        // ── Animated sub-updates ────────────────────────────────────

        private void UpdateCardsAnimated(PlayerState player, GameState game, bool animate)
        {
            if (!player.HasCards || game.HandStep < 4 || player.IsFolded)
            {
                // If fold animation is still in flight, let it finish
                if (_foldTiltHandle != null && !_foldTiltHandle.IsComplete)
                    goto TrackState;

                // Fold animation: cards tilt and fade when player just folded
                if (player.IsFolded && !_wasFolded && animate
                    && _card1.CurrentState != CardView.State.Empty)
                {
                    AnimateFoldCards();
                }
                else
                {
                    _card1.SetEmpty();
                    _card2.SetEmpty();
                }

                TrackState:
                _wasFolded = player.IsFolded;
                _prevCardsWereFaceDown = false;
                return;
            }

            string card1Str = player.Cards.Count > 0 ? player.Cards[0] : null;
            string card2Str = player.Cards.Count > 1 ? player.Cards[1] : null;

            bool shouldShowFaceUp = ShowdownLogic.ShouldShowCards(
                game.HandStep, player.Status, player.Winnings);

            // Card flip animation: face-down -> face-up at showdown
            if (shouldShowFaceUp && _prevCardsWereFaceDown && animate)
            {
                if (!string.IsNullOrEmpty(card1Str))
                    _flipHandle1 = _card1.AnimateFlip(card1Str, AnimController);
                if (!string.IsNullOrEmpty(card2Str))
                    _flipHandle2 = _card2.AnimateFlip(card2Str, AnimController);
            }
            else
            {
                // Don't overwrite cards mid-flip
                bool card1Flipping = _flipHandle1 != null && !_flipHandle1.IsComplete;
                bool card2Flipping = _flipHandle2 != null && !_flipHandle2.IsComplete;

                if (!card1Flipping)
                    _card1.SetFromPlayerData(card1Str, game.HandStep, player.Status, player.Winnings);
                if (!card2Flipping)
                    _card2.SetFromPlayerData(card2Str, game.HandStep, player.Status, player.Winnings);
            }

            _prevCardsWereFaceDown = !shouldShowFaceUp
                && _card1.CurrentState != CardView.State.Empty;
            _wasFolded = player.IsFolded;
        }

        private void AnimateFoldCards()
        {
            AudioManager.Instance?.Play(SoundType.FoldSwoosh);
            float duration = 0.3f;
            System.Action snapBothToFinal = () =>
            {
                _card1.SetEmpty();
                _card2.SetEmpty();
                _card1.ResetFoldVisuals();
                _card2.ResetFoldVisuals();
            };

            // Tilt cards in opposite directions
            _foldTiltHandle = AnimController.Play(Tweener.TweenFloat(0f, 15f, duration,
                v =>
                {
                    _card1.RectTransform.localEulerAngles = new Vector3(0, 0, v);
                    _card2.RectTransform.localEulerAngles = new Vector3(0, 0, -v);
                }));
            _foldTiltHandle.SnapToFinal = snapBothToFinal;

            // Fade cards out
            _foldFadeHandle = AnimController.Play(Tweener.TweenFloat(1f, 0f, duration,
                v =>
                {
                    _card1.CanvasGroup.alpha = v;
                    _card2.CanvasGroup.alpha = v;
                }));
            _foldFadeHandle.SnapToFinal = snapBothToFinal;
            _foldFadeHandle.OnComplete(snapBothToFinal);
        }

        private void UpdateBetAnimated(PlayerState player, bool animate)
        {
            float newBet = player.Bet;
            if (newBet > 0)
            {
                _betCg.alpha = 1f;
                if (animate && Mathf.Abs(newBet - _prevBet) > 0.01f)
                {
                    float fromBet = _prevBet;
                    AnimController.Play(Tweener.TweenFloat(fromBet, newBet, 0.3f,
                        v => _betText.text = MoneyFormatter.Format(v)));
                    if (newBet > _prevBet)
                        AudioManager.Instance?.Play(SoundType.ChipClink);
                }
                else
                {
                    _betText.text = MoneyFormatter.Format(newBet);
                }
            }
            else
            {
                _betCg.alpha = 0f;
            }
            _prevBet = newBet;
        }

        private void UpdateActionBadgeAnimated(PlayerState player, bool animate)
        {
            if (string.IsNullOrEmpty(player.Action))
            {
                _actionBadgeCg.alpha = 0f;
                _prevAction = null;
                return;
            }

            string displayAction = player.Action.ToUpper();
            _actionBadgeText.text = displayAction;

            Color badgeColor;
            switch (player.Action.ToLower())
            {
                case "check": badgeColor = UIFactory.ActionCheck; break;
                case "call": badgeColor = UIFactory.ActionCall; break;
                case "bet":
                case "raise": badgeColor = UIFactory.ActionBet; break;
                case "fold": badgeColor = UIFactory.ActionFold; break;
                case "allin": badgeColor = UIFactory.ActionAllIn; break;
                default: badgeColor = UIFactory.TextMuted; break;
            }
            _actionBadgeBg.color = badgeColor;

            _actionBadgeCg.alpha = 1f;

            // Scale pop when action first appears or changes
            bool isNewAction = player.Action != _prevAction;
            if (animate && isNewAction)
            {
                AnimController.Play(Tweener.ScalePop(_actionBadge, 0.2f, 1.3f));
            }

            _prevAction = player.Action;
        }

        private void UpdateActiveGlow(PlayerState player, GameState game, bool animate)
        {
            bool isActive = game.Move == player.Seat && game.Move > 0;

            // Restart glow if handle was killed by CancelAll (detected by null handle + wasActive)
            bool needsRestart = isActive
                && (_activeGlowTween == null || _activeGlowTween.IsComplete);

            if (isActive && (!_wasActive || needsRestart))
            {
                // Start/restart breathing cyan glow
                _activeGlowTween?.Cancel();
                _activeGlowTween = AnimController?.Play(Tweener.PulseGlow(
                    a => _activeGlow.color = new Color(0, 0.83f, 1f, a),
                    0.05f, 0.2f, 1.0f));

                if (_activeGlowTween == null)
                    _activeGlow.color = new Color(0, 0.83f, 1f, 0.12f);
            }
            else if (!isActive && _wasActive)
            {
                _activeGlowTween?.Cancel();
                _activeGlowTween = null;
                _activeGlow.color = new Color(0, 0, 0, 0);
            }
            else if (!isActive)
            {
                // Keep glow off (unless winner glow handles it)
                if (!_wasWinner && !(player.IsWinner && game.HandStep >= 13))
                    _activeGlow.color = new Color(0, 0, 0, 0);
            }

            _wasActive = isActive;
        }

        private void UpdateWinnerState(PlayerState player, GameState game, bool animate)
        {
            bool isWinner = player.IsWinner && game.HandStep >= 13;

            // Restart glow if handle was killed by CancelAll
            bool needsRestart = isWinner
                && (_winnerGlowTween == null || _winnerGlowTween.IsComplete);

            if (isWinner && (!_wasWinner || needsRestart))
            {
                // Start/restart pulsing gold glow
                _activeGlowTween?.Cancel();
                _winnerGlowTween?.Cancel();
                _winnerGlowTween = AnimController?.Play(Tweener.PulseGlow(
                    a => _activeGlow.color = new Color(1f, 0.84f, 0f, a),
                    0.05f, 0.25f, 0.8f));

                if (_winnerGlowTween == null)
                    _activeGlow.color = new Color(1f, 0.84f, 0f, 0.15f);
            }
            else if (!isWinner && _wasWinner)
            {
                _winnerGlowTween?.Cancel();
                _winnerGlowTween = null;
            }

            // Hand rank (with fade-in)
            if (isWinner && !string.IsNullOrEmpty(player.HandRank))
            {
                _handRankText.text = player.HandRank;
                _handRankText.gameObject.SetActive(true);

                if (animate && !_wasWinner)
                {
                    var goldFaded = new Color(UIFactory.AccentGold.r, UIFactory.AccentGold.g,
                        UIFactory.AccentGold.b, 0f);
                    _handRankText.color = goldFaded;
                    AnimController.Play(Tweener.TweenColor(goldFaded, UIFactory.AccentGold,
                        0.4f, c => { if (_handRankText != null) _handRankText.color = c; }));
                }
            }
            else
            {
                _handRankText.gameObject.SetActive(false);
            }

            // Winnings (with scale pop)
            bool hasWinnings = player.Winnings > 0 && game.HandStep >= 14;
            if (hasWinnings)
            {
                _winningsText.text = $"+{MoneyFormatter.Format(player.Winnings)}";
                _winningsText.gameObject.SetActive(true);

                if (animate && !_prevHadWinnings)
                {
                    AnimController.Play(
                        Tweener.ScalePop(_winningsText.transform, 0.4f, 1.2f));
                }
            }
            else
            {
                _winningsText.gameObject.SetActive(false);
            }
            _prevHadWinnings = hasWinnings;

            _wasWinner = isWinner;
        }

        private void UpdatePositionBadge(int seat, GameState game)
        {
            string label = null;
            if (seat == game.DealerSeat) label = "D";
            else if (seat == game.SmallBlindSeat) label = "SB";
            else if (seat == game.BigBlindSeat) label = "BB";

            if (label != null)
            {
                _positionBadge.gameObject.SetActive(true);
                _positionBadgeText.text = label;
            }
            else
            {
                _positionBadge.gameObject.SetActive(false);
            }
        }

        private void ResetAnimState()
        {
            _hasRenderedOnce = false;
            _prevStack = 0;
            _prevBet = 0;
            _wasActive = false;
            _wasWinner = false;
            _wasFolded = false;
            _prevAction = null;
            _prevHadWinnings = false;
            _prevCardsWereFaceDown = false;
            _deferredStack = null;

            _activeGlowTween?.Cancel();
            _activeGlowTween = null;
            _winnerGlowTween?.Cancel();
            _winnerGlowTween = null;
            _flipHandle1 = null;
            _flipHandle2 = null;
            _foldTiltHandle = null;
            _foldFadeHandle = null;
        }
    }
}
