using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// One repair bay, drawn as a card: the car, its rarity ribbon, the payout on offer, a patience
    /// timer, and a chip per repair job showing which mini-game it uses and how far along it is.
    ///
    /// Tapping the card puts the player to work on that car.
    /// </summary>
    public sealed class BayCardView
    {
        private const int MaxJobChips = 5;

        /// <summary>
        /// The height this card's layout is designed for. GarageScreen pins each card to it, and the
        /// positions in Build() are all measured against this budget.
        /// </summary>
        public const float CardHeight = 190f;

        private readonly int _bayIndex;
        private readonly Action<int> _onSelected;

        private RectTransform _root;
        private Image _background;
        private Image _carImage;
        private Image _rarityRibbon;
        private Text _rarityText;
        private Text _carName;
        private Text _payoutText;
        private Text _emptyText;
        private ProgressBar _timerBar;
        private ProgressBar _progressBar;
        private Image _workerBadge;
        private Text _workerText;
        private RectTransform _jobRow;
        private Button _button;

        private readonly List<Image> _jobChips = new List<Image>();
        private readonly List<Text> _jobLabels = new List<Text>();
        private readonly List<ProgressBar> _jobBars = new List<ProgressBar>();

        /// <summary>The card's rect, so the screen can position it and toasts can find it.</summary>
        public RectTransform Root { get { return _root; } }

        /// <summary>Strength of the current completion flash, decaying to zero.</summary>
        private float _flash;

        /// <summary>Colour the current flash is tinted with.</summary>
        private Color _flashColor = Theme.Success;

        /// <summary>
        /// Lights the card up briefly. Called when a job finishes or a car is completed, so the
        /// player's eye is drawn to the right bay even if they were looking at the workbench.
        /// </summary>
        public void Flash(Color color)
        {
            _flash = 1f;
            _flashColor = color;
        }

        public BayCardView(int bayIndex, Action<int> onSelected)
        {
            _bayIndex = bayIndex;
            _onSelected = onSelected;
        }

        public void Build(RectTransform parent)
        {
            _background = UIFactory.CreatePanel("Bay" + _bayIndex, parent, Theme.PanelRaised);
            _root = _background.rectTransform;
            UIFactory.AddOutline(_background, Theme.PanelOutline);

            _button = _background.gameObject.AddComponent<Button>();
            _button.targetGraphic = _background;
            _button.transition = Selectable.Transition.None;
            _button.onClick.AddListener(() =>
            {
                if (_onSelected != null) _onSelected(_bayIndex);
            });

            // ---- layout note ----
            // The card is CardHeight tall (see GarageScreen, which pins it). Everything below is
            // positioned against that budget: header strip on top, car art and job chips through the
            // middle, and the two bars along the bottom. Keep the numbers in sync if you resize it.

            // ---- car art ----
            _carImage = UIFactory.CreateImage("Car", _root, Color.white, UISprites.CarSilhouette());
            _carImage.preserveAspect = true;
            _carImage.raycastTarget = false;
            RectTransform carRect = _carImage.rectTransform;
            carRect.anchorMin = new Vector2(0f, 0.5f);
            carRect.anchorMax = new Vector2(0f, 0.5f);
            carRect.pivot = new Vector2(0f, 0.5f);
            carRect.sizeDelta = new Vector2(150f, 76f);
            carRect.anchoredPosition = new Vector2(Theme.PanelPadding, -6f);

            // ---- rarity ribbon (header, left) ----
            _rarityRibbon = UIFactory.CreatePanel("Rarity", _root, Theme.TextMuted, 8);
            _rarityRibbon.raycastTarget = false;
            RectTransform ribbonRect = _rarityRibbon.rectTransform;
            ribbonRect.anchorMin = new Vector2(0f, 1f);
            ribbonRect.anchorMax = new Vector2(0f, 1f);
            ribbonRect.pivot = new Vector2(0f, 1f);
            ribbonRect.sizeDelta = new Vector2(140f, 30f);
            ribbonRect.anchoredPosition = new Vector2(Theme.PanelPadding, -10f);

            _rarityText = UIFactory.CreateText("RarityText", _rarityRibbon.transform, "Common", Theme.FontTiny,
                Theme.TextOnAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_rarityText.rectTransform);

            // ---- name (header, middle) and payout (header, right) ----
            _carName = UIFactory.CreateText("CarName", _root, string.Empty, Theme.FontBody,
                Theme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            RectTransform nameRect = _carName.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.offsetMin = new Vector2(Theme.PanelPadding + 152f, -44f);
            nameRect.offsetMax = new Vector2(-220f, -10f);

            _payoutText = UIFactory.CreateText("Payout", _root, string.Empty, Theme.FontBody,
                Theme.Cash, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.AnchorTop(_payoutText.rectTransform, 34f, 10f, Theme.PanelPadding);

            // ---- who is working on it (under the header, right) ----
            _workerBadge = UIFactory.CreatePanel("WorkerBadge", _root, Theme.Success, 8);
            _workerBadge.raycastTarget = false;
            RectTransform badgeRect = _workerBadge.rectTransform;
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.sizeDelta = new Vector2(150f, 28f);
            badgeRect.anchoredPosition = new Vector2(-Theme.PanelPadding, -48f);

            // ---- job chips, to the right of the car ----
            _jobRow = UIFactory.CreateRect("JobRow", _root);
            _jobRow.anchorMin = new Vector2(0f, 0f);
            _jobRow.anchorMax = new Vector2(1f, 0f);
            _jobRow.pivot = new Vector2(0.5f, 0f);
            _jobRow.offsetMin = new Vector2(Theme.PanelPadding + 165f, 56f);
            _jobRow.offsetMax = new Vector2(-Theme.PanelPadding, 114f);
            UIFactory.AddHorizontalLayout(_jobRow.gameObject, 8f);

            for (int i = 0; i < MaxJobChips; i++)
            {
                Image chip = UIFactory.CreatePanel("Job" + i, _jobRow, Theme.PanelSunken, 10);
                chip.raycastTarget = false;

                Text label = UIFactory.CreateText("Label", chip.transform, string.Empty, Theme.FontTiny,
                    Theme.TextSecondary, TextAnchor.UpperCenter);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.offsetMin = new Vector2(3f, 12f);
                labelRect.offsetMax = new Vector2(-3f, -3f);

                ProgressBar bar = UIFactory.CreateProgressBar("Bar", chip.transform, Theme.Success, 4);
                RectTransform barRect = bar.Rect;
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(1f, 0f);
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.offsetMin = new Vector2(5f, 5f);
                barRect.offsetMax = new Vector2(-5f, 13f);

                _jobChips.Add(chip);
                _jobLabels.Add(label);
                _jobBars.Add(bar);
            }

            // ---- bars along the bottom ----
            _progressBar = UIFactory.CreateProgressBar("Progress", _root, Theme.Info, 8);
            UIFactory.AnchorBottom(_progressBar.Rect, 12f, 36f, Theme.PanelPadding);
            // Repair progress slides rather than jumping, so a completed round reads as a movement.
            _progressBar.SmoothSpeed = 9f;

            _timerBar = UIFactory.CreateProgressBar("Timer", _root, Theme.Success, 8);
            UIFactory.AnchorBottom(_timerBar.Rect, 14f, 14f, Theme.PanelPadding);
            _timerBar.SmoothSpeed = 14f;

            // ---- empty-bay message ----
            _emptyText = UIFactory.CreateText("Empty", _root, "Empty bay - waiting for a customer",
                Theme.FontBody, Theme.TextMuted, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_emptyText.rectTransform, Theme.PanelPadding);
        }

        /// <summary>
        /// Redraws the card from the current state of the bay.
        /// </summary>
        /// <param name="car">The car in this bay, or null if it is empty.</param>
        /// <param name="isPlayerWorking">True if the player is currently working this bay.</param>
        /// <param name="isMechanicWorking">True if a hired mechanic is on this car.</param>
        public void Refresh(ActiveCar car, bool isPlayerWorking, bool isMechanicWorking)
        {
            bool hasCar = car != null;

            _carImage.gameObject.SetActive(hasCar);
            _rarityRibbon.gameObject.SetActive(hasCar);
            _carName.gameObject.SetActive(hasCar);
            _payoutText.gameObject.SetActive(hasCar);
            _jobRow.gameObject.SetActive(hasCar);
            _progressBar.Rect.gameObject.SetActive(hasCar);
            _timerBar.Rect.gameObject.SetActive(hasCar);
            _emptyText.gameObject.SetActive(!hasCar);
            _workerBadge.gameObject.SetActive(hasCar && (isPlayerWorking || isMechanicWorking));
            _button.interactable = hasCar && !car.AllJobsComplete;

            if (!hasCar)
            {
                _background.color = Theme.Panel;
                return;
            }

            // Selected bay gets a brighter card so it is obvious what you are working on,
            // and a recently completed job tints it towards the flash colour as that fades out.
            Color baseColor = isPlayerWorking ? Theme.PanelRaised * 1.15f : Theme.PanelRaised;

            if (_flash > 0f)
            {
                _flash -= Time.deltaTime * 2.2f;
                if (_flash < 0f) _flash = 0f;
                baseColor = Color.Lerp(baseColor, _flashColor, _flash * 0.55f);
            }

            _background.color = baseColor;

            _carImage.color = Theme.Hex(car.Definition.BodyColorHex);

            _rarityRibbon.color = Theme.Hex(car.Definition.Rarity.ColorHex());
            _rarityText.text = car.Definition.Rarity.DisplayName().ToUpperInvariant();

            _carName.text = car.Definition.DisplayName;
            _payoutText.text = "$" + CashFormat.Short(car.TotalPayout);

            if (isPlayerWorking)
            {
                _workerBadge.color = Theme.Info;
                _workerText.text = "YOU";
            }
            else if (isMechanicWorking)
            {
                _workerBadge.color = Theme.Success;
                _workerText.text = "MECHANIC";
            }

            _progressBar.Fraction = car.OverallProgress;

            _timerBar.Fraction = car.TimeFraction;
            _timerBar.FillColor = Theme.TimerColor(car.TimeFraction);

            RefreshJobChips(car);
        }

        private void RefreshJobChips(ActiveCar car)
        {
            for (int i = 0; i < _jobChips.Count; i++)
            {
                bool used = i < car.Jobs.Count;
                _jobChips[i].gameObject.SetActive(used);
                if (!used) continue;

                RepairJob job = car.Jobs[i];

                // The chip names the job and the mini-game it uses, so the player can see at a glance
                // what they are in for before committing to the car.
                _jobLabels[i].text = job.Type.DisplayName() + "\n" + job.Minigame.DisplayName();

                bool isActive = car.ActiveJobIndex == i;

                if (job.IsComplete)
                {
                    _jobChips[i].color = Theme.WithAlpha(Theme.Success, 0.35f);
                    _jobLabels[i].color = Theme.TextSecondary;
                }
                else if (isActive)
                {
                    _jobChips[i].color = Theme.WithAlpha(Theme.Info, 0.45f);
                    _jobLabels[i].color = Theme.TextPrimary;
                }
                else
                {
                    _jobChips[i].color = Theme.PanelSunken;
                    _jobLabels[i].color = Theme.TextSecondary;
                }

                _jobBars[i].Fraction = job.Progress;
                _jobBars[i].FillColor = job.IsComplete ? Theme.Success : Theme.Info;
            }
        }
    }
}
