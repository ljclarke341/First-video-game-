using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Minigames
{
    /// <summary>
    /// MINI-GAME 3 - HOLD AND RELEASE.
    /// Hold the button to wind torque/pressure into the gauge and let go inside the green band.
    /// Let go early and the bolt is loose (weak progress). Hold too long and you strip the thread:
    /// that is DAMAGE - progress is actively lost.
    /// </summary>
    public sealed class HoldReleaseMinigame : MinigameBase
    {
        /// <summary>Needle position on the gauge. 1.0 is the redline; it can overshoot to 1.2 before blowing.</summary>
        public float Pressure { get; private set; }

        /// <summary>Centre of the safe green band.</summary>
        public float TargetCenter { get; private set; }

        /// <summary>Half width of the safe band.</summary>
        public float TargetHalfWidth { get; private set; }

        /// <summary>Half width of the gold core inside the band.</summary>
        public float PerfectHalfWidth { get; private set; }

        /// <summary>Gauge units gained per second while held.</summary>
        public float FillRate { get; private set; }

        /// <summary>True while the player is holding the button down.</summary>
        public bool IsHolding { get; private set; }

        /// <summary>Pressure at which the part gives up entirely.</summary>
        public const float BlowoutPressure = 1.0f;

        public override MinigameType Type { get { return MinigameType.HoldRelease; } }

        public override string Prompt { get { return "Hold, then release in the green"; } }

        public HoldReleaseMinigame(float difficulty, MinigameTuning tuning, IRandomSource random)
            : base(difficulty, tuning, random)
        {
            TargetHalfWidth = ScaleWindow(0.10f, 0.035f, 0.26f);
            PerfectHalfWidth = TargetHalfWidth * 0.36f;

            // The band always sits in the upper half of the gauge, close enough to the redline to be tense,
            // but never so close that a perfect release is impossible.
            float lowest = 0.45f + TargetHalfWidth;
            float highest = BlowoutPressure - TargetHalfWidth - 0.04f;
            if (highest < lowest) highest = lowest;
            TargetCenter = Random.Range(lowest, highest);

            FillRate = ScaleSpeed(0.42f);
            Pressure = 0f;
            IsHolding = false;

            TimeLimit = 7f;
        }

        protected override void OnTick(float deltaTime)
        {
            if (!IsHolding) return;

            Pressure += FillRate * deltaTime;

            if (Pressure >= BlowoutPressure)
            {
                Pressure = BlowoutPressure;
                // Held past the redline: the thread strips whether the player lets go now or not.
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Damage, "Stripped the thread!"));
            }
        }

        /// <summary>Finger down: start winding the gauge up.</summary>
        public override void Press()
        {
            if (IsFinished) return;
            IsHolding = true;
        }

        /// <summary>Finger up: this is the moment that gets judged.</summary>
        public override void Release()
        {
            if (IsFinished) return;

            // Releasing without ever pressing does nothing at all.
            if (!IsHolding) return;

            IsHolding = false;

            float distance = MathUtil.Abs(Pressure - TargetCenter);

            if (distance <= PerfectHalfWidth)
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Perfect, "PERFECT TORQUE!"));
            }
            else if (distance <= TargetHalfWidth)
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Good, "Torqued up"));
            }
            else if (Pressure > TargetCenter)
            {
                // Over the band but short of the redline: over-torqued, something cracks.
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Damage, "Over-torqued!"));
            }
            else if (Pressure >= TargetCenter * 0.5f)
            {
                // Well short of the band: the fastener is on, just not tight.
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Weak, "Left it loose"));
            }
            else
            {
                Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Barely turned it"));
            }
        }

        protected override void OnTimeout()
        {
            // Running the clock out while never pressing is a plain miss.
            Finish(MinigameResult.FromOutcome(MinigameOutcome.Miss, "Too slow!"));
        }
    }
}
