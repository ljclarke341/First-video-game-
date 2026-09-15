using System;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Events;
using GarageTycoon.Core.Minigames;
using GarageTycoon.Core.Save;
using GarageTycoon.Core.Simulation;
using GarageTycoon.Unity.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GarageTycoon.Unity.Platform
{
    /// <summary>
    /// The one MonoBehaviour that starts the game.
    ///
    /// Drop this on a single empty GameObject in a scene and it does the rest: loads the save (or
    /// starts a new garage), builds the entire UI in code, drives the simulation every frame, wires
    /// the simulation's events up to on-screen feedback, and saves on the way out.
    ///
    /// Everything it owns is destroyed with it, so entering and exiting play mode repeatedly in the
    /// editor leaves nothing behind.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Simulation")]
        [Tooltip("Seed used only when starting a brand new game. Leave at 0 to pick one from the clock.")]
        [SerializeField] private int _newGameSeed = 0;

        [Tooltip("Seconds between automatic saves.")]
        [SerializeField] private float _autoSaveInterval = 20f;

        [Tooltip("Turn off to ignore any existing save and always start fresh. Handy while testing.")]
        [SerializeField] private bool _loadSaveOnStart = true;

        [Header("Debug")]
        [Tooltip("Speeds the whole simulation up. 1 is normal; try 5 to watch a long session quickly.")]
        [Range(0.25f, 10f)]
        [SerializeField] private float _timeScale = 1f;

        private GarageSimulation _simulation;
        private Canvas _canvas;
        private GarageScreen _garageScreen;
        private UpgradeScreen _upgradeScreen;
        private PopupPanel _popup;
        private ToastLayer _toasts;
        private RectTransform _toastRoot;

        private float _autoSaveTimer;
        private bool _isQuitting;

        /// <summary>
        /// True only when this session actually restored a save. Offline progress keys off this
        /// rather than "a save file exists", so starting fresh with a save still on disk cannot
        /// hand the new garage hours of the old garage's idle income.
        /// </summary>
        private bool _loadedExistingSave;

        /// <summary>The running game, exposed for the editor tools and for debugging.</summary>
        public GarageSimulation Simulation { get { return _simulation; } }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            // Mobile housekeeping: a steady 60fps and a screen that does not sleep mid-repair.
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            CreateSimulation();
            BuildUi();
            SubscribeToSimulation();
            ApplyOfflineProgress();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime * Mathf.Max(0.01f, _timeScale);

            // The garage stands still while a full-screen panel is open.
            //
            // Without this, a customer can run out of patience while the player is reading the
            // upgrade list - losing a car to a menu you cannot see past is the kind of thing that
            // makes people stop opening the menu, which is the opposite of what an upgrade screen
            // is for. There is nothing to exploit here: it is a single player game, and the idle
            // mechanics are paid out from elapsed time rather than from frames.
            bool modalOpen = _upgradeScreen.IsVisible || _popup.IsVisible;

            if (!modalOpen)
            {
                _simulation.Tick(deltaTime);
            }

            _garageScreen.Refresh();
            _upgradeScreen.Refresh();

            TickAutoSave(deltaTime);
        }

        private void OnApplicationPause(bool paused)
        {
            // On Android this is the reliable "the player just left" hook - OnApplicationQuit often
            // never fires, because the OS simply suspends the process.
            if (paused) Save();
            else CatchUpAfterResume();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Save();
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            Save();
        }

        private void OnDestroy()
        {
            if (!_isQuitting) Save();
            UnsubscribeFromSimulation();
        }

        // ------------------------------------------------------------------
        // Start-up
        // ------------------------------------------------------------------

        private void CreateSimulation()
        {
            int seed = _newGameSeed != 0 ? _newGameSeed : Environment.TickCount;

            if (_loadSaveOnStart)
            {
                string json = SaveFile.Read();
                if (!string.IsNullOrEmpty(json))
                {
                    _simulation = GameStateSerializer.Load(json, seed);

                    if (_simulation == null)
                    {
                        // A corrupt save is not a crash: tell the player and start fresh.
                        Debug.LogWarning("[GarageTycoon] The save file could not be read, starting a new garage.");
                    }
                    else
                    {
                        _loadedExistingSave = true;
                    }
                }
            }

            if (_simulation == null) _simulation = new GarageSimulation(seed);
        }

        private void BuildUi()
        {
            EnsureEventSystem();

            // ---- canvas ----
            GameObject canvasObject = new GameObject("GarageCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Theme.ReferenceResolution;
            // Match on height: phones vary far more in aspect ratio than in usable height.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = (RectTransform)canvasObject.transform;

            // ---- screens, in draw order ----
            _garageScreen = new GarageScreen();
            _garageScreen.Build(canvasRect, _simulation);
            _garageScreen.UpgradesRequested += () => _upgradeScreen.Show();
            _garageScreen.StatsRequested += ShowStats;
            _garageScreen.PrestigeRequested += ShowPrestigeConfirmation;

            _upgradeScreen = new UpgradeScreen();
            _upgradeScreen.Build(canvasRect, _simulation, HandleUpgradePurchased);

            _popup = new PopupPanel();
            _popup.Build(canvasRect);

            // Toasts sit above everything so feedback is never hidden behind a panel.
            _toastRoot = UIFactory.CreateRect("Toasts", canvasRect);
            UIFactory.Stretch(_toastRoot);
            CanvasGroup toastGroup = UIFactory.AddCanvasGroup(_toastRoot.gameObject);
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;

            _toasts = _toastRoot.gameObject.AddComponent<ToastLayer>();
            _toasts.Initialise(_toastRoot);
        }

        /// <summary>Creates an EventSystem if the scene has none, so UI input works from a bare scene.</summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.transform.SetParent(transform, false);

            // StandaloneInputModule covers mouse in the editor and touch on device via the legacy
            // input manager, which keeps this project free of extra package dependencies.
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // ------------------------------------------------------------------
        // Simulation events -> on-screen feedback
        // ------------------------------------------------------------------

        private void SubscribeToSimulation()
        {
            _simulation.RoundResolved += HandleRoundResolved;
            _simulation.JobCompleted += HandleJobCompleted;
            _simulation.CarCompleted += HandleCarCompleted;
            _simulation.CarLeftAngry += HandleCarLeft;
            _simulation.CarSpawned += HandleCarSpawned;
            _simulation.EventStarted += HandleEventStarted;
        }

        private void UnsubscribeFromSimulation()
        {
            if (_simulation == null) return;

            _simulation.RoundResolved -= HandleRoundResolved;
            _simulation.JobCompleted -= HandleJobCompleted;
            _simulation.CarCompleted -= HandleCarCompleted;
            _simulation.CarLeftAngry -= HandleCarLeft;
            _simulation.CarSpawned -= HandleCarSpawned;
            _simulation.EventStarted -= HandleEventStarted;
        }

        private void HandleRoundResolved(WorkSession session, MinigameResult result)
        {
            // Only shout about the player's own taps; mechanics working away in the background
            // would otherwise bury the screen in messages.
            if (session.IsMechanic) return;

            Color color;
            switch (result.Outcome)
            {
                case MinigameOutcome.Perfect: color = Theme.PerfectZone; break;
                case MinigameOutcome.Good: color = Theme.Success; break;
                case MinigameOutcome.Weak: color = Theme.TextSecondary; break;
                case MinigameOutcome.Damage: color = Theme.Danger; break;
                default: color = Theme.Warning; break;
            }

            _toasts.Show(result.Message, color, new Vector2(0f, -240f));
        }

        private void HandleJobCompleted(ActiveCar car, RepairJob job, double payout)
        {
            _toasts.Show(job.Type.DisplayName() + " done!  +$" + CashFormat.Short(payout),
                Theme.Cash, new Vector2(0f, -120f));

            // Light the car's own card up too, so the feedback is tied to the bay it came from.
            _garageScreen.FlashCar(car, Theme.Cash);
        }

        private void HandleCarCompleted(ActiveCar car, double earned)
        {
            string message = car.IsFlawless
                ? "FLAWLESS!  " + car.Definition.DisplayName + "  +$" + CashFormat.Short(earned)
                : car.Definition.DisplayName + " done!  +$" + CashFormat.Short(earned);

            _toasts.Show(message, car.IsFlawless ? Theme.PerfectZone : Theme.Success, new Vector2(0f, 60f));
            _garageScreen.FlashCar(car, car.IsFlawless ? Theme.PerfectZone : Theme.Success);
        }

        private void HandleCarLeft(ActiveCar car)
        {
            _toasts.Show(car.Definition.DisplayName + " gave up and left!", Theme.Danger, new Vector2(0f, 60f));
            _garageScreen.FlashCar(car, Theme.Danger);
        }

        private void HandleCarSpawned(ActiveCar car)
        {
            // Only make a fuss about the genuinely valuable arrivals.
            if (car.Definition.Rarity >= CarRarity.Epic)
            {
                _toasts.Show(car.Definition.Rarity.DisplayName().ToUpperInvariant() + " CAR!  " + car.Definition.DisplayName,
                    Theme.Hex(car.Definition.Rarity.ColorHex()), new Vector2(0f, 180f));
            }
        }

        private void HandleEventStarted(GameEventDefinition definition)
        {
            if (definition == null) return;

            _toasts.ShowCentre(definition.DisplayName + "!", Theme.Hex(definition.ColorHex));
        }

        private void HandleUpgradePurchased(string upgradeId)
        {
            _toasts.ShowCentre("Upgraded!", Theme.Info);
            Save();
        }

        // ------------------------------------------------------------------
        // Popups
        // ------------------------------------------------------------------

        private void ShowStats()
        {
            GameStats stats = _simulation.Stats;

            string body =
                "Cars completed:  " + stats.CarsCompleted + "\n" +
                "Customers lost:  " + stats.CarsLost + "\n" +
                "Happy customers:  " + Mathf.RoundToInt(stats.SatisfactionRate * 100f) + "%\n\n" +
                "Jobs finished:  " + stats.JobsCompleted + "\n" +
                "Mini-games played:  " + stats.RoundsPlayed + "\n" +
                "Perfect rounds:  " + Mathf.RoundToInt(stats.PerfectRate * 100f) + "%\n" +
                "Parts damaged:  " + stats.DamagedRounds + "\n\n" +
                "Best single car:  $" + CashFormat.Full(stats.BestCarPayout) + "\n" +
                "Earned this run:  $" + CashFormat.Full(_simulation.Wallet.LifetimeEarnings) + "\n" +
                "Earned all time:  $" + CashFormat.Full(_simulation.Wallet.AllTimeEarnings) + "\n\n" +
                "Time in the garage:  " + CashFormat.Duration(stats.PlayTimeSeconds) + "\n" +
                "Garage sold:  " + _simulation.Prestige.PrestigeCount + " times";

            _popup.Show("GARAGE STATS", body, "CLOSE", null, null, null, Theme.Info);
        }

        private void ShowPrestigeConfirmation()
        {
            if (!_simulation.CanPrestige())
            {
                double needed = _simulation.Prestige.CashRequirement - _simulation.Wallet.Cash;

                _popup.Show("NOT YET",
                    "Selling the garage resets your cash and upgrades, but earns permanent Reputation "
                    + "Tokens that boost every future payout.\n\nYou need $"
                    + CashFormat.Full(Math.Max(0d, needed)) + " more in the bank first.",
                    "KEEP WORKING", null, null, null, Theme.Prestige);
                return;
            }

            int tokens = _simulation.Prestige.TokensForReset(_simulation.Wallet.LifetimeEarnings);

            _popup.Show("SELL THE GARAGE?",
                "You will lose all your cash, upgrades and the cars on the forecourt.\n\n"
                + "You will keep " + tokens + " Reputation Token" + (tokens == 1 ? "" : "s")
                + ", worth a permanent +" + Mathf.RoundToInt(tokens * 12f) + "% on every payout from now on.",
                "SELL UP", PerformPrestige, "NOT YET", null, Theme.Prestige);
        }

        private void PerformPrestige()
        {
            int tokens = _simulation.TryPrestige();
            if (tokens <= 0) return;

            _toasts.ShowCentre("+" + tokens + " Reputation Token" + (tokens == 1 ? "" : "s"), Theme.Prestige);
            Save();
        }

        private void ApplyOfflineProgress()
        {
            if (!_loadedExistingSave) return;

            string json = SaveFile.Read();
            if (string.IsNullOrEmpty(json)) return;

            JsonValue root = JsonValue.Parse(json);
            if (root == null) return;

            double savedAt = root["savedAt"].AsDouble(0d);
            if (savedAt <= 0d) return;

            double elapsed = SaveFile.NowUnixSeconds() - savedAt;

            // A negative gap means the device clock moved backwards; ignore it rather than
            // rewarding or punishing the player for a time zone change.
            if (elapsed <= 60d) return;

            OfflineReport report = _simulation.ApplyOfflineProgress(elapsed);
            if (!report.HasAnythingToReport) return;

            string body =
                "You were away for " + CashFormat.Duration(report.SecondsSimulated) + ".\n\n"
                + "Your mechanics earned  $" + CashFormat.Full(report.CashEarned) + "\n"
                + "Cars finished:  " + report.CarsCompleted + "\n"
                + "Customers lost:  " + report.CarsLost;

            _popup.Show("WHILE YOU WERE AWAY", body, "NICE", null, null, null, Theme.Success);
        }

        /// <summary>After a resume, catch the garage up on the time the app spent in the background.</summary>
        private void CatchUpAfterResume()
        {
            // The simulation is driven by Time.deltaTime, which does not advance while suspended,
            // so without this the garage would appear frozen in time after every phone call.
            string json = SaveFile.Read();
            if (string.IsNullOrEmpty(json)) return;

            JsonValue root = JsonValue.Parse(json);
            if (root == null) return;

            double savedAt = root["savedAt"].AsDouble(0d);
            if (savedAt <= 0d) return;

            double elapsed = SaveFile.NowUnixSeconds() - savedAt;
            if (elapsed <= 60d) return;

            OfflineReport report = _simulation.ApplyOfflineProgress(elapsed);
            if (report.HasAnythingToReport)
            {
                _toasts.ShowCentre("Mechanics earned $" + CashFormat.Short(report.CashEarned) + " while you were away",
                    Theme.Success);
            }
        }

        // ------------------------------------------------------------------
        // Saving
        // ------------------------------------------------------------------

        private void TickAutoSave(float deltaTime)
        {
            _autoSaveTimer += deltaTime;
            if (_autoSaveTimer < _autoSaveInterval) return;

            _autoSaveTimer = 0f;
            Save();
        }

        /// <summary>Writes the current game to disk. Safe to call as often as you like.</summary>
        public void Save()
        {
            if (_simulation == null) return;

            string json = GameStateSerializer.Save(_simulation, SaveFile.NowUnixSeconds());
            SaveFile.Write(json);
        }

        /// <summary>Wipes the save and starts a brand new garage. Used by the editor tools.</summary>
        public void ResetProgress()
        {
            SaveFile.Delete();

            UnsubscribeFromSimulation();
            _simulation = new GarageSimulation(Environment.TickCount);
            _loadedExistingSave = false;
            SubscribeToSimulation();

            // The screens hold a reference to the old simulation, so rebuild them against the new one.
            if (_canvas != null) Destroy(_canvas.gameObject);
            BuildUi();
        }
    }
}
