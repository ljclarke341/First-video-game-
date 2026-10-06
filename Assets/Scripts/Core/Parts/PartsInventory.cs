using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Parts
{
    /// <summary>What actually got fitted to a job, and what it cost.</summary>
    public struct PartFitting
    {
        public PartKind Kind;
        public PartGrade Grade;
        public double Cost;

        /// <summary>True when there was none on the shelf and it had to be bought at the counter.</summary>
        /// <summary>
        /// Cash that genuinely leaves the wallet: the SURCHARGE for not having the part in, and
        /// nothing else. A part off the shelf costs the garage nothing beyond its share of the job.
        /// </summary>
        public bool BoughtIn;

        /// <summary>True when the repair fits nothing - a diagnostic scan.</summary>
        public bool NoPartNeeded;

        /// <summary>
        /// The part's share of the job's gross price - what the customer paid for it.
        ///
        /// This never passes through the wallet. The customer's money for the part and the bill
        /// from the supplier are the same money, so simulating the round trip only inflates
        /// lifetime earnings - which drives garage rank, which measurably put a half-hour run 37%
        /// ahead of where it should have been. The job simply keeps the labour.
        /// </summary>
        public double Value;
    }

    /// <summary>
    /// What is on the shelf, and what it costs to put it there.
    ///
    /// The whole system is one decision repeated: which grade do we fit? Everything else - the
    /// stock counts, the prices, the counter markup - exists to give that decision weight. There
    /// is deliberately no per-job part picking, no supplier list and no delivery time: that is
    /// stock-keeping, and this is a game about repairing cars.
    /// </summary>
    public sealed class PartsInventory
    {
        /// <summary>
        /// What the garage fits by default. One setting, garage-wide, changed in the parts screen
        /// or on a quote - not a question asked once per job.
        /// </summary>
        public PartGrade Policy { get; set; }

        /// <summary>Stock, indexed [kind - 1, grade]. Kind 0 is None and has no shelf.</summary>
        private readonly int[,] _stock = new int[PartKinds.Count, PartGrades.Count];

        /// <summary>Running total handed over for parts, at the shop and at the counter.</summary>
        public double TotalSpent { get; private set; }

        /// <summary>
        /// The list value of every part actually FITTED.
        ///
        /// This, not TotalSpent, is what a part costs the garage. Buying a pack is moving cash onto
        /// the shelf, not losing it - measuring purchases instead makes a well-stocked garage look
        /// like it is haemorrhaging money right up until the moment it uses what it bought.
        /// </summary>
        public double TotalConsumedValue { get; private set; }

        public PartsInventory()
        {
            Policy = PartGrade.Standard;

            // A garage that cannot do its first job because the shelf is empty is not a decision,
            // it is a wall. Opening stock is Standard only: Budget and Performance are chosen.
            for (int kind = 0; kind < PartKinds.Count; kind++)
            {
                _stock[kind, (int)PartGrade.Standard] = GameBalance.StartingPartStock;
            }
        }

        // ------------------------------------------------------------------
        // Prices
        // ------------------------------------------------------------------

        /// <summary>
        /// What one part of this kind costs at Standard grade, at rank 0.
        ///
        /// These are MEASURED, not guessed. Each is 22% of the average gross payout of a real job
        /// of that kind, taken over 400 seeded garages and ~19,000 jobs
        /// ("dotnet run --project Tools/HeadlessTests -- probe parts" prints the measurement).
        ///
        /// That is what makes the economy come out neutral: the gross payout was raised by exactly
        /// the share the part costs, so a garage that keeps Standard stock pays the raise straight
        /// back out to the supplier and nets what it netted before parts existed. A first attempt
        /// derived these from the job payout weights instead and was out by up to 2.5x, because a
        /// job's value depends on which cars carry it, not only on its own weighting.
        /// </summary>
        public static double ReferencePrice(PartKind kind)
        {
            switch (kind)
            {
                case PartKind.EngineParts: return 29d;
                case PartKind.SuspensionParts: return 30d;
                case PartKind.Electrical: return 21d;
                case PartKind.Paint: return 15d;
                case PartKind.BodyPanel: return 13d;
                case PartKind.BrakePads: return 12d;
                case PartKind.ExhaustParts: return 10d;
                case PartKind.Tyres: return 8d;
                default: return 0d;
            }
        }

        /// <summary>The shelf price of one part, at the garage's current rank.</summary>
        public static double ListPrice(PartKind kind, PartGrade grade, int rankLevel)
        {
            if (kind == PartKind.None) return 0d;

            double price = ReferencePrice(kind)
                           * grade.CostMultiplier()
                           * (1d + rankLevel * GameBalance.PartPricePerRank);

            return MathUtil.RoundCash(price);
        }

        // ------------------------------------------------------------------
        // Stock
        // ------------------------------------------------------------------

        public int StockOf(PartKind kind, PartGrade grade)
        {
            if (kind == PartKind.None) return 0;
            return _stock[(int)kind - 1, (int)grade];
        }

        /// <summary>Everything on the shelf of this kind, whatever the grade.</summary>
        public int TotalStockOf(PartKind kind)
        {
            if (kind == PartKind.None) return 0;

            int total = 0;
            for (int grade = 0; grade < PartGrades.Count; grade++) total += _stock[(int)kind - 1, grade];
            return total;
        }

        public void AddStock(PartKind kind, PartGrade grade, int count)
        {
            if (kind == PartKind.None || count <= 0) return;
            _stock[(int)kind - 1, (int)grade] += count;
        }

        /// <summary>Records money spent on parts, wherever it was spent.</summary>
        public void RecordSpend(double amount)
        {
            if (amount > 0d) TotalSpent += amount;
        }

        // ------------------------------------------------------------------
        // Fitting
        // ------------------------------------------------------------------

        /// <summary>
        /// Takes the part a job needs off the shelf.
        ///
        /// The fallback order is the point of the whole system. The policy grade is tried first;
        /// if the shelf is bare it drops to whatever IS there, because fitting something is always
        /// better than stranding the car; and only if there is nothing at all does it buy one in
        /// at the counter, at a markup the player can see coming. There is no path where a repair
        /// cannot happen - just paths where it costs more than it needed to.
        /// </summary>
        /// <summary>
        /// What the part on a given job is worth.
        ///
        /// Taken as a share of THAT JOB's gross price rather than from a price list, and that is
        /// what makes the economy come out neutral: the gross was raised by exactly this share, so
        /// it is handed straight back. It also means a part is always worth proportionally the
        /// same, on a hatchback or a supercar, with no scaling factor to drift.
        ///
        /// An earlier version priced parts from a fixed list scaled by garage rank. It made parts
        /// nearly twice as dear at rank 1 while payouts did not move with rank at all, and the
        /// half-hour economy came out 6% light as a result.
        /// </summary>
        public static double ValueOnJob(double grossPayout, PartGrade grade)
        {
            // NOT rounded. This is an intermediate, and rounding it rounds the labour the other
            // way on every single job - a job paying 10 gross would keep 8 instead of 7.80, which
            // measured out at about 6% across a half hour once it compounded into upgrades.
            // The payout is rounded once, at the end, where it becomes money.
            return grossPayout * GameBalance.PartCostFraction * grade.CostMultiplier();
        }

        public PartFitting Fit(JobType jobType, double grossPayout, int rankLevel)
        {
            PartFitting fitting = new PartFitting();

            PartKind kind = PartKinds.For(jobType);
            if (kind == PartKind.None)
            {
                fitting.NoPartNeeded = true;
                return fitting;
            }

            fitting.Kind = kind;

            PartGrade grade;
            if (TryTakeFromShelf(kind, out grade))
            {
                fitting.Grade = grade;
                fitting.Value = ValueOnJob(grossPayout, grade);
                fitting.Cost = 0d;
                TotalConsumedValue += fitting.Value;
                return fitting;
            }

            // Nothing on the shelf: it comes off the van, and only the PREMIUM is cash out.
            fitting.Grade = Policy;
            fitting.BoughtIn = true;
            fitting.Value = ValueOnJob(grossPayout, Policy);
            fitting.Cost = MathUtil.RoundCash(fitting.Value * (GameBalance.PartsCounterMarkup - 1d));

            TotalConsumedValue += fitting.Value + fitting.Cost;
            return fitting;
        }

        /// <summary>
        /// Takes the best available part, starting from the policy grade.
        ///
        /// Prefers the policy, then drops DOWN through the cheaper grades, then goes up. Dropping
        /// first matters: a player who set the policy to Budget to save money should not have their
        /// Performance stock quietly eaten because the budget shelf ran dry.
        /// </summary>
        private bool TryTakeFromShelf(PartKind kind, out PartGrade grade)
        {
            int row = (int)kind - 1;

            if (_stock[row, (int)Policy] > 0)
            {
                _stock[row, (int)Policy]--;
                grade = Policy;
                return true;
            }

            for (int candidate = (int)Policy - 1; candidate >= 0; candidate--)
            {
                if (_stock[row, candidate] <= 0) continue;
                _stock[row, candidate]--;
                grade = (PartGrade)candidate;
                return true;
            }

            for (int candidate = (int)Policy + 1; candidate < PartGrades.Count; candidate++)
            {
                if (_stock[row, candidate] <= 0) continue;
                _stock[row, candidate]--;
                grade = (PartGrade)candidate;
                return true;
            }

            grade = Policy;
            return false;
        }

        /// <summary>
        /// Which grade a job WOULD be fitted with right now, without taking anything off the shelf.
        /// The quote screen uses this to say what the player is about to do.
        /// </summary>
        public PartGrade PreviewGrade(JobType jobType, out bool inStock)
        {
            inStock = false;

            PartKind kind = PartKinds.For(jobType);
            if (kind == PartKind.None) return Policy;

            int row = (int)kind - 1;

            if (_stock[row, (int)Policy] > 0) { inStock = true; return Policy; }

            for (int candidate = (int)Policy - 1; candidate >= 0; candidate--)
            {
                if (_stock[row, candidate] > 0) { inStock = true; return (PartGrade)candidate; }
            }

            for (int candidate = (int)Policy + 1; candidate < PartGrades.Count; candidate++)
            {
                if (_stock[row, candidate] > 0) { inStock = true; return (PartGrade)candidate; }
            }

            return Policy;
        }

        // ------------------------------------------------------------------
        // Deliveries
        // ------------------------------------------------------------------

        /// <summary>Seconds until the next part turns up on the standing order.</summary>
        public float DeliveryTimer { get; set; }

        /// <summary>
        /// Runs the standing order.
        ///
        /// Stock ARRIVES rather than being bought, and that is what keeps the economy honest. The
        /// first version of this sold parts at a fixed shop price while jobs were charged a share
        /// of their own value - so a player could stock up cheaply on hatchback money and fit
        /// those parts to supercars. Measured, that arbitrage put the half-hour economy 23% up.
        /// With deliveries free and every job paying for its own part, there is nothing to
        /// arbitrage: the raise built into the gross payout is handed straight back.
        ///
        /// Stock still matters, because running dry costs the counter markup.
        /// </summary>
        /// <param name="mechanicCount">
        /// How many mechanics are on the books. A bigger garage gets its parts faster, because a
        /// flat rate meant the surcharge grew from 15% to 64% of parts as the player expanded.
        /// </param>
        public void TickDeliveries(float deltaTime, int mechanicCount = 0)
        {
            if (deltaTime <= 0f) return;

            DeliveryTimer -= deltaTime;
            if (DeliveryTimer > 0f) return;

            DeliveryTimer = GameBalance.PartDeliveryInterval(mechanicCount);

            // One part, to whichever shelf is barest. A garage that never looks at the parts
            // screen still slowly fills up; one that does can expedite what it actually needs.
            PartKind lowest = PartKind.None;
            int lowestCount = int.MaxValue;

            for (int kind = 1; kind <= PartKinds.Count; kind++)
            {
                int count = TotalStockOf((PartKind)kind);
                if (count >= GameBalance.PartShelfCap) continue;
                if (count >= lowestCount) continue;

                lowestCount = count;
                lowest = (PartKind)kind;
            }

            if (lowest != PartKind.None) AddStock(lowest, Policy, 1);
        }

        /// <summary>What it costs to have a shelf filled now rather than waiting for the van.</summary>
        public static double ExpediteFee(PartKind kind, int rankLevel)
        {
            if (kind == PartKind.None) return 0d;

            return MathUtil.RoundCash(
                ReferencePrice(kind) * (1d + rankLevel * GameBalance.PartPricePerRank));
        }

        /// <summary>Fills one shelf to the cap. The caller takes the money.</summary>
        public void Expedite(PartKind kind)
        {
            if (kind == PartKind.None) return;

            int missing = GameBalance.PartShelfCap - TotalStockOf(kind);
            if (missing > 0) AddStock(kind, Policy, missing);
        }

        // ------------------------------------------------------------------
        // Saving
        // ------------------------------------------------------------------

        public IEnumerable<KeyValuePair<string, int>> ToDictionary()
        {
            List<KeyValuePair<string, int>> entries = new List<KeyValuePair<string, int>>();

            for (int kind = 0; kind < PartKinds.Count; kind++)
            {
                for (int grade = 0; grade < PartGrades.Count; grade++)
                {
                    int count = _stock[kind, grade];
                    if (count <= 0) continue;

                    entries.Add(new KeyValuePair<string, int>(kind + "_" + grade, count));
                }
            }

            return entries;
        }

        /// <summary>
        /// Puts stock back from a save.
        ///
        /// A save written before parts existed has no entry at all, and the caller leaves the
        /// opening stock in place - so an old garage comes back with a full shelf rather than an
        /// empty one, which would quietly charge the player the counter markup on every job.
        /// </summary>
        public void Restore(IEnumerable<KeyValuePair<string, int>> entries, PartGrade policy,
            double totalSpent, double totalConsumed)
        {
            Array.Clear(_stock, 0, _stock.Length);

            Policy = policy;
            TotalSpent = totalSpent < 0d ? 0d : totalSpent;
            TotalConsumedValue = totalConsumed < 0d ? 0d : totalConsumed;

            if (entries == null) return;

            foreach (KeyValuePair<string, int> entry in entries)
            {
                string[] parts = entry.Key.Split('_');
                if (parts.Length != 2) continue;

                int kind, grade;
                if (!int.TryParse(parts[0], out kind)) continue;
                if (!int.TryParse(parts[1], out grade)) continue;

                if (kind < 0 || kind >= PartKinds.Count) continue;
                if (grade < 0 || grade >= PartGrades.Count) continue;

                _stock[kind, grade] = entry.Value < 0 ? 0 : entry.Value;
            }
        }
    }
}
