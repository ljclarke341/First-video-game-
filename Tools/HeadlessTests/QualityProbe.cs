using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.HeadlessTests.Tests;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Phase C.3: a controlled bench for the quality chain.
    ///
    ///     dotnet run --project Tools/HeadlessTests -- quality
    ///
    /// Everything else that measures quality plays the game and reports what came out. This does
    /// the opposite: it DICTATES how a job was executed, round by round, and walks the result all
    /// the way down the chain - score, pay multiplier, satisfaction, standing, rarity bias - so the
    /// exact stage that flattens the difference between good work and perfect work is visible.
    ///
    /// Nothing here is random and nothing here plays a mini-game.
    /// </summary>
    public static class QualityProbe
    {
        /// <summary>
        /// One way of working: the outcomes a pair of hands produces, cycled until the job is done.
        /// Named for what a player would call it, worst first.
        /// </summary>
        private struct Band
        {
            public string Name;
            public MinigameOutcome[] Cycle;

            public Band(string name, params MinigameOutcome[] cycle) { Name = name; Cycle = cycle; }
        }

        private static readonly Band[] Bands =
        {
            new Band("terrible",      MinigameOutcome.Damage, MinigameOutcome.Miss, MinigameOutcome.Damage, MinigameOutcome.Weak),
            new Band("poor",          MinigameOutcome.Miss, MinigameOutcome.Weak, MinigameOutcome.Miss, MinigameOutcome.Good),
            new Band("below average", MinigameOutcome.Weak, MinigameOutcome.Weak, MinigameOutcome.Good),
            new Band("average",       MinigameOutcome.Good, MinigameOutcome.Weak, MinigameOutcome.Good),
            new Band("good",          MinigameOutcome.Good),
            new Band("very good",     MinigameOutcome.Good, MinigameOutcome.Perfect),
            new Band("excellent",     MinigameOutcome.Perfect, MinigameOutcome.Perfect, MinigameOutcome.Good),
            new Band("perfect",       MinigameOutcome.Perfect),
        };

        /// <summary>One job context to score the bands against.</summary>
        private struct Context
        {
            public string Name;
            public CustomerMood Mood;
            public PartGrade Fitted;
            public PartGrade? Expected;
            public double QualityWeight;
            public double ReputationWeight;

            public Context(string name, CustomerMood mood, PartGrade fitted, PartGrade? expected,
                double qualityWeight, double reputationWeight)
            {
                Name = name; Mood = mood; Fitted = fitted; Expected = expected;
                QualityWeight = qualityWeight; ReputationWeight = reputationWeight;
            }
        }

        private static readonly Context[] Contexts =
        {
            // Dials taken from SpecialJobCatalog so the bench cannot drift from the shipped jobs.
            new Context("ORDINARY (standard part, no expectation)",
                CustomerMood.Ordinary, PartGrade.Standard, null, 1d, 1d),
            new Context("PERFORMANCE (standard part fitted, performance expected)",
                CustomerMood.Ordinary, PartGrade.Standard, PartGrade.Performance, 1.8d, 1d),
            new Context("PERFORMANCE (performance part fitted, as asked)",
                CustomerMood.Ordinary, PartGrade.Performance, PartGrade.Performance, 1.8d, 1d),
            new Context("COLLECTOR (vip mood, standard part)",
                CustomerMood.Vip, PartGrade.Standard, null, 1.3d, 6d),
        };


        /// <summary>
        /// Phase C.3 section 14: the distribution, split by WHO did the work and how good they are.
        ///
        ///     dotnet run --project Tools/HeadlessTests -- quality dist
        ///
        /// A single distribution for the whole garage hides the thing that matters. If the curve is
        /// healthy, a better player must land further up it than a worse one, and a trained mechanic
        /// further up than an untrained one. If every actor produces the same shape, the curve is
        /// still flat and only the labels have changed.
        /// </summary>
        public static void Distribution(int seeds)
        {
            Console.WriteLine("=== WHO PRODUCES WHAT QUALITY ===");
            Console.WriteLine();
            Console.WriteLine("perfect% is scores of exactly 1.000; 'top band' is 0.90 and up.");
            Console.WriteLine();
            Console.WriteLine("actor                      jobs  perfect%  top band%   p10    p25    p50    p75    p90   mean");

            // The player alone, at three very different standards of play.
            Actor("player, weak (0.55)",    seeds, 1, 0, false, 0.55f, false);
            Actor("player, decent (0.75)",  seeds, 1, 0, false, 0.75f, false);
            Actor("player, strong (0.85)",  seeds, 1, 0, false, 0.85f, false);
            Actor("player, expert (0.95)",  seeds, 1, 0, false, 0.95f, false);

            Console.WriteLine();

            // Mechanics working alone, so nothing the player did is mixed into the scores.
            Actor("mechanics, untrained",   seeds, 4, 4, false, 0.85f, true);
            Actor("mechanics, trained",     seeds, 4, 4, true,  0.85f, true);

            Console.WriteLine();
        }

        private static void Actor(string name, int seeds, int bays, int crew, bool trained,
            float skill, bool handsOff)
        {
            List<double> scores = new List<double>();

            for (int seed = 0; seed < seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(95000 + seed);

                GameplayHarness.GrantUpgrade(simulation, "workshop_rates", crew == 0 ? 0 : (crew >= 4 ? 10 : 5));
                if (bays > 1) GameplayHarness.GrantUpgrade(simulation, "workshop_bays", bays - 1);
                if (crew > 0) GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", crew);
                if (trained && crew > 0)
                {
                    GameplayHarness.GrantUpgrade(simulation, "auto_skill", 6);
                    GameplayHarness.GrantUpgrade(simulation, "auto_speed", 6);
                }
                if (crew > 0) simulation.Wallet.Earn(crew >= 4 ? 1600000d : 120000d);

                simulation.JobCompleted += (car, job, cash) =>
                {
                    scores.Add(RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade).Score);
                };

                GameplayHarness.Play(simulation, 900f, skill,
                    bayPicker: handsOff ? (Func<GarageSimulation, int>)(sim => -1) : null);
            }

            scores.Sort();
            Report(name, scores);
        }

        private static void Report(string name, List<double> scores)
        {
            int perfect = 0, topBand = 0;
            double sum = 0d;
            for (int i = 0; i < scores.Count; i++)
            {
                sum += scores[i];
                if (scores[i] >= 0.9995d) perfect++;
                if (scores[i] >= 0.9d) topBand++;
            }

            Console.WriteLine(
                name.PadRight(26)
                + scores.Count.ToString().PadLeft(7)
                + ((scores.Count == 0 ? 0d : perfect * 100d / scores.Count).ToString("0.0") + "%").PadLeft(10)
                + ((scores.Count == 0 ? 0d : topBand * 100d / scores.Count).ToString("0.0") + "%").PadLeft(11)
                + Pct(scores, 0.10d).ToString("0.000").PadLeft(7)
                + Pct(scores, 0.25d).ToString("0.000").PadLeft(7)
                + Pct(scores, 0.50d).ToString("0.000").PadLeft(7)
                + Pct(scores, 0.75d).ToString("0.000").PadLeft(7)
                + Pct(scores, 0.90d).ToString("0.000").PadLeft(7)
                + (scores.Count == 0 ? 0d : sum / scores.Count).ToString("0.000").PadLeft(7));
        }

        private static double Pct(List<double> sorted, double fraction)
        {
            if (sorted.Count == 0) return 0d;
            int index = (int)(fraction * (sorted.Count - 1));
            return sorted[index < 0 ? 0 : index];
        }

        public static void Run()
        {
            Console.WriteLine("=== THE QUALITY CHAIN, UNDER CONTROL ===");
            Console.WriteLine();
            Console.WriteLine("Each row is a job worked to completion in a dictated way. No randomness, no mini-games.");
            Console.WriteLine("'standing' is the move one such car makes; 'bias' is what that move does to the spawner.");
            Console.WriteLine();

            foreach (Context context in Contexts)
            {
                Console.WriteLine("-- " + context.Name + " --");
                Console.WriteLine("band            rounds  perf good weak dmg   exec    eff   raw score  score  pay x  payout x  satisf  standing      bias");

                foreach (Band band in Bands)
                {
                    Row(band, context);
                }

                Console.WriteLine();
            }

            Summary();
        }

        private static void Row(Band band, Context context)
        {
            RepairJob job = Play(band, context.Fitted);
            QualityReport report = RepairQuality.ForJob(job, context.Mood, context.Expected);

            // The raw score before the 0..1 clamp, recomputed the same way RepairQuality does, so
            // the amount of headroom being thrown away at the top is visible rather than inferred.
            double raw = RawScore(job, context.Expected);

            // What the wallet actually sees: the job's weight decides how much of the quality
            // multiplier reaches the payout.
            double payout = 1d + (report.PayMultiplier - 1d) * context.QualityWeight;

            double move = (report.Satisfaction - GameBalance.NeutralSatisfaction)
                          * GameBalance.StandingStep * context.ReputationWeight;

            Console.WriteLine(
                band.Name.PadRight(15)
                + job.RoundsPlayed.ToString().PadLeft(6)
                + job.PerfectRounds.ToString().PadLeft(6)
                + job.GoodRounds.ToString().PadLeft(5)
                + job.WeakRounds.ToString().PadLeft(5)
                + job.DamagedRounds.ToString().PadLeft(4)
                + report.Execution.ToString("0.00").PadLeft(7)
                + report.Efficiency.ToString("0.00").PadLeft(7)
                + raw.ToString("0.000").PadLeft(12)
                + report.Score.ToString("0.000").PadLeft(7)
                + report.PayMultiplier.ToString("0.000").PadLeft(7)
                + payout.ToString("0.000").PadLeft(10)
                + report.Satisfaction.ToString("0.000").PadLeft(8)
                + move.ToString("+0.0000;-0.0000;0.0000").PadLeft(10)
                + (move * GameBalance.StandingBiasRange).ToString("+0.0000;-0.0000;0.0000").PadLeft(10));
        }

        /// <summary>Works a standard-sized job to completion with the band's outcome cycle.</summary>
        private static RepairJob Play(Band band, PartGrade fitted)
        {
            RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1f, 100d, 0.5f);

            // A hard cap so a band that never finishes a job cannot spin forever. Terrible work
            // genuinely can go backwards, so this has to be generous.
            for (int i = 0; i < 400 && !job.IsComplete; i++)
            {
                job.ApplyResult(MinigameResult.FromOutcome(band.Cycle[i % band.Cycle.Length], null));
            }

            job.RecordPart(fitted, 0d, 0d);
            return job;
        }

        /// <summary>
        /// RepairQuality's own formula, minus the final clamp. Kept deliberately as a copy: if it
        /// drifts from the real one the bench stops matching the game, which the parity suite and
        /// the quality tests will both catch.
        /// </summary>
        private static double RawScore(RepairJob job, PartGrade? expected)
        {
            if (job.RoundsPlayed <= 0) return 0d;

            double execution = RepairQuality.ExecutionOf(job);
            double damage = job.DamagedRounds / (double)job.RoundsPlayed;
            double efficiency = RepairQuality.MinimumRounds(job) / (double)job.RoundsPlayed;
            if (efficiency > 1d) efficiency = 1d;

            double score = execution * 0.55d + efficiency * 0.45d - damage * 0.7d;
            if (job.PartFitted) score += job.FittedGrade.QualityModifier();

            if (expected.HasValue && job.PartFitted)
            {
                int stepsBelow = (int)expected.Value - (int)job.FittedGrade;
                if (stepsBelow > 0) score -= stepsBelow * GameBalance.GradeShortfallPenalty;
            }

            return score;
        }

        /// <summary>
        /// Walks the satisfaction and pay curves directly, so the flat stretches are impossible to
        /// miss. These are the two mappings every quality result has to pass through.
        /// </summary>
        private static void Summary()
        {
            Console.WriteLine("-- the curves themselves, walked score by score --");
            Console.WriteLine();
            Console.WriteLine("score   pay x   satisf(ordinary)  satisf(vip)  standing(ord)  standing(collector)");

            for (double score = 0d; score <= 1.0001d; score += 0.05d)
            {
                double pay = RepairQuality.QualityBase + score * RepairQuality.QualitySlope;
                double satOrdinary = Sat(score, RepairQuality.ExpectationOf(CustomerMood.Ordinary));
                double satVip = Sat(score, RepairQuality.ExpectationOf(CustomerMood.Vip));

                double moveOrd = (satOrdinary - GameBalance.NeutralSatisfaction) * GameBalance.StandingStep;
                double moveCol = (satVip - GameBalance.NeutralSatisfaction) * GameBalance.StandingStep * 6d;

                Console.WriteLine(
                    score.ToString("0.00").PadLeft(5)
                    + pay.ToString("0.000").PadLeft(8)
                    + satOrdinary.ToString("0.000").PadLeft(18)
                    + satVip.ToString("0.000").PadLeft(13)
                    + moveOrd.ToString("+0.0000;-0.0000;0.0000").PadLeft(15)
                    + moveCol.ToString("+0.0000;-0.0000;0.0000").PadLeft(21));
            }

            Console.WriteLine();
        }

        /// <summary>The shipped satisfaction mapping, in one place so the walk cannot lie about it.</summary>
        private static double Sat(double score, double expectation)
        {
            double value;
            if (score <= expectation)
            {
                value = RepairQuality.SatisfactionAtExpectation
                    + (score - expectation) * RepairQuality.ShortfallSlope;
            }
            else
            {
                double headroom = 1d - expectation;
                double above = headroom <= 0d ? 1d : (score - expectation) / headroom;
                value = RepairQuality.SatisfactionAtExpectation
                    + above * (1d - RepairQuality.SatisfactionAtExpectation);
            }
            return value < 0d ? 0d : (value > 1d ? 1d : value);
        }
    }
}
