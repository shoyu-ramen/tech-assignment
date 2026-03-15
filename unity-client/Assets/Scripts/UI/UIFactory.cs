using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HijackPoker.Managers;

namespace HijackPoker.UI
{
    public static class UIFactory
    {
        // ── Color palette ────────────────────────────────────────────
        public static readonly Color Background = HexColor("#080812");
        public static readonly Color TableFelt = HexColor("#0e0e1e");
        public static readonly Color TableBorder = HexColor("#00d4ff", 0.25f);
        public static readonly Color AccentCyan = HexColor("#00d4ff");
        public static readonly Color AccentMagenta = HexColor("#ff006e");
        public static readonly Color AccentGold = HexColor("#ffc847");
        public static readonly Color CardFace = HexColor("#1e1e30");
        public static readonly Color CardBack = HexColor("#0e1830");
        public static readonly Color CardBackLight = HexColor("#162040");
        public static readonly Color CardRed = HexColor("#cc2222");
        public static readonly Color TextPrimary = new Color(0.95f, 0.95f, 0.97f, 1f);
        public static readonly Color TextSecondary = new Color(0.65f, 0.65f, 0.72f, 1f);
        public static readonly Color TextMuted = new Color(0.42f, 0.42f, 0.5f, 1f);
        public static readonly Color ActionCheck = HexColor("#22c55e");
        public static readonly Color ActionCall = HexColor("#38bdf8");
        public static readonly Color ActionBet = HexColor("#f59e0b");
        public static readonly Color ActionFold = HexColor("#555566");
        public static readonly Color ActionAllIn = HexColor("#f43f5e");
        public static readonly Color Transparent = new Color(0, 0, 0, 0);

        // ── Extended palette (named inline colors) ─────────────────────
        public static readonly Color PanelDark = new Color(0.04f, 0.04f, 0.08f, 0.7f);
        public static readonly Color PanelDarkSolid = new Color(0.04f, 0.04f, 0.08f, 0.92f);
        public static readonly Color ButtonDefault = new Color(0.12f, 0.12f, 0.22f, 1f);
        public static readonly Color ControlsBarBg = new Color(0.03f, 0.03f, 0.06f, 0.85f);
        public static readonly Color StatusBarBg = new Color(0.03f, 0.03f, 0.06f, 0.6f);
        public static readonly Color SubtleBorder = new Color(0f, 0.6f, 0.8f, 0.15f);
        public static readonly Color TableGlowColor = new Color(0f, 0.5f, 0.7f, 0.08f);
        public static readonly Color TableBorderColor = new Color(0f, 0.6f, 0.8f, 0.2f);
        public static readonly Color FeltColor = new Color(0.09f, 0.09f, 0.18f, 1f);
        public static readonly Color FeltHighlight = new Color(0.12f, 0.12f, 0.22f, 1f);
        public static readonly Color GradientHint = new Color(0.06f, 0.06f, 0.14f, 1f);
        public static readonly Color CardBgFaceUp = new Color(0.92f, 0.90f, 0.87f, 1f);
        public static readonly Color CardInnerBg = new Color(0.97f, 0.96f, 0.94f, 1f);
        public static readonly Color CardEdgeHighlight = new Color(0, 0, 0, 0.08f);
        public static readonly Color CardBlack = new Color(0.10f, 0.10f, 0.13f, 1f);
        public static readonly Color CardPlaceholderOutline = new Color(1, 1, 1, 0.1f);
        public static readonly Color CardPlaceholderFill = new Color(0.05f, 0.05f, 0.1f, 0.5f);
        public static readonly Color CardBackOverlay = new Color(0.10f, 0.16f, 0.40f, 1f);
        public static readonly Color CardBackInner = new Color(0.14f, 0.22f, 0.50f, 1f);
        public static readonly Color CardBackCenter = new Color(0.08f, 0.13f, 0.35f, 1f);
        public static readonly Color CardBackBorder = new Color(0.75f, 0.65f, 0.40f, 0.35f);
        public static readonly Color AvatarRing = new Color(1, 1, 1, 0.15f);
        public static readonly Color ActiveGlowCyan = new Color(0, 0.83f, 1f);
        public static readonly Color PositionBadgeBg = new Color(0.1f, 0.1f, 0.2f, 0.95f);
        public static readonly Color AutoPlayActiveBg = new Color(0f, 0.25f, 0.1f, 1f);
        public static readonly Color InputFieldBg = new Color(0.1f, 0.1f, 0.18f, 1f);
        public static readonly Color HeaderBarBg = new Color(0.06f, 0.06f, 0.12f, 1f);
        public static readonly Color ToggleBg = new Color(0.08f, 0.08f, 0.15f, 0.9f);
        public static readonly Color ShimmerColor = new Color(1, 1, 1, 0.2f);
        public static readonly Color SeparatorColor = new Color(1, 1, 1, 0.08f);
        public static readonly Color FoldedTextColor = new Color(1, 1, 1, 0.4f);
        public static readonly Color WinnerGlowGold = new Color(1f, 0.84f, 0f);

        // ── Avatar colors (deterministic from player ID) ─────────────
        private static readonly Color[] AvatarColors =
        {
            HexColor("#e74c3c"), HexColor("#3498db"), HexColor("#2ecc71"),
            HexColor("#f39c12"), HexColor("#9b59b6"), HexColor("#1abc9c"),
            HexColor("#e67e22"), HexColor("#00bcd4"), HexColor("#8bc34a"),
            HexColor("#ff5722")
        };

        public static Color GetAvatarColor(int playerId)
        {
            return AvatarColors[Mathf.Abs(playerId) % AvatarColors.Length];
        }

        // ── Factory methods ──────────────────────────────────────────

        public static RectTransform CreatePanel(string name, Transform parent,
            Color? color = null, Vector2? size = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();

            if (color.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = color.Value;
            }

            if (size.HasValue)
                rt.sizeDelta = size.Value;

            return rt;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent,
            string text = "", float fontSize = 16f, Color? color = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color ?? TextPrimary;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            return tmp;
        }

        public static Button CreateButton(string name, Transform parent,
            string label, float fontSize = 18f, Color? bgColor = null,
            Color? textColor = null, Vector2? size = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size ?? new Vector2(160, 44);

            var img = go.AddComponent<Image>();
            img.color = bgColor ?? new Color(0.15f, 0.15f, 0.25f, 1f);
            img.sprite = TextureGenerator.GetRoundedRect(64, 44, 6);
            img.type = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;

            btn.onClick.AddListener(() => AudioManager.Instance?.Play(SoundType.ButtonClick));

            var tmp = CreateText("Label", go.transform, label, fontSize,
                textColor ?? TextPrimary);
            var tmpRt = tmp.GetComponent<RectTransform>();
            tmpRt.anchorMin = Vector2.zero;
            tmpRt.anchorMax = Vector2.one;
            tmpRt.sizeDelta = Vector2.zero;
            tmp.raycastTarget = false;

            return btn;
        }

        public static Image CreateImage(string name, Transform parent,
            Color? color = null, Vector2? size = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (size.HasValue)
                rt.sizeDelta = size.Value;

            var img = go.AddComponent<Image>();
            img.color = color ?? Color.white;
            img.raycastTarget = false;

            return img;
        }

        /// <summary>
        /// Sets RectTransform anchors to a single point and positions via anchoredPosition.
        /// </summary>
        public static void SetAnchor(RectTransform rt, float anchorX, float anchorY,
            Vector2? pivot = null)
        {
            rt.anchorMin = new Vector2(anchorX, anchorY);
            rt.anchorMax = new Vector2(anchorX, anchorY);
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Stretch RectTransform to fill parent with optional padding.
        /// </summary>
        public static void StretchFill(RectTransform rt, float padding = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        public static string GetInitials(string username)
        {
            if (string.IsNullOrEmpty(username)) return "?";
            var parts = username.Split(' ');
            if (parts.Length >= 2)
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
            return username.Length >= 2
                ? $"{char.ToUpper(username[0])}{char.ToUpper(username[1])}"
                : $"{char.ToUpper(username[0])}";
        }

        // ── Helpers ──────────────────────────────────────────────────

        public static Color HexColor(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            color.a = alpha;
            return color;
        }
    }
}
