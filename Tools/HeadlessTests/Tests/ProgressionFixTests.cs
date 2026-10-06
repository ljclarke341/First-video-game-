using System;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>
    /// Phase C.4 implementation: the four problems the audit confirmed.
    ///
    ///   1. selling up left the old garage's reputation and fleet run behind
    ///   2. a mechanic with no bay to work in was still for sale
    ///   3. the Tool Wall charged throughput for the preview it sold
    ///   4. the sell-up progress bar slid backwards with nothing to explain it
    /// </summary>
    public static class ProgressionFixTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Phase C.4: progression fixes");

            // ---------------- 1. the sell-up ----------------

            suite.Add("Selling up clears the garage's name", () =>
            {
                GarageSimulation simulation = Sellable(out double standingBefore);

                Check.IsTrue(Math.Abs(standingBefore) > 0.0001d,
                    "the probe needs a garage with a reputation to lose, got " + standingBefore);

                Check.IsTrue(simulation.TryPrestige() > 0, "the sell-up should have gone through");

                Check.IsTrue(Math.Abs(simulation.Stats.Standing) < 0.0001d,
                    "standing should be 0 in the new garage, got " + simulation.Stats.Standing);
                Check.IsTrue(Math.Abs(simulation.Stats.Standing - standingBefore) > 0.0001d,
                    "standing must actually have changed across the sell-up");
            });

            suite.Add("Selling up ends a fleet account in progress", () =>
            {
                GarageSimulation simulation = Sellable(out double _);
                simulation.RestoreFleet(1, 5, 8);

                Check.IsTrue(simulation.FleetRemaining > 0,
                    "the probe needs a run in progress, got " + simulation.FleetRemaining);

                Check.IsTrue(simulation.TryPrestige() > 0, "the sell-up should have gone through");

                Check.IsTrue(simulation.FleetRemaining == 0,
                    "the fleet run should be cancelled, " + simulation.FleetRemaining + " vans still owed");
            });

            suite.Add("A stale fleet run cannot force vans into the next garage", () =>
            {
                // NOT a claim that the new garage is rank 0 - rank comes from all-time earnings and
                // deliberately survives a sell-up, so a wealthy player keeps their unlocks. The bug
                // was narrower: the OLD run's counter survived, and TickFleet forces a van whenever
                // it is above zero, bypassing the ordinary 12% roll entirely.
                //
                // So this garage is deliberately built poor enough that fleets are NOT unlocked,
                // which makes any van that appears necessarily a forced one.
                GarageSimulation simulation = BarelySellable();

                Check.IsTrue(simulation.RankLevel < FleetRankLevel(),
                    "the probe needs a garage below the fleet unlock, rank " + simulation.RankLevel);

                simulation.RestoreFleet(1, 5, 8);
                Check.IsTrue(simulation.TryPrestige() > 0, "the sell-up should have gone through");

                int fleetCars = 0;
                simulation.CarSpawned += car =>
                {
                    if (car.SpecialType == Core.Special.SpecialJobType.Fleet) fleetCars++;
                };

                GameplayHarness.Play(simulation, 300f, 0.85f);

                Check.IsTrue(fleetCars == 0,
                    fleetCars + " fleet vans were forced into the new garage by the old run's counter");
            });

            suite.Add("Selling up keeps what is meant to be permanent", () =>
            {
                GarageSimulation simulation = Sellable(out double _);

                double allTime = simulation.Wallet.AllTimeEarnings;
                int prestiges = simulation.Prestige.PrestigeCount;

                int awarded = simulation.TryPrestige();
                Check.IsTrue(awarded > 0, "the sell-up should have paid tokens");

                Check.IsTrue(simulation.Prestige.TokensEarned >= awarded,
                    "tokens earned should have gone up, got " + simulation.Prestige.TokensEarned);
                Check.IsTrue(simulation.Prestige.PrestigeCount == prestiges + 1,
                    "the prestige count should have gone up");
                Check.IsTrue(simulation.Wallet.AllTimeEarnings >= allTime,
                    "all-time earnings are a career figure and must not be wiped");
                Check.IsTrue(Math.Abs(GameBalance.PrestigeCashCap - 120000d) < 0.01d,
                    "the cash requirement must stay at $120,000");
            });

            // ---------------- 2. mechanics and bays ----------------

            suite.Add("A garage can only hire one mechanic fewer than it has bays", () =>
            {
                Check.IsTrue(UpgradeState.MaxMechanicsFor(1) == 0, "1 bay should support no mechanics");
                Check.IsTrue(UpgradeState.MaxMechanicsFor(2) == 1, "2 bays should support 1");
                Check.IsTrue(UpgradeState.MaxMechanicsFor(3) == 2, "3 bays should support 2");
                Check.IsTrue(UpgradeState.MaxMechanicsFor(4) == 3, "4 bays should support 3");
            });

            suite.Add("Hiring is blocked, with a reason, when there is no bay for them", () =>
            {
                GarageSimulation simulation = new GarageSimulation(31);
                simulation.Wallet.Earn(500000d);

                UpgradeDefinition hire = UpgradeCatalog.FindById("auto_mechanic");

                // One bay: nobody can be hired at all.
                Check.IsTrue(simulation.BlockedReason(hire) != null,
                    "hiring into a one-bay garage should be blocked");
                Check.IsTrue(simulation.BlockedReason(hire).Contains("bay"),
                    "the reason should mention a bay, got: " + simulation.BlockedReason(hire));
                Check.IsTrue(!simulation.TryBuyUpgrade("auto_mechanic"),
                    "and the purchase must actually fail");

                // Every bay count in turn: one more bay, one more mechanic, never more.
                for (int bays = 2; bays <= GameBalance.MaxBayCount; bays++)
                {
                    GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 1);

                    Check.IsTrue(simulation.BayCount == bays,
                        "expected " + bays + " bays, got " + simulation.BayCount);
                    Check.IsTrue(simulation.BlockedReason(hire) == null,
                        bays + " bays should allow another mechanic");
                    Check.IsTrue(simulation.TryBuyUpgrade("auto_mechanic"),
                        "hiring the " + (bays - 1) + "th mechanic should succeed at " + bays + " bays");
                    Check.IsTrue(simulation.Effects.MechanicCount == bays - 1,
                        "expected " + (bays - 1) + " working mechanics, got " + simulation.Effects.MechanicCount);
                    Check.IsTrue(simulation.BlockedReason(hire) != null,
                        bays + " bays should now be full up");
                }
            });

            suite.Add("No money is taken for a mechanic who cannot work", () =>
            {
                GarageSimulation simulation = new GarageSimulation(32);
                simulation.Wallet.Earn(500000d);
                double before = simulation.Wallet.Cash;

                Check.IsTrue(!simulation.TryBuyUpgrade("auto_mechanic"), "the purchase should fail");
                Check.IsTrue(Math.Abs(simulation.Wallet.Cash - before) < 0.01d,
                    "the wallet must be untouched, went from " + before + " to " + simulation.Wallet.Cash);
            });

            suite.Add("A save with an impossible crew loads safely and keeps the crew", () =>
            {
                // Written before the rule existed: four mechanics in a one-bay garage. It must load,
                // it must not corrupt, the extra mechanics must simply not work - and they must come
                // back the moment the bays are there, rather than having been quietly refunded.
                GarageSimulation simulation = new GarageSimulation(33);
                GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 3);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 3);
                string json = Core.Save.GameStateSerializer.Save(simulation, 1000d);

                // Strip the bays back out of the save, leaving the crew behind.
                string impossible = json.Replace("\"workshop_bays\":3", "\"workshop_bays\":0");
                Check.IsTrue(impossible != json, "the probe needs to have actually edited the save");

                GarageSimulation loaded = Core.Save.GameStateSerializer.Load(impossible, 1);
                Check.IsTrue(loaded != null, "the save must still load");
                Check.IsTrue(loaded.BayCount == 1, "expected the stripped-down garage, got " + loaded.BayCount);
                Check.IsTrue(loaded.Effects.MechanicCount == 0,
                    "no mechanic can work in a one-bay garage, got " + loaded.Effects.MechanicCount);
                Check.IsTrue(loaded.Upgrades.GetLevel("auto_mechanic") == 3,
                    "the hired crew must be remembered, not refunded; got "
                        + loaded.Upgrades.GetLevel("auto_mechanic"));

                // Open the bays again and the crew comes straight back.
                GameplayHarness.GrantUpgrade(loaded, "workshop_bays", 3);
                Check.IsTrue(loaded.Effects.MechanicCount == 3,
                    "the crew should work again at 4 bays, got " + loaded.Effects.MechanicCount);
            });

            suite.Add("A mechanic with no bay earns nothing while away", () =>
            {
                // The flip side of the rule, stated plainly so it cannot change by accident.
                GarageSimulation simulation = new GarageSimulation(34);
                GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 1);
                simulation.Tick(1f);

                Check.IsTrue(simulation.Effects.MechanicCount == 0,
                    "a one-bay garage should have no working crew");

                double before = simulation.Wallet.Cash;
                simulation.ApplyOfflineProgress(3600d);

                Check.IsTrue(Math.Abs(simulation.Wallet.Cash - before) < 0.01d,
                    "nothing should have been earned while away, gained "
                        + (simulation.Wallet.Cash - before));
            });

            // ---------------- 3. the Tool Wall ----------------

            suite.Add("The Tool Wall lengthens the preview, not the round", () =>
            {
                foreach (float bonus in new[] { 0f, 0.54f, 1.08f })
                {
                    MinigameTuning tuning = MinigameTuning.Default;
                    tuning.PreviewBonusSeconds = bonus;

                    ToolMatchMinigame tool = new ToolMatchMinigame(
                        JobType.Electrics, 1f, tuning, new Core.Util.XorShiftRandom(7));
                    RapidSequenceMinigame rapid = new RapidSequenceMinigame(
                        1f, tuning, new Core.Util.XorShiftRandom(7));

                    MinigameTuning plain = MinigameTuning.Default;
                    ToolMatchMinigame toolPlain = new ToolMatchMinigame(
                        JobType.Electrics, 1f, plain, new Core.Util.XorShiftRandom(7));
                    RapidSequenceMinigame rapidPlain = new RapidSequenceMinigame(
                        1f, plain, new Core.Util.XorShiftRandom(7));

                    Check.IsTrue(Math.Abs(tool.TimeLimit - toolPlain.TimeLimit) < 0.0001f,
                        "a tool round must be the same length at bonus " + bonus
                            + ": " + tool.TimeLimit + " against " + toolPlain.TimeLimit);
                    Check.IsTrue(Math.Abs(rapid.TimeLimit - rapidPlain.TimeLimit) < 0.0001f,
                        "a sequence round must be the same length at bonus " + bonus);

                    // The answer window is the round minus the gate, and neither moves.
                    Check.IsTrue(Math.Abs((tool.TimeLimit - tool.PreviewSeconds)
                            - (toolPlain.TimeLimit - toolPlain.PreviewSeconds)) < 0.0001f,
                        "the answer window must not shrink either");

                    // What the upgrade DOES buy.
                    Check.IsTrue(Math.Abs(tool.LabelHoldSeconds - (tool.PreviewSeconds + bonus)) < 0.0001f,
                        "the labels should linger by exactly the bonus, got " + tool.LabelHoldSeconds);
                    Check.IsTrue(Math.Abs(rapid.PatternHoldSeconds - (rapid.PreviewSeconds + bonus)) < 0.0001f,
                        "the pattern should linger by exactly the bonus");
                }
            });

            suite.Add("The twists still remove the information they are built on", () =>
            {
                MinigameTuning tuning = MinigameTuning.Default;
                tuning.PreviewBonusSeconds = 1.08f;

                ToolMatchMinigame shuffled = new ToolMatchMinigame(
                    JobType.Electrics, 1f, tuning, new Core.Util.XorShiftRandom(7));
                shuffled.SetModifier(MinigameModifier.Shuffle);

                Check.IsTrue(shuffled.LabelHoldSeconds <= shuffled.ShuffleAt + 0.0001f,
                    "labels must not survive the shuffle, or the twist is just readable: hold "
                        + shuffled.LabelHoldSeconds + " against shuffle at " + shuffled.ShuffleAt);

                RapidSequenceMinigame reversed = new RapidSequenceMinigame(
                    1f, tuning, new Core.Util.XorShiftRandom(7));
                reversed.SetModifier(MinigameModifier.Reversed);

                Check.IsTrue(Math.Abs(reversed.PatternHoldSeconds - reversed.PreviewSeconds) < 0.0001f,
                    "the pattern must not linger on a BACKWARDS round");
            });

            // ---------------- 4. the sell-up readout ----------------

            suite.Add("The readout says what the bar cannot", () =>
            {
                PrestigeState prestige = new PrestigeState();

                PrestigeReadout mid = prestige.BuildReadout(74250d, 1250000d, 1800d, GameBalance.StartingCash);
                Check.IsTrue(Math.Abs(mid.Requirement - 120000d) < 0.01d, "the requirement should be $120,000");
                Check.IsTrue(Math.Abs(mid.Remaining - 45750d) < 0.01d,
                    "it should know $45,750 is still needed, got " + mid.Remaining);
                Check.IsTrue(mid.TokensIfSoldNow == 12,
                    "selling now should pay 12 tokens, got " + mid.TokensIfSoldNow);
                Check.IsTrue(!mid.Ready, "and the sell-up should not be available yet");
                Check.IsTrue(mid.HasEstimate && mid.EstimateSeconds > 0d,
                    "half an hour of play at a healthy rate should be enough to estimate from");
            });

            suite.Add("No estimate is offered when one cannot be trusted", () =>
            {
                PrestigeState prestige = new PrestigeState();

                PrestigeReadout tooEarly = prestige.BuildReadout(500d, 900d, 30d, GameBalance.StartingCash);
                Check.IsTrue(!tooEarly.HasEstimate, "half a minute of play is not a pace");

                // Spending everything as fast as it arrives: the honest answer is no answer.
                PrestigeReadout flat = prestige.BuildReadout(
                    GameBalance.StartingCash, 900000d, 3600d, GameBalance.StartingCash);
                Check.IsTrue(!flat.HasEstimate, "a garage banking nothing must not be given an ETA");
            });

            suite.Add("Holding the cash is not the same as being ready", () =>
            {
                PrestigeState prestige = new PrestigeState();

                // Cash is there, but the garage has not traded enough to be worth a token.
                PrestigeReadout rich = prestige.BuildReadout(150000d, 5000d, 1800d, GameBalance.StartingCash);
                Check.IsTrue(!rich.Ready, "a garage worth no tokens cannot sell up");
                Check.IsTrue(rich.NeedsMoreEarnings,
                    "and the readout has to be able to say that is the reason");
                Check.IsTrue(Math.Abs(rich.Remaining) < 0.01d, "nothing more is needed in cash");

                PrestigeReadout ready = prestige.BuildReadout(150000d, 1250000d, 1800d, GameBalance.StartingCash);
                Check.IsTrue(ready.Ready, "with the history as well, it should be ready");
                Check.IsTrue(!ready.NeedsMoreEarnings, "and not blame the history");
            });

            suite.Add("Spending drives the readout backwards, and it still adds up", () =>
            {
                // The whole reason the bar was misleading. Asserted so nobody 'fixes' it by
                // quietly latching the highest value reached.
                PrestigeState prestige = new PrestigeState();

                PrestigeReadout before = prestige.BuildReadout(90000d, 500000d, 1800d, GameBalance.StartingCash);
                PrestigeReadout after = prestige.BuildReadout(30000d, 500000d, 1800d, GameBalance.StartingCash);

                Check.IsTrue(after.Fraction < before.Fraction,
                    "spending must be allowed to reduce progress; that is what the copy explains");
                Check.IsTrue(after.Remaining > before.Remaining, "and raise what is left to find");
                Check.IsTrue(Math.Abs(after.CurrentCash + after.Remaining - after.Requirement) < 0.01d,
                    "current plus remaining must always be the requirement");
            });

            return suite;
        }

        /// <summary>The rank at which fleet accounts start turning up.</summary>
        private static int FleetRankLevel()
        {
            Core.Special.SpecialJobDefinition fleet =
                Core.Special.SpecialJobCatalog.FindByType(Core.Special.SpecialJobType.Fleet);
            return fleet == null ? int.MaxValue : fleet.MinRankLevel;
        }

        /// <summary>
        /// Just barely able to sell up: the cash and a single token's worth of trading, and nothing
        /// like enough all-time earnings to have unlocked fleet accounts.
        /// </summary>
        private static GarageSimulation BarelySellable()
        {
            GarageSimulation simulation = new GarageSimulation(556);
            simulation.Wallet.Earn(GameBalance.PrestigeCashCap + 10000d);
            return simulation;
        }

        /// <summary>A garage that has earned a name, a history and enough cash to sell up.</summary>
        private static GarageSimulation Sellable(out double standing)
        {
            GarageSimulation simulation = new GarageSimulation(555);

            GameplayHarness.GrantUpgrade(simulation, "workshop_bays", 3);
            GameplayHarness.GrantUpgrade(simulation, "auto_mechanic", 2);
            simulation.Wallet.Earn(2000000d);

            // Played badly on purpose, so there is a reputation worth losing.
            GameplayHarness.Play(simulation, 600f, 0.2f);

            standing = simulation.Stats.Standing;
            return simulation;
        }
    }
}
