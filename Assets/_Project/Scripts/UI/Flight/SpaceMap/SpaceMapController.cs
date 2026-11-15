using System;
using System.Collections;
using System.Collections.Generic;
using AstraLink.Core;
using AstraLink.UI.Flight.SpaceMap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// Full-viewport interactive, astronomically ordered animated Solar System Space Map.
    /// Renders pixel twinkling starfield, blazing Sun, Mercury-to-Neptune animated GIF sprite sheets,
    /// moons (Earth Moon &amp; Jovian Io), random multi-variant animated Main Asteroid Belt,
    /// interactive pan/zoom/inspect camera, and live player spacecraft mission telemetry/launch trajectory.
    /// </summary>
    public class SpaceMapController : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IScrollHandler, IPointerClickHandler
    {
        public static SpaceMapController Instance { get; private set; }

        private const int AsteroidCount = 420;
        private const int TransferDotCount = 24;
        private const float MinZoom = 0.22f;
        private const float MaxZoom = 14.00f;

        [Header("Background & Environment Sprites (Fallback / Inspector Compatibility)")]
        [SerializeField] private Sprite starfieldSprite;
        [SerializeField] private Sprite launchPadSprite;
        [SerializeField] private Sprite eclipseOverlaySprite;

        [Header("Planet Sprites (Fallback / Inspector Compatibility)")]
        [SerializeField] private Sprite earthSprite;
        [SerializeField] private Sprite moonSprite;
        [SerializeField] private Sprite marsSprite;
        [SerializeField] private Sprite venusSprite;

        [Header("Rocket & VFX Sprites")]
        [SerializeField] private Sprite smallRocketSprite;
        [SerializeField] private Sprite reusableRocketSprite;
        [SerializeField] private Sprite heavyLiftRocketSprite;
        [SerializeField] private Sprite rocketFlameSprite;
        [SerializeField] private Sprite launchSmokeSprite;

        [Header("Satellite Sprites")]
        [SerializeField] private Sprite cubesatSprite;
        [SerializeField] private Sprite satelliteSprite;
        [SerializeField] private Sprite deepSpaceProbeSprite;

        [Header("Animation Timings & Mode")]
        [SerializeField] private float phase1AscentDurationSeconds = 1.6f;
        [SerializeField] private float phase2SeparationDurationSeconds = 0.45f;
        [SerializeField] private bool showSandboxTestBar = false;
        [SerializeField] private bool autoSetupDemoOnStart = true;

        private sealed class CelestialRuntimeNode
        {
            public SolarSystemBodyCatalog.BodyDefinition Spec;
            public RectTransform Rect;
            public Image PlanetImage;
            public RectTransform OrbitRingRect;
            public Graphic OrbitRingGraphic;
            public Image SelectionReticle;
            public Text LabelText;
            public Sprite[] Frames;
            public float FrameRate;
            public float AnimTime;
            public float CurrentOrbitAngleDeg;
            public Vector2 CurrentStagePos;
            public float HoverBlend;
        }

        private sealed class AsteroidRuntimeNode
        {
            public RectTransform Rect;
            public Image Img;
            public Sprite[] Frames;
            public float FrameRate;
            public float AnimTime;
            public float OrbitRadiusPx;
            public float OrbitAngleDeg;
            public float OrbitSpeedDegPerSec;
            public float SpinSpeedDegPerSec;
            public float SpinAngleDeg;
            public float RadialWobbleAmp;
            public float WobblePhase;
        }

        private sealed class SolarBodyHitProxy : MonoBehaviour,
            IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
            IBeginDragHandler, IDragHandler, IScrollHandler
        {
            public SpaceMapController Owner;
            public string BodyId;

            public void OnPointerEnter(PointerEventData eventData) => Owner?.SetHoveredBody(BodyId);
            public void OnPointerExit(PointerEventData eventData) => Owner?.ClearHoveredBody(BodyId);
            public void OnPointerClick(PointerEventData eventData)
            {
                if (!eventData.dragging)
                {
                    Owner?.SelectBodyById(BodyId, focusCamera: true);
                }
            }
            public void OnBeginDrag(PointerEventData eventData) => Owner?.OnBeginDrag(eventData);
            public void OnDrag(PointerEventData eventData) => Owner?.OnDrag(eventData);
            public void OnScroll(PointerEventData eventData) => Owner?.OnScroll(eventData);
        }

        private SolarSystemArtBank _artBank;
        private PixelStarfieldLayer _starfieldLayer;

        // Cached mission config
        private MissionGoal _currentGoal;
        private OrbitDefinition _currentOrbit;
        private RocketOption _currentRocket;
        private readonly List<SatelliteComponent> _currentComponents = new List<SatelliteComponent>();

        // Hierarchy containers
        private RectTransform _mapViewportRect;
        private RectTransform _stageRect;
        private RectTransform _orbitsLayerRect;
        private RectTransform _asteroidsLayerRect;
        private RectTransform _bodiesLayerRect;
        private RectTransform _missionLayerRect;

        private readonly List<CelestialRuntimeNode> _bodies = new List<CelestialRuntimeNode>();
        private readonly Dictionary<string, CelestialRuntimeNode> _bodyMap = new Dictionary<string, CelestialRuntimeNode>(StringComparer.Ordinal);
        private readonly List<AsteroidRuntimeNode> _asteroids = new List<AsteroidRuntimeNode>();

        // Mission overlay elements (transfer arc, target orbit halo, sectors, player spacecraft & launch rocket)
        private Image _sunCoronaGlow;
        private RectTransform _sunlightSectorRect;
        private Image _sunlightSectorImg;
        private RectTransform _eclipseSectorRect;
        private Image _eclipseSectorImg;
        private RectTransform _commsSectorRect;
        private Image _commsSectorImg;
        private RectTransform _missionOrbitHaloRect;
        private Image _missionOrbitHaloImg;
        private Image _commsPulseRingImg;
        private Image _eclipseShadowBadgeImg;
        private readonly List<RectTransform> _transferDots = new List<RectTransform>();
        private readonly List<Image> _transferDotImages = new List<Image>();

        private RectTransform _rocketRootRect;
        private Image _rocketBodyImage;
        private Image _rocketFlameImage;
        private RectTransform _satelliteRootRect;
        private Image _satelliteBodyImage;
        private Image _safeModeShieldImg;
        private Image _deployFlashImg;

        // Camera pan & zoom state
        private Vector2 _currentPanPx = new Vector2(-110f, 0f);
        private Vector2 _targetPanPx = new Vector2(-110f, 0f);
        private float _currentZoom = 0.56f;
        private float _targetZoom = 0.56f;
        private bool _isFollowingSelected;
        private string _selectedBodyId = "earth";
        private string _hoveredBodyId;
        private bool _hasInitialOverviewFit;

        // Launch & orbital flight playback state
        private Coroutine _launchCoroutine;
        private bool _isLaunchPlaying;
        private bool _skipRequested;
        private float _launchSpeedMultiplier = 1.0f;
        private string _targetPlanetBodyId = "earth";
        private float _spacecraftOrbitProgress;
        private float _spacecraftOrbitDeg;
        private bool _spacecraftInEclipse;
        private bool _spacecraftInComm;
        private bool _spacecraftSafeMode;
        private bool _spacecraftCommsOn = true;
        private bool _spacecraftStormActive;

        // Simulation playback time (Pause / Play / 1x / 5x / 10x sync)
        private bool _isSimulationPlaying = false;
        private float _simulationSpeedMultiplier = 1f;
        private float _mapSimTime = 0f;

        // Demo/sandbox state
        private GameObject _sandboxToolbarObj;
        private bool _isDemoAutoOrbiting;
        private float _demoOrbitProgress;

        // Procedural sprites
        private static Sprite _procRingSprite;
        private static Sprite _procSolidGlowSprite;
        private static Sprite _procDotSprite;
        private static Sprite _procReticleSprite;
        private static Sprite _procDiscSprite;
        private static Sprite _smoothDiscSprite;

        public event Action OnLaunchSequenceCompleted;
        public event Action<SolarSystemBodyCatalog.BodyDefinition> OnCelestialBodySelected;
        public event Action<bool> OnOverviewModeChanged;

        private bool _isSystemOverview;

        public bool IsLaunchPlaying => _isLaunchPlaying;
        public bool IsSystemOverview => _isSystemOverview;
        public SolarSystemBodyCatalog.BodyDefinition SelectedBody =>
            _bodyMap.TryGetValue(_selectedBodyId, out CelestialRuntimeNode n) ? n.Spec : null;

        /// <summary>
        /// Synchronises the Space Map visual time with the flight-playback state.
        /// Call this whenever play/pause state or speed multiplier changes in MissionReportUI.
        /// </summary>
        public void SetSimulationPlaybackState(bool isPlaying, float speedMultiplier)
        {
            _isSimulationPlaying = isPlaying;
            _simulationSpeedMultiplier = Mathf.Max(0f, speedMultiplier);
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            EnsureSpaceMapHierarchy();
            if (autoSetupDemoOnStart && _currentOrbit == null)
            {
                SetupDemoPreset(0);
            }
        }

        private void OnDisable()
        {
            if (_launchCoroutine != null)
            {
                StopCoroutine(_launchCoroutine);
                _launchCoroutine = null;
            }

            if (_isLaunchPlaying)
            {
                _isLaunchPlaying = false;
                _skipRequested = false;
                if (_rocketRootRect != null) _rocketRootRect.gameObject.SetActive(false);
                if (_deployFlashImg != null) _deployFlashImg.color = new Color(0.75f, 0.98f, 1f, 0f);
                if (_satelliteRootRect != null) _satelliteRootRect.gameObject.SetActive(true);
            }
        }

        private void Update()
        {
            if (_stageRect == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            float unscaledDt = Time.unscaledDeltaTime;
            float simDt = _isSimulationPlaying ? dt * _simulationSpeedMultiplier : 0f;
            _mapSimTime += simDt;

            FitOverviewOnFirstValidLayout();
            AdvanceCelestialOrbitsAndSprites(simDt, unscaledDt);
            AdvanceAsteroidBelt(simDt, unscaledDt);
            UpdateMissionOverlayVisuals(simDt, unscaledDt);
            UpdateCameraPanAndZoom(unscaledDt);

            if (_starfieldLayer != null)
            {
                _starfieldLayer.Animate(simDt, _mapSimTime, _currentPanPx);
            }

            if (_isDemoAutoOrbiting && !_isLaunchPlaying)
            {
                float prev = _demoOrbitProgress;
                _demoOrbitProgress = (_demoOrbitProgress + dt * 0.12f) % 1.0f;
                var curSnap = new SimulationTickSnapshot
                {
                    OrbitProgress = prev,
                    IsInEclipse = prev >= (1f - (_currentOrbit != null ? _currentOrbit.EclipseFraction : 0.35f)),
                    IsInCommWindow = prev < (_currentOrbit != null ? _currentOrbit.CommWindowFraction : 0.25f)
                };
                var nextSnap = new SimulationTickSnapshot
                {
                    OrbitProgress = _demoOrbitProgress,
                    IsInEclipse = _demoOrbitProgress >= (1f - (_currentOrbit != null ? _currentOrbit.EclipseFraction : 0.35f)),
                    IsInCommWindow = _demoOrbitProgress < (_currentOrbit != null ? _currentOrbit.CommWindowFraction : 0.25f)
                };
                UpdateFlightTick(curSnap, nextSnap, 1.0f);
            }
        }

        public void SetAutoSetupDemoOnStart(bool value)
        {
            autoSetupDemoOnStart = value;
        }

        public void SetSandboxTestBarVisible(bool visible)
        {
            showSandboxTestBar = visible;
            if (_sandboxToolbarObj != null)
            {
                _sandboxToolbarObj.SetActive(visible);
            }
        }

        public void ConfigureSprites(
            Sprite starfield,
            Sprite launchPad,
            Sprite eclipseOverlay,
            Sprite earth,
            Sprite moon,
            Sprite mars,
            Sprite venus,
            Sprite smallRocket,
            Sprite reusableRocket,
            Sprite heavyLiftRocket,
            Sprite rocketFlame,
            Sprite launchSmoke,
            Sprite cubesat,
            Sprite satellite,
            Sprite deepSpaceProbe)
        {
            if (starfield != null) starfieldSprite = starfield;
            if (launchPad != null) launchPadSprite = launchPad;
            if (eclipseOverlay != null) eclipseOverlaySprite = eclipseOverlay;
            if (earth != null) earthSprite = earth;
            if (moon != null) moonSprite = moon;
            if (mars != null) marsSprite = mars;
            if (venus != null) venusSprite = venus;
            if (smallRocket != null) smallRocketSprite = smallRocket;
            if (reusableRocket != null) reusableRocketSprite = reusableRocket;
            if (heavyLiftRocket != null) heavyLiftRocketSprite = heavyLiftRocket;
            if (rocketFlame != null) rocketFlameSprite = rocketFlame;
            if (launchSmoke != null) launchSmokeSprite = launchSmoke;
            if (cubesat != null) cubesatSprite = cubesat;
            if (satellite != null) satelliteSprite = satellite;
            if (deepSpaceProbe != null) deepSpaceProbeSprite = deepSpaceProbe;

            RefreshMissionVehicleSprites();
        }

        public void Initialize(
            MissionGoal goal,
            OrbitDefinition orbit,
            RocketOption rocket,
            List<SatelliteComponent> components)
        {
            if (_launchCoroutine != null)
            {
                StopCoroutine(_launchCoroutine);
                _launchCoroutine = null;
            }

            _isLaunchPlaying = false;
            _skipRequested = false;
            _isDemoAutoOrbiting = false;

            _currentGoal = goal;
            _currentOrbit = orbit;
            _currentRocket = rocket;
            _currentComponents.Clear();
            if (components != null)
            {
                _currentComponents.AddRange(components);
            }

            EnsureSpaceMapHierarchy();

            _targetPlanetBodyId = SolarSystemBodyCatalog.MapOrbitToBodyId(_currentOrbit, _currentGoal);
            RefreshMissionVehicleSprites();

            _hasInitialOverviewFit = false;
            if (!string.IsNullOrEmpty(_targetPlanetBodyId) && _bodyMap.ContainsKey(_targetPlanetBodyId))
            {
                SelectBodyById(_targetPlanetBodyId, focusCamera: true);
                _currentZoom = _targetZoom;
                _currentPanPx = _targetPanPx;
                if (_stageRect != null)
                {
                    _stageRect.localScale = new Vector3(_currentZoom, _currentZoom, 1f);
                    _stageRect.anchoredPosition = _currentPanPx;
                }
                _hasInitialOverviewFit = true;
            }
            else
            {
                FitOverviewOnFirstValidLayout();
                ResetCameraToSystemOverview();
            }
        }

        public void PlayLaunchSequence()
        {
            EnsureSpaceMapHierarchy();
            _isDemoAutoOrbiting = false;

            if (_launchCoroutine != null)
            {
                StopCoroutine(_launchCoroutine);
                _launchCoroutine = null;
            }

            _skipRequested = false;
            _isLaunchPlaying = true;
            _launchSpeedMultiplier = 1.0f;

            if (gameObject.activeInHierarchy)
            {
                _launchCoroutine = StartCoroutine(RunTransferLaunchRoutine());
            }
            else
            {
                CompleteLaunchImmediate();
            }
        }

        public void UpdateFlightTick(
            SimulationTickSnapshot currentTick,
            SimulationTickSnapshot nextTick,
            float lerpT)
        {
            EnsureSpaceMapHierarchy();
            if (currentTick == null) return;

            float clampedT = Mathf.Clamp01(lerpT);
            float prevProg = Mathf.Repeat(currentTick.OrbitProgress, 1.0f);
            float nextProg = nextTick != null ? Mathf.Repeat(nextTick.OrbitProgress, 1.0f) : prevProg;
            if (nextProg < prevProg && prevProg - nextProg > 0.5f)
            {
                nextProg += 1.0f;
            }

            float smoothProg = Mathf.Repeat(Mathf.Lerp(prevProg, nextProg, clampedT), 1.0f);
            _spacecraftOrbitProgress = smoothProg;
            _spacecraftOrbitDeg = 90f - smoothProg * 360f;

            _spacecraftInEclipse = currentTick.IsInEclipse;
            _spacecraftInComm = currentTick.IsInCommWindow;
            _spacecraftSafeMode = currentTick.SafeModeActive;
            _spacecraftCommsOn = currentTick.CommsActive;
            _spacecraftStormActive = !string.IsNullOrEmpty(currentTick.ActiveEventTitle) || currentTick.RadiationLevel >= 50f;
        }

        public void ToggleLaunchSpeed2x()
        {
            _launchSpeedMultiplier = _launchSpeedMultiplier < 1.5f ? 2.0f : 1.0f;
        }

        public void SkipLaunchSequence()
        {
            _skipRequested = true;
            if (_isLaunchPlaying)
            {
                CompleteLaunchImmediate();
            }
        }

        public void SelectNextBody(int step)
        {
            if (_bodies.Count == 0) return;
            int idx = _bodies.FindIndex(b => string.Equals(b.Spec.Id, _selectedBodyId, StringComparison.Ordinal));
            if (idx < 0) idx = 0;
            idx = ((idx + step) % _bodies.Count + _bodies.Count) % _bodies.Count;
            SelectBodyById(_bodies[idx].Spec.Id, focusCamera: true);
        }

        public void SelectBodyById(string bodyId, bool focusCamera)
        {
            if (string.IsNullOrEmpty(bodyId) || !_bodyMap.TryGetValue(bodyId, out CelestialRuntimeNode node))
            {
                return;
            }

            _selectedBodyId = bodyId;
            for (int i = 0; i < _bodies.Count; i++)
            {
                CelestialRuntimeNode b = _bodies[i];
                bool isSel = string.Equals(b.Spec.Id, _selectedBodyId, StringComparison.Ordinal);
                if (b.SelectionReticle != null)
                {
                    b.SelectionReticle.gameObject.SetActive(isSel || string.Equals(b.Spec.Id, _hoveredBodyId, StringComparison.Ordinal));
                    b.SelectionReticle.color = isSel ? b.Spec.AccentColor : new Color(0.85f, 0.95f, 1f, 0.7f);
                }
            }

            if (focusCamera)
            {
                SetOverviewMode(false);
                _isFollowingSelected = !string.Equals(bodyId, "sun", StringComparison.Ordinal) && !node.Spec.IsAsteroidBelt;
                float idealZoom = !string.IsNullOrEmpty(node.Spec.ParentId)
                    ? 7.50f
                    : (node.Spec.OrbitRadiusPx > 600f
                        ? 2.20f
                        : (string.Equals(bodyId, "sun", StringComparison.Ordinal)
                            ? 0.95f
                            : (node.Spec.IsAsteroidBelt ? 0.72f : 5.80f)));
                _targetZoom = Mathf.Clamp(idealZoom, MinZoom, MaxZoom);
                _targetPanPx = -node.CurrentStagePos * _targetZoom - new Vector2(110f, 0f);
            }

            OnCelestialBodySelected?.Invoke(node.Spec);
        }

        public void FocusTargetMission()
        {
            string targetId = !string.IsNullOrEmpty(_targetPlanetBodyId) ? _targetPlanetBodyId : "earth";
            SelectBodyById(targetId, focusCamera: true);
        }

        public void ResetCameraToSystemOverview()
        {
            _isFollowingSelected = false;
            float vw = _mapViewportRect != null && _mapViewportRect.rect.width > 100f ? _mapViewportRect.rect.width : 1920f;
            float vh = _mapViewportRect != null && _mapViewportRect.rect.height > 100f ? _mapViewportRect.rect.height : 1010f;
            float fitDim = Mathf.Min(vw * 0.78f, vh * 0.94f);
            _targetZoom = Mathf.Clamp(fitDim / 1380f, 0.55f, 0.96f);
            _targetPanPx = new Vector2(-40f, 0f);
            SetOverviewMode(true);
        }

        private void SetOverviewMode(bool isOverview)
        {
            if (_isSystemOverview == isOverview) return;
            _isSystemOverview = isOverview;
            OnOverviewModeChanged?.Invoke(_isSystemOverview);
        }

        [ContextMenu("Generate SpaceMap UI In Scene")]
        public void BuildUIInEditor()
        {
            Transform existing = transform.Find("SpaceMapContentRoot");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }
            _stageRect = null;
            EnsureSpaceMapHierarchy();
        }

        [ContextMenu("Auto-Assign Real Pixel Art Sprites")]
        public void AutoAssignRealSpritesInEditor()
        {
            _artBank = SolarSystemArtBank.Load();
            RefreshMissionVehicleSprites();
        }

        public void SetupDemoPreset(int presetIndex)
        {
            EnsureSpaceMapHierarchy();
            _isDemoAutoOrbiting = false;
            if (_launchCoroutine != null)
            {
                StopCoroutine(_launchCoroutine);
                _launchCoroutine = null;
            }

            MissionGoal goal;
            OrbitDefinition orbit;
            RocketOption rocket;

            switch (presetIndex)
            {
                case 1:
                    goal = new MissionGoal { Id = "m2_lunar_mapper", Title = "2. Lunar Radar Mapper", OrbitId = "orbit_lunar" };
                    orbit = new OrbitDefinition { Id = "orbit_lunar", Name = "Lunar Orbit (100 km)", Type = OrbitType.Lunar, OrbitPeriodHours = 2.0f, EclipseFraction = 0.45f, CommWindowFraction = 0.15f, SolarFluxFactor = 1.0f, SignalDelaySeconds = 3.0f, BaseRadiation = 15.0f };
                    rocket = new RocketOption { Id = "rocket_medium", Name = "Soyuz-K Medium", MaxMassLeoKg = 35f, MaxMassLunarKg = 35f, CostThousands = 35f };
                    break;
                case 2:
                    goal = new MissionGoal { Id = "m3_mars_probe", Title = "3. Mars Deep-Space Probe", OrbitId = "orbit_mars" };
                    orbit = new OrbitDefinition { Id = "orbit_mars", Name = "Mars Orbit (350 km)", Type = OrbitType.Mars, OrbitPeriodHours = 2.0f, EclipseFraction = 0.35f, CommWindowFraction = 0.15f, SolarFluxFactor = 0.43f, SignalDelaySeconds = 10.0f, BaseRadiation = 20.0f };
                    rocket = new RocketOption { Id = "rocket_heavy", Name = "Titan Heavy Lift", MaxMassMarsKg = 80f, CostThousands = 85f };
                    break;
                case 3:
                    goal = new MissionGoal { Id = "m4_venus_probe", Title = "4. Venus Atmospheric Probe", OrbitId = "orbit_venus" };
                    orbit = new OrbitDefinition { Id = "orbit_venus", Name = "Venus Orbit (300 km)", Type = OrbitType.DeepSpace, OrbitPeriodHours = 1.6f, EclipseFraction = 0.40f, CommWindowFraction = 0.15f, SolarFluxFactor = 1.90f, SignalDelaySeconds = 12.0f, BaseRadiation = 35.0f };
                    rocket = new RocketOption { Id = "rocket_heavy", Name = "Titan Heavy Lift", MaxMassDeepSpaceKg = 80f, CostThousands = 85f };
                    break;
                default:
                    goal = new MissionGoal { Id = "m1_leo_weather", Title = "1. LEO Weather CubeSat", OrbitId = "orbit_leo" };
                    orbit = new OrbitDefinition { Id = "orbit_leo", Name = "LEO 400 km", Type = OrbitType.LowEarth, OrbitPeriodHours = 1.5f, EclipseFraction = 0.35f, CommWindowFraction = 0.25f, SolarFluxFactor = 1.0f, SignalDelaySeconds = 1.0f, BaseRadiation = 5.0f };
                    rocket = new RocketOption { Id = "rocket_light", Name = "Vega-Light", MaxMassLeoKg = 14f, CostThousands = 15f };
                    break;
            }

            Initialize(goal, orbit, rocket, null);
        }

        public void StartDemoLaunchAndOrbit()
        {
            if (_currentOrbit == null) SetupDemoPreset(0);
            _isDemoAutoOrbiting = false;
            OnLaunchSequenceCompleted -= OnDemoCompleted;
            OnLaunchSequenceCompleted += OnDemoCompleted;
            PlayLaunchSequence();
        }

        private void OnDemoCompleted()
        {
            OnLaunchSequenceCompleted -= OnDemoCompleted;
            _isDemoAutoOrbiting = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isFollowingSelected = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            _isFollowingSelected = false;
            float scale = GetCanvasScaleFactor();
            _targetPanPx += eventData.delta / scale;
            ClampTargetPan();
        }

        private float GetCanvasScaleFactor()
        {
            Canvas c = GetComponentInParent<Canvas>();
            return (c != null && c.scaleFactor > 0.01f) ? c.scaleFactor : 1f;
        }

        public void OnScroll(PointerEventData eventData)
        {
            float factor = eventData.scrollDelta.y > 0f ? 1.14f : 0.875f;
            float newZoom = Mathf.Clamp(_targetZoom * factor, MinZoom, MaxZoom);
            if (_mapViewportRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_mapViewportRect, eventData.position, eventData.pressEventCamera, out Vector2 localCursor))
            {
                float ratio = newZoom / Mathf.Max(0.05f, _targetZoom);
                _targetPanPx = localCursor + (_targetPanPx - localCursor) * ratio;
            }
            _targetZoom = newZoom;
            ClampTargetPan();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging)
            {
                return;
            }

            if (eventData.clickCount >= 2)
            {
                ResetCameraToSystemOverview();
                return;
            }

            if (_stageRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_stageRect, eventData.position, eventData.pressEventCamera, out Vector2 stagePt))
            {
                float dist = stagePt.magnitude;
                if (dist >= 490f && dist <= 630f)
                {
                    SelectBodyById("belt", focusCamera: true);
                }
            }
        }

        public void EnsureSpaceMapHierarchy()
        {
            if (_stageRect != null && _bodies.Count > 0)
            {
                return;
            }

            EnsureProceduralSprites();
            _artBank = SolarSystemArtBank.Load();

            RectTransform hostRt = GetComponent<RectTransform>();
            if (hostRt == null) hostRt = gameObject.AddComponent<RectTransform>();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            // Root viewport with mask
            GameObject rootObj = CreateUIObject("SpaceMapContentRoot", transform);
            _mapViewportRect = rootObj.GetComponent<RectTransform>();
            StretchFull(_mapViewportRect);
            rootObj.AddComponent<RectMask2D>();
            Image catchBg = rootObj.AddComponent<Image>();
            catchBg.color = new Color(0.01f, 0.02f, 0.05f, 1f);
            catchBg.raycastTarget = true;

            // Layer 1: Pixel twinkling starfield
            GameObject starObj = CreateUIObject("PixelStarfieldBackdrop", rootObj.transform);
            _starfieldLayer = starObj.AddComponent<PixelStarfieldLayer>();
            _starfieldLayer.EnsureBuilt();

            // Layer 2: Interactive Solar System zoom/pan world stage
            GameObject stageObj = CreateUIObject("SolarSystemStage", rootObj.transform);
            _stageRect = stageObj.GetComponent<RectTransform>();
            _stageRect.anchorMin = new Vector2(0.5f, 0.5f);
            _stageRect.anchorMax = new Vector2(0.5f, 0.5f);
            _stageRect.pivot = new Vector2(0.5f, 0.5f);
            _stageRect.anchoredPosition = _currentPanPx;
            _stageRect.sizeDelta = new Vector2(2600f, 2600f);

            _orbitsLayerRect = CreateUIObject("OrbitsLayer", _stageRect).GetComponent<RectTransform>();
            _asteroidsLayerRect = CreateUIObject("AsteroidBeltLayer", _stageRect).GetComponent<RectTransform>();
            _missionLayerRect = CreateUIObject("MissionTrajectoryLayer", _stageRect).GetComponent<RectTransform>();
            _bodiesLayerRect = CreateUIObject("CelestialBodiesLayer", _stageRect).GetComponent<RectTransform>();

            BuildAstronomicalSolarSystem();
            BuildRandomAsteroidBelt();
            BuildMissionOverlayLayer();

            RefreshMissionVehicleSprites();
            AdvanceCelestialOrbitsAndSprites(0f, 0f);
            AdvanceAsteroidBelt(0f, 0f);
        }

        private void BuildAstronomicalSolarSystem()
        {
            _bodies.Clear();
            _bodyMap.Clear();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            List<SolarSystemBodyCatalog.BodyDefinition> catalog = SolarSystemBodyCatalog.BuildStandardSolarSystem();

            // Sun warm pulsating pixel glow underneath everything
            GameObject glowObj = CreateUIObject("SunCoronaGlow", _bodiesLayerRect);
            RectTransform glowRt = glowObj.GetComponent<RectTransform>();
            glowRt.sizeDelta = new Vector2(440f, 440f);
            _sunCoronaGlow = glowObj.AddComponent<Image>();
            _sunCoronaGlow.sprite = _procSolidGlowSprite;
            _sunCoronaGlow.color = new Color(1.0f, 0.65f, 0.18f, 0.24f);
            _sunCoronaGlow.raycastTarget = false;

            for (int i = 0; i < catalog.Count; i++)
            {
                SolarSystemBodyCatalog.BodyDefinition spec = catalog[i];

                // Orbit ring (smooth anti-aliased vector ellipse)
                RectTransform ringRt = null;
                Graphic ringGraphic = null;
                if (spec.OrbitRadiusPx > 1f || spec.SatelliteOrbitRadiusPx > 1f)
                {
                    float a = spec.OrbitRadiusPx > 1f ? spec.OrbitRadiusPx : spec.SatelliteOrbitRadiusPx;
                    float b = a * Mathf.Sqrt(Mathf.Max(0.25f, 1f - spec.Eccentricity * spec.Eccentricity));

                    GameObject ringObj = CreateUIObject("Orbit_" + spec.Id, _orbitsLayerRect);
                    ringRt = ringObj.GetComponent<RectTransform>();
                    ringRt.sizeDelta = new Vector2(a * 2f, b * 2f);
                    ringRt.localEulerAngles = new Vector3(0f, 0f, spec.PerihelionAngleDeg);

                    float focusShift = -a * spec.Eccentricity;
                    float tiltRad = spec.PerihelionAngleDeg * Mathf.Deg2Rad;
                    ringRt.anchoredPosition = new Vector2(focusShift * Mathf.Cos(tiltRad), focusShift * Mathf.Sin(tiltRad));

                    ringObj.AddComponent<CanvasRenderer>();
                    OrbitEllipseGraphic ellipse = ringObj.AddComponent<OrbitEllipseGraphic>();
                    ellipse.color = spec.OrbitColor;
                    ellipse.raycastTarget = false;
                    ellipse.Thickness = string.IsNullOrEmpty(spec.ParentId) ? 2.6f : 1.6f;
                    ellipse.Segments = 240;
                    ringGraphic = ellipse;
                }

                // Planet / Sun / Belt marker node
                GameObject nodeObj = CreateUIObject("Body_" + spec.Id, _bodiesLayerRect);
                RectTransform nodeRt = nodeObj.GetComponent<RectTransform>();
                nodeRt.sizeDelta = new Vector2(spec.VisualWidthPx, spec.VisualHeightPx);

                Image planetImg = nodeObj.AddComponent<Image>();
                planetImg.preserveAspect = true;
                planetImg.raycastTarget = true;

                Sprite[] frames = SolarSystemArtBank.GetAnimatedFrames(spec.SheetKey, out float fps);
                if (frames != null && frames.Length > 0)
                {
                    planetImg.sprite = frames[0];
                }
                else
                {
                    planetImg.sprite = _procDotSprite;
                    planetImg.color = spec.AccentColor;
                }

                // Selection & hover pixel reticle
                GameObject retObj = CreateUIObject("Reticle", nodeObj.transform);
                RectTransform retRt = retObj.GetComponent<RectTransform>();
                retRt.anchorMin = new Vector2(0.5f, 0.5f);
                retRt.anchorMax = new Vector2(0.5f, 0.5f);
                retRt.sizeDelta = new Vector2(Mathf.Max(spec.VisualWidthPx, spec.VisualHeightPx) + 16f, Mathf.Max(spec.VisualWidthPx, spec.VisualHeightPx) + 16f);
                Image retImg = retObj.AddComponent<Image>();
                retImg.sprite = _procReticleSprite;
                retImg.color = spec.AccentColor;
                retImg.raycastTarget = false;
                retObj.SetActive(false);

                // Compact label underneath body
                GameObject lblObj = CreateUIObject("Label", nodeObj.transform);
                RectTransform lblRt = lblObj.GetComponent<RectTransform>();
                lblRt.anchorMin = new Vector2(0.5f, 0f);
                lblRt.anchorMax = new Vector2(0.5f, 0f);
                lblRt.pivot = new Vector2(0.5f, 1f);
                lblRt.anchoredPosition = new Vector2(0f, -6f);
                lblRt.sizeDelta = new Vector2(180f, 26f);

                Text lblText = lblObj.AddComponent<Text>();
                lblText.font = font;
                lblText.text = spec.DisplayName.ToUpperInvariant();
                lblText.fontSize = string.IsNullOrEmpty(spec.ParentId) ? 18 : 13;
                lblText.fontStyle = FontStyle.Bold;
                lblText.color = new Color(0.95f, 0.98f, 1f, 0.96f);
                lblText.alignment = TextAnchor.UpperCenter;
                lblText.horizontalOverflow = HorizontalWrapMode.Overflow;
                lblText.verticalOverflow = VerticalWrapMode.Overflow;
                lblText.raycastTarget = false;
                Outline lblOutline = lblObj.AddComponent<Outline>();
                lblOutline.effectColor = new Color(0.02f, 0.04f, 0.09f, 0.98f);
                lblOutline.effectDistance = new Vector2(1.5f, -1.5f);

                var proxy = nodeObj.AddComponent<SolarBodyHitProxy>();
                proxy.Owner = this;
                proxy.BodyId = spec.Id;

                var runtimeNode = new CelestialRuntimeNode
                {
                    Spec = spec,
                    Rect = nodeRt,
                    PlanetImage = planetImg,
                    OrbitRingRect = ringRt,
                    OrbitRingGraphic = ringGraphic,
                    SelectionReticle = retImg,
                    LabelText = lblText,
                    Frames = frames,
                    FrameRate = fps,
                    AnimTime = UnityEngine.Random.Range(0f, 8f),
                    CurrentOrbitAngleDeg = spec.InitialPhaseDeg
                };

                _bodies.Add(runtimeNode);
                _bodyMap[spec.Id] = runtimeNode;
            }
        }

        private void BuildRandomAsteroidBelt()
        {
            _asteroids.Clear();

            Sprite[][] asteroidVariants = new Sprite[6][];
            float[] asteroidFps = new float[6];
            for (int v = 0; v < 6; v++)
            {
                asteroidVariants[v] = SolarSystemArtBank.GetAnimatedFrames("asteroid_" + v + "_sheet", out asteroidFps[v]);
            }

            // Reproducible spread yet randomized asteroid variant assignment across the 6 GIFs
            for (int i = 0; i < AsteroidCount; i++)
            {
                int variantIdx = UnityEngine.Random.Range(0, 6);
                Sprite[] frames = asteroidVariants[variantIdx];

                GameObject astObj = CreateUIObject("BeltAsteroid_" + i, _asteroidsLayerRect);
                RectTransform rt = astObj.GetComponent<RectTransform>();
                float size = UnityEngine.Random.Range(5.5f, 15.5f);
                rt.sizeDelta = new Vector2(size, size);

                Image img = astObj.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                float shade = UnityEngine.Random.Range(0.78f, 1.0f);
                img.color = new Color(shade, shade * 0.97f, shade, UnityEngine.Random.Range(0.85f, 1.0f));
                if (frames != null && frames.Length > 0)
                {
                    img.sprite = frames[0];
                }

                float radius = UnityEngine.Random.Range(485f, 635f);
                float keplerSpeed = 2.4f * Mathf.Pow(560f / radius, 1.5f);

                _asteroids.Add(new AsteroidRuntimeNode
                {
                    Rect = rt,
                    Img = img,
                    Frames = frames,
                    FrameRate = UnityEngine.Random.Range(4.5f, 8.5f),
                    AnimTime = UnityEngine.Random.Range(0f, 10f),
                    OrbitRadiusPx = radius,
                    OrbitAngleDeg = UnityEngine.Random.Range(0f, 360f),
                    OrbitSpeedDegPerSec = keplerSpeed,
                    SpinSpeedDegPerSec = UnityEngine.Random.Range(-24f, 24f),
                    SpinAngleDeg = UnityEngine.Random.Range(0f, 360f),
                    RadialWobbleAmp = UnityEngine.Random.Range(1.5f, 4.5f),
                    WobblePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
                });
            }
        }

        private void BuildMissionOverlayLayer()
        {
            _transferDots.Clear();
            _transferDotImages.Clear();

            for (int i = 0; i < TransferDotCount; i++)
            {
                GameObject dotObj = CreateUIObject("TransferDot_" + i, _missionLayerRect);
                RectTransform drt = dotObj.GetComponent<RectTransform>();
                drt.sizeDelta = new Vector2(5f, 5f);
                Image di = dotObj.AddComponent<Image>();
                di.sprite = _procDotSprite;
                di.color = new Color(0.40f, 0.90f, 1f, 0.72f);
                di.raycastTarget = false;
                dotObj.SetActive(false);
                _transferDots.Add(drt);
                _transferDotImages.Add(di);
            }

            // Orbital Sunlight, Shadow (Eclipse), and Comms Link Visual Sectors around Target Planet
            _sunlightSectorImg = CreateRadialSector("SunlightSector", _missionLayerRect, new Color(1.0f, 0.86f, 0.32f, 0.14f), 0.50f, out _sunlightSectorRect);
            _eclipseSectorImg = CreateRadialSector("EclipseShadowSector", _missionLayerRect, new Color(0.05f, 0.08f, 0.24f, 0.48f), 0.35f, out _eclipseSectorRect);
            _commsSectorImg = CreateRadialSector("CommsLinkSector", _missionLayerRect, new Color(0.22f, 0.96f, 0.72f, 0.22f), 0.25f, out _commsSectorRect);

            // Target planet mission ring
            GameObject haloObj = CreateUIObject("MissionOrbitHalo", _missionLayerRect);
            _missionOrbitHaloRect = haloObj.GetComponent<RectTransform>();
            _missionOrbitHaloRect.sizeDelta = new Vector2(84f, 84f);
            _missionOrbitHaloImg = haloObj.AddComponent<Image>();
            _missionOrbitHaloImg.sprite = _procRingSprite;
            _missionOrbitHaloImg.color = new Color(0.35f, 0.95f, 0.75f, 0.72f);
            _missionOrbitHaloImg.raycastTarget = false;

            // Player Spacecraft (smaller than Moon 10x10 and orbiting inside Moon's orbit)
            GameObject satObj = CreateUIObject("PlayerSpacecraft", _missionLayerRect);
            _satelliteRootRect = satObj.GetComponent<RectTransform>();
            _satelliteRootRect.sizeDelta = new Vector2(7f, 7f);

            GameObject shieldObj = CreateUIObject("SafeModeShield", satObj.transform);
            RectTransform shRt = shieldObj.GetComponent<RectTransform>();
            shRt.sizeDelta = new Vector2(11f, 11f);
            _safeModeShieldImg = shieldObj.AddComponent<Image>();
            _safeModeShieldImg.sprite = _procRingSprite;
            _safeModeShieldImg.color = new Color(1f, 0.85f, 0.28f, 0f);
            _safeModeShieldImg.raycastTarget = false;

            _satelliteBodyImage = satObj.AddComponent<Image>();
            _satelliteBodyImage.preserveAspect = true;
            _satelliteBodyImage.raycastTarget = false;

            GameObject pulseObj = CreateUIObject("CommsLinkPulse", satObj.transform);
            RectTransform pRt = pulseObj.GetComponent<RectTransform>();
            pRt.sizeDelta = new Vector2(10f, 10f);
            _commsPulseRingImg = pulseObj.AddComponent<Image>();
            _commsPulseRingImg.sprite = _procRingSprite;
            _commsPulseRingImg.color = new Color(0.35f, 0.96f, 1f, 0f);
            _commsPulseRingImg.raycastTarget = false;

            GameObject eclipseObj = CreateUIObject("EclipseShadowBadge", satObj.transform);
            RectTransform ecRt = eclipseObj.GetComponent<RectTransform>();
            ecRt.sizeDelta = new Vector2(8.5f, 8.5f);
            _eclipseShadowBadgeImg = eclipseObj.AddComponent<Image>();
            _eclipseShadowBadgeImg.sprite = _procSolidGlowSprite;
            _eclipseShadowBadgeImg.color = new Color(0.15f, 0.25f, 0.55f, 0f);
            _eclipseShadowBadgeImg.raycastTarget = false;

            // Rocket during launch sequence
            GameObject rocketObj = CreateUIObject("LaunchRocket", _missionLayerRect);
            _rocketRootRect = rocketObj.GetComponent<RectTransform>();
            _rocketRootRect.sizeDelta = new Vector2(8f, 11.5f);
            _rocketBodyImage = rocketObj.AddComponent<Image>();
            _rocketBodyImage.preserveAspect = true;
            _rocketBodyImage.raycastTarget = false;

            GameObject flameObj = CreateUIObject("Flame", rocketObj.transform);
            RectTransform fRt = flameObj.GetComponent<RectTransform>();
            fRt.anchorMin = new Vector2(0.5f, 0f);
            fRt.anchorMax = new Vector2(0.5f, 0f);
            fRt.pivot = new Vector2(0.5f, 1f);
            fRt.sizeDelta = new Vector2(4.5f, 6f);
            _rocketFlameImage = flameObj.AddComponent<Image>();
            _rocketFlameImage.preserveAspect = true;
            _rocketFlameImage.raycastTarget = false;
            rocketObj.SetActive(false);

            GameObject flashObj = CreateUIObject("SeparationFlash", _missionLayerRect);
            RectTransform flRt = flashObj.GetComponent<RectTransform>();
            flRt.sizeDelta = new Vector2(24f, 24f);
            _deployFlashImg = flashObj.AddComponent<Image>();
            _deployFlashImg.sprite = _procSolidGlowSprite;
            _deployFlashImg.color = new Color(0.75f, 0.98f, 1f, 0f);
            _deployFlashImg.raycastTarget = false;
        }

        private void AdvanceCelestialOrbitsAndSprites(float simDt, float unscaledDt)
        {
            // Advance Sun & planets in heliocentric order first (parents appear before moons in catalog)
            for (int i = 0; i < _bodies.Count; i++)
            {
                CelestialRuntimeNode node = _bodies[i];
                SolarSystemBodyCatalog.BodyDefinition spec = node.Spec;

                node.CurrentOrbitAngleDeg = (node.CurrentOrbitAngleDeg + spec.OrbitalSpeedDegPerSec * simDt) % 360f;

                Vector2 pos = Vector2.zero;
                if (string.IsNullOrEmpty(spec.ParentId))
                {
                    if (spec.OrbitRadiusPx > 0.5f)
                    {
                        pos = SolarSystemBodyCatalog.EvaluateEllipsePosition(
                            spec.OrbitRadiusPx, spec.Eccentricity, spec.PerihelionAngleDeg, node.CurrentOrbitAngleDeg);
                    }
                }
                else if (_bodyMap.TryGetValue(spec.ParentId, out CelestialRuntimeNode parent))
                {
                    Vector2 localMoonPos = SolarSystemBodyCatalog.EvaluateEllipsePosition(
                        spec.SatelliteOrbitRadiusPx, spec.Eccentricity, spec.PerihelionAngleDeg, node.CurrentOrbitAngleDeg);
                    pos = parent.CurrentStagePos + localMoonPos;

                    if (node.OrbitRingRect != null)
                    {
                        node.OrbitRingRect.anchoredPosition = parent.CurrentStagePos;
                    }
                }

                node.CurrentStagePos = pos;
                if (node.Rect != null)
                {
                    node.Rect.anchoredPosition = pos;
                    bool hovered = string.Equals(spec.Id, _hoveredBodyId, StringComparison.Ordinal);
                    node.HoverBlend = Mathf.MoveTowards(node.HoverBlend, hovered ? 1f : 0f, unscaledDt * 9f);
                    float scale = 1f + node.HoverBlend * 0.18f;
                    node.Rect.localScale = new Vector3(scale, scale, 1f);
                }

                if (node.Frames != null && node.Frames.Length > 0 && node.PlanetImage != null)
                {
                    node.AnimTime += simDt * node.FrameRate;
                    int f = Mathf.FloorToInt(node.AnimTime) % node.Frames.Length;
                    if (f < 0) f += node.Frames.Length;
                    Sprite sp = node.Frames[f];
                    if (sp != null && node.PlanetImage.sprite != sp)
                    {
                        node.PlanetImage.sprite = sp;
                    }
                }
            }

            if (_sunCoronaGlow != null)
            {
                float pulse = 0.20f + 0.08f * Mathf.Sin(_mapSimTime * 2.2f);
                _sunCoronaGlow.color = new Color(1.0f, 0.66f, 0.20f, pulse);
                float coronaScale = 1.0f + 0.06f * Mathf.Sin(_mapSimTime * 1.7f);
                _sunCoronaGlow.rectTransform.localScale = new Vector3(coronaScale, coronaScale, 1f);
            }
        }

        private void AdvanceAsteroidBelt(float simDt, float unscaledDt)
        {
            float now = _mapSimTime;
            for (int i = 0; i < _asteroids.Count; i++)
            {
                AsteroidRuntimeNode ast = _asteroids[i];
                ast.OrbitAngleDeg = (ast.OrbitAngleDeg + ast.OrbitSpeedDegPerSec * simDt) % 360f;
                ast.SpinAngleDeg = (ast.SpinAngleDeg + ast.SpinSpeedDegPerSec * simDt) % 360f;

                float r = ast.OrbitRadiusPx + Mathf.Sin(now * 0.7f + ast.WobblePhase) * ast.RadialWobbleAmp;
                float rad = ast.OrbitAngleDeg * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r);

                if (ast.Rect != null)
                {
                    ast.Rect.anchoredPosition = pos;
                    ast.Rect.localEulerAngles = new Vector3(0f, 0f, ast.SpinAngleDeg);
                }

                if (ast.Frames != null && ast.Frames.Length > 0 && ast.Img != null)
                {
                    ast.AnimTime += simDt * ast.FrameRate;
                    int f = Mathf.FloorToInt(ast.AnimTime) % ast.Frames.Length;
                    if (f < 0) f += ast.Frames.Length;
                    Sprite sp = ast.Frames[f];
                    if (sp != null && ast.Img.sprite != sp)
                    {
                        ast.Img.sprite = sp;
                    }
                }
            }
        }

        private void UpdateMissionOverlayVisuals(float simDt, float unscaledDt)
        {
            if (!_bodyMap.TryGetValue(_targetPlanetBodyId, out CelestialRuntimeNode targetNode))
            {
                return;
            }

            Vector2 targetPos = targetNode.CurrentStagePos;
            float bodyRadius = Mathf.Max(targetNode.Spec.VisualWidthPx, targetNode.Spec.VisualHeightPx) * 0.5f;
            float orbitRadius = bodyRadius + Mathf.Clamp(bodyRadius * 0.42f, 5f, 11f);

            // Update Sunlight, Shadow (Eclipse), and Comms visual sectors around target planet
            Vector2 sunPos = _bodyMap.TryGetValue("sun", out CelestialRuntimeNode sunNode) ? sunNode.CurrentStagePos : Vector2.zero;
            Vector2 toSun = sunPos - targetPos;
            float sunAngleDeg = toSun.sqrMagnitude > 0.01f ? Mathf.Atan2(toSun.y, toSun.x) * Mathf.Rad2Deg : 180f;
            float sectorDiam = orbitRadius * 2.55f;

            float eclipseFrac = Mathf.Clamp(_currentOrbit != null ? _currentOrbit.EclipseFraction : 0.35f, 0.20f, 0.48f);
            float sunlightFrac = 1.0f - eclipseFrac; // Seamless 360-degree coverage with zero gap

            // Align spacecraft orbital angle with the anti-solar Eclipse sector [1 - eclipseFrac, 1.0)
            // and immediate post-eclipse Comms sector [0, commFrac)
            _spacecraftOrbitDeg = (sunAngleDeg + 180f) + (1f - eclipseFrac * 0.5f - _spacecraftOrbitProgress) * 360f;

            if (_sunlightSectorRect != null && _sunlightSectorImg != null)
            {
                _sunlightSectorRect.anchoredPosition = targetPos;
                _sunlightSectorRect.sizeDelta = new Vector2(sectorDiam, sectorDiam);
                _sunlightSectorImg.fillAmount = sunlightFrac;
                // Origin360.Top midpoint in local space is (90 - fillAmount * 180)
                _sunlightSectorRect.localEulerAngles = new Vector3(0f, 0f, sunAngleDeg - 90f + sunlightFrac * 180f);
            }

            if (_eclipseSectorRect != null && _eclipseSectorImg != null)
            {
                _eclipseSectorRect.anchoredPosition = targetPos;
                _eclipseSectorRect.sizeDelta = new Vector2(sectorDiam, sectorDiam);
                _eclipseSectorImg.fillAmount = eclipseFrac;
                _eclipseSectorRect.localEulerAngles = new Vector3(0f, 0f, sunAngleDeg + 90f + eclipseFrac * 180f);
            }

            if (_commsSectorRect != null && _commsSectorImg != null)
            {
                float commFrac = Mathf.Clamp(_currentOrbit != null ? _currentOrbit.CommWindowFraction : 0.25f, 0.16f, 0.40f);
                float commDiam = orbitRadius * 2.22f;
                float commCenterDeg = sunAngleDeg + 90f - eclipseFrac * 180f;
                if (!string.Equals(_targetPlanetBodyId, "earth", StringComparison.Ordinal)
                    && _bodyMap.TryGetValue("earth", out CelestialRuntimeNode earthCommNode)
                    && earthCommNode != null)
                {
                    Vector2 toEarth = earthCommNode.CurrentStagePos - targetPos;
                    if (toEarth.sqrMagnitude > 0.01f)
                    {
                        commCenterDeg = Mathf.Atan2(toEarth.y, toEarth.x) * Mathf.Rad2Deg;
                    }
                }

                _commsSectorRect.anchoredPosition = targetPos;
                _commsSectorRect.sizeDelta = new Vector2(commDiam, commDiam);
                _commsSectorImg.fillAmount = commFrac;
                _commsSectorImg.color = _spacecraftCommsOn
                    ? new Color(0.22f, 0.96f, 0.72f, _spacecraftInComm ? 0.34f : 0.20f)
                    : new Color(0.45f, 0.55f, 0.65f, 0.10f);
                _commsSectorRect.localEulerAngles = new Vector3(0f, 0f, commCenterDeg - 90f + commFrac * 180f);
            }

            if (_missionOrbitHaloRect != null)
            {
                _missionOrbitHaloRect.anchoredPosition = targetPos;
                _missionOrbitHaloRect.sizeDelta = new Vector2(orbitRadius * 2f, orbitRadius * 2f);
                if (_missionOrbitHaloImg != null)
                {
                    _missionOrbitHaloImg.color = _spacecraftStormActive
                        ? new Color(1.0f, 0.36f, 0.32f, 0.80f)
                        : (_spacecraftInComm && _spacecraftCommsOn
                            ? new Color(0.35f, 0.96f, 0.72f, 0.76f)
                            : new Color(0.40f, 0.78f, 1.0f, 0.56f));
                }
            }

            // Transfer arc from Earth -> Target when interplanetary / lunar
            CelestialRuntimeNode earthNode = null;
            bool showTransfer = !string.Equals(_targetPlanetBodyId, "earth", StringComparison.Ordinal)
                                && _bodyMap.TryGetValue("earth", out earthNode)
                                && earthNode != null;
            if (showTransfer)
            {
                Vector2 p0 = earthNode.CurrentStagePos;
                Vector2 p2 = targetPos;
                Vector2 mid = (p0 + p2) * 0.5f;
                Vector2 perp = Vector2.Perpendicular((p2 - p0).normalized);
                Vector2 p1 = mid + perp * (Vector2.Distance(p0, p2) * 0.24f);

                float phase = _mapSimTime * 2.2f;
                for (int i = 0; i < _transferDots.Count; i++)
                {
                    float t = (i + 1) / (float)(TransferDotCount + 1);
                    Vector2 bp = (1f - t) * (1f - t) * p0 + 2f * (1f - t) * t * p1 + t * t * p2;
                    _transferDots[i].anchoredPosition = bp;
                    _transferDots[i].gameObject.SetActive(true);
                    float pulse = 0.25f + 0.65f * Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(phase - i * 0.45f));
                    _transferDotImages[i].color = new Color(0.45f, 0.92f, 1.0f, pulse);
                }
            }
            else
            {
                for (int i = 0; i < _transferDots.Count; i++)
                {
                    if (_transferDots[i] != null && _transferDots[i].gameObject.activeSelf)
                    {
                        _transferDots[i].gameObject.SetActive(false);
                    }
                }
            }

            // Spacecraft orbiting target body (visible when not waiting for initial ascent)
            if (_satelliteRootRect != null)
            {
                _satelliteRootRect.gameObject.SetActive(!_isLaunchPlaying);
                float rad = _spacecraftOrbitDeg * Mathf.Deg2Rad;
                _satelliteRootRect.anchoredPosition = targetPos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
                _satelliteRootRect.localEulerAngles = new Vector3(0f, 0f, _spacecraftOrbitDeg + 90f);

                if (_safeModeShieldImg != null)
                {
                    _safeModeShieldImg.color = _spacecraftSafeMode
                        ? new Color(1f, 0.86f, 0.28f, 0.78f + 0.2f * Mathf.Sin(_mapSimTime * 6f))
                        : new Color(1f, 0.86f, 0.28f, 0f);
                }

                if (_commsPulseRingImg != null)
                {
                    float commAlpha = (_spacecraftInComm && _spacecraftCommsOn)
                        ? 0.45f + 0.40f * Mathf.Sin(_mapSimTime * 5f)
                        : 0f;
                    _commsPulseRingImg.color = new Color(0.32f, 0.96f, 1f, commAlpha);
                }

                if (_eclipseShadowBadgeImg != null)
                {
                    _eclipseShadowBadgeImg.color = _spacecraftInEclipse
                        ? new Color(0.14f, 0.26f, 0.60f, 0.45f)
                        : new Color(0.14f, 0.26f, 0.60f, 0f);
                }

                if (_satelliteBodyImage != null)
                {
                    _satelliteBodyImage.color = _spacecraftInEclipse
                        ? new Color(0.64f, 0.72f, 0.90f, 1f)
                        : Color.white;
                }
            }
        }

        private Vector2 GetTargetSatelliteOrbitPos(CelestialRuntimeNode targetNode)
        {
            if (targetNode == null) return Vector2.zero;
            float bodyRadius = Mathf.Max(targetNode.Spec.VisualWidthPx, targetNode.Spec.VisualHeightPx) * 0.5f;
            float orbitRadius = bodyRadius + Mathf.Clamp(bodyRadius * 0.42f, 5f, 11f);
            float rad = _spacecraftOrbitDeg * Mathf.Deg2Rad;
            return targetNode.CurrentStagePos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
        }

        private IEnumerator RunTransferLaunchRoutine()
        {
            Vector2 startPos = _bodyMap.TryGetValue("earth", out CelestialRuntimeNode earth) ? earth.CurrentStagePos : Vector2.zero;
            Vector2 endPos = _bodyMap.TryGetValue(_targetPlanetBodyId, out CelestialRuntimeNode tgt) ? GetTargetSatelliteOrbitPos(tgt) : startPos;

            if (_rocketRootRect != null) _rocketRootRect.gameObject.SetActive(true);
            if (_satelliteRootRect != null) _satelliteRootRect.gameObject.SetActive(false);

            float ascentElapsed = 0f;
            float ascentDur = Mathf.Max(0.2f, phase1AscentDurationSeconds);

            while (ascentElapsed < ascentDur && !_skipRequested)
            {
                ascentElapsed += Time.unscaledDeltaTime * _launchSpeedMultiplier;
                float u = Mathf.Clamp01(ascentElapsed / ascentDur);
                float eased = 1f - Mathf.Pow(1f - u, 2.2f);

                if (_bodyMap.TryGetValue("earth", out CelestialRuntimeNode eNow)) startPos = eNow.CurrentStagePos;
                if (_bodyMap.TryGetValue(_targetPlanetBodyId, out CelestialRuntimeNode tNow)) endPos = GetTargetSatelliteOrbitPos(tNow);

                Vector2 pos;
                float tangentDeg;
                if (string.Equals(_targetPlanetBodyId, "earth", StringComparison.Ordinal))
                {
                    pos = Vector2.Lerp(startPos, endPos, eased);
                    Vector2 dir = endPos - startPos;
                    tangentDeg = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f : 0f;
                }
                else
                {
                    Vector2 mid = (startPos + endPos) * 0.5f + Vector2.Perpendicular((endPos - startPos).normalized) * (Vector2.Distance(startPos, endPos) * 0.24f);
                    pos = (1f - eased) * (1f - eased) * startPos + 2f * (1f - eased) * eased * mid + eased * eased * endPos;
                    Vector2 deriv = 2f * (1f - eased) * (mid - startPos) + 2f * eased * (endPos - mid);
                    tangentDeg = deriv.sqrMagnitude > 0.001f ? Mathf.Atan2(deriv.y, deriv.x) * Mathf.Rad2Deg - 90f : 0f;
                }

                if (_rocketRootRect != null)
                {
                    _rocketRootRect.anchoredPosition = pos;
                    _rocketRootRect.localEulerAngles = new Vector3(0f, 0f, tangentDeg);
                }

                if (_rocketFlameImage != null)
                {
                    float flicker = 0.85f + 0.25f * Mathf.Sin(Time.unscaledTime * 35f);
                    _rocketFlameImage.rectTransform.localScale = new Vector3(flicker, flicker, 1f);
                }

                yield return null;
            }

            if (_rocketRootRect != null) _rocketRootRect.gameObject.SetActive(false);

            // Brief stage separation flash at the exact satellite deployment point
            float flashDur = Mathf.Max(0.15f, phase2SeparationDurationSeconds);
            float flashElapsed = 0f;
            while (flashElapsed < flashDur && !_skipRequested)
            {
                flashElapsed += Time.unscaledDeltaTime * _launchSpeedMultiplier;
                float p = Mathf.Clamp01(flashElapsed / flashDur);
                if (_deployFlashImg != null && _bodyMap.TryGetValue(_targetPlanetBodyId, out CelestialRuntimeNode tgtNode))
                {
                    _deployFlashImg.rectTransform.anchoredPosition = GetTargetSatelliteOrbitPos(tgtNode);
                    _deployFlashImg.color = new Color(0.75f, 0.98f, 1f, Mathf.Sin(p * Mathf.PI) * 0.85f);
                }
                yield return null;
            }

            CompleteLaunchImmediate();
        }

        private void CompleteLaunchImmediate()
        {
            if (_launchCoroutine != null)
            {
                StopCoroutine(_launchCoroutine);
                _launchCoroutine = null;
            }
            _isLaunchPlaying = false;
            _skipRequested = false;

            if (_rocketRootRect != null) _rocketRootRect.gameObject.SetActive(false);
            if (_deployFlashImg != null) _deployFlashImg.color = new Color(0.75f, 0.98f, 1f, 0f);
            if (_satelliteRootRect != null)
            {
                if (_bodyMap.TryGetValue(_targetPlanetBodyId, out CelestialRuntimeNode tgtNode))
                {
                    _satelliteRootRect.anchoredPosition = GetTargetSatelliteOrbitPos(tgtNode);
                    _satelliteRootRect.localEulerAngles = new Vector3(0f, 0f, _spacecraftOrbitDeg + 90f);
                }
                _satelliteRootRect.gameObject.SetActive(true);
            }

            SelectBodyById(_targetPlanetBodyId, focusCamera: true);
            OnLaunchSequenceCompleted?.Invoke();
        }

        private void UpdateCameraPanAndZoom(float unscaledDt)
        {
            if (_isFollowingSelected && _bodyMap.TryGetValue(_selectedBodyId, out CelestialRuntimeNode follow))
            {
                _targetPanPx = -follow.CurrentStagePos * _targetZoom - new Vector2(110f, 0f);
            }

            float smooth = 1f - Mathf.Exp(-unscaledDt * 8.5f);
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, smooth);
            _currentPanPx = Vector2.Lerp(_currentPanPx, _targetPanPx, smooth);

            _stageRect.localScale = new Vector3(_currentZoom, _currentZoom, 1f);
            _stageRect.anchoredPosition = _currentPanPx;

            // Keep celestial body labels razor-sharp at high zoom by counter-scaling to ~1:1 screen pixels
            float invZoom = Mathf.Clamp(1f / Mathf.Max(0.22f, _currentZoom), 0.06f, 1.55f);
            for (int i = 0; i < _bodies.Count; i++)
            {
                Text lbl = _bodies[i].LabelText;
                if (lbl == null) continue;
                RectTransform lrt = lbl.rectTransform;
                lrt.localScale = new Vector3(invZoom, invZoom, 1f);
                lrt.anchoredPosition = new Vector2(0f, -6f * invZoom);
            }
        }

        private void ClampTargetPan()
        {
            float maxExtent = 1550f * Mathf.Max(0.5f, _targetZoom);
            _targetPanPx.x = Mathf.Clamp(_targetPanPx.x, -maxExtent, maxExtent);
            _targetPanPx.y = Mathf.Clamp(_targetPanPx.y, -maxExtent, maxExtent);
        }

        private void FitOverviewOnFirstValidLayout()
        {
            if (_hasInitialOverviewFit || _mapViewportRect == null) return;
            if (_mapViewportRect.rect.width < 120f || _mapViewportRect.rect.height < 120f) return;

            _hasInitialOverviewFit = true;
            if (!string.IsNullOrEmpty(_targetPlanetBodyId) && _bodyMap.ContainsKey(_targetPlanetBodyId))
            {
                SelectBodyById(_targetPlanetBodyId, focusCamera: true);
            }
            else
            {
                ResetCameraToSystemOverview();
            }
            _currentZoom = _targetZoom;
            _currentPanPx = _targetPanPx;
            if (_stageRect != null)
            {
                _stageRect.localScale = new Vector3(_currentZoom, _currentZoom, 1f);
                _stageRect.anchoredPosition = _currentPanPx;
            }
        }

        private void RefreshMissionVehicleSprites()
        {
            _artBank = _artBank ?? SolarSystemArtBank.Load();

            Sprite rocketSp = smallRocketSprite;
            string rocketId = _currentRocket != null ? (_currentRocket.Id ?? string.Empty).ToLowerInvariant() : string.Empty;
            if (rocketId.Contains("heavy")) rocketSp = heavyLiftRocketSprite ?? (_artBank != null ? _artBank.heavyLiftRocketSprite : null);
            else if (rocketId.Contains("medium") || rocketId.Contains("reusable")) rocketSp = reusableRocketSprite ?? (_artBank != null ? _artBank.reusableRocketSprite : null);
            if (rocketSp == null && _artBank != null) rocketSp = _artBank.smallRocketSprite;

            if (_rocketBodyImage != null)
            {
                _rocketBodyImage.sprite = rocketSp ?? _procDotSprite;
            }
            if (_rocketFlameImage != null)
            {
                _rocketFlameImage.sprite = rocketFlameSprite ?? (_artBank != null ? _artBank.rocketFlameSprite : _procSolidGlowSprite);
            }

            Sprite craftSp = cubesatSprite;
            for (int i = 0; i < _currentComponents.Count; i++)
            {
                string cid = (_currentComponents[i].Id ?? string.Empty).ToLowerInvariant();
                if (cid.Contains("deep") || cid.Contains("probe"))
                {
                    craftSp = deepSpaceProbeSprite ?? (_artBank != null ? _artBank.deepSpaceProbeSprite : null);
                    break;
                }
                if (cid.Contains("smallsat") || cid.Contains("bus_small"))
                {
                    craftSp = satelliteSprite ?? (_artBank != null ? _artBank.satelliteSprite : null);
                }
            }
            if (craftSp == null && _artBank != null)
            {
                craftSp = _artBank.cubesatSprite ?? _artBank.satelliteSprite;
            }

            if (_satelliteBodyImage != null)
            {
                _satelliteBodyImage.sprite = craftSp ?? _procDotSprite;
            }
        }

        private void SetHoveredBody(string bodyId)
        {
            _hoveredBodyId = bodyId;
            UpdateReticleVisibility();
        }

        private void ClearHoveredBody(string bodyId)
        {
            if (string.Equals(_hoveredBodyId, bodyId, StringComparison.Ordinal))
            {
                _hoveredBodyId = null;
                UpdateReticleVisibility();
            }
        }

        private void UpdateReticleVisibility()
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                CelestialRuntimeNode b = _bodies[i];
                bool isSel = string.Equals(b.Spec.Id, _selectedBodyId, StringComparison.Ordinal);
                bool isHov = string.Equals(b.Spec.Id, _hoveredBodyId, StringComparison.Ordinal);
                if (b.SelectionReticle != null)
                {
                    b.SelectionReticle.gameObject.SetActive(isSel || isHov);
                }
            }
        }

        private static void EnsureProceduralSprites()
        {
            if (_procRingSprite == null)
            {
                const int size = 1024;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                Color[] px = new Color[size * size];
                float half = size * 0.5f;
                float rMid = half - 5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - half;
                        float dy = y + 0.5f - half;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float diff = Mathf.Abs(dist - rMid);
                        if (diff < 2.6f)
                        {
                            float alpha = Mathf.Clamp01(1f - diff / 2.6f);
                            px[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                        }
                        else
                        {
                            px[y * size + x] = Color.clear;
                        }
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                _procRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                _procRingSprite.name = "Proc_OrbitRing";
            }

            if (_procSolidGlowSprite == null)
            {
                const int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color[] px = new Color[size * size];
                float half = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f - half) / half;
                        float dy = (y + 0.5f - half) / half;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        float stepped = Mathf.Round(a * a * 6f) / 6f;
                        px[y * size + x] = new Color(1f, 1f, 1f, stepped);
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                _procSolidGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            if (_procDotSprite == null)
            {
                Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color[] px = new Color[64];
                for (int i = 0; i < 64; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                _procDotSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f);
            }

            if (_procReticleSprite == null)
            {
                const int size = 32;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color[] px = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool edgeX = x < 2 || x >= size - 2;
                        bool edgeY = y < 2 || y >= size - 2;
                        bool cornerSeg = (x < 9 || x >= size - 9) && (y < 9 || y >= size - 9);
                        px[y * size + x] = (cornerSeg && (edgeX || edgeY)) ? Color.white : Color.clear;
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                _procReticleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            if (_procDiscSprite == null)
            {
                const int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color[] px = new Color[size * size];
                float half = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f - half) / half;
                        float dy = (y + 0.5f - half) / half;
                        px[y * size + x] = (dx * dx + dy * dy <= 1.0f) ? Color.white : Color.clear;
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                _procDiscSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
            if (_smoothDiscSprite == null)
            {
                const int size = 256;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                Color[] px = new Color[size * size];
                float half = size * 0.5f;
                float edgeSoftness = 2.0f / half;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f - half) / half;
                        float dy = (y + 0.5f - half) / half;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float alpha = Mathf.Clamp01((1.0f - dist) / edgeSoftness);
                        px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                _smoothDiscSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        private static Image CreateRadialSector(string name, Transform parent, Color tint, float fillAmount, out RectTransform rt)
        {
            GameObject obj = CreateUIObject(name, parent);
            rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(256f, 256f);
            Image img = obj.AddComponent<Image>();
            img.sprite = _smoothDiscSprite != null ? _smoothDiscSprite : _procDiscSprite;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = fillAmount;
            img.color = tint;
            img.raycastTarget = false;
            return img;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class OrbitEllipseGraphic : MaskableGraphic
    {
        public float Thickness = 2.4f;
        public float Feather = 1.1f;
        public int Segments = 240;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float rx = rect.width * 0.5f;
            float ry = rect.height * 0.5f;
            if (rx <= 1f || ry <= 1f) return;

            int segs = Mathf.Clamp(Segments, 32, 360);
            float halfCore = Mathf.Max(0.4f, Thickness * 0.5f);
            float outerPad = halfCore + Mathf.Max(0.2f, Feather);
            float innerPad = halfCore;

            Color c = color;
            Color32 coreCol = c;
            Color32 edgeCol = new Color32(coreCol.r, coreCol.g, coreCol.b, 0);
            Vector2 center = rect.center;

            for (int i = 0; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                Vector2 p = new Vector2(center.x + rx * cos, center.y + ry * sin);
                Vector2 n = new Vector2(cos / rx, sin / ry);
                float nLen = n.magnitude;
                Vector2 dir = nLen > 1e-6f ? n / nLen : new Vector2(cos, sin);

                vh.AddVert(p - dir * outerPad, edgeCol, Vector2.zero);
                vh.AddVert(p - dir * innerPad, coreCol, Vector2.zero);
                vh.AddVert(p + dir * innerPad, coreCol, Vector2.zero);
                vh.AddVert(p + dir * outerPad, edgeCol, Vector2.zero);
            }

            for (int i = 0; i < segs; i++)
            {
                int b0 = i * 4;
                int b1 = (i + 1) * 4;
                for (int s = 0; s < 3; s++)
                {
                    int v0 = b0 + s;
                    int v1 = b0 + s + 1;
                    int v2 = b1 + s + 1;
                    int v3 = b1 + s;
                    vh.AddTriangle(v0, v1, v2);
                    vh.AddTriangle(v2, v3, v0);
                }
            }
        }
    }
}
