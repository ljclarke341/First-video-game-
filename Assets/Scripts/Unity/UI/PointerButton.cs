using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// A touch target that reports press and release separately, which the standard Button does not.
    ///
    /// The hold-and-release mini-game needs to know the exact moment a finger lifts, so it cannot use
    /// Button.onClick (which only fires after the release, and only if the finger stayed inside).
    /// Dragging off the button counts as a release so a finger sliding away can never leave the
    /// gauge stuck on full.
    /// </summary>
    public sealed class PointerButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        /// <summary>Raised when a finger or mouse button goes down on this element.</summary>
        public event Action Pressed;

        /// <summary>Raised when the press ends, either by lifting off or by dragging away.</summary>
        public event Action Released;

        /// <summary>True while a press is in progress.</summary>
        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHeld) return;
            IsHeld = true;

            Action handler = Pressed;
            if (handler != null) handler();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ReleaseIfHeld();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // A finger sliding off the button ends the hold; otherwise the gauge would run to the
            // redline with nothing the player could do about it.
            ReleaseIfHeld();
        }

        private void OnDisable()
        {
            // Being hidden mid-hold (round ended, screen switched) must not leave the flag stuck.
            ReleaseIfHeld();
        }

        private void ReleaseIfHeld()
        {
            if (!IsHeld) return;
            IsHeld = false;

            Action handler = Released;
            if (handler != null) handler();
        }

        /// <summary>Clears the held state without raising events, for when a round is torn down.</summary>
        public void ResetState()
        {
            IsHeld = false;
        }
    }
}
