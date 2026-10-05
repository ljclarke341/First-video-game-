using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// The inspection ramp: what the customer said, what the garage has worked out so far, and the
    /// checks still worth running.
    ///
    /// This is the Unity half of the same screen the web build shows. All the rules live in
    /// Core (CarCondition, CarDiagnosis, DiagnosisActions) - this only draws them and forwards
    /// taps, exactly like the mini-game views.
    /// </summary>
    public sealed class InspectPanel
    {
        /// <summary>One row of the condition sheet.</summary>
        private sealed class SystemRow
        {
            public Text Name;
            public ProgressBar Bar;
            public Text Percent;
        }

        /// <summary>One check button.</summary>
        private sealed class CheckButton
        {
            public Button Button;
            public Image Background;
            public Text Title;
            public Text Hint;
        }

        private const float RowHeight = 26f;
        private const float CheckHeight = 62f;

        private RectTransform _root;
        private Text _complaint;
        private Text _complaintHeading;
        private Image _complaintPanel;
        private readonly List<SystemRow> _rows = new List<SystemRow>();
        private readonly List<CheckButton> _checks = new List<CheckButton>();
        private Button _skipButton;
        private Button _quoteButton;
        private Text _quoteLabel;

        private GarageSimulation _simulation;
        private ActiveCar _car;

        /// <summary>Raised when the player is done looking and wants to write the quote.</summary>
        public event Action<ActiveCar> QuoteRequested;

        /// <summary>
        /// Raised by "just get stuck in": take the whole car on, now, without looking at it.
        ///
        /// Deliberately NOT the quote event. Skipping used to reveal the entire condition sheet
        /// and then open the quote, which handed the player every reading for free and made
        /// ignoring diagnosis the strongest play in the game. Skipping now buys speed and nothing
        /// else: straight to the spanner, no readings, no bill, no bonus.
        /// </summary>
        public event Action<ActiveCar> CommitRequested;

        public void Build(RectTransform parent, GarageSimulation simulation)
        {
            _simulation = simulation;

            _root = UIFactory.CreateRect("InspectPanel", parent);
            UIFactory.Stretch(_root);

            // ---- what the customer said ----
            Image complaintPanel = UIFactory.CreatePanel("Complaint", _root, Theme.WithAlpha(Theme.Info, 0.12f), 10);
            UIFactory.AnchorTop(complaintPanel.rectTransform, 74f, 0f, 0f);

            _complaintPanel = complaintPanel;

            Text heading = UIFactory.CreateText("Heading", complaintPanel.transform, "CUSTOMER SAYS",
                Theme.FontTiny, Theme.Info, TextAnchor.UpperLeft, FontStyle.Bold);
            _complaintHeading = heading;
            UIFactory.AnchorTop(heading.rectTransform, 22f, 6f, 12f);

            _complaint = UIFactory.CreateText("Text", complaintPanel.transform, string.Empty,
                Theme.FontSmall, Theme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Italic);
            _complaint.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.AnchorTop(_complaint.rectTransform, 44f, 28f, 12f);

            // ---- the condition sheet ----
            RectTransform sheet = UIFactory.CreateRect("Sheet", _root);
            UIFactory.AnchorTop(sheet, RowHeight * VehicleSystemExtensions.Count, 82f, 0f);
            UIFactory.AddVerticalLayout(sheet.gameObject, 1f);

            for (int i = 0; i < VehicleSystemExtensions.Count; i++)
            {
                _rows.Add(BuildRow(sheet, (VehicleSystem)i));
            }

            // ---- the checks, two to a row ----
            float checksTop = 82f + RowHeight * VehicleSystemExtensions.Count + 8f;

            for (int i = 0; i < DiagnosisActions.Count; i++)
            {
                _checks.Add(BuildCheck(_root, (DiagnosisAction)i, i, checksTop));
            }

            // ---- the two ways off the ramp ----
            RectTransform actions = UIFactory.CreateRect("Actions", _root);
            UIFactory.AnchorBottom(actions, 76f, 0f, 0f);
            UIFactory.AddHorizontalLayout(actions.gameObject, 8f);

            // Skipping is always allowed and always free: it is what stops diagnosis ever being
            // a gate. All it costs is the bonus for a thorough inspection.
            _skipButton = UIFactory.CreateButton("Skip", actions, "JUST GET STUCK IN",
                Theme.PanelRaised, Theme.TextPrimary, Theme.FontSmall, HandleSkip);

            _quoteButton = UIFactory.CreateButton("Quote", actions, "WRITE THE QUOTE",
                Theme.Info, Theme.TextOnAccent, Theme.FontSmall, HandleQuote);
            _quoteLabel = _quoteButton.GetComponentInChildren<Text>();

            SetVisible(false);
        }

        private SystemRow BuildRow(RectTransform parent, VehicleSystem system)
        {
            RectTransform row = UIFactory.CreateRect(system.ToString(), parent);
            UIFactory.SetPreferredHeight(row.gameObject, RowHeight);

            SystemRow built = new SystemRow();

            built.Name = UIFactory.CreateText("Name", row, system.DisplayName().ToUpperInvariant(),
                Theme.FontTiny, Theme.TextSecondary, TextAnchor.MiddleLeft, FontStyle.Bold);
            built.Name.rectTransform.anchorMin = new Vector2(0f, 0f);
            built.Name.rectTransform.anchorMax = new Vector2(0f, 1f);
            built.Name.rectTransform.pivot = new Vector2(0f, 0.5f);
            built.Name.rectTransform.sizeDelta = new Vector2(230f, 0f);
            built.Name.rectTransform.anchoredPosition = Vector2.zero;

            built.Bar = UIFactory.CreateProgressBar("Bar", row, Theme.Success, 5);
            built.Bar.Rect.anchorMin = new Vector2(0f, 0.5f);
            built.Bar.Rect.anchorMax = new Vector2(1f, 0.5f);
            built.Bar.Rect.pivot = new Vector2(0.5f, 0.5f);
            built.Bar.Rect.offsetMin = new Vector2(240f, -5f);
            built.Bar.Rect.offsetMax = new Vector2(-80f, 5f);

            built.Percent = UIFactory.CreateText("Percent", row, "--", Theme.FontTiny,
                Theme.TextMuted, TextAnchor.MiddleRight, FontStyle.Bold);
            built.Percent.rectTransform.anchorMin = new Vector2(1f, 0f);
            built.Percent.rectTransform.anchorMax = new Vector2(1f, 1f);
            built.Percent.rectTransform.pivot = new Vector2(1f, 0.5f);
            built.Percent.rectTransform.sizeDelta = new Vector2(72f, 0f);
            built.Percent.rectTransform.anchoredPosition = Vector2.zero;

            return built;
        }

        private CheckButton BuildCheck(RectTransform parent, DiagnosisAction action, int index, float top)
        {
            // Two columns, so all seven checks fit without the ramp needing to scroll.
            int column = index % 2;
            int rowIndex = index / 2;

            Button button = UIFactory.CreateButton("Check" + index, parent, string.Empty,
                Theme.PanelRaised, Theme.TextPrimary, Theme.FontSmall,
                () => HandleCheck(action));

            // CreateButton centres a label; this one wants two lines, left aligned.
            Text placeholder = button.GetComponentInChildren<Text>();
            placeholder.gameObject.SetActive(false);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(column * 0.5f, 1f);
            rect.anchorMax = new Vector2(column * 0.5f + 0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(column == 0 ? 0f : 4f, 0f);
            rect.offsetMax = new Vector2(column == 0 ? -4f : 0f, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, CheckHeight);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -(top + rowIndex * (CheckHeight + 6f)));

            CheckButton built = new CheckButton();
            built.Button = button;
            built.Background = button.GetComponent<Image>();

            built.Title = UIFactory.CreateText("Title", button.transform, action.DisplayName(),
                Theme.FontSmall, Theme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.AnchorTop(built.Title.rectTransform, 30f, 6f, 10f);

            built.Hint = UIFactory.CreateText("Hint", button.transform, action.ShortHint(),
                Theme.FontTiny, Theme.TextMuted, TextAnchor.UpperLeft);
            UIFactory.AnchorTop(built.Hint.rectTransform, 24f, 32f, 10f);

            return built;
        }

        /// <summary>Points the panel at a car and shows it.</summary>
        public void Bind(ActiveCar car)
        {
            _car = car;
            SetVisible(car != null);
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Redraws from the car's current state. Cheap enough to call every frame.</summary>
        public void Refresh()
        {
            if (_root == null || _car == null) return;

            // On a special job the heading carries the warning, because this is the screen where
            // the player decides how many checks to run - and on an urgent car that choice is the
            // whole job. Saying it here rather than afterwards is the point.
            if (_car.Special == null)
            {
                _complaintHeading.text = "CUSTOMER SAYS";
                _complaintHeading.color = Theme.Info;
                _complaintPanel.color = Theme.WithAlpha(Theme.Info, 0.12f);
                _complaint.text = _car.Complaint;
            }
            else
            {
                Color accent = Theme.Hex(_car.Special.ColorHex);
                _complaintHeading.text = _car.Special.DisplayName.ToUpperInvariant() + " - CUSTOMER SAYS";
                _complaintHeading.color = accent;
                _complaintPanel.color = Theme.WithAlpha(accent, 0.14f);
                _complaint.text = _car.Complaint + "  (" + _car.Special.Tagline + ")";

                if (_car.FleetBatchId != 0)
                {
                    _complaintHeading.text = "VAN " + _car.FleetIndex + " OF " + _car.FleetSize
                        + " - " + _car.Special.DisplayName.ToUpperInvariant();
                }
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                VehicleSystem system = (VehicleSystem)i;
                bool known = _car.Diagnosis.IsRevealed(system);
                float health = (float)(_car.Condition.Get(system));

                SystemRow row = _rows[i];

                // An unknown system shows an empty track and "--": the garage genuinely does not
                // know yet, and pretending otherwise would make inspecting pointless.
                row.Bar.Fraction = known ? health : 0f;
                row.Bar.FillColor = ConditionColor(health);
                row.Percent.text = known ? _car.Condition.Percent(system) + "%" : "--";
                row.Percent.color = known ? ConditionColor(health) : Theme.TextMuted;
                row.Name.color = known ? Theme.TextSecondary : Theme.TextMuted;
            }

            for (int i = 0; i < _checks.Count; i++)
            {
                DiagnosisAction action = (DiagnosisAction)i;
                bool done = !_car.Diagnosis.CanRun(action);

                CheckButton check = _checks[i];
                check.Button.interactable = !done;
                check.Background.color = done ? Theme.WithAlpha(Theme.Success, 0.16f) : Theme.PanelRaised;
                check.Hint.text = done ? "Done" : action.ShortHint();
            }

            bool found = _car.Diagnosis.FoundEverything(_car.Condition);
            // With nothing found there is nothing to quote for, so the button says so rather than
            // opening an empty bill. "Just get stuck in" is still right there, so the player is
            // never stuck - they just cannot write a quote for work nobody has looked at.
            bool anythingFound = _car.Diagnosis.RevealedCount > 0;

            _quoteButton.interactable = anythingFound;
            _quoteLabel.text = !anythingFound ? "NOTHING FOUND YET"
                : found ? "WRITE THE QUOTE" : "QUOTE WHAT I FOUND";
            _quoteLabel.color = anythingFound ? Theme.TextOnAccent : Theme.TextMuted;
            _quoteButton.GetComponent<Image>().color = anythingFound ? Theme.Info : Theme.PanelSunken;
        }

        /// <summary>Red is a real fault, amber is worn, green is fine - the timer bar's language.</summary>
        private static Color ConditionColor(float health)
        {
            if (health <= Quote.EssentialThreshold) return Theme.Danger;
            if (health <= CarCondition.FaultThreshold) return Theme.Warning;
            return Theme.Success;
        }

        private void HandleCheck(DiagnosisAction action)
        {
            if (_car == null) return;
            _simulation.StartDiagnosis(_car.BayIndex, action);
        }

        private void HandleSkip()
        {
            if (_car == null) return;

            // Commits to the lot without looking: no readings, no quote, no bonus.
            _car.Diagnosis.Skip();
            _car.AcceptAllWork();

            Action<ActiveCar> handler = CommitRequested;
            if (handler != null) handler(_car);
        }

        private void HandleQuote()
        {
            RaiseQuote();
        }

        private void RaiseQuote()
        {
            Action<ActiveCar> handler = QuoteRequested;
            if (handler != null && _car != null) handler(_car);
        }
    }
}
