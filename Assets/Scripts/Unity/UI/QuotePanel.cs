using System;
using System.Collections.Generic;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// The bill the player puts in front of the customer.
    ///
    /// The Unity half of the same screen the web build shows: every outstanding repair with the
    /// reading that justifies it, the ones the car should not leave without marked, and three
    /// answers. The rules are all in Core.Quote; this draws them.
    /// </summary>
    public sealed class QuotePanel
    {
        private sealed class LineView
        {
            public RectTransform Root;
            public Image Background;
            public Text Name;
            public Text Why;
            public Text Needed;
            public Text Price;
        }

        /// <summary>Enough rows for the biggest car the catalogue can produce.</summary>
        private const int MaxLines = 6;
        private const float LineHeight = 58f;

        private RectTransform _root;
        private readonly List<LineView> _lines = new List<LineView>();
        private Text _wantHint;
        private Text _partsEverything;
        private Text _partsEssential;
        private Button _everythingButton;
        private Button _essentialButton;
        private Button _backButton;
        private Text _everythingPrice;
        private Text _essentialPrice;
        private Image _everythingBackground;
        private Image _essentialBackground;

        private GarageSimulation _simulation;
        private ActiveCar _car;
        private Quote _quote;

        /// <summary>Raised with the customer's answer. Null option means "back to the ramp".</summary>
        public event Action<ActiveCar, Quote, QuoteOption?> Answered;

        public void Build(RectTransform parent, GarageSimulation simulation)
        {
            _simulation = simulation;

            _root = UIFactory.CreateRect("QuotePanel", parent);
            UIFactory.Stretch(_root);

            RectTransform rows = UIFactory.CreateRect("Lines", _root);
            UIFactory.AnchorTop(rows, LineHeight * MaxLines, 0f, 0f);
            UIFactory.AddVerticalLayout(rows.gameObject, 4f);

            for (int i = 0; i < MaxLines; i++) _lines.Add(BuildLine(rows));

            // What the parts will take out of each option. The cost is the same money either way;
            // the surcharge is not, so a shelf that cannot cover the work is called out here - at
            // the moment the decision is made, rather than as a surprise afterwards.
            _partsEverything = UIFactory.CreateText("PartsEverything", _root, string.Empty,
                Theme.FontTiny, Theme.TextMuted, TextAnchor.MiddleLeft);
            UIFactory.AnchorBottom(_partsEverything.rectTransform, 24f, 162f, 6f);

            _partsEssential = UIFactory.CreateText("PartsEssential", _root, string.Empty,
                Theme.FontTiny, Theme.TextMuted, TextAnchor.MiddleLeft);
            UIFactory.AnchorBottom(_partsEssential.rectTransform, 24f, 136f, 6f);

            _wantHint = UIFactory.CreateText("WantHint", _root, string.Empty, Theme.FontTiny,
                Theme.TextMuted, TextAnchor.MiddleCenter);
            UIFactory.AnchorBottom(_wantHint.rectTransform, 26f, 108f, 0f);

            RectTransform options = UIFactory.CreateRect("Options", _root);
            UIFactory.AnchorBottom(options, 100f, 0f, 0f);
            UIFactory.AddHorizontalLayout(options.gameObject, 6f);

            _everythingButton = BuildOption(options, "Everything", "EVERYTHING",
                () => Answer(QuoteOption.Everything), out _everythingPrice, out _everythingBackground);

            _essentialButton = BuildOption(options, "Essentials", "ESSENTIALS\nONLY",
                () => Answer(QuoteOption.EssentialOnly), out _essentialPrice, out _essentialBackground);

            Text ignored;
            Image ignoredBackground;
            _backButton = BuildOption(options, "Back", "BACK TO\nTHE RAMP",
                () => Answer(null), out ignored, out ignoredBackground);
            ignored.text = "inspect more";
            ignored.color = Theme.TextMuted;
            ignored.fontSize = Theme.FontTiny;

            SetVisible(false);
        }

        private LineView BuildLine(RectTransform parent)
        {
            Image panel = UIFactory.CreatePanel("Line", parent, Theme.PanelSunken, 8);
            UIFactory.SetPreferredHeight(panel.gameObject, LineHeight - 4f);

            LineView line = new LineView();
            line.Root = panel.rectTransform;
            line.Background = panel;

            line.Name = UIFactory.CreateText("Name", panel.transform, string.Empty, Theme.FontBody,
                Theme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.AnchorTop(line.Name.rectTransform, 30f, 5f, 12f);

            line.Why = UIFactory.CreateText("Why", panel.transform, string.Empty, Theme.FontTiny,
                Theme.TextMuted, TextAnchor.UpperLeft);
            UIFactory.AnchorTop(line.Why.rectTransform, 22f, 30f, 12f);

            line.Price = UIFactory.CreateText("Price", panel.transform, string.Empty, Theme.FontBody,
                Theme.Cash, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Stretch(line.Price.rectTransform, 12f);

            line.Needed = UIFactory.CreateText("Needed", panel.transform, "NEEDED", Theme.FontTiny,
                Theme.Danger, TextAnchor.MiddleRight, FontStyle.Bold);
            line.Needed.rectTransform.anchorMin = new Vector2(1f, 0f);
            line.Needed.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.Needed.rectTransform.pivot = new Vector2(1f, 0.5f);
            line.Needed.rectTransform.sizeDelta = new Vector2(150f, 0f);
            line.Needed.rectTransform.anchoredPosition = new Vector2(-110f, 0f);

            panel.gameObject.SetActive(false);
            return line;
        }

        private Button BuildOption(RectTransform parent, string name, string label, Action onClick,
            out Text priceText, out Image background)
        {
            Button button = UIFactory.CreateButton(name, parent, string.Empty,
                Theme.PanelRaised, Theme.TextPrimary, Theme.FontSmall, onClick);

            background = button.GetComponent<Image>();

            Text placeholder = button.GetComponentInChildren<Text>();
            placeholder.gameObject.SetActive(false);

            Text title = UIFactory.CreateText("Title", button.transform, label, Theme.FontSmall,
                Theme.TextPrimary, TextAnchor.LowerCenter, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.AnchorTop(title.rectTransform, 56f, 6f, 4f);

            priceText = UIFactory.CreateText("Price", button.transform, string.Empty, Theme.FontBody,
                Theme.Cash, TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.AnchorBottom(priceText.rectTransform, 30f, 6f, 4f);

            return button;
        }

        /// <summary>Points the panel at a car's quote and shows it.</summary>
        public void Bind(ActiveCar car, Quote quote)
        {
            _car = car;
            _quote = quote;
            SetVisible(car != null && quote != null);
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        public void Refresh()
        {
            if (_root == null || _car == null || _quote == null) return;

            for (int i = 0; i < _lines.Count; i++)
            {
                LineView view = _lines[i];

                if (i >= _quote.LineCount)
                {
                    view.Root.gameObject.SetActive(false);
                    continue;
                }

                QuoteLine line = _quote.Lines[i];
                view.Root.gameObject.SetActive(true);

                view.Name.text = line.Type.DisplayName();

                // The reading that justifies the line. "Brakes at 34%" is why the brake service
                // is on the bill - without it the quote is just a list of prices.
                view.Why.text = line.System.DisplayName() + " at " + line.ConditionPercent + "%";

                view.Price.text = "$" + CashFormat.Short(line.Price);
                view.Needed.gameObject.SetActive(line.IsEssential);
                view.Background.color = line.IsEssential
                    ? Theme.WithAlpha(Theme.Danger, 0.12f)
                    : Theme.PanelSunken;
            }

            RefreshPartsLine(_partsEverything, "Everything", false);
            RefreshPartsLine(_partsEssential, "Essentials", true);

            _everythingPrice.text = "$" + CashFormat.Short(_quote.EverythingPrice);
            _essentialPrice.text = "$" + CashFormat.Short(_quote.EssentialPrice);

            // The option this customer was hoping for, outlined so the choice is informed.
            QuoteOption preferred = Quote.PreferenceOf(_car.Mood);
            _everythingBackground.color = preferred == QuoteOption.Everything
                ? Theme.WithAlpha(Theme.Success, 0.18f) : Theme.PanelRaised;
            _essentialBackground.color = preferred == QuoteOption.EssentialOnly
                ? Theme.WithAlpha(Theme.Success, 0.18f) : Theme.PanelRaised;

            _wantHint.text = preferred == QuoteOption.Everything
                ? "Wants it done properly"
                : "Would rather keep the bill down";
        }

        /// <summary>
        /// Writes one "what the parts cost" line.
        ///
        /// Everything here is read from Core - the grade, the share it costs, what is on the shelf.
        /// The view works out which kinds are short only by counting how many of each the quote
        /// needs against what the inventory holds; it never re-derives a price.
        /// </summary>
        private void RefreshPartsLine(Text label, string prefix, bool essentialOnly)
        {
            // The counting lives in Core.Quote.SummariseParts, so this view and the web build
            // cannot come to different conclusions about the same quote.
            Quote.PartsSummary summary = _quote.SummariseParts(_simulation.Inventory, essentialOnly);

            if (summary.NeedsNothing)
            {
                label.text = prefix + ": no parts needed";
                label.color = Theme.TextMuted;
                return;
            }

            bool isShort = summary.Short.Count > 0;

            string text = prefix + ": " + _simulation.Inventory.Policy.DisplayName() + " parts  "
                          + (isShort ? "~" : string.Empty)
                          + "$" + CashFormat.Short(summary.Value);

            if (isShort)
            {
                List<string> names = new List<string>();
                for (int i = 0; i < summary.Short.Count; i++) names.Add(summary.Short[i].DisplayName());

                text += "   " + string.Join(", ", names.ToArray()) + " off the van";
            }

            label.text = text;
            label.color = isShort ? Theme.Danger : Theme.TextMuted;
        }

        private void Answer(QuoteOption? option)
        {
            Action<ActiveCar, Quote, QuoteOption?> handler = Answered;
            if (handler != null && _car != null) handler(_car, _quote, option);
        }
    }
}
