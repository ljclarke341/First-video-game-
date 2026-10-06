using System;
using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Vehicle;

namespace GarageTycoon.HeadlessTests
{
    /// <summary>
    /// What does inspecting a car actually TELL you?
    ///
    /// Separate from the income measurement on purpose. Income says whether inspecting pays; this
    /// says whether it informs - and a system can fail the first test while passing the second,
    /// which would be a very different problem to fix.
    ///
    /// The method is a paired comparison on the same car: take every decision the player makes
    /// after the ramp, work it out once with nothing inspected and once with everything inspected,
    /// and count how often the two answers differ. Nothing is changed and nothing is played; this
    /// only asks what the game would decide.
    /// </summary>
    public static class InformationValue
    {
        public static void Run(int seeds)
        {
            Console.WriteLine("-- WHAT INSPECTING TELLS YOU --");
            Console.WriteLine("Every decision after the ramp, worked out twice on the same car:");
            Console.WriteLine("once knowing nothing, once knowing everything.");
            Console.WriteLine();

            int cars = 0;
            int quoteChanged = 0;
            int gradeChanged = 0;
            int outcomeChanged = 0;
            int preventedRepair = 0;
            int foundExtraWork = 0;

            int complaintNamesTwo = 0;
            int carsWithDeclinableWork = 0;
            int carsWithHiddenJobs = 0;
            int jobs = 0;
            int jobsHiddenBeforeInspecting = 0;

            for (int seed = 0; seed < seeds; seed++)
            {
                GarageSimulation simulation = new GarageSimulation(41000 + seed);

                for (int i = 0; i < 40; i++)
                {
                    ActiveCar car = simulation.SpawnCar();
                    if (car == null) continue;

                    cars++;
                    jobs += car.Jobs.Count;

                    // How selective "only inspect a multi-symptom complaint" actually is. If almost
                    // every complaint names two, that strategy is "inspect everything" wearing a hat.
                    if (!string.IsNullOrEmpty(car.Complaint) && car.Complaint.Contains(", and ")) complaintNamesTwo++;

                    // ---- knowing nothing: straight off the back of the truck ----
                    Quote blind = Quote.For(car);
                    PartGrade[] blindGrades = GradesFor(simulation, car);
                    float[] blindDifficulty = DifficultyFor(car);

                    int hidden = 0;
                    for (int j = 0; j < car.Jobs.Count; j++) if (!car.IsJobRevealed(j)) hidden++;

                    jobsHiddenBeforeInspecting += hidden;
                    if (hidden > 0) carsWithHiddenJobs++;

                    // ---- knowing everything: every check run, every fault found ----
                    car.Diagnosis.RevealAll(false);

                    Quote informed = Quote.For(car);
                    PartGrade[] informedGrades = GradesFor(simulation, car);
                    float[] informedDifficulty = DifficultyFor(car);

                    if (QuoteDiffers(blind, informed)) quoteChanged++;
                    if (Differs(blindGrades, informedGrades)) gradeChanged++;
                    if (Differs(blindDifficulty, informedDifficulty)) outcomeChanged++;

                    // Work the customer does not need, which is the repair inspecting would
                    // "prevent" - but only if the player could not already see it was optional.
                    int optional = informed.LineCount - informed.EssentialCount;
                    if (optional > 0)
                    {
                        carsWithDeclinableWork++;
                        if (QuoteDiffers(blind, informed)) preventedRepair++;
                    }

                    // Profitable work the blind quote did not already list.
                    if (informed.LineCount > blind.LineCount) foundExtraWork++;
                }
            }

            Console.WriteLine("cars sampled: " + cars + " (" + jobs + " jobs)");
            Console.WriteLine();
            Console.WriteLine("prevents an unnecessary repair    " + Pct(preventedRepair, cars));
            Console.WriteLine("finds an additional repair        " + Pct(foundExtraWork, cars));
            Console.WriteLine("changes the quote decision        " + Pct(quoteChanged, cars));
            Console.WriteLine("changes the chosen parts grade    " + Pct(gradeChanged, cars));
            Console.WriteLine("changes the repair outcome        " + Pct(outcomeChanged, cars));
            Console.WriteLine();
            Console.WriteLine("for context, on the same cars:");
            Console.WriteLine("  carry work that could be declined " + Pct(carsWithDeclinableWork, cars));
            Console.WriteLine("  complaint names two symptoms      " + Pct(complaintNamesTwo, cars));
            Console.WriteLine("  have jobs hidden on the bay card  " + Pct(carsWithHiddenJobs, cars)
                + "  (" + Pct(jobsHiddenBeforeInspecting, jobs) + " of all jobs)");
            Console.WriteLine();
        }

        /// <summary>
        /// Which grade the shelf would fit to each job. Previewed rather than fitted, so asking
        /// the question does not empty the shelf and change the answer.
        /// </summary>
        private static PartGrade[] GradesFor(GarageSimulation simulation, ActiveCar car)
        {
            PartGrade[] grades = new PartGrade[car.Jobs.Count];

            for (int i = 0; i < car.Jobs.Count; i++)
            {
                bool inStock;
                grades[i] = simulation.Inventory.PreviewGrade(car.Jobs[i].Type, out inStock);
            }

            return grades;
        }

        /// <summary>What each job's mini-game will be set to.</summary>
        private static float[] DifficultyFor(ActiveCar car)
        {
            float[] difficulty = new float[car.Jobs.Count];
            for (int i = 0; i < car.Jobs.Count; i++) difficulty[i] = car.Jobs[i].Difficulty;
            return difficulty;
        }

        private static bool QuoteDiffers(Quote a, Quote b)
        {
            if (a.LineCount != b.LineCount) return true;
            if (a.EssentialCount != b.EssentialCount) return true;
            if (Math.Abs(a.EverythingPrice - b.EverythingPrice) > 0.001d) return true;
            if (Math.Abs(a.EssentialPrice - b.EssentialPrice) > 0.001d) return true;

            for (int i = 0; i < a.Lines.Count; i++)
            {
                if (a.Lines[i].IsEssential != b.Lines[i].IsEssential) return true;
                if (a.Lines[i].Type != b.Lines[i].Type) return true;
            }

            return false;
        }

        private static bool Differs(PartGrade[] a, PartGrade[] b)
        {
            if (a.Length != b.Length) return true;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return true;
            return false;
        }

        private static bool Differs(float[] a, float[] b)
        {
            if (a.Length != b.Length) return true;
            for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i] - b[i]) > 0.0001f) return true;
            return false;
        }

        private static string Pct(int part, int whole)
        {
            if (whole == 0) return "n/a";
            return (part * 100d / whole).ToString("0.0").PadLeft(6) + "%  (" + part + " of " + whole + ")";
        }
    }
}
