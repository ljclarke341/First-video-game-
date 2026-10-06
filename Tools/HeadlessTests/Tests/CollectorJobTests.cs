using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Parts;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Core.Special;
using GarageTycoon.Core.Util;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// The collector: the one where your name is on the line.
    ///
    /// Its car is ordinary on purpose. What is different is that this customer's opinion counts six
    /// times as heavily towards the garage's standing - and standing decides the calibre of car
    /// that turns up next, through the same rarity bias the Reputation upgrades have always bought.
    ///
    /// So the risk is not the car. It is the next twenty. These tests defend that, and defend the
    /// thing it must not become: a parts decision, which Performance already owns.
    /// </summary>
    public static class CollectorJobTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Phase B: the collector");

            // ----------------------------------------------------------
            // availability and identity
            // ----------------------------------------------------------

            suite.Add("The collector unlocks at the top rank", () =>
            {
                SpecialJobDefinition collector = SpecialJobCatalog.FindByType(SpecialJobType.Vip);
                Check.IsTrue(collector != null, "the collector is missing");

                Check.AreEqual(4, collector.MinRankLevel, "the collector should unlock at rank 4");
                Check.AreEqual(0, Count(SpecialJobCatalog.AvailableAt(3), SpecialJobType.Vip),
                    "a rank 3 garage was offered a collector");
                Check.AreEqual(1, Count(SpecialJobCatalog.AvailableAt(4), SpecialJobType.Vip),
                    "a rank 4 garage was not offered a collector");
            });

            suite.Add("A collector's car is an ordinary car", () =>
            {
                // The pressure is the customer, not the motoring. If this ever stops being true it
                // has turned into Restoration with a different badge.
                SpecialJobDefinition collector = SpecialJobCatalog.FindByType(SpecialJobType.Vip);

                Check.IsTrue(Math.Abs(collector.PatienceMultiplier - 1d) < 0.0001d,
                    "a collector should not be another urgent job");
                Check.IsTrue(Math.Abs(collector.WorkMultiplier - 1d) < 0.0001d,
                    "a collector's repairs should be ordinary length");
                Check.AreEqual(0, collector.ExtraJobs, "a collector should not add jobs");
                Check.AreEqual(0, collector.MinimumJobs, "a collector should have no job floor");
                Check.AreEqual(0, collector.FleetSize, "a collector is one customer");
                Check.IsFalse(collector.ExpectedGrade.HasValue,
                    "a collector must not expect a grade - Performance already owns that decision");
            });

            suite.Add("The collector's identity is whose opinion it is", () =>
            {
                SpecialJobDefinition collector = SpecialJobCatalog.FindByType(SpecialJobType.Vip);
                SpecialJobDefinition performance = SpecialJobCatalog.FindByType(SpecialJobType.Performance);

                Check.IsTrue(collector.ReputationWeight > 1d,
                    "a collector's opinion should carry more weight than anybody else's");

                // Quality matters, but noticeably less than on Performance - or the two jobs would
                // be the same decision with different artwork.
                Check.IsTrue(collector.QualityWeight > 1d, "quality should matter on a collector");
                Check.IsTrue(collector.QualityWeight < performance.QualityWeight,
                    "the collector weighs quality at " + collector.QualityWeight
                        + " and Performance at " + performance.QualityWeight
                        + "; the collector must not out-Performance Performance");
            });

            suite.Add("A collector pays more, but not enough to be an auto-take", () =>
            {
                SpecialJobDefinition collector = SpecialJobCatalog.FindByType(SpecialJobType.Vip);

                Check.IsTrue(collector.PayoutMultiplier > 1d, "a collector should be worth taking");
                Check.IsTrue(collector.PayoutMultiplier < 1.8d,
                    "a collector pays " + collector.PayoutMultiplier
                        + "x, which makes it money rather than a risk");
            });

            // ----------------------------------------------------------
            // standing: the mechanic
            // ----------------------------------------------------------

            suite.Add("A garage nobody has heard of starts neutral", () =>
            {
                GarageSimulation simulation = new GarageSimulation(15000);
                Check.IsTrue(Math.Abs(simulation.Stats.Standing) < 0.0001d,
                    "a new garage started with an opinion already formed");
            });

            suite.Add("Good work raises the garage's standing, bad work lowers it", () =>
            {
                double afterGood = StandingAfter(15100, skill: 0.98f);
                double afterBad = StandingAfter(15100, skill: 0.25f);

                Check.IsTrue(afterGood > afterBad,
                    "careful work should leave a better name than sloppy work: "
                        + afterGood.ToString("0.000") + " against " + afterBad.ToString("0.000"));

                Check.IsTrue(afterBad < 0d, "a garage doing bad work should get a bad name");
            });

            suite.Add("Competent ordinary work drifts nowhere in particular", () =>
            {
                // The neutral point is set at what an ordinary job actually scores, so a garage
                // serving ordinary customers competently keeps the name it had. Without this the
                // whole ordinary economy would creep, which is a change nobody asked for.
                double standing = StandingAfter(15200, skill: 0.8f);

                Check.IsTrue(Math.Abs(standing) < 0.25d,
                    "ordinary play drifted to " + standing.ToString("0.000")
                        + ", which moves the rarity bias by "
                        + (standing * GameBalance.StandingBiasRange).ToString("0.000"));
            });

            suite.Add("Standing is bounded, however long the session", () =>
            {
                GarageSimulation simulation = Ranked(15300, 1600000d);
                GameplayHarness.Play(simulation, 3600f, 0.99f, buyUpgrades: true);

                Check.IsTrue(simulation.Stats.Standing <= 1d && simulation.Stats.Standing >= -1d,
                    "standing left its range at " + simulation.Stats.Standing);
            });

            suite.Add("A collector moves the garage's name several times as far", () =>
            {
                // Same satisfaction, different customer. This is the whole job in one assertion.
                SpecialJobDefinition collector = SpecialJobCatalog.FindByType(SpecialJobType.Vip);

                double ordinaryMove = (0.6d - GameBalance.NeutralSatisfaction) * GameBalance.StandingStep * 1d;
                double collectorMove = (0.6d - GameBalance.NeutralSatisfaction) * GameBalance.StandingStep
                                       * collector.ReputationWeight;

                Check.IsTrue(Math.Abs(collectorMove) > Math.Abs(ordinaryMove) * 3d,
                    "a collector should matter far more than an ordinary customer: "
                        + collectorMove.ToString("0.0000") + " against " + ordinaryMove.ToString("0.0000"));

                // And in the same direction - a happy collector must never cost you your name.
                Check.IsTrue(Math.Sign(collectorMove) == Math.Sign(ordinaryMove),
                    "a collector moves standing the opposite way to everybody else");
            });

            suite.Add("Standing feeds the rarity bias the game already had", () =>
            {
                // Not a second reputation score: the same dial the Reputation upgrades buy.
                CarSpawner spawner = new CarSpawner(new XorShiftRandom(15400));

                int goodNameRare = 0, badNameRare = 0;

                for (int i = 0; i < 4000; i++)
                {
                    if (spawner.RollRarity(0.5f) >= CarRarity.Rare) goodNameRare++;
                    if (spawner.RollRarity(0f) >= CarRarity.Rare) badNameRare++;
                }

                Check.IsTrue(goodNameRare > badNameRare,
                    "a better bias should bring better cars: " + goodNameRare + " against " + badNameRare);
            });

            suite.Add("A car nobody worked on has no opinion", () =>
            {
                // Turning work away is a capacity decision. If it also moved standing it would
                // quietly become a reputation decision, and declining would stop being free.
                GarageSimulation simulation = Ranked(15500, 1600000d);
                Advance(simulation, 20f);

                ActiveCar car = simulation.Bays[0];
                Check.IsTrue(car != null, "expected a car in the bay");

                double before = simulation.Stats.Standing;

                car.Diagnosis.RevealAll(false);
                Quote.For(car).Apply(car, QuoteOption.Declined);
                Advance(simulation, 5f);

                Check.IsTrue(Math.Abs(simulation.Stats.Standing - before) < 0.0001d,
                    "turning a job away moved the garage's standing");
            });

            // ----------------------------------------------------------
            // the money, and no stacking
            // ----------------------------------------------------------

            suite.Add("A collector is worth about what its multiplier says, and no more", () =>
            {
                // The stacking audit. A collector's gross should be its payout multiplier against
                // an ordinary car and nothing else - no quiet second helping from quality, the tip
                // or the rank.
                SpecialJobDefinition collector = SpecialJobCatalog.FindByType(SpecialJobType.Vip);

                CarSpawner spawner = new CarSpawner(new XorShiftRandom(15600));

                SpawnParameters parameters = SpawnParameters.Default;
                parameters.RankLevel = 4;

                double collectorGross = 0d; int collectors = 0;
                double ordinaryGross = 0d; int ordinaries = 0;

                for (int i = 0; i < 6000; i++)
                {
                    parameters.ForcedSpecial = i % 2 == 0 ? collector : null;
                    ActiveCar car = spawner.Spawn(parameters);

                    double gross = 0d;
                    for (int j = 0; j < car.Jobs.Count; j++) gross += car.Jobs[j].Payout;

                    if (car.SpecialType == SpecialJobType.Vip) { collectorGross += gross; collectors++; }
                    else if (car.Special == null) { ordinaryGross += gross; ordinaries++; }
                }

                double ratio = (collectorGross / collectors) / (ordinaryGross / ordinaries);

                Check.IsTrue(Math.Abs(ratio - collector.PayoutMultiplier) < 0.12d,
                    "a collector grosses " + ratio.ToString("0.00")
                        + "x an ordinary car, and its multiplier is " + collector.PayoutMultiplier
                        + " - something else is multiplying too");
            });

            suite.Add("Quality swings a collector's pay harder than an ordinary car's", () =>
            {
                SpecialJobDefinition collector = SpecialJobCatalog.FindByType(SpecialJobType.Vip);

                double collectorSwing = PayFor(collector.QualityWeight, 4, 0, 0)
                                        - PayFor(collector.QualityWeight, 1, 1, 2);
                double ordinarySwing = PayFor(1d, 4, 0, 0) - PayFor(1d, 1, 1, 2);

                Check.IsTrue(collectorSwing > ordinarySwing,
                    "quality should matter more here: $" + collectorSwing.ToString("0")
                        + " against an ordinary $" + ordinarySwing.ToString("0"));
            });

            suite.Add("A collector is not secretly a parts decision", () =>
            {
                // Performance owns "buy the better part". If fitting one changed a collector's
                // outcome as much as it changes a Performance job's, the two would be one job.
                SpecialJobDefinition collector = SpecialJobCatalog.FindByType(SpecialJobType.Vip);
                SpecialJobDefinition performance = SpecialJobCatalog.FindByType(SpecialJobType.Performance);

                double collectorGap = PayWithPart(PartGrade.Performance, collector) 
                                      - PayWithPart(PartGrade.Budget, collector);
                double performanceGap = PayWithPart(PartGrade.Performance, performance)
                                        - PayWithPart(PartGrade.Budget, performance);

                Check.IsTrue(collectorGap < performanceGap,
                    "parts move a collector by $" + collectorGap.ToString("0")
                        + " and a performance job by $" + performanceGap.ToString("0")
                        + "; the collector must not be the bigger parts decision");
            });

            // ----------------------------------------------------------
            // the rest of the game
            // ----------------------------------------------------------

            suite.Add("A collector still goes through diagnosis like anybody else", () =>
            {
                ActiveCar car = SpawnCollector();
                Check.IsTrue(car != null, "could not spawn a collector");

                Check.AreEqual(0, car.Diagnosis.RevealedCount, "a collector arrived already diagnosed");
                Check.AreEqual(0, Quote.For(car).LineCount, "an uninspected collector could be quoted");

                car.Diagnosis.Skip();
                car.AcceptAllWork();

                Check.AreEqual(0, car.Diagnosis.RevealedCount, "skipping revealed a collector");
                Check.IsTrue(Math.Abs(car.Diagnosis.PayoutBonus(car.Condition) - 1d) < 0.0001d,
                    "a skipped collector was paid a diagnosis bonus");
            });

            suite.Add("The other four jobs are unchanged", () =>
            {
                SpecialJobDefinition urgent = SpecialJobCatalog.FindByType(SpecialJobType.Urgent);
                SpecialJobDefinition performance = SpecialJobCatalog.FindByType(SpecialJobType.Performance);
                SpecialJobDefinition restoration = SpecialJobCatalog.FindByType(SpecialJobType.Restoration);
                SpecialJobDefinition fleet = SpecialJobCatalog.FindByType(SpecialJobType.Fleet);

                Check.IsTrue(Math.Abs(urgent.PatienceMultiplier - 0.75d) < 0.0001d, "urgent patience moved");
                Check.IsTrue(Math.Abs(urgent.PayoutMultiplier - 1.5d) < 0.0001d, "urgent payout moved");
                Check.IsTrue(Math.Abs(performance.PayoutMultiplier - 1.15d) < 0.0001d, "performance payout moved");
                Check.IsTrue(Math.Abs(performance.QualityWeight - 1.8d) < 0.0001d, "performance quality moved");
                Check.IsTrue(Math.Abs(restoration.PayoutMultiplier - 2d) < 0.0001d, "restoration payout moved");
                Check.IsTrue(Math.Abs(restoration.WorkMultiplier - 1.5d) < 0.0001d, "restoration work moved");
                Check.IsTrue(Math.Abs(fleet.PayoutMultiplier - 0.72d) < 0.0001d, "fleet payout moved");
                Check.AreEqual(8, fleet.FleetSize, "fleet size moved");

                // And none of them tells people about it.
                Check.IsTrue(Math.Abs(urgent.ReputationWeight - 1d) < 0.0001d, "urgent gained a reputation weight");
                Check.IsTrue(Math.Abs(performance.ReputationWeight - 1d) < 0.0001d, "performance gained one");
                Check.IsTrue(Math.Abs(restoration.ReputationWeight - 1d) < 0.0001d, "restoration gained one");
                Check.IsTrue(Math.Abs(fleet.ReputationWeight - 1d) < 0.0001d, "fleet gained one");
            });

            // ----------------------------------------------------------
            // persistence
            // ----------------------------------------------------------

            suite.Add("A hard-won name survives a save", () =>
            {
                GarageSimulation simulation = Ranked(15700, 1600000d);
                GameplayHarness.Play(simulation, 300f, 0.95f);

                double standing = simulation.Stats.Standing;

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);
                Check.IsTrue(loaded != null, "the save did not load");

                Check.IsTrue(Math.Abs(loaded.Stats.Standing - standing) < 0.0001d,
                    "the garage's name changed across the save: " + standing + " to " + loaded.Stats.Standing);
            });

            suite.Add("A ruined name survives a save too", () =>
            {
                GarageSimulation simulation = Ranked(15750, 1600000d);
                GameplayHarness.Play(simulation, 300f, 0.2f);

                double standing = simulation.Stats.Standing;
                Check.IsTrue(standing < 0d, "bad work did not cost the garage its name");

                string json = GameStateSerializer.Save(simulation, 1000d);
                GarageSimulation loaded = GameStateSerializer.Load(json, 1);

                Check.IsTrue(Math.Abs(loaded.Stats.Standing - standing) < 0.0001d,
                    "a bad name was quietly forgiven by the save");
            });

            suite.Add("A save from before standing loads neutral", () =>
            {
                GarageSimulation simulation = new GarageSimulation(15800);
                Advance(simulation, 20f);

                string json = GameStateSerializer.Save(simulation, 1000d);
                string old = json.Replace("\"standing\":", "\"X\":");

                GarageSimulation loaded = GameStateSerializer.Load(old, 1);
                Check.IsTrue(loaded != null, "an old save was refused");
                Check.IsTrue(Math.Abs(loaded.Stats.Standing) < 0.0001d,
                    "an old save came back with an opinion already formed");
            });

            suite.Add("Offline catch-up cannot run the garage's name away", () =>
            {
                GarageSimulation simulation = Ranked(15900, 1600000d);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);
                Advance(simulation, 60f);

                simulation.ApplyOfflineProgress(28800d);

                Check.IsTrue(simulation.Stats.Standing <= 1d && simulation.Stats.Standing >= -1d,
                    "the catch-up drove standing to " + simulation.Stats.Standing);
                Check.IsTrue(simulation.Wallet.Cash >= 0d, "cash went negative over the catch-up");

                // And again, to be sure repeated catch-ups do not compound.
                double after = simulation.Stats.Standing;
                simulation.ApplyOfflineProgress(28800d);

                Check.IsTrue(simulation.Stats.Standing <= 1d && simulation.Stats.Standing >= -1d,
                    "a second catch-up drove standing to " + simulation.Stats.Standing);
                Check.IsTrue(Math.Abs(simulation.Stats.Standing - after) <= 1d,
                    "a second catch-up moved the name by more than the whole range");
            });

            suite.Add("A garage full of collectors still finishes cars", () =>
            {
                GarageSimulation simulation = Ranked(16000, 1600000d);
                GameplayHarness.Play(simulation, 900f, 0.85f);

                Check.IsTrue(simulation.Stats.CarsCompleted > 10,
                    "cars stopped finishing once collectors existed, got "
                        + simulation.Stats.CarsCompleted);
            });

            return suite;
        }

        // ------------------------------------------------------------------

        private static int Count(List<SpecialJobDefinition> list, SpecialJobType type)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].Type == type) count++;
            return count;
        }

        private static GarageSimulation Ranked(int seed, double earnings)
        {
            GarageSimulation simulation = new GarageSimulation(seed);
            GameplayHarness.GrantUpgrade(simulation, "workshop_rates", 10);
            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 2);
            simulation.Wallet.Earn(earnings);
            return simulation;
        }

        private static double StandingAfter(int seed, float skill)
        {
            GarageSimulation simulation = Ranked(seed, 400000d);   // rank 3: no collectors
            GameplayHarness.Play(simulation, 600f, skill);
            return simulation.Stats.Standing;
        }

        private static ActiveCar SpawnCollector()
        {
            CarSpawner spawner = new CarSpawner(new XorShiftRandom(16100));

            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RankLevel = 4;

            for (int i = 0; i < 6000; i++)
            {
                ActiveCar car = spawner.Spawn(parameters);
                if (car.SpecialType == SpecialJobType.Vip) return car;
            }
            return null;
        }

        private static RepairJob Played(PartGrade grade, int perfect, int good, int weak)
        {
            RepairJob job = new RepairJob(JobType.Engine, MinigameType.TimingBar, 1.4f, 1000d, 1f);

            for (int i = 0; i < perfect; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Perfect, ""));
            for (int i = 0; i < good; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Good, ""));
            for (int i = 0; i < weak; i++) job.ApplyResult(MinigameResult.FromOutcome(MinigameOutcome.Weak, ""));

            job.RecordPart(grade, 0d, PartsInventory.ValueOnJob(1000d, grade));
            return job;
        }

        private static double PayFor(double qualityWeight, int perfect, int good, int weak)
        {
            RepairJob job = Played(PartGrade.Standard, perfect, good, weak);
            QualityReport quality = RepairQuality.ForJob(job, CustomerMood.Ordinary, null);

            double pay = job.LabourPayout * (1d + (quality.PayMultiplier - 1d) * qualityWeight);
            if (job.IsFlawless) pay *= GameBalance.PerfectJobCashBonus;
            return MathUtil.RoundCash(pay);
        }

        private static double PayWithPart(PartGrade grade, SpecialJobDefinition definition)
        {
            RepairJob job = Played(grade, 2, 2, 0);
            QualityReport quality = RepairQuality.ForJob(job, CustomerMood.Ordinary, definition.ExpectedGrade);

            double pay = job.LabourPayout * (1d + (quality.PayMultiplier - 1d) * definition.QualityWeight);
            return MathUtil.RoundCash(pay);
        }

        private static void Advance(GarageSimulation simulation, float seconds)
        {
            int steps = (int)(seconds * 60f);
            for (int i = 0; i < steps; i++) simulation.Tick(1f / 60f);
        }
    }
}
