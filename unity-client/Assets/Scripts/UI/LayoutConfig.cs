using UnityEngine;

namespace HijackPoker.UI
{
    /// <summary>
    /// Centralized responsive layout values. Detects portrait/landscape at startup
    /// and provides sizing and spacing for all UI elements.
    /// Uses layout groups for positioning — no absolute anchor coordinates.
    /// </summary>
    public static class LayoutConfig
    {
        public const int MaxSeats = 6;

        private static bool? _isPortrait;
        private static RectTransform _contentRoot;

        /// <summary>
        /// Detects portrait vs landscape. On iOS, Screen.orientation and
        /// Input.deviceOrientation are checked first since Screen dimensions
        /// may still reflect portrait during early frames before autorotation.
        /// </summary>
        public static bool IsPortrait
        {
            get
            {
                if (_isPortrait.HasValue) return _isPortrait.Value;

                var o = Screen.orientation;
                if (o == ScreenOrientation.LandscapeLeft || o == ScreenOrientation.LandscapeRight)
                    _isPortrait = false;
                else if (o == ScreenOrientation.Portrait || o == ScreenOrientation.PortraitUpsideDown)
                    _isPortrait = true;
                else
                {
                    // AutoRotation — check physical device orientation
                    var d = Input.deviceOrientation;
                    if (d == DeviceOrientation.LandscapeLeft || d == DeviceOrientation.LandscapeRight)
                        _isPortrait = false;
                    else if (d == DeviceOrientation.Portrait || d == DeviceOrientation.PortraitUpsideDown)
                        _isPortrait = true;
                    else
                        _isPortrait = Screen.height > Screen.width;
                }

                return _isPortrait.Value;
            }
        }

        public static void ResetOrientationCache()
        {
            _isPortrait = null;
        }

        public static void SetContentRoot(RectTransform rt)
        {
            _contentRoot = rt;
        }

        // ── Canvas ────────────────────────────────────────────────────

        public static Vector2 ReferenceResolution => IsPortrait
            ? new Vector2(1080, 1920)
            : new Vector2(1920, 1080);

        public static float CanvasMatch => IsPortrait ? 0f : 0.5f;

        // ── Main grid row heights ───────────────────────────────────
        // The main layout is a 3-row vertical grid:
        //   Row 0: HUD (fixed height)
        //   Row 1: Game area (flexible, fills remaining space)
        //   Row 2: Controls (fixed height)

        public static float HudRowHeight => IsPortrait ? 80f : 50f;

        // ── Game area grid ──────────────────────────────────────────
        // The game area is a 3-row vertical grid within the flexible middle:
        //   Top seats row, center row (community cards + pot), bottom seats row
        // Proportions are expressed as flex weights.

        public static float TopSeatsRowFlex => 1f;
        public static float CenterRowFlex => IsPortrait ? 0.6f : 0.5f;
        public static float BottomSeatsRowFlex => 1f;
        public static float GameAreaSpacing => IsPortrait ? 4f : 2f;
        public static float SeatRowSpacing => IsPortrait ? 8f : 12f;

        // ── Table surface (anchor-range, already relative) ──────────

        public static Vector2 TableSurfaceMin => IsPortrait
            ? new Vector2(0.05f, 0.22f) : new Vector2(0.19f, 0.20f);
        public static Vector2 TableSurfaceMax => IsPortrait
            ? new Vector2(0.95f, 0.76f) : new Vector2(0.81f, 0.80f);

        public static Vector2 TableGlowMin => IsPortrait
            ? new Vector2(0.03f, 0.20f) : new Vector2(0.17f, 0.18f);
        public static Vector2 TableGlowMax => IsPortrait
            ? new Vector2(0.97f, 0.78f) : new Vector2(0.83f, 0.82f);

        public static Vector2 GradientMin => IsPortrait
            ? new Vector2(0.03f, 0.15f) : new Vector2(0.15f, 0.15f);
        public static Vector2 GradientMax => IsPortrait
            ? new Vector2(0.97f, 0.85f) : new Vector2(0.85f, 0.85f);

        // ── Card size ─────────────────────────────────────────────────

        public static Vector2 CardSize => IsPortrait
            ? new Vector2(68, 96) : new Vector2(48, 68);

        // ── Controls ──────────────────────────────────────────────────

        public static float ControlsBarHeight => IsPortrait ? 120f : 56f;
        public static float ControlsBarPadding => IsPortrait ? 20f : 40f;
        public static float ControlsButtonHeight => IsPortrait ? 46f : 38f;
        public static float ControlsSecondaryHeight => IsPortrait ? 42f : 38f;

        // ── Seat elements ─────────────────────────────────────────────

        public static Vector2 SeatSize => IsPortrait
            ? new Vector2(200, 300) : new Vector2(160, 240);

        public static float SeatCardSpacing => IsPortrait ? 38f : 28f;
        public static float SeatCardScale => IsPortrait ? 0.88f : 0.9f;

        public static float SeatElementSpacing => IsPortrait ? 4f : 3f;

        // Avatar
        public static float AvatarSize => IsPortrait ? 52f : 42f;
        public static float AvatarRingSize => AvatarSize + 6f;
        public static float AvatarFontSize => IsPortrait ? 18f : 15f;

        // Info panel (name + stack backdrop)
        public static Vector2 SeatInfoSize => IsPortrait
            ? new Vector2(148, 50) : new Vector2(120, 42);
        public static float SeatNameFontSize => IsPortrait ? 15f : 13f;
        public static float SeatNameWidth => IsPortrait ? 160f : 130f;
        public static float SeatStackFontSize => IsPortrait ? 13f : 12f;

        // Badges
        public static Vector2 SeatPositionBadgeSize => IsPortrait
            ? new Vector2(38, 24) : new Vector2(34, 20);
        public static Vector2 SeatActionBadgeSize => IsPortrait
            ? new Vector2(84, 28) : new Vector2(72, 24);
        public static Vector2 SeatAllInBadgeSize => IsPortrait
            ? new Vector2(72, 22) : new Vector2(64, 20);
        public static float SeatBetWidth => IsPortrait ? 120f : 100f;

        // ── HUD elements ────────────────────────────────────────────

        public static Vector2 PhaseLabelContainerSize => IsPortrait
            ? new Vector2(360, 34) : new Vector2(400, 38);
        public static float PhaseFontSize => IsPortrait ? 22f : 24f;
        public static Vector2 PotBgSize => IsPortrait
            ? new Vector2(160, 30) : new Vector2(180, 34);
        public static float PotFontSize => IsPortrait ? 16f : 18f;
        public static float HandNumberWidth => IsPortrait ? 260f : 300f;
        public static float BlindsWidth => IsPortrait ? 200f : 220f;
        public static float StatusWidth => IsPortrait ? 420f : 500f;

        // ── Community cards ─────────────────────────────────────────

        public static float CommunityCardGap => IsPortrait ? 10f : 8f;

        // ── Runtime position helpers ────────────────────────────────
        // Animations need world-space positions of seats/pot/deck at runtime.
        // These are computed from the actual RectTransform positions rather
        // than precomputed anchor coordinates.

        /// <summary>
        /// Converts a RectTransform's world position to a local anchoredPosition
        /// relative to the content root (safe area). Used by animations to get
        /// the canvas-space position of layout-driven elements.
        /// </summary>
        public static Vector2 WorldToCanvasPos(RectTransform element)
        {
            if (_contentRoot == null || element == null) return Vector2.zero;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, element.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _contentRoot, screenPoint, null, out Vector2 localPoint);
            return localPoint;
        }

        /// <summary>
        /// Gets the canvas-space center position of the content root.
        /// Used as the "center of table" for deck/pot animations.
        /// </summary>
        public static Vector2 CanvasCenter => Vector2.zero;
    }
}
