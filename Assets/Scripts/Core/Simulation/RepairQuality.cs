using System;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Simulation
{
    /// <summary>How a finished piece of work scored, and how the customer felt about it.</summary>
    public struct QualityReport
    {
        /// <summary>The workmanship score, 0..1. Nothing else here is anything but a view of this.</summary>
        public float Score;

        /// <summary>The same score as whole percent, which is what the UI shows.</summary>
        public int Percent;

        /// <summary>1 to 5, for the star row.</summary>
        public int Stars;

        /// <summary>Share of rounds that were dead-on.</summary>
        public float Accuracy;

        /// <summary>How close the job came to the fewest rounds it could possibly have taken.</summary>
        public float Efficiency;

        /// <summary>Share of rounds that broke something.</summary>
        public float DamageRate;

        public int RoundsPlayed;
        public int PerfectRounds;
        public int DamagedRounds;

        /// <summary>
        /// How happy this leaves the customer, 0..1 - the score judged against what THEY expected,
        /// not against perfection. A collector and a bloke in a hurry do not want the same thing.
        /// </summary>
        public float Satisfaction;

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
        /// <summary>How much of the score is workmanship rather than pace.</summary>
        private const float AccuracyWeight = 0.55f;
        private const float EfficiencyWeight = 0.45f;

        /// <summary>Breaking something costs more than merely missing, so it is priced separately.</summary>
        private const float DamagePenalty = 0.7f;

        /// <summary>Meeting a customer's expectation exactly lands here, not at 100%.</summary>
        private const float SatisfactionAtExpectation = 0.8f;

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

        /// <summary>Scores one finished job.</summary>
        public static QualityReport ForJob(RepairJob job, CustomerMood mood)
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
                report.Score = 0.6f;
                return Finish(report, mood);
            }

            report.Accuracy = job.PerfectRounds / (float)job.RoundsPlayed;
            report.DamageRate = job.DamagedRounds / (float)job.RoundsPlayed;
            report.Efficiency = MathUtil.Clamp01(MinimumRounds(job) / (float)job.RoundsPlayed);

            float score = report.Accuracy * AccuracyWeight + report.Efficiency * EfficiencyWeight;
            score -= report.DamageRate * DamagePenalty;

            report.Score = MathUtil.Clamp01(score);
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

            float weightedScore = 0f;
            float totalWeight = 0f;

            for (int i = 0; i < car.Jobs.Count; i++)
            {
                RepairJob job = car.Jobs[i];
                QualityReport jobReport = ForJob(job, car.Mood);

                float weight = job.WorkAmount;
                weightedScore += jobReport.Score * weight;
                totalWeight += weight;

                report.RoundsPlayed += jobReport.RoundsPlayed;
                report.PerfectRounds += jobReport.PerfectRounds;
                report.DamagedRounds += jobReport.DamagedRounds;
            }

            report.Score = totalWeight <= 0f ? 0f : MathUtil.Clamp01(weightedScore / totalWeight);

            if (report.RoundsPlayed > 0)
            {
                report.Accuracy = report.PerfectRounds / (float)report.RoundsPlayed;
                report.DamageRate = report.DamagedRounds / (float)report.RoundsPlayed;
            }

            return Finish(report, car.Mood);
        }

        /// <summary>
        /// What this customer considers an acceptable job, 0..1.
        ///
        /// This is the whole point of customer types being more than a tip multiplier: the SAME
        /// piece of work can delight one customer and disappoint another.
        /// </summary>
        public static float ExpectationOf(CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Relaxed: return 0.42f;      // no rush, no fuss
                case CustomerMood.Ordinary: return 0.55f;
                case CustomerMood.Impatient: return 0.48f;    // wants it done, not done beautifully
                case CustomerMood.BigTipper: return 0.62f;
                case CustomerMood.Vip: return 0.78f;          // expects the best, and notices
                default: return 0.55f;
            }
        }

        /// <summary>Fills in the parts of the report that are the same whatever was scored.</summary>
        private static QualityReport Finish(QualityReport report, CustomerMood mood)
        {
            report.Percent = (int)(report.Score * 100f + 0.5f);

            // 1 star for finishing at all, 5 for near-perfect work.
            report.Stars = MathUtil.ClampInt((int)(report.Score * 5f) + 1, 1, 5);

            float expectation = ExpectationOf(mood);
            report.Satisfaction = MathUtil.Clamp01(
                SatisfactionAtExpectation + (report.Score - expectation) * 0.8f);

            // Centred so that typical work pays what it always did: only genuinely good or
            // genuinely poor work moves the number.
            report.PayMultiplier = 0.75d + report.Score * 0.5d;

            return report;
        }

        /// <summary>The star row as text, for places that cannot draw one.</summary>
        public static string StarsText(int stars)
        {
            stars = MathUtil.ClampInt(stars, 0, 5);
            return new string('★', stars) + new string('☆', 5 - stars);
        }
    }
}
