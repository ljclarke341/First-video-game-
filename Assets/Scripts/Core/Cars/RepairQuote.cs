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
        Custom = 2,

        /// <summary>
        /// The garage turned the work down. The customer takes the car away unrepaired.
        ///
        /// This exists because a job you cannot refuse is not a decision. A restoration is twice
        /// the work for twice the money, so whether it is worth taking depends entirely on what
        /// else wants your hands - and without this the car simply happens to you.
        ///
        /// Nothing is earned and nothing is lost: the bay frees on the next tick, which is the
        /// point. It is a general rule rather than a restoration-only one, because "you can always
        /// turn work down" is easier to understand than a button that appears on some cars.
        /// </summary>
        Declined = 3
    }

    /// <summary>
    /// Whether a car can be quoted for, and if not, why not.
    ///
    /// These are FOUR different states that were previously collapsed into one question -
    /// "has anything been revealed?" - and that conflation was a bug. A diagnosis check reveals a
    /// SYSTEM, and a revealed system need not carry any outstanding work: it can be perfectly
    /// healthy, or its job can already be finished. So a successful check could leave the garage
    /// with something revealed and nothing to quote for.
    ///
    /// The web build then treated that empty quote as "this car is already done" and silently
    /// accepted EVERY job on the car, hidden ones included, and started repairing. The player
    /// pressed a button marked "Quote what I found", never saw a quote or a price, and was
    /// committed to the most expensive option on work they had never been shown.
    ///
    /// Core owns the distinction now so the two builds cannot answer it differently.
    /// </summary>
    public enum QuoteReadiness
    {
        /// <summary>Nobody has looked at anything yet. There is nothing to write a quote from.</summary>
        NothingInspected = 0,

        /// <summary>
        /// Something has been revealed, but none of it is work that still needs doing - and the car
        /// DOES still have jobs nobody has found. Quoting here would be quoting for an empty bill,
        /// and must not be mistaken for the car being finished.
        /// </summary>
        NothingRepairableFound = 1,

        /// <summary>There is revealed, outstanding work. A quote can be written for it.</summary>
        ReadyToQuote = 2,

        /// <summary>
        /// The car genuinely has no outstanding work left, revealed or hidden. This is the real
        /// "already done" case, and the only one in which an empty quote may be completed outright.
        /// </summary>
        NoWorkRemaining = 3
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
        // A double, like FaultThreshold: a threshold both builds compare against.
        public const double EssentialThreshold = 0.34d;

        private readonly List<QuoteLine> _lines = new List<QuoteLine>();

        public IReadOnlyList<QuoteLine> Lines { get { return _lines; } }

        /// <summary>What the customer pays for the lot.</summary>
        public double EverythingPrice { get; private set; }

        /// <summary>What they pay for the must-dos only.</summary>
        public double EssentialPrice { get; private set; }

        public int EssentialCount { get; private set; }

        public int LineCount { get { return _lines.Count; } }

        private Quote() { }

        /// <summary>
        /// Builds the quote for a car from the work the garage has actually FOUND.
        ///
        /// A quote can only list what was discovered. This used to read the condition straight off
        /// the car, which meant the quote screen showed every fault at its true percentage whether
        /// or not anybody had looked - and that made inspecting worthless. It was measured: with
        /// the quote reading the condition directly, running every check changed the quote
        /// decision on 0.0% of 4,800 cars. The information was already free.
        ///
        /// The condition values themselves are untouched, and so are the faults. The only thing
        /// that changed is WHEN the garage is allowed to see them.
        /// </summary>
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

                // Work nobody has found yet cannot be quoted for. There is no danger of stranding
                // it: picking up a spanner on a car that was never inspected still reveals the lot
                // (see CarDiagnosis.RevealAll), so an unquoted job is deferred, never lost.
                if (!car.Diagnosis.IsRevealed(system)) continue;

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
        /// Which of the four quote states this car is in. The single rule both UIs ask.
        ///
        /// "Outstanding" means a job that is not finished. A job the customer declined is still
        /// outstanding work as far as this question goes - declining is an answer to a quote, and
        /// the car leaves on the next tick, so it never reaches here.
        /// </summary>
        public static QuoteReadiness ReadinessFor(ActiveCar car)
        {
            if (car == null) return QuoteReadiness.NothingInspected;

            int revealedOutstanding = 0;
            int hiddenOutstanding = 0;

            for (int i = 0; i < car.Jobs.Count; i++)
            {
                RepairJob job = car.Jobs[i];
                if (job.IsComplete) continue;

                if (car.Diagnosis.IsRevealed(CarCondition.SystemFor(job.Type))) revealedOutstanding++;
                else hiddenOutstanding++;
            }

            // Nothing left to do at all: the genuine completion case.
            if (revealedOutstanding == 0 && hiddenOutstanding == 0) return QuoteReadiness.NoWorkRemaining;

            // Something found that still needs doing: a quote can be written.
            if (revealedOutstanding > 0) return QuoteReadiness.ReadyToQuote;

            // Work remains, but none of it has been found. Either nobody has looked, or what was
            // looked at turned out to be fine - two different things to tell the player.
            return car.Diagnosis.RevealedCount > 0
                ? QuoteReadiness.NothingRepairableFound
                : QuoteReadiness.NothingInspected;
        }

        /// <summary>
        /// True only when an empty quote means the car is finished, rather than meaning the garage
        /// has not found the work yet. The web build's empty-quote shortcut must be gated on this.
        /// </summary>
        public static bool CanCompleteWithoutQuoting(ActiveCar car)
        {
            return ReadinessFor(car) == QuoteReadiness.NoWorkRemaining;
        }

        /// <summary>
        /// Whether a repair is one the car should not leave without.
        ///
        /// Brakes get a lower bar than everything else, because brakes are brakes - a garage that
        /// sends a car out with soft ones has a bigger problem than a thin margin.
        /// </summary>
        public static bool IsEssential(JobType jobType, double systemHealth)
        {
            if (jobType == JobType.Brakes) return systemHealth <= CarCondition.FaultThreshold;

            // Cosmetic work is never essential however bad it looks.
            if (jobType == JobType.Paint) return false;

            return systemHealth <= EssentialThreshold;
        }

        /// <summary>Applies the customer's answer to the car, marking each job accepted or declined.</summary>
        /// <summary>
        /// Writes the player's answer back onto the car.
        ///
        /// Only the quoted lines are touched. A job the garage has not found yet keeps the state
        /// it already had, which is accepted: the customer was never told about it, so turning it
        /// down on their behalf would be putting words in their mouth.
        /// </summary>
        public void Apply(ActiveCar car, QuoteOption option)
        {
            if (car == null) return;

            if (option == QuoteOption.Declined)
            {
                // Turned down: nothing is accepted, so the car has no outstanding work, finishes
                // owing nothing and releases the bay. Uses the same path as picking lines by hand.
                ApplyCustom(car, null);
                car.SetQuoted(QuoteOption.Declined);
                return;
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                QuoteLine line = _lines[i];
                bool accepted = option == QuoteOption.Everything || line.IsEssential;

                car.Jobs[line.JobIndex].SetAccepted(accepted);
            }

            car.SetQuoted(option);
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

            car.SetQuoted(QuoteOption.Custom);
        }

        /// <summary>What the parts on a quote will cost, and which of them the shelf cannot cover.</summary>
        public struct PartsSummary
        {
            /// <summary>Total value of the parts this option needs, at the garage's current policy.</summary>
            public double Value;

            /// <summary>Kinds the shelf is short of - these come off the van, with the surcharge.</summary>
            public List<Parts.PartKind> Short;

            /// <summary>True when the option needs nothing off the shelf at all.</summary>
            public bool NeedsNothing;
        }

        /// <summary>
        /// Works out the parts bill for an option on this quote.
        ///
        /// IN CORE, not in either view. Both builds draw the same line on the quote screen - what
        /// the parts cost and what is short - and the counting behind it is a rule, not a drawing.
        /// Letting each front end work it out separately is how the two start disagreeing about
        /// the same car, which is the one thing this project has already been bitten by.
        /// </summary>
        public PartsSummary SummariseParts(Parts.PartsInventory inventory, bool essentialOnly)
        {
            PartsSummary summary = new PartsSummary();
            summary.Short = new List<Parts.PartKind>();

            if (inventory == null) { summary.NeedsNothing = true; return summary; }

            Parts.PartGrade policy = inventory.Policy;
            Dictionary<Parts.PartKind, int> needed = new Dictionary<Parts.PartKind, int>();

            for (int i = 0; i < _lines.Count; i++)
            {
                QuoteLine line = _lines[i];
                if (essentialOnly && !line.IsEssential) continue;

                Parts.PartKind kind = Parts.PartKinds.For(line.Type);
                if (kind == Parts.PartKind.None) continue;

                summary.Value += Parts.PartsInventory.ValueOnJob(line.Price, policy);
                needed[kind] = (needed.ContainsKey(kind) ? needed[kind] : 0) + 1;
            }

            if (needed.Count == 0) { summary.NeedsNothing = true; return summary; }

            // Ordered by kind so the two builds list a shortage in the same order, which makes the
            // cross-build diff meaningful rather than order-dependent.
            for (int kind = 1; kind <= Parts.PartKinds.Count; kind++)
            {
                Parts.PartKind candidate = (Parts.PartKind)kind;
                if (!needed.ContainsKey(candidate)) continue;

                if (inventory.TotalStockOf(candidate) < needed[candidate]) summary.Short.Add(candidate);
            }

            return summary;
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
        public static double SatisfactionModifier(CustomerMood mood, QuoteOption chosen)
        {
            QuoteOption preferred = PreferenceOf(mood);

            if (chosen == QuoteOption.Custom) return 0d;     // no strong feelings either way
            if (chosen == preferred) return 0.08d;

            // A VIP told "we only did the bare minimum" takes it worse than most.
            return mood == CustomerMood.Vip ? -0.12d : -0.06d;
        }

        /// <summary>
        /// Satisfaction for finished work, adjusted for how it was quoted.
        /// Double, to match QualityReport.Satisfaction - see the note on its Score field.
        /// </summary>
        public static double AdjustSatisfaction(double satisfaction, CustomerMood mood, QuoteOption chosen)
        {
            double adjusted = satisfaction + SatisfactionModifier(mood, chosen);
            if (adjusted < 0d) return 0d;
            return adjusted > 1d ? 1d : adjusted;
        }
    }
}
