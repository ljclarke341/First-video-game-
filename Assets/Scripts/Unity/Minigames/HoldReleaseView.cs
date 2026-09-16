using GarageTycoon.Core.Minigames;
using GarageTycoon.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.Minigames
{
    /// <summary>
    /// Draws the torque gauge: a horizontal bar that fills while the player holds, a green safe band
    /// to release inside, and a red danger stretch beyond it leading to the redline.
    /// </summary>
    public sealed class HoldReleaseView : MinigameView
    {
        private const float GaugeHeight = 110f;

        private Text _prompt;
        private RectTransform _gauge;
        private Image _dangerBand;
        private Image _targetBand;
        private Image _perfectCore;
        private Image _fill;
        private RectTransform _needle;
        private Button _holdButton;
        private Text _holdLabel;
        private PointerButton _pointer;

        public override MinigameType Type { get { return MinigameType.HoldRelease; } }

        protected override void OnBuild()
        {
            _prompt = UIFactory.CreateText("Prompt", Root, "Hold, then release in the green", Theme.FontHeading,
                Theme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.AnchorTop(_prompt.rectTransform, 56f, 8f, Theme.PanelPadding);

            Image gaugeImage = UIFactory.CreatePanel("Gauge", Root, Theme.PanelSunken, 16);
            _gauge = gaugeImage.rectTransform;
            UIFactory.AnchorTop(_gauge, GaugeHeight, 86f, Theme.PanelPadding * 2f);

            // Danger stretch first so the bands paint on top of it.
            _dangerBand = UIFactory.CreatePanel("DangerBand", _gauge, Theme.WithAlpha(Theme.DangerZone, 0.35f), 12);
            _targetBand = UIFactory.CreatePanel("TargetBand", _gauge, Theme.SweetSpot, 12);
            _perfectCore = UIFactory.CreatePanel("PerfectCore", _gauge, Theme.PerfectZone, 10);

            // The fill is drawn over the bands, semi-transparent, so the zones stay visible underneath.
            _fill = UIFactory.CreatePanel("Fill", _gauge, Theme.WithAlpha(Theme.Info, 0.55f), 12);
            _fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            _fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _fill.rectTransform.offsetMin = new Vector2(0f, 6f);
            _fill.rectTransform.offsetMax = new Vector2(0f, -6f);

            Image needleImage = UIFactory.CreatePanel("Needle", _gauge, Theme.Marker, 6);
            _needle = needleImage.rectTransform;
            _needle.sizeDelta = new Vector2(10f, 0f);

            // The hold button is deliberately huge: it is the only thing the player touches.
            _holdButton = UIFactory.CreateButton("HoldButton", Root, "HOLD", Theme.Info, Theme.TextOnAccent, Theme.FontTitle);
            UIFactory.AnchorBottom(_holdButton.GetComponent<RectTransform>(), 190f, 30f, Theme.PanelPadding * 2f);
            _holdLabel = _holdButton.GetComponentInChildren<Text>();

            // Button handles the visuals; PointerButton gives us the press/release timing.
            _holdButton.transition = Selectable.Transition.ColorTint;
            _pointer = _holdButton.gameObject.AddComponent<PointerButton>();
            _pointer.Pressed += HandlePressed;
            _pointer.Released += HandleReleased;
        }

        private void HandlePressed()
        {
            if (Simulation != null) Simulation.PlayerPress();
        }

        private void HandleReleased()
        {
            if (Simulation != null) Simulation.PlayerRelease();
        }

        protected override void OnBind()
        {
            HoldReleaseMinigame game = Minigame as HoldReleaseMinigame;
            if (game == null) return;

            _prompt.text = game.Prompt;
            _pointer.ResetState();

            // Everything past the safe band is danger, up to the redline at 1.0.
            float bandTop = Mathf.Clamp01(game.TargetCenter + game.TargetHalfWidth);
            SetBand(_dangerBand.rectTransform, bandTop, 1f);
            SetBand(_targetBand.rectTransform, game.TargetCenter - game.TargetHalfWidth, game.TargetCenter + game.TargetHalfWidth);
            SetBand(_perfectCore.rectTransform, game.TargetCenter - game.PerfectHalfWidth, game.TargetCenter + game.PerfectHalfWidth);
        }

        protected override void OnUnbind()
        {
            if (_pointer != null) _pointer.ResetState();
        }

        protected override void OnRefresh()
        {
            HoldReleaseMinigame game = Minigame as HoldReleaseMinigame;
            if (game == null) return;

            float pressure = Mathf.Clamp01(game.Pressure / HoldReleaseMinigame.BlowoutPressure);

            _fill.rectTransform.anchorMax = new Vector2(pressure, 1f);
            _fill.rectTransform.offsetMin = new Vector2(0f, 6f);
            _fill.rectTransform.offsetMax = new Vector2(0f, -6f);

            _needle.anchorMin = new Vector2(pressure, 0f);
            _needle.anchorMax = new Vector2(pressure, 1f);
            _needle.offsetMin = new Vector2(-5f, 4f);
            _needle.offsetMax = new Vector2(5f, -4f);

            // Colour the fill by what releasing right now would earn: gold, green, or red.
            float distance = Mathf.Abs(game.Pressure - game.TargetCenter);
            Color fillColor;
            if (distance <= game.PerfectHalfWidth) fillColor = Theme.PerfectZone;
            else if (distance <= game.TargetHalfWidth) fillColor = Theme.SweetSpot;
            else if (game.Pressure > game.TargetCenter) fillColor = Theme.DangerZone;
            else fillColor = Theme.Info;

            _fill.color = Theme.WithAlpha(fillColor, 0.55f);

            _holdLabel.text = game.IsHolding ? "RELEASE!" : "HOLD";
        }

        /// <summary>Positions a band between two normalised gauge positions.</summary>
        private static void SetBand(RectTransform rect, float from, float to)
        {
            float min = Mathf.Clamp01(Mathf.Min(from, to));
            float max = Mathf.Clamp01(Mathf.Max(from, to));

            rect.anchorMin = new Vector2(min, 0f);
            rect.anchorMax = new Vector2(max, 1f);
            rect.offsetMin = new Vector2(0f, 6f);
            rect.offsetMax = new Vector2(0f, -6f);
        }
    }
}
