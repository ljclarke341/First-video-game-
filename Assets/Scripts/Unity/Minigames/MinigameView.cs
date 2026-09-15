using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Unity.UI;
using UnityEngine;

namespace GarageTycoon.Unity.Minigames
{
    /// <summary>
    /// Base class for the on-screen half of a mini-game.
    ///
    /// The split to understand here: the mini-game CLASSES in Core hold all the rules and state and
    /// know nothing about Unity; these VIEW classes only draw that state and forward touches back.
    /// Because of that split the rules can be tested without ever opening the editor.
    /// </summary>
    public abstract class MinigameView
    {
        /// <summary>Root object for this view. Toggled on and off as rounds start and end.</summary>
        public RectTransform Root { get; protected set; }

        /// <summary>The round currently being drawn, or null when idle.</summary>
        protected MinigameBase Minigame { get; private set; }

        /// <summary>The simulation, so input can be forwarded straight to the player's session.</summary>
        protected GarageSimulation Simulation { get; private set; }

        /// <summary>Which mini-game this view can draw.</summary>
        public abstract MinigameType Type { get; }

        /// <summary>Builds the view's objects once, at startup.</summary>
        public void Build(RectTransform parent, GarageSimulation simulation)
        {
            Simulation = simulation;
            Root = UIFactory.CreateRect(GetType().Name, parent);
            UIFactory.Stretch(Root);
            OnBuild();
            SetVisible(false);
        }

        /// <summary>Points the view at a new round and shows it.</summary>
        public void Bind(MinigameBase minigame)
        {
            Minigame = minigame;
            SetVisible(true);
            OnBind();
            Refresh();
        }

        /// <summary>Hides the view and forgets the round.</summary>
        public void Unbind()
        {
            OnUnbind();
            Minigame = null;
            SetVisible(false);
        }

        /// <summary>Redraws from the current state. Called once a frame while visible.</summary>
        public void Refresh()
        {
            if (Minigame == null) return;
            OnRefresh();
        }

        public void SetVisible(bool visible)
        {
            if (Root != null && Root.gameObject.activeSelf != visible)
            {
                Root.gameObject.SetActive(visible);
            }
        }

        protected abstract void OnBuild();
        protected virtual void OnBind() { }
        protected virtual void OnUnbind() { }
        protected abstract void OnRefresh();
    }
}
