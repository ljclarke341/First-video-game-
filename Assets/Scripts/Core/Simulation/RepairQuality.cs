using System;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Simulation
{
    /// <summary>How a finished piece of work scored, and how the customer felt about it.</summary>
    public struct QualityReport
    {
        /// <summary>
        /// The workmanship score, 0..1. Nothing else here is anything but a view of this.
        ///
        /// DOUBLE, not float, for the same reason the tip multiplier is. A score of 0.475 held as
        /// a float is 0.4749999940395355, so the percentage shown rounded to 47 here and 48 in the
        /// web build, whose numbers are all doubles. The formula is untouched; only its precision.
        /// </summary>
        public double Score;

        /// <summary>The same score as whole percent, which is what the UI shows.</summary>
        public int Percent;

        /// <summary>1 to 5, for the star row.</summary>
        public int Stars;

        /// <summary>Share of rounds that were dead-on.</summary>
        public double Accuracy;

        /// <summary>
        /// How well the rounds were executed, 0..1, giving partial credit for work that was right
        /// without being flawless. This is what the score is actually built from; Accuracy above is
        /// kept because the UI and the stats screen both report "perfect rounds" to the player.
        /// </summary>
        public double Execution;

        /// <summary>How close the job came to the fewest rounds it could possibly have taken.</summary>
        public double Efficiency;

        /// <summary>Share of rounds that broke something.</summary>
        public double DamageRate;

        public int RoundsPlayed;
        public int PerfectRounds;
        public int DamagedRounds;

        /// <summary>What was fitted, so the finished-job readout can say so.</summary>
        public Parts.PartGrade PartGrade;
        public bool PartFitted;

        /// <summary>
        /// How happy this leaves the customer, 0..1 - the score judged against what THEY expected,
        /// not against perfection. A collector and a bloke in a hurry do not want the same thing.
        /// </summary>
        public double Satisfaction;

        /// <summary>
        /// What this workmanship is worth as a multiple of the job's listed price.
        ///
        /// NOT APPLIED TO THE WALLET YET, deliberately. Paying on quality replaces the existing
        /// flat flawless bonus, which moves average income, and that is a balance change that
        /// deserves to be measured on purpose rather than smuggled in alongside a scoring system.
        /// The simulation computes and reports it; wiring it to Wallet.Earn is one line when the
        /// probe says what it costs.
        /// </summary>
        public double PayMultiplier;
    }

    /// <summary>
    /// Scores finished work.
    ///
    /// This is a PURE function over state the simulation already tracked - rounds played, perfect
    /// rounds, damaged rounds, and how much patience was left. It adds no fields to anything and
    /// changes no behaviour, which is why it could be built first and with no risk: if every line
    /// of it were deleted the game would play exactly as it does now.
    /// </summary>
    public static class RepairQuality
    {
        /// <summary>
        /// What a round is worth towards the workmanship score, by how it went.
        ///
        /// Perfect is 1 and a miss is 0, which was always true. What is new is that the two
        /// outcomes in between are worth something. Before this, the score counted perfect rounds
        /// only: a player who hit every window but never dead-centre scored ZERO on workmanship,
        /// identically to one who missed every round, and the only thing separating them was that
        /// missing takes more rounds. That is what made the measured distribution bimodal - there
        /// was no arithmetic path to a middling score.
        ///
        /// A Good round is deliberately worth less than a Perfect one by a wide margin, so aiming
        /// for dead-centre is still the thing that distinguishes a skilled player.
        /// </summary>
        public const double GoodRoundCredit = 0.55d;

        public const double WeakRoundCredit = 0.22d;

        /// <summary>How much of the score is workmanship rather than pace.</summary>
        private const double AccuracyWeight = 0.55d;
        private const double EfficiencyWeight = 0.45d;

        /// <summary>Breaking something costs more than merely missing, so it is priced separately.</summary>
        private const double DamagePenalty = 0.7d;

        /// <summary>
        /// Where the pay curve starts, and how steeply it climbs.
        ///
        /// Named rather than inlined because both builds have to agree on them and the parity
        /// suite pins them by name. The base is set from the measured median score, not chosen.
        /// </summary>
        public const double QualityBase = 0.53d;
        public const double QualitySlope = 0.5d;

        /// <summary>Meeting a customer's expectation exactly lands here, not at 100%.</summary>
        public const double SatisfactionAtExpectation = 0.8d;

        /// <summary>How steeply satisfaction falls away BELOW what the customer expected.</summary>
        public const double ShortfallSlope = 0.8d;

        /// <summary>
        /// The fewest rounds this job could ever have taken: every round perfect, no misses.
        /// Used as the yardstick for efficiency, so a job is measured against its own size rather
        /// than against a fixed number of rounds that would punish big jobs.
        /// </summary>
        public static int MinimumRounds(RepairJob job)
        {
            if (job == null) return 1;

            int rounds = (int)Math.Ceiling(job.WorkAmount / GameBalance.PerfectProgress);
            return rounds < 1 ? 1 : rounds;
        }

        /// <summary>
        /// Workmanship across a job's rounds, 0..1, with partial credit for the middle outcomes.
        ///
        /// A save written before good and weak rounds were counted reports both as zero, so this
        /// falls back to the share of perfect rounds - which is exactly how that save was scored
        /// when it was written. Old saves therefore keep their scores instead of being re-judged.
        /// </summary>
        public static double ExecutionOf(RepairJob job)
        {
            if (job == null || job.RoundsPlayed <= 0) return 0d;

            double credited = job.PerfectRounds
                + job.GoodRounds * GoodRoundCredit
                + job.WeakRounds * WeakRoundCredit;

            return Clamp01(credited / job.RoundsPlayed);
        }

        /// <summary>Scores one finished job for a customer who expects nothing in particular.</summary>
        public static QualityReport ForJob(RepairJob job, CustomerMood mood)
        {
            return ForJob(job, mood, null);
        }

        /// <summary>
        /// Scores one finished job against what this customer expected to be fitted.
        ///
        /// Ordinary customers have no expectation at all, so the shortfall term is zero for them
        /// and the score is exactly what it has always been. A customer who turned up asking for
        /// performance parts and got budget ones notices.
        /// </summary>
        public static QualityReport ForJob(RepairJob job, CustomerMood mood, Parts.PartGrade? expectedGrade)
        {
            QualityReport report = new QualityReport();
            if (job == null) return report;

            report.RoundsPlayed = job.RoundsPlayed;
            report.PerfectRounds = job.PerfectRounds;
            report.DamagedRounds = job.DamagedRounds;

            if (job.RoundsPlayed <= 0)
            {
                // Nothing was played - a mechanic's instant work, or a restored save. Treat it as
                // competent rather than as a zero, which would read as a punishment for automating.
                report.Score = Clamp01(
                    0.6d + (job.PartFitted ? job.FittedGrade.QualityModifier() : 0d)
                        - ShortfallPenalty(job, expectedGrade));
                report.PartGrade = job.FittedGrade;
                report.PartFitted = job.PartFitted;
                return Finish(report, mood);
            }

            report.Accuracy = job.PerfectRounds / (double)job.RoundsPlayed;
            report.Execution = ExecutionOf(job);
            report.DamageRate = job.DamagedRounds / (double)job.RoundsPlayed;
            report.Efficiency = Clamp01(MinimumRounds(job) / (double)job.RoundsPlayed);

            double score = report.Execution * AccuracyWeight + report.Efficiency * EfficiencyWeight;
            score -= report.DamageRate * DamagePenalty;

            // What went on the car counts, but only a little. A good part must not rescue sloppy
            // work and a cheap one must not ruin careful work - the mini-game is still the repair.
            if (job.PartFitted) score += job.FittedGrade.QualityModifier();

            // And for a customer who asked for something better, falling short of it shows.
            score -= ShortfallPenalty(job, expectedGrade);

            report.Score = Clamp01(score);
            report.PartGrade = job.FittedGrade;
            report.PartFitted = job.PartFitted;

            return Finish(report, mood);
        }

        /// <summary>
        /// Scores a whole car by rolling up its jobs, weighted by how much work each one was.
        /// A five-round engine rebuild should count for more than a two-round paint touch-up.
        /// </summary>
        public static QualityReport ForCar(ActiveCar car)
        {
            QualityReport report = new QualityReport();
            if (car == null || car.Jobs.Count == 0) return report;

            double weightedScore = 0d;
            double totalWeight = 0d;

            for (int i = 0; i < car.Jobs.Count; i++)
            {
                RepairJob job = car.Jobs[i];
                QualityReport jobReport = ForJob(job, car.Mood, car.ExpectedPartGrade);

                double weight = job.WorkAmount;
                weightedScore += jobReport.Score * weight;
                totalWeight += weight;

                report.RoundsPlayed += jobReport.RoundsPlayed;
                report.PerfectRounds += jobReport.PerfectRounds;
                report.DamagedRounds += jobReport.DamagedRounds;
            }

            report.Score = totalWeight <= 0d ? 0d : Clamp01(weightedScore / totalWeight);

            if (report.RoundsPlayed > 0)
            {
                report.Accuracy = report.PerfectRounds / (double)report.RoundsPlayed;
                report.DamageRate = report.DamagedRounds / (double)report.RoundsPlayed;

                // Rolled up across the car's jobs the same way the score is: by work, not by job
                // count, so a long engine job counts for more than a quick touch-up.
                double credited = 0d, rounds = 0d;
                for (int i = 0; i < car.Jobs.Count; i++)
                {
                    RepairJob job = car.Jobs[i];
                    if (job.RoundsPlayed <= 0) continue;
                    credited += ExecutionOf(job) * job.RoundsPlayed;
                    rounds += job.RoundsPlayed;
                }
                report.Execution = rounds <= 0d ? 0d : Clamp01(credited / rounds);
            }

            return Finish(report, car.Mood);
        }

        /// <summary>
        /// What this customer considers an acceptable job, 0..1.
        ///
        /// This is the whole point of customer types being more than a tip multiplier: the SAME
        /// piece of work can delight one customer and disappoint another.
        /// </summary>
        public static double ExpectationOf(CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Relaxed: return 0.42d;      // no rush, no fuss
                case CustomerMood.Ordinary: return 0.55d;
                case CustomerMood.Impatient: return 0.48d;    // wants it done, not done beautifully
                case CustomerMood.BigTipper: return 0.62d;
                case CustomerMood.Vip: return 0.78d;          // expects the best, and notices
                default: return 0.55d;
            }
        }

        /// <summary>Fills in the parts of the report that are the same whatever was scored.</summary>
        /// <summary>
        /// How far below the customer's expectation this part fell, in quality.
        ///
        /// Only ever a penalty, never a reward: fitting something dearer than was asked for is
        /// already paid for by PartGrade.QualityModifier, and paying twice for it would make the
        /// top grade an obvious auto-buy rather than a decision.
        /// </summary>
        private static double ShortfallPenalty(RepairJob job, Parts.PartGrade? expectedGrade)
        {
            if (job == null || !job.PartFitted || !expectedGrade.HasValue) return 0d;

            int stepsBelow = (int)expectedGrade.Value - (int)job.FittedGrade;
            if (stepsBelow <= 0) return 0d;

            return stepsBelow * GameBalance.GradeShortfallPenalty;
        }

        private static QualityReport Finish(QualityReport report, CustomerMood mood)
        {
            report.Percent = (int)Math.Floor(report.Score * 100d + 0.5d);

            // 1 star for finishing at all, 5 for near-perfect work.
            report.Stars = MathUtil.ClampInt((int)(report.Score * 5d) + 1, 1, 5);

            // Satisfaction, with the top half of the curve given room to breathe.
            //
            // This used to be one straight line of slope 0.8 through the expectation point, which
            // reached 1.0 at a score only 0.25 above expectation and was clamped flat from there
            // on. For an ordinary customer that meant EVERY score from 0.80 upwards produced
            // identical satisfaction, so good work, excellent work and flawless work were the same
            // event to standing, reputation and the customer mix downstream.
            //
            // Below expectation the line is untouched, deliberately: falling short should cost
            // exactly what it always cost. Above it, the remaining headroom is spread across the
            // remaining score range, so a perfect job reaches 1.0 and nothing short of it does.
            double expectation = ExpectationOf(mood);

            if (report.Score <= expectation)
            {
                report.Satisfaction = Clamp01(
                    SatisfactionAtExpectation + (report.Score - expectation) * ShortfallSlope);
            }
            else
            {
                double headroom = 1d - expectation;
                double above = headroom <= 0d ? 1d : (report.Score - expectation) / headroom;
                report.Satisfaction = Clamp01(
                    SatisfactionAtExpectation + above * (1d - SatisfactionAtExpectation));
            }

            // Centred on what players ACTUALLY score, not on the midpoint of the scale.
            //
            // The base was 0.75, which pays 1.0x at a score of 0.50 - and measured across 5,330
            // real jobs, the median score is 0.90, so typical play was collecting 1.20x and
            // opening income had risen from $504 to $550 a minute. 0.55 puts the median back at
            // 1.00x. The SLOPE is untouched at 0.50, so quality is rewarded exactly as steeply as
            // before; only the point the curve passes through 1.0 has moved.
            //
            //   score 0.00 -> 0.55x     score 0.50 -> 0.80x
            //   score 0.90 -> 1.00x     score 1.00 -> 1.05x
            report.PayMultiplier = QualityBase + report.Score * QualitySlope;

            return report;
        }

        /// <summary>Clamp to 0..1 in double. MathUtil's is float, which is what caused the drift.</summary>
        private static double Clamp01(double value)
        {
            if (value < 0d) return 0d;
            return value > 1d ? 1d : value;
        }

        /// <summary>The star row as text, for places that cannot draw one.</summary>
        public static string StarsText(int stars)
        {
            stars = MathUtil.ClampInt(stars, 0, 5);
            return new string('★', stars) + new string('☆', 5 - stars);
        }
    }
}
