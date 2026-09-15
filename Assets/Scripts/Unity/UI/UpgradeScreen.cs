using System;
using System.Collections.Generic;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// The upgrade shop: a tab per branch and a scrollable list of upgrade rows, each showing the
    /// current level, what the next level does, and what it costs.
    ///
    /// Buy buttons stay visible but greyed out when unaffordable rather than disappearing, so the
    /// player can always see what they are saving towards.
    /// </summary>
    public sealed class UpgradeScreen
    {
        private GarageSimulation _simulation;
        private Action<string> _onPurchased;

        private RectTransform _root;
        private RectTransform _rowHost;
        private Text _cashLabel;
        private Text _branchDescription;

        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<UpgradeRow> _rows = new List<UpgradeRow>();

        private UpgradeBranch _activeBranch = UpgradeBranch.Precision;

        /// <summary>True while the shop is on screen.</summary>
        public bool IsVisible { get { return _root != null && _root.gameObject.activeSelf; } }

        /// <summary>One row in the shop list.</summary>
        private sealed class UpgradeRow
        {
            public UpgradeDefinition Definition;
            public RectTransform Root;
            public Text Name;
            public Text Description;
            public Text Level;
            public Button Buy;
            public Text BuyLabel;
            public Image BuyBackground;
            public ProgressBar LevelBar;
        }

        public void Build(RectTransform parent, GarageSimulation simulation, Action<string> onPurchased)
        {
            _simulation = simulation;
            _onPurchased = onPurchased;

            Image backdrop = UIFactory.CreateImage("UpgradeScreen", parent, new Color(0.04f, 0.06f, 0.09f, 0.97f));
            _root = backdrop.rectTransform;
            UIFactory.Stretch(_root);
            backdrop.raycastTarget = true; // swallow taps so the game behind cannot be poked

            Text title = UIFactory.CreateText("Title", _root, "UPGRADES", Theme.FontTitle,
                Theme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AnchorTop(title.rectTransform, 60f, 40f, Theme.ScreenPadding);

            _cashLabel = UIFactory.CreateText("Cash", _root, string.Empty, Theme.FontHeading,
                Theme.Cash, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.AnchorTop(_cashLabel.rectTransform, 60f, 40f, Theme.ScreenPadding);

            // ---- branch tabs ----
            RectTransform tabRow = UIFactory.CreateRect("Tabs", _root);
            UIFactory.AnchorTop(tabRow, 92f, 112f, Theme.ScreenPadding);
            UIFactory.AddHorizontalLayout(tabRow.gameObject, 10f);

            for (int i = 0; i < 4; i++)
            {
                UpgradeBranch branch = (UpgradeBranch)i;

                Button tab = UIFactory.CreateButton("Tab" + i, tabRow, branch.DisplayName(),
                    Theme.PanelRaised, Theme.TextPrimary, Theme.FontSmall, () => ShowBranch(branch));

                _tabButtons.Add(tab);
            }

            _branchDescription = UIFactory.CreateText("BranchDescription", _root, string.Empty, Theme.FontSmall,
                Theme.TextSecondary, TextAnchor.MiddleCenter);
            UIFactory.AnchorTop(_branchDescription.rectTransform, 36f, 212f, Theme.ScreenPadding);

            // ---- scrollable list ----
            RectTransform viewport = UIFactory.CreateRect("Viewport", _root);
            UIFactory.AnchorMiddle(viewport, 256f, 150f, Theme.ScreenPadding);

            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f); // needs a graphic to act as a mask
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _rowHost = UIFactory.CreateRect("Content", viewport);
            _rowHost.anchorMin = new Vector2(0f, 1f);
            _rowHost.anchorMax = new Vector2(1f, 1f);
            _rowHost.pivot = new Vector2(0.5f, 1f);
            UIFactory.AddVerticalLayout(_rowHost.gameObject, Theme.ElementSpacing);
            ContentSizeFitter fitter = _rowHost.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _rowHost;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            // ---- one row per upgrade, built once and shown/hidden per tab ----
            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                _rows.Add(BuildRow(UpgradeCatalog.All[i]));
            }

            // ---- close button ----
            Button close = UIFactory.CreateButton("Close", _root, "BACK TO THE GARAGE", Theme.Info,
                Theme.TextOnAccent, Theme.FontBody, Hide);
            UIFactory.AnchorBottom(close.GetComponent<RectTransform>(), Theme.TouchTargetHeight, 40f, Theme.ScreenPadding);

            ShowBranch(UpgradeBranch.Precision);
            _root.gameObject.SetActive(false);
        }

        private UpgradeRow BuildRow(UpgradeDefinition definition)
        {
            Image panel = UIFactory.CreatePanel("Row_" + definition.Id, _rowHost, Theme.Panel);
            UIFactory.SetPreferredHeight(panel.gameObject, 190f);

            UpgradeRow row = new UpgradeRow();
            row.Definition = definition;
            row.Root = panel.rectTransform;

            row.Name = UIFactory.CreateText("Name", panel.transform, definition.DisplayName, Theme.FontBody,
                Theme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.AnchorTop(row.Name.rectTransform, 38f, 16f, Theme.PanelPadding);

            row.Level = UIFactory.CreateText("Level", panel.transform, string.Empty, Theme.FontSmall,
                Theme.Hex(definition.Branch.ColorHex()), TextAnchor.UpperRight, FontStyle.Bold);
            UIFactory.AnchorTop(row.Level.rectTransform, 38f, 16f, Theme.PanelPadding);

            row.Description = UIFactory.CreateText("Description", panel.transform, definition.Description,
                Theme.FontTiny, Theme.TextSecondary, TextAnchor.UpperLeft);
            UIFactory.AnchorTop(row.Description.rectTransform, 32f, 56f, Theme.PanelPadding);

            row.LevelBar = UIFactory.CreateProgressBar("LevelBar", panel.transform,
                Theme.Hex(definition.Branch.ColorHex()), 5);
            UIFactory.AnchorTop(row.LevelBar.Rect, 10f, 96f, Theme.PanelPadding);

            row.Buy = UIFactory.CreateButton("Buy", panel.transform, string.Empty, Theme.Success,
                Theme.TextOnAccent, Theme.FontSmall, () => Purchase(row));
            UIFactory.AnchorBottom(row.Buy.GetComponent<RectTransform>(), 64f, Theme.PanelPadding, Theme.PanelPadding);

            row.BuyLabel = row.Buy.GetComponentInChildren<Text>();
            row.BuyBackground = row.Buy.GetComponent<Image>();

            return row;
        }

        private void Purchase(UpgradeRow row)
        {
            if (_simulation.TryBuyUpgrade(row.Definition.Id))
            {
                Refresh();
                if (_onPurchased != null) _onPurchased(row.Definition.Id);
            }
        }

        /// <summary>Switches tab and redraws the list.</summary>
        public void ShowBranch(UpgradeBranch branch)
        {
            _activeBranch = branch;
            _branchDescription.text = branch.Description();

            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool isActive = i == (int)branch;
                Image background = _tabButtons[i].GetComponent<Image>();
                background.color = isActive ? Theme.Hex(((UpgradeBranch)i).ColorHex()) : Theme.PanelRaised;

                Text label = _tabButtons[i].GetComponentInChildren<Text>();
                label.color = isActive ? Theme.TextOnAccent : Theme.TextSecondary;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Root.gameObject.SetActive(_rows[i].Definition.Branch == branch);
            }

            // Jump back to the top when changing tabs.
            _rowHost.anchoredPosition = Vector2.zero;

            Refresh();
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        /// <summary>Keeps prices, levels and affordability up to date. Cheap enough to call every frame.</summary>
        public void Refresh()
        {
            if (!IsVisible) return;

            _cashLabel.text = "$" + CashFormat.Full(_simulation.Wallet.Cash);

            for (int i = 0; i < _rows.Count; i++)
            {
                UpgradeRow row = _rows[i];
                if (!row.Root.gameObject.activeSelf) continue;

                UpgradeDefinition definition = row.Definition;
                int level = _simulation.Upgrades.GetLevel(definition.Id);
                bool maxed = level >= definition.MaxLevel;

                row.Level.text = "LVL " + level + " / " + definition.MaxLevel;
                row.LevelBar.Fraction = (float)level / definition.MaxLevel;

                if (maxed)
                {
                    row.BuyLabel.text = "MAXED OUT";
                    row.BuyBackground.color = Theme.PanelSunken;
                    row.BuyLabel.color = Theme.TextMuted;
                    row.Buy.interactable = false;
                    continue;
                }

                double cost = _simulation.GetUpgradeCost(definition);
                bool affordable = _simulation.Wallet.CanAfford(cost);
                bool discounted = cost < definition.CostForLevel(level) - 0.5d;

                row.BuyLabel.text = definition.DescribeNextLevel() + "   $" + CashFormat.Short(cost)
                                    + (discounted ? "  (SALE)" : string.Empty);

                row.Buy.interactable = affordable;
                row.BuyBackground.color = affordable
                    ? (discounted ? Theme.Warning : Theme.Success)
                    : Theme.PanelSunken;
                row.BuyLabel.color = affordable ? Theme.TextOnAccent : Theme.TextMuted;
            }
        }
    }
}
