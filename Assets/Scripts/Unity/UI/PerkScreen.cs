using System;
using System.Collections.Generic;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// The Reputation screen: where tokens earned by selling the garage are spent on permanent
    /// perks. Deliberately separate from the ordinary upgrade shop, because these are bought in a
    /// different currency, survive every reset, and are the only decision a sell-up actually asks
    /// the player to make.
    /// </summary>
    public sealed class PerkScreen
    {
        private GarageSimulation _simulation;
        private Action _onPurchased;

        private RectTransform _root;
        private RectTransform _rowHost;
        private Text _tokenCount;
        private Text _note;

        private readonly List<PerkRow> _rows = new List<PerkRow>();

        private sealed class PerkRow
        {
            public PrestigePerk Perk;
            public Text Level;
            public Button Buy;
            public Text BuyLabel;
            public Image BuyBackground;
            public ProgressBar Track;
        }

        public bool IsVisible { get { return _root != null && _root.gameObject.activeSelf; } }

        public void Build(RectTransform parent, GarageSimulation simulation, Action onPurchased)
        {
            _simulation = simulation;
            _onPurchased = onPurchased;

            Image backdrop = UIFactory.CreateImage("PerkScreen", parent, Theme.Hex("#0C1117"));
            _root = backdrop.rectTransform;
            UIFactory.Stretch(_root);
            backdrop.raycastTarget = true;

            Text title = UIFactory.CreateText("Title", _root, "REPUTATION", Theme.FontTitle,
                Theme.Prestige, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AnchorTop(title.rectTransform, 60f, 40f, Theme.ScreenPadding);

            // ---- token banner ----
            Image banner = UIFactory.CreatePanel("Banner", _root, Theme.WithAlpha(Theme.Prestige, 0.16f));
            UIFactory.AnchorTop(banner.rectTransform, 86f, 110f, Theme.ScreenPadding);
            UIFactory.AddOutline(banner, Theme.Prestige);

            _tokenCount = UIFactory.CreateText("Tokens", banner.transform, "0", Theme.FontTitle,
                Theme.Prestige, TextAnchor.MiddleLeft, FontStyle.Bold);
            RectTransform countRect = _tokenCount.rectTransform;
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(0f, 1f);
            countRect.pivot = new Vector2(0f, 0.5f);
            countRect.sizeDelta = new Vector2(110f, 0f);
            countRect.anchoredPosition = new Vector2(Theme.PanelPadding, 0f);

            _note = UIFactory.CreateText("Note", banner.transform, string.Empty, Theme.FontTiny,
                Theme.TextSecondary, TextAnchor.MiddleLeft);
            _note.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform noteRect = _note.rectTransform;
            noteRect.anchorMin = Vector2.zero;
            noteRect.anchorMax = Vector2.one;
            noteRect.offsetMin = new Vector2(Theme.PanelPadding + 115f, 8f);
            noteRect.offsetMax = new Vector2(-Theme.PanelPadding, -8f);

            // ---- scrollable list ----
            RectTransform viewport = UIFactory.CreateRect("Viewport", _root);
            UIFactory.AnchorMiddle(viewport, 210f, 150f, Theme.ScreenPadding);

            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _rowHost = UIFactory.CreateRect("Content", viewport);
            _rowHost.anchorMin = new Vector2(0f, 1f);
            _rowHost.anchorMax = new Vector2(1f, 1f);
            _rowHost.pivot = new Vector2(0.5f, 1f);
            _rowHost.offsetMin = Vector2.zero;
            _rowHost.offsetMax = Vector2.zero;

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

            for (int i = 0; i < PerkCatalog.All.Count; i++) _rows.Add(BuildRow(PerkCatalog.All[i]));

            Button close = UIFactory.CreateButton("Close", _root, "BACK TO THE GARAGE", Theme.Prestige,
                Theme.TextOnAccent, Theme.FontBody, Hide);
            UIFactory.AnchorBottom(close.GetComponent<RectTransform>(), Theme.TouchTargetHeight, 40f, Theme.ScreenPadding);

            _root.gameObject.SetActive(false);
        }

        private PerkRow BuildRow(PrestigePerk perk)
        {
            Image panel = UIFactory.CreatePanel("Perk_" + perk.Id, _rowHost, Theme.Panel);
            UIFactory.SetPreferredHeight(panel.gameObject, 190f);

            PerkRow row = new PerkRow();
            row.Perk = perk;

            Text name = UIFactory.CreateText("Name", panel.transform, perk.DisplayName, Theme.FontBody,
                Theme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.AnchorTop(name.rectTransform, 38f, 16f, Theme.PanelPadding);

            row.Level = UIFactory.CreateText("Level", panel.transform, string.Empty, Theme.FontSmall,
                Theme.Hex(perk.ColorHex), TextAnchor.UpperRight, FontStyle.Bold);
            UIFactory.AnchorTop(row.Level.rectTransform, 38f, 16f, Theme.PanelPadding);

            Text description = UIFactory.CreateText("Description", panel.transform, perk.Description,
                Theme.FontTiny, Theme.TextSecondary, TextAnchor.UpperLeft);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.AnchorTop(description.rectTransform, 34f, 56f, Theme.PanelPadding);

            row.Track = UIFactory.CreateProgressBar("Track", panel.transform, Theme.Hex(perk.ColorHex), 5);
            UIFactory.AnchorTop(row.Track.Rect, 10f, 98f, Theme.PanelPadding);

            row.Buy = UIFactory.CreateButton("Buy", panel.transform, string.Empty, Theme.Prestige,
                Theme.TextOnAccent, Theme.FontSmall, () => Purchase(row));
            UIFactory.AnchorBottom(row.Buy.GetComponent<RectTransform>(), 64f, Theme.PanelPadding, Theme.PanelPadding);

            row.BuyLabel = row.Buy.GetComponentInChildren<Text>();
            row.BuyBackground = row.Buy.GetComponent<Image>();

            return row;
        }

        private void Purchase(PerkRow row)
        {
            if (!_simulation.Prestige.TryBuyPerk(row.Perk)) return;

            // Perks change bays, patience and the streak cap, so the run must be told at once.
            _simulation.RefreshEffects();
            Refresh();

            if (_onPurchased != null) _onPurchased();
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _rowHost.anchoredPosition = Vector2.zero;
            Refresh();
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (!IsVisible) return;

            PrestigeState prestige = _simulation.Prestige;
            int available = prestige.TokensAvailable;

            _tokenCount.text = available.ToString();

            if (prestige.TokensEarned <= 0)
            {
                _note.text = "Sell the garage to earn your first Reputation Tokens. Perks bought here last forever.";
            }
            else if (available > 0)
            {
                _note.text = "Tokens to spend. Perks last through every future sell-up.";
            }
            else
            {
                _note.text = "All spent. Sell the garage again to earn more.";
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                PerkRow row = _rows[i];
                int level = prestige.GetPerkLevel(row.Perk.Id);
                bool maxed = level >= row.Perk.MaxLevel;

                row.Level.text = level + " / " + row.Perk.MaxLevel;
                row.Track.Fraction = (float)level / row.Perk.MaxLevel;

                if (maxed)
                {
                    row.BuyLabel.text = "MAXED OUT";
                    row.BuyBackground.color = Theme.PanelSunken;
                    row.BuyLabel.color = Theme.TextMuted;
                    row.Buy.interactable = false;
                    continue;
                }

                int cost = prestige.GetPerkCost(row.Perk);
                bool affordable = cost <= available;

                row.BuyLabel.text = "LEVEL " + (level + 1) + "   " + cost + (cost == 1 ? " TOKEN" : " TOKENS");
                row.Buy.interactable = affordable;
                row.BuyBackground.color = affordable ? Theme.Prestige : Theme.PanelSunken;
                row.BuyLabel.color = affordable ? Theme.TextOnAccent : Theme.TextMuted;
            }
        }
    }
}
