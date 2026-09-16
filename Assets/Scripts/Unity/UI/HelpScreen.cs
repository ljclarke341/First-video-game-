using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Minigames;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// The "how to play" screen.
    ///
    /// It opens by itself the first time someone plays and is always one tap away afterwards.
    /// Each mini-game is introduced with its own colour-coded heading rather than a wall of text,
    /// because the thing a new player needs first is "which of these four am I looking at".
    /// </summary>
    public sealed class HelpScreen
    {
        private RectTransform _root;
        private RectTransform _content;

        /// <summary>True while the help screen is on screen.</summary>
        public bool IsVisible { get { return _root != null && _root.gameObject.activeSelf; } }

        public void Build(RectTransform parent)
        {
            // Solid rather than translucent: this screen is read, not glanced at, and the garage
            // showing through behind the text makes it hard work.
            Image backdrop = UIFactory.CreateImage("HelpScreen", parent, Theme.Hex("#0C1117"));
            _root = backdrop.rectTransform;
            UIFactory.Stretch(_root);
            backdrop.raycastTarget = true;

            Text title = UIFactory.CreateText("Title", _root, "HOW TO PLAY", Theme.FontTitle,
                Theme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.AnchorTop(title.rectTransform, 60f, 40f, Theme.ScreenPadding);

            // ---- scrollable body ----
            RectTransform viewport = UIFactory.CreateRect("Viewport", _root);
            UIFactory.AnchorMiddle(viewport, 118f, 150f, Theme.ScreenPadding);

            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _content = UIFactory.CreateRect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;

            UIFactory.AddVerticalLayout(_content.gameObject, 10f);
            ContentSizeFitter fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 45f;

            BuildContent();

            Button close = UIFactory.CreateButton("Close", _root, "GOT IT", Theme.Success,
                Theme.TextOnAccent, Theme.FontBody, Hide);
            UIFactory.AnchorBottom(close.GetComponent<RectTransform>(), Theme.TouchTargetHeight, 40f, Theme.ScreenPadding);

            _root.gameObject.SetActive(false);
        }

        private void BuildContent()
        {
            Paragraph("You run a car garage. Cars roll in needing repairs, and every repair job is a "
                      + "small mini-game. Finish a car before the owner loses patience and you get paid.");

            Heading("THE LOOP");
            Step(1, "A car arrives with two to four jobs on it. Its card shows what it needs, what it "
                    + "pays, and a bar counting down the owner's patience.");
            Step(2, "Tap the car to start work. Each job is played as one of the four mini-games below, "
                    + "over and over, until that job's bar is full.");
            Step(3, "Finish every job and the owner pays up. Let the timer run out and they leave "
                    + "annoyed - you keep what the finished jobs earned and lose the rest.");

            Heading("THE FOUR JOBS");
            Minigame(MinigameType.TimingBar, Theme.Info,
                "A marker sweeps across the bar. Tap when it is in the GREEN. The gold centre is a "
                + "perfect hit and pays a bonus.");
            Minigame(MinigameType.ToolMatch, Theme.Warning,
                "The job stays on screen and the tools flash up briefly. Once they hide, tap the one "
                + "that was right. The WRONG TOOL DAMAGES THE PART - you lose progress, not just time.");
            Minigame(MinigameType.HoldRelease, Theme.Success,
                "HOLD the button to wind the gauge up and let go in the green. Let go early and the "
                + "bolt is loose. Hold past the red line and you strip the thread.");
            Minigame(MinigameType.RapidSequence, Theme.Prestige,
                "A short pattern of directions lights up one at a time, then hides. Repeat it on the "
                + "pad. One wrong tap ends the round, so take the moment to read it.");

            Heading("GETTING PAID");
            Bullet(Theme.Cash, "Perfect rounds pay a bonus. Finish a whole job without dropping a "
                               + "single round and it pays 25% more.");
            Bullet(Theme.Success, "Finish quickly for a tip worth up to a quarter of the car, based on "
                                  + "how fast the repair went once you started it.");
            Bullet(Theme.Danger, "Damage - the wrong tool, or over-torquing - costs you progress you "
                                 + "already earned, and patience on top.");
            Bullet(Theme.Info, "Rarer cars pay far more but are harder and take longer.");

            Heading("GROWING THE GARAGE");
            for (int i = 0; i < 4; i++)
            {
                UpgradeBranch branch = (UpgradeBranch)i;
                Bullet(Theme.Hex(branch.ColorHex()), branch.DisplayName() + " - " + branch.Description());
            }
            Paragraph("Once you bank $" + CashFormat.Short(GameBalance.PrestigeCashCap)
                      + " you can sell the garage: you lose your cash and upgrades, but keep Reputation "
                      + "Tokens that raise every future payout, permanently.");

            Heading("IF IT FEELS TOO FAST");
            Paragraph("Open STATS and turn on RELAXED PACE. It gives you longer to read the tools and "
                      + "patterns, and slows the markers down. It pays exactly the same - nothing is "
                      + "lost by using it.");
        }

        // ------------------------------------------------------------------
        // Small builders. Each returns a row already sized for the layout group.
        // ------------------------------------------------------------------

        private void Heading(string text)
        {
            Text label = UIFactory.CreateText("Heading", _content, text, Theme.FontSmall,
                Theme.Cash, TextAnchor.LowerLeft, FontStyle.Bold);
            UIFactory.SetPreferredHeight(label.gameObject, 52f);
        }

        private void Paragraph(string text)
        {
            Text label = UIFactory.CreateText("Paragraph", _content, text, Theme.FontTiny,
                Theme.TextSecondary, TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetPreferredHeight(label.gameObject, EstimateHeight(text, 30f));
        }

        private void Step(int number, string text)
        {
            Image row = UIFactory.CreateImage("Step", _content, new Color(0f, 0f, 0f, 0f));
            UIFactory.SetPreferredHeight(row.gameObject, EstimateHeight(text, 42f));

            Image bubble = UIFactory.CreatePanel("Number", row.transform, Theme.Cash, 24);
            RectTransform bubbleRect = bubble.rectTransform;
            bubbleRect.anchorMin = new Vector2(0f, 1f);
            bubbleRect.anchorMax = new Vector2(0f, 1f);
            bubbleRect.pivot = new Vector2(0f, 1f);
            bubbleRect.sizeDelta = new Vector2(40f, 40f);
            bubbleRect.anchoredPosition = Vector2.zero;

            Text numberLabel = UIFactory.CreateText("N", bubble.transform, number.ToString(),
                Theme.FontSmall, Theme.TextOnAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(numberLabel.rectTransform);

            Text body = UIFactory.CreateText("Body", row.transform, text, Theme.FontTiny,
                Theme.TextSecondary, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(54f, 0f);
            bodyRect.offsetMax = Vector2.zero;
        }

        private void Minigame(MinigameType type, Color accent, string text)
        {
            Image card = UIFactory.CreatePanel("Minigame", _content, Theme.Panel);
            UIFactory.SetPreferredHeight(card.gameObject, EstimateHeight(text, 78f));

            Image tag = UIFactory.CreatePanel("Tag", card.transform, accent, 8);
            RectTransform tagRect = tag.rectTransform;
            tagRect.anchorMin = new Vector2(0f, 1f);
            tagRect.anchorMax = new Vector2(0f, 1f);
            tagRect.pivot = new Vector2(0f, 1f);
            tagRect.sizeDelta = new Vector2(190f, 34f);
            tagRect.anchoredPosition = new Vector2(Theme.PanelPadding, -Theme.PanelPadding * 0.6f);

            Text tagLabel = UIFactory.CreateText("TagLabel", tag.transform, type.DisplayName().ToUpperInvariant(),
                Theme.FontTiny, Theme.TextOnAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(tagLabel.rectTransform);

            Text body = UIFactory.CreateText("Body", card.transform, text, Theme.FontTiny,
                Theme.TextSecondary, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(Theme.PanelPadding, Theme.PanelPadding * 0.6f);
            bodyRect.offsetMax = new Vector2(-Theme.PanelPadding, -52f);
        }

        private void Bullet(Color dotColor, string text)
        {
            Image row = UIFactory.CreateImage("Bullet", _content, new Color(0f, 0f, 0f, 0f));
            UIFactory.SetPreferredHeight(row.gameObject, EstimateHeight(text, 34f));

            Image dot = UIFactory.CreateImage("Dot", row.transform, dotColor, UISprites.Circle(32));
            RectTransform dotRect = dot.rectTransform;
            dotRect.anchorMin = new Vector2(0f, 1f);
            dotRect.anchorMax = new Vector2(0f, 1f);
            dotRect.pivot = new Vector2(0f, 1f);
            dotRect.sizeDelta = new Vector2(16f, 16f);
            dotRect.anchoredPosition = new Vector2(2f, -8f);

            Text body = UIFactory.CreateText("Body", row.transform, text, Theme.FontTiny,
                Theme.TextSecondary, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(34f, 0f);
            bodyRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Rough height for a wrapped block of text. Unity's layout system cannot size a wrapped
        /// Text inside a vertical group without a ContentSizeFitter per row, which is heavier than
        /// this screen needs - so estimate from the character count and err generous.
        /// </summary>
        private static float EstimateHeight(string text, float extra)
        {
            const float CharactersPerLine = 46f;
            const float LineHeight = 34f;

            int lines = Mathf.Max(1, Mathf.CeilToInt(text.Length / CharactersPerLine));
            return lines * LineHeight + extra;
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _content.anchoredPosition = Vector2.zero;
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }
}
