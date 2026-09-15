using System.Collections.Generic;

namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// The master list of every car that can roll into the garage.
    /// Add a new entry here and it automatically joins the spawn pool - no other code needs to change.
    /// </summary>
    public static class CarCatalog
    {
        private static readonly List<CarDefinition> _all = new List<CarDefinition>
        {
            // ---------------- Common: cheap, forgiving, frequent ----------------
            new CarDefinition("rusty_ute", "Rusty Ute", CarRarity.Common, 42d, 2, 3, 26f, 11f, 1.2f, "#8D6E63",
                new[] { JobType.Tires, JobType.Exhaust, JobType.Panels, JobType.Brakes }),

            new CarDefinition("family_sedan", "Family Sedan", CarRarity.Common, 56d, 2, 3, 28f, 11f, 1.0f, "#546E7A",
                new[] { JobType.Brakes, JobType.Tires, JobType.Electrics, JobType.Diagnostics }),

            new CarDefinition("city_hatch", "City Hatchback", CarRarity.Common, 48d, 2, 3, 27f, 11f, 1.0f, "#00897B",
                new[] { JobType.Tires, JobType.Paint, JobType.Brakes, JobType.Diagnostics }),

            // ---------------- Uncommon ----------------
            new CarDefinition("delivery_van", "Delivery Van", CarRarity.Uncommon, 98d, 2, 4, 30f, 12f, 1.0f, "#ECEFF1",
                new[] { JobType.Engine, JobType.Brakes, JobType.Suspension, JobType.Exhaust }),

            new CarDefinition("sports_coupe", "Sports Coupe", CarRarity.Uncommon, 145d, 2, 4, 29f, 12f, 1.0f, "#C0392B",
                new[] { JobType.Engine, JobType.Tires, JobType.Brakes, JobType.Electrics }),

            // ---------------- Rare ----------------
            new CarDefinition("offroad_4x4", "Off-Road 4x4", CarRarity.Rare, 245d, 3, 4, 32f, 13f, 1.0f, "#6D4C41",
                new[] { JobType.Suspension, JobType.Tires, JobType.Engine, JobType.Panels, JobType.Exhaust }),

            new CarDefinition("classic_muscle", "Classic Muscle", CarRarity.Rare, 330d, 3, 4, 33f, 13f, 1.0f, "#1E88E5",
                new[] { JobType.Engine, JobType.Paint, JobType.Exhaust, JobType.Electrics, JobType.Brakes }),

            // ---------------- Epic ----------------
            new CarDefinition("luxury_limo", "Luxury Limo", CarRarity.Epic, 640d, 3, 4, 36f, 14f, 1.0f, "#212121",
                new[] { JobType.Electrics, JobType.Diagnostics, JobType.Suspension, JobType.Paint, JobType.Brakes }),

            new CarDefinition("track_supercar", "Track Supercar", CarRarity.Epic, 880d, 3, 4, 35f, 14f, 0.9f, "#F1C40F",
                new[] { JobType.Engine, JobType.Brakes, JobType.Tires, JobType.Diagnostics, JobType.Suspension }),

            // ---------------- Legendary: rare, lucrative, punishing ----------------
            new CarDefinition("concours_proto", "Concours Prototype", CarRarity.Legendary, 1850d, 4, 4, 40f, 15f, 1.0f, "#7B1FA2",
                new[] { JobType.Engine, JobType.Electrics, JobType.Diagnostics, JobType.Paint, JobType.Panels, JobType.Suspension })
        };

        /// <summary>All car blueprints, in catalog order.</summary>
        public static IReadOnlyList<CarDefinition> All { get { return _all; } }

        /// <summary>Finds a blueprint by id, or returns null if the id is unknown (e.g. an old save file).</summary>
        public static CarDefinition FindById(string id)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Id == id) return _all[i];
            }
            return null;
        }

        /// <summary>All cars of a given rarity. Used by the spawner after it has rolled a rarity.</summary>
        public static List<CarDefinition> OfRarity(CarRarity rarity)
        {
            List<CarDefinition> result = new List<CarDefinition>();
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Rarity == rarity) result.Add(_all[i]);
            }
            return result;
        }
    }
}
