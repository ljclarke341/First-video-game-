using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Util;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>Checks the car spawner and the repair-job maths.</summary>
    public static class CarAndJobTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Cars and jobs");

            suite.Add("Every catalog car spawns a valid customer", SpawnedCarsAreValid);
            suite.Add("Job payouts add up to the car's value", PayoutsAddUp);
            suite.Add("A car never gets the same job twice", JobsAreDistinct);
            suite.Add("Reputation bias really does attract rarer cars", RarityBiasWorks);
            suite.Add("Rarer cars are worth more and are harder", RarityScalesValueAndDifficulty);
            suite.Add("All four mini-games appear across a day of cars", MinigameVariety);
            suite.Add("Consecutive jobs rarely repeat the same mini-game", MinigameRepetitionIsRare);
            suite.Add("Perfect rounds complete a job and flag it flawless", JobProgressAndFlawless);
            suite.Add("Damage cannot push progress below zero", ProgressNeverGoesNegative);
            suite.Add("Patience upgrades give customers more time", PatienceScales);

            return suite;
        }

        private static void SpawnedCarsAreValid()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(101));

            for (int i = 0; i < 400; i++)
            {
                ActiveCar car = spawner.Spawn(SpawnParameters.Default);

                Check.IsNotNull(car, "Spawner returned null");
                Check.IsNotNull(car.Definition, "Car has no definition");
                Check.IsTrue(car.Jobs.Count >= 1, "Car spawned with no jobs");
                Check.IsTrue(car.Jobs.Count <= car.Definition.LikelyJobs.Length, "Car has more jobs than its blueprint allows");
                Check.IsTrue(car.TotalTime > 0f, "Car spawned with no patience");
                Check.IsTrue(car.TotalPayout > 0d, "Car spawned worthless");
                Check.AreEqual((int)CarState.Waiting, (int)car.State, "New cars should start in the waiting state");

                for (int j = 0; j < car.Jobs.Count; j++)
                {
                    Check.IsTrue(car.Jobs[j].Payout >= 1d, "Job pays less than a dollar");
                    Check.IsTrue(car.Jobs[j].WorkAmount > 0f, "Job has no work in it");
                    Check.IsFalse(car.Jobs[j].IsComplete, "A fresh job should not be complete");
                }
            }
        }

        private static void PayoutsAddUp()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(102));

            for (int i = 0; i < 200; i++)
            {
                ActiveCar car = spawner.Spawn(SpawnParameters.Default);

                double sum = 0d;
                for (int j = 0; j < car.Jobs.Count; j++) sum += car.Jobs[j].Payout;

                Check.AreClose(car.TotalPayout, sum, 0.001d, "Car total payout should equal the sum of its jobs");

                // Rounding each job to whole dollars can drift a little from the blueprint value.
                Check.InRange(sum, car.Definition.BasePayout - car.Jobs.Count, car.Definition.BasePayout + car.Jobs.Count,
                    "Job payouts should add up to roughly the car's base value");
            }
        }

        private static void JobsAreDistinct()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(103));

            for (int i = 0; i < 300; i++)
            {
                ActiveCar car = spawner.Spawn(SpawnParameters.Default);

                HashSet<JobType> seen = new HashSet<JobType>();
                for (int j = 0; j < car.Jobs.Count; j++)
                {
                    Check.IsTrue(seen.Add(car.Jobs[j].Type),
                        "Car " + car.Definition.DisplayName + " got the same job type twice");
                }
            }
        }

        private static void RarityBiasWorks()
        {
            const int Samples = 3000;

            int plainRare = CountRareOrBetter(0f, Samples, 104);
            int biasedRare = CountRareOrBetter(0.7f, Samples, 104);

            Check.IsTrue(biasedRare > plainRare * 1.5f,
                string.Format("Reputation should meaningfully raise rare spawns ({0} -> {1} of {2})",
                    plainRare, biasedRare, Samples));
        }

        private static int CountRareOrBetter(float bias, int samples, int seed)
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(seed));
            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RarityBias = bias;

            int count = 0;
            for (int i = 0; i < samples; i++)
            {
                ActiveCar car = spawner.Spawn(parameters);
                if (car.Definition.Rarity >= CarRarity.Rare) count++;
            }
            return count;
        }

        private static void RarityScalesValueAndDifficulty()
        {
            CarDefinition cheapest = null;
            CarDefinition dearest = null;

            for (int i = 0; i < CarCatalog.All.Count; i++)
            {
                CarDefinition definition = CarCatalog.All[i];
                if (definition.Rarity == CarRarity.Common && (cheapest == null || definition.BasePayout < cheapest.BasePayout)) cheapest = definition;
                if (definition.Rarity == CarRarity.Legendary && (dearest == null || definition.BasePayout > dearest.BasePayout)) dearest = definition;
            }

            Check.IsNotNull(cheapest, "Catalog has no common cars");
            Check.IsNotNull(dearest, "Catalog has no legendary cars");
            Check.IsTrue(dearest.BasePayout > cheapest.BasePayout * 10d, "Legendary cars should be worth far more");
            Check.IsTrue(CarRarity.Legendary.DifficultyScale() > CarRarity.Common.DifficultyScale(), "Legendary cars should be harder");
        }

        private static void MinigameVariety()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(105));
            HashSet<MinigameType> seen = new HashSet<MinigameType>();

            for (int i = 0; i < 200; i++)
            {
                ActiveCar car = spawner.Spawn(SpawnParameters.Default);
                for (int j = 0; j < car.Jobs.Count; j++) seen.Add(car.Jobs[j].Minigame);
            }

            Check.AreEqual(4, seen.Count, "All four mini-games should show up over a day's work");
        }

        private static void MinigameRepetitionIsRare()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(106));

            int adjacentPairs = 0;
            int repeats = 0;

            for (int i = 0; i < 600; i++)
            {
                ActiveCar car = spawner.Spawn(SpawnParameters.Default);
                for (int j = 1; j < car.Jobs.Count; j++)
                {
                    adjacentPairs++;
                    if (car.Jobs[j].Minigame == car.Jobs[j - 1].Minigame) repeats++;
                }
            }

            Check.IsTrue(adjacentPairs > 100, "Not enough samples to judge repetition");

            float repeatRate = (float)repeats / adjacentPairs;
            Check.IsTrue(repeatRate < 0.15f,
                string.Format("Back-to-back jobs repeat the same mini-game too often ({0:0.0}%)", repeatRate * 100f));
        }

        private static void JobProgressAndFlawless()
        {
            RepairJob job = new RepairJob(JobType.Brakes, MinigameType.TimingBar, 1f, 100d, 1f);

            int guard = 0;
            while (!job.IsComplete && guard < 50)
            {
                job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, "test"));
                guard++;
            }

            Check.IsTrue(job.IsComplete, "Perfect rounds should eventually finish a job");
            Check.IsTrue(job.IsFlawless, "A job of nothing but perfect rounds should be flawless");
            Check.IsTrue(job.RoundsPlayed <= 4, "A common-difficulty job should take at most a few perfect rounds");

            job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Miss, "test"));
            Check.IsFalse(job.IsFlawless, "A miss should break the flawless streak");
            Check.IsTrue(job.Progress <= 1f, "Progress must never exceed 1");
        }

        private static void ProgressNeverGoesNegative()
        {
            RepairJob job = new RepairJob(JobType.Engine, MinigameType.HoldRelease, 1f, 50d, 1f);

            for (int i = 0; i < 20; i++)
            {
                job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Damage, "test"));
                Check.InRange(job.Progress, 0d, 1d, "Progress escaped the 0-1 range after damage");
            }

            Check.AreEqual(20, job.DamagedRounds, "Damaged rounds should be counted");
        }

        private static void PatienceScales()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(107));
            CarDefinition definition = CarCatalog.FindById("family_sedan");

            SpawnParameters plain = SpawnParameters.Default;
            SpawnParameters lounge = SpawnParameters.Default;
            lounge.PatienceMultiplier = 1.5f;

            ActiveCar plainCar = spawner.SpawnSpecific(definition, plain);
            ActiveCar loungeCar = spawner.SpawnSpecific(definition, lounge);

            // Compare per-job so a different job count does not skew the comparison.
            float plainPerJob = plainCar.TotalTime / plainCar.Jobs.Count;
            float loungePerJob = loungeCar.TotalTime / loungeCar.Jobs.Count;

            Check.IsTrue(loungePerJob > plainPerJob, "The customer lounge upgrade should buy more patience");
        }
    }
}
