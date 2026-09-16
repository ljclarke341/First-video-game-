using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;
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
        private GarageSimulation _simulation;

        private RectTransform _root;
        private RectTransform _viewHost;
        private Text _jobLabel;
        private Text _carLabel;
        private ProgressBar _jobProgress;
        private ProgressBar _roundTimer;

        private RectTransform _idlePanel;
        private Text _idleText;

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

            // ---- the area the mini-game views live in ----
            _viewHost = UIFactory.CreateRect("ViewHost", _root);
            UIFactory.AnchorMiddle(_viewHost, 92f, 0f, 0f);

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

        /// <summary>Called once a frame to keep the panel in step with the simulation.</summary>
        public void Refresh()
        {
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

            // ---- header ----
            _jobLabel.text = job.Type.DisplayName();
            _carLabel.text = car.Definition.DisplayName;
            _jobProgress.Fraction = job.Progress;
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

            _roundTimer.Fraction = minigame.TimeLimit <= 0f ? 0f : minigame.TimeRemaining / minigame.TimeLimit;
            _roundTimer.FillColor = Theme.TimerColor(_roundTimer.Fraction);

            if (_activeView != null) _activeView.Refresh();
        }

        private void BindView(MinigameBase minigame)
        {
            if (_activeView != null) _activeView.Unbind();

            _activeView = FindView(minigame.Type);
            _boundMinigame = minigame;

            _idlePanel.gameObject.SetActive(false);

            if (_activeView != null) _activeView.Bind(minigame);
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

            _idlePanel.gameObject.SetActive(true);
            _idleText.text = message;

            _jobLabel.text = "No job selected";
            _carLabel.text = string.Empty;
            _jobProgress.Fraction = 0f;
            _roundTimer.Fraction = 0f;
        }
    }
}
