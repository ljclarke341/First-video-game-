using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;
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

            json.Append("\n]\n}");

            Console.WriteLine(json.ToString());
        }

        private static string F(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
