using System.Collections.Generic;
using System.Text;

namespace GarageTycoon.Core.Vehicle
{
    /// <summary>
    /// Turns a condition reading into the sentence the customer actually says at the counter.
    ///
    /// This matters more than it looks. Before diagnosis the complaint is ALL the player has to go
    /// on, so it has to hint at the real faults without listing them - otherwise diagnosis is a
    /// formality. It names at most two systems and never says how bad they are.
    /// </summary>
    public static class CustomerComplaint
    {
        /// <summary>Builds the drop-off line for a car in this condition.</summary>
        public static string For(CarCondition condition)
        {
            if (condition == null) return "It just needs a once-over.";

            List<VehicleSystem> faults = condition.FaultySystems();

            // A car with nothing obviously wrong still gets a line, or the card looks broken.
            if (faults.Count == 0) return "It's due a service - have a look over it for me.";

            StringBuilder text = new StringBuilder();
            text.Append(faults[0].Complaint());

            // Two symptoms is plenty. More than that and it stops being a hint and becomes a list.
            if (faults.Count > 1)
            {
                text.Append(", and ");
                text.Append(faults[1].Complaint());
            }

            if (faults.Count > 2) text.Append(", and there's a couple of other bits");

            text.Append('.');

            return Capitalise(text.ToString());
        }

        /// <summary>
        /// Upper-cases the first letter.
        ///
        /// The phrases are written lower-case and sentence-shaped ("the brakes feel soft") so they
        /// can be joined in any order. An earlier version glued a fixed "My " on the front and then
        /// patched up the grammar with string replacements, which produced "My the brakes feel
        /// soft" for anything it had not anticipated. One capital is harder to get wrong.
        /// </summary>
        private static string Capitalise(string sentence)
        {
            if (string.IsNullOrEmpty(sentence)) return sentence;
            return char.ToUpperInvariant(sentence[0]) + sentence.Substring(1);
        }
    }
}
