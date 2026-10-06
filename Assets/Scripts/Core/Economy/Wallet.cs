using System;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Economy
{
    /// <summary>
    /// The player's money. Deliberately the only place cash can change, so every gain and every
    /// purchase is funnelled through one auditable spot (and one event the UI can listen to).
    /// </summary>
    public sealed class Wallet
    {
        private double _cash;

        /// <summary>Fired whenever the balance changes. The argument is the NEW balance.</summary>
        public event Action<double> CashChanged;

        /// <summary>Current balance.</summary>
        public double Cash { get { return _cash; } }

        /// <summary>Total cash ever earned since the last prestige. Drives the prestige token award.</summary>
        public double LifetimeEarnings { get; private set; }

        /// <summary>Total cash ever earned across ALL prestige runs. Pure bragging rights for the stats screen.</summary>
        public double AllTimeEarnings { get; private set; }

        public Wallet(double startingCash)
        {
            _cash = startingCash < 0d ? 0d : startingCash;
        }

        /// <summary>Banks money earned from a repair. Negative or zero amounts are ignored.</summary>
        public void Earn(double amount)
        {
            if (amount <= 0d || double.IsNaN(amount)) return;

            double rounded = MathUtil.RoundCash(amount);
            _cash += rounded;
            LifetimeEarnings += rounded;
            AllTimeEarnings += rounded;

            RaiseChanged();
        }

        /// <summary>
        /// Attempts to spend money. Returns false and changes nothing if the player cannot afford it,
        /// which is why upgrade buttons can safely call this directly.
        /// </summary>
        public bool TrySpend(double amount)
        {
            if (amount < 0d || double.IsNaN(amount)) return false;
            if (_cash < amount) return false;

            _cash -= amount;
            RaiseChanged();
            return true;
        }

        /// <summary>True if the player could afford the given price right now.</summary>
        public bool CanAfford(double amount)
        {
            return _cash >= amount;
        }

        /// <summary>Wipes the balance back to the starting float for a prestige reset.</summary>
        public void ResetForPrestige(double startingCash)
        {
            _cash = startingCash;
            LifetimeEarnings = 0d;
            RaiseChanged();
        }

        /// <summary>Restores a wallet from a save file without firing "you earned money" side effects.</summary>
        public void Restore(double cash, double lifetimeEarnings, double allTimeEarnings)
        {
            _cash = cash < 0d ? 0d : cash;
            LifetimeEarnings = lifetimeEarnings < 0d ? 0d : lifetimeEarnings;
            AllTimeEarnings = allTimeEarnings < 0d ? 0d : allTimeEarnings;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Action<double> handler = CashChanged;
            if (handler != null) handler(_cash);
        }
    }
}
