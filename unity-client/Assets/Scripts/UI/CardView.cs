using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HijackPoker.Animation;
using HijackPoker.Managers;
using HijackPoker.Utils;

namespace HijackPoker.UI
{
    /// <summary>
    /// Renders a single playing card: face-up (rank + suit), face-down (blue back),
    /// or empty placeholder (dashed outline).
    /// </summary>
    public class CardView : MonoBehaviour
    {
        public static Vector2 CardSize => LayoutConfig.CardSize;

        private Image _background;
        private TextMeshProUGUI _rankText;
        private TextMeshProUGUI _suitText;
        private TextMeshProUGUI _cornerText;
        private Image _backOverlay;
        private Image _placeholderOutline;
        private Image _shadow;
        private RectTransform _rt;
        private CanvasGroup _canvasGroup;

        public RectTransform RectTransform => _rt;
        public CanvasGroup CanvasGroup => _canvasGroup;

        public enum State { Empty, FaceDown, FaceUp }

        private State _currentState = State.Empty;
        public State CurrentState => _currentState;

        public static CardView Create(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = CardSize;

            var view = go.AddComponent<CardView>();
            view._rt = rt;
            view._canvasGroup = go.AddComponent<CanvasGroup>();
            view.BuildUI();
            view.SetEmpty();
            return view;
        }

        private void BuildUI()
        {
            var roundedSprite = TextureGenerator.GetRoundedRect(64, 96, 8);

            // Drop shadow (behind everything)
            _shadow = UIFactory.CreateImage("Shadow", transform,
                new Color(0, 0, 0, 0.45f), CardSize);
            _shadow.sprite = roundedSprite;
            _shadow.type = Image.Type.Sliced;
            var shadowRt = _shadow.GetComponent<RectTransform>();
            UIFactory.StretchFill(shadowRt);
            shadowRt.offsetMin = new Vector2(-2, -5);
            shadowRt.offsetMax = new Vector2(2, 0);

            // Placeholder outline (dashed border effect — subtle dim outline)
            _placeholderOutline = UIFactory.CreateImage("Outline", transform,
                UIFactory.CardPlaceholderOutline, CardSize);
            _placeholderOutline.sprite = roundedSprite;
            _placeholderOutline.type = Image.Type.Sliced;
            var outlineRt = _placeholderOutline.GetComponent<RectTransform>();
            UIFactory.StretchFill(outlineRt);

            var innerPlaceholder = UIFactory.CreateImage("InnerPlaceholder", _placeholderOutline.transform,
                UIFactory.CardPlaceholderFill);
            innerPlaceholder.sprite = roundedSprite;
            innerPlaceholder.type = Image.Type.Sliced;
            var ipRt = innerPlaceholder.GetComponent<RectTransform>();
            ipRt.anchorMin = Vector2.zero;
            ipRt.anchorMax = Vector2.one;
            ipRt.offsetMin = new Vector2(2, 2);
            ipRt.offsetMax = new Vector2(-2, -2);

            // Card background (face-up) — ivory card face
            _background = UIFactory.CreateImage("CardBg", transform,
                UIFactory.CardBgFaceUp, CardSize);
            _background.sprite = roundedSprite;
            _background.type = Image.Type.Sliced;
            var bgRt = _background.GetComponent<RectTransform>();
            UIFactory.StretchFill(bgRt);

            // Subtle border tint on face-up card
            var cardEdge = UIFactory.CreateImage("CardEdge", _background.transform,
                UIFactory.CardEdgeHighlight);
            cardEdge.sprite = roundedSprite;
            cardEdge.type = Image.Type.Sliced;
            var edgeRt = cardEdge.GetComponent<RectTransform>();
            UIFactory.StretchFill(edgeRt);

            // Inner card face — clean white playing surface
            var cardInner = UIFactory.CreateImage("CardInner", _background.transform,
                UIFactory.CardInnerBg);
            cardInner.sprite = roundedSprite;
            cardInner.type = Image.Type.Sliced;
            var innerRt = cardInner.GetComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(1.5f, 1.5f);
            innerRt.offsetMax = new Vector2(-1.5f, -1.5f);

            // Corner pip (top-left): rank + suit
            _cornerText = UIFactory.CreateText("Corner", _background.transform, "",
                11f, UIFactory.CardBlack, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            _cornerText.lineSpacing = -25;
            var cornerRt = _cornerText.GetComponent<RectTransform>();
            cornerRt.anchorMin = Vector2.zero;
            cornerRt.anchorMax = Vector2.one;
            cornerRt.offsetMin = new Vector2(4, 0);
            cornerRt.offsetMax = new Vector2(0, -3);

            // Center suit pip (the main visual)
            _suitText = UIFactory.CreateText("Suit", _background.transform, "",
                22f, UIFactory.CardBlack, TextAlignmentOptions.Center);
            var suitRt = _suitText.GetComponent<RectTransform>();
            suitRt.anchorMin = new Vector2(0, 0.22f);
            suitRt.anchorMax = new Vector2(1, 0.78f);
            suitRt.offsetMin = Vector2.zero;
            suitRt.offsetMax = Vector2.zero;

            // Corner pip (bottom-right, rotated 180°): rank + suit
            _rankText = UIFactory.CreateText("CornerBR", _background.transform, "",
                11f, UIFactory.CardBlack, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            _rankText.lineSpacing = -25;
            var rankRt = _rankText.GetComponent<RectTransform>();
            rankRt.anchorMin = Vector2.zero;
            rankRt.anchorMax = Vector2.one;
            rankRt.offsetMin = new Vector2(0, 5);
            rankRt.offsetMax = new Vector2(-5, 0);
            rankRt.localEulerAngles = new Vector3(0, 0, 180);

            // Face-down overlay — royal blue card back
            _backOverlay = UIFactory.CreateImage("Back", transform,
                UIFactory.CardBackOverlay, CardSize);
            _backOverlay.sprite = roundedSprite;
            _backOverlay.type = Image.Type.Sliced;
            var backRt = _backOverlay.GetComponent<RectTransform>();
            UIFactory.StretchFill(backRt);

            // Gold border frame on card back
            var backBorder = UIFactory.CreateImage("BackBorder", _backOverlay.transform,
                UIFactory.CardBackBorder);
            backBorder.sprite = roundedSprite;
            backBorder.type = Image.Type.Sliced;
            var bbRt = backBorder.GetComponent<RectTransform>();
            UIFactory.StretchFill(bbRt, 2);

            // Inner area on back
            var backInner = UIFactory.CreateImage("BackInner", _backOverlay.transform,
                UIFactory.CardBackInner);
            backInner.sprite = roundedSprite;
            backInner.type = Image.Type.Sliced;
            var biRt = backInner.GetComponent<RectTransform>();
            biRt.anchorMin = Vector2.zero;
            biRt.anchorMax = Vector2.one;
            biRt.offsetMin = new Vector2(4, 4);
            biRt.offsetMax = new Vector2(-4, -4);

            // Center pattern on back
            var backCenter = UIFactory.CreateImage("BackCenter", backInner.transform,
                UIFactory.CardBackCenter);
            backCenter.sprite = roundedSprite;
            backCenter.type = Image.Type.Sliced;
            var bcRt = backCenter.GetComponent<RectTransform>();
            bcRt.anchorMin = new Vector2(0.12f, 0.12f);
            bcRt.anchorMax = new Vector2(0.88f, 0.88f);
            bcRt.offsetMin = Vector2.zero;
            bcRt.offsetMax = Vector2.zero;
        }

        public void SetEmpty()
        {
            _currentState = State.Empty;
            _shadow.gameObject.SetActive(false);
            _placeholderOutline.gameObject.SetActive(true);
            _background.gameObject.SetActive(false);
            _backOverlay.gameObject.SetActive(false);
        }

        public void SetFaceDown()
        {
            _currentState = State.FaceDown;
            _shadow.gameObject.SetActive(true);
            _placeholderOutline.gameObject.SetActive(false);
            _background.gameObject.SetActive(false);
            _backOverlay.gameObject.SetActive(true);
        }

        public void SetFaceUp(string cardString)
        {
            _currentState = State.FaceUp;
            _shadow.gameObject.SetActive(true);
            _placeholderOutline.gameObject.SetActive(false);
            _background.gameObject.SetActive(true);
            _backOverlay.gameObject.SetActive(false);

            ParsedCard parsed;
            try
            {
                parsed = CardUtils.Parse(cardString);
            }
            catch (System.ArgumentException)
            {
                SetEmpty();
                return;
            }
            var textColor = parsed.IsRed ? UIFactory.CardRed : UIFactory.CardBlack;

            _cornerText.text = $"{parsed.Rank}\n{parsed.Symbol}";
            _cornerText.color = textColor;

            _suitText.text = parsed.Symbol;
            _suitText.color = textColor;

            // Bottom-right corner (rotated 180°)
            _rankText.text = $"{parsed.Rank}\n{parsed.Symbol}";
            _rankText.color = textColor;
        }

        /// <summary>
        /// Set card state from game data: decides face-up vs face-down using ShowdownLogic.
        /// </summary>
        public void SetFromPlayerData(string cardString, int handStep, string status, float winnings)
        {
            if (string.IsNullOrEmpty(cardString))
            {
                SetEmpty();
                return;
            }

            bool showFaceUp = ShowdownLogic.ShouldShowCards(handStep, status, winnings);
            if (showFaceUp)
                SetFaceUp(cardString);
            else
                SetFaceDown();
        }

        /// <summary>
        /// Animated flip from face-down to face-up (scaleX squeeze at midpoint).
        /// </summary>
        public TweenHandle AnimateFlip(string cardString, AnimationController anim)
        {
            return anim.Play(Tweener.FlipCard(_rt, () =>
            {
                SetFaceUp(cardString);
                AudioManager.Instance?.Play(SoundType.CardFlip);
            }, 0.3f));
        }

        /// <summary>
        /// Resets card visual state after a fold animation (alpha, rotation).
        /// Call this after the fold tween completes.
        /// </summary>
        public void ResetFoldVisuals()
        {
            _canvasGroup.alpha = 1f;
            _rt.localEulerAngles = Vector3.zero;
        }
    }
}
