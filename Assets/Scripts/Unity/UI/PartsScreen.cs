using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// The parts screen: what the garage fits, and what is on the shelf.
    ///
    /// Every rule and number here comes out of Core.Parts - the grades, their costs and quality
    /// shifts, the shelf cap, the expedite fee. Nothing is restated locally, because a second copy
    /// of the rules in the view is exactly how two builds of the same game start disagreeing.
    /// </summary>
    public sealed class PartsScreen
    {
        /// <summary>One of the three grade buttons.</summary>
        private sealed class GradeRow
        {
            public PartGrade Grade;
            public Button Button;
            public Image Background;
            public Text Stars;
        }

        /// <summary>One shelf line: a kind, its pips, and the button to fill it now.</summary>
        private sealed class ShelfRow
        {
            public PartKind Kind;
            public Image Panel;
            public Text Name;
            public readonly List<Image> Pips = new List<Image>();
            public Button Fill;
            public Text FillLabel;
        }

        private const float GradeHeight = 96f;
        private const float ShelfRowHeight = 54f;

        private GarageSimulation _simulation;
        private Action _onChanged;

        private RectTransform _root;
        private Text _cash;
        private readonly List<GradeRow> _grades = new List<GradeRow>();
        private readonly List<ShelfRow> _shelf = new List<ShelfRow>();

        public bool IsVisible { get { return _root != null && _root.gameObject.activeSelf; } }

        public void Build(RectTransform parent, GarageSimulation simulation, Action onChanged)
        {
            _simulation = simulation;
            _onChanged = onChanged;

            Image backdrop = UIFactory.CreateImage("PartsScreen", parent, Theme.Hex("#0C1117"));
            _root = backdrop.rectTransform;
            UIFactory.Stretch(_root);
            backdrop.raycastTarget = true;

            Text title = UIFactory.CreateText("Title", _root, "PARTS", Theme.FontTitle,
                Theme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AnchorTop(title.rectTransform, 60f, 40f, Theme.ScreenPadding);

            _cash = UIFactory.CreateText("Cash", _root, "$0", Theme.FontHeading,
                Theme.Cash, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.AnchorTop(_cash.rectTransform, 60f, 40f, Theme.ScreenPadding);

            Text blurb = UIFactory.CreateText("Blurb", _root,
                "What the garage fits. Cheaper parts leave more in the till and finish worse; "
                + "better ones cost you and finish better.",
                Theme.FontSmall, Theme.TextSecondary, TextAnchor.UpperLeft);
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.AnchorTop(blurb.rectTransform, 60f, 106f, Theme.ScreenPadding);

            // ---- the one decision ----
            float gradesTop = 176f;
            foreach (PartGrade grade in Enum.GetValues(typeof(PartGrade)))
            {
                _grades.Add(BuildGrade(grade, gradesTop + (int)grade * (GradeHeight + 8f)));
            }

            // ---- the shelf ----
            float shelfTop = gradesTop + PartGrades.Count * (GradeHeight + 8f) + 14f;

            Text shelfHeading = UIFactory.CreateText("ShelfHeading", _root, "ON THE SHELF",
                Theme.FontTiny, Theme.Cash, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AnchorTop(shelfHeading.rectTransform, 26f, shelfTop, Theme.ScreenPadding);

            Text shelfBlurb = UIFactory.CreateText("ShelfBlurb", _root,
                "Stock turns up on its own, free. Running out is not a blocker - the part comes off "
                + "the van instead, at " + SurchargePercent() + "% on top.",
                Theme.FontTiny, Theme.TextMuted, TextAnchor.UpperLeft);
            shelfBlurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.AnchorTop(shelfBlurb.rectTransform, 48f, shelfTop + 28f, Theme.ScreenPadding);

            float rowTop = shelfTop + 82f;
            int index = 0;

            foreach (PartKind kind in Enum.GetValues(typeof(PartKind)))
            {
                if (kind == PartKind.None) continue;

                _shelf.Add(BuildShelfRow(kind, rowTop + index * (ShelfRowHeight + 5f)));
                index++;
            }

            // ---- back ----
            Button back = UIFactory.CreateButton("Back", _root, "BACK TO THE GARAGE",
                Theme.PanelRaised, Theme.TextPrimary, Theme.FontBody, Hide);
            UIFactory.AnchorBottom(back.GetComponent<RectTransform>(), Theme.TouchTargetHeight,
                28f, Theme.ScreenPadding);

            Hide();
        }

        /// <summary>The surcharge as a whole percent, read off the Core constant rather than typed.</summary>
        private static int SurchargePercent()
        {
            return (int)((GameBalance.PartsCounterMarkup - 1d) * 100d + 0.5d);
        }

        private GradeRow BuildGrade(PartGrade grade, float top)
        {
            Button button = UIFactory.CreateButton("Grade" + grade, _root, string.Empty,
                Theme.PanelRaised, Theme.TextPrimary, Theme.FontBody, () => Choose(grade));

            RectTransform rect = button.GetComponent<RectTransform>();
            UIFactory.AnchorTop(rect, GradeHeight, top, Theme.ScreenPadding);

            Text placeholder = button.GetComponentInChildren<Text>();
            placeholder.gameObject.SetActive(false);

            Text name = UIFactory.CreateText("Name", button.transform, grade.DisplayName(),
                Theme.FontHeading, Theme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.AnchorTop(name.rectTransform, 40f, 8f, 18f);

            GradeRow row = new GradeRow();
            row.Grade = grade;
            row.Button = button;
            row.Background = button.GetComponent<Image>();

            // Stars come from the grade itself, so the shop and the rules can never disagree.
            row.Stars = UIFactory.CreateText("Stars", button.transform,
                RepairQuality.StarsText(grade.QualityStars()),
                Theme.FontSmall, Theme.Cash, TextAnchor.UpperRight, FontStyle.Bold);
            UIFactory.AnchorTop(row.Stars.rectTransform, 40f, 10f, 18f);

            Text description = UIFactory.CreateText("Desc", button.transform, grade.Description(),
                Theme.FontSmall, Theme.TextMuted, TextAnchor.UpperLeft);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.AnchorTop(description.rectTransform, 44f, 46f, 18f);

            return row;
        }

        private ShelfRow BuildShelfRow(PartKind kind, float top)
        {
            Image panel = UIFactory.CreatePanel("Shelf" + kind, _root, Theme.PanelSunken, 8);
            UIFactory.AnchorTop(panel.rectTransform, ShelfRowHeight, top, Theme.ScreenPadding);

            ShelfRow row = new ShelfRow();
            row.Kind = kind;
            row.Panel = panel;

            row.Name = UIFactory.CreateText("Name", panel.transform, kind.DisplayName().ToUpperInvariant(),
                Theme.FontTiny, Theme.TextSecondary, TextAnchor.MiddleLeft, FontStyle.Bold);
            row.Name.rectTransform.anchorMin = new Vector2(0f, 0f);
            row.Name.rectTransform.anchorMax = new Vector2(0f, 1f);
            row.Name.rectTransform.pivot = new Vector2(0f, 0.5f);
            row.Name.rectTransform.sizeDelta = new Vector2(250f, 0f);
            row.Name.rectTransform.anchoredPosition = new Vector2(14f, 0f);

            // One pip per shelf slot, sized from the Core cap so the two can never disagree.
            RectTransform pips = UIFactory.CreateRect("Pips", panel.transform);
            pips.anchorMin = new Vector2(0f, 0.5f);
            pips.anchorMax = new Vector2(1f, 0.5f);
            pips.pivot = new Vector2(0.5f, 0.5f);
            pips.offsetMin = new Vector2(268f, -9f);
            pips.offsetMax = new Vector2(-150f, 9f);
            UIFactory.AddHorizontalLayout(pips.gameObject, 5f);

            for (int i = 0; i < GameBalance.PartShelfCap; i++)
            {
                Image pip = UIFactory.CreatePanel("Pip" + i, pips, Theme.PanelRaised, 4);
                row.Pips.Add(pip);
            }

            row.Fill = UIFactory.CreateButton("Fill", panel.transform, string.Empty,
                Theme.PanelRaised, Theme.TextPrimary, Theme.FontTiny, () => Expedite(kind));

            RectTransform fillRect = row.Fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(1f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.pivot = new Vector2(1f, 0.5f);
            fillRect.sizeDelta = new Vector2(132f, 38f);
            fillRect.anchoredPosition = new Vector2(-10f, 0f);

            row.FillLabel = row.Fill.GetComponentInChildren<Text>();

            return row;
        }

        private void Choose(PartGrade grade)
        {
            _simulation.Inventory.Policy = grade;
            Refresh();
            if (_onChanged != null) _onChanged();
        }

        private void Expedite(PartKind kind)
        {
            if (!_simulation.TryExpediteParts(kind)) return;

            Refresh();
            if (_onChanged != null) _onChanged();
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Refresh();
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (!IsVisible) return;

            PartsInventory inventory = _simulation.Inventory;
            _cash.text = "$" + CashFormat.Short(_simulation.Wallet.Cash);

            for (int i = 0; i < _grades.Count; i++)
            {
                GradeRow row = _grades[i];
                bool chosen = inventory.Policy == row.Grade;

                row.Background.color = chosen
                    ? Theme.WithAlpha(Theme.Success, 0.18f)
                    : Theme.PanelRaised;
            }

            for (int i = 0; i < _shelf.Count; i++)
            {
                ShelfRow row = _shelf[i];
                int stock = inventory.TotalStockOf(row.Kind);

                for (int pip = 0; pip < row.Pips.Count; pip++)
                {
                    row.Pips[pip].color = pip < stock ? Theme.Success : Theme.PanelSunken;
                }

                // A bare shelf is the one thing worth shouting about: it is what starts costing
                // the surcharge on the very next job of that kind.
                row.Panel.color = stock == 0
                    ? Theme.WithAlpha(Theme.Danger, 0.12f)
                    : Theme.PanelSunken;
                row.Name.color = stock == 0 ? Theme.Danger : Theme.TextSecondary;

                bool full = stock >= GameBalance.PartShelfCap;
                double fee = PartsInventory.ExpediteFee(row.Kind, _simulation.RankLevel);

                row.FillLabel.text = full ? "Full" : "$" + CashFormat.Short(fee);
                row.Fill.interactable = !full && _simulation.Wallet.Cash >= fee;
            }
        }
    }
}
