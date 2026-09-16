using System.Collections.Generic;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.Minigames
{
    /// <summary>
    /// Draws the tool matching game: the job is written across the top, the tools flash up on
    /// buttons, and once the preview timer runs out the labels turn into question marks. The player
    /// then has to remember which button held the right tool.
    /// </summary>
    public sealed class ToolMatchView : MinigameView
    {
        /// <summary>The largest option count any difficulty produces. Buttons are pre-built to this.</summary>
        private const int MaxOptions = 5;

        private Text _prompt;
        private Text _instruction;
        private ProgressBar _previewBar;
        private RectTransform _optionRow;

        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Text> _labels = new List<Text>();
        private readonly List<Image> _backgrounds = new List<Image>();

        public override MinigameType Type { get { return MinigameType.ToolMatch; } }

        protected override void OnBuild()
        {
            _prompt = UIFactory.CreateText("Prompt", Root, string.Empty, Theme.FontHeading,
                Theme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.AnchorTop(_prompt.rectTransform, 56f, 6f, Theme.PanelPadding);

            _instruction = UIFactory.CreateText("Instruction", Root, "Memorise the tools", Theme.FontSmall,
                Theme.TextSecondary, TextAnchor.MiddleCenter);
            UIFactory.AnchorTop(_instruction.rectTransform, 40f, 64f, Theme.PanelPadding);

            _previewBar = UIFactory.CreateProgressBar("PreviewBar", Root, Theme.Info, 8);
            UIFactory.AnchorTop(_previewBar.Rect, 14f, 112f, Theme.PanelPadding * 2f);

            // One row of tool buttons, laid out automatically.
            RectTransform rowRect = UIFactory.CreateRect("OptionRow", Root);
            _optionRow = rowRect;
            UIFactory.AnchorMiddle(rowRect, 150f, 40f, Theme.PanelPadding);
            UIFactory.AddHorizontalLayout(rowRect.gameObject, Theme.ElementSpacing);

            for (int i = 0; i < MaxOptions; i++)
            {
                int index = i; // captured per iteration for the click handler

                Button button = UIFactory.CreateButton("Tool" + i, rowRect, string.Empty, Theme.PanelRaised,
                    Theme.TextPrimary, Theme.FontSmall, () =>
                    {
                        if (Simulation != null) Simulation.PlayerSelectOption(index);
                    });

                Text label = button.GetComponentInChildren<Text>();
                // Tool names are long, so let them wrap inside the button rather than spill out.
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;

                _buttons.Add(button);
                _labels.Add(label);
                _backgrounds.Add(button.GetComponent<Image>());
            }
        }

        protected override void OnBind()
        {
            ToolMatchMinigame game = Minigame as ToolMatchMinigame;
            if (game == null) return;

            _prompt.text = game.TaskPrompt;

            // Show exactly as many buttons as this round offers, hide the rest.
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool used = i < game.Options.Count;
                _buttons[i].gameObject.SetActive(used);

                if (used)
                {
                    _labels[i].text = game.Options[i];
                    _backgrounds[i].color = Theme.PanelRaised;
                    _buttons[i].interactable = true;
                }
            }
        }

        protected override void OnRefresh()
        {
            ToolMatchMinigame game = Minigame as ToolMatchMinigame;
            if (game == null) return;

            bool previewing = game.IsPreviewing;

            _instruction.text = previewing ? "Memorise the tools" : "Which one was it?";
            _instruction.color = previewing ? Theme.Info : Theme.Warning;

            _previewBar.Fraction = game.PreviewSeconds <= 0f ? 0f : game.PreviewRemaining / game.PreviewSeconds;
            _previewBar.Rect.gameObject.SetActive(previewing);

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (!_buttons[i].gameObject.activeSelf) continue;

                if (previewing)
                {
                    // Labels readable, buttons dimmed to signal "not yet".
                    _labels[i].text = game.Options[i];
                    _labels[i].color = Theme.TextPrimary;
                    _backgrounds[i].color = Theme.PanelRaised;
                }
                else
                {
                    // Hidden: the player is picking from memory now.
                    _labels[i].text = "?";
                    _labels[i].color = Theme.TextMuted;
                    _backgrounds[i].color = Theme.PanelSunken;
                }
            }
        }
    }
}
