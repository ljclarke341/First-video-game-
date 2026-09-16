using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Economy;

namespace GarageTycoon.HeadlessTests.Tests
{
    /// <summary>Wallet, upgrade pricing/effects and the prestige rules.</summary>
    public static class EconomyTests
    {
        public static TestSuite Build()
        {
            TestSuite suite = new TestSuite("Economy");

            suite.Add("Wallet tracks earnings and refuses overspending", WalletBasics);
            suite.Add("Wallet ignores nonsense amounts", WalletRejectsJunk);
            suite.Add("Upgrade costs rise with each level", CostsScale);
            suite.Add("Upgrades stop at their max level", MaxLevelRespected);
            suite.Add("Buying upgrades changes the gameplay numbers", EffectsApply);
            suite.Add("Maxing every upgrade keeps values sane", MaxedEffectsStaySane);
            suite.Add("Unknown upgrade ids in a save are ignored", RestoreIgnoresUnknownIds);
            suite.Add("Prestige needs both cash and lifetime earnings", PrestigeRequirements);
            suite.Add("Prestige tokens permanently raise payouts", PrestigeMultiplier);

            return suite;
        }

        private static void WalletBasics()
        {
            Wallet wallet = new Wallet(100d);

            wallet.Earn(50d);
            Check.AreClose(150d, wallet.Cash, 0.001d, "Earnings should be added");
            Check.AreClose(50d, wallet.LifetimeEarnings, 0.001d, "Lifetime earnings should track only income");

            Check.IsTrue(wallet.TrySpend(120d), "Should be able to afford 120 out of 150");
            Check.AreClose(30d, wallet.Cash, 0.001d, "Spending should deduct cash");

            Check.IsFalse(wallet.TrySpend(31d), "Should not be able to overspend");
            Check.AreClose(30d, wallet.Cash, 0.001d, "A failed purchase must not change the balance");
            Check.AreClose(50d, wallet.LifetimeEarnings, 0.001d, "Spending should not reduce lifetime earnings");
        }

        private static void WalletRejectsJunk()
        {
            Wallet wallet = new Wallet(10d);

            wallet.Earn(-100d);
            wallet.Earn(0d);
            wallet.Earn(double.NaN);
            Check.AreClose(10d, wallet.Cash, 0.001d, "Negative or NaN earnings must be ignored");

            Check.IsFalse(wallet.TrySpend(-5d), "Spending a negative amount must be refused");
            Check.IsFalse(wallet.TrySpend(double.NaN), "Spending NaN must be refused");
            Check.AreClose(10d, wallet.Cash, 0.001d, "Junk spends must not change the balance");
        }

        private static void CostsScale()
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById("precision_window");
            Check.IsNotNull(definition, "precision_window upgrade is missing from the catalog");

            double level0 = definition.CostForLevel(0);
            double level1 = definition.CostForLevel(1);
            double level3 = definition.CostForLevel(3);

            Check.AreClose(definition.BaseCost, level0, 1d, "First level should cost the base cost");
            Check.IsTrue(level1 > level0, "Costs must rise per level");
            Check.IsTrue(level3 > level1 * 1.5d, "Costs should compound noticeably by level 3");
        }

        private static void MaxLevelRespected()
        {
            UpgradeState state = new UpgradeState();
            Wallet wallet = new Wallet(100000000d);
            UpgradeDefinition definition = UpgradeCatalog.FindById("workshop_bays");

            for (int i = 0; i < definition.MaxLevel; i++)
            {
                Check.IsTrue(state.TryPurchase(definition, wallet), "Should be able to buy level " + (i + 1));
            }

            Check.IsTrue(state.IsMaxed(definition), "Upgrade should now be maxed");
            Check.IsFalse(state.TryPurchase(definition, wallet), "Buying past the max level must fail");
            Check.AreEqual(definition.MaxLevel, state.GetLevel(definition.Id), "Level should stop at the max");
        }

        private static void EffectsApply()
        {
            UpgradeState state = new UpgradeState();
            Wallet wallet = new Wallet(100000000d);

            UpgradeEffects before = state.BuildEffects(1d);

            state.TryPurchase(UpgradeCatalog.FindById("precision_window"), wallet);
            state.TryPurchase(UpgradeCatalog.FindById("rep_signage"), wallet);
            state.TryPurchase(UpgradeCatalog.FindById("auto_mechanic"), wallet);
            state.TryPurchase(UpgradeCatalog.FindById("workshop_bays"), wallet);
            state.TryPurchase(UpgradeCatalog.FindById("workshop_rates"), wallet);
            state.TryPurchase(UpgradeCatalog.FindById("rep_marketing"), wallet);

            UpgradeEffects after = state.BuildEffects(1d);

            Check.IsTrue(after.WindowMultiplier > before.WindowMultiplier, "Precision should widen windows");
            Check.IsTrue(after.RarityBias > before.RarityBias, "Signage should raise the rarity bias");
            Check.AreEqual(1, after.MechanicCount, "Hiring should add a mechanic");
            Check.IsTrue(after.MechanicSkill > 0f, "A hired mechanic should have some skill");
            Check.AreEqual(before.BayCount + 1, after.BayCount, "Extra Bay should add a bay");
            Check.IsTrue(after.PayoutMultiplier > before.PayoutMultiplier, "Labour rates should raise payouts");
            Check.IsTrue(after.SpawnIntervalMultiplier < before.SpawnIntervalMultiplier, "Ads should shorten the wait");
        }

        private static void MaxedEffectsStaySane()
        {
            UpgradeState state = new UpgradeState();
            Wallet wallet = new Wallet(1000000000000d);

            for (int i = 0; i < UpgradeCatalog.All.Count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalog.All[i];
                for (int level = 0; level < definition.MaxLevel; level++)
                {
                    state.TryPurchase(definition, wallet);
                }
            }

            UpgradeEffects effects = state.BuildEffects(5d);

            Check.InRange(effects.RarityBias, 0d, 1d, "Rarity bias must stay within 0-1");
            Check.InRange(effects.BayCount, 1, GameBalance.MaxBayCount, "Bay count must stay within limits");
            Check.InRange(effects.MechanicSkill, 0d, 1d, "Mechanic skill must stay within 0-1");
            Check.InRange(effects.SpawnIntervalMultiplier, 0.3d, 1d, "Spawn interval multiplier must stay sane");
            Check.InRange(effects.ToTuning().WindowMultiplier, 0.5d, 3d, "Window multiplier must stay clamped");
            Check.InRange(effects.ToTuning().SpeedReduction, 0d, 0.6d, "Speed reduction must stay clamped");
            Check.IsTrue(effects.PayoutMultiplier > 1d, "A fully upgraded garage should pay more");
        }

        private static void RestoreIgnoresUnknownIds()
        {
            UpgradeState state = new UpgradeState();

            System.Collections.Generic.Dictionary<string, int> levels = new System.Collections.Generic.Dictionary<string, int>();
            levels["precision_window"] = 3;
            levels["upgrade_from_a_future_version"] = 99;
            levels["workshop_bays"] = 999; // absurd level from a tampered save

            state.Restore(levels);

            Check.AreEqual(3, state.GetLevel("precision_window"), "Known upgrade should restore");
            Check.AreEqual(0, state.GetLevel("upgrade_from_a_future_version"), "Unknown upgrade should be dropped");
            Check.AreEqual(UpgradeCatalog.FindById("workshop_bays").MaxLevel, state.GetLevel("workshop_bays"),
                "An over-large level should be clamped to the max");
        }

        private static void PrestigeRequirements()
        {
            PrestigeState prestige = new PrestigeState();

            Check.IsFalse(prestige.CanPrestige(0d, 0d), "A new player must not be able to prestige");
            Check.IsFalse(prestige.CanPrestige(GameBalance.PrestigeCashCap, 0d),
                "Cash alone without lifetime earnings should not qualify");
            Check.IsFalse(prestige.CanPrestige(GameBalance.PrestigeCashCap - 1d, GameBalance.PrestigeCashCap),
                "Being a dollar short should not qualify");

            Check.IsTrue(prestige.CanPrestige(GameBalance.PrestigeCashCap, GameBalance.PrestigeCashCap),
                "Hitting the cap with matching earnings should qualify");

            Check.AreEqual(0, prestige.Prestige(0d, 0d), "An ineligible prestige should award nothing");
            Check.AreEqual(0, prestige.Tokens, "An ineligible prestige must not grant tokens");
        }

        private static void PrestigeMultiplier()
        {
            PrestigeState prestige = new PrestigeState();

            double lifetime = GameBalance.LifetimeEarningsPerToken * 3d;
            int awarded = prestige.Prestige(GameBalance.PrestigeCashCap, lifetime);

            Check.AreEqual(3, awarded, "Three tokens' worth of earnings should award three tokens");
            Check.AreEqual(1, prestige.PrestigeCount, "Prestige count should increase");
            Check.IsTrue(prestige.PayoutMultiplier > 1d, "Tokens should raise the payout multiplier");
            Check.AreClose(1d + 3d * GameBalance.PrestigeBonusPerToken, prestige.PayoutMultiplier, 0.0001d,
                "Payout multiplier should match the token bonus");
        }
    }
}
