using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Events;
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


            // --- the graded quality curve, Phase C.3 ---
            //
            // The section above predates partial credit for good rounds, so it cannot see the new
            // term at all: every case in it has zero good rounds. This one drives the whole chain
            // on purpose - execution, score, satisfaction and pay - across every round mix, every
            // part grade, with and without a customer expectation, for both an ordinary customer
            // and a collector.
            //
            // Emitted at six decimals rather than three because satisfaction now varies in the
            // third decimal where it used to be clamped flat, and a three-decimal round would hide
            // exactly the separation this phase was built to create.
            json.Append("\"qualityGraded\":[\n");
            first = true;
            foreach (int perfect in new[] { 0, 1, 3 })
            {
                foreach (int good in new[] { 0, 1, 2, 4 })
                {
                    foreach (int weak in new[] { 0, 2 })
                    {
                        foreach (int damage in new[] { 0, 1 })
                        {
                            foreach (Core.Parts.PartGrade grade in Enum.GetValues(typeof(Core.Parts.PartGrade)))
                            {
                                foreach (Core.Parts.PartGrade? expected in new Core.Parts.PartGrade?[] { null, Core.Parts.PartGrade.Performance })
                                {
                                    RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1.4f, 100d, 1f);
                                    for (int i = 0; i < perfect; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
                                    for (int i = 0; i < good; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, ""));
                                    for (int i = 0; i < weak; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));
                                    for (int i = 0; i < damage; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Damage, ""));
                                    job.RecordPart(grade, 0d, 0d);

                                    foreach (CustomerMood mood in new[] { CustomerMood.Ordinary, CustomerMood.Vip })
                                    {
                                        QualityReport report = RepairQuality.ForJob(job, mood, expected);

                                        if (!first) json.Append(",\n");
                                        first = false;

                                        json.Append("  {\"p\":").Append(perfect)
                                            .Append(",\"g\":").Append(good)
                                            .Append(",\"k\":").Append(weak)
                                            .Append(",\"d\":").Append(damage)
                                            .Append(",\"grade\":").Append((int)grade)
                                            .Append(",\"exp\":").Append(expected.HasValue ? ((int)expected.Value).ToString(CultureInfo.InvariantCulture) : "-1")
                                            .Append(",\"m\":").Append((int)mood)
                                            .Append(",\"exec\":").Append(D9(report.Execution))
                                            .Append(",\"score\":").Append(D9(report.Score))
                                            .Append(",\"sat\":").Append(D9(report.Satisfaction))
                                            .Append(",\"pay\":").Append(D9(report.PayMultiplier))
                                            .Append('}');
                                    }
                                }
                            }
                        }
                    }
                }
            }
            json.Append("\n],\n");


            // --- the crew cap and the sell-up readout, Phase C.4 ---
            //
            // Both are shared rules that the two builds compute independently, so both get pinned.
            // The readout especially: it decides what the player is told about a goal hours away.
            json.Append("\"crewCap\":[\n");
            first = true;
            for (int bays = 1; bays <= Core.Balance.GameBalance.MaxBayCount; bays++)
            {
                if (!first) json.Append(",\n");
                first = false;
                json.Append("  {\"bays\":").Append(bays)
                    .Append(",\"max\":").Append(Core.Economy.UpgradeState.MaxMechanicsFor(bays))
                    .Append('}');
            }
            json.Append("\n],\n");

            json.Append("\"prestigeReadout\":[\n");
            first = true;
            {
                Core.Economy.PrestigeState prestige = new Core.Economy.PrestigeState();

                foreach (double cash in new[] { 0d, 500d, 30000d, 74250d, 119999d, 120000d, 150000d })
                {
                    foreach (double lifetime in new[] { 900d, 100000d, 1250000d })
                    {
                        foreach (double played in new[] { 30d, 1800d, 7200d })
                        {
                            Core.Economy.PrestigeReadout readout = prestige.BuildReadout(
                                cash, lifetime, played, Core.Balance.GameBalance.StartingCash);

                            if (!first) json.Append(",\n");
                            first = false;

                            json.Append("  {\"cash\":").Append(D(cash))
                                .Append(",\"life\":").Append(D(lifetime))
                                .Append(",\"played\":").Append(D(played))
                                .Append(",\"need\":").Append(D(readout.Requirement))
                                .Append(",\"left\":").Append(D(readout.Remaining))
                                .Append(",\"frac\":").Append(D(Math.Round(readout.Fraction, 6)))
                                .Append(",\"tokens\":").Append(readout.TokensIfSoldNow)
                                .Append(",\"ready\":").Append(readout.Ready ? 1 : 0)
                                .Append(",\"needsMore\":").Append(readout.NeedsMoreEarnings ? 1 : 0)
                                .Append(",\"hasEta\":").Append(readout.HasEstimate ? 1 : 0)
                                .Append(",\"eta\":").Append(D9(readout.HasEstimate ? Math.Round(readout.EstimateSeconds, 3) : 0d))
                                .Append('}');
                        }
                    }
                }
            }
            json.Append("\n],\n");


            // --- quote readiness: the state that decides whether a quote may be opened ---
            //
            // Driven over every combination of revealed/hidden and outstanding/finished work, as
            // counts rather than live cars, because that is exactly what the rule reads. If either
            // build ever answers one of these differently, the quote bypass is back.
            json.Append("\"quoteReadiness\":[\n");
            first = true;
            foreach (int revealedOutstanding in new[] { 0, 1, 3 })
            {
                foreach (int hiddenOutstanding in new[] { 0, 1, 2 })
                {
                    foreach (int revealedCount in new[] { 0, 1, 4 })
                    {
                        // A system cannot be revealed-and-outstanding without something revealed.
                        if (revealedOutstanding > 0 && revealedCount == 0) continue;

                        int state;
                        if (revealedOutstanding == 0 && hiddenOutstanding == 0) state = 3;        // NoWorkRemaining
                        else if (revealedOutstanding > 0) state = 2;                              // ReadyToQuote
                        else state = revealedCount > 0 ? 1 : 0;                                  // NothingRepairable / NothingInspected

                        if (!first) json.Append(",\n");
                        first = false;

                        json.Append("  {\"revOut\":").Append(revealedOutstanding)
                            .Append(",\"hidOut\":").Append(hiddenOutstanding)
                            .Append(",\"revCount\":").Append(revealedCount)
                            .Append(",\"state\":").Append(state)
                            .Append(",\"canComplete\":").Append(state == 3 ? 1 : 0)
                            .Append(",\"canQuote\":").Append(state == 2 ? 1 : 0)
                            .Append('}');
                    }
                }
            }
            json.Append("\n],\n");

            // --- restoring an event from a save ---
            // Driven through the shipped Core rule, not a copy of it: whatever
            // RandomEventSystem.RestoredRemaining decides is what the web must decide too.
            // --- the plain timers a reload has to carry ---
            // Driven through the shipped Core rules, so the web cannot clamp them differently.
            // --- what a car in a bay pays in patience, including on the inspection ramp ---
            // Driven through the shipped Core rule, so the price of a check cannot drift.
            json.Append("\"patienceRate\":[\n");
            first = true;
            foreach (bool diag in new[] { false, true })
            {
                foreach (bool attended in new[] { false, true })
                {
                    foreach (bool preview in new[] { false, true })
                    {
                        if (!first) json.Append(",\n");
                        first = false;

                        json.Append("  {\"diag\":").Append(diag ? 1 : 0)
                            .Append(",\"attended\":").Append(attended ? 1 : 0)
                            .Append(",\"preview\":").Append(preview ? 1 : 0)
                            .Append(",\"rate\":")
                            .Append(D(Math.Round((double)GarageSimulation.PatienceRateForBayCar(
                                diag, attended, preview), 6)))
                            .Append('}');
                    }
                }
            }
            json.Append("\n],\n");

            json.Append("\"saveTimers\":[\n");
            first = true;
            foreach (double saved in new[] { -12d, -0.5d, 0d, 0.05d, 4.5d, 30d, 59.5d, 9999d })
            {
                foreach (double total in new[] { 30d, 45d })
                {
                    if (!first) json.Append(",\n");
                    first = false;

                    json.Append("  {\"saved\":").Append(D(saved))
                        .Append(",\"total\":").Append(D(total))
                        .Append(",\"spawn\":")
                        .Append(D(Math.Round((double)GarageSimulation.RestoredSpawnTimer((float)saved), 4)))
                        .Append(",\"workBegan\":")
                        .Append(D(Math.Round((double)ActiveCar.RestoredWorkBegan((float)saved, (float)total), 4)))
                        .Append('}');
                }
            }
            json.Append("\n],\n");

            json.Append("\"eventRestore\":[\n");
            first = true;
            foreach (GameEventDefinition definition in GameEventCatalog.All)
            {
                // Doubles, so the dump prints 34.9 rather than a float's 34.900002 - the web
                // has no floats to widen and the keys have to match exactly.
                foreach (double saved in new[] { -5d, 0d, 0.5d, 10d, 34.9d, 40d, 59.5d, 60d, 9999d })
                {
                    float restored = RandomEventSystem.RestoredRemaining(
                        (float)saved, definition.DurationSeconds);

                    if (!first) json.Append(",\n");
                    first = false;

                    json.Append("  {\"id\":").Append((int)definition.Id)
                        .Append(",\"dur\":").Append(D(definition.DurationSeconds))
                        .Append(",\"saved\":").Append(D(saved))
                        .Append(",\"restored\":").Append(D(Math.Round((double)restored, 4)))
                        .Append(",\"active\":").Append(restored > 0f ? 1 : 0)
                        .Append('}');
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
                            // Six decimals, not four: four lands on an exact rounding tie here, where C#
                            // rounds half-to-even and JS half-up, so 0.85825 read 0.8582 in one dump
                            // and 0.8583 in the other with nothing actually different. The
                            // comparator's tolerance should judge this, not the formatter.
                            .Append(",\"mult\":").Append(D(Math.Round(report.PayMultiplier, 6)))
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
                    .Append(",\"maxJobs\":").Append(definition.MaximumJobs)
                    .Append(",\"fleet\":").Append(definition.FleetSize)
                    .Append(",\"rep\":").Append(D(definition.ReputationWeight))
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

            // --- standing: what one customer does to the garage's name ---
            //
            // The satisfaction figure is the existing one; all that is new is how far it moves the
            // needle. Compared across a spread of satisfactions and weights, because a build that
            // agreed on the weight and disagreed on the arithmetic would drift apart over a
            // session rather than at once.
            json.Append("\"standing\":[\n");
            first = true;

            foreach (double satisfaction in new[] { 0d, 0.4d, 0.75d, 0.93d, 0.99d, 1d })
            {
                foreach (double weight in new[] { 1d, 6d })
                {
                    foreach (double from in new[] { -0.5d, 0d, 0.5d })
                    {
                        double move = (satisfaction - Core.Balance.GameBalance.NeutralSatisfaction)
                                      * Core.Balance.GameBalance.StandingStep * weight;

                        double after = Core.Util.MathUtil.Clamp(from + move, -1d, 1d);
                        double bias = after * Core.Balance.GameBalance.StandingBiasRange;

                        if (!first) json.Append(",\n");
                        first = false;

                        json.Append("  {\"sat\":").Append(D(satisfaction))
                            .Append(",\"w\":").Append(D(weight))
                            .Append(",\"from\":").Append(D(from))
                            .Append(",\"after\":").Append(D(Math.Round(after, 6)))
                            .Append(",\"bias\":").Append(D(Math.Round(bias, 6)))
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

        /// <summary>
        /// Nine decimals, for values that land on an exact tie at six.
        ///
        /// A pay multiplier of 0.8190625 is exactly half way at the sixth decimal, where C# rounds
        /// half-to-even and JS rounds half-up: 0.819062 against 0.819063. The two builds agree
        /// perfectly and the printout does not, and the difference reads as 1.0000000000287557e-06
        /// in floating point, which clears the comparator's 1e-6 tolerance by a hair and fails.
        /// Printing past the tie removes the ambiguity rather than widening the tolerance to hide it.
        /// </summary>
        private static string D9(double value)
        {
            return value.ToString("0.#########", CultureInfo.InvariantCulture);
        }
    }
}
