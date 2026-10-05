using System.Collections.Generic;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Cars
{
    /// <summary>
    /// Turns blueprints into actual customers: rolls a rarity, picks a car of that rarity, decides which
    /// jobs it needs, assigns a mini-game to each job, and splits the payout between them.
    /// </summary>
    public sealed class CarSpawner
    {
        private readonly IRandomSource _random;
        private int _nextInstanceId = 1;

        /// <summary>Base chance of each rarity before the Reputation upgrade branch tilts the odds.</summary>
        private static readonly float[] BaseRarityWeights = { 60f, 24f, 10f, 5f, 1f };

        /// <summary>
        /// How strongly each rarity responds to the player's reputation.
        /// Common goes DOWN as reputation climbs; legendary climbs steeply.
        /// </summary>
        private static readonly float[] RarityBiasResponse = { -0.65f, 0.15f, 1.4f, 3.2f, 7f };

        public CarSpawner(IRandomSource random)
        {
            _random = random;
        }

        /// <summary>Next id that will be handed out. Saved so ids stay unique across sessions.</summary>
        public int NextInstanceId
        {
            get { return _nextInstanceId; }
            set { _nextInstanceId = value < 1 ? 1 : value; }
        }

        /// <summary>Rolls a rarity using the player's reputation bias.</summary>
        public CarRarity RollRarity(float rarityBias)
        {
            float bias = MathUtil.Clamp(rarityBias, 0f, 1f);

            float[] weights = new float[BaseRarityWeights.Length];
            float total = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                float weight = BaseRarityWeights[i] * (1f + RarityBiasResponse[i] * bias);
                if (weight < 0.01f) weight = 0.01f;
                weights[i] = weight;
                total += weight;
            }

            float roll = _random.NextFloat() * total;
            float running = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                running += weights[i];
                if (roll < running) return (CarRarity)i;
            }

            return CarRarity.Common;
        }

        /// <summary>Picks one blueprint from the given rarity, weighted by each car's SpawnWeight.</summary>
        private CarDefinition PickDefinition(CarRarity rarity)
        {
            List<CarDefinition> candidates = CarCatalog.OfRarity(rarity);

            // Defensive fallback: if a rarity tier is ever left empty, drop to whatever the catalog has.
            if (candidates.Count == 0)
            {
                candidates = new List<CarDefinition>(CarCatalog.All);
            }

            float total = 0f;
            for (int i = 0; i < candidates.Count; i++) total += candidates[i].SpawnWeight;

            float roll = _random.NextFloat() * total;
            float running = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                running += candidates[i].SpawnWeight;
                if (roll < running) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        /// <summary>Builds a complete, ready-to-repair car.</summary>
        public ActiveCar Spawn(SpawnParameters parameters)
        {
            CarRarity rarity = RollRarity(parameters.RarityBias);
            CarDefinition definition = PickDefinition(rarity);
            return SpawnSpecific(definition, parameters);
        }

        /// <summary>
        /// Builds a car from a specific blueprint, skipping the rarity roll.
        /// Handy for tests and for scripted "a VIP just called ahead" events.
        /// </summary>
        /// <summary>Rolls who is dropping the car off.</summary>
        public CustomerMood RollMood()
        {
            CustomerMood[] moods =
            {
                CustomerMood.Relaxed, CustomerMood.Ordinary, CustomerMood.Impatient,
                CustomerMood.BigTipper, CustomerMood.Vip
            };

            float total = 0f;
            for (int i = 0; i < moods.Length; i++) total += moods[i].SpawnWeight();

            float roll = _random.NextFloat() * total;
            for (int i = 0; i < moods.Length; i++)
            {
                roll -= moods[i].SpawnWeight();
                if (roll < 0f) return moods[i];
            }
            return CustomerMood.Ordinary;
        }

        public ActiveCar SpawnSpecific(CarDefinition definition, SpawnParameters parameters)
        {
            // Rolled before the jobs, because a special job can add work to the car.
            Special.SpecialJobDefinition special = Special.SpecialJobCatalog.Roll(_random, parameters.RankLevel);

            int jobCount = _random.NextInt(definition.MinJobs, definition.MaxJobs + 1);

            if (_random.Chance(parameters.ExtraJobChance)) jobCount++;
            if (special != null) jobCount += special.ExtraJobs;

            // Never ask for more distinct jobs than the car actually has on its list.
            jobCount = MathUtil.ClampInt(jobCount, 1, definition.LikelyJobs.Length);

            List<JobType> chosenTypes = PickDistinctJobs(definition, jobCount);

            float difficulty = definition.Rarity.DifficultyScale();

            // Work per job also grows with rarity, so a supercar is not just faster-paced but longer.
            float baseWork = 1f + ((int)definition.Rarity) * 0.22f;

            List<RepairJob> jobs = new List<RepairJob>();
            List<float> weights = new List<float>();
            float weightTotal = 0f;

            MinigameType? previousType = null;

            for (int i = 0; i < chosenTypes.Count; i++)
            {
                JobType jobType = chosenTypes[i];

                float work = baseWork * _random.Range(0.9f, 1.15f);
                MinigameType minigame = MinigameFactory.ChooseType(jobType, _random, previousType);
                previousType = minigame;

                float weight = jobType.PayoutWeight() * work;
                weights.Add(weight);
                weightTotal += weight;

                // Payout is filled in on the second pass once we know the total weight.
                jobs.Add(new RepairJob(jobType, minigame, work, 0d, difficulty));
            }

            CustomerMood mood = RollMood();

            // GROSS, i.e. including the part. The compensation is what lets parts cost real
            // money without moving the economy: a Standard-parts garage pays it straight back out
            // and nets exactly what it netted before parts existed.
            double payoutPool = definition.BasePayout * parameters.PayoutMultiplier
                                * mood.PayoutMultiplier() * Balance.GameBalance.PartsPayoutCompensation;

            if (special != null) payoutPool *= special.PayoutMultiplier;

            List<RepairJob> finalJobs = new List<RepairJob>();
            for (int i = 0; i < jobs.Count; i++)
            {
                double share = weightTotal <= 0f ? (1d / jobs.Count) : (weights[i] / weightTotal);
                double payout = MathUtil.RoundCash(payoutPool * share);
                if (payout < 1d) payout = 1d;

                finalJobs.Add(new RepairJob(jobs[i].Type, jobs[i].Minigame, jobs[i].WorkAmount, payout, difficulty));
            }

            // Worked out in double and narrowed once at the end. Multiplying floats step by step
            // drifted the patience timer a thousandth of a second away from the web build's, which
            // is invisible in play and still a parity failure - and parity is how the two builds
            // are kept honest.
            double patienceSeconds = definition.BasePatienceSeconds
                                     + definition.PatiencePerJobSeconds * (finalJobs.Count - 1);
            patienceSeconds *= Balance.GameBalance.PatienceScale;
            patienceSeconds *= mood.PatienceMultiplier();
            patienceSeconds *= parameters.PatienceMultiplier <= 0f ? 1d : parameters.PatienceMultiplier;
            if (special != null) patienceSeconds *= special.PatienceMultiplier;

            float patience = (float)patienceSeconds;

            ActiveCar car = new ActiveCar(_nextInstanceId, definition, finalJobs, patience, mood);
            car.SetSpecial(special);

            // The inspection reading is read OFF the jobs just chosen, so the two can never
            // disagree - the car's condition always explains the work it needs. See CarCondition
            // for why it is derived this way round rather than driving the job roll.
            Vehicle.CarCondition condition = Vehicle.CarCondition.ForCar(_nextInstanceId, finalJobs);
            car.SetCondition(condition, Vehicle.CustomerComplaint.For(condition));

            _nextInstanceId++;
            return car;
        }

        /// <summary>Picks distinct job types from the car's "likely jobs" list.</summary>
        private List<JobType> PickDistinctJobs(CarDefinition definition, int count)
        {
            List<JobType> pool = new List<JobType>(definition.LikelyJobs);
            List<JobType> chosen = new List<JobType>();

            while (chosen.Count < count && pool.Count > 0)
            {
                int index = _random.NextInt(0, pool.Count);
                chosen.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return chosen;
        }
    }
}
