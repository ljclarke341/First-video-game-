using System.Collections.Generic;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Events;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Simulation;

namespace GarageTycoon.Core.Save
{
    /// <summary>
    /// Converts a live <see cref="GarageSimulation"/> to and from a JSON string.
    ///
    /// It saves the cars currently on the forecourt as well as the money and upgrades, so closing the
    /// app mid-repair and coming back picks up exactly where you left off.
    ///
    /// Loading is deliberately defensive: any missing or unrecognised field falls back to a sensible
    /// default rather than throwing, so an old save from an earlier build still loads.
    /// </summary>
    public static class GameStateSerializer
    {
        /// <summary>Bumped whenever the save shape changes, so old files can be migrated or discarded.</summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// The oldest save this build can still read. Anything older is refused rather than loaded
        /// half-wrong, which is the kinder failure: a fresh garage beats a corrupted one.
        ///
        /// Version 1 saves ARE still readable - they simply have no condition data, and the loader
        /// derives it from the jobs each car already carries.
        /// </summary>
        public const int MinimumReadableVersion = 1;

        // ------------------------------------------------------------------
        // Saving
        // ------------------------------------------------------------------

        public static string Save(GarageSimulation simulation, double unixTimeSeconds)
        {
            JsonValue root = JsonValue.Object();

            root.Add("version", CurrentVersion);
            root.Add("savedAt", unixTimeSeconds);

            // --- money ---
            root.Add("cash", simulation.Wallet.Cash);
            root.Add("lifetimeEarnings", simulation.Wallet.LifetimeEarnings);
            root.Add("allTimeEarnings", simulation.Wallet.AllTimeEarnings);

            // --- settings ---
            root.Add("relaxedPace", simulation.RelaxedPace);

            // --- prestige ---
            root.Add("prestigeTokens", simulation.Prestige.TokensEarned);
            root.Add("prestigeCount", simulation.Prestige.PrestigeCount);

            JsonValue perks = JsonValue.Object();
            foreach (KeyValuePair<string, int> pair in simulation.Prestige.PerksToDictionary())
            {
                perks.Add(pair.Key, pair.Value);
            }
            root.Add("perks", perks);

            // --- the work streak ---
            root.Add("combo", simulation.Combo.Streak);
            root.Add("comboBest", simulation.Combo.BestStreak);
            root.Add("calmCooldown", simulation.CalmCooldownRemaining);

            // --- upgrades ---
            JsonValue upgrades = JsonValue.Object();
            foreach (KeyValuePair<string, int> pair in simulation.Upgrades.ToDictionary())
            {
                upgrades.Add(pair.Key, pair.Value);
            }
            root.Add("upgrades", upgrades);

            // --- random state, so the sequence of cars continues rather than restarting ---
            root.Add("randomState", simulation.Random.State);
            root.Add("nextCarId", simulation.Spawner.NextInstanceId);
            root.Add("spawnTimer", simulation.SpawnTimer);

            // --- events ---
            JsonValue events = JsonValue.Object();
            events.Add("activeId", simulation.Events.Active == null ? 0 : (int)simulation.Events.Active.Id);
            events.Add("activeRemaining", simulation.Events.ActiveRemaining);
            events.Add("timeUntilNext", simulation.Events.TimeUntilNext);
            root.Add("events", events);

            // --- statistics ---
            GameStats stats = simulation.Stats;
            JsonValue statsJson = JsonValue.Object();
            statsJson.Add("carsCompleted", stats.CarsCompleted);
            statsJson.Add("carsLost", stats.CarsLost);
            statsJson.Add("jobsCompleted", stats.JobsCompleted);
            statsJson.Add("roundsPlayed", stats.RoundsPlayed);
            statsJson.Add("perfectRounds", stats.PerfectRounds);
            statsJson.Add("damagedRounds", stats.DamagedRounds);
            statsJson.Add("bestCarPayout", stats.BestCarPayout);
            statsJson.Add("playTimeSeconds", stats.PlayTimeSeconds);
            statsJson.Add("bestStreak", stats.BestStreak);
            root.Add("stats", statsJson);

            // --- cars on the forecourt ---
            JsonValue cars = JsonValue.Array();

            for (int i = 0; i < simulation.WaitingCars.Count; i++)
            {
                cars.Append(SaveCar(simulation.WaitingCars[i], -1));
            }

            for (int bayIndex = 0; bayIndex < simulation.Bays.Count; bayIndex++)
            {
                ActiveCar car = simulation.Bays[bayIndex];
                if (car != null) cars.Append(SaveCar(car, bayIndex));
            }

            root.Add("cars", cars);

            return root.ToString();
        }

        private static JsonValue SaveCar(ActiveCar car, int bayIndex)
        {
            JsonValue json = JsonValue.Object();

            json.Add("id", car.InstanceId);
            json.Add("def", car.Definition.Id);
            json.Add("bay", bayIndex);
            json.Add("mood", (int)car.Mood);
            json.Add("timeRemaining", car.TimeRemaining);
            json.Add("totalTime", car.TotalTime);
            json.Add("earned", car.EarnedSoFar);
            json.Add("complaint", car.Complaint);

            // Condition is SAVED rather than re-derived: a car whose engine read 41% before you
            // closed the app has to still read 41% when you come back.
            JsonValue condition = JsonValue.Array();
            int[] percents = car.Condition.ToPercents();
            for (int i = 0; i < percents.Length; i++) condition.Append(JsonValue.Number(percents[i]));
            json.Add("condition", condition);

            JsonValue jobs = JsonValue.Array();
            for (int i = 0; i < car.Jobs.Count; i++)
            {
                RepairJob job = car.Jobs[i];
                JsonValue jobJson = JsonValue.Object();
                jobJson.Add("type", (int)job.Type);
                jobJson.Add("game", (int)job.Minigame);
                jobJson.Add("work", job.WorkAmount);
                jobJson.Add("pay", job.Payout);
                jobJson.Add("diff", job.Difficulty);
                jobJson.Add("progress", job.Progress);
                jobJson.Add("rounds", job.RoundsPlayed);
                jobJson.Add("perfect", job.PerfectRounds);
                jobJson.Add("damaged", job.DamagedRounds);
                jobJson.Add("accepted", job.IsAccepted);
                jobs.Append(jobJson);
            }
            json.Add("jobs", jobs);

            // --- what the garage has worked out about this car ---
            JsonValue diagnosis = JsonValue.Object();
            diagnosis.Add("mask", car.Diagnosis.RevealedMask());
            diagnosis.Add("started", car.Diagnosis.HasStarted);
            diagnosis.Add("skipped", car.Diagnosis.WasSkipped);
            diagnosis.Add("accuracy", car.Diagnosis.Accuracy);

            JsonValue actions = JsonValue.Array();
            for (int i = 0; i < car.Diagnosis.ActionsRun.Count; i++)
            {
                actions.Append(JsonValue.Number((int)car.Diagnosis.ActionsRun[i]));
            }
            diagnosis.Add("actions", actions);

            json.Add("diagnosis", diagnosis);

            return json;
        }

        // ------------------------------------------------------------------
        // Loading
        // ------------------------------------------------------------------

        /// <summary>
        /// Rebuilds a simulation from JSON. Returns null when the text cannot be parsed at all,
        /// which tells the caller to start a brand new game.
        /// </summary>
        public static GarageSimulation Load(string json, int fallbackSeed)
        {
            JsonValue root = JsonValue.Parse(json);
            if (root == null || root.Type != JsonType.Object) return null;

            // A save with no version at all is treated as version 1, which is what the first
            // builds wrote. One written by a NEWER build than this is refused: guessing at a shape
            // we have never seen is how saves get silently mangled.
            int version = root["version"].AsInt(1);
            if (version < MinimumReadableVersion || version > CurrentVersion) return null;

            GarageSimulation simulation = new GarageSimulation(fallbackSeed);

            simulation.RelaxedPace = root["relaxedPace"].AsBool(false);

            // --- prestige first: it feeds the payout multiplier used by BuildEffects ---
            Dictionary<string, int> perkLevels = new Dictionary<string, int>();
            foreach (KeyValuePair<string, JsonValue> pair in root["perks"].Fields)
            {
                perkLevels[pair.Key] = pair.Value.AsInt(0);
            }

            simulation.Prestige.Restore(
                root["prestigeTokens"].AsInt(0),
                root["prestigeCount"].AsInt(0),
                perkLevels);

            simulation.Combo.Restore(root["combo"].AsInt(0), root["comboBest"].AsInt(0));
            simulation.CalmCooldownRemaining = root["calmCooldown"].AsFloat(0f);

            // --- upgrades ---
            Dictionary<string, int> levels = new Dictionary<string, int>();
            JsonValue upgrades = root["upgrades"];
            foreach (KeyValuePair<string, JsonValue> pair in upgrades.Fields)
            {
                levels[pair.Key] = pair.Value.AsInt(0);
            }
            simulation.Upgrades.Restore(levels);

            // Effects (and therefore the bay count) must be current before cars are put back in bays.
            simulation.RefreshEffects();

            // --- money ---
            simulation.Wallet.Restore(
                root["cash"].AsDouble(Balance.GameBalance.StartingCash),
                root["lifetimeEarnings"].AsDouble(0d),
                root["allTimeEarnings"].AsDouble(0d));

            // --- random + spawning ---
            if (root.Has("randomState"))
            {
                simulation.Random.State = (uint)root["randomState"].AsDouble(0d);
            }
            simulation.Spawner.NextInstanceId = root["nextCarId"].AsInt(1);
            simulation.SpawnTimer = root["spawnTimer"].AsFloat(2f);

            // --- events ---
            JsonValue events = root["events"];
            int activeEventId = events["activeId"].AsInt(0);
            if (activeEventId != 0)
            {
                GameEventDefinition definition = GameEventCatalog.FindById((GameEventId)activeEventId);
                if (definition != null) simulation.Events.StartEvent(definition);
            }
            simulation.Events.TimeUntilNext = events["timeUntilNext"].AsFloat(90f);

            // --- statistics ---
            JsonValue stats = root["stats"];
            simulation.Stats.CarsCompleted = stats["carsCompleted"].AsInt(0);
            simulation.Stats.CarsLost = stats["carsLost"].AsInt(0);
            simulation.Stats.JobsCompleted = stats["jobsCompleted"].AsInt(0);
            simulation.Stats.RoundsPlayed = stats["roundsPlayed"].AsInt(0);
            simulation.Stats.PerfectRounds = stats["perfectRounds"].AsInt(0);
            simulation.Stats.DamagedRounds = stats["damagedRounds"].AsInt(0);
            simulation.Stats.BestCarPayout = stats["bestCarPayout"].AsDouble(0d);
            simulation.Stats.PlayTimeSeconds = stats["playTimeSeconds"].AsFloat(0f);
            simulation.Stats.BestStreak = stats["bestStreak"].AsInt(0);

            // --- cars ---
            JsonValue cars = root["cars"];
            for (int i = 0; i < cars.Count; i++)
            {
                JsonValue carJson = cars[i];
                ActiveCar car = LoadCar(carJson);
                if (car == null) continue;

                int bayIndex = carJson["bay"].AsInt(-1);
                if (bayIndex >= 0)
                {
                    simulation.RestoreBayCar(car, bayIndex);
                    car.RestoreState(CarState.InBay, carJson["timeRemaining"].AsFloat(car.TotalTime), bayIndex, carJson["earned"].AsDouble(0d));
                }
                else
                {
                    simulation.RestoreWaitingCar(car);
                    car.RestoreState(CarState.Waiting, carJson["timeRemaining"].AsFloat(car.TotalTime), -1, carJson["earned"].AsDouble(0d));
                }
            }

            simulation.SyncRankBaseline();
            return simulation;
        }

        /// <summary>
        /// Reads a car's condition back, or invents a believable one for a save written before
        /// conditions existed.
        ///
        /// The fallback goes through CarCondition.ForCar, which seeds itself from the car's id.
        /// So an old car gets back exactly the reading it would have been given on the day it
        /// spawned, and the simulation's own random stream is left untouched.
        /// </summary>
        private static Vehicle.CarCondition LoadCondition(JsonValue json, int instanceId, List<RepairJob> jobs)
        {
            JsonValue stored = json["condition"];

            if (stored.Count > 0)
            {
                int[] percents = new int[stored.Count];
                for (int i = 0; i < stored.Count; i++) percents[i] = stored[i].AsInt(100);
                return Vehicle.CarCondition.FromPercents(percents);
            }

            return Vehicle.CarCondition.ForCar(instanceId, jobs);
        }

        /// <summary>
        /// Restores what had been worked out about a car.
        ///
        /// A save written before diagnosis existed has no entry here. Those cars are marked fully
        /// revealed and skipped: the player could already see all their jobs in the build that
        /// wrote the save, so hiding them now would be a nasty surprise on load - and marking
        /// them skipped means nobody is retroactively paid a diagnosis bonus they never earned.
        /// </summary>
        private static void LoadDiagnosis(ActiveCar car, JsonValue json)
        {
            if (json.Type != JsonType.Object)
            {
                car.Diagnosis.RevealAll(true);
                return;
            }

            List<int> actions = new List<int>();
            JsonValue actionsJson = json["actions"];
            for (int i = 0; i < actionsJson.Count; i++) actions.Add(actionsJson[i].AsInt(-1));

            car.Diagnosis.Restore(
                json["mask"].AsInt(0),
                json["started"].AsBool(false),
                json["skipped"].AsBool(false),
                json["accuracy"].AsFloat(0f),
                actions);
        }

        private static ActiveCar LoadCar(JsonValue json)
        {
            CarDefinition definition = CarCatalog.FindById(json["def"].AsString(string.Empty));

            // A car type that no longer exists in the catalog is simply dropped.
            if (definition == null) return null;

            JsonValue jobsJson = json["jobs"];
            if (jobsJson.Count == 0) return null;

            List<RepairJob> jobs = new List<RepairJob>();

            for (int i = 0; i < jobsJson.Count; i++)
            {
                JsonValue jobJson = jobsJson[i];

                RepairJob job = new RepairJob(
                    (JobType)jobJson["type"].AsInt(0),
                    (MinigameType)jobJson["game"].AsInt(0),
                    jobJson["work"].AsFloat(1f),
                    jobJson["pay"].AsDouble(1d),
                    jobJson["diff"].AsFloat(1f));

                job.RestoreProgress(
                    jobJson["progress"].AsFloat(0f),
                    jobJson["rounds"].AsInt(0),
                    jobJson["perfect"].AsInt(0),
                    jobJson["damaged"].AsInt(0),
                    // Defaults to accepted, so every job in a save written before quotes existed
                    // comes back as work the customer wants doing - exactly as it behaved then.
                    jobJson["accepted"].AsBool(true));

                jobs.Add(job);
            }

            float totalTime = json["totalTime"].AsFloat(30f);
            CustomerMood mood = (CustomerMood)json["mood"].AsInt((int)CustomerMood.Ordinary);
            int instanceId = json["id"].AsInt(1);

            ActiveCar car = new ActiveCar(instanceId, definition, jobs, totalTime, mood);
            car.SetCondition(LoadCondition(json, instanceId, jobs), json["complaint"].AsString(string.Empty));
            LoadDiagnosis(car, json["diagnosis"]);

            // A version 1 save has no complaint either, so write one from the condition we just
            // derived. Without this an old car shows a blank line where its problem should be.
            if (string.IsNullOrEmpty(car.Complaint))
            {
                car.SetCondition(car.Condition, Vehicle.CustomerComplaint.For(car.Condition));
            }

            return car;
        }
    }
}
