using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AstraLink.Core;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// Procedural sprite generator for the 2D Orbit Radar (Task S1-08).
    /// Generates anti-aliased radial sprites at runtime so <see cref="Image.Type.Filled"/> (<see cref="Image.FillMethod.Radial360"/>)
    /// works out-of-the-box without external PNG dependencies, while still supporting custom sprites in the Inspector.
    /// </summary>
    public static class RadarProceduralSprites
    {
        private static Sprite _discSprite;
        private static Sprite _ringSprite;
        private static Sprite _commSectorSprite;
        private static Sprite _sunlightConeSprite;
        private static Sprite _shadowConeSprite;
        private static Sprite _satelliteTriangleSprite;

        /// <summary>
        /// Returns a soft-edged circular disc sprite used for planets and circular backgrounds.
        /// </summary>
        public static Sprite GetDiscSprite()
        {
            if (_discSprite != null) return _discSprite;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Procedural_Disc",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float maxR = center - 2f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((maxR - r) * 0.75f);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _discSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _discSprite;
        }

        /// <summary>
        /// Returns a crisp circular ring sprite representing the satellite orbital trajectory.
        /// </summary>
        public static Sprite GetOrbitRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            _ringSprite = CreateAnnulusSprite("Procedural_OrbitRing", 256, 0.85f, 0.90f, 1.0f, 0.0f);
            return _ringSprite;
        }

        /// <summary>
        /// Returns an outer arc band sprite used to highlight the ground-station communication window sector.
        /// </summary>
        public static Sprite GetCommSectorSprite()
        {
            if (_commSectorSprite != null) return _commSectorSprite;
            _commSectorSprite = CreateAnnulusSprite("Procedural_CommSector", 256, 0.74f, 0.98f, 0.95f, 0.25f);
            return _commSectorSprite;
        }

        /// <summary>
        /// Returns a radial cone sprite used to visualize the illuminated Sunlight orbital sector.
        /// </summary>
        public static Sprite GetSunlightConeSprite()
        {
            if (_sunlightConeSprite != null) return _sunlightConeSprite;
            _sunlightConeSprite = CreateAnnulusSprite("Procedural_SunlightCone", 256, 0.28f, 0.88f, 0.90f, 0.22f);
            return _sunlightConeSprite;
        }

        /// <summary>
        /// Returns a radial cone sprite with an accentuated outer rim used to visualize the planetary Shadow/Eclipse sector.
        /// </summary>
        public static Sprite GetShadowConeSprite()
        {
            if (_shadowConeSprite != null) return _shadowConeSprite;
            _shadowConeSprite = CreateAnnulusSprite("Procedural_ShadowCone", 256, 0.28f, 0.94f, 0.95f, 0.35f);
            return _shadowConeSprite;
        }

        /// <summary>
        /// Returns a directional triangle spacecraft sprite with solar wings for the orbiting satellite marker.
        /// </summary>
        public static Sprite GetSatelliteTriangleSprite()
        {
            if (_satelliteTriangleSprite != null) return _satelliteTriangleSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Procedural_SatelliteTriangle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = y / (float)(size - 1); // 0 bottom .. 1 top (nose)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f; // -1 left .. +1 right

                    bool insideTriangle = ny >= 0.12f && ny <= 0.92f && Mathf.Abs(nx) <= (0.94f - ny) * 0.82f;
                    bool rearNotch = ny < 0.22f && Mathf.Abs(nx) < (0.22f - ny) * 1.4f;
                    bool solarWing = ny >= 0.34f && ny <= 0.48f && Mathf.Abs(nx) <= 0.92f && Mathf.Abs(nx) >= 0.32f;

                    if ((insideTriangle && !rearNotch) || solarWing)
                    {
                        pixels[y * size + x] = new Color32(255, 255, 255, 255);
                    }
                    else
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _satelliteTriangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _satelliteTriangleSprite;
        }

        private static Sprite CreateAnnulusSprite(
            string name,
            int size,
            float innerRatio,
            float outerRatio,
            float maxAlpha,
            float rimBoost)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float innerR = center * innerRatio;
            float outerR = center * outerRatio;
            float bandWidth = Mathf.Max(1f, outerR - innerR);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float inEdge = Mathf.Clamp01((r - innerR) * 0.8f);
                    float outEdge = Mathf.Clamp01((outerR - r) * 0.8f);
                    float normalizedBand = Mathf.Clamp01((r - innerR) / bandWidth);

                    // Subtle radial gradient + brighter outer rim so sectors remain clearly visible
                    float radialProfile = Mathf.Lerp(0.65f, 1.0f, normalizedBand);
                    if (normalizedBand > 0.88f)
                    {
                        radialProfile = Mathf.Clamp01(radialProfile + rimBoost);
                    }

                    float alpha = inEdge * outEdge * maxAlpha * radialProfile;
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>
    /// 2D Orbit Radar UI Controller (Task S1-08).
    /// Renders the central planet, the orbital trajectory ring, and three distinct orbital sectors:
    /// 1) Sunlight Sector (<c>OrbitProgress &lt; 1 - EclipseFraction</c>),
    /// 2) Shadow / Eclipse Sector (<c>OrbitProgress &gt;= 1 - EclipseFraction</c>),
    /// 3) Communication Window Sector (<c>OrbitProgress &lt; CommWindowFraction</c>),
    /// and animates the satellite smoothly across the 20 ticks returned by <see cref="IMissionSimulator.SimulateFullMission"/>.
    /// </summary>
    public class OrbitRadarUI : MonoBehaviour
    {
        [Header("Radar Visual References (Auto-bound if null)")]
        [SerializeField] private RectTransform radarViewportRect;
        [SerializeField] private Image planetImage;
        [SerializeField] private Text planetNameLabel;
        [SerializeField] private Image orbitRingImage;
        [SerializeField] private Image sunlightSectorImage;
        [SerializeField] private Image eclipseSectorImage;
        [SerializeField] private Image commSectorImage;
        [SerializeField] private RectTransform sunlightLabelRect;
        [SerializeField] private Text sunlightSectorLabel;
        [SerializeField] private RectTransform eclipseLabelRect;
        [SerializeField] private Text eclipseSectorLabel;
        [SerializeField] private RectTransform commLabelRect;
        [SerializeField] private Text commSectorLabel;
        [SerializeField] private RectTransform satelliteRect;
        [SerializeField] private Image satelliteImage;

        [Header("Controls & Telemetry Labels (Auto-bound if null)")]
        [SerializeField] private Button startTestFlightButton;
        [SerializeField] private Text startTestFlightButtonLabel;
        [SerializeField] private Button speed1xButton;
        [SerializeField] private Button speed3xButton;
        [SerializeField] private Button speed5xButton;
        [SerializeField] private Button speed10xButton;
        [SerializeField] private Text statusHeaderLabel;
        [SerializeField] private Text tickProgressLabel;
        [SerializeField] private Text zoneBadgeLabel;
        [SerializeField] private Image zoneBadgeBackground;
        [SerializeField] private Text telemetrySnapshotLabel;

        [Header("Radar Geometry Settings")]
        [SerializeField] private float orbitRadiusPx = 178f;
        [SerializeField] private float baseSecondsPerTick = 1.0f;

        private IMissionSimulator _simulator;
        private OrbitDefinition _currentOrbit;
        private RocketOption _currentRocket;
        private MissionGoal _currentGoal;
        private List<SatelliteComponent> _currentComponents;
        private List<AutopilotRule> _currentRules;

        private MissionReport _activeReport;
        private bool _isPlaying;
        private int _currentTickIndex;
        private float _tickElapsedSeconds;
        private float _speedMultiplier = 1.0f;
        private float _currentDisplayedProgress;

        /// <summary>
        /// Fired when the player starts or restarts the 20-tick test flight simulation.
        /// </summary>
        public event Action OnTestFlightStarted;

        /// <summary>
        /// Fired whenever the playback advances to a new <see cref="SimulationTickSnapshot"/>.
        /// </summary>
        public event Action<SimulationTickSnapshot, int> OnTickUpdated;

        /// <summary>
        /// Fired when the 20-tick orbital test flight finishes a full revolution.
        /// </summary>
        public event Action<MissionReport> OnTestFlightCompleted;

        /// <summary>
        /// Fired when the playback speed multiplier changes (e.g. 1x, 3x, 5x).
        /// </summary>
        public event Action<float> OnSpeedChanged;

        /// <summary>
        /// Gets a value indicating whether the orbital test flight animation is currently playing.
        /// </summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// Gets the active playback speed multiplier (1x, 3x, 5x).
        /// </summary>
        public float SpeedMultiplier => _speedMultiplier;

        /// <summary>
        /// Gets the currently displayed <see cref="OrbitDefinition"/>.
        /// </summary>
        public OrbitDefinition CurrentOrbit => _currentOrbit;

        /// <summary>
        /// Gets the currently selected <see cref="RocketOption"/>.
        /// </summary>
        public RocketOption CurrentRocket => _currentRocket;

        /// <summary>
        /// Gets the most recent <see cref="MissionReport"/> produced by the simulator.
        /// </summary>
        public MissionReport LastReport => _activeReport;

        /// <summary>
        /// Gets the zero-based index of the currently displayed simulation tick.
        /// </summary>
        public int CurrentTickIndex => _currentTickIndex;

        /// <summary>
        /// Gets the total number of ticks in the active <see cref="MissionReport"/>.
        /// </summary>
        public int TotalTicks => (_activeReport != null && _activeReport.Ticks != null) ? _activeReport.Ticks.Count : 0;

        /// <summary>
        /// Gets the currently displayed <see cref="SimulationTickSnapshot"/>, or null if no report is loaded.
        /// </summary>
        public SimulationTickSnapshot CurrentTickSnapshot =>
            (_activeReport != null && _activeReport.Ticks != null && _currentTickIndex >= 0 && _currentTickIndex < _activeReport.Ticks.Count)
                ? _activeReport.Ticks[_currentTickIndex]
                : null;

        private void Awake()
        {
            EnsureRadarHierarchy();
            BindButtons();
        }

        private void Start()
        {
            if (_currentOrbit == null || _simulator == null)
            {
                IMissionSimulator sim = _simulator;
                if (sim == null)
                {
                    sim = (GameManager.Instance != null && GameManager.Instance.Simulator != null)
                        ? GameManager.Instance.Simulator
                        : new MockMissionSimulator();
                }

                OrbitDefinition defaultOrbit = _currentOrbit ?? CreateDefaultLeoOrbit();
                Initialize(sim, defaultOrbit, _currentRocket, _currentGoal, _currentComponents, _currentRules);
            }
        }

        /// <summary>
        /// Primary entry point used by <see cref="GameManager"/> to initialize the radar prefab in MainScene.
        /// </summary>
        public void Initialize(
            IMissionSimulator simulator,
            OrbitDefinition orbit = null,
            RocketOption rocket = null,
            MissionGoal goal = null,
            List<SatelliteComponent> components = null,
            List<AutopilotRule> rules = null)
        {
            EnsureRadarHierarchy();

            if (simulator == null)
            {
                Debug.LogWarning("[OrbitRadarUI] Initialize received null simulator; falling back to MockMissionSimulator.");
                simulator = new MockMissionSimulator();
            }

            _simulator = simulator;
            _currentOrbit = orbit ?? CreateDefaultLeoOrbit();
            _currentRocket = rocket;
            _currentGoal = goal;
            _currentComponents = components ?? new List<SatelliteComponent>();
            _currentRules = rules ?? new List<AutopilotRule>();

            StopTestFlight(resetToStart: true);
            RefreshOrbitVisuals();
            UpdateSpeedButtonsVisuals();

            Debug.Log("[OrbitRadarUI] Initialized with orbit: " + _currentOrbit.Name);
        }

        /// <summary>
        /// Initializes the radar directly with an <see cref="OrbitDefinition"/> and a precomputed <see cref="MissionReport"/>.
        /// </summary>
        public void Initialize(OrbitDefinition orbit, MissionReport report)
        {
            EnsureRadarHierarchy();

            if (orbit == null)
            {
                Debug.LogWarning("[OrbitRadarUI] Initialize(orbit, report) received null orbit; using default LEO orbit.");
                orbit = CreateDefaultLeoOrbit();
            }

            _currentOrbit = orbit;
            _activeReport = report;
            StopTestFlight(resetToStart: true);
            RefreshOrbitVisuals();
        }

        /// <summary>
        /// Updates the active orbit definition and immediately recalculates the Sunlight, Shadow, and Comm sectors.
        /// </summary>
        public void SetOrbit(OrbitDefinition orbit)
        {
            if (orbit == null)
            {
                Debug.LogError("[OrbitRadarUI] SetOrbit received a null OrbitDefinition.");
                return;
            }

            EnsureRadarHierarchy();
            _currentOrbit = orbit;
            StopTestFlight(resetToStart: true);
            RefreshOrbitVisuals();
        }

        /// <summary>
        /// Updates the selected launch vehicle reference displayed in the radar header.
        /// </summary>
        public void SetRocket(RocketOption rocket)
        {
            if (rocket == null)
            {
                Debug.LogWarning("[OrbitRadarUI] SetRocket received a null RocketOption.");
                return;
            }

            _currentRocket = rocket;
            RefreshIdleSummaryText();
        }

        /// <summary>
        /// Starts a 20-tick orbital test flight using <see cref="IMissionSimulator.SimulateFullMission"/>.
        /// </summary>
        public void StartTestFlight()
        {
            EnsureRadarHierarchy();

            if (_simulator == null)
            {
                _simulator = (GameManager.Instance != null && GameManager.Instance.Simulator != null)
                    ? GameManager.Instance.Simulator
                    : new MockMissionSimulator();
            }

            if (_currentOrbit == null)
            {
                _currentOrbit = CreateDefaultLeoOrbit();
            }

            _activeReport = _simulator.SimulateFullMission(
                _currentComponents,
                _currentRocket,
                _currentOrbit,
                _currentGoal,
                _currentRules);

            if (_activeReport == null || _activeReport.Ticks == null || _activeReport.Ticks.Count == 0)
            {
                Debug.LogError("[OrbitRadarUI] SimulateFullMission returned an empty or null MissionReport.");
                return;
            }

            _isPlaying = true;
            _currentTickIndex = 0;
            _tickElapsedSeconds = 0f;

            if (startTestFlightButtonLabel != null)
            {
                startTestFlightButtonLabel.text = "Restart Flight Test";
            }

            ApplyTickSnapshot(_activeReport.Ticks[0], 0, _activeReport.Ticks.Count);
            SetSatelliteOrbitProgress(_activeReport.Ticks[0].OrbitProgress);

            Debug.Log(string.Format(
                "[OrbitRadarUI] Test flight started on '{0}' ({1} ticks, speed {2:0.#}x).",
                _currentOrbit.Name,
                _activeReport.Ticks.Count,
                _speedMultiplier));

            OnTestFlightStarted?.Invoke();
        }

        /// <summary>
        /// Stops the current orbital playback and optionally resets the satellite marker to <c>u = 0.0</c>.
        /// </summary>
        public void StopTestFlight(bool resetToStart = false)
        {
            _isPlaying = false;
            _tickElapsedSeconds = 0f;

            if (resetToStart)
            {
                _currentTickIndex = 0;
                SetSatelliteOrbitProgress(0f);
                RefreshIdleSummaryText();
            }

            if (startTestFlightButtonLabel != null)
            {
                startTestFlightButtonLabel.text = "Start Flight Test";
            }
        }

        /// <summary>
        /// Sets the simulation playback speed multiplier (e.g., 1x, 5x, 10x).
        /// </summary>
        public void SetPlaybackSpeed(float multiplier)
        {
            _speedMultiplier = Mathf.Clamp(multiplier, 0.25f, 20f);
            UpdateSpeedButtonsVisuals();
            if (_activeReport != null && _activeReport.Ticks != null && _currentTickIndex >= 0 && _currentTickIndex < _activeReport.Ticks.Count)
            {
                SimulationTickSnapshot snap = _activeReport.Ticks[_currentTickIndex];
                if (tickProgressLabel != null)
                {
                    tickProgressLabel.text = string.Format(
                        "Tick: {0:00} / {1:00}   |   OrbitProgress: {2:0.00}   |   Speed: {3:0.#}x",
                        _currentTickIndex + 1,
                        _activeReport.Ticks.Count,
                        snap.OrbitProgress,
                        _speedMultiplier);
                }
            }
            OnSpeedChanged?.Invoke(_speedMultiplier);
        }

        /// <summary>
        /// Switches simulation playback speed to 1x.
        /// </summary>
        public void SetSpeed1x() => SetPlaybackSpeed(1.0f);

        /// <summary>
        /// Switches simulation playback speed to 5x.
        /// </summary>
        public void SetSpeed5x() => SetPlaybackSpeed(5.0f);

        /// <summary>
        /// Switches simulation playback speed to 10x.
        /// </summary>
        public void SetSpeed10x() => SetPlaybackSpeed(10.0f);

        /// <summary>
        /// Loads or hot-swaps a <see cref="MissionReport"/> into the radar for step-by-step playback.
        /// When <paramref name="resetTickIndex"/> is false, preserves the current tick index so mid-flight manual commands recalculate seamlessly.
        /// </summary>
        public void LoadMissionReport(
            MissionReport report,
            OrbitDefinition orbit = null,
            bool autoPlay = true,
            bool resetTickIndex = true)
        {
            EnsureRadarHierarchy();

            if (orbit != null)
            {
                _currentOrbit = orbit;
            }
            else if (_currentOrbit == null)
            {
                _currentOrbit = CreateDefaultLeoOrbit();
            }

            _activeReport = report;
            RefreshOrbitVisuals();

            if (_activeReport == null || _activeReport.Ticks == null || _activeReport.Ticks.Count == 0)
            {
                _isPlaying = false;
                return;
            }

            if (resetTickIndex)
            {
                _currentTickIndex = 0;
                _tickElapsedSeconds = 0f;
            }
            else
            {
                _currentTickIndex = Mathf.Clamp(_currentTickIndex, 0, _activeReport.Ticks.Count - 1);
            }

            _isPlaying = autoPlay;
            if (startTestFlightButtonLabel != null)
            {
                startTestFlightButtonLabel.text = _isPlaying ? "Restart Flight Test" : "Start Flight Test";
            }

            SimulationTickSnapshot snap = _activeReport.Ticks[_currentTickIndex];
            ApplyTickSnapshot(snap, _currentTickIndex, _activeReport.Ticks.Count);
            SetSatelliteOrbitProgress(snap.OrbitProgress);
        }

        /// <summary>
        /// Toggles play/pause state for the step-by-step tick playback.
        /// </summary>
        public void TogglePlayPause()
        {
            if (_activeReport == null || _activeReport.Ticks == null || _activeReport.Ticks.Count == 0)
            {
                StartTestFlight();
                return;
            }

            if (!_isPlaying && _currentTickIndex >= _activeReport.Ticks.Count - 1)
            {
                _currentTickIndex = 0;
                _tickElapsedSeconds = 0f;
                SimulationTickSnapshot firstSnap = _activeReport.Ticks[0];
                ApplyTickSnapshot(firstSnap, 0, _activeReport.Ticks.Count);
                SetSatelliteOrbitProgress(firstSnap.OrbitProgress);
            }

            _isPlaying = !_isPlaying;
        }

        /// <summary>
        /// Steps forward or backward by <paramref name="deltaTicks"/> and updates the radar and telemetry listeners immediately.
        /// </summary>
        public void StepSingleTick(int deltaTicks = 1)
        {
            if (_activeReport == null || _activeReport.Ticks == null || _activeReport.Ticks.Count == 0)
            {
                return;
            }

            _isPlaying = false;
            _tickElapsedSeconds = 0f;
            _currentTickIndex = Mathf.Clamp(_currentTickIndex + deltaTicks, 0, _activeReport.Ticks.Count - 1);

            SimulationTickSnapshot snap = _activeReport.Ticks[_currentTickIndex];
            ApplyTickSnapshot(snap, _currentTickIndex, _activeReport.Ticks.Count);
            SetSatelliteOrbitProgress(snap.OrbitProgress);
        }

        /// <summary>
        /// Jumps directly to a specific tick index in <see cref="MissionReport.Ticks"/>.
        /// </summary>
        public void JumpToTick(int tickIndex, bool pausePlayback = false)
        {
            if (_activeReport == null || _activeReport.Ticks == null || _activeReport.Ticks.Count == 0)
            {
                return;
            }

            if (pausePlayback)
            {
                _isPlaying = false;
            }

            _tickElapsedSeconds = 0f;
            _currentTickIndex = Mathf.Clamp(tickIndex, 0, _activeReport.Ticks.Count - 1);

            SimulationTickSnapshot snap = _activeReport.Ticks[_currentTickIndex];
            ApplyTickSnapshot(snap, _currentTickIndex, _activeReport.Ticks.Count);
            SetSatelliteOrbitProgress(snap.OrbitProgress);
        }

        private void Update()
        {
            if (!_isPlaying || _activeReport == null || _activeReport.Ticks == null || _activeReport.Ticks.Count == 0)
            {
                return;
            }

            int totalTicks = _activeReport.Ticks.Count;
            float secondsPerTick = Mathf.Max(0.05f, baseSecondsPerTick / Mathf.Max(0.1f, _speedMultiplier));

            _tickElapsedSeconds += Time.deltaTime;

            while (_tickElapsedSeconds >= secondsPerTick && _isPlaying)
            {
                _tickElapsedSeconds -= secondsPerTick;
                _currentTickIndex++;

                if (_currentTickIndex >= totalTicks)
                {
                    // Complete the full revolution smoothly back to u = 1.0 (top origin)
                    _isPlaying = false;
                    _currentTickIndex = totalTicks - 1;
                    SetSatelliteOrbitProgress(1.0f);

                    if (statusHeaderLabel != null)
                    {
                        statusHeaderLabel.text = string.Format(
                            "FLIGHT TEST COMPLETE: {0} ({1} ticks simulated)",
                            _currentOrbit != null ? _currentOrbit.Name : "Orbit",
                            totalTicks);
                    }

                    if (startTestFlightButtonLabel != null)
                    {
                        startTestFlightButtonLabel.text = "Start Flight Test";
                    }

                    OnTestFlightCompleted?.Invoke(_activeReport);
                    return;
                }

                SimulationTickSnapshot currentSnap = _activeReport.Ticks[_currentTickIndex];
                ApplyTickSnapshot(currentSnap, _currentTickIndex, totalTicks);
            }

            if (_isPlaying && _currentTickIndex < totalTicks)
            {
                SimulationTickSnapshot tickSnap = _activeReport.Ticks[_currentTickIndex];
                float startProgress = tickSnap.OrbitProgress;
                float endProgress;

                if (_currentTickIndex + 1 < totalTicks && _activeReport.Ticks[_currentTickIndex + 1].OrbitProgress > startProgress)
                {
                    endProgress = _activeReport.Ticks[_currentTickIndex + 1].OrbitProgress;
                }
                else
                {
                    // Smoothly interpolate the final tick of the orbit (0.95 -> 1.00)
                    float stepSize = 1.0f / Mathf.Max(1, MockMissionSimulator.TicksPerOrbit);
                    endProgress = Mathf.Min(1.0f, startProgress + stepSize);
                }

                float t = Mathf.Clamp01(_tickElapsedSeconds / secondsPerTick);
                float smoothProgress = Mathf.Lerp(startProgress, endProgress, t);
                SetSatelliteOrbitProgress(smoothProgress);
            }
        }

        private void RefreshOrbitVisuals()
        {
            if (_currentOrbit == null) return;

            float eclipseFraction = Mathf.Clamp01(_currentOrbit.EclipseFraction);
            float sunlightFraction = Mathf.Clamp01(1.0f - eclipseFraction);
            float commFraction = Mathf.Clamp01(_currentOrbit.CommWindowFraction);

            // 1. Central Planet Disc
            if (planetImage != null)
            {
                if (planetImage.sprite == null)
                {
                    planetImage.sprite = RadarProceduralSprites.GetDiscSprite();
                }
                planetImage.color = GetPlanetColor(_currentOrbit.Type, _currentOrbit.Id);
            }

            if (planetNameLabel != null)
            {
                planetNameLabel.text = string.Format(
                    "{0}\nSolar: {1:0}% | Delay: {2:0.#}s",
                    _currentOrbit.Name,
                    _currentOrbit.SolarFluxFactor * 100f,
                    _currentOrbit.SignalDelaySeconds);
            }

            // 2. Orbit Trajectory Ring
            if (orbitRingImage != null)
            {
                if (orbitRingImage.sprite == null)
                {
                    orbitRingImage.sprite = RadarProceduralSprites.GetOrbitRingSprite();
                }
                orbitRingImage.color = new Color(0.55f, 0.78f, 1.0f, 0.85f);
            }

            // 3. Sunlight Sector (Warm Golden Sector where OrbitProgress < 1 - EclipseFraction)
            // Filled clockwise from Top (u = 0.0) by (1.0 - EclipseFraction).
            if (sunlightSectorImage != null)
            {
                if (sunlightSectorImage.sprite == null)
                {
                    sunlightSectorImage.sprite = RadarProceduralSprites.GetSunlightConeSprite();
                }
                sunlightSectorImage.type = Image.Type.Filled;
                sunlightSectorImage.fillMethod = Image.FillMethod.Radial360;
                sunlightSectorImage.fillOrigin = (int)Image.Origin360.Top;
                sunlightSectorImage.fillClockwise = true;
                sunlightSectorImage.fillAmount = sunlightFraction;
                sunlightSectorImage.color = new Color(1.0f, 0.80f, 0.20f, 0.34f);
            }

            if (sunlightSectorLabel != null)
            {
                sunlightSectorLabel.text = string.Format(
                    "SUNLIGHT ({0:0}%)\nu < {1:0.00}",
                    sunlightFraction * 100f,
                    sunlightFraction);
            }

            if (sunlightLabelRect != null)
            {
                float midSunlightProgress = Mathf.Lerp(commFraction, sunlightFraction, 0.55f);
                sunlightLabelRect.anchoredPosition = OrbitProgressToPosition(midSunlightProgress, orbitRadiusPx * 0.58f);
            }

            // 4. Shadow / Eclipse Sector (High-Contrast Indigo-Violet Cone where OrbitProgress >= 1 - EclipseFraction)
            // Filled counter-clockwise from Top (u = 1.0) by EclipseFraction, covering [1.0 - EclipseFraction .. 1.0].
            if (eclipseSectorImage != null)
            {
                if (eclipseSectorImage.sprite == null)
                {
                    eclipseSectorImage.sprite = RadarProceduralSprites.GetShadowConeSprite();
                }
                eclipseSectorImage.type = Image.Type.Filled;
                eclipseSectorImage.fillMethod = Image.FillMethod.Radial360;
                eclipseSectorImage.fillOrigin = (int)Image.Origin360.Top;
                eclipseSectorImage.fillClockwise = false;
                eclipseSectorImage.fillAmount = eclipseFraction;
                eclipseSectorImage.color = new Color(0.34f, 0.24f, 0.72f, 0.56f);
            }

            if (eclipseSectorLabel != null)
            {
                eclipseSectorLabel.text = string.Format(
                    "SHADOW ({0:0}%)\nu >= {1:0.00}",
                    eclipseFraction * 100f,
                    sunlightFraction);
            }

            if (eclipseLabelRect != null)
            {
                float midEclipseProgress = 1.0f - (eclipseFraction * 0.5f);
                eclipseLabelRect.anchoredPosition = OrbitProgressToPosition(midEclipseProgress, orbitRadiusPx * 0.64f);
            }

            // 5. Communication Window Sector (Cyan-Green Outer Arc where OrbitProgress < CommWindowFraction)
            // Filled clockwise from Top (u = 0.0) by CommWindowFraction, covering [0.0 .. CommWindowFraction].
            if (commSectorImage != null)
            {
                if (commSectorImage.sprite == null)
                {
                    commSectorImage.sprite = RadarProceduralSprites.GetCommSectorSprite();
                }
                commSectorImage.type = Image.Type.Filled;
                commSectorImage.fillMethod = Image.FillMethod.Radial360;
                commSectorImage.fillOrigin = (int)Image.Origin360.Top;
                commSectorImage.fillClockwise = true;
                commSectorImage.fillAmount = commFraction;
                commSectorImage.color = new Color(0.08f, 0.96f, 0.74f, 0.70f);
            }

            if (commSectorLabel != null)
            {
                commSectorLabel.text = string.Format(
                    "COMM LINK ({0:0}%)\nu < {1:0.00}",
                    commFraction * 100f,
                    commFraction);
            }

            if (commLabelRect != null)
            {
                float midCommProgress = commFraction * 0.5f;
                commLabelRect.anchoredPosition = OrbitProgressToPosition(midCommProgress, orbitRadiusPx * 0.80f);
            }

            // 6. Satellite Triangle Sprite
            if (satelliteImage != null && satelliteImage.sprite == null)
            {
                satelliteImage.sprite = RadarProceduralSprites.GetSatelliteTriangleSprite();
            }

            SetSatelliteOrbitProgress(_currentDisplayedProgress);
            if (!_isPlaying)
            {
                RefreshIdleSummaryText();
            }
        }

        /// <summary>
        /// Positions and rotates the satellite sprite along the orbit ring according to normalized progress <c>[0.0 .. 1.0]</c>.
        /// <c>u = 0.0</c> is at the Top (12 o'clock) of the orbit ring and advances clockwise.
        /// </summary>
        public void SetSatelliteOrbitProgress(float orbitProgress)
        {
            float wrappedProgress = Mathf.Repeat(orbitProgress, 1.0f);
            if (Mathf.Approximately(orbitProgress, 1.0f))
            {
                wrappedProgress = 0.0f;
            }

            _currentDisplayedProgress = Mathf.Clamp01(orbitProgress);

            if (satelliteRect != null)
            {
                satelliteRect.anchoredPosition = OrbitProgressToPosition(wrappedProgress, orbitRadiusPx);
                float tangentAngleDeg = -(wrappedProgress * 360f) - 90f;
                satelliteRect.localEulerAngles = new Vector3(0f, 0f, tangentAngleDeg);
            }

            bool inEclipseZone = _currentOrbit != null &&
                                 wrappedProgress >= (1.0f - Mathf.Clamp01(_currentOrbit.EclipseFraction) - 0.0001f);
            bool inCommZone = _currentOrbit != null &&
                              wrappedProgress < (Mathf.Clamp01(_currentOrbit.CommWindowFraction) - 0.0001f);

            if (satelliteImage != null)
            {
                if (inEclipseZone)
                {
                    satelliteImage.color = new Color(0.68f, 0.72f, 1.0f, 1.0f);
                }
                else if (inCommZone)
                {
                    satelliteImage.color = new Color(0.20f, 1.0f, 0.82f, 1.0f);
                }
                else
                {
                    satelliteImage.color = new Color(1.0f, 0.92f, 0.32f, 1.0f);
                }
            }

            UpdateZoneBadge(wrappedProgress, inCommZone, inEclipseZone);
        }

        private void ApplyTickSnapshot(SimulationTickSnapshot snapshot, int tickIndex, int totalTicks)
        {
            if (snapshot == null) return;

            if (statusHeaderLabel != null)
            {
                statusHeaderLabel.text = string.Format(
                    "ORBITAL FLIGHT: {0} (Orbit #{1})",
                    _currentOrbit != null ? _currentOrbit.Name : "LEO",
                    snapshot.OrbitNumber);
            }

            float eclipseThreshold = _currentOrbit != null ? (1.0f - _currentOrbit.EclipseFraction) : 0.65f;
            float commThreshold = _currentOrbit != null ? _currentOrbit.CommWindowFraction : 0.25f;
            bool inEclipseByOrbit = snapshot.OrbitProgress >= (eclipseThreshold - 0.0001f);
            bool inCommByOrbit = snapshot.OrbitProgress < (commThreshold - 0.0001f);

            if (tickProgressLabel != null)
            {
                tickProgressLabel.text = string.Format(
                    "Tick: {0:00} / {1:00}   |   OrbitProgress: {2:0.00}   |   Speed: {3:0.#}x",
                    tickIndex + 1,
                    totalTicks,
                    snapshot.OrbitProgress,
                    _speedMultiplier);
            }

            if (telemetrySnapshotLabel != null)
            {
                telemetrySnapshotLabel.text = string.Format(
                    "Battery: {0:0}% ({1:0.0} Wh)  |  SSD: {2:0}% ({3:0.0} GB)  |  Solar: {4:0} W  |  Science: {5:0}",
                    snapshot.BatteryPercent,
                    snapshot.BatteryWh,
                    snapshot.SsdUsedPercent,
                    snapshot.SsdUsedGb,
                    inEclipseByOrbit ? 0f : snapshot.SolarGenWatts,
                    snapshot.DeliveredScience);
            }

            UpdateZoneBadge(snapshot.OrbitProgress, inCommByOrbit, inEclipseByOrbit);
            OnTickUpdated?.Invoke(snapshot, tickIndex);
        }

        private void UpdateZoneBadge(float progress, bool inCommZone, bool inEclipseZone)
        {
            if (zoneBadgeLabel == null) return;

            float eclipseStart = _currentOrbit != null ? (1.0f - _currentOrbit.EclipseFraction) : 0.65f;
            float commEnd = _currentOrbit != null ? _currentOrbit.CommWindowFraction : 0.25f;

            if (inEclipseZone)
            {
                zoneBadgeLabel.text = string.Format(
                    "ZONE: PLANETARY SHADOW / ECLIPSE (OrbitProgress {0:0.00} >= {1:0.00})",
                    progress,
                    eclipseStart);
                if (zoneBadgeBackground != null)
                {
                    zoneBadgeBackground.color = new Color(0.24f, 0.16f, 0.48f, 0.96f);
                }
            }
            else if (inCommZone)
            {
                zoneBadgeLabel.text = string.Format(
                    "ZONE: COMM WINDOW + SUNLIGHT (OrbitProgress {0:0.00} < {1:0.00})",
                    progress,
                    commEnd);
                if (zoneBadgeBackground != null)
                {
                    zoneBadgeBackground.color = new Color(0.06f, 0.44f, 0.38f, 0.96f);
                }
            }
            else
            {
                zoneBadgeLabel.text = string.Format(
                    "ZONE: SUNLIGHT SECTOR ({1:0.00} <= OrbitProgress {0:0.00} < {2:0.00})",
                    progress,
                    commEnd,
                    eclipseStart);
                if (zoneBadgeBackground != null)
                {
                    zoneBadgeBackground.color = new Color(0.46f, 0.34f, 0.06f, 0.96f);
                }
            }
        }

        private void RefreshIdleSummaryText()
        {
            if (_currentOrbit == null) return;

            if (statusHeaderLabel != null)
            {
                string rocketTitle = _currentRocket != null ? _currentRocket.Name : "Arrow-1 (Default)";
                statusHeaderLabel.text = string.Format("2D ORBIT RADAR: {0}  |  Rocket: {1}", _currentOrbit.Name, rocketTitle);
            }

            if (tickProgressLabel != null)
            {
                float sunFraction = 1.0f - _currentOrbit.EclipseFraction;
                tickProgressLabel.text = string.Format(
                    "Ready (20 ticks/orbit)  |  Sunlight: {0:0}% (u < {1:0.00})  |  Shadow: {2:0}% (u >= {1:0.00})  |  Comm: {3:0}% (u < {4:0.00})",
                    sunFraction * 100f,
                    sunFraction,
                    _currentOrbit.EclipseFraction * 100f,
                    _currentOrbit.CommWindowFraction * 100f,
                    _currentOrbit.CommWindowFraction);
            }

            if (telemetrySnapshotLabel != null)
            {
                telemetrySnapshotLabel.text = "Click 'Start Flight Test' to simulate 20 orbital steps across Comm Window, Sunlight, and Shadow.";
            }
        }

        private static Vector2 OrbitProgressToPosition(float orbitProgress, float radius)
        {
            // u = 0.0 -> Top (0, +radius), advancing clockwise
            float angleRad = orbitProgress * Mathf.PI * 2.0f;
            return new Vector2(Mathf.Sin(angleRad) * radius, Mathf.Cos(angleRad) * radius);
        }

        private static Color GetPlanetColor(OrbitType type, string orbitId)
        {
            if (!string.IsNullOrEmpty(orbitId) && orbitId.IndexOf("venus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(0.92f, 0.72f, 0.28f, 1.0f);
            }

            switch (type)
            {
                case OrbitType.LowEarth:
                    return new Color(0.18f, 0.56f, 0.94f, 1.0f);
                case OrbitType.Lunar:
                    return new Color(0.74f, 0.78f, 0.84f, 1.0f);
                case OrbitType.Mars:
                    return new Color(0.88f, 0.36f, 0.22f, 1.0f);
                case OrbitType.DeepSpace:
                    return new Color(0.90f, 0.68f, 0.28f, 1.0f);
                default:
                    return new Color(0.22f, 0.60f, 0.95f, 1.0f);
            }
        }

        /// <summary>
        /// Creates the default Low Earth Orbit (LEO 400 km) definition for standalone/mock initialization.
        /// </summary>
        public static OrbitDefinition CreateDefaultLeoOrbit()
        {
            return new OrbitDefinition
            {
                Id = "orbit_leo",
                Name = "LEO 400 km (Earth)",
                Type = OrbitType.LowEarth,
                OrbitPeriodHours = 1.5f,
                EclipseFraction = 0.35f,
                CommWindowFraction = 0.25f,
                SolarFluxFactor = 1.0f,
                SignalDelaySeconds = 1.0f,
                BaseRadiation = 5.0f
            };
        }

        private void BindButtons()
        {
            if (startTestFlightButton != null)
            {
                startTestFlightButton.onClick.RemoveListener(StartTestFlight);
                startTestFlightButton.onClick.AddListener(StartTestFlight);
            }

            if (speed1xButton != null)
            {
                speed1xButton.onClick.RemoveAllListeners();
                speed1xButton.onClick.AddListener(() => SetPlaybackSpeed(1.0f));
            }

            if (speed3xButton != null)
            {
                speed3xButton.onClick.RemoveAllListeners();
                speed3xButton.onClick.AddListener(() => SetPlaybackSpeed(3.0f));
            }

            if (speed5xButton != null)
            {
                speed5xButton.onClick.RemoveAllListeners();
                speed5xButton.onClick.AddListener(() => SetPlaybackSpeed(5.0f));
            }

            if (speed10xButton != null)
            {
                speed10xButton.onClick.RemoveAllListeners();
                speed10xButton.onClick.AddListener(() => SetPlaybackSpeed(10.0f));
            }
        }

        private void UpdateSpeedButtonsVisuals()
        {
            HighlightSpeedButton(speed1xButton, Mathf.Approximately(_speedMultiplier, 1.0f));
            HighlightSpeedButton(speed3xButton, Mathf.Approximately(_speedMultiplier, 3.0f));
            HighlightSpeedButton(speed5xButton, Mathf.Approximately(_speedMultiplier, 5.0f));
            HighlightSpeedButton(speed10xButton, Mathf.Approximately(_speedMultiplier, 10.0f));
        }

        private static void HighlightSpeedButton(Button btn, bool active)
        {
            if (btn == null) return;
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = active
                    ? new Color(0.14f, 0.62f, 0.88f, 1.0f)
                    : new Color(0.13f, 0.20f, 0.32f, 1.0f);
            }
        }

        /// <summary>
        /// Builds or binds the complete 2D radar UI hierarchy so the prefab works reliably without null references.
        /// </summary>
        public void EnsureRadarHierarchy()
        {
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
            {
                rootRt = gameObject.AddComponent<RectTransform>();
            }

            Image bgImage = GetComponent<Image>();
            if (bgImage == null)
            {
                bgImage = gameObject.AddComponent<Image>();
                bgImage.color = new Color(0.05f, 0.09f, 0.16f, 0.96f);
            }

            Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Header Status Label
            if (statusHeaderLabel == null)
            {
                statusHeaderLabel = FindOrCreateText(
                    rootRt,
                    "StatusHeaderLabel",
                    new Vector2(0.03f, 0.91f),
                    new Vector2(0.97f, 0.98f),
                    "2D ORBIT RADAR (S1-08)",
                    21,
                    FontStyle.Bold,
                    new Color(0.88f, 0.95f, 1.0f, 1.0f),
                    TextAnchor.MiddleLeft,
                    builtinFont);
            }

            // Sector Color Legend Bar (Top Subheader)
            FindOrCreateText(
                rootRt,
                "RadarLegendLabel",
                new Vector2(0.03f, 0.865f),
                new Vector2(0.97f, 0.91f),
                "SECTORS:   [GOLD] SUNLIGHT (u < 1 - Eclipse)     [PURPLE] SHADOW / ECLIPSE (u >= 1 - Eclipse)     [CYAN] COMM WINDOW (u < Comm)",
                13,
                FontStyle.Bold,
                new Color(0.82f, 0.90f, 1.0f, 0.92f),
                TextAnchor.MiddleCenter,
                builtinFont);

            // Radar Viewport Box (center)
            if (radarViewportRect == null)
            {
                Transform existingVp = rootRt.Find("RadarViewport");
                if (existingVp != null)
                {
                    radarViewportRect = existingVp.GetComponent<RectTransform>();
                }
                else
                {
                    var vpObj = new GameObject("RadarViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    vpObj.transform.SetParent(rootRt, false);
                    radarViewportRect = vpObj.GetComponent<RectTransform>();
                    radarViewportRect.anchorMin = new Vector2(0.5f, 0.53f);
                    radarViewportRect.anchorMax = new Vector2(0.5f, 0.53f);
                    radarViewportRect.pivot = new Vector2(0.5f, 0.5f);
                    radarViewportRect.anchoredPosition = Vector2.zero;
                    radarViewportRect.sizeDelta = new Vector2(430f, 430f);

                    var vpImg = vpObj.GetComponent<Image>();
                    vpImg.sprite = RadarProceduralSprites.GetDiscSprite();
                    vpImg.color = new Color(0.02f, 0.05f, 0.10f, 0.96f);
                }
            }

            // Crosshair lines inside RadarViewport
            EnsureRadarCrosshairs(radarViewportRect, builtinFont);

            // 1. Sunlight Sector (Warm Golden Sector: OrbitProgress < 1 - EclipseFraction)
            if (sunlightSectorImage == null)
            {
                sunlightSectorImage = FindOrCreateCenteredImage(
                    radarViewportRect,
                    "SunlightSector",
                    new Vector2(390f, 390f),
                    RadarProceduralSprites.GetSunlightConeSprite(),
                    new Color(1.0f, 0.80f, 0.20f, 0.34f));
                sunlightSectorImage.type = Image.Type.Filled;
                sunlightSectorImage.fillMethod = Image.FillMethod.Radial360;
                sunlightSectorImage.fillOrigin = (int)Image.Origin360.Top;
                sunlightSectorImage.fillClockwise = true;
                sunlightSectorImage.fillAmount = 0.65f;
            }

            // 2. Shadow / Eclipse Sector (Indigo-Purple Cone: OrbitProgress >= 1 - EclipseFraction)
            if (eclipseSectorImage == null)
            {
                eclipseSectorImage = FindOrCreateCenteredImage(
                    radarViewportRect,
                    "EclipseShadowSector",
                    new Vector2(390f, 390f),
                    RadarProceduralSprites.GetShadowConeSprite(),
                    new Color(0.34f, 0.24f, 0.72f, 0.56f));
                eclipseSectorImage.type = Image.Type.Filled;
                eclipseSectorImage.fillMethod = Image.FillMethod.Radial360;
                eclipseSectorImage.fillOrigin = (int)Image.Origin360.Top;
                eclipseSectorImage.fillClockwise = false;
                eclipseSectorImage.fillAmount = 0.35f;
            }

            // 3. Communication Window Sector (Cyan-Green Band: OrbitProgress < CommWindowFraction)
            if (commSectorImage == null)
            {
                commSectorImage = FindOrCreateCenteredImage(
                    radarViewportRect,
                    "CommWindowSector",
                    new Vector2(396f, 396f),
                    RadarProceduralSprites.GetCommSectorSprite(),
                    new Color(0.08f, 0.96f, 0.74f, 0.70f));
                commSectorImage.type = Image.Type.Filled;
                commSectorImage.fillMethod = Image.FillMethod.Radial360;
                commSectorImage.fillOrigin = (int)Image.Origin360.Top;
                commSectorImage.fillClockwise = true;
                commSectorImage.fillAmount = 0.25f;
            }

            // 4. Orbit Circle Track
            if (orbitRingImage == null)
            {
                orbitRingImage = FindOrCreateCenteredImage(
                    radarViewportRect,
                    "OrbitRing",
                    new Vector2(404f, 404f),
                    RadarProceduralSprites.GetOrbitRingSprite(),
                    new Color(0.55f, 0.78f, 1.0f, 0.85f));
            }

            // 5. Center Planet Disc
            if (planetImage == null)
            {
                planetImage = FindOrCreateCenteredImage(
                    radarViewportRect,
                    "PlanetDisc",
                    new Vector2(120f, 120f),
                    RadarProceduralSprites.GetDiscSprite(),
                    new Color(0.18f, 0.56f, 0.94f, 1.0f));
            }

            if (planetNameLabel == null && planetImage != null)
            {
                planetNameLabel = FindOrCreateText(
                    planetImage.rectTransform,
                    "PlanetNameLabel",
                    new Vector2(-0.35f, -0.35f),
                    new Vector2(1.35f, 1.35f),
                    "LEO 400 km",
                    13,
                    FontStyle.Bold,
                    Color.white,
                    TextAnchor.MiddleCenter,
                    builtinFont);
            }

            // 6. Floating Sector Labels (Sunlight, Shadow, Comm Link)
            if (sunlightSectorLabel == null)
            {
                sunlightSectorLabel = FindOrCreateFloatingLabel(
                    radarViewportRect,
                    "SunlightSectorLabel",
                    new Vector2(65f, -75f),
                    "SUNLIGHT (65%)",
                    new Color(1.0f, 0.90f, 0.42f, 0.98f),
                    builtinFont,
                    out sunlightLabelRect);
            }
            else if (sunlightLabelRect == null)
            {
                sunlightLabelRect = sunlightSectorLabel.rectTransform;
            }

            if (eclipseSectorLabel == null)
            {
                eclipseSectorLabel = FindOrCreateFloatingLabel(
                    radarViewportRect,
                    "EclipseSectorLabel",
                    new Vector2(-110f, 60f),
                    "SHADOW (35%)",
                    new Color(0.84f, 0.78f, 1.0f, 0.98f),
                    builtinFont,
                    out eclipseLabelRect);
            }
            else if (eclipseLabelRect == null)
            {
                eclipseLabelRect = eclipseSectorLabel.rectTransform;
            }

            if (commSectorLabel == null)
            {
                commSectorLabel = FindOrCreateFloatingLabel(
                    radarViewportRect,
                    "CommSectorLabel",
                    new Vector2(105f, 100f),
                    "COMM LINK (25%)",
                    new Color(0.42f, 1.0f, 0.86f, 0.98f),
                    builtinFont,
                    out commLabelRect);
            }
            else if (commLabelRect == null)
            {
                commLabelRect = commSectorLabel.rectTransform;
            }

            // 7. Satellite Marker Sprite (Triangle Spacecraft)
            if (satelliteImage == null)
            {
                satelliteImage = FindOrCreateCenteredImage(
                    radarViewportRect,
                    "SatelliteMarker",
                    new Vector2(34f, 34f),
                    RadarProceduralSprites.GetSatelliteTriangleSprite(),
                    new Color(0.20f, 1.0f, 0.82f, 1.0f));
                satelliteRect = satelliteImage.rectTransform;
                satelliteRect.anchoredPosition = new Vector2(0f, orbitRadiusPx);
            }
            else if (satelliteRect == null)
            {
                satelliteRect = satelliteImage.rectTransform;
            }

            // 8. Zone Badge Bar
            if (zoneBadgeBackground == null)
            {
                Transform existingBadge = rootRt.Find("ZoneBadge");
                if (existingBadge != null)
                {
                    zoneBadgeBackground = existingBadge.GetComponent<Image>();
                    zoneBadgeLabel = existingBadge.GetComponentInChildren<Text>();
                }
                else
                {
                    var badgeObj = new GameObject("ZoneBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    badgeObj.transform.SetParent(rootRt, false);
                    var brt = badgeObj.GetComponent<RectTransform>();
                    brt.anchorMin = new Vector2(0.04f, 0.17f);
                    brt.anchorMax = new Vector2(0.96f, 0.225f);
                    brt.offsetMin = Vector2.zero;
                    brt.offsetMax = Vector2.zero;

                    zoneBadgeBackground = badgeObj.GetComponent<Image>();
                    zoneBadgeBackground.color = new Color(0.06f, 0.44f, 0.38f, 0.96f);

                    zoneBadgeLabel = FindOrCreateText(
                        brt,
                        "ZoneBadgeLabel",
                        Vector2.zero,
                        Vector2.one,
                        "ZONE: COMM WINDOW + SUNLIGHT (OrbitProgress 0.00 < 0.25)",
                        15,
                        FontStyle.Bold,
                        Color.white,
                        TextAnchor.MiddleCenter,
                        builtinFont);
                }
            }

            // 9. Tick & Telemetry Readout Labels
            if (tickProgressLabel == null)
            {
                tickProgressLabel = FindOrCreateText(
                    rootRt,
                    "TickProgressLabel",
                    new Vector2(0.04f, 0.125f),
                    new Vector2(0.96f, 0.17f),
                    "Tick: 00 / 20   |   OrbitProgress: 0.00   |   Speed: 1x",
                    15,
                    FontStyle.Normal,
                    new Color(0.80f, 0.90f, 1.0f, 1.0f),
                    TextAnchor.MiddleCenter,
                    builtinFont);
            }

            if (telemetrySnapshotLabel == null)
            {
                telemetrySnapshotLabel = FindOrCreateText(
                    rootRt,
                    "TelemetrySnapshotLabel",
                    new Vector2(0.04f, 0.085f),
                    new Vector2(0.96f, 0.125f),
                    "Click 'Start Flight Test' to simulate 20 orbital steps across Comm Window, Sunlight, and Shadow.",
                    14,
                    FontStyle.Normal,
                    new Color(0.68f, 0.82f, 0.95f, 1.0f),
                    TextAnchor.MiddleCenter,
                    builtinFont);
            }

            // 10. Bottom Controls Bar ('Start Flight Test' + 1x / 3x / 5x)
            EnsureControlButtons(rootRt, builtinFont);
        }

        private static void EnsureRadarCrosshairs(RectTransform viewport, Font font)
        {
            if (viewport == null || viewport.Find("CrosshairH") != null) return;

            var hLine = new GameObject("CrosshairH", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hLine.transform.SetParent(viewport, false);
            var hRt = hLine.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0.5f, 0.5f);
            hRt.anchorMax = new Vector2(0.5f, 0.5f);
            hRt.sizeDelta = new Vector2(400f, 1.5f);
            hLine.GetComponent<Image>().color = new Color(0.25f, 0.42f, 0.65f, 0.28f);

            var vLine = new GameObject("CrosshairV", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            vLine.transform.SetParent(viewport, false);
            var vRt = vLine.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(0.5f, 0.5f);
            vRt.anchorMax = new Vector2(0.5f, 0.5f);
            vRt.sizeDelta = new Vector2(1.5f, 400f);
            vLine.GetComponent<Image>().color = new Color(0.25f, 0.42f, 0.65f, 0.28f);

            // Top Start Marker (u = 0.0)
            RectTransform dummy;
            FindOrCreateFloatingLabel(
                viewport,
                "OriginMarkerLabel",
                new Vector2(0f, 204f),
                "▼ u = 0.0 (ORBIT START)",
                new Color(0.75f, 0.92f, 1.0f, 0.92f),
                font,
                out dummy);
        }

        private void EnsureControlButtons(RectTransform rootRt, Font font)
        {
            Transform barTransform = rootRt.Find("RadarControlsBar");
            RectTransform barRt;
            if (barTransform == null)
            {
                var barObj = new GameObject("RadarControlsBar", typeof(RectTransform));
                barObj.transform.SetParent(rootRt, false);
                barRt = barObj.GetComponent<RectTransform>();
                barRt.anchorMin = new Vector2(0.04f, 0.015f);
                barRt.anchorMax = new Vector2(0.96f, 0.08f);
                barRt.offsetMin = Vector2.zero;
                barRt.offsetMax = Vector2.zero;
            }
            else
            {
                barRt = barTransform.GetComponent<RectTransform>();
            }

            if (startTestFlightButton == null)
            {
                startTestFlightButton = CreateButton(
                    barRt,
                    "Btn_StartTestFlight",
                    "Start Flight Test",
                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.52f, 1.0f),
                    new Color(0.12f, 0.58f, 0.36f, 1.0f),
                    font,
                    out startTestFlightButtonLabel);
            }

            if (speed1xButton == null)
            {
                Text dummy;
                speed1xButton = CreateButton(
                    barRt,
                    "Btn_Speed1x",
                    "1x",
                    new Vector2(0.54f, 0.0f),
                    new Vector2(0.68f, 1.0f),
                    new Color(0.14f, 0.62f, 0.88f, 1.0f),
                    font,
                    out dummy);
            }

            if (speed3xButton == null)
            {
                Transform existing3x = barRt.Find("Btn_Speed3x");
                if (existing3x != null)
                {
                    speed3xButton = existing3x.GetComponent<Button>();
                }
            }

            if (speed5xButton == null)
            {
                Text dummy;
                speed5xButton = CreateButton(
                    barRt,
                    "Btn_Speed5x",
                    "5x",
                    new Vector2(0.70f, 0.0f),
                    new Vector2(0.84f, 1.0f),
                    new Color(0.13f, 0.20f, 0.32f, 1.0f),
                    font,
                    out dummy);
            }

            if (speed10xButton == null)
            {
                Text dummy;
                speed10xButton = CreateButton(
                    barRt,
                    "Btn_Speed10x",
                    "10x",
                    new Vector2(0.86f, 0.0f),
                    new Vector2(1.0f, 1.0f),
                    new Color(0.13f, 0.20f, 0.32f, 1.0f),
                    font,
                    out dummy);
            }
        }

        private static Image FindOrCreateCenteredImage(
            RectTransform parent,
            string name,
            Vector2 size,
            Sprite sprite,
            Color color)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Image existingImg = existing.GetComponent<Image>();
                if (existingImg != null)
                {
                    if (existingImg.sprite == null) existingImg.sprite = sprite;
                    return existingImg;
                }
            }

            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            var img = obj.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text FindOrCreateFloatingLabel(
            RectTransform parent,
            string name,
            Vector2 anchoredPos,
            string defaultText,
            Color color,
            Font font,
            out RectTransform labelRt)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                labelRt = existing.GetComponent<RectTransform>();
                return existing.GetComponent<Text>();
            }

            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            labelRt = obj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.anchoredPosition = anchoredPos;
            labelRt.sizeDelta = new Vector2(180f, 44f);

            var txt = obj.GetComponent<Text>();
            txt.font = font;
            txt.text = defaultText;
            txt.fontSize = 13;
            txt.fontStyle = FontStyle.Bold;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            return txt;
        }

        private static Text FindOrCreateText(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string text,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment,
            Font font)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Text existingTxt = existing.GetComponent<Text>();
                if (existingTxt != null) return existingTxt;
            }

            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var txt = obj.GetComponent<Text>();
            txt.font = font;
            txt.text = text;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color bgColor,
            Font font,
            out Text buttonLabel)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                buttonLabel = existing.GetComponentInChildren<Text>();
                return existing.GetComponent<Button>();
            }

            var btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;

            buttonLabel = FindOrCreateText(
                rt,
                "Label",
                Vector2.zero,
                Vector2.one,
                label,
                16,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter,
                font);

            return btn;
        }
    }
}
