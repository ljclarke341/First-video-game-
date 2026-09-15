using GarageTycoon.Core.Minigames;
using GarageTycoon.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.Minigames
{
    /// <summary>
    /// Draws the timing bar: a track with a green sweet spot, a gold perfect core, and a marker
    /// sweeping back and forth. The whole panel is the tap target so the player never has to aim
    /// at a small button while watching the marker.
    /// </summary>
    public sealed class TimingBarView : MinigameView
    {
        private const float TrackHeight = 96f;

        private Text _prompt;
        private RectTransform _track;
        private Image _sweetSpot;
        private Image _perfectCore;
        private RectTransform _marker;
        private Text _hint;

        public override MinigameType Type { get { return MinigameType.TimingBar; } }

        protected override void OnBuild()
        {
            _prompt = UIFactory.CreateText("Prompt", Root, "Tap inside the green zone", Theme.FontHeading,
                Theme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.AnchorTop(_prompt.rectTransform, 60f, 10f, Theme.PanelPadding);

            // The track sits in the middle of the panel.
            Image trackImage = UIFactory.CreatePanel("Track", Root, Theme.PanelSunken, 16);
            _track = trackImage.rectTransform;
            _track.anchorMin = new Vector2(0f, 0.5f);
            _track.anchorMax = new Vector2(1f, 0.5f);
            _track.pivot = new Vector2(0.5f, 0.5f);
            _track.offsetMin = new Vector2(Theme.PanelPadding * 2f, -TrackHeight * 0.5f);
            _track.offsetMax = new Vector2(-Theme.PanelPadding * 2f, TrackHeight * 0.5f);

            // Sweet spot and perfect core are positioned by normalised anchors each frame.
            _sweetSpot = UIFactory.CreatePanel("SweetSpot", _track, Theme.SweetSpot, 12);
            _perfectCore = UIFactory.CreatePanel("PerfectCore", _track, Theme.PerfectZone, 10);

            Image markerImage = UIFactory.CreatePanel("Marker", _track, Theme.Marker, 6);
            _marker = markerImage.rectTransform;
            _marker.anchorMin = new Vector2(0.5f, 0f);
            _marker.anchorMax = new Vector2(0.5f, 1f);
            _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(12f, 26f);
            _marker.anchoredPosition = Vector2.zero;

            _hint = UIFactory.CreateText("Hint", Root, "TAP ANYWHERE", Theme.FontSmall,
                Theme.TextMuted, TextAnchor.MiddleCenter);
            UIFactory.AnchorBottom(_hint.rectTransform, 50f, 18f, Theme.PanelPadding);

            // A transparent button over the whole panel: the tap target is the entire area.
            Image tapArea = UIFactory.CreateImage("TapArea", Root, new Color(1f, 1f, 1f, 0f));
            UIFactory.Stretch(tapArea.rectTransform);
            tapArea.raycastTarget = true;

            Button button = tapArea.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (Simulation != null) Simulation.PlayerPress();
            });
        }

        protected override void OnBind()
        {
            TimingBarMinigame game = Minigame as TimingBarMinigame;
            if (game == null) return;

            _prompt.text = game.Prompt;

            // Zones only change when a new round starts, so position them once here.
            SetHorizontalBand(_sweetSpot.rectTransform, game.SweetSpotCenter, game.SweetSpotHalfWidth);
            SetHorizontalBand(_perfectCore.rectTransform, game.SweetSpotCenter, game.PerfectHalfWidth);
        }

        protected override void OnRefresh()
        {
            TimingBarMinigame game = Minigame as TimingBarMinigame;
            if (game == null) return;

            // Slide the marker along the track using the same 0..1 position the logic uses.
            _marker.anchorMin = new Vector2(game.MarkerPosition, 0f);
            _marker.anchorMax = new Vector2(game.MarkerPosition, 1f);
            _marker.offsetMin = new Vector2(-6f, 6f);
            _marker.offsetMax = new Vector2(6f, -6f);

            // Flash the marker gold while it is over the perfect core: a readable "now!" cue.
            bool inPerfect = Mathf.Abs(game.MarkerPosition - game.SweetSpotCenter) <= game.PerfectHalfWidth;
            Image markerImage = _marker.GetComponent<Image>();
            if (markerImage != null) markerImage.color = inPerfect ? Theme.PerfectZone : Theme.Marker;
        }

        /// <summary>Positions a band across the track using normalised centre and half-width.</summary>
        private static void SetHorizontalBand(RectTransform rect, float centre, float halfWidth)
        {
            float min = Mathf.Clamp01(centre - halfWidth);
            float max = Mathf.Clamp01(centre + halfWidth);

            rect.anchorMin = new Vector2(min, 0f);
            rect.anchorMax = new Vector2(max, 1f);
            rect.offsetMin = new Vector2(0f, 8f);
            rect.offsetMax = new Vector2(0f, -8f);
        }
    }
}
