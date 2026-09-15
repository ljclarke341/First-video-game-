using System.Collections.Generic;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.Minigames
{
    /// <summary>
    /// Draws the rapid sequence game: the pattern flashes up one step at a time, then hides, and the
    /// player repeats it on four big direction buttons.
    ///
    /// The buttons are labelled with words rather than arrow glyphs on purpose - the built-in Unity
    /// font does not carry every arrow character on every device, and a missing glyph in a memory
    /// game would be genuinely unfair.
    /// </summary>
    public sealed class RapidSequenceView : MinigameView
    {
        private const int MaxSteps = 6;

        private static readonly string[] DirectionNames = { "UP", "RIGHT", "DOWN", "LEFT" };

        private Text _prompt;
        private RectTransform _stepRow;
        private readonly List<Image> _stepChips = new List<Image>();
        private readonly List<Text> _stepLabels = new List<Text>();
        private readonly List<Button> _directionButtons = new List<Button>();

        public override MinigameType Type { get { return MinigameType.RapidSequence; } }

        protected override void OnBuild()
        {
            _prompt = UIFactory.CreateText("Prompt", Root, "Memorise the pattern", Theme.FontHeading,
                Theme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.AnchorTop(_prompt.rectTransform, 56f, 8f, Theme.PanelPadding);

            // The pattern, shown as a row of chips.
            _stepRow = UIFactory.CreateRect("StepRow", Root);
            UIFactory.AnchorTop(_stepRow, 90f, 74f, Theme.PanelPadding);
            UIFactory.AddHorizontalLayout(_stepRow.gameObject, 10f);

            for (int i = 0; i < MaxSteps; i++)
            {
                Image chip = UIFactory.CreatePanel("Step" + i, _stepRow, Theme.PanelSunken, 12);
                Text label = UIFactory.CreateText("Label", chip.transform, string.Empty, Theme.FontTiny,
                    Theme.TextSecondary, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Stretch(label.rectTransform, 4f);

                _stepChips.Add(chip);
                _stepLabels.Add(label);
            }

            // A four-button pad. Up on top, left/right in the middle, down at the bottom, which is
            // the layout a thumb expects.
            RectTransform pad = UIFactory.CreateRect("DirectionPad", Root);
            UIFactory.AnchorMiddle(pad, 180f, 20f, Theme.PanelPadding);

            CreateDirectionButton(pad, 0, new Vector2(0.33f, 0.62f), new Vector2(0.67f, 1f));   // UP
            CreateDirectionButton(pad, 1, new Vector2(0.68f, 0.31f), new Vector2(1f, 0.69f));   // RIGHT
            CreateDirectionButton(pad, 2, new Vector2(0.33f, 0f), new Vector2(0.67f, 0.38f));   // DOWN
            CreateDirectionButton(pad, 3, new Vector2(0f, 0.31f), new Vector2(0.32f, 0.69f));   // LEFT
        }

        private void CreateDirectionButton(RectTransform parent, int direction, Vector2 anchorMin, Vector2 anchorMax)
        {
            Button button = UIFactory.CreateButton("Dir" + direction, parent, DirectionNames[direction],
                Theme.PanelRaised, Theme.TextPrimary, Theme.FontBody, () =>
                {
                    if (Simulation != null) Simulation.PlayerSelectOption(direction);
                });

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);

            _directionButtons.Add(button);
        }

        protected override void OnBind()
        {
            RapidSequenceMinigame game = Minigame as RapidSequenceMinigame;
            if (game == null) return;

            for (int i = 0; i < _stepChips.Count; i++)
            {
                bool used = i < game.Sequence.Count;
                _stepChips[i].gameObject.SetActive(used);
                if (used) _stepLabels[i].text = DirectionNames[(int)game.Sequence[i]];
            }
        }

        protected override void OnRefresh()
        {
            RapidSequenceMinigame game = Minigame as RapidSequenceMinigame;
            if (game == null) return;

            bool previewing = game.IsPreviewing;

            _prompt.text = game.Prompt;
            _prompt.color = previewing ? Theme.Info : Theme.Warning;

            int highlighted = game.HighlightedStep;

            for (int i = 0; i < _stepChips.Count; i++)
            {
                if (!_stepChips[i].gameObject.activeSelf) continue;

                if (previewing)
                {
                    // Light each step in turn, so the pattern reads as a rhythm rather than a wall of text.
                    bool isCurrent = i == highlighted;
                    _stepChips[i].color = isCurrent ? Theme.Info : Theme.PanelSunken;
                    _stepLabels[i].text = DirectionNames[(int)game.Sequence[i]];
                    _stepLabels[i].color = isCurrent ? Theme.TextPrimary : Theme.TextMuted;
                }
                else
                {
                    // Input phase: chips become progress pips, filling in as the player gets them right.
                    bool entered = i < game.ProgressIndex;
                    _stepChips[i].color = entered ? Theme.Success : Theme.PanelSunken;
                    _stepLabels[i].text = entered ? "OK" : "?";
                    _stepLabels[i].color = entered ? Theme.TextPrimary : Theme.TextMuted;
                }
            }

            // The pad is dead during the preview, and the greying makes that obvious.
            for (int i = 0; i < _directionButtons.Count; i++)
            {
                _directionButtons[i].interactable = !previewing;
            }
        }
    }
}
