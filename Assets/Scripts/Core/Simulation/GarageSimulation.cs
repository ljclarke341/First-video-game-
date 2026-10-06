using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Diagnosis;
using GarageTycoon.Core.Economy;
using GarageTycoon.Core.Events;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Util;

namespace GarageTycoon.Core.Simulation
{
    /// <summary>
    /// THE GAME. Everything that actually happens in Garage Tycoon happens in here: cars arrive,
    /// queue, take a bay, get repaired round by round, pay out or storm off; upgrades are bought;
    /// mechanics work on their own; events come and go.
    ///
    /// It contains no Unity code whatsoever and is driven purely by Tick(deltaTime), which is why the
    /// automated tests can run entire eight-hour play sessions in a fraction of a second.
    /// </summary>
    public sealed class GarageSimulation
    {
        // ------------------------------------------------------------------
        // Owned systems
        // ------------------------------------------------------------------

        public Wallet Wallet { get; private set; }
        public UpgradeState Upgrades { get; private set; }
        public PrestigeState Prestige { get; private set; }
        public RandomEventSystem Events { get; private set; }
        public CarSpawner Spawner { get; private set; }
        public GameStats Stats { get; private set; }

        /// <summary>What is on the shelf, and what the garage fits by default.</summary>
        public Parts.PartsInventory Inventory { get; private set; }

        /// <summary>The work streak. Landing rounds back to back pays more.</summary>
        public ComboTracker Combo { get; private set; }

        /// <summary>
        /// The garage's standing, earned by all-time earnings and therefore surviving a sell-up.
        /// Each rank unlocks a new twist on one of the mini-games.
        /// </summary>
        public int RankLevel { get { return Economy.GarageRank.LevelFor(Wallet.AllTimeEarnings); } }

        /// <summary>Raised when the garage reaches a new rank, with the new level.</summary>
        public event Action<int> RankedUp;

        private int _lastSeenRank;

        /// <summary>The random source everything shares, so one seed reproduces an entire session.</summary>
        public XorShiftRandom Random { get; private set; }

        // ------------------------------------------------------------------
        // Live state
        // ------------------------------------------------------------------

        /// <summary>Cars parked outside waiting for a bay.</summary>
        public IReadOnlyList<ActiveCar> WaitingCars { get { return _waiting; } }

        /// <summary>One slot per bay. A null slot is an empty bay.</summary>
        public IReadOnlyList<ActiveCar> Bays { get { return _bays; } }

        /// <summary>The player's current job, or null when they are not working on anything.</summary>
        public WorkSession PlayerSession { get; private set; }

        /// <summary>Sessions belonging to hired mechanics.</summary>
        public IReadOnlyList<WorkSession> MechanicSessions { get { return _mechanicSessions; } }

        /// <summary>Cached upgrade effects. Recalculated whenever something is bought.</summary>
        public UpgradeEffects Effects { get; private set; }

        /// <summary>
        /// An accessibility setting, not a difficulty one: it buys reading time and slows the
        /// moving parts, and changes nothing about what anything pays. People read at different
        /// speeds, and a mini-game nobody can read is not a skill test - it is a coin flip.
        /// </summary>
        public bool RelaxedPace { get; set; }

        /// <summary>The mini-game tuning to build rounds with, including the relaxed-pace setting.</summary>
        public MinigameTuning CurrentTuning()
        {
            MinigameTuning tuning = Effects.ToTuning();

            if (RelaxedPace)
            {
                tuning.WindowMultiplier *= 1.25f;
                tuning.PreviewBonusSeconds += 1.5f;
                tuning.SpeedReduction += 0.25f;
            }

            return tuning.Sanitised();
        }

        /// <summary>Seconds until the next car rolls in.</summary>
        public float TimeUntilNextCar { get { return _spawnTimer; } }

        /// <summary>How many bays the garage currently has.</summary>
        public int BayCount { get { return _bays.Count; } }

        /// <summary>Vehicles still to come on the current fleet run, 0 when none is running.</summary>
        public int FleetRemaining { get { return _fleetRemaining; } }

        /// <summary>How many vehicles the current run was for.</summary>
        public int FleetSize { get { return _fleetSize; } }

        /// <summary>The id of the run in progress, 0 when none is.</summary>
        public int FleetBatchId { get { return _fleetBatchId; } }

        private readonly List<ActiveCar> _waiting = new List<ActiveCar>();
        private readonly List<ActiveCar> _bays = new List<ActiveCar>();
        private readonly List<WorkSession> _mechanicSessions = new List<WorkSession>();

        private float _spawnTimer;

        // A fleet run, in its entirety. Three integers, deliberately: the brief for this was a
        // batch, not a contract system, and anything more would have to be saved, migrated and
        // reasoned about during offline catch-up for no gain.
        private int _fleetBatchId;
        private int _fleetRemaining;
        private int _fleetSize;
        private int _nextFleetBatchId = 1;

        // ------------------------------------------------------------------
        // Events for the UI layer to subscribe to
        // ------------------------------------------------------------------

        public event Action<ActiveCar> CarSpawned;
        public event Action<ActiveCar, int> CarEnteredBay;
        public event Action<ActiveCar, double> CarCompleted;
        public event Action<ActiveCar> CarLeftAngry;
        public event Action<ActiveCar, RepairJob, double> JobCompleted;
        public event Action<WorkSession, MinigameResult> RoundResolved;
        public event Action<WorkSession> RoundStarted;
        public event Action<GameEventDefinition> EventStarted;
        public event Action<GameEventDefinition> EventEnded;
        public event Action<int> PrestigePerformed;

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        public GarageSimulation(int seed)
        {
            Random = new XorShiftRandom(seed);
            // The Opening Float perk is applied by the save loader once perks are known; a fresh
            // game with no perks simply starts on the base float.
            Wallet = new Wallet(GameBalance.StartingCash);
            Upgrades = new UpgradeState();
            Prestige = new PrestigeState();
            Spawner = new CarSpawner(Random);
            Events = new RandomEventSystem(Random);
            Stats = new GameStats();
            Inventory = new Parts.PartsInventory();
            Combo = new ComboTracker();

            Events.EventStarted += HandleEventStarted;
            Events.EventEnded += HandleEventEnded;

            RefreshEffects();
            ResizeBays();

            _lastSeenRank = RankLevel;
            _spawnTimer = 2f; // A car is waiting almost immediately so a new player has something to do.
        }

        /// <summary>Re-syncs the rank baseline, so loading a save does not replay old rank-ups.</summary>
        public void SyncRankBaseline()
        {
            _lastSeenRank = RankLevel;
        }

        /// <summary>Recomputes cached upgrade effects. Call after any purchase or prestige.</summary>
        public void RefreshEffects()
        {
            Effects = Upgrades.BuildEffects(Prestige.PayoutMultiplier);

            // Permanent perks stack on top of whatever this run has bought.
            Effects = ApplyPerks(Effects);

            Combo.Cap = ComboTracker.DefaultCap + Prestige.ComboCapBonus;

            ResizeBays();
        }

        /// <summary>Folds the permanent prestige perks into this run's upgrade effects.</summary>
        private UpgradeEffects ApplyPerks(UpgradeEffects effects)
        {
            effects.BayCount = MathUtil.ClampInt(
                effects.BayCount + Prestige.StartingBayBonus, 1, GameBalance.MaxBayCount);

            effects.PatienceMultiplier += Prestige.PatienceBonus;

            // The crew is capped by the bays, and THIS is the only place the bay count is final -
            // Inherited Lease has just been added to it. A mechanic can only work a car already in
            // a bay that nobody else holds, and the player permanently occupies one bay, so the
            // last mechanic hired measured 0.0% busy whenever the crew matched the bay count.
            //
            // Clamped rather than refunded: the stored level is untouched, so a save carrying an
            // impossible crew keeps it and that mechanic starts working the moment a bay opens.
            effects.MechanicCount = MathUtil.ClampInt(
                effects.MechanicCount, 0, UpgradeState.MaxMechanicsFor(effects.BayCount));

            if (effects.MechanicCount > 0)
            {
                effects.MechanicSkill = MathUtil.Clamp(
                    effects.MechanicSkill + Prestige.MechanicSkillBonus, 0f, UpgradeState.MechanicMaxSkill);
            }
            else
            {
                // No crew that can actually work means no crew skill to report.
                effects.MechanicSkill = 0f;
            }

            return effects;
        }

        /// <summary>Grows the bay list when an Extra Bay upgrade is bought.</summary>
        private void ResizeBays()
        {
            int target = MathUtil.ClampInt(Effects.BayCount, 1, GameBalance.MaxBayCount);
            while (_bays.Count < target) _bays.Add(null);

            // Bays are never removed during play - nothing in the game reduces the count -
            // but if it ever did, cars in the doomed bays go back to the front of the queue.
            while (_bays.Count > target)
            {
                int last = _bays.Count - 1;
                ActiveCar evicted = _bays[last];
                if (evicted != null)
                {
                    CancelSessionsForCar(evicted);
                    evicted.RestoreState(CarState.Waiting, evicted.TimeRemaining, -1, evicted.EarnedSoFar);
                    _waiting.Insert(0, evicted);
                }
                _bays.RemoveAt(last);
            }
        }

        // ------------------------------------------------------------------
        // Main loop
        // ------------------------------------------------------------------

        /// <summary>
        /// Advances the whole game by one frame.
        /// Order matters: work is resolved BEFORE patience is charged, so a job that lands on the very
        /// last tick still counts. That is a deliberate "benefit of the doubt" to the player.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            // Guard against giant frame spikes (app resumed, editor paused) wrecking the pacing.
            if (deltaTime > 0.5f) deltaTime = 0.5f;

            Stats.PlayTimeSeconds += deltaTime;

            if (CalmCooldownRemaining > 0f)
            {
                CalmCooldownRemaining -= deltaTime;
                if (CalmCooldownRemaining < 0f) CalmCooldownRemaining = 0f;
            }

            Combo.Tick(deltaTime);
            Inventory.TickDeliveries(deltaTime, Effects.MechanicCount);

            // Ranking up is permanent progression, so it is checked wherever earnings move.
            int rank = RankLevel;
            if (rank > _lastSeenRank)
            {
                _lastSeenRank = rank;
                Action<int> rankHandler = RankedUp;
                if (rankHandler != null) rankHandler(rank);
            }

            Events.Tick(deltaTime);
            TickSpawning(deltaTime);
            TickFleet();
            AssignCarsToBays();
            AssignMechanics();

            TickDiagnosis(deltaTime);
            TickSession(PlayerSession, deltaTime);

            for (int i = _mechanicSessions.Count - 1; i >= 0; i--)
            {
                TickSession(_mechanicSessions[i], deltaTime);
            }

            RetireFinishedCars();
            TickPatience(deltaTime);
        }

        /// <summary>
        /// Safety net: pays out and clears any car in a bay whose jobs are all done.
        /// Normally ResolveRound completes a car the instant its last job lands, but this catches the
        /// cases where progress arrived some other way (a restored save, a future instant-finish perk)
        /// so a finished car can never sit in a bay blocking it forever.
        /// </summary>
        private void RetireFinishedCars()
        {
            for (int bayIndex = 0; bayIndex < _bays.Count; bayIndex++)
            {
                ActiveCar car = _bays[bayIndex];
                if (car == null) continue;
                if (car.State != CarState.InBay) continue;
                if (!car.AllJobsComplete) continue;

                CompleteCar(car);
            }
        }

        private void TickSpawning(float deltaTime)
        {
            _spawnTimer -= deltaTime;
            if (_spawnTimer > 0f) return;

            _spawnTimer = CurrentSpawnInterval();

            // The forecourt is full: the customer drives past rather than queueing forever.
            if (_waiting.Count >= GameBalance.MaxQueuedCars) return;

            SpawnCar();
        }

        /// <summary>
        /// Keeps one vehicle of the current fleet run on the forecourt.
        ///
        /// This is the whole upside of a fleet, and the whole cost of one. The run's vehicles come
        /// ON TOP of the ordinary trickle rather than instead of it, so a garage with bays going
        /// spare gets work it would not otherwise have had. A garage that is already full gets a
        /// queue it cannot serve, and the ordinary customers behind it drive past.
        ///
        /// Only ever one at a time, so a run can never flood the forecourt, and never when the
        /// queue is full - a fleet customer waits their turn like anybody else.
        /// </summary>
        private void TickFleet()
        {
            if (_fleetRemaining <= 0) return;
            if (_waiting.Count >= GameBalance.MaxQueuedCars) return;

            for (int i = 0; i < _waiting.Count; i++)
            {
                if (_waiting[i].FleetBatchId == _fleetBatchId) return;      // one is already here
            }

            Special.SpecialJobDefinition fleet =
                Special.SpecialJobCatalog.FindByType(Special.SpecialJobType.Fleet);
            if (fleet == null) { _fleetRemaining = 0; return; }

            SpawnCar(fleet);
        }

        /// <summary>
        /// Puts a fleet run back after a load. The vehicles themselves are restored separately,
        /// like any other car; this is only the "and there are two more to come" part.
        /// </summary>
        public void RestoreFleet(int batchId, int remaining, int size)
        {
            _fleetBatchId = batchId < 0 ? 0 : batchId;
            _fleetRemaining = remaining < 0 ? 0 : remaining;
            _fleetSize = size < 0 ? 0 : size;

            // Ids must never be handed out twice, or a reloaded run and a new one would share one.
            if (_fleetBatchId >= _nextFleetBatchId) _nextFleetBatchId = _fleetBatchId + 1;
        }

        /// <summary>
        /// Ends the current fleet run. The customer takes the rest of their vehicles elsewhere.
        ///
        /// Called when one of the run's vehicles is turned down, which is what makes declining a
        /// fleet mean something: you are not refusing one van, you are refusing the account.
        /// </summary>
        public void CancelFleet()
        {
            _fleetRemaining = 0;
            _fleetBatchId = 0;
            _fleetSize = 0;
        }

        /// <summary>Seconds between arrivals right now, including upgrades and any active event.</summary>
        public float CurrentSpawnInterval()
        {
            EventModifiers modifiers = Events.CurrentModifiers;
            float interval = GameBalance.BaseSpawnIntervalSeconds
                             * Effects.SpawnIntervalMultiplier
                             * modifiers.SpawnIntervalMultiplier;
            return MathUtil.Clamp(interval, GameBalance.MinSpawnIntervalSeconds, 60f);
        }

        /// <summary>Rolls and queues one new customer. Public so tests can force arrivals.</summary>
        public ActiveCar SpawnCar() { return SpawnCar(null); }

        /// <param name="forcedSpecial">
        /// Used for the second and later vehicles of a fleet run, which are the same customer
        /// rather than a fresh roll.
        /// </param>
        public ActiveCar SpawnCar(Special.SpecialJobDefinition forcedSpecial)
        {
            EventModifiers modifiers = Events.CurrentModifiers;
            SpawnParametersBundle bundle = Effects.ToSpawnValues();

            SpawnParameters parameters = SpawnParameters.Default;
            // Standing rides on top of what the Reputation upgrades bought. Positive standing
            // brings better cars in, negative keeps them away - the same dial, now earned as well
            // as bought. Clamped by the spawner, so this can never drive it out of range.
            parameters.RarityBias = bundle.RarityBias
                + (float)(Stats.Standing * GameBalance.StandingBiasRange);
            parameters.PatienceMultiplier = bundle.PatienceMultiplier;
            parameters.PayoutMultiplier = bundle.PayoutMultiplier * modifiers.PayoutMultiplier;
            parameters.ExtraJobChance = modifiers.ExtraJobChance;
            parameters.RankLevel = RankLevel;
            parameters.ForcedSpecial = forcedSpecial;

            ActiveCar car = Spawner.Spawn(parameters);

            // A fleet customer who turns up while a run is already going is just an ordinary
            // customer: one account at a time keeps the decision legible and the queue sane.
            if (car.Special != null && car.Special.FleetSize > 0)
            {
                // No "and this was not forced" guard here: TickFleet only ever forces a van
                // while a run is already in progress, so the open-a-run branch cannot be reached
                // by a continuation. Guarding on it as well meant a deliberately started run
                // never opened, and a ninth van could then arrive on a run of eight.
                if (_fleetRemaining <= 0)
                {
                    _fleetBatchId = _nextFleetBatchId++;
                    _fleetSize = car.Special.FleetSize;
                    _fleetRemaining = _fleetSize;
                }

                if (_fleetRemaining > 0)
                {
                    car.SetFleet(_fleetBatchId, _fleetSize - _fleetRemaining + 1, _fleetSize);
                    _fleetRemaining--;
                }
                else
                {
                    car.SetSpecial(null);
                }
            }

            _waiting.Add(car);

            Action<ActiveCar> handler = CarSpawned;
            if (handler != null) handler(car);

            return car;
        }

        /// <summary>Moves waiting cars into any free bays, oldest customer first.</summary>
        private void AssignCarsToBays()
        {
            for (int bayIndex = 0; bayIndex < _bays.Count; bayIndex++)
            {
                if (_bays[bayIndex] != null) continue;
                if (_waiting.Count == 0) return;

                ActiveCar car = _waiting[0];
                _waiting.RemoveAt(0);

                _bays[bayIndex] = car;
                car.MoveToBay(bayIndex);

                Action<ActiveCar, int> handler = CarEnteredBay;
                if (handler != null) handler(car, bayIndex);
            }
        }

        /// <summary>
        /// Keeps hired mechanics busy. Mechanics never steal the bay the player is working in,
        /// and they drop what they are doing the moment the player takes over their bay.
        /// </summary>
        private void AssignMechanics()
        {
            EventModifiers modifiers = Events.CurrentModifiers;
            int mechanicCount = Effects.MechanicCount;

            // Drop sessions whose car has gone, or whose bay the player has claimed.
            for (int i = _mechanicSessions.Count - 1; i >= 0; i--)
            {
                WorkSession session = _mechanicSessions[i];
                bool carGone = session.Car == null || session.Car.State != CarState.InBay;
                bool playerTookOver = PlayerSession != null && PlayerSession.Car == session.Car;
                bool laidOff = session.MechanicIndex >= mechanicCount;

                if (carGone || playerTookOver || laidOff)
                {
                    _mechanicSessions.RemoveAt(i);
                }
            }

            if (mechanicCount <= 0) return;

            float skill = MathUtil.Clamp01(Effects.MechanicSkill + modifiers.MechanicSkillBonus);
            float speed = Effects.MechanicSpeedMultiplier;

            // Keep existing sessions' tuning current (training bought mid-job should apply at once).
            for (int i = 0; i < _mechanicSessions.Count; i++)
            {
                _mechanicSessions[i].Skill = skill;
                _mechanicSessions[i].SpeedMultiplier = speed;
            }

            // Hand any idle mechanic a car that nobody is working on.
            for (int mechanicIndex = 0; mechanicIndex < mechanicCount; mechanicIndex++)
            {
                if (HasMechanicSession(mechanicIndex)) continue;

                ActiveCar car = FindUnattendedCar();
                if (car == null) break;

                int jobIndex = car.FirstIncompleteJobIndex();
                if (jobIndex < 0) continue;

                // A mechanic works out what is wrong themselves. Without this the player could
                // watch someone repair a car whose faults the card still showed as unknown.
                if (!car.Diagnosis.FoundEverything(car.Condition)) car.Diagnosis.RevealAll(true);

                WorkSession session = new WorkSession(car, jobIndex, true, mechanicIndex);
                session.Skill = skill;
                session.SpeedMultiplier = speed;
                _mechanicSessions.Add(session);
            }
        }

        private bool HasMechanicSession(int mechanicIndex)
        {
            for (int i = 0; i < _mechanicSessions.Count; i++)
            {
                if (_mechanicSessions[i].MechanicIndex == mechanicIndex) return true;
            }
            return false;
        }

        /// <summary>Finds a car in a bay that neither the player nor another mechanic is working on.</summary>
        private ActiveCar FindUnattendedCar()
        {
            for (int bayIndex = 0; bayIndex < _bays.Count; bayIndex++)
            {
                ActiveCar car = _bays[bayIndex];
                if (car == null || car.State != CarState.InBay) continue;
                if (car.AllJobsComplete) continue;

                if (PlayerSession != null && PlayerSession.Car == car) continue;

                bool taken = false;
                for (int i = 0; i < _mechanicSessions.Count; i++)
                {
                    if (_mechanicSessions[i].Car == car) { taken = true; break; }
                }

                if (!taken) return car;
            }
            return null;
        }

        /// <summary>Runs one session for a frame: start a round, advance it, resolve it.</summary>
        private void TickSession(WorkSession session, float deltaTime)
        {
            if (session == null) return;

            ActiveCar car = session.Car;
            if (car == null || car.State != CarState.InBay)
            {
                if (session == PlayerSession) PlayerSession = null;
                return;
            }

            float scaledDelta = deltaTime * (session.IsMechanic ? session.SpeedMultiplier : 1f);

            // Short breather between rounds so the player can read the feedback.
            if (session.RestartDelay > 0f)
            {
                session.RestartDelay -= scaledDelta;
                if (session.RestartDelay > 0f) return;
                session.RestartDelay = 0f;
            }

            // Make sure the session is pointed at a job that still needs doing.
            RepairJob job = session.Job;
            if (job == null || job.IsComplete)
            {
                int nextJob = car.FirstIncompleteJobIndex();
                if (nextJob < 0)
                {
                    // Nothing left: the car should already have been completed by ResolveRound.
                    return;
                }
                session.SetJobIndex(nextJob);
                if (!session.IsMechanic) car.SetActiveJob(nextJob);
                job = session.Job;
                if (job == null) return;
            }

            // Start a fresh round if there is not one running.
            if (session.Minigame == null)
            {
                MinigameBase minigame = MinigameFactory.CreateRanked(
                    job.Minigame, job.Type, job.Difficulty, CurrentTuning(), Random, RankLevel);

                session.BeginRound(minigame, Random);

                Action<WorkSession> startHandler = RoundStarted;
                if (startHandler != null) startHandler(session);
            }

            car.MarkWorkBegun();

            // Advance the round, then let a mechanic's auto-player react to the new state.
            session.Minigame.Tick(scaledDelta);
            if (session.AutoPlayer != null) session.AutoPlayer.Tick(scaledDelta);

            if (session.Minigame.IsFinished)
            {
                ResolveRound(session, session.Minigame.Result);
            }
        }

        /// <summary>Applies the verdict of a finished round: progress, penalties, payouts, events.</summary>
        private void ResolveRound(WorkSession session, MinigameResult result)
        {
            ActiveCar car = session.Car;
            RepairJob job = session.Job;

            session.ClearRound();

            if (car == null || job == null) return;

            Stats.RoundsPlayed++;
            if (result.Outcome == MinigameOutcome.Perfect) Stats.PerfectRounds++;
            if (result.Outcome == MinigameOutcome.Damage) Stats.DamagedRounds++;

            // Only the player's own hands build the streak. A hired mechanic quietly holding a
            // 12x chain in a bay you are not even looking at would make the streak meaningless.
            if (!session.IsMechanic)
            {
                Combo.Register(result.Outcome);
                if (Combo.BestStreak > Stats.BestStreak) Stats.BestStreak = Combo.BestStreak;
            }

            job.ApplyResult(result);
            car.ApplyTimePenalty(result.TimePenaltySeconds);

            Action<WorkSession, MinigameResult> roundHandler = RoundResolved;
            if (roundHandler != null) roundHandler(session, result);

            if (job.IsComplete)
            {
                // The part goes on as the job finishes. Nothing here can refuse: the inventory
                // always returns something, buying it in at a markup if the shelf is bare, so a
                // repair can never be blocked by stock - only made more expensive.
                Parts.PartFitting fitting = Inventory.Fit(job.Type, job.Payout, RankLevel);

                // A job that fits nothing - a diagnostic scan - must NOT be recorded as having
                // had a part. Recording a zero-value one marked it as "part fitted", so its
                // labour came out as the whole gross price, which includes the parts raise. Those
                // jobs were quietly paying 28% over the odds and it was worth 6% of the economy.
                if (!fitting.NoPartNeeded)
                {
                    job.RecordPart(fitting.Grade, fitting.Cost, fitting.Value);
                }

                // Only the surcharge moves money. The part's own share of the price came from the
                // customer and goes to the supplier - routing it through the wallet would inflate
                // lifetime earnings, and lifetime earnings are what garage rank is built on.
                if (fitting.Cost > 0d)
                {
                    Wallet.TrySpend(fitting.Cost);
                    Inventory.RecordSpend(fitting.Cost);

                    Action<ActiveCar, RepairJob, Parts.PartFitting> boughtHandler = PartBoughtIn;
                    if (boughtHandler != null) boughtHandler(car, job, fitting);
                }

                // Bonuses multiply the LABOUR only. The part's share of the gross is passed
                // straight through: it is the customer paying for the part, and a flawless repair
                // does not make the part itself worth more.
                double payout = job.LabourPayout;

                // The customer pays for how well it was done. This is scored AFTER the part was
                // recorded, so the grade that went on is already in the number - which is the
                // whole point: without it, a cheap part cost the player nothing but a figure on a
                // toast, and Budget was a free 13% a car.
                //
                // Applied exactly ONCE, here, to the labour. Nothing downstream multiplies by it
                // again, and the car's finishing tip is taken from LabourPayout rather than from
                // this, so a good repair is not paid for twice.
                // A fussier customer cares MORE about the same score. This stretches the existing
                // curve around 1.0 rather than redefining it, so quality still means exactly what
                // it meant - it is simply worth more to this particular customer.
                QualityReport quality = RepairQuality.ForJob(job, car.Mood, car.ExpectedPartGrade);
                payout *= 1d + (quality.PayMultiplier - 1d) * car.QualityWeight;

                if (job.IsFlawless) payout *= GameBalance.PerfectJobCashBonus;

                // The streak pays out on the player's own work, not on a mechanic's.
                if (!session.IsMechanic) payout *= Combo.Multiplier;

                payout = MathUtil.RoundCash(payout);

                Wallet.Earn(payout);
                car.AddEarnings(payout);
                Stats.JobsCompleted++;

                Action<ActiveCar, RepairJob, double> jobHandler = JobCompleted;
                if (jobHandler != null) jobHandler(car, job, payout);

                if (car.AllJobsComplete)
                {
                    CompleteCar(car);
                    return;
                }

                // Move on to the next job on the same car.
                int nextJob = car.FirstIncompleteJobIndex();
                session.SetJobIndex(nextJob);
                if (!session.IsMechanic) car.SetActiveJob(nextJob);
            }

            // A beat of feedback time. Mechanics pause too, but their delay is scaled by their speed.
            session.RestartDelay = session.IsMechanic ? 0.3f : 0.4f;
        }

        /// <summary>
        /// Moves the garage's standing by how this customer felt about the work.
        ///
        /// The satisfaction figure is the existing one, computed exactly as it always was and
        /// adjusted for what they were quoted. All that is new is that it now DOES something: it
        /// nudges the bias the spawner has always rolled rarity against. Serve people well and
        /// better cars start turning up.
        ///
        /// Measured either side of NeutralSatisfaction, so a garage doing competent ordinary work
        /// drifts nowhere in particular. A customer whose opinion carries weight - a collector -
        /// moves it several times as far, in whichever direction they are pointing.
        ///
        /// A car nobody did any work on cannot have an opinion worth recording, so a declined or
        /// instantly-finished one is skipped. Without that, turning work away would quietly be a
        /// reputation decision as well as a capacity one.
        /// </summary>
        private void RecordStanding(ActiveCar car)
        {
            if (car == null) return;
            if (car.QuotedAs == Cars.QuoteOption.Declined) return;

            int roundsPlayed = 0;
            for (int i = 0; i < car.Jobs.Count; i++) roundsPlayed += car.Jobs[i].RoundsPlayed;
            if (roundsPlayed <= 0) return;

            QualityReport quality = RepairQuality.ForCar(car);

            double satisfaction = car.Quoted
                ? Cars.Quote.AdjustSatisfaction(quality.Satisfaction, car.Mood, car.QuotedAs)
                : quality.Satisfaction;

            double move = (satisfaction - GameBalance.NeutralSatisfaction)
                          * GameBalance.StandingStep * car.ReputationWeight;

            Stats.Standing = MathUtil.Clamp(Stats.Standing + move, -1d, 1d);
        }

        /// <summary>Pays the finishing tips, frees the bay and tells the UI the customer drove off happy.</summary>
        private void CompleteCar(ActiveCar car)
        {
            // Turning a fleet vehicle down is not refusing one van - it ends the account, and the
            // rest of the run never turns up. That is what gives declining a fleet real weight:
            // the choice is about the whole run, taken at the first vehicle.
            if (car.FleetBatchId != 0 && car.FleetBatchId == _fleetBatchId
                && car.QuotedAs == Cars.QuoteOption.Declined)
            {
                CancelFleet();
            }

            RecordStanding(car);

            // Finishing early earns a tip proportional to the car's value, which is what makes
            // speed worth chasing without letting the tip eclipse the repair itself.
            // Only the work the customer agreed to counts towards the tip. Declining a repair
            // and still being tipped on it would make quoting small strictly better than quoting
            // honestly, which is the opposite of a decision.
            double basePayout = 0d;
            for (int i = 0; i < car.Jobs.Count; i++)
            {
                // LabourPayout, not Payout: the gross includes the part, and tipping on money
                // that goes straight back out to the supplier would inflate every car.
                if (car.Jobs[i].IsAccepted) basePayout += car.Jobs[i].LabourPayout;
            }

            // SpeedTipFraction comes off the car, so an urgent job can make finishing fast worth
            // double without a second tip system existing alongside the first.
            double tip = MathUtil.RoundCash(
                basePayout * car.SpeedTipFraction * car.RepairSpeedFraction * car.Mood.TipMultiplier());
            if (tip > 0d)
            {
                Wallet.Earn(tip);
                car.AddEarnings(tip);
            }

            // What a thorough inspection was worth. Small on purpose: diagnosis should be worth
            // doing, not compulsory-by-economics.
            double diagnosisBonus = car.Diagnosis.PayoutBonus(car.Condition);
            if (diagnosisBonus > 1d)
            {
                double extra = MathUtil.RoundCash(basePayout * (diagnosisBonus - 1d));
                if (extra > 0d)
                {
                    Wallet.Earn(extra);
                    car.AddEarnings(extra);
                }
            }

            car.MarkCompleted();
            Stats.CarsCompleted++;
            if (car.EarnedSoFar > Stats.BestCarPayout) Stats.BestCarPayout = car.EarnedSoFar;

            ReleaseCar(car);

            Action<ActiveCar, double> handler = CarCompleted;
            if (handler != null) handler(car, car.EarnedSoFar);
        }

        /// <summary>
        /// How fast a car loses patience while NOBODY is working on it - whether it is queuing
        /// outside or parked in a bay.
        ///
        /// One rate for both, and that matters. It used to be faster in a bay than in the queue,
        /// which quietly made Extra Bay a TRAP: the new bay pulled a car out of the forgiving
        /// queue into a harsher bay where, with only one pair of hands, it sat and rotted. Buying
        /// capacity made you poorer. With a single rate, parking a car is never worse than leaving
        /// it outside, so a bay is strictly an upgrade and the real clock is the one on the car
        /// you are actually working on.
        /// </summary>
        private const float UnattendedPatienceRate = 0.22f;

        /// <summary>Counts down every customer's patience and boots out the ones who give up.</summary>
        private void TickPatience(float deltaTime)
        {

            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                ActiveCar car = _waiting[i];
                if (car.TickPatience(deltaTime * UnattendedPatienceRate))
                {
                    _waiting.RemoveAt(i);
                    LoseCar(car);
                }
            }

            for (int bayIndex = 0; bayIndex < _bays.Count; bayIndex++)
            {
                ActiveCar car = _bays[bayIndex];
                if (car == null) continue;

                // The pressure comes from the car under your hands, not from the ones waiting -
                // and not from the seconds you spend reading what the round is asking of you.
                float rate = IsAttended(car) && !IsReadingPreview(car) ? 1f : UnattendedPatienceRate;

                if (car.TickPatience(deltaTime * rate))
                {
                    LoseCar(car);
                }
            }
        }

        /// <summary>True when this car's current round is still showing something to memorise.</summary>
        private bool IsReadingPreview(ActiveCar car)
        {
            if (PlayerSession != null && PlayerSession.Car == car)
            {
                return PlayerSession.Minigame != null && PlayerSession.Minigame.IsShowingPreview;
            }

            for (int i = 0; i < _mechanicSessions.Count; i++)
            {
                WorkSession session = _mechanicSessions[i];
                if (session.Car != car) continue;
                return session.Minigame != null && session.Minigame.IsShowingPreview;
            }
            return false;
        }

        /// <summary>True when the player or a mechanic is currently working on this car.</summary>
        public bool IsAttended(ActiveCar car)
        {
            if (car == null) return false;
            if (PlayerSession != null && PlayerSession.Car == car) return true;

            for (int i = 0; i < _mechanicSessions.Count; i++)
            {
                if (_mechanicSessions[i].Car == car) return true;
            }
            return false;
        }

        /// <summary>The customer has had enough: cancel any work, free the bay, log the loss.</summary>
        private void LoseCar(ActiveCar car)
        {
            car.MarkLeftAngry();
            Stats.CarsLost++;

            // Letting a customer walk breaks the streak. Otherwise the optimal play is to ignore
            // a dying car entirely and keep chaining rounds on a healthy one.
            if (PlayerSession != null && PlayerSession.Car == car) Combo.Break();

            ReleaseCar(car);

            Action<ActiveCar> handler = CarLeftAngry;
            if (handler != null) handler(car);
        }

        /// <summary>Removes a car from its bay and tears down any sessions pointing at it.</summary>
        private void ReleaseCar(ActiveCar car)
        {
            CancelSessionsForCar(car);

            for (int i = 0; i < _bays.Count; i++)
            {
                if (_bays[i] == car) _bays[i] = null;
            }

            _waiting.Remove(car);
        }

        private void CancelSessionsForCar(ActiveCar car)
        {
            if (PlayerSession != null && PlayerSession.Car == car) PlayerSession = null;

            for (int i = _mechanicSessions.Count - 1; i >= 0; i--)
            {
                if (_mechanicSessions[i].Car == car) _mechanicSessions.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------
        // Player actions (called by the UI)
        // ------------------------------------------------------------------

        /// <summary>
        /// Puts the player to work on the car in the given bay. Returns false if the bay is empty
        /// or the car there is already finished.
        /// </summary>
        public bool SelectBay(int bayIndex)
        {
            if (bayIndex < 0 || bayIndex >= _bays.Count) return false;

            ActiveCar car = _bays[bayIndex];
            if (car == null || car.State != CarState.InBay || car.AllJobsComplete) return false;

            if (PlayerSession != null && PlayerSession.Car == car) return true;

            // THE SAFETY VALVE. Picking up a spanner on a car nobody inspected commits to the
            // work, with no diagnosis bonus. This is what keeps diagnosis from ever being a gate:
            // there is no sequence of inputs that leaves a car with work the player cannot reach,
            // and a player who ignores the whole system has the game they had before.
            //
            // It marks the car skipped WITHOUT revealing it. It used to reveal everything, which
            // handed the player the full condition sheet for free the instant they picked up a
            // spanner - and that made ignoring diagnosis strictly better than using it. A job does
            // not need to be revealed to be worked on, so nothing here is stranded by staying
            // unknown.
            if (!car.Diagnosis.HasStarted) car.Diagnosis.Skip();

            int jobIndex = car.FirstIncompleteJobIndex();
            if (jobIndex < 0) return false;

            PlayerSession = new WorkSession(car, jobIndex, false, -1);
            car.SetActiveJob(jobIndex);

            // Any mechanic on this car steps aside; AssignMechanics will re-home them next tick.
            for (int i = _mechanicSessions.Count - 1; i >= 0; i--)
            {
                if (_mechanicSessions[i].Car == car) _mechanicSessions.RemoveAt(i);
            }

            return true;
        }

        /// <summary>Switches the player to a different job on the car they are already working.</summary>
        public bool SelectJob(int jobIndex)
        {
            if (PlayerSession == null) return false;

            ActiveCar car = PlayerSession.Car;
            if (car == null || jobIndex < 0 || jobIndex >= car.Jobs.Count) return false;

            // Declined work is not yours to do. Without this the player could tap a job the
            // customer refused and spend real time on something nobody is going to pay for.
            if (!car.Jobs[jobIndex].NeedsWork) return false;

            PlayerSession.SetJobIndex(jobIndex);
            car.SetActiveJob(jobIndex);
            return true;
        }

        // ------------------------------------------------------------------
        // Diagnosis
        // ------------------------------------------------------------------

        /// <summary>The inspection round the player is currently playing, or null.</summary>
        public DiagnosisSession DiagnosisSession { get; private set; }

        public event Action<ActiveCar, DiagnosisAction, MinigameResult> DiagnosisResolved;

        /// <summary>Raised when a job had to buy its part at the counter, so the UI can say so.</summary>
        public event Action<ActiveCar, RepairJob, Parts.PartFitting> PartBoughtIn;

        /// <summary>
        /// Starts an inspection on the car in a bay. Returns false when that check has already
        /// been run on this car, or there is nothing there to look at.
        /// </summary>
        public bool StartDiagnosis(int bayIndex, DiagnosisAction action)
        {
            if (bayIndex < 0 || bayIndex >= _bays.Count) return false;

            ActiveCar car = _bays[bayIndex];
            if (car == null || car.State != CarState.InBay || car.AllJobsComplete) return false;
            if (!car.Diagnosis.CanRun(action)) return false;

            // Inspecting is hands-on, so it takes the place of whatever was being worked.
            ClearPlayerSession();

            // Worked out in double, narrowed once for the mini-game, which runs on float time.
            float difficulty = (float)(car.Definition.Rarity.DifficultyScale()
                                       * DiagnosisActions.DifficultyScale);

            MinigameBase minigame = MinigameFactory.Create(
                action.MinigameFor(), JobType.Diagnostics, difficulty, CurrentTuning(), Random);

            DiagnosisSession = new DiagnosisSession(car, action, minigame);
            return true;
        }

        /// <summary>Advances the inspection round and applies it once it finishes.</summary>
        private void TickDiagnosis(float deltaTime)
        {
            DiagnosisSession session = DiagnosisSession;
            if (session == null) return;

            ActiveCar car = session.Car;

            // The customer left, or the car finished some other way.
            if (car == null || car.State != CarState.InBay)
            {
                DiagnosisSession = null;
                return;
            }

            session.Minigame.Tick(deltaTime);
            if (!session.Minigame.IsFinished) return;

            MinigameResult result = session.Minigame.Result;
            car.Diagnosis.Record(session.Action, result.Outcome, car.Condition);

            // An inspection does not count as repair work: it must not build the streak, and it
            // must not start the speed-tip clock, or looking at a car would be its own penalty.
            Stats.DiagnosisRoundsPlayed++;

            DiagnosisSession = null;

            Action<ActiveCar, DiagnosisAction, MinigameResult> handler = DiagnosisResolved;
            if (handler != null) handler(car, session.Action, result);
        }

        /// <summary>Feeds a tap through to the inspection round, if one is running.</summary>
        /// <summary>Abandons the inspection without crediting it.</summary>
        public void CancelDiagnosis()
        {
            DiagnosisSession = null;
        }

        /// <summary>Puts the tools down. The car stays in its bay and mechanics may pick it up.</summary>
        public void ClearPlayerSession()
        {
            if (PlayerSession != null && PlayerSession.Car != null)
            {
                PlayerSession.Car.SetActiveJob(-1);
            }
            PlayerSession = null;
        }

        /// <summary>Seconds that must pass between calming customers.</summary>
        public const float CalmCooldownSeconds = 40f;

        /// <summary>Seconds left before the player can calm a customer again.</summary>
        public float CalmCooldownRemaining { get; set; }

        /// <summary>True when a customer can be calmed right now.</summary>
        public bool CanCalmCustomer { get { return CalmCooldownRemaining <= 0f; } }

        /// <summary>
        /// Has a word with a waiting customer, buying back some of the patience they have lost.
        /// Free, but on a cooldown, so it is a decision about WHICH car to save rather than a
        /// button to hold down. Returns the seconds granted, or 0 if it did nothing.
        /// </summary>
        public float TryCalmCustomer(int bayIndex)
        {
            if (!CanCalmCustomer) return 0f;
            if (bayIndex < 0 || bayIndex >= _bays.Count) return 0f;

            ActiveCar car = _bays[bayIndex];
            if (car == null || car.State != CarState.InBay) return 0f;

            const float PatienceRestored = 0.35f;
            float granted = car.CalmCustomer(PatienceRestored);
            if (granted <= 0f) return 0f;

            CalmCooldownRemaining = CalmCooldownSeconds;

            Action<ActiveCar, float> handler = CustomerCalmed;
            if (handler != null) handler(car, granted);

            return granted;
        }

        /// <summary>Raised when a customer is talked round, with the seconds bought back.</summary>
        public event Action<ActiveCar, float> CustomerCalmed;

        /// <summary>Forwards a screen tap / button press into the player's current round.</summary>
        /// <summary>
        /// The round the player's hands are on right now - an inspection if one is running,
        /// otherwise the repair. Every control routes through this, so a diagnosis round gets the
        /// existing mini-game views and inputs for free rather than needing a parallel path that
        /// could drift out of step with them.
        /// </summary>
        public MinigameBase LiveMinigame
        {
            get
            {
                if (DiagnosisSession != null) return DiagnosisSession.Minigame;
                return PlayerSession != null ? PlayerSession.Minigame : null;
            }
        }

        public void PlayerPress()
        {
            MinigameBase minigame = LiveMinigame;
            if (minigame != null) minigame.Press();
        }

        /// <summary>Forwards a finger lift into the player's current round.</summary>
        public void PlayerRelease()
        {
            MinigameBase minigame = LiveMinigame;
            if (minigame != null) minigame.Release();
        }

        /// <summary>Forwards a tool button / direction button tap into the player's current round.</summary>
        public void PlayerSelectOption(int optionIndex)
        {
            MinigameBase minigame = LiveMinigame;
            if (minigame != null) minigame.SelectOption(optionIndex);
        }

        // ------------------------------------------------------------------
        // Shop
        // ------------------------------------------------------------------

        /// <summary>Price of the next level including any Tool Sale discount.</summary>
        public double GetUpgradeCost(UpgradeDefinition definition)
        {
            double cost = Upgrades.GetNextCost(definition);
            if (double.IsInfinity(cost)) return cost;

            float discount = Events.CurrentModifiers.UpgradeDiscount;
            if (discount > 0f) cost = MathUtil.RoundCash(cost * (1f - discount));

            return cost;
        }

        /// <summary>Buys one level of an upgrade if affordable, and re-applies the effects immediately.</summary>
        /// <summary>
        /// Why this upgrade cannot be bought right now, or null when it can.
        ///
        /// Exposed so the shop can say so BEFORE the player spends anything, rather than the
        /// purchase failing silently. Cost is deliberately not checked here: a price the player
        /// cannot afford yet is already shown on the button.
        /// </summary>
        public string BlockedReason(UpgradeDefinition definition)
        {
            if (definition == null) return "Unknown upgrade";
            if (Upgrades.IsMaxed(definition)) return "Fully upgraded";

            if (definition.Id == "auto_mechanic")
            {
                int hired = Upgrades.GetLevel("auto_mechanic");
                if (hired >= UpgradeState.MaxMechanicsFor(BayCount))
                {
                    // The player holds a bay themselves, so there is physically nowhere for this
                    // mechanic to work. Measured at 0.0% busy, so selling it would be a con.
                    //
                    // Only ever reached below the bay ceiling: at the maximum bays the crew cap and
                    // the upgrade's own maximum coincide, so IsMaxed above has already answered.
                    return "Need another bay first";
                }
            }

            return null;
        }

        public bool TryBuyUpgrade(string upgradeId)
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById(upgradeId);
            if (definition == null) return false;
            if (Upgrades.IsMaxed(definition)) return false;
            if (BlockedReason(definition) != null) return false;

            double cost = GetUpgradeCost(definition);
            if (double.IsInfinity(cost)) return false;
            if (!Wallet.CanAfford(cost)) return false;

            // Spend the (possibly discounted) price directly, then record the level.
            if (!Wallet.TrySpend(cost)) return false;

            bool applied = ApplyPurchasedLevel(definition);
            if (!applied)
            {
                // Should be impossible, but never silently swallow the player's money.
                Wallet.Earn(cost);
                return false;
            }

            RefreshEffects();
            return true;
        }

        /// <summary>Records a bought level. Split out so the discount path and the plain path agree.</summary>
        private bool ApplyPurchasedLevel(UpgradeDefinition definition)
        {
            Dictionary<string, int> levels = Upgrades.ToDictionary();
            int current;
            if (!levels.TryGetValue(definition.Id, out current)) current = 0;
            if (current >= definition.MaxLevel) return false;

            levels[definition.Id] = current + 1;
            Upgrades.Restore(levels);
            return true;
        }

        // ------------------------------------------------------------------
        // Prestige
        // ------------------------------------------------------------------

        /// <summary>True when the player has earned enough to sell the garage.</summary>
        /// <summary>
        /// Has a shelf filled now rather than waiting for the van. Returns false when the garage
        /// cannot afford it. The only thing this buys is avoiding the counter markup.
        /// </summary>
        public bool TryExpediteParts(Parts.PartKind kind)
        {
            if (kind == Parts.PartKind.None) return false;
            if (Inventory.TotalStockOf(kind) >= GameBalance.PartShelfCap) return false;

            double fee = Parts.PartsInventory.ExpediteFee(kind, RankLevel);
            if (!Wallet.TrySpend(fee)) return false;

            Inventory.Expedite(kind);
            Inventory.RecordSpend(fee);
            return true;
        }

        public bool CanPrestige()
        {
            return Prestige.CanPrestige(Wallet.Cash, Wallet.LifetimeEarnings);
        }

        /// <summary>
        /// Sells the garage: banks reputation tokens, wipes cash, upgrades and the forecourt,
        /// and starts a fresh run with a permanently higher payout multiplier.
        /// Returns the number of tokens awarded, or 0 if the player was not eligible.
        /// </summary>
        public int TryPrestige()
        {
            if (!CanPrestige()) return 0;

            int awarded = Prestige.Prestige(Wallet.Cash, Wallet.LifetimeEarnings);
            if (awarded <= 0) return 0;

            // Clear the shop floor.
            PlayerSession = null;
            _mechanicSessions.Clear();
            _waiting.Clear();
            for (int i = 0; i < _bays.Count; i++) _bays[i] = null;

            Upgrades.ResetAll();
            Combo.Reset();
            Wallet.ResetForPrestige(GameBalance.StartingCash + Prestige.StartingCashBonus);
            Events.ClearActive();
            CalmCooldownRemaining = 0f;

            // Selling up ends any fleet account in progress. Without this the new garage keeps
            // the old run's counter, and TickFleet forces the remaining vans onto a rank-0
            // forecourt where fleets are not unlocked for another three ranks.
            CancelFleet();

            // And nobody has heard of the new garage. Standing is the one stat that describes
            // THIS garage rather than the player's career, so carrying a ruined name across a
            // sell-up would make prestige a punishment for having had a bad run.
            // Tokens, prestige count, perks and all-time earnings are career-long and stay.
            Stats.Standing = 0d;

            RefreshEffects();

            // Bays shrink back to one, so rebuild the list from scratch.
            _bays.Clear();
            ResizeBays();

            _spawnTimer = 2f;

            Action<int> handler = PrestigePerformed;
            if (handler != null) handler(awarded);

            return awarded;
        }

        // ------------------------------------------------------------------
        // Offline / idle progress
        // ------------------------------------------------------------------

        /// <summary>
        /// Catches the game up after the app was closed. Hired mechanics keep working (at reduced
        /// efficiency); with no mechanics hired, nothing is earned and any stale cars are cleared out.
        /// </summary>
        public OfflineReport ApplyOfflineProgress(double offlineSeconds)
        {
            OfflineReport report = new OfflineReport();

            if (offlineSeconds <= 1d) return report;

            double seconds = Math.Min(offlineSeconds, GameBalance.MaxOfflineSeconds);
            report.SecondsSimulated = seconds;

            if (Effects.MechanicCount <= 0)
            {
                // Nobody was here: every customer left. Clear them so the player does not come back
                // to a forecourt of cars with zero seconds on the clock.
                ClearForecourt();
                return report;
            }

            double cashBefore = Wallet.Cash;
            int completedBefore = Stats.CarsCompleted;
            int lostBefore = Stats.CarsLost;

            // The player is not there to play, so only mechanics work. A coarse step keeps the
            // catch-up fast: eight hours at a quarter-second step is ~115k ticks, a few frames of work.
            const float Step = 0.25f;
            int steps = (int)(seconds / Step);

            WorkSession suspendedPlayer = PlayerSession;
            PlayerSession = null;

            // An inspection the player left open is abandoned rather than ticked through the
            // catch-up. Nobody was holding the phone, so it would simply time out and record a
            // failed check - the player would come back to a wasted inspection they never played.
            DiagnosisSession = null;

            for (int i = 0; i < steps; i++)
            {
                Tick(Step);
            }

            PlayerSession = suspendedPlayer;

            double earned = Wallet.Cash - cashBefore;

            // Offline work is worth less than being there in person.
            if (earned > 0d)
            {
                double efficiency = MathUtil.Clamp(
                    GameBalance.OfflineEfficiency + Prestige.OfflineBonus, 0f, 1f);
                double keep = earned * efficiency;
                double giveBack = earned - keep;
                if (giveBack > 0d) Wallet.TrySpend(MathUtil.RoundCash(giveBack));
                report.CashEarned = MathUtil.RoundCash(keep);
            }

            report.CarsCompleted = Stats.CarsCompleted - completedBefore;
            report.CarsLost = Stats.CarsLost - lostBefore;

            return report;
        }

        /// <summary>Empties the queue and the bays, e.g. after a long absence with no mechanics.</summary>
        public void ClearForecourt()
        {
            PlayerSession = null;
            _mechanicSessions.Clear();
            _waiting.Clear();
            for (int i = 0; i < _bays.Count; i++) _bays[i] = null;
            _spawnTimer = 2f;
        }

        // ------------------------------------------------------------------
        // Save support
        // ------------------------------------------------------------------

        /// <summary>Spawn timer value, exposed so the save file can restore mid-cycle timing.</summary>
        public float SpawnTimer
        {
            get { return _spawnTimer; }
            set { _spawnTimer = value < 0f ? 0f : value; }
        }

        /// <summary>Adds a car straight into the waiting queue (used when loading a save).</summary>
        public void RestoreWaitingCar(ActiveCar car)
        {
            if (car == null) return;
            _waiting.Add(car);
        }

        /// <summary>Puts a car straight into a bay (used when loading a save).</summary>
        public void RestoreBayCar(ActiveCar car, int bayIndex)
        {
            if (car == null) return;
            if (bayIndex < 0 || bayIndex >= _bays.Count)
            {
                _waiting.Add(car);
                return;
            }

            _bays[bayIndex] = car;
            car.MoveToBay(bayIndex);
        }

        private void HandleEventStarted(GameEventDefinition definition)
        {
            if (definition != null && definition.Id == GameEventId.CoffeeRun)
            {
                // Instant effect: everyone currently waiting gets more patience.
                const float CoffeeSeconds = 9f;
                for (int i = 0; i < _waiting.Count; i++) _waiting[i].GrantExtraTime(CoffeeSeconds);
                for (int i = 0; i < _bays.Count; i++)
                {
                    if (_bays[i] != null) _bays[i].GrantExtraTime(CoffeeSeconds);
                }
            }

            Action<GameEventDefinition> handler = EventStarted;
            if (handler != null) handler(definition);
        }

        private void HandleEventEnded(GameEventDefinition definition)
        {
            Action<GameEventDefinition> handler = EventEnded;
            if (handler != null) handler(definition);
        }
    }
}
