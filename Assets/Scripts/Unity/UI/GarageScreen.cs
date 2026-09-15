using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Events;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Unity.Minigames;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// The main screen. From top to bottom: the money HUD, an event banner, the repair bays, the
    /// queue of cars waiting outside, and the workbench where the mini-games are played.
    ///
    /// This class owns layout and per-frame refresh only. It never changes game state directly -
    /// every button calls into GarageSimulation, which is the single source of truth.
    /// </summary>
    public sealed class GarageScreen
    {
        private GarageSimulation _simulation;

        private RectTransform _root;
        private Text _cashLabel;
        private Text _rateLabel;
        private Text _tokenLabel;
        private ProgressBar _prestigeBar;

        private Image _eventBanner;
        private Text _eventText;

        private readonly List<BayCardView> _bayCards = new List<BayCardView>();
        private RectTransform _bayColumn;

        private RectTransform _queueRow;
        private readonly List<Image> _queueChips = new List<Image>();
        private readonly List<Text> _queueLabels = new List<Text>();

        private MinigamePanel _minigamePanel;
        private Text _nextCarLabel;

        private Button _upgradeButton;
        private Button _statsButton;
        private Button _prestigeButton;

        /// <summary>Raised when the player taps the upgrades button.</summary>
        public event Action UpgradesRequested;

        /// <summary>Raised when the player taps the stats button.</summary>
        public event Action StatsRequested;

        /// <summary>Raised when the player taps the (rare) sell-the-garage button.</summary>
        public event Action PrestigeRequested;

        public void Build(RectTransform parent, GarageSimulation simulation)
        {
            _simulation = simulation;

            _root = UIFactory.CreateRect("GarageScreen", parent);
            UIFactory.Stretch(_root);

            BuildBackground();
            BuildHud();
            BuildEventBanner();
            BuildBays();
            BuildQueue();
            BuildWorkbench();
            BuildBottomBar();
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private void BuildBackground()
        {
            Image background = UIFactory.CreateImage("Background", _root, Color.white, UISprites.GarageBackground());
            UIFactory.Stretch(background.rectTransform);
            background.raycastTarget = false;
        }

        private void BuildHud()
        {
            Image hud = UIFactory.CreatePanel("Hud", _root, Theme.WithAlpha(Theme.PanelSunken, 0.92f));
            UIFactory.AnchorTop(hud.rectTransform, 150f, 24f, Theme.ScreenPadding);

            _cashLabel = UIFactory.CreateText("Cash", hud.transform, "$0", Theme.FontHuge,
                Theme.Cash, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AnchorTop(_cashLabel.rectTransform, 68f, 12f, Theme.PanelPadding);

            _rateLabel = UIFactory.CreateText("Rate", hud.transform, string.Empty, Theme.FontTiny,
                Theme.TextSecondary, TextAnchor.MiddleLeft);
            UIFactory.AnchorBottom(_rateLabel.rectTransform, 32f, 14f, Theme.PanelPadding);

            _tokenLabel = UIFactory.CreateText("Tokens", hud.transform, string.Empty, Theme.FontSmall,
                Theme.Prestige, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.AnchorTop(_tokenLabel.rectTransform, 40f, 18f, Theme.PanelPadding);

            _prestigeBar = UIFactory.CreateProgressBar("PrestigeBar", hud.transform, Theme.Prestige, 5);
            _prestigeBar.SmoothSpeed = 6f;
            RectTransform barRect = _prestigeBar.Rect;
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.offsetMin = new Vector2(0f, 20f);
            barRect.offsetMax = new Vector2(-Theme.PanelPadding, 30f);
        }

        private void BuildEventBanner()
        {
            _eventBanner = UIFactory.CreatePanel("EventBanner", _root, Theme.Success, 12);
            UIFactory.AnchorTop(_eventBanner.rectTransform, 56f, 184f, Theme.ScreenPadding);

            _eventText = UIFactory.CreateText("EventText", _eventBanner.transform, string.Empty, Theme.FontSmall,
                Theme.TextOnAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_eventText.rectTransform, 8f);

            _eventBanner.gameObject.SetActive(false);
        }

        /// <summary>
        /// Vertical budget on the 1080x1920 design canvas, measured from the top and bottom edges:
        ///   HUD      24..174 from the top
        ///   Banner  184..240 from the top
        ///   Bays    250 from the top down to 856 from the bottom (four 190px cards plus spacing)
        ///   Queue   740..838 from the bottom
        ///   Bench   160..720 from the bottom
        ///   Nav      28..136 from the bottom
        /// The canvas scaler matches on height, so these hold on any phone aspect ratio.
        /// </summary>
        private void BuildBays()
        {
            _bayColumn = UIFactory.CreateRect("Bays", _root);
            // Sits below the HUD and banner, above the queue strip.
            UIFactory.AnchorMiddle(_bayColumn, 250f, 856f, Theme.ScreenPadding);
            UIFactory.AddVerticalLayout(_bayColumn.gameObject, Theme.ElementSpacing);

            // Cards for every bay the garage could ever have are built up front and shown as unlocked.
            for (int i = 0; i < GameBalance.MaxBayCount; i++)
            {
                int bayIndex = i;
                BayCardView card = new BayCardView(bayIndex, HandleBaySelected);
                card.Build(_bayColumn);
                UIFactory.SetPreferredHeight(card.Root.gameObject, BayCardView.CardHeight);
                _bayCards.Add(card);
            }
        }

        private void BuildQueue()
        {
            Image queuePanel = UIFactory.CreatePanel("QueuePanel", _root, Theme.WithAlpha(Theme.PanelSunken, 0.85f), 14);
            RectTransform panelRect = queuePanel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.offsetMin = new Vector2(Theme.ScreenPadding, 740f);
            panelRect.offsetMax = new Vector2(-Theme.ScreenPadding, 740f + 98f);

            Text heading = UIFactory.CreateText("Heading", queuePanel.transform, "WAITING OUTSIDE", Theme.FontTiny,
                Theme.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AnchorTop(heading.rectTransform, 24f, 6f, 14f);

            _nextCarLabel = UIFactory.CreateText("NextCar", queuePanel.transform, string.Empty, Theme.FontTiny,
                Theme.TextMuted, TextAnchor.MiddleRight);
            UIFactory.AnchorTop(_nextCarLabel.rectTransform, 24f, 6f, 14f);

            _queueRow = UIFactory.CreateRect("QueueRow", queuePanel.transform);
            UIFactory.AnchorBottom(_queueRow, 56f, 8f, 14f);
            UIFactory.AddHorizontalLayout(_queueRow.gameObject, 8f);

            for (int i = 0; i < GameBalance.MaxQueuedCars; i++)
            {
                Image chip = UIFactory.CreatePanel("Queued" + i, _queueRow, Theme.PanelRaised, 10);
                chip.raycastTarget = false;

                Text label = UIFactory.CreateText("Label", chip.transform, string.Empty, Theme.FontTiny,
                    Theme.TextSecondary, TextAnchor.MiddleCenter);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                UIFactory.Stretch(label.rectTransform, 4f);

                _queueChips.Add(chip);
                _queueLabels.Add(label);
                chip.gameObject.SetActive(false);
            }
        }

        private void BuildWorkbench()
        {
            RectTransform host = UIFactory.CreateRect("Workbench", _root);
            host.anchorMin = new Vector2(0f, 0f);
            host.anchorMax = new Vector2(1f, 0f);
            host.pivot = new Vector2(0.5f, 0f);
            host.offsetMin = new Vector2(Theme.ScreenPadding, 160f);
            host.offsetMax = new Vector2(-Theme.ScreenPadding, 160f + 560f);

            _minigamePanel = new MinigamePanel();
            _minigamePanel.Build(host, _simulation);
        }

        private void BuildBottomBar()
        {
            RectTransform bar = UIFactory.CreateRect("BottomBar", _root);
            UIFactory.AnchorBottom(bar, Theme.TouchTargetHeight, 28f, Theme.ScreenPadding);
            UIFactory.AddHorizontalLayout(bar.gameObject, Theme.ElementSpacing);

            _upgradeButton = UIFactory.CreateButton("Upgrades", bar, "UPGRADES", Theme.Info,
                Theme.TextOnAccent, Theme.FontBody, () => Raise(UpgradesRequested));

            _statsButton = UIFactory.CreateButton("Stats", bar, "STATS", Theme.PanelRaised,
                Theme.TextPrimary, Theme.FontBody, () => Raise(StatsRequested));

            _prestigeButton = UIFactory.CreateButton("Prestige", bar, "SELL GARAGE", Theme.Prestige,
                Theme.TextOnAccent, Theme.FontBody, () => Raise(PrestigeRequested));
        }

        private static void Raise(Action handler)
        {
            if (handler != null) handler();
        }

        private void HandleBaySelected(int bayIndex)
        {
            _simulation.SelectBay(bayIndex);
        }

        // ------------------------------------------------------------------
        // Per-frame refresh
        // ------------------------------------------------------------------

        public void Refresh()
        {
            RefreshHud();
            RefreshEventBanner();
            RefreshBays();
            RefreshQueue();
            _minigamePanel.Refresh();
        }

        private void RefreshHud()
        {
            _cashLabel.text = "$" + CashFormat.Full(_simulation.Wallet.Cash);

            int mechanics = _simulation.Effects.MechanicCount;
            _rateLabel.text = mechanics > 0
                ? mechanics + (mechanics == 1 ? " mechanic on shift" : " mechanics on shift")
                : "No mechanics hired";

            int tokens = _simulation.Prestige.Tokens;
            _tokenLabel.text = tokens > 0
                ? tokens + (tokens == 1 ? " token  (+" : " tokens  (+")
                  + Mathf.RoundToInt((float)(_simulation.Prestige.PayoutMultiplier - 1d) * 100f) + "%)"
                : string.Empty;

            _prestigeBar.Fraction = _simulation.Prestige.ProgressTowardsPrestige(_simulation.Wallet.Cash);

            bool canPrestige = _simulation.CanPrestige();
            _prestigeButton.interactable = canPrestige;
            _prestigeButton.GetComponent<Image>().color = canPrestige ? Theme.Prestige : Theme.PanelSunken;
            _prestigeButton.GetComponentInChildren<Text>().color = canPrestige ? Theme.TextOnAccent : Theme.TextMuted;
        }

        private void RefreshEventBanner()
        {
            GameEventDefinition active = _simulation.Events.Active;

            if (active == null)
            {
                if (_eventBanner.gameObject.activeSelf) _eventBanner.gameObject.SetActive(false);
                return;
            }

            if (!_eventBanner.gameObject.activeSelf) _eventBanner.gameObject.SetActive(true);

            _eventBanner.color = Theme.Hex(active.ColorHex);
            _eventText.text = active.DisplayName.ToUpperInvariant() + " - " + active.Description
                              + "  (" + Mathf.CeilToInt(_simulation.Events.ActiveRemaining) + "s)";
        }

        private void RefreshBays()
        {
            int bayCount = _simulation.BayCount;

            for (int i = 0; i < _bayCards.Count; i++)
            {
                bool unlocked = i < bayCount;
                _bayCards[i].Root.gameObject.SetActive(unlocked);
                if (!unlocked) continue;

                ActiveCar car = _simulation.Bays[i];

                bool playerWorking = _simulation.PlayerSession != null
                                     && _simulation.PlayerSession.Car == car
                                     && car != null;

                bool mechanicWorking = false;
                if (car != null)
                {
                    for (int m = 0; m < _simulation.MechanicSessions.Count; m++)
                    {
                        if (_simulation.MechanicSessions[m].Car == car) { mechanicWorking = true; break; }
                    }
                }

                _bayCards[i].Refresh(car, playerWorking, mechanicWorking);
            }
        }

        private void RefreshQueue()
        {
            var waiting = _simulation.WaitingCars;

            for (int i = 0; i < _queueChips.Count; i++)
            {
                bool used = i < waiting.Count;
                _queueChips[i].gameObject.SetActive(used);
                if (!used) continue;

                ActiveCar car = waiting[i];
                _queueChips[i].color = Theme.Hex(car.Definition.Rarity.ColorHex());
                _queueLabels[i].text = car.Definition.DisplayName;
                _queueLabels[i].color = Theme.TextOnAccent;
            }

            _nextCarLabel.text = waiting.Count >= GameBalance.MaxQueuedCars
                ? "FORECOURT FULL"
                : "next in " + Mathf.CeilToInt(_simulation.TimeUntilNextCar) + "s";
        }

        /// <summary>The bay card for a given bay, so toasts can be popped over the right car.</summary>
        public RectTransform GetBayRect(int bayIndex)
        {
            if (bayIndex < 0 || bayIndex >= _bayCards.Count) return null;
            return _bayCards[bayIndex].Root;
        }

        /// <summary>
        /// Flashes the card for a car.
        ///
        /// Note the fallback: a car is removed from its bay BEFORE the completed / left-angry events
        /// are raised, so searching the live bays would find nothing for exactly the two moments most
        /// worth flashing. ActiveCar keeps its last bay index, which is what we fall back to.
        /// </summary>
        public void FlashCar(ActiveCar car, Color color)
        {
            if (car == null) return;

            for (int i = 0; i < _bayCards.Count && i < _simulation.Bays.Count; i++)
            {
                if (_simulation.Bays[i] == car)
                {
                    _bayCards[i].Flash(color);
                    return;
                }
            }

            int lastBay = car.BayIndex;
            if (lastBay >= 0 && lastBay < _bayCards.Count) _bayCards[lastBay].Flash(color);
        }
    }
}
