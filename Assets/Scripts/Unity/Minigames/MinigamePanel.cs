using System.Collections.Generic;
using System;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Vehicle;
using GarageTycoon.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.Minigames
{
    /// <summary>
    /// The workbench at the bottom of the screen. It owns one view per mini-game type and swaps
    /// between them as rounds start, plus the header strip showing which car and job is being worked.
    /// When nothing is selected it shows a prompt telling the player to tap a car.
    /// </summary>
    public sealed class MinigamePanel
    {
        /// <summary>Where the car band starts, just under the header strip.</summary>
        private const float RepairBandTop = 92f;

        /// <summary>How tall the car band is. Everything below is left to the mini-game.</summary>
        private const float RepairBandHeight = 150f;

        private GarageSimulation _simulation;

        private RectTransform _root;
        private RectTransform _viewHost;
        private Text _jobLabel;
        private Text _twistLabel;
        private Text _carLabel;
        private ProgressBar _jobProgress;
        private ProgressBar _roundTimer;

        private RectTransform _idlePanel;
        private Text _idleText;

        /// <summary>The car on the ramp, showing the repair actually happening.</summary>
        private readonly RepairView _repairView = new RepairView();

        /// <summary>The inspection ramp and the quote. Both live in the bench, not in an overlay,
        /// so the whole job stays in one place on a phone - and so a diagnosis round plays in
        /// exactly the spot a repair round does.</summary>
        private readonly InspectPanel _inspectPanel = new InspectPanel();
        private readonly QuotePanel _quotePanel = new QuotePanel();

        /// <summary>The car being looked over, and the bill being written. UI state, not saved -
        /// a reload drops you back at the bay, which is where the web build lands too.</summary>
        private ActiveCar _inspecting;
        private ActiveCar _quotingCar;
        private Quote _quote;

        /// <summary>Raised when a quote is answered, so the screen can start the work.</summary>
        public event Action<ActiveCar, QuoteOption> QuoteAccepted;

        private readonly List<MinigameView> _views = new List<MinigameView>();
        private MinigameView _activeView;
        private MinigameBase _boundMinigame;

        /// <summary>Builds the panel and every mini-game view inside it.</summary>
        public void Build(RectTransform parent, GarageSimulation simulation)
        {
            _simulation = simulation;

            Image panel = UIFactory.CreatePanel("MinigamePanel", parent, Theme.Panel);
            _root = panel.rectTransform;
            UIFactory.Stretch(_root);
            UIFactory.AddOutline(panel, Theme.PanelOutline);

            // ---- header: what am I working on? ----
            _jobLabel = UIFactory.CreateText("JobLabel", _root, "No job selected", Theme.FontBody,
                Theme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AnchorTop(_jobLabel.rectTransform, 40f, 14f, Theme.PanelPadding);

            _carLabel = UIFactory.CreateText("CarLabel", _root, string.Empty, Theme.FontTiny,
                Theme.TextSecondary, TextAnchor.MiddleRight);
            UIFactory.AnchorTop(_carLabel.rectTransform, 40f, 14f, Theme.PanelPadding);

            _jobProgress = UIFactory.CreateProgressBar("JobProgress", _root, Theme.Success, 8);
            // Smoothed so finishing a round reads as the bar sweeping forward.
            _jobProgress.SmoothSpeed = 10f;
            UIFactory.AnchorTop(_jobProgress.Rect, 12f, 58f, Theme.PanelPadding);

            _roundTimer = UIFactory.CreateProgressBar("RoundTimer", _root, Theme.Warning, 6);
            UIFactory.AnchorTop(_roundTimer.Rect, 8f, 74f, Theme.PanelPadding);

            // A twisted round has to announce itself, or the player just thinks it is broken.
            _twistLabel = UIFactory.CreateText("Twist", _root, string.Empty, Theme.FontTiny,
                Theme.Prestige, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.AnchorTop(_twistLabel.rectTransform, 24f, 86f, Theme.PanelPadding);

            // ---- the car, sitting above the controls ----
            // The band is deliberately shallow: the car is the reward for winning a round, not the
            // thing you interact with, so the mini-game keeps the bulk of the panel.
            RectTransform repairBand = UIFactory.CreateRect("RepairBand", _root);
            UIFactory.AnchorTop(repairBand, RepairBandHeight, RepairBandTop, Theme.PanelPadding);
            _repairView.Build(repairBand);

            // ---- the area the mini-game views live in ----
            _viewHost = UIFactory.CreateRect("ViewHost", _root);
            UIFactory.AnchorMiddle(_viewHost, RepairBandTop + RepairBandHeight + 6f, 0f, 0f);

            _inspectPanel.Build(_viewHost, _simulation);
            _inspectPanel.QuoteRequested += OpenQuote;
            _inspectPanel.CommitRequested += CommitToRepair;

            _quotePanel.Build(_viewHost, _simulation);
            _quotePanel.Answered += HandleQuoteAnswered;

            _views.Add(new TimingBarView());
            _views.Add(new ToolMatchView());
            _views.Add(new HoldReleaseView());
            _views.Add(new RapidSequenceView());

            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].Build(_viewHost, _simulation);
            }

            // ---- the "nothing selected" state ----
            Image idle = UIFactory.CreateImage("IdlePanel", _viewHost, new Color(0f, 0f, 0f, 0f));
            _idlePanel = idle.rectTransform;
            UIFactory.Stretch(_idlePanel);

            _idleText = UIFactory.CreateText("IdleText", _idlePanel,
                "Tap a car above to start work", Theme.FontHeading, Theme.TextMuted, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_idleText.rectTransform, Theme.PanelPadding);
        }

        /// <summary>
        /// Puts a car on the inspection ramp. Called when the player taps a bay holding a car
        /// nobody has looked at yet.
        /// </summary>
        public void Inspect(ActiveCar car)
        {
            _inspecting = car;
            _quotingCar = null;
            _quote = null;
            _simulation.ClearPlayerSession();
            Refresh();
        }

        /// <summary>True while the bench is showing the ramp or the quote rather than a repair.</summary>
        public bool IsDeciding { get { return _inspecting != null || _quotingCar != null; } }

        /// <summary>
        /// "Just get stuck in": skip the ramp and the quote, and start turning spanners.
        ///
        /// The quote screen is never shown, so the player is not asked to choose between
        /// Everything and Essentials after deciding not to look - that choice needs readings,
        /// and the whole point of skipping is that there are none.
        /// </summary>
        private void CommitToRepair(ActiveCar car)
        {
            _inspecting = null;
            _quotingCar = null;
            _quote = null;
            _simulation.CancelDiagnosis();

            Action<ActiveCar> handler = RepairCommitted;
            if (handler != null) handler(car);

            Refresh();
        }

        private void OpenQuote(ActiveCar car)
        {
            // Belt and braces behind InspectPanel's gate: never open a bill with nothing on it.
            // An empty quote has no decision in it, and the one thing it must NEVER do is stand in
            // for "this car is finished" - that would commit the player to work they cannot see.
            // The player stays on the ramp and keeps control of the car.
            if (Quote.ReadinessFor(car) != QuoteReadiness.ReadyToQuote)
            {
                _inspecting = car;
                Refresh();
                return;
            }

            _inspecting = null;
            _simulation.CancelDiagnosis();

            _quotingCar = car;
            _quote = Quote.For(car);
            Refresh();
        }

        /// <summary>Raised when the player commits to a car without inspecting it.</summary>
        public event Action<ActiveCar> RepairCommitted;

        private void HandleQuoteAnswered(ActiveCar car, Quote quote, QuoteOption? option)
        {
            // No option means "back to the ramp for another look".
            if (!option.HasValue)
            {
                _quotingCar = null;
                _quote = null;
                _inspecting = car;
                Refresh();
                return;
            }

            quote.Apply(car, option.Value);

            _quotingCar = null;
            _quote = null;
            _inspecting = null;

            Action<ActiveCar, QuoteOption> handler = QuoteAccepted;
            if (handler != null) handler(car, option.Value);

            Refresh();
        }

        /// <summary>Drops whatever decision was in progress, e.g. when its car leaves.</summary>
        private void ClearDecision()
        {
            _inspecting = null;
            _quotingCar = null;
            _quote = null;
        }

        /// <summary>Called once a frame to keep the panel in step with the simulation.</summary>
        public void Refresh()
        {
            // ---- an inspection round owns the bench while it plays ----
            DiagnosisSession diagnosis = _simulation.DiagnosisSession;
            if (diagnosis != null && diagnosis.Car != null)
            {
                _jobLabel.text = diagnosis.Action.DisplayName();
                _carLabel.text = diagnosis.Car.Definition.DisplayName;
                _jobProgress.Fraction = 0f;
                _twistLabel.gameObject.SetActive(false);

                _inspectPanel.SetVisible(false);
                _quotePanel.SetVisible(false);
                _idlePanel.gameObject.SetActive(false);
                _repairView.Clear();

                if (!ReferenceEquals(diagnosis.Minigame, _boundMinigame)) BindView(diagnosis.Minigame);

                _roundTimer.Fraction = diagnosis.Minigame.TimeLimit <= 0f
                    ? 0f : diagnosis.Minigame.TimeRemaining / diagnosis.Minigame.TimeLimit;
                _roundTimer.FillColor = Theme.TimerColor(_roundTimer.Fraction);

                if (_activeView != null) _activeView.Refresh();
                return;
            }

            // ---- the ramp ----
            if (_inspecting != null && _inspecting.State != CarState.InBay) ClearDecision();

            if (_inspecting != null)
            {
                ShowDecision(_inspecting, "On the ramp");
                _inspectPanel.Bind(_inspecting);
                _inspectPanel.Refresh();
                _quotePanel.SetVisible(false);

                // How much of the car has been worked out, as the top bar.
                _jobProgress.Fraction =
                    _inspecting.Diagnosis.RevealedCount / (float)VehicleSystemExtensions.Count;
                return;
            }

            // ---- the quote ----
            if (_quotingCar != null && _quotingCar.State != CarState.InBay) ClearDecision();

            if (_quotingCar != null)
            {
                ShowDecision(_quotingCar, "The quote");
                _quotePanel.Bind(_quotingCar, _quote);
                _quotePanel.Refresh();
                _inspectPanel.SetVisible(false);
                _jobProgress.Fraction = 1f;
                return;
            }

            _inspectPanel.SetVisible(false);
            _quotePanel.SetVisible(false);

            WorkSession session = _simulation.PlayerSession;

            if (session == null || session.Car == null)
            {
                ShowIdle("Tap a car above to start work");
                return;
            }

            RepairJob job = session.Job;
            ActiveCar car = session.Car;

            if (job == null)
            {
                ShowIdle("All jobs done on this car");
                return;
            }

            // ---- the car on the ramp ----
            _repairView.Refresh(car, car.ActiveJobIndex);

            // ---- header ----
            _jobLabel.text = job.Type.DisplayName();
            _carLabel.text = car.Definition.DisplayName;
            _jobProgress.Fraction = (float)(job.Progress);
            _jobProgress.Rect.gameObject.SetActive(true);

            MinigameBase minigame = session.Minigame;

            if (minigame == null)
            {
                // Between rounds: hold the last view on screen so the panel does not flicker.
                _roundTimer.Fraction = 0f;
                if (_activeView != null) _activeView.Refresh();
                _idlePanel.gameObject.SetActive(false);
                return;
            }

            // ---- swap views if the round changed ----
            if (!ReferenceEquals(minigame, _boundMinigame))
            {
                BindView(minigame);
            }

            bool twisted = minigame.Modifier != MinigameModifier.None;
            _twistLabel.gameObject.SetActive(twisted);
            if (twisted)
            {
                _twistLabel.text = minigame.Modifier.DisplayName() + "  \u00B7  " + minigame.Modifier.Hint();
            }

            _roundTimer.Fraction = minigame.TimeLimit <= 0f ? 0f : minigame.TimeRemaining / minigame.TimeLimit;
            _roundTimer.FillColor = Theme.TimerColor(_roundTimer.Fraction);

            if (_activeView != null) _activeView.Refresh();
        }

        /// <summary>Shared chrome for the two decision screens.</summary>
        private void ShowDecision(ActiveCar car, string title)
        {
            if (_activeView != null) { _activeView.Unbind(); _activeView = null; }
            _boundMinigame = null;

            _jobLabel.text = title;
            _carLabel.text = car.Definition.DisplayName;
            _roundTimer.Fraction = 0f;
            _twistLabel.gameObject.SetActive(false);
            _idlePanel.gameObject.SetActive(false);

            // The car silhouette is already on the bay card directly above, and both decision
            // screens need the height more than they need a second copy of it.
            _repairView.Clear();
        }

        private void BindView(MinigameBase minigame)
        {
            if (_activeView != null) _activeView.Unbind();

            _activeView = FindView(minigame.Type);
            _boundMinigame = minigame;

            _idlePanel.gameObject.SetActive(false);

            if (_activeView != null) _activeView.Bind(minigame);
        }

        /// <summary>
        /// Called when a round finishes so the car reacts: a bolt goes tight, a wheel turns, or
        /// the whole thing gets a knock if the round was missed.
        /// </summary>
        public void NotifyRoundResolved(WorkSession session, MinigameResult result)
        {
            if (session == null || session.Car == null || session.Job == null) return;

            _repairView.Pulse(session.Car, session.Job.Type, result.Outcome);
        }

        /// <summary>Called when a car is finished, for the final flourish.</summary>
        public void NotifyCarCompleted(ActiveCar car)
        {
            _repairView.Finish();
        }

        private MinigameView FindView(MinigameType type)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                if (_views[i].Type == type) return _views[i];
            }
            return null;
        }

        private void ShowIdle(string message)
        {
            if (_activeView != null)
            {
                _activeView.Unbind();
                _activeView = null;
            }

            _boundMinigame = null;

            _repairView.Clear();

            _idlePanel.gameObject.SetActive(true);
            _idleText.text = message;

            _jobLabel.text = "No job selected";
            _carLabel.text = string.Empty;
            _jobProgress.Fraction = 0f;
            _roundTimer.Fraction = 0f;
            if (_twistLabel != null) _twistLabel.gameObject.SetActive(false);
        }
    }
}
