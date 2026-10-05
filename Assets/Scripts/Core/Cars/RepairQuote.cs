using System.Collections.Generic;
using GarageTycoon.Core.Util;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.Core.Cars
{
    /// <summary>Which bundle of work the player put in front of the customer.</summary>
    public enum QuoteOption
    {
        /// <summary>Everything the car needs. Most money, most time on the clock.</summary>
        Everything = 0,

        /// <summary>Only what the car genuinely has to have. Less money, out of the bay sooner.</summary>
        EssentialOnly = 1,

        /// <summary>The player picked line by line.</summary>
        Custom = 2
    }

    /// <summary>One line on the quote: a repair, what it is for, and what it costs.</summary>
    public struct QuoteLine
    {
        public int JobIndex;
        public JobType Type;
        public VehicleSystem System;

        /// <summary>The reading that justifies the line. "Front brakes at 28%".</summary>
        public int ConditionPercent;

        public double Price;

        /// <summary>True when the car really should not leave without this doing.</summary>
        public bool IsEssential;
    }

    /// <summary>
    /// The bill the player puts in front of the customer.
    ///
    /// This is where the game stops being "do all the work that appeared" and starts being a
    /// decision. Quoting for everything pays more but keeps the car in the bay longer, against a
    /// patience clock that does not care how much you are earning. Quoting for the essentials
    /// alone gets the customer out quickly for less. Neither is right; it depends on the car, the
    /// queue outside, and who is waiting.
    /// </summary>
    public sealed class Quote
    {
        /// <summary>At or below this a system is bad enough that the repair is not optional.</summary>
        public const float EssentialThreshold = 0.34f;

        private readonly List<QuoteLine> _lines = new List<QuoteLine>();

        public IReadOnlyList<QuoteLine> Lines { get { return _lines; } }

        /// <summary>What the customer pays for the lot.</summary>
        public double EverythingPrice { get; private set; }

        /// <summary>What they pay for the must-dos only.</summary>
        public double EssentialPrice { get; private set; }

        public int EssentialCount { get; private set; }

        public int LineCount { get { return _lines.Count; } }

        private Quote() { }

        /// <summary>Builds the quote for a car from its jobs and its condition reading.</summary>
        public static Quote For(ActiveCar car)
        {
            Quote quote = new Quote();
            if (car == null) return quote;

            for (int i = 0; i < car.Jobs.Count; i++)
            {
                RepairJob job = car.Jobs[i];

                // Finished work is off the quote: it has already been done and paid for.
                if (job.IsComplete) continue;

                VehicleSystem system = CarCondition.SystemFor(job.Type);

                QuoteLine line = new QuoteLine();
                line.JobIndex = i;
                line.Type = job.Type;
                line.System = system;
                line.ConditionPercent = car.Condition.Percent(system);
                line.Price = job.Payout;
                line.IsEssential = IsEssential(job.Type, car.Condition.Get(system));

                quote._lines.Add(line);

                quote.EverythingPrice += line.Price;
                if (line.IsEssential)
                {
                    quote.EssentialPrice += line.Price;
                    quote.EssentialCount++;
                }
            }

            // A quote whose "essentials only" option is empty is not a choice, it is a dead end.
            // If nothing crossed the line, the worst item becomes the essential one.
            if (quote.EssentialCount == 0 && quote._lines.Count > 0)
            {
                int worst = 0;
                for (int i = 1; i < quote._lines.Count; i++)
                {
                    if (quote._lines[i].ConditionPercent < quote._lines[worst].ConditionPercent) worst = i;
                }

                QuoteLine promoted = quote._lines[worst];
                promoted.IsEssential = true;
                quote._lines[worst] = promoted;

                quote.EssentialPrice = promoted.Price;
                quote.EssentialCount = 1;
            }

            return quote;
        }

        /// <summary>
        /// Whether a repair is one the car should not leave without.
        ///
        /// Brakes get a lower bar than everything else, because brakes are brakes - a garage that
        /// sends a car out with soft ones has a bigger problem than a thin margin.
        /// </summary>
        public static bool IsEssential(JobType jobType, float systemHealth)
        {
            if (jobType == JobType.Brakes) return systemHealth <= CarCondition.FaultThreshold;

            // Cosmetic work is never essential however bad it looks.
            if (jobType == JobType.Paint) return false;

            return systemHealth <= EssentialThreshold;
        }

        /// <summary>Applies the customer's answer to the car, marking each job accepted or declined.</summary>
        public void Apply(ActiveCar car, QuoteOption option)
        {
            if (car == null) return;

            for (int i = 0; i < _lines.Count; i++)
            {
                QuoteLine line = _lines[i];
                bool accepted = option == QuoteOption.Everything || line.IsEssential;

                car.Jobs[line.JobIndex].SetAccepted(accepted);
            }
        }

        /// <summary>Applies a hand-picked set of job indices; everything else on the quote is declined.</summary>
        public void ApplyCustom(ActiveCar car, ICollection<int> acceptedJobIndices)
        {
            if (car == null) return;

            for (int i = 0; i < _lines.Count; i++)
            {
                int jobIndex = _lines[i].JobIndex;
                bool accepted = acceptedJobIndices != null && acceptedJobIndices.Contains(jobIndex);

                car.Jobs[jobIndex].SetAccepted(accepted);
            }
        }

        /// <summary>The price of a given answer.</summary>
        public double PriceOf(QuoteOption option)
        {
            return option == QuoteOption.Everything ? EverythingPrice : EssentialPrice;
        }

        // ------------------------------------------------------------------
        // What the customer makes of it
        // ------------------------------------------------------------------

        /// <summary>
        /// The quote this customer was hoping to hear.
        ///
        /// These map onto the customer types the game already has rather than adding new ones.
        /// New personalities (budget, enthusiast, taxi driver, collector) would change the spawn
        /// pool, and therefore patience and payout distribution, and therefore the measured
        /// economy - so they are a deliberate balance change for later, not a side effect of
        /// adding quotes.
        /// </summary>
        public static QuoteOption PreferenceOf(CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Vip: return QuoteOption.Everything;        // expects it done properly
                case CustomerMood.BigTipper: return QuoteOption.Everything;  // money is not the issue
                case CustomerMood.Relaxed: return QuoteOption.Everything;    // happy to wait for it
                case CustomerMood.Impatient: return QuoteOption.EssentialOnly;
                default: return QuoteOption.EssentialOnly;                   // most people want the bill small
            }
        }

        /// <summary>
        /// How much the choice of quote shifts satisfaction, before any work is even done.
        ///
        /// Kept small on purpose. This should make the player think about who they are talking to,
        /// not punish them for guessing wrong - the repair itself still matters far more.
        /// </summary>
        public static float SatisfactionModifier(CustomerMood mood, QuoteOption chosen)
        {
            QuoteOption preferred = PreferenceOf(mood);

            if (chosen == QuoteOption.Custom) return 0f;     // no strong feelings either way
            if (chosen == preferred) return 0.08f;

            // A VIP told "we only did the bare minimum" takes it worse than most.
            return mood == CustomerMood.Vip ? -0.12f : -0.06f;
        }

        /// <summary>Satisfaction for finished work, adjusted for how it was quoted.</summary>
        public static float AdjustSatisfaction(float satisfaction, CustomerMood mood, QuoteOption chosen)
        {
            return MathUtil.Clamp01(satisfaction + SatisfactionModifier(mood, chosen));
        }
    }
}
