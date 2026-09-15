using UnityEngine;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// Every colour, size and spacing value the UI uses, in one place.
    ///
    /// The whole interface is built from code with generated sprites - there are no imported art
    /// assets - so changing the look of the game means changing numbers here rather than reopening
    /// a design tool.
    /// </summary>
    public static class Theme
    {
        // ---------------- Garage surfaces ----------------

        /// <summary>Dark workshop wall at the top of the screen.</summary>
        public static readonly Color WallTop = Hex("#1B2430");

        /// <summary>Slightly lighter wall further down, for a subtle gradient.</summary>
        public static readonly Color WallBottom = Hex("#232F3E");

        /// <summary>Concrete floor.</summary>
        public static readonly Color Floor = Hex("#2B3644");

        /// <summary>Painted safety stripe on the floor.</summary>
        public static readonly Color FloorStripe = Hex("#3A4757");

        // ---------------- Panels ----------------

        public static readonly Color Panel = Hex("#27323F");
        public static readonly Color PanelRaised = Hex("#33404F");
        public static readonly Color PanelSunken = Hex("#1D2732");
        public static readonly Color PanelOutline = Hex("#46566A");

        // ---------------- Text ----------------

        public static readonly Color TextPrimary = Hex("#F2F5F8");
        public static readonly Color TextSecondary = Hex("#A8B6C6");
        public static readonly Color TextMuted = Hex("#74849A");
        public static readonly Color TextOnAccent = Hex("#10161E");

        // ---------------- Accents ----------------

        public static readonly Color Cash = Hex("#F2C94C");
        public static readonly Color Success = Hex("#27AE60");
        public static readonly Color Warning = Hex("#F2994A");
        public static readonly Color Danger = Hex("#EB5757");
        public static readonly Color Info = Hex("#2D9CDB");
        public static readonly Color Prestige = Hex("#BB6BD9");

        // ---------------- Mini-game colours ----------------

        /// <summary>The green "you scored" zone on bars and gauges.</summary>
        public static readonly Color SweetSpot = Hex("#27AE60");

        /// <summary>The gold core inside the sweet spot that pays a bonus.</summary>
        public static readonly Color PerfectZone = Hex("#F2C94C");

        /// <summary>The red danger area past the redline.</summary>
        public static readonly Color DangerZone = Hex("#EB5757");

        /// <summary>The travelling marker / needle.</summary>
        public static readonly Color Marker = Hex("#FFFFFF");

        // ---------------- Layout ----------------

        /// <summary>Design resolution. The canvas scaler matches phones to this.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        public const float ScreenPadding = 28f;
        public const float PanelPadding = 20f;
        public const float ElementSpacing = 14f;
        public const float CornerRadius = 22f;

        /// <summary>Minimum height for anything a finger has to hit. Keep this generous on mobile.</summary>
        public const float TouchTargetHeight = 108f;

        // ---------------- Type scale ----------------

        public const int FontHuge = 62;
        public const int FontTitle = 44;
        public const int FontHeading = 36;
        public const int FontBody = 30;
        public const int FontSmall = 26;
        public const int FontTiny = 22;

        /// <summary>Turns "#RRGGBB" into a Color, falling back to magenta so mistakes are obvious.</summary>
        public static Color Hex(string hex)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString(hex, out color)) return color;
            return Color.magenta;
        }

        /// <summary>Same colour with a different alpha - handy for overlays and disabled states.</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>
        /// The colour a countdown bar should be at the given fraction of time remaining:
        /// green when there is plenty, amber when it is getting tight, red when it is nearly up.
        /// </summary>
        public static Color TimerColor(float fraction)
        {
            if (fraction > 0.5f) return Success;
            if (fraction > 0.25f) return Warning;
            return Danger;
        }
    }
}
