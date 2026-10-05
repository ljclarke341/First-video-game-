using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Minigames;

namespace GarageTycoon.Core.Simulation
{
    /// <summary>
    /// One inspection round in progress: which car, which check, and the mini-game being played.
    ///
    /// Kept separate from <see cref="WorkSession"/> rather than folded into it. A work session is
    /// built around a job - it advances progress, pays out, builds the streak, starts the speed-tip
    /// clock. An inspection does none of those things, and bending WorkSession to sometimes not do
    /// them would put a branch through the most load-bearing code in the game.
    /// </summary>
    public sealed class DiagnosisSession
    {
        public ActiveCar Car { get; private set; }
        public DiagnosisAction Action { get; private set; }
        public MinigameBase Minigame { get; private set; }

        public DiagnosisSession(ActiveCar car, DiagnosisAction action, MinigameBase minigame)
        {
            Car = car;
            Action = action;
            Minigame = minigame;
        }
    }
}
