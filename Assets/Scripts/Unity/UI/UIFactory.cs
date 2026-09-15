using System;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// Builds the UI widgets the game is made of: panels, labels, buttons and bars.
    ///
    /// Everything is created in code rather than laid out in prefabs. That means the whole interface
    /// lives in source control as readable C#, and there are no prefab merge conflicts or missing
    /// reference errors to chase - which matters a lot when you are learning.
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;

        /// <summary>
        /// The built-in font Unity ships with, so the project needs no font asset and no TextMeshPro
        /// "import essentials" step before it will run.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                // Unity 2022+ calls it LegacyRuntime.ttf; older versions called it Arial.ttf.
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", Theme.FontBody);

                return _font;
            }
        }

        // ------------------------------------------------------------------
        // Core objects
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates an empty UI object with a RectTransform, parented and ready to position.
        ///
        /// Note the explicit anchors. A freshly created RectTransform anchors to its parent's
        /// BOTTOM-LEFT corner, which is a classic Unity trap: an element positioned at (0, 120)
        /// expecting to sit near the middle of the screen ends up just off the bottom-left instead.
        /// Centring here means anything this factory makes behaves predictably even if the caller
        /// never sets anchors itself.
        /// </summary>
        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            return rect;
        }

        /// <summary>A flat coloured rectangle.</summary>
        public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            if (sprite != null)
            {
                image.sprite = sprite;
                // Any sprite we generate with a border is meant to be 9-sliced.
                image.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            }

            return image;
        }

        /// <summary>A rounded panel, the building block of every card and popup in the game.</summary>
        public static Image CreatePanel(string name, Transform parent, Color color, int radius = -1)
        {
            int cornerRadius = radius < 0 ? Mathf.RoundToInt(Theme.CornerRadius) : radius;
            Image image = CreateImage(name, parent, color, UISprites.RoundedRect(cornerRadius));
            return image;
        }

        /// <summary>A text label. Defaults are tuned for the dark garage background.</summary>
        public static Text CreateText(string name, Transform parent, string content, int fontSize,
            Color? color = null, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();

            text.font = Font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color ?? Theme.TextPrimary;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            return text;
        }

        /// <summary>
        /// A tappable button with a rounded background and a centred label.
        /// Returns the Button so the caller can wire up onClick and keep a handle on it.
        /// </summary>
        public static Button CreateButton(string name, Transform parent, string label, Color background,
            Color? labelColor = null, int fontSize = Theme.FontBody, Action onClick = null)
        {
            Image image = CreatePanel(name, parent, background);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // A visible press state matters a lot on touch, where there is no hover to rely on.
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.65f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            Text text = CreateText("Label", image.transform, label, fontSize, labelColor ?? Theme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform, 12f);

            if (onClick != null) button.onClick.AddListener(() => onClick());

            return button;
        }

        /// <summary>
        /// A progress / timer bar: a sunken track with a fill that is resized by setting
        /// <see cref="ProgressBar.Fraction"/>. Used for repair progress, patience and gauges.
        /// </summary>
        public static ProgressBar CreateProgressBar(string name, Transform parent, Color fillColor, int radius = 10)
        {
            Image track = CreatePanel(name, parent, Theme.PanelSunken, radius);

            Image fill = CreatePanel("Fill", track.transform, fillColor, radius);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(0f, 0f);
            fillRect.offsetMax = new Vector2(0f, 0f);

            ProgressBar bar = track.gameObject.AddComponent<ProgressBar>();
            bar.Initialise(track, fill);
            return bar;
        }

        // ------------------------------------------------------------------
        // Layout helpers
        // ------------------------------------------------------------------

        /// <summary>Makes a rect fill its parent, optionally inset by a margin on all sides.</summary>
        public static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        /// <summary>Anchors a rect to the top of its parent with a fixed height.</summary>
        public static void AnchorTop(RectTransform rect, float height, float topOffset = 0f, float sideMargin = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(sideMargin, -topOffset - height);
            rect.offsetMax = new Vector2(-sideMargin, -topOffset);
        }

        /// <summary>Anchors a rect to the bottom of its parent with a fixed height.</summary>
        public static void AnchorBottom(RectTransform rect, float height, float bottomOffset = 0f, float sideMargin = 0f)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(sideMargin, bottomOffset);
            rect.offsetMax = new Vector2(-sideMargin, bottomOffset + height);
        }

        /// <summary>Fills the parent vertically between a top and bottom inset.</summary>
        public static void AnchorMiddle(RectTransform rect, float topInset, float bottomInset, float sideMargin = 0f)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(sideMargin, bottomInset);
            rect.offsetMax = new Vector2(-sideMargin, -topInset);
        }

        /// <summary>Adds a vertical stack layout, the easy way to lay out lists of rows.</summary>
        public static VerticalLayoutGroup AddVerticalLayout(GameObject target, float spacing, RectOffset padding = null,
            TextAnchor alignment = TextAnchor.UpperCenter)
        {
            VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = alignment;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            return layout;
        }

        /// <summary>Adds a horizontal stack layout.</summary>
        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject target, float spacing, RectOffset padding = null,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            HorizontalLayoutGroup layout = target.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = alignment;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            return layout;
        }

        /// <summary>Pins a preferred height on an element inside a layout group.</summary>
        public static LayoutElement SetPreferredHeight(GameObject target, float height)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();
            if (element == null) element = target.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0f;
            return element;
        }

        /// <summary>Adds a thin outline to a panel, drawn as four child strips.</summary>
        public static void AddOutline(Image panel, Color color, float thickness = 2f)
        {
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = false;
        }

        /// <summary>Adds a CanvasGroup, used for fading popups in and out.</summary>
        public static CanvasGroup AddCanvasGroup(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            return group;
        }
    }

    /// <summary>
    /// A simple bar whose fill is driven by a 0..1 fraction.
    /// Kept as a component (rather than raw RectTransform maths at every call site) so progress bars
    /// behave identically everywhere they appear.
    /// </summary>
    public sealed class ProgressBar : MonoBehaviour
    {
        private Image _track;
        private Image _fill;
        private float _fraction;
        private float _displayed;

        /// <summary>
        /// How quickly the bar slides towards its target, in bar-widths per second.
        /// Zero snaps instantly, which is what you want for something like a torque gauge where the
        /// exact value is the game. Anything above zero smooths, which suits progress and timers.
        /// </summary>
        public float SmoothSpeed { get; set; }

        public void Initialise(Image track, Image fill)
        {
            _track = track;
            _fill = fill;
            SmoothSpeed = 0f;
            _fraction = 0f;
            _displayed = 0f;
            Apply(0f);
        }

        /// <summary>How full the bar is, 0..1. Reading it gives the target, not the drawn position.</summary>
        public float Fraction
        {
            get { return _fraction; }
            set
            {
                _fraction = Mathf.Clamp01(value);

                if (SmoothSpeed <= 0f)
                {
                    _displayed = _fraction;
                    Apply(_displayed);
                }
            }
        }

        private void Update()
        {
            if (SmoothSpeed <= 0f) return;
            if (Mathf.Abs(_displayed - _fraction) < 0.0005f) return;

            // Move towards the target at a fixed rate, then snap over the last sliver so the bar
            // always actually arrives rather than creeping forever.
            _displayed = Mathf.Lerp(_displayed, _fraction, Mathf.Clamp01(Time.deltaTime * SmoothSpeed));
            if (Mathf.Abs(_displayed - _fraction) < 0.002f) _displayed = _fraction;

            Apply(_displayed);
        }

        private void Apply(float fraction)
        {
            if (_fill == null) return;

            RectTransform fillRect = _fill.rectTransform;
            fillRect.anchorMax = new Vector2(fraction, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        /// <summary>Recolours the fill, e.g. a timer bar going from green to red.</summary>
        public Color FillColor
        {
            get { return _fill == null ? Color.white : _fill.color; }
            set { if (_fill != null) _fill.color = value; }
        }

        /// <summary>Recolours the track behind the fill.</summary>
        public Color TrackColor
        {
            get { return _track == null ? Color.white : _track.color; }
            set { if (_track != null) _track.color = value; }
        }

        /// <summary>The bar's own rect, for positioning by the caller.</summary>
        public RectTransform Rect
        {
            get { return _track == null ? null : _track.rectTransform; }
        }
    }
}
