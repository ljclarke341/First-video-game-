using System;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// A reusable modal: a dimmed backdrop, a rounded card, a title, a body and up to two buttons.
    /// Used for the welcome-back report, the prestige confirmation and the stats screen.
    /// </summary>
    public sealed class PopupPanel
    {
        private RectTransform _root;
        private Image _card;
        private Text _title;
        private Text _body;
        private Button _primary;
        private Text _primaryLabel;
        private Button _secondary;
        private Text _secondaryLabel;

        private Action _onPrimary;
        private Action _onSecondary;

        public bool IsVisible { get { return _root != null && _root.gameObject.activeSelf; } }

        public void Build(RectTransform parent)
        {
            Image backdrop = UIFactory.CreateImage("Popup", parent, new Color(0f, 0f, 0f, 0.72f));
            _root = backdrop.rectTransform;
            UIFactory.Stretch(_root);
            backdrop.raycastTarget = true;

            _card = UIFactory.CreatePanel("Card", _root, Theme.Panel);
            UIFactory.AddOutline(_card, Theme.PanelOutline);
            RectTransform cardRect = _card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(880f, 760f);

            _title = UIFactory.CreateText("Title", _card.transform, string.Empty, Theme.FontTitle,
                Theme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.AnchorTop(_title.rectTransform, 70f, 32f, Theme.PanelPadding);

            _body = UIFactory.CreateText("Body", _card.transform, string.Empty, Theme.FontBody,
                Theme.TextSecondary, TextAnchor.UpperCenter);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.AnchorMiddle(_body.rectTransform, 120f, 200f, Theme.PanelPadding * 1.5f);

            _primary = UIFactory.CreateButton("Primary", _card.transform, "OK", Theme.Success,
                Theme.TextOnAccent, Theme.FontBody, () =>
                {
                    Action handler = _onPrimary;
                    Hide();
                    if (handler != null) handler();
                });
            UIFactory.AnchorBottom(_primary.GetComponent<RectTransform>(), Theme.TouchTargetHeight, 32f, Theme.PanelPadding);
            _primaryLabel = _primary.GetComponentInChildren<Text>();

            _secondary = UIFactory.CreateButton("Secondary", _card.transform, "Cancel", Theme.PanelRaised,
                Theme.TextPrimary, Theme.FontBody, () =>
                {
                    Action handler = _onSecondary;
                    Hide();
                    if (handler != null) handler();
                });
            UIFactory.AnchorBottom(_secondary.GetComponent<RectTransform>(), Theme.TouchTargetHeight, 32f + Theme.TouchTargetHeight + 14f, Theme.PanelPadding);
            _secondaryLabel = _secondary.GetComponentInChildren<Text>();

            // A popup that snaps into existence reads as a glitch; a short fade reads as intent.
            UiFader fader = _root.gameObject.AddComponent<UiFader>();
            fader.ScaleTarget = _card.rectTransform;

            _root.gameObject.SetActive(false);
        }

        /// <summary>Shows the popup. Pass null for the secondary label to hide the second button.</summary>
        public void Show(string title, string body, string primaryLabel, Action onPrimary,
            string secondaryLabel = null, Action onSecondary = null, Color? accent = null)
        {
            _title.text = title;
            _title.color = accent ?? Theme.TextPrimary;
            _body.text = body;

            _primaryLabel.text = primaryLabel;
            _primary.GetComponent<Image>().color = accent ?? Theme.Success;
            _onPrimary = onPrimary;

            bool hasSecondary = !string.IsNullOrEmpty(secondaryLabel);
            _secondary.gameObject.SetActive(hasSecondary);
            if (hasSecondary) _secondaryLabel.text = secondaryLabel;
            _onSecondary = onSecondary;

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }
}
