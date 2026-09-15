using System;
using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
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

        /// <summary>Seconds until the next car rolls in.</summary>
        public float TimeUntilNextCar { get { return _spawnTimer; } }

        /// <summary>How many bays the garage currently has.</summary>
        public int BayCount { get { return _bays.Count; } }

        private readonly List<ActiveCar> _waiting = new List<ActiveCar>();
        private readonly List<ActiveCar> _bays = new List<ActiveCar>();
        private readonly List<WorkSession> _mechanicSessions = new List<WorkSession>();

        private float _spawnTimer;

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
            Wallet = new Wallet(GameBalance.StartingCash);
            Upgrades = new UpgradeState();
            Prestige = new PrestigeState();
            Spawner = new CarSpawner(Random);
            Events = new RandomEventSystem(Random);
            Stats = new GameStats();

            Events.EventStarted += HandleEventStarted;
            Events.EventEnded += HandleEventEnded;

            RefreshEffects();
            ResizeBays();

            _spawnTimer = 2f; // A car is waiting almost immediately so a new player has something to do.
        }

        /// <summary>Recomputes cached upgrade effects. Call after any purchase or prestige.</summary>
        public void RefreshEffects()
        {
            Effects = Upgrades.BuildEffects(Prestige.PayoutMultiplier);
            ResizeBays();
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

            Events.Tick(deltaTime);
            TickSpawning(deltaTime);
            AssignCarsToBays();
            AssignMechanics();

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
        public ActiveCar SpawnCar()
        {
            EventModifiers modifiers = Events.CurrentModifiers;
            SpawnParametersBundle bundle = Effects.ToSpawnValues();

            SpawnParameters parameters = SpawnParameters.Default;
            parameters.RarityBias = bundle.RarityBias;
            parameters.PatienceMultiplier = bundle.PatienceMultiplier;
            parameters.PayoutMultiplier = bundle.PayoutMultiplier * modifiers.PayoutMultiplier;
            parameters.ExtraJobChance = modifiers.ExtraJobChance;

            ActiveCar car = Spawner.Spawn(parameters);
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
                MinigameBase minigame = MinigameFactory.Create(
                    job.Minigame, job.Type, job.Difficulty, Effects.ToTuning(), Random);

                session.BeginRound(minigame, Random);

                Action<WorkSession> startHandler = RoundStarted;
                if (startHandler != null) startHandler(session);
            }

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

            job.ApplyResult(result);
            car.ApplyTimePenalty(result.TimePenaltySeconds);

            Action<WorkSession, MinigameResult> roundHandler = RoundResolved;
            if (roundHandler != null) roundHandler(session, result);

            if (job.IsComplete)
            {
                double payout = job.Payout;
                if (job.IsFlawless) payout *= GameBalance.PerfectJobCashBonus;
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

        /// <summary>Pays the finishing tips, frees the bay and tells the UI the customer drove off happy.</summary>
        private void CompleteCar(ActiveCar car)
        {
            // Finishing early earns a tip, which is what makes speed worth chasing.
            double tip = MathUtil.RoundCash(car.TimeRemaining * GameBalance.SpeedTipPerSecond);
            if (tip > 0d)
            {
                Wallet.Earn(tip);
                car.AddEarnings(tip);
            }

            car.MarkCompleted();
            Stats.CarsCompleted++;
            if (car.EarnedSoFar > Stats.BestCarPayout) Stats.BestCarPayout = car.EarnedSoFar;

            ReleaseCar(car);

            Action<ActiveCar, double> handler = CarCompleted;
            if (handler != null) handler(car, car.EarnedSoFar);
        }

        /// <summary>Counts down every customer's patience and boots out the ones who give up.</summary>
        private void TickPatience(float deltaTime)
        {
            // Cars still outside are far more forgiving than a customer watching you work on theirs.
            // This is what makes the waiting queue a useful buffer rather than a stream of lost income:
            // a car can sit outside for a few minutes, but once it is on the ramp the clock is real.
            const float QueuePatienceRate = 0.22f;

            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                ActiveCar car = _waiting[i];
                if (car.TickPatience(deltaTime * QueuePatienceRate))
                {
                    _waiting.RemoveAt(i);
                    LoseCar(car);
                }
            }

            for (int bayIndex = 0; bayIndex < _bays.Count; bayIndex++)
            {
                ActiveCar car = _bays[bayIndex];
                if (car == null) continue;

                if (car.TickPatience(deltaTime))
                {
                    LoseCar(car);
                }
            }
        }

        /// <summary>The customer has had enough: cancel any work, free the bay, log the loss.</summary>
        private void LoseCar(ActiveCar car)
        {
            car.MarkLeftAngry();
            Stats.CarsLost++;

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
            if (car.Jobs[jobIndex].IsComplete) return false;

            PlayerSession.SetJobIndex(jobIndex);
            car.SetActiveJob(jobIndex);
            return true;
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

        /// <summary>Forwards a screen tap / button press into the player's current round.</summary>
        public void PlayerPress()
        {
            if (PlayerSession != null && PlayerSession.Minigame != null) PlayerSession.Minigame.Press();
        }

        /// <summary>Forwards a finger lift into the player's current round.</summary>
        public void PlayerRelease()
        {
            if (PlayerSession != null && PlayerSession.Minigame != null) PlayerSession.Minigame.Release();
        }

        /// <summary>Forwards a tool button / direction button tap into the player's current round.</summary>
        public void PlayerSelectOption(int optionIndex)
        {
            if (PlayerSession != null && PlayerSession.Minigame != null) PlayerSession.Minigame.SelectOption(optionIndex);
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
        public bool TryBuyUpgrade(string upgradeId)
        {
            UpgradeDefinition definition = UpgradeCatalog.FindById(upgradeId);
            if (definition == null) return false;
            if (Upgrades.IsMaxed(definition)) return false;

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
            Wallet.ResetForPrestige(GameBalance.StartingCash);
            Events.ClearActive();

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

            for (int i = 0; i < steps; i++)
            {
                Tick(Step);
            }

            PlayerSession = suspendedPlayer;

            double earned = Wallet.Cash - cashBefore;

            // Offline work is worth less than being there in person.
            if (earned > 0d)
            {
                double keep = earned * GameBalance.OfflineEfficiency;
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
