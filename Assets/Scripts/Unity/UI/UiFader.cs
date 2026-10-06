using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// Fades and pops a panel in whenever it is enabled.
    ///
    /// Popups appearing instantly feel like a bug report; a 150ms fade with a slight scale-up reads
    /// as deliberate. Uses unscaled time so it still animates if the game is ever paused.
    /// </summary>
    public sealed class UiFader : MonoBehaviour
    {
        /// <summary>How long the fade takes.</summary>
        public float Duration = 0.15f;

        /// <summary>Scale the panel starts at before settling to 1.</summary>
        public float StartScale = 0.94f;

        /// <summary>Optional inner panel to scale. If unset, only the alpha animates.</summary>
        public RectTransform ScaleTarget;

        private CanvasGroup _group;
        private float _age;
        private bool _animating;

        private void Awake()
        {
            _group = UIFactory.AddCanvasGroup(gameObject);
        }

        private void OnEnable()
        {
            _age = 0f;
            _animating = true;

            if (_group != null) _group.alpha = 0f;
            if (ScaleTarget != null) ScaleTarget.localScale = new Vector3(StartScale, StartScale, 1f);
        }

        private void Update()
        {
            if (!_animating) return;

            _age += Time.unscaledDeltaTime;
            float t = Duration <= 0f ? 1f : Mathf.Clamp01(_age / Duration);

            // Ease out: fast at the start, settling at the end.
            float eased = 1f - (1f - t) * (1f - t);

            if (_group != null) _group.alpha = eased;

            if (ScaleTarget != null)
            {
                float scale = Mathf.Lerp(StartScale, 1f, eased);
                ScaleTarget.localScale = new Vector3(scale, scale, 1f);
            }

            if (t >= 1f)
            {
                _animating = false;
                if (_group != null) _group.alpha = 1f;
                if (ScaleTarget != null) ScaleTarget.localScale = Vector3.one;
            }
        }
    }
}
