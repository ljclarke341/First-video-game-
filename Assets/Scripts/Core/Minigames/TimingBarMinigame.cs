using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>
    /// MINI-GAME 1 - TIMING BAR.
    /// A marker sweeps left/right across a bar and bounces off the ends. The player taps when the marker
    /// is inside the highlighted sweet spot. Dead centre = PERFECT (bonus cash), the outer edge still
    /// counts, a near miss is a weak scrape, anything else is a clean miss.
    /// </summary>
    public sealed class TimingBarMinigame : MinigameBase
    {
        /// <summary>Marker position along the bar, 0 = far left, 1 = far right.</summary>
        public float MarkerPosition { get; private set; }

        /// <summary>+1 while the marker travels right, -1 while it travels left.</summary>
        public int Direction { get; private set; }

        /// <summary>Centre of the green zone, 0..1 along the bar.</summary>
        public float SweetSpotCenter { get; private set; }

        /// <summary>Half the width of the green zone. The full zone is centre +/- this.</summary>
        public float SweetSpotHalfWidth { get; private set; }

        /// <summary>Half width of the gold "perfect" core in the middle of the green zone.</summary>
        public float PerfectHalfWidth { get; private set; }

        /// <summary>Bar units travelled per second.</summary>
        public float Speed { get; private set; }

        public override MinigameType Type { get { return MinigameType.TimingBar; } }

        public override string Prompt { get { return "Tap inside the green zone"; } }

        public TimingBarMinigame(float difficulty, MinigameTuning tuning, IRandomSource random)
            : base(difficulty, tuning, random)
        {
            // The green zone shrinks on rarer cars and grows with Precision upgrades.
            SweetSpotHalfWidth = ScaleWindow(0.115f, 0.035f, 0.30f);

            // The gold core is always a third of the green zone, so "perfect" stays meaningfully hard.
            PerfectHalfWidth = SweetSpotHalfWidth * 0.34f;

            // Keep the zone away from the very ends of the bar, where the marker slows to turn around.
            float edgePadding = SweetSpotHalfWidth + 0.08f;
            SweetSpotCenter = Random.Range(edgePadding, 1f - edgePadding);

            Speed = ScaleSpeed(0.85f);

            // Start the marker at a random end so the player cannot memorise the rhythm.
            bool startLeft = Random.Chance(0.5f);
            MarkerPosition = startLeft ? 0f : 1f;
            Direction = startLeft ? 1 : -1;

            TimeLimit = 6f;
        }

        protected override void OnTick(float deltaTime)
        {
            MarkerPosition += Direction * Speed * deltaTime;

            // Bounce off both ends. The while-loop handles a huge deltaTime (e.g. a frame hitch)
            // without the marker escaping the bar.
            while (MarkerPosition < 0f || MarkerPosition > 1f)
            {
                if (MarkerPosition > 1f)
                {
                    MarkerPosition = 2f - MarkerPosition;
                    Direction = -1;
                }
                else
                {
                    MarkerPosition = -MarkerPosition;
                    Direction = 1;
                }
            }
        }

        /// <summary>A tap. This is the only input the timing bar cares about.</summary>
        public override void Press()
        {
            if (IsFinished) return;

            float distance = MathUtil.Abs(MarkerPosition - SweetSpotCenter);

            if (distance <= PerfectHalfWidth)
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Perfect, "PERFECT!"));
            }
            else if (distance <= SweetSpotHalfWidth)
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Good, "Nice!"));
            }
            else if (distance <= SweetSpotHalfWidth * 1.75f)
            {
                // A near miss still turns the bolt a little - it keeps the game feeling fair.
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Weak, "Just grazed it"));
            }
            else
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Missed!"));
            }
        }

        protected override void OnTimeout()
        {
            Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Too slow!"));
        }
    }
}
