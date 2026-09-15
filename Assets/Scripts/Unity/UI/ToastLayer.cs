using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// The floating feedback text that pops up when something happens: "PERFECT!", "+$120",
    /// "Customer left!". Messages rise, fade and clean themselves up.
    ///
    /// Toasts are pooled rather than created and destroyed, because allocating objects every time
    /// the player taps is exactly the kind of thing that makes a phone stutter.
    /// </summary>
    public sealed class ToastLayer : MonoBehaviour
    {
        private const int PoolSize = 12;
        private const float Lifetime = 1.1f;
        private const float RiseDistance = 90f;

        private readonly List<Toast> _pool = new List<Toast>();
        private RectTransform _root;

        private sealed class Toast
        {
            public RectTransform Rect;
            public Text Label;
            public CanvasGroup Group;
            public float Age;
            public bool Active;
            public Vector2 Origin;
        }

        /// <summary>Creates the pool. Call once when the UI is built.</summary>
        public void Initialise(RectTransform root)
        {
            _root = root;

            for (int i = 0; i < PoolSize; i++)
            {
                RectTransform rect = UIFactory.CreateRect("Toast" + i, _root);
                rect.sizeDelta = new Vector2(520f, 70f);

                Text label = UIFactory.CreateText("Label", rect, string.Empty, Theme.FontHeading,
                    Theme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Stretch(label.rectTransform);

                // A dark outline keeps light text readable against the pale parts of a car card.
                Outline outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
                outline.effectDistance = new Vector2(2f, -2f);

                CanvasGroup group = UIFactory.AddCanvasGroup(rect.gameObject);
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                Toast toast = new Toast();
                toast.Rect = rect;
                toast.Label = label;
                toast.Group = group;
                toast.Active = false;
                _pool.Add(toast);

                rect.gameObject.SetActive(false);
            }
        }

        /// <summary>Pops a message at a position in the canvas, in the root's local space.</summary>
        public void Show(string message, Color color, Vector2 anchoredPosition)
        {
            Toast toast = FindFreeToast();
            if (toast == null) return;

            toast.Label.text = message;
            toast.Label.color = color;
            toast.Age = 0f;
            toast.Active = true;
            toast.Origin = anchoredPosition;
            toast.Rect.anchoredPosition = anchoredPosition;
            toast.Group.alpha = 1f;
            toast.Rect.gameObject.SetActive(true);
        }

        /// <summary>Pops a message in the middle of the screen, slightly above centre.</summary>
        public void ShowCentre(string message, Color color)
        {
            Show(message, color, new Vector2(0f, 120f));
        }

        private Toast FindFreeToast()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].Active) return _pool[i];
            }

            // All in use: recycle the oldest so the newest message is never silently dropped.
            Toast oldest = null;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (oldest == null || _pool[i].Age > oldest.Age) oldest = _pool[i];
            }
            return oldest;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            for (int i = 0; i < _pool.Count; i++)
            {
                Toast toast = _pool[i];
                if (!toast.Active) continue;

                toast.Age += deltaTime;
                float t = toast.Age / Lifetime;

                if (t >= 1f)
                {
                    toast.Active = false;
                    toast.Group.alpha = 0f;
                    toast.Rect.gameObject.SetActive(false);
                    continue;
                }

                // Rise quickly then ease out, fading over the last third.
                float rise = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f) * RiseDistance;
                toast.Rect.anchoredPosition = toast.Origin + new Vector2(0f, rise);
                toast.Group.alpha = t < 0.65f ? 1f : Mathf.InverseLerp(1f, 0.65f, t);

                // A small pop on the way in gives the feedback some weight.
                float scale = t < 0.15f ? Mathf.Lerp(0.7f, 1f, t / 0.15f) : 1f;
                toast.Rect.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
