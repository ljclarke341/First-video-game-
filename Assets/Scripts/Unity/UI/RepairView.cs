using System.Collections.Generic;
using GarageTycoon.Core.Balance;
using GarageTycoon.Core.Cars;
using GarageTycoon.Core.Minigames;
using UnityEngine;
using UnityEngine.UI;

namespace GarageTycoon.Unity.UI
{
    /// <summary>
    /// The car on the workbench, with the repair happening ON it.
    ///
    /// Progress used to be entirely abstract: a bar filled up. A bar tells you a number, but it
    /// never shows you the work. This draws the car above the mini-game and puts the job onto it,
    /// so every round you win visibly changes the car.
    ///
    /// FASTENERS ARE THE THROUGH-LINE. Every job type gets a small ring of bolts somewhere sensible
    /// on the car, and how many are turned tracks that job's progress. So "I finished a round"
    /// always reads as "another bolt just went tight", whichever mini-game it happened to be.
    /// Wheel jobs additionally spin the wheel, because that is the one part everybody recognises.
    /// </summary>
    public sealed class RepairView
    {
        /// <summary>
        /// Where each job sits on the car comes from Core.RepairLayout, so the placement rule and
        /// the "how many bolts are turned" rule are covered by the headless tests rather than only
        /// being visible by eye. This class is just the drawing.
        /// </summary>

        /// <summary>Stage size in reference pixels. 2:1, matching the car sprite so nothing stretches.</summary>
        private const float StageWidth = 290f;
        private const float StageHeight = 145f;

        /// <summary>The worst case is every job on a car showing its biggest cluster, plus spare.</summary>
        private const int FastenerPoolSize = GameBalance.MaxJobsPerCar * RepairLayout.MaxFastenersPerJob + 2;

        private RectTransform _root;
        private RectTransform _stage;
        private Image _car;
        private RectTransform _wheelFront;
        private RectTransform _wheelRear;
        private Image _ring;
        private Image _spark;
        private Text _caption;
        private RepairAnimator _animator;

        private readonly List<Image> _fasteners = new List<Image>();

        /// <summary>Which pooled bolt each job's cluster starts at. Rebuilt when the car changes.</summary>
        private readonly Dictionary<JobType, int> _clusterStart = new Dictionary<JobType, int>();

        private int _boundCarId = -1;

        /// <summary>Builds the view into the band it has been given. Call once.</summary>
        public void Build(RectTransform parent)
        {
            Image panel = UIFactory.CreatePanel("RepairView", parent, Theme.PanelSunken, 12);
            _root = panel.rectTransform;
            UIFactory.Stretch(_root);

            _animator = _root.gameObject.AddComponent<RepairAnimator>();

            // Everything car-shaped lives on a fixed-size stage. Because the stage is exactly 2:1
            // like the sprite, normalised coordinates land where the drawing actually is.
            _stage = UIFactory.CreateRect("Stage", _root);
            _stage.anchorMin = new Vector2(0.5f, 0.5f);
            _stage.anchorMax = new Vector2(0.5f, 0.5f);
            _stage.pivot = new Vector2(0.5f, 0.5f);
            _stage.sizeDelta = new Vector2(StageWidth, StageHeight);
            _stage.anchoredPosition = Vector2.zero;

            _car = UIFactory.CreateImage("Car", _stage, Color.white, UISprites.CarSilhouette());
            _car.raycastTarget = false;
            UIFactory.Stretch(_car.rectTransform);

            // Wheels sit on top of the ones drawn into the sprite so they can actually turn.
            _wheelRear = BuildWheel("WheelRear", 0.25f, 0.25f);
            _wheelFront = BuildWheel("WheelFront", 0.75f, 0.25f);

            // The "you are working here" marker.
            _ring = UIFactory.CreateImage("ActiveRing", _stage, Theme.Info, UISprites.Ring(96, 8));
            _ring.raycastTarget = false;
            Place(_ring.rectTransform, 0.5f, 0.5f, 46f);
            _ring.gameObject.SetActive(false);

            // A pool of bolts, re-laid-out per car rather than created and destroyed.
            for (int i = 0; i < FastenerPoolSize; i++)
            {
                Image bolt = UIFactory.CreateImage("Bolt" + i, _stage, Theme.TextMuted, UISprites.Circle(32));
                bolt.raycastTarget = false;
                Place(bolt.rectTransform, 0.5f, 0.5f, 12f);
                bolt.gameObject.SetActive(false);
                _fasteners.Add(bolt);
            }

            _spark = UIFactory.CreateImage("Spark", _stage, Theme.PerfectZone, UISprites.Circle(48));
            _spark.raycastTarget = false;
            Place(_spark.rectTransform, 0.5f, 0.5f, 26f);
            _spark.gameObject.SetActive(false);

            // A plain count, for anyone who wants the number as well as the picture.
            _caption = UIFactory.CreateText("Caption", _root, string.Empty, Theme.FontTiny,
                Theme.TextMuted, TextAnchor.MiddleCenter);
            UIFactory.AnchorBottom(_caption.rectTransform, 22f, 4f, 10f);

            _animator.Initialise(_stage, _spark, _wheelFront, _wheelRear);
            _root.gameObject.SetActive(false);
        }

        /// <summary>A dark tyre with a visible spoke cross, so rotation reads at a glance.</summary>
        private RectTransform BuildWheel(string name, float x, float y)
        {
            Image wheel = UIFactory.CreateImage(name, _stage, Theme.Hex("#12171D"), UISprites.Circle(96));
            wheel.raycastTarget = false;
            Place(wheel.rectTransform, x, y, 56f);

            AddSpoke(wheel.transform, "SpokeA", new Vector2(5f, 44f));
            AddSpoke(wheel.transform, "SpokeB", new Vector2(44f, 5f));

            Image hub = UIFactory.CreateImage("Hub", wheel.transform, Theme.Hex("#B9C3CE"), UISprites.Circle(32));
            hub.raycastTarget = false;
            hub.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            hub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hub.rectTransform.sizeDelta = new Vector2(16f, 16f);
            hub.rectTransform.anchoredPosition = Vector2.zero;

            return wheel.rectTransform;
        }

        private static void AddSpoke(Transform parent, string name, Vector2 size)
        {
            Image spoke = UIFactory.CreateImage(name, parent, Theme.Hex("#7E8B99"), UISprites.Solid());
            spoke.raycastTarget = false;
            spoke.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            spoke.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            spoke.rectTransform.sizeDelta = size;
            spoke.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>Pins an element to a normalised point on the stage, at a fixed pixel size.</summary>
        private static void Place(RectTransform rect, float x, float y, float size)
        {
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(x, y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>Lays the bolt clusters out for one specific car. Only runs when the car changes.</summary>
        private void Bind(ActiveCar car)
        {
            _boundCarId = car.InstanceId;
            _clusterStart.Clear();
            _car.color = Theme.Hex(car.Definition.BodyColorHex);

            int next = 0;

            for (int i = 0; i < car.Jobs.Count; i++)
            {
                JobType job = car.Jobs[i].Type;

                RepairSpot spot = RepairLayout.For(job);
                if (_clusterStart.ContainsKey(job)) continue;            // a car should not repeat a job, but be safe
                if (next + spot.FastenerCount > _fasteners.Count) break;

                _clusterStart[job] = next;

                for (int bolt = 0; bolt < spot.FastenerCount; bolt++)
                {
                    // Evenly around a small circle, which reads as a bolt pattern anywhere on the car.
                    float angle = Mathf.PI * 0.5f + bolt * (Mathf.PI * 2f / spot.FastenerCount);

                    // The stage is twice as wide as it is tall, so the Y radius has to be doubled
                    // in normalised terms for the ring to come out round on screen.
                    const float RadiusX = 0.045f;
                    const float RadiusY = RadiusX * 2f;

                    Image element = _fasteners[next++];
                    Place(element.rectTransform,
                        spot.X + Mathf.Cos(angle) * RadiusX,
                        spot.Y + Mathf.Sin(angle) * RadiusY,
                        12f);

                    element.gameObject.SetActive(true);
                    element.rectTransform.localScale = Vector3.one;
                    element.rectTransform.localRotation = Quaternion.identity;
                }
            }

            for (int i = next; i < _fasteners.Count; i++) _fasteners[i].gameObject.SetActive(false);
        }

        /// <summary>Redraws from the car's current state. Cheap enough to call every frame.</summary>
        public void Refresh(ActiveCar car, int activeJobIndex)
        {
            if (_root == null) return;

            if (car == null)
            {
                Clear();
                return;
            }

            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
            if (car.InstanceId != _boundCarId) Bind(car);

            int totalBolts = 0;
            int tightBolts = 0;

            for (int i = 0; i < car.Jobs.Count; i++)
            {
                RepairJob job = car.Jobs[i];

                int start;
                if (!_clusterStart.TryGetValue(job.Type, out start)) continue;

                RepairSpot spot = RepairLayout.For(job.Type);

                // Bolts turned = how far through this job you are. One round is usually one more
                // bolt, which is exactly the feedback a progress bar alone never gave.
                int tight = RepairLayout.TightFasteners(job.Type, job.Progress);

                totalBolts += spot.FastenerCount;
                tightBolts += tight;

                for (int bolt = 0; bolt < spot.FastenerCount; bolt++)
                {
                    Image element = _fasteners[start + bolt];
                    element.color = bolt < tight
                        ? Theme.Success
                        : Theme.WithAlpha(Theme.TextMuted, 0.45f);
                }
            }

            _caption.text = totalBolts == 0
                ? string.Empty
                : tightBolts + " / " + totalBolts + " fasteners torqued";

            // Ring whatever is being worked on right now.
            if (activeJobIndex >= 0 && activeJobIndex < car.Jobs.Count)
            {
                RepairSpot active = RepairLayout.For(car.Jobs[activeJobIndex].Type);

                if (!_ring.gameObject.activeSelf) _ring.gameObject.SetActive(true);

                _ring.rectTransform.anchorMin = new Vector2(active.X, active.Y);
                _ring.rectTransform.anchorMax = new Vector2(active.X, active.Y);
                _ring.rectTransform.anchoredPosition = Vector2.zero;

                // A slow breathe, so the eye finds it without it being noisy.
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.4f) * 0.09f;
                _ring.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
            else if (_ring.gameObject.activeSelf)
            {
                _ring.gameObject.SetActive(false);
            }
        }

        /// <summary>Plays the work landing, right after a round resolves.</summary>
        public void Pulse(ActiveCar car, JobType jobType, MinigameOutcome outcome)
        {
            if (_root == null || car == null || car.InstanceId != _boundCarId) return;

            RepairSpot spot = RepairLayout.For(jobType);

            // A miss or a breakage moves nothing, so it gets a knock instead of a bolt.
            if (outcome == MinigameOutcome.Miss || outcome == MinigameOutcome.Damage)
            {
                _animator.Shake();
                return;
            }

            int start;
            if (_clusterStart.TryGetValue(jobType, out start))
            {
                RepairJob job = FindJob(car, jobType);

                if (job != null)
                {
                    // Pop the bolt that this round just turned - the newest tight one.
                    int index = Mathf.Clamp(
                        RepairLayout.TightFasteners(jobType, job.Progress) - 1, 0, spot.FastenerCount - 1);
                    _animator.PopBolt(_fasteners[start + index].rectTransform);
                }
            }

            // Wheel work visibly turns the wheel it belongs to.
            if (jobType == JobType.Tires) _animator.SpinWheel(true);
            else if (jobType == JobType.Brakes) _animator.SpinWheel(false);

            _animator.Spark(spot.X, spot.Y,
                outcome == MinigameOutcome.Perfect ? Theme.PerfectZone : Theme.Success);
        }

        /// <summary>A last flourish when every job on the car is done.</summary>
        public void Finish()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;
            _animator.Shine();
        }

        /// <summary>Hides the view when there is nothing on the ramp.</summary>
        public void Clear()
        {
            if (_root == null) return;
            if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false);
            _boundCarId = -1;
        }

        private static RepairJob FindJob(ActiveCar car, JobType jobType)
        {
            for (int i = 0; i < car.Jobs.Count; i++)
            {
                if (car.Jobs[i].Type == jobType) return car.Jobs[i];
            }

            return null;
        }
    }

    /// <summary>
    /// Drives the short animations on the repair view. This is a MonoBehaviour because everything
    /// here happens over time and needs an Update; RepairView itself stays a plain class.
    /// </summary>
    public sealed class RepairAnimator : MonoBehaviour
    {
        private const float ShakeSeconds = 0.32f;
        private const float SparkSeconds = 0.42f;
        private const float PopSeconds = 0.34f;
        private const float ShineSeconds = 0.7f;

        private RectTransform _stage;
        private Image _spark;
        private RectTransform _wheelFront;
        private RectTransform _wheelRear;

        private float _shakeLeft;
        private float _sparkLeft;
        private float _popLeft;
        private float _shineLeft;

        private Color _sparkColor;
        private RectTransform _poppingBolt;

        // Wheels ease towards a target angle rather than snapping, so a round of tyre work looks
        // like the wheel being spun on rather than teleporting.
        private float _frontTarget;
        private float _rearTarget;
        private float _frontAngle;
        private float _rearAngle;

        public void Initialise(RectTransform stage, Image spark, RectTransform wheelFront, RectTransform wheelRear)
        {
            _stage = stage;
            _spark = spark;
            _wheelFront = wheelFront;
            _wheelRear = wheelRear;
        }

        public void Shake()
        {
            _shakeLeft = ShakeSeconds;
        }

        public void Shine()
        {
            _shineLeft = ShineSeconds;
        }

        public void SpinWheel(bool front)
        {
            if (front) _frontTarget += 120f;
            else _rearTarget += 120f;
        }

        public void PopBolt(RectTransform bolt)
        {
            // Tidy up whatever was mid-pop, so a fast player never leaves a bolt scaled up.
            if (_poppingBolt != null && _poppingBolt != bolt) ResetBolt();

            _poppingBolt = bolt;
            _popLeft = PopSeconds;
        }

        public void Spark(float x, float y, Color color)
        {
            if (_spark == null) return;

            RectTransform rect = _spark.rectTransform;
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(x, y);
            rect.anchoredPosition = Vector2.zero;

            _sparkColor = color;
            _spark.color = color;
            _spark.gameObject.SetActive(true);
            _sparkLeft = SparkSeconds;
        }

        private void Update()
        {
            // Unscaled, so the animations still read correctly if the game is ever paused or slowed.
            float deltaTime = Time.unscaledDeltaTime;

            UpdateWheels(deltaTime);
            UpdateShake(deltaTime);
            UpdateSpark(deltaTime);
            UpdateBolt(deltaTime);
            UpdateShine(deltaTime);
        }

        private void UpdateWheels(float deltaTime)
        {
            float ease = Mathf.Clamp01(deltaTime * 9f);

            _frontAngle = Mathf.Lerp(_frontAngle, _frontTarget, ease);
            _rearAngle = Mathf.Lerp(_rearAngle, _rearTarget, ease);

            // Negative because a car driving forward turns its wheels clockwise on screen.
            if (_wheelFront != null) _wheelFront.localRotation = Quaternion.Euler(0f, 0f, -_frontAngle);
            if (_wheelRear != null) _wheelRear.localRotation = Quaternion.Euler(0f, 0f, -_rearAngle);
        }

        private void UpdateShake(float deltaTime)
        {
            if (_shakeLeft <= 0f || _stage == null) return;

            _shakeLeft -= deltaTime;

            if (_shakeLeft <= 0f)
            {
                _stage.anchoredPosition = Vector2.zero;
                return;
            }

            float strength = _shakeLeft / ShakeSeconds;
            _stage.anchoredPosition = new Vector2(Mathf.Sin(_shakeLeft * 72f) * 8f * strength, 0f);
        }

        private void UpdateSpark(float deltaTime)
        {
            if (_sparkLeft <= 0f || _spark == null) return;

            _sparkLeft -= deltaTime;

            if (_sparkLeft <= 0f)
            {
                _spark.gameObject.SetActive(false);
                return;
            }

            // Expand and fade: a quick flash of light where the spanner just landed.
            float t = 1f - (_sparkLeft / SparkSeconds);
            float scale = Mathf.Lerp(0.4f, 2.1f, t);

            _spark.rectTransform.localScale = new Vector3(scale, scale, 1f);
            _spark.color = Theme.WithAlpha(_sparkColor, 1f - t);
        }

        private void UpdateBolt(float deltaTime)
        {
            if (_popLeft <= 0f || _poppingBolt == null) return;

            _popLeft -= deltaTime;

            if (_popLeft <= 0f)
            {
                ResetBolt();
                return;
            }

            // Out and back with a part-turn, so the bolt visibly goes tight rather than just
            // changing colour.
            float t = 1f - (_popLeft / PopSeconds);
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.6f;

            _poppingBolt.localScale = new Vector3(scale, scale, 1f);
            _poppingBolt.localRotation = Quaternion.Euler(0f, 0f, -72f * t);
        }

        private void UpdateShine(float deltaTime)
        {
            if (_shineLeft <= 0f || _stage == null) return;

            _shineLeft -= deltaTime;

            if (_shineLeft <= 0f)
            {
                _stage.localScale = Vector3.one;
                return;
            }

            // One soft swell of the whole car: "finished, and looking good".
            float t = 1f - (_shineLeft / ShineSeconds);
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.06f;
            _stage.localScale = new Vector3(scale, scale, 1f);
        }

        private void ResetBolt()
        {
            if (_poppingBolt == null) return;

            _poppingBolt.localScale = Vector3.one;
            _poppingBolt.localRotation = Quaternion.identity;
            _poppingBolt = null;
        }
    }
}
