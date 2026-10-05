using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// Prints the shared Phase A calculations as JSON so the SAME inputs can be run through the
    /// web build in a browser and the two outputs compared line for line.
    ///
    /// The parity test suite pins the constants; this proves the arithmetic actually agrees. A
    /// matching constant with a different formula around it still gives two different games.
    /// </summary>
    public static class ParityDump
    {
        public static void Run()
        {
            StringBuilder json = new StringBuilder();
            json.Append("{\n");

            // --- condition, for a spread of car ids and job shapes ---
            json.Append("\"condition\":[\n");

            JobType[][] jobSets =
            {
                new[] { JobType.Brakes },
                new[] { JobType.Engine, JobType.Paint },
                new[] { JobType.Tires, JobType.Electrics, JobType.Panels },
                new[] { JobType.Engine, JobType.Brakes, JobType.Suspension, JobType.Exhaust }
            };

            bool first = true;
            for (int setIndex = 0; setIndex < jobSets.Length; setIndex++)
            {
                foreach (int carId in new[] { 1, 7, 42, 555, 99999 })
                {
                    List<RepairJob> jobs = new List<RepairJob>();
                    for (int i = 0; i < jobSets[setIndex].Length; i++)
                    {
                        // A fixed work amount per position, so both builds feed identical input.
                        jobs.Add(new RepairJob(jobSets[setIndex][i], MinigameType.TimingBar,
                            1f + i * 0.3f, 100d, 1f));
                    }

                    CarCondition condition = CarCondition.ForCar(carId, jobs);

                    if (!first) json.Append(",\n");
                    first = false;

                    json.Append("  {\"set\":").Append(setIndex).Append(",\"id\":").Append(carId)
                        .Append(",\"p\":[");
                    int[] percents = condition.ToPercents();
                    for (int i = 0; i < percents.Length; i++)
                    {
                        if (i > 0) json.Append(',');
                        json.Append(percents[i]);
                    }
                    json.Append("]}");
                }
            }
            json.Append("\n],\n");

            // --- quality, over a grid of round outcomes ---
            json.Append("\"quality\":[\n");
            first = true;
            foreach (float work in new[] { 1f, 1.5f, 1.9f })
            {
                foreach (int perfect in new[] { 0, 2, 4 })
                {
                    foreach (int weak in new[] { 0, 3 })
                    {
                        foreach (int damage in new[] { 0, 2 })
                        {
                            RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, work, 100d, 1f);
                            for (int i = 0; i < perfect; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
                            for (int i = 0; i < weak; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));
                            for (int i = 0; i < damage; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Damage, ""));

                            foreach (CustomerMood mood in Enum.GetValues(typeof(CustomerMood)))
                            {
                                QualityReport report = RepairQuality.ForJob(job, mood);

                                if (!first) json.Append(",\n");
                                first = false;

                                json.Append("  {\"w\":").Append(F(work))
                                    .Append(",\"p\":").Append(perfect)
                                    .Append(",\"k\":").Append(weak)
                                    .Append(",\"d\":").Append(damage)
                                    .Append(",\"m\":").Append((int)mood)
                                    .Append(",\"pct\":").Append(report.Percent)
                                    .Append(",\"stars\":").Append(report.Stars)
                                    .Append(",\"sat\":").Append(F((float)Math.Round(report.Satisfaction, 3)))
                                    .Append('}');
                            }
                        }
                    }
                }
            }
            json.Append("\n],\n");

            // --- the diagnosis bonus, over accuracy ---
            json.Append("\"diagBonus\":[\n");
            first = true;
            foreach (MinigameOutcome outcome in new[]
                     { MinigameOutcome.Perfect, MinigameOutcome.Good, MinigameOutcome.Weak, MinigameOutcome.Miss })
            {
                List<RepairJob> jobs = new List<RepairJob>
                {
                    new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1.4f, 100d, 1f)
                };
                CarCondition condition = CarCondition.ForCar(31337, jobs);

                CarDiagnosis diagnosis = new CarDiagnosis();
                foreach (DiagnosisAction action in Enum.GetValues(typeof(DiagnosisAction)))
                {
                    diagnosis.Record(action, outcome, condition);
                }

                if (!first) json.Append(",\n");
                first = false;

                json.Append("  {\"outcome\":\"").Append(outcome.ToString().ToLowerInvariant())
                    .Append("\",\"revealed\":").Append(diagnosis.RevealedCount)
                    .Append(",\"bonus\":").Append(F((float)Math.Round(diagnosis.PayoutBonus(condition), 4)))
                    .Append('}');
            }
            json.Append("\n],\n");

            // --- the tip ---
            json.Append("\"tip\":[\n");
            first = true;
            foreach (double payout in new[] { 100d, 360d, 1850d })
            {
                foreach (float speed in new[] { 1f, 0.5f, 0.1f })
                {
                    foreach (CustomerMood mood in Enum.GetValues(typeof(CustomerMood)))
                    {
                        // MathUtil.RoundCash, NOT Math.Round. C#'s default rounds halves to even
                        // and JavaScript's rounds them up, so a tip of exactly 12.5 came out as 12
                        // here and 13 in the browser - a divergence invented by this harness rather
                        // than present in the game. The rule: a parity dump must call the same
                        // helper the game calls, never reimplement it.
                        double tip = Core.Util.MathUtil.RoundCash(
                            payout * Core.Balance.GameBalance.SpeedTipFraction * speed * mood.TipMultiplier());

                        if (!first) json.Append(",\n");
                        first = false;

                        json.Append("  {\"pay\":").Append(F((float)payout))
                            .Append(",\"speed\":").Append(F(speed))
                            .Append(",\"m\":").Append((int)mood)
                            .Append(",\"tip\":").Append(F((float)tip))
                            .Append('}');
                    }
                }
            }
            json.Append("\n],\n");

            // --- parts: the value of a part on a job, and the labour left after it ---
            json.Append("\"parts\":[\n");
            first = true;
            foreach (double gross in new[] { 37d, 100d, 413d, 1000d, 7391d })
            {
                foreach (Core.Parts.PartGrade grade in Enum.GetValues(typeof(Core.Parts.PartGrade)))
                {
                    double value = Core.Parts.PartsInventory.ValueOnJob(gross, grade);

                    RepairJob job = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, gross, 1f);
                    job.RecordPart(grade, 0d, value);

                    if (!first) json.Append(",\n");
                    first = false;

                    json.Append("  {\"gross\":").Append(F((float)gross))
                        .Append(",\"g\":").Append((int)grade)
                        .Append(",\"value\":").Append(F((float)Math.Round(value, 4)))
                        .Append(",\"labour\":").Append(F((float)Math.Round(job.LabourPayout, 4)))
                        .Append(",\"surcharge\":").Append(F((float)Core.Util.MathUtil.RoundCash(
                            value * (Core.Balance.GameBalance.PartsCounterMarkup - 1d))))
                        .Append('}');
                }
            }
            json.Append("\n],\n");

            // --- the quote's parts line: what each option costs, and what is short ---
            json.Append("\"quoteParts\":[\n");
            first = true;

            foreach (int seed in new[] { 61, 404, 1234, 9090 })
            {
                foreach (Core.Parts.PartGrade policy in Enum.GetValues(typeof(Core.Parts.PartGrade)))
                {
                    foreach (bool bareShelf in new[] { false, true })
                    {
                        GarageSimulation simulation = new GarageSimulation(seed);
                        ActiveCar car = simulation.SpawnCar();

                        simulation.Inventory.Policy = policy;

                        if (bareShelf)
                        {
                            // Drain every shelf, so the shortage list is exercised too.
                            for (int i = 0; i < 200; i++)
                            {
                                simulation.Inventory.Fit(JobType.Brakes, 1d, 0);
                                simulation.Inventory.Fit(JobType.Engine, 1d, 0);
                                simulation.Inventory.Fit(JobType.Tires, 1d, 0);
                                simulation.Inventory.Fit(JobType.Paint, 1d, 0);
                                simulation.Inventory.Fit(JobType.Panels, 1d, 0);
                                simulation.Inventory.Fit(JobType.Electrics, 1d, 0);
                                simulation.Inventory.Fit(JobType.Suspension, 1d, 0);
                                simulation.Inventory.Fit(JobType.Exhaust, 1d, 0);
                            }
                        }

                        Quote quote = Quote.For(car);

                        foreach (bool essentialOnly in new[] { false, true })
                        {
                            Quote.PartsSummary summary = quote.SummariseParts(
                                simulation.Inventory, essentialOnly);

                            if (!first) json.Append(",\n");
                            first = false;

                            json.Append("  {\"seed\":").Append(seed)
                                .Append(",\"policy\":").Append((int)policy)
                                .Append(",\"bare\":").Append(bareShelf ? 1 : 0)
                                .Append(",\"ess\":").Append(essentialOnly ? 1 : 0)
                                .Append(",\"lines\":[");

                            for (int i = 0; i < quote.LineCount; i++)
                            {
                                if (i > 0) json.Append(',');
                                json.Append("{\"t\":").Append((int)quote.Lines[i].Type)
                                    .Append(",\"p\":").Append(F((float)quote.Lines[i].Price))
                                    .Append(",\"e\":").Append(quote.Lines[i].IsEssential ? 1 : 0)
                                    .Append('}');
                            }

                            json.Append(']')
                                .Append(",\"value\":").Append(F((float)Math.Round(summary.Value, 3)))
                                .Append(",\"nothing\":").Append(summary.NeedsNothing ? 1 : 0)
                                .Append(",\"short\":[");

                            for (int i = 0; i < summary.Short.Count; i++)
                            {
                                if (i > 0) json.Append(',');
                                json.Append((int)summary.Short[i]);
                            }

                            json.Append("]}");
                        }
                    }
                }
            }

            json.Append("\n],\n");

            // --- how fast parts arrive for a garage of a given size ---
            json.Append("\"delivery\":[\n");
            first = true;
            for (int mechanics = 0; mechanics <= 6; mechanics++)
            {
                if (!first) json.Append(",\n");
                first = false;

                json.Append("  {\"m\":").Append(mechanics)
                    .Append(",\"interval\":").Append(F((float)Math.Round(
                        Core.Balance.GameBalance.PartDeliveryInterval(mechanics), 4)))
                    .Append('}');
            }

            json.Append("\n],\n");

            // --- the finished payout: labour, part, quality and the flawless bonus together ---
            json.Append("\"payout\":[\n");
            first = true;

            foreach (double gross in new[] { 37d, 100d, 413d, 1000d, 7391d })
            {
                foreach (Core.Parts.PartGrade grade in Enum.GetValues(typeof(Core.Parts.PartGrade)))
                {
                    foreach (int[] mix in new[]
                             {
                                 new[] { 3, 0, 0 },   // flawless
                                 new[] { 1, 2, 1 },   // ordinary
                                 new[] { 0, 0, 8 }    // scrappy
                             })
                    {
                        RepairJob job = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, gross, 1f);

                        for (int i = 0; i < mix[0]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
                        for (int i = 0; i < mix[1]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, ""));
                        for (int i = 0; i < mix[2]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));

                        job.RecordPart(grade, 0d, Core.Parts.PartsInventory.ValueOnJob(gross, grade));

                        QualityReport report = RepairQuality.ForJob(job, CustomerMood.Ordinary);

                        double payout = job.LabourPayout * report.PayMultiplier;
                        if (job.IsFlawless) payout *= Core.Balance.GameBalance.PerfectJobCashBonus;

                        if (!first) json.Append(",\n");
                        first = false;

                        json.Append("  {\"gross\":").Append(F((float)gross))
                            .Append(",\"g\":").Append((int)grade)
                            .Append(",\"mix\":\"").Append(mix[0]).Append('-').Append(mix[1]).Append('-').Append(mix[2])
                            .Append("\",\"pct\":").Append(report.Percent)
                            .Append(",\"mult\":").Append(F((float)Math.Round(report.PayMultiplier, 4)))
                            .Append(",\"pay\":").Append(F((float)Core.Util.MathUtil.RoundCash(payout)))
                            .Append('}');
                    }
                }
            }

            json.Append("\n],\n");

            // --- special jobs: the dials, the roll, and what they do to a car ---
            //
            // The dials alone are not enough. Two builds can agree on "patience x 0.55" and still
            // disagree on the car, because one of them applies it before the mood multiplier and
            // the other after. So this dumps the finished numbers as well as the inputs.
            json.Append("\"specialDials\":[\n");
            first = true;

            for (int i = 0; i < Core.Special.SpecialJobCatalog.All.Count; i++)
            {
                Core.Special.SpecialJobDefinition definition = Core.Special.SpecialJobCatalog.All[i];

                if (!first) json.Append(",\n");
                first = false;

                json.Append("  {\"type\":").Append((int)definition.Type)
                    .Append(",\"name\":\"").Append(definition.DisplayName)
                    .Append("\",\"patience\":").Append(F((float)definition.PatienceMultiplier))
                    .Append(",\"payout\":").Append(F((float)definition.PayoutMultiplier))
                    .Append(",\"tip\":").Append(F((float)definition.SpeedTipMultiplier))
                    .Append(",\"quality\":").Append(F((float)definition.QualityWeight))
                    .Append(",\"extraJobs\":").Append(definition.ExtraJobs)
                    .Append(",\"grade\":").Append(definition.ExpectedGrade.HasValue ? (int)definition.ExpectedGrade.Value : -1)
                    .Append(",\"work\":").Append(D(definition.WorkMultiplier))
                    .Append(",\"minJobs\":").Append(definition.MinimumJobs)
                    .Append(",\"weight\":").Append(F(definition.SpawnWeight))
                    .Append(",\"minRank\":").Append(definition.MinRankLevel)
                    .Append('}');
            }

            json.Append("\n],\n");

            // What a special job does to the car, after every other multiplier has had its turn.
            //
            // The dials alone are not enough: two builds can agree on "patience x 0.55" and still
            // disagree on the car, because one applies it before the mood multiplier and the other
            // after. The roll itself is deliberately NOT compared - the two builds draw from
            // different generators by design, so only the arithmetic can be held to parity.
            json.Append("\"specialApply\":[\n");
            first = true;

            foreach (int typeValue in new[] { 0, 1 })
            {
                Core.Special.SpecialJobDefinition definition =
                    Core.Special.SpecialJobCatalog.FindByType((Core.Special.SpecialJobType)typeValue);

                foreach (float basePatience in new[] { 18f, 30f, 47.5f })
                {
                    foreach (double baseGross in new[] { 60d, 240d, 1337d })
                    {
                        for (int moodIndex = 0; moodIndex < 5; moodIndex++)
                        {
                            CustomerMood mood = (CustomerMood)moodIndex;

                            // Exactly the order CarSpawner uses: scale, then mood, then special.
                            double patience = basePatience * Core.Balance.GameBalance.PatienceScale;
                            patience *= mood.PatienceMultiplier();
                            if (definition != null) patience *= definition.PatienceMultiplier;

                            double gross = baseGross * mood.PayoutMultiplier()
                                           * Core.Balance.GameBalance.PartsPayoutCompensation;
                            if (definition != null) gross *= definition.PayoutMultiplier;

                            double tipFraction = Core.Balance.GameBalance.SpeedTipFraction
                                                 * (definition == null ? 1d : definition.SpeedTipMultiplier);
                            double qualityWeight = definition == null ? 1d : definition.QualityWeight;

                            if (!first) json.Append(",\n");
                            first = false;

                            json.Append("  {\"type\":").Append(typeValue)
                                .Append(",\"basePat\":").Append(F(basePatience))
                                .Append(",\"baseGross\":").Append(F((float)baseGross))
                                .Append(",\"m\":").Append(moodIndex)
                                .Append(",\"patience\":").Append(D(patience))
                                .Append(",\"gross\":").Append(D(gross))
                                .Append(",\"tipFraction\":").Append(D(tipFraction))
                                .Append(",\"qualityWeight\":").Append(D(qualityWeight))
                                .Append('}');
                        }
                    }
                }
            }

            json.Append("\n],\n");

            // --- the diagnosis gate: what the quote is allowed to see ---
            //
            // Like quoteParts, this emits its INPUTS as well as its answer. The web spawner is
            // Math.random based, so the same car cannot be reproduced there by seed; replaying the
            // inputs compares the RULE, which is the thing that has to agree.
            //
            // Two builds can agree on every price and still disagree about which lines exist, and
            // that would be two different games.
            json.Append("\"quoteGate\":[\n");
            first = true;

            foreach (int seed in new[] { 1001, 4242, 6007, 7700 })
            {
                GarageSimulation gateSim = new GarageSimulation(seed);
                ActiveCar gateCar = gateSim.SpawnCar();

                // 0 = nothing looked at, 127 = the whole car, and a spread in between.
                foreach (int mask in new[] { 0, 1, 2, 3, 5, 18, 63, 127 })
                {
                    gateCar.Diagnosis.Restore(mask, mask != 0, false, 1f, new List<int>());

                    Quote quote = Quote.For(gateCar);

                    if (!first) json.Append(",\n");
                    first = false;

                    json.Append("  {\"seed\":").Append(seed)
                        .Append(",\"mask\":").Append(mask)
                        .Append(",\"jobs\":[");

                    for (int i = 0; i < gateCar.Jobs.Count; i++)
                    {
                        if (i > 0) json.Append(',');
                        json.Append((int)gateCar.Jobs[i].Type);
                    }

                    json.Append("],\"pays\":[");
                    for (int i = 0; i < gateCar.Jobs.Count; i++)
                    {
                        if (i > 0) json.Append(',');
                        json.Append(D(gateCar.Jobs[i].Payout));
                    }

                    json.Append("],\"cond\":[");
                    for (int i = 0; i < VehicleSystemExtensions.Count; i++)
                    {
                        if (i > 0) json.Append(',');
                        json.Append(D(Math.Round(gateCar.Condition.Get((VehicleSystem)i), 6)));
                    }

                    json.Append("],\"lines\":").Append(quote.LineCount)
                        .Append(",\"essential\":").Append(quote.EssentialCount)
                        .Append(",\"all\":").Append(D(Math.Round(quote.EverythingPrice, 2)))
                        .Append(",\"ess\":").Append(D(Math.Round(quote.EssentialPrice, 2)))
                        .Append(",\"pct\":[");

                    for (int i = 0; i < quote.Lines.Count; i++)
                    {
                        if (i > 0) json.Append(',');
                        json.Append(quote.Lines[i].ConditionPercent);
                    }

                    json.Append("]}");
                }
            }

            json.Append("\n],\n");

            // --- "just get stuck in": what skipping does, and does not do ---
            //
            // Three things have to agree, or one build is giving information away that the other
            // charges for: the reveal mask stays empty, the quote stays empty, and the bonus stays
            // at 1. The accepted-job count is in here too because skipping commits to the car.
            json.Append("\"skipRule\":[\n");
            first = true;

            foreach (int seed in new[] { 7800, 7801, 7802, 8050 })
            {
                GarageSimulation skipSim = new GarageSimulation(seed);
                ActiveCar skipCar = skipSim.SpawnCar();

                skipCar.Diagnosis.Skip();
                skipCar.AcceptAllWork();

                Quote afterSkip = Quote.For(skipCar);

                if (!first) json.Append(",\n");
                first = false;

                json.Append("  {\"seed\":").Append(seed)
                    .Append(",\"jobs\":[");

                for (int i = 0; i < skipCar.Jobs.Count; i++)
                {
                    if (i > 0) json.Append(',');
                    json.Append((int)skipCar.Jobs[i].Type);
                }

                json.Append("],\"revealed\":").Append(skipCar.Diagnosis.RevealedCount)
                    .Append(",\"started\":").Append(skipCar.Diagnosis.HasStarted ? 1 : 0)
                    .Append(",\"skipped\":").Append(skipCar.Diagnosis.WasSkipped ? 1 : 0)
                    .Append(",\"quoteLines\":").Append(afterSkip.LineCount)
                    .Append(",\"accepted\":").Append(skipCar.AcceptedJobCount)
                    .Append(",\"bonus\":").Append(D(skipCar.Diagnosis.PayoutBonus(skipCar.Condition)))
                    .Append('}');
            }

            json.Append("\n],\n");

            // --- the grade expectation: what falling short of it costs ---
            //
            // Every combination of fitted grade against expected grade, including "no expectation",
            // which is every ordinary car. The null row matters most: if the two builds disagree
            // about what a customer with no opinion does to a budget part, the whole parts economy
            // has quietly forked.
            json.Append("\"gradeExpectation\":[\n");
            first = true;

            foreach (int expected in new[] { -1, 0, 1, 2 })        // -1 = no expectation
            {
                foreach (Core.Parts.PartGrade fitted in Enum.GetValues(typeof(Core.Parts.PartGrade)))
                {
                    foreach (int[] mix in new[]
                             {
                                 new[] { 4, 0, 0 },
                                 new[] { 2, 2, 0 },
                                 new[] { 1, 1, 2 }
                             })
                    {
                        const double Gross = 1000d;

                        RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1.4f, Gross, 1f);

                        for (int i = 0; i < mix[0]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
                        for (int i = 0; i < mix[1]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, ""));
                        for (int i = 0; i < mix[2]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));

                        job.RecordPart(fitted, 0d, Core.Parts.PartsInventory.ValueOnJob(Gross, fitted));

                        Core.Parts.PartGrade? expectation =
                            expected < 0 ? (Core.Parts.PartGrade?)null : (Core.Parts.PartGrade)expected;

                        QualityReport quality = RepairQuality.ForJob(job, CustomerMood.Ordinary, expectation);

                        // And the finished pay at the performance job's weighting, so the rule is
                        // compared where it actually bites rather than only in isolation.
                        double weight = SpecialJobCatalog
                            .FindByType(SpecialJobType.Performance).QualityWeight;

                        double pay = job.LabourPayout * (1d + (quality.PayMultiplier - 1d) * weight);
                        if (job.IsFlawless) pay *= Core.Balance.GameBalance.PerfectJobCashBonus;

                        if (!first) json.Append(",\n");
                        first = false;

                        json.Append("  {\"expected\":").Append(expected)
                            .Append(",\"fitted\":").Append((int)fitted)
                            .Append(",\"mix\":\"").Append(mix[0]).Append('-').Append(mix[1]).Append('-').Append(mix[2])
                            .Append("\",\"score\":").Append(D(Math.Round(quality.Score, 6)))
                            .Append(",\"pct\":").Append(quality.Percent)
                            .Append(",\"sat\":").Append(D(Math.Round(quality.Satisfaction, 6)))
                            .Append(",\"pay\":").Append(D(Core.Util.MathUtil.RoundCash(pay)))
                            .Append('}');
                    }
                }
            }

            json.Append("\n],\n");

            // The weighted quality payout. A special job stretches the existing curve rather than
            // getting its own, so this proves the weighting itself agrees - weight 1 has to come
            // out byte for byte identical to the plain multiplier it replaced.
            json.Append("\"specialQualityPay\":[\n");
            first = true;

            foreach (int typeValue in new[] { 0, 1 })
            {
                Core.Special.SpecialJobDefinition definition =
                    Core.Special.SpecialJobCatalog.FindByType((Core.Special.SpecialJobType)typeValue);
                double weight = definition == null ? 1d : definition.QualityWeight;

                foreach (double gross in new[] { 73d, 418d, 2051d })
                {
                    foreach (int[] mix in new[]
                             {
                                 new[] { 3, 0, 0 },
                                 new[] { 1, 2, 1 },
                                 new[] { 0, 0, 5 }
                             })
                    {
                        RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1.4f, gross, 1f);

                        for (int i = 0; i < mix[0]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
                        for (int i = 0; i < mix[1]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, ""));
                        for (int i = 0; i < mix[2]; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));

                        job.RecordPart(Core.Parts.PartGrade.Standard, 0d,
                            Core.Parts.PartsInventory.ValueOnJob(gross, Core.Parts.PartGrade.Standard));

                        QualityReport report = RepairQuality.ForJob(job, CustomerMood.Ordinary);

                        double pay = job.LabourPayout * (1d + (report.PayMultiplier - 1d) * weight);
                        if (job.IsFlawless) pay *= Core.Balance.GameBalance.PerfectJobCashBonus;

                        if (!first) json.Append(",\n");
                        first = false;

                        json.Append("  {\"type\":").Append(typeValue)
                            .Append(",\"gross\":").Append(F((float)gross))
                            .Append(",\"mix\":\"").Append(mix[0]).Append('-').Append(mix[1]).Append('-').Append(mix[2])
                            .Append("\",\"pay\":").Append(F((float)Core.Util.MathUtil.RoundCash(pay)))
                            .Append('}');
                    }
                }
            }

            json.Append("\n]\n}");

            Console.WriteLine(json.ToString());
        }

        private static string F(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Prints a double at full comparison precision.
        ///
        /// Rounding a dump value to three decimals was enough to manufacture a parity failure out
        /// of nothing: 47.5475 is a tie, and C# rounds a tie to even while JavaScript rounds it up,
        /// so two builds that agreed to thirteen decimal places disagreed on the printout. The
        /// comparator already allows 1e-6, so the honest thing is to print the number and let it
        /// judge. Narrowing to float would be just as wrong - that loses more than the tolerance.
        /// </summary>
        private static string D(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
