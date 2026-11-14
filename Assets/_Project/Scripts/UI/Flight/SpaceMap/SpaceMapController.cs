using System;
using System.Collections;
using System.Collections.Generic;
using AstraLink.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// 2D Pixel-Art Space Map, Automatic Rocket Launch Sequence (Phase 1 & Phase 2),
    /// and Live Orbital Satellite Movement Controller (Ivan V).
    /// Path: Assets/_Project/Scripts/UI/Flight/SpaceMap/SpaceMapController.cs
    /// Prefab: SpaceMapFlightView.prefab
    /// </summary>
    public class SpaceMapController : MonoBehaviour
    {
        public static SpaceMapController Instance { get; private set; }

        private const int BezierDotCount = 26;
        private const int SmokeCount = 3;

        [Header("Background & Environment Sprites (64x64 Point Filter — Optional, uses placeholders if null)")]
        [SerializeField] private Sprite starfieldSprite;
        [SerializeField] private Sprite launchPadSprite;
        [SerializeField] private Sprite eclipseOverlaySprite;

        [Header("Planet Sprites (earth.png, moon.png, mars.png, venus_planet.png)")]
        [SerializeField] private Sprite earthSprite;
        [SerializeField] private Sprite moonSprite;
        [SerializeField] private Sprite marsSprite;
        [SerializeField] private Sprite venusSprite;

        [Header("Rocket & VFX Sprites (small_rocket, reusable_rocket, heavy_lift_rocket, flame, smoke)")]
        [SerializeField] private Sprite smallRocketSprite;
        [SerializeField] private Sprite reusableRocketSprite;
        [SerializeField] private Sprite heavyLiftRocketSprite;
        [SerializeField] private Sprite rocketFlameSprite;
        [SerializeField] private Sprite launchSmokeSprite;

        [Header("Satellite Sprites (cubesat.png, satellite.png, deep_space_probe.png)")]
        [SerializeField] private Sprite cubesatSprite;
        [SerializeField] private Sprite satelliteSprite;
        [SerializeField] private Sprite deepSpaceProbeSprite;

        [Header("Animation Timings")]
        [SerializeField] private float phase1AscentDurationSeconds = 4.0f;
        [SerializeField] private float phase2SeparationDurationSeconds = 1.0f;
        [SerializeField] private bool showSandboxTestBar = true;

        // Cached mission config
        private MissionGoal _currentGoal;
        private OrbitDefinition _currentOrbit;
        private RocketOption _currentRocket;
        private List<SatelliteComponent> _currentComponents = new List<SatelliteComponent>();

        // UI Hierarchy References
        private RectTransform _mapViewportRect;
        private RectTransform _starfieldLayerA;
        private RectTransform _starfieldLayerB;
        private Image _starfieldImageA;
        private Image _starfieldImageB;

        // Earth & Launch Pad
        private RectTransform _earthRect;
        private Image _earthImage;
        private Text _earthPlaceholderLabel;
        private RectTransform _launchPadRect;
        private Image _launchPadImage;
        private Text _launchPadPlaceholderLabel;

        // Target Planet (used for Moon, Mars, Venus)
        private RectTransform _targetPlanetRect;
        private Image _targetPlanetImage;
        private Text _targetPlanetPlaceholderLabel;

        // Orbit Ring, Eclipse Cone & Comm Window Arc (around active orbit planet)
        private RectTransform _orbitContainerRect;
        private Image _orbitRingImage;
        private Image _eclipseSectorImage;
        private Image _commSectorImage;
        private Text _eclipseTagText;
        private Text _commTagText;

        // Bezier Transfer Trajectory Dots (Earth -> Target Planet)
        private RectTransform _bezierContainerRect;
        private readonly List<RectTransform> _bezierDotRects = new List<RectTransform>();

        // Rocket + Flame + Smoke + Mars Aerobraking Glow
        private RectTransform _rocketRootRect;
        private Image _rocketBodyImage;
        private Text _rocketPlaceholderLabel;
        private RectTransform _rocketFlameRect;
        private Image _rocketFlameImage;
        private Image _aerobrakeGlowImage;
        private readonly RectTransform[] _smokeRects = new RectTransform[3];
        private readonly Image[] _smokeImages = new Image[3];

        // Player Satellite + Deploy Flash
        private RectTransform _satelliteRootRect;
        private Image _satelliteBodyImage;
        private Text _satellitePlaceholderLabel;
        private Image _deployFlashImage;

        // Launch Controls (Skip / 2x) & Status Banner
        private GameObject _launchControlsPanel;
        private Text _statusBannerText;
        private Text _speedButtonText;
        private GameObject _sandboxToolbarObj;

        // State
        private Coroutine _launchCoroutine;
        private bool _isLaunchPlaying;
        private bool _skipRequested;
        private float _launchSpeedMultiplier = 1.0f;
        private float _orbitRadiusPx = 220f;
        private Vector2 _activeOrbitCenterPos = Vector2.zero;
        private bool _isDemoAutoOrbiting;
        private float _demoOrbitProgress;

        // Procedural fallback sprites (Point filter 64x64)
        private static Sprite _procStarfieldSprite;
        private static Sprite _procPlanetCircleSprite;
        private static Sprite _procDashedRingSprite;
        private static Sprite _procSectorArcSprite;
        private static Sprite _procEclipseConeSprite;
        private static Sprite _procLaunchPadSprite;
        private static Sprite _procRocketSprite;
        private static Sprite _procFlameSprite;
        private static Sprite _procSmokeSprite;
        private static Sprite _procSatelliteSprite;
        private static Sprite _procDotSprite;

        /// <summary>
        /// Fired when Phase 1 (Rocket Ascent) and Phase 2 (Satellite Deployment) complete.
        /// </summary>
        public event Action OnLaunchSequenceCompleted;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (_currentOrbit == null)
            {
                SetupDemoPreset(0);
            }
        }

        private void Update()
        {
            AnimateStarfieldDrift();

            if (_isDemoAutoOrbiting && !_isLaunchPlaying)
            {
                float prev = _demoOrbitProgress;
                _demoOrbitProgress = (_demoOrbitProgress + Time.deltaTime * 0.12f) % 1.0f;

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

        // =====================================================================
        // PUBLIC INTEGRATION API (Required by Spec)
        // =====================================================================

        /// <summary>
        /// Initializes the Space Map layout (planets, orbit ring, eclipse/comm sectors, rocket, and satellite sprites)
        /// for the given mission contract, orbit, rocket, and assembled satellite components.
        /// </summary>
        public void Initialize(
            MissionGoal goal,
            OrbitDefinition orbit,
            RocketOption rocket,
            List<SatelliteComponent> components)
        {
            _currentGoal = goal;
            _currentOrbit = orbit ?? CreateFallbackOrbit(OrbitType.LowEarth, "orbit_leo", "LEO 400 km (Earth)", 0.35f, 0.25f);
            _currentRocket = rocket ?? new RocketOption { Id = "rocket_light", Name = "Light Rocket 'Arrow-1'" };
            _currentComponents = components ?? new List<SatelliteComponent>();
            _isDemoAutoOrbiting = false;

            EnsureSpaceMapHierarchy();
            ConfigureLayoutForMission();
            ResetRocketOnLaunchPad();
        }

        /// <summary>
        /// Starts the automatic rocket launch animation:
        /// - Phase 1 (~3-5s, with Skip / 2x controls): Liftoff with pulsing flame + smoke, ascent to LEO or Bezier transfer to Moon/Mars/Venus.
        /// - Phase 2 (~1s): Flame extinguishes, rocket fades out (alpha 1 -> 0) while satellite pops in (scale 0.2 -> 1.0).
        /// Invokes OnLaunchSequenceCompleted at the end.
        /// </summary>
        public void PlayLaunchSequence()
        {
            EnsureSpaceMapHierarchy();
            _isDemoAutoOrbiting = false;

            if (_launchCoroutine != null)
            {
                StopCoroutine(_launchCoroutine);
                _launchCoroutine = null;
            }

            if (Application.isPlaying && gameObject.activeInHierarchy)
            {
                _launchCoroutine = StartCoroutine(RunLaunchSequenceRoutine());
            }
            else
            {
                CompleteLaunchInstantly();
            }
        }

        /// <summary>
        /// Smoothly moves the player's satellite around the target planet's orbital ring according to OrbitProgress.
        /// </summary>
        public void UpdateFlightTick(
            SimulationTickSnapshot currentTick,
            SimulationTickSnapshot nextTick,
            float lerpT)
        {
            EnsureSpaceMapHierarchy();
            if (currentTick == null) return;

            float progressA = Mathf.Clamp01(currentTick.OrbitProgress);
            float progressB = nextTick != null ? Mathf.Clamp01(nextTick.OrbitProgress) : progressA;

            // Handle wrap-around from end of orbit (e.g. 0.95 -> 0.0)
            if (progressB < progressA && (progressA - progressB) > 0.5f)
            {
                progressB += 1.0f;
            }

            float interpolatedProgress = Mathf.Repeat(Mathf.Lerp(progressA, progressB, Mathf.Clamp01(lerpT)), 1.0f);
            PositionSatelliteAtOrbitProgress(interpolatedProgress, currentTick);
        }

        // =====================================================================
        // LAUNCH CONTROLS (2x Speed / Skip) & SANDBOX PRESETS
        // =====================================================================

        public void ToggleLaunchSpeed2x()
        {
            _launchSpeedMultiplier = (_launchSpeedMultiplier < 1.5f) ? 2.0f : 1.0f;
            if (_speedButtonText != null)
            {
                _speedButtonText.text = _launchSpeedMultiplier > 1.5f ? "SPEED: 2x (ACTIVE)" : "SPEED: 1x -> 2x";
            }
        }

        public void SkipLaunchSequence()
        {
            if (_isLaunchPlaying)
            {
                _skipRequested = true;
            }
            else
            {
                CompleteLaunchInstantly();
            }
        }

        [ContextMenu("Generate SpaceMap UI In Scene")]
        public void BuildUIInEditor()
        {
            Transform existing = transform.Find("SpaceMapContentRoot");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            EnsureSpaceMapHierarchy();
            SetupDemoPreset(0);
            Debug.Log("[SpaceMapController] SpaceMapFlightView UI Hierarchy generated with placeholders & animations!");
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Assign Real Pixel-Art Sprites From Assets/_Project/Art")]
        public void AutoAssignRealSpritesInEditor()
        {
            starfieldSprite = LoadSpriteAt("Assets/_Project/Art/Planets/Backgrounds/orbital_starfield.png");
            earthSprite = LoadSpriteAt("Assets/_Project/Art/Planets/earth.png");
            eclipseOverlaySprite = LoadSpriteAt("Assets/_Project/Art/Planets/eclipse.png");
            moonSprite = LoadSpriteAt("Assets/_Project/Art/Planets/moon.png");
            marsSprite = LoadSpriteAt("Assets/_Project/Art/Planets/mars.png");
            venusSprite = LoadSpriteAt("Assets/_Project/Art/Planets/venus_planet.png");

            launchPadSprite = LoadSpriteAt("Assets/_Project/Art/Spacecraft/Launch/launch_pad.png");
            smallRocketSprite = LoadSpriteAt("Assets/_Project/Art/Spacecraft/small_rocket.png");
            reusableRocketSprite = LoadSpriteAt("Assets/_Project/Art/Spacecraft/reusable_rocket.png");
            heavyLiftRocketSprite = LoadSpriteAt("Assets/_Project/Art/Spacecraft/heavy_lift_rocket.png");
            rocketFlameSprite = LoadSpriteAt("Assets/_Project/Art/Spacecraft/Launch/rocket_flame.png");
            launchSmokeSprite = LoadSpriteAt("Assets/_Project/Art/Spacecraft/Launch/launch_smoke.png");

            cubesatSprite = LoadSpriteAt("Assets/_Project/Art/Spacecraft/cubesat.png");
            satelliteSprite = LoadSpriteAt("Assets/_Project/Art/Spacecraft/satellite.png");
            deepSpaceProbeSprite = LoadSpriteAt("Assets/_Project/Art/Spacecraft/deep_space_probe.png");

            UnityEditor.EditorUtility.SetDirty(this);
            ConfigureLayoutForMission();
            Debug.Log("[SpaceMapController] Assigned all 15 pixel-art sprites from Assets/_Project/Art!");
        }

        private static Sprite LoadSpriteAt(string assetPath)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
#endif

        public void SetupDemoPreset(int presetIndex)
        {
            OrbitDefinition orbit;
            MissionGoal goal;
            RocketOption rocket;
            var comps = new List<SatelliteComponent>();

            switch (presetIndex)
            {
                case 1: // Mission 2: Moon (Lunar Orbit)
                    orbit = CreateFallbackOrbit(OrbitType.Lunar, "orbit_lunar", "Lunar Orbit (100 km)", 0.45f, 0.15f);
                    goal = new MissionGoal { Id = "m2_lunar_mapper", Title = "2. Lunar Radar Mapper", OrbitId = "orbit_lunar" };
                    rocket = new RocketOption { Id = "rocket_medium", Name = "Medium Rocket 'Falcon-M'" };
                    comps.Add(new SatelliteComponent { Id = "bus_smallsat", Name = "SmallSat Bus 'Orion-S'", Category = ComponentCategory.CorePlatform });
                    break;

                case 2: // Mission 3: Mars (with Atmospheric Aerobraking)
                    orbit = CreateFallbackOrbit(OrbitType.Mars, "orbit_mars", "Mars Orbit (350 km)", 0.35f, 0.15f);
                    goal = new MissionGoal { Id = "m3_mars_probe", Title = "3. Mars Deep-Space Probe", OrbitId = "orbit_mars" };
                    rocket = new RocketOption { Id = "rocket_heavy", Name = "Heavy Interplanetary 'Titan-9'" };
                    comps.Add(new SatelliteComponent { Id = "bus_deepspace", Name = "DeepSpace Bus 'Voyager-M'", Category = ComponentCategory.CorePlatform });
                    break;

                case 3: // Venus (DeepSpace)
                    orbit = CreateFallbackOrbit(OrbitType.DeepSpace, "orbit_venus", "Venus Orbit (300 km)", 0.40f, 0.15f);
                    goal = new MissionGoal { Id = "m4_venus_probe", Title = "4. Venus Atmospheric Probe", OrbitId = "orbit_venus" };
                    rocket = new RocketOption { Id = "rocket_heavy", Name = "Heavy Interplanetary 'Titan-9'" };
                    comps.Add(new SatelliteComponent { Id = "bus_deepspace", Name = "DeepSpace Bus 'Voyager-M'", Category = ComponentCategory.CorePlatform });
                    break;

                default: // Mission 1: LEO (Earth Centered)
                    orbit = CreateFallbackOrbit(OrbitType.LowEarth, "orbit_leo", "LEO 400 km (Earth)", 0.35f, 0.25f);
                    goal = new MissionGoal { Id = "m1_leo_weather", Title = "1. LEO Weather CubeSat", OrbitId = "orbit_leo" };
                    rocket = new RocketOption { Id = "rocket_light", Name = "Light Rocket 'Arrow-1'" };
                    comps.Add(new SatelliteComponent { Id = "bus_cubesat_3u", Name = "CubeSat 3U Bus", Category = ComponentCategory.CorePlatform });
                    break;
            }

            Initialize(goal, orbit, rocket, comps);
        }

        public void StartDemoLaunchAndOrbit()
        {
            _OnLaunchCompletedDemoSubscribe();
            PlayLaunchSequence();
        }

        private void _OnLaunchCompletedDemoSubscribe()
        {
            OnLaunchSequenceCompleted -= EnableDemoOrbitAfterLaunch;
            OnLaunchSequenceCompleted += EnableDemoOrbitAfterLaunch;
        }

        private void EnableDemoOrbitAfterLaunch()
        {
            OnLaunchSequenceCompleted -= EnableDemoOrbitAfterLaunch;
            _demoOrbitProgress = 0f;
            _isDemoAutoOrbiting = true;
        }

        // =====================================================================
        // PLANET, ORBIT & BEZIER LAYOUT CONFIGURATION
        // =====================================================================

        private bool IsTwoPlanetTransferMission()
        {
            if (_currentOrbit == null) return false;
            if (_currentOrbit.Type != OrbitType.LowEarth) return true;
            string id = _currentOrbit.Id ?? string.Empty;
            return id.IndexOf("lunar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("moon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("mars", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("venus", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsMarsMission()
        {
            if (_currentOrbit == null) return false;
            if (_currentOrbit.Type == OrbitType.Mars) return true;
            string id = _currentOrbit.Id ?? string.Empty;
            return id.IndexOf("mars", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsVenusMission()
        {
            if (_currentOrbit == null) return false;
            string id = _currentOrbit.Id ?? string.Empty;
            return id.IndexOf("venus", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ConfigureLayoutForMission()
        {
            EnsureProceduralSprites();

            // 1. Starfield Background
            AssignSpriteWithPointFilter(_starfieldImageA, starfieldSprite, _procStarfieldSprite, Color.white);
            AssignSpriteWithPointFilter(_starfieldImageB, starfieldSprite, _procStarfieldSprite, new Color(0.8f, 0.9f, 1f, 0.55f));

            bool isTwoPlanets = IsTwoPlanetTransferMission();

            // 2. Earth & Launch Pad positioning
            AssignSpriteWithPointFilter(_earthImage, earthSprite, _procPlanetCircleSprite,
                earthSprite != null ? Color.white : new Color(0.22f, 0.58f, 0.95f, 1f));
            SetPlaceholderVisible(_earthPlaceholderLabel, earthSprite == null, "EARTH\n(earth.png)");

            AssignSpriteWithPointFilter(_launchPadImage, launchPadSprite, _procLaunchPadSprite,
                launchPadSprite != null ? Color.white : new Color(0.85f, 0.88f, 0.92f, 1f));
            SetPlaceholderVisible(_launchPadPlaceholderLabel, launchPadSprite == null, "PAD");

            if (!isTwoPlanets)
            {
                // Mission 1 (LEO): Earth centered and large, orbit around Earth
                _earthRect.anchoredPosition = new Vector2(0f, -10f);
                _earthRect.sizeDelta = new Vector2(210f, 210f);

                // Launch pad attached at top-left edge of Earth
                _launchPadRect.anchoredPosition = new Vector2(-10f, -118f);
                _launchPadRect.localEulerAngles = new Vector3(0f, 0f, 180f);

                _targetPlanetRect.gameObject.SetActive(false);
                _bezierContainerRect.gameObject.SetActive(false);

                _activeOrbitCenterPos = _earthRect.anchoredPosition;
                _orbitRadiusPx = 240f;
            }
            else
            {
                // Mission 2 (Moon), Mission 3 (Mars), or Venus: TWO PLANETS IN FRAME
                _earthRect.anchoredPosition = new Vector2(-520f, -60f);
                _earthRect.sizeDelta = new Vector2(165f, 165f);

                // Launch pad on right/top side of Earth facing target planet
                _launchPadRect.anchoredPosition = new Vector2(-438f, -35f);
                _launchPadRect.localEulerAngles = new Vector3(0f, 0f, -65f);

                _targetPlanetRect.gameObject.SetActive(true);
                _targetPlanetRect.anchoredPosition = new Vector2(430f, 20f);
                _targetPlanetRect.sizeDelta = new Vector2(170f, 170f);

                Sprite customTargetSprite;
                Color fallbackPlanetColor;
                string planetLabel;

                if (IsMarsMission())
                {
                    customTargetSprite = marsSprite;
                    fallbackPlanetColor = new Color(0.90f, 0.38f, 0.22f, 1f);
                    planetLabel = "MARS\n(mars.png)";
                }
                else if (IsVenusMission())
                {
                    customTargetSprite = venusSprite;
                    fallbackPlanetColor = new Color(0.95f, 0.78f, 0.36f, 1f);
                    planetLabel = "VENUS\n(venus_planet.png)";
                }
                else
                {
                    customTargetSprite = moonSprite;
                    fallbackPlanetColor = new Color(0.76f, 0.80f, 0.86f, 1f);
                    planetLabel = "MOON\n(moon.png)";
                }

                AssignSpriteWithPointFilter(_targetPlanetImage, customTargetSprite, _procPlanetCircleSprite,
                    customTargetSprite != null ? Color.white : fallbackPlanetColor);
                SetPlaceholderVisible(_targetPlanetPlaceholderLabel, customTargetSprite == null, planetLabel);

                _activeOrbitCenterPos = _targetPlanetRect.anchoredPosition;
                _orbitRadiusPx = 210f;

                // Build dashed Bezier curve between Earth LaunchPad and Target Orbit entry point
                _bezierContainerRect.gameObject.SetActive(true);
                UpdateBezierTransferDots();
            }

            // 3. Configure Orbit Ring + Eclipse Cone + Comm Window Arc around _activeOrbitCenterPos
            _orbitContainerRect.anchoredPosition = _activeOrbitCenterPos;
            float diameter = _orbitRadiusPx * 2f;
            _orbitContainerRect.sizeDelta = new Vector2(diameter, diameter);

            float eclipseFrac = _currentOrbit != null ? Mathf.Clamp(_currentOrbit.EclipseFraction, 0.1f, 0.6f) : 0.35f;
            float commFrac = _currentOrbit != null ? Mathf.Clamp(_currentOrbit.CommWindowFraction, 0.1f, 0.6f) : 0.25f;

            // Comm Window Sector (OrbitProgress 0 .. commFrac) -> Green Arc on ground station side
            if (_commSectorImage != null)
            {
                _commSectorImage.type = Image.Type.Filled;
                _commSectorImage.fillMethod = Image.FillMethod.Radial360;
                _commSectorImage.fillOrigin = (int)Image.Origin360.Top;
                _commSectorImage.fillClockwise = true;
                _commSectorImage.fillAmount = commFrac;
                _commSectorImage.rectTransform.localEulerAngles = Vector3.zero;
            }

            // Eclipse Shadow Sector (OrbitProgress (1 - eclipseFrac) .. 1.0) -> Dark semi-transparent cone/sector
            if (_eclipseSectorImage != null)
            {
                if (eclipseOverlaySprite != null)
                {
                    _eclipseSectorImage.sprite = eclipseOverlaySprite;
                }
                _eclipseSectorImage.type = Image.Type.Filled;
                _eclipseSectorImage.fillMethod = Image.FillMethod.Radial360;
                _eclipseSectorImage.fillOrigin = (int)Image.Origin360.Top;
                _eclipseSectorImage.fillClockwise = false;
                _eclipseSectorImage.fillAmount = eclipseFrac;
                _eclipseSectorImage.rectTransform.localEulerAngles = Vector3.zero;
            }

            // Position sector labels around orbit ring
            if (_commTagText != null)
            {
                float midCommProgress = commFrac * 0.5f;
                _commTagText.rectTransform.anchoredPosition = GetLocalOrbitPoint(midCommProgress, _orbitRadiusPx + 28f);
            }
            if (_eclipseTagText != null)
            {
                float midEclipseProgress = 1.0f - (eclipseFrac * 0.5f);
                _eclipseTagText.rectTransform.anchoredPosition = GetLocalOrbitPoint(midEclipseProgress, _orbitRadiusPx + 28f);
            }

            // 4. Configure Rocket & Satellite sprites based on selection
            Sprite chosenRocketSprite = SelectRocketSprite(_currentRocket);
            AssignSpriteWithPointFilter(_rocketBodyImage, chosenRocketSprite, _procRocketSprite,
                chosenRocketSprite != null ? Color.white : new Color(0.95f, 0.96f, 1f, 1f));
            SetPlaceholderVisible(_rocketPlaceholderLabel, chosenRocketSprite == null, GetRocketShortTag(_currentRocket));

            AssignSpriteWithPointFilter(_rocketFlameImage, rocketFlameSprite, _procFlameSprite,
                rocketFlameSprite != null ? Color.white : new Color(1f, 0.68f, 0.18f, 1f));

            for (int i = 0; i < _smokeImages.Length; i++)
            {
                AssignSpriteWithPointFilter(_smokeImages[i], launchSmokeSprite, _procSmokeSprite,
                    new Color(0.88f, 0.90f, 0.94f, 0f));
            }

            Sprite chosenSatSprite = SelectSatelliteSprite(_currentComponents, _currentOrbit);
            AssignSpriteWithPointFilter(_satelliteBodyImage, chosenSatSprite, _procSatelliteSprite,
                chosenSatSprite != null ? Color.white : new Color(0.45f, 0.88f, 1f, 1f));
            SetPlaceholderVisible(_satellitePlaceholderLabel, chosenSatSprite == null, GetSatelliteShortTag(_currentComponents));
        }

        private void UpdateBezierTransferDots()
        {
            Vector2 p0 = _launchPadRect.anchoredPosition;
            Vector2 p3 = GetWorldOrbitPoint(0.75f); // Left-side entry into target planet's orbital ring (for braking arc 0.75 -> 1.0)
            Vector2 p1 = p0 + new Vector2(190f, 240f);
            Vector2 p2 = p3 + new Vector2(-180f, -130f);

            for (int i = 0; i < _bezierDotRects.Count; i++)
            {
                float t = (i + 1f) / (_bezierDotRects.Count + 1f);
                Vector2 pt = EvaluateCubicBezier(p0, p1, p2, p3, t);
                _bezierDotRects[i].anchoredPosition = pt;
            }
        }

        // =====================================================================
        // PHASE 1 & PHASE 2 LAUNCH ANIMATION COROUTINE
        // =====================================================================

        private void ResetRocketOnLaunchPad()
        {
            _rocketRootRect.gameObject.SetActive(true);
            _rocketRootRect.anchoredPosition = _launchPadRect.anchoredPosition;
            _rocketRootRect.localScale = Vector3.one;
            _rocketRootRect.localEulerAngles = _launchPadRect.localEulerAngles;

            SetImageAlpha(_rocketBodyImage, 1f);
            if (_rocketPlaceholderLabel != null) SetTextAlpha(_rocketPlaceholderLabel, 1f);

            _rocketFlameRect.gameObject.SetActive(false);
            if (_aerobrakeGlowImage != null) _aerobrakeGlowImage.gameObject.SetActive(false);

            for (int i = 0; i < _smokeRects.Length; i++)
            {
                if (_smokeRects[i] != null) _smokeRects[i].gameObject.SetActive(false);
            }

            _satelliteRootRect.gameObject.SetActive(false);
            if (_deployFlashImage != null) _deployFlashImage.gameObject.SetActive(false);

            if (_statusBannerText != null)
            {
                string orbitName = _currentOrbit != null ? _currentOrbit.Name : "LEO";
                _statusBannerText.text = string.Format("PAD READY: {0} -> Target: {1} (Click Launch or PlayLaunchSequence)",
                    _currentRocket != null ? _currentRocket.Name : "Rocket", orbitName);
            }
        }

        private IEnumerator RunLaunchSequenceRoutine()
        {
            _isLaunchPlaying = true;
            _skipRequested = false;
            if (_launchControlsPanel != null) _launchControlsPanel.SetActive(true);

            ResetRocketOnLaunchPad();

            // Enable Flame & Smoke Puffs at Liftoff
            _rocketFlameRect.gameObject.SetActive(true);
            Vector2 padOrigin = _launchPadRect.anchoredPosition;
            for (int i = 0; i < _smokeRects.Length; i++)
            {
                if (_smokeRects[i] != null)
                {
                    _smokeRects[i].gameObject.SetActive(true);
                    _smokeRects[i].anchoredPosition = padOrigin;
                    _smokeRects[i].localScale = Vector3.one * 0.5f;
                    SetImageAlpha(_smokeImages[i], 0.85f);
                }
            }

            bool isTwoPlanets = IsTwoPlanetTransferMission();
            bool isMars = IsMarsMission();
            Vector2 orbitInsertionPoint = GetWorldOrbitPoint(0.0f);

            // Control points for smooth trajectory
            Vector2 p0 = padOrigin;
            Vector2 p1, p2, p3;

            if (!isTwoPlanets)
            {
                // LEO curved ascent from bottom of Earth around the left side up to top of LEO ring
                p3 = orbitInsertionPoint;
                p1 = padOrigin + new Vector2(-260f, -40f);
                p2 = orbitInsertionPoint + new Vector2(-240f, -20f);
            }
            else
            {
                // Interplanetary / Lunar Bezier transfer from Earth pad to left entry of Target Planet orbit (0.75f),
                // followed by a braking orbital turn (0.75f -> 1.0f) around the target planet!
                p3 = GetWorldOrbitPoint(0.75f);
                p1 = p0 + new Vector2(190f, 240f);
                p2 = p3 + new Vector2(-180f, -130f);
            }

            // --- PHASE 1: ROCKET ASCENT & TRANSFER (~3–5 seconds) ---
            float elapsed = 0f;
            float duration = Mathf.Max(2.5f, phase1AscentDurationSeconds);

            while (elapsed < duration && !_skipRequested)
            {
                float dt = Time.deltaTime * _launchSpeedMultiplier;
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / duration);

                if (!isTwoPlanets)
                {
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    Vector2 pos = EvaluateCubicBezier(p0, p1, p2, p3, smoothT);
                    Vector2 tangent = EvaluateCubicBezierTangent(p0, p1, p2, p3, Mathf.Clamp(smoothT, 0.01f, 0.99f));
                    _rocketRootRect.anchoredPosition = pos;

                    if (tangent.sqrMagnitude > 0.0001f)
                    {
                        float angleDeg = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f;
                        _rocketRootRect.localEulerAngles = new Vector3(0f, 0f, angleDeg);
                    }
                }
                else
                {
                    // First 72%: Bezier transfer from Earth to target planet's orbit entry (0.75f)
                    // Last 28%: Braking orbital turn along target planet's orbit (0.75f -> 1.0f)
                    const float transferSplit = 0.72f;
                    if (t < transferSplit)
                    {
                        float bezierT = Mathf.SmoothStep(0f, 1f, t / transferSplit);
                        Vector2 pos = EvaluateCubicBezier(p0, p1, p2, p3, bezierT);
                        Vector2 tangent = EvaluateCubicBezierTangent(p0, p1, p2, p3, Mathf.Clamp(bezierT, 0.01f, 0.99f));
                        _rocketRootRect.anchoredPosition = pos;

                        if (tangent.sqrMagnitude > 0.0001f)
                        {
                            float angleDeg = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f;
                            _rocketRootRect.localEulerAngles = new Vector3(0f, 0f, angleDeg);
                        }
                    }
                    else
                    {
                        float brakeU = Mathf.SmoothStep(0f, 1f, (t - transferSplit) / (1f - transferSplit));
                        float arcProgress = Mathf.Lerp(0.75f, 1.0f, brakeU);
                        Vector2 pos = GetWorldOrbitPoint(arcProgress);
                        Vector2 nextPos = GetWorldOrbitPoint(Mathf.Min(1.0f, arcProgress + 0.01f));
                        Vector2 tangent = nextPos - pos;
                        _rocketRootRect.anchoredPosition = pos;

                        if (tangent.sqrMagnitude > 0.0001f)
                        {
                            float angleDeg = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f;
                            _rocketRootRect.localEulerAngles = new Vector3(0f, 0f, angleDeg);
                        }
                    }
                }

                // Pulsing nozzle flame (scale pulse)
                float flamePulse = 0.85f + 0.35f * Mathf.Sin(Time.time * 28f);
                _rocketFlameRect.localScale = new Vector3(1f, flamePulse, 1f);

                // Launch smoke puffs expanding & fading during first 1.8s
                if (elapsed < 1.8f)
                {
                    float smokeT = elapsed / 1.8f;
                    Vector2[] offsets =
                    {
                        new Vector2(-42f, -28f),
                        new Vector2(38f, -34f),
                        new Vector2(-12f, -52f)
                    };
                    for (int i = 0; i < _smokeRects.Length; i++)
                    {
                        if (_smokeRects[i] == null) continue;
                        _smokeRects[i].anchoredPosition = padOrigin + offsets[i] * smokeT;
                        float s = Mathf.Lerp(0.5f, 1.9f + i * 0.25f, smokeT);
                        _smokeRects[i].localScale = new Vector3(s, s, 1f);
                        SetImageAlpha(_smokeImages[i], Mathf.Lerp(0.85f, 0f, smokeT));
                    }
                }
                else
                {
                    for (int i = 0; i < _smokeRects.Length; i++)
                    {
                        if (_smokeRects[i] != null && _smokeRects[i].gameObject.activeSelf)
                            _smokeRects[i].gameObject.SetActive(false);
                    }
                }

                // Mars Atmospheric Braking Glow near arrival (t = 0.72 .. 0.98)
                if (_aerobrakeGlowImage != null)
                {
                    bool showMarsGlow = isMars && t >= 0.72f && t <= 0.98f;
                    _aerobrakeGlowImage.gameObject.SetActive(showMarsGlow);
                    if (showMarsGlow)
                    {
                        float glowT = Mathf.Sin(((t - 0.72f) / 0.26f) * Mathf.PI);
                        _aerobrakeGlowImage.color = new Color(1.0f, 0.48f, 0.12f, 0.85f * glowT);
                    }
                }

                if (_statusBannerText != null)
                {
                    if (!isTwoPlanets)
                        _statusBannerText.text = string.Format("PHASE 1: LEO ORBITAL ASCENT ({0:0}%)", t * 100f);
                    else if (isMars && t >= 0.72f)
                        _statusBannerText.text = "PHASE 1: MARS ATMOSPHERIC AEROBRAKING MANEUVER!";
                    else
                        _statusBannerText.text = string.Format("PHASE 1: INTERPLANETARY TRANSFER TRAJECTORY ({0:0}%)", t * 100f);
                }

                yield return null;
            }

            // Ensure smoke & aerobrake glow are off at end of Phase 1
            _rocketFlameRect.gameObject.SetActive(false);
            if (_aerobrakeGlowImage != null) _aerobrakeGlowImage.gameObject.SetActive(false);
            for (int i = 0; i < _smokeRects.Length; i++)
            {
                if (_smokeRects[i] != null) _smokeRects[i].gameObject.SetActive(false);
            }

            // --- PHASE 2: ROCKET FADE-OUT & SATELLITE DEPLOYMENT (~1 second) ---
            _rocketRootRect.anchoredPosition = orbitInsertionPoint;
            _rocketRootRect.localEulerAngles = new Vector3(0f, 0f, -90f); // Aligned along clockwise top tangent

            _satelliteRootRect.gameObject.SetActive(true);
            _satelliteRootRect.anchoredPosition = orbitInsertionPoint;
            _satelliteRootRect.localScale = Vector3.one * 0.2f;
            _satelliteRootRect.localEulerAngles = Vector3.zero;
            SetImageAlpha(_satelliteBodyImage, 1f);

            if (_deployFlashImage != null)
            {
                _deployFlashImage.gameObject.SetActive(true);
                _deployFlashImage.rectTransform.anchoredPosition = orbitInsertionPoint;
            }

            float sepElapsed = 0f;
            float sepDuration = Mathf.Max(0.4f, phase2SeparationDurationSeconds);

            while (sepElapsed < sepDuration && !_skipRequested)
            {
                float dt = Time.deltaTime * _launchSpeedMultiplier;
                sepElapsed += dt;
                float u = Mathf.Clamp01(sepElapsed / sepDuration);

                // Rocket fades out (alpha 1 -> 0) and drifts slightly sideways/behind
                _rocketRootRect.anchoredPosition = orbitInsertionPoint + new Vector2(-36f * u, 16f * u);
                SetImageAlpha(_rocketBodyImage, 1f - u);
                if (_rocketPlaceholderLabel != null) SetTextAlpha(_rocketPlaceholderLabel, 1f - u);

                // Satellite pops in (scale 0.2 -> 1.0)
                float satScale = Mathf.Lerp(0.2f, 1.0f, Mathf.SmoothStep(0f, 1f, u));
                _satelliteRootRect.localScale = new Vector3(satScale, satScale, 1f);

                // Flash ring expands & fades
                if (_deployFlashImage != null)
                {
                    float flashScale = Mathf.Lerp(0.4f, 2.2f, u);
                    _deployFlashImage.rectTransform.localScale = new Vector3(flashScale, flashScale, 1f);
                    _deployFlashImage.color = new Color(0.75f, 0.96f, 1f, Mathf.Lerp(0.9f, 0f, u));
                }

                if (_statusBannerText != null)
                {
                    _statusBannerText.text = "PHASE 2: PAYLOAD FAIRING SEPARATION & SATELLITE DEPLOYMENT...";
                }

                yield return null;
            }

            CompleteLaunchInstantly();
        }

        private void CompleteLaunchInstantly()
        {
            _isLaunchPlaying = false;
            _skipRequested = false;
            _launchCoroutine = null;

            _rocketFlameRect.gameObject.SetActive(false);
            if (_aerobrakeGlowImage != null) _aerobrakeGlowImage.gameObject.SetActive(false);
            if (_deployFlashImage != null) _deployFlashImage.gameObject.SetActive(false);
            for (int i = 0; i < _smokeRects.Length; i++)
            {
                if (_smokeRects[i] != null) _smokeRects[i].gameObject.SetActive(false);
            }

            _rocketRootRect.gameObject.SetActive(false);

            Vector2 orbitInsertionPoint = GetWorldOrbitPoint(0.0f);
            _satelliteRootRect.gameObject.SetActive(true);
            _satelliteRootRect.anchoredPosition = orbitInsertionPoint;
            _satelliteRootRect.localScale = Vector3.one;
            SetImageAlpha(_satelliteBodyImage, 1f);

            if (_statusBannerText != null)
            {
                _statusBannerText.text = "ORBITAL INSERTION COMPLETE — SATELLITE ONLINE IN ORBIT";
            }

            OnLaunchSequenceCompleted?.Invoke();
        }

        // =====================================================================
        // ORBITAL TICK MOVEMENT (UpdateFlightTick)
        // =====================================================================

        private void PositionSatelliteAtOrbitProgress(float orbitProgress01, SimulationTickSnapshot tick)
        {
            if (!_satelliteRootRect.gameObject.activeSelf)
            {
                _rocketRootRect.gameObject.SetActive(false);
                _satelliteRootRect.gameObject.SetActive(true);
                _satelliteRootRect.localScale = Vector3.one;
            }

            Vector2 worldPos = GetWorldOrbitPoint(orbitProgress01);
            _satelliteRootRect.anchoredPosition = worldPos;

            // Slight orientation along orbit tangent
            float angleDeg = -orbitProgress01 * 360f;
            _satelliteRootRect.localEulerAngles = new Vector3(0f, 0f, angleDeg);

            if (tick != null && _statusBannerText != null && !_isLaunchPlaying)
            {
                string zone = tick.IsInEclipse
                    ? "ECLIPSE SHADOW"
                    : (tick.IsInCommWindow ? "COMM WINDOW (LINK ACTIVE)" : "SUNLIGHT");
                _statusBannerText.text = string.Format(
                    "ORBIT #{0}  |  Progress: {1:0}%  |  Zone: {2}  |  Bat: {3:0}%",
                    Mathf.Max(1, tick.OrbitNumber), orbitProgress01 * 100f, zone, tick.BatteryPercent);
            }
        }

        private Vector2 GetLocalOrbitPoint(float progress01, float radius)
        {
            // Progress 0 = Top (90 deg), moves clockwise around the planet
            float rad = (90f - progress01 * 360f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
        }

        private Vector2 GetWorldOrbitPoint(float progress01)
        {
            return _activeOrbitCenterPos + GetLocalOrbitPoint(progress01, _orbitRadiusPx);
        }

        private static Vector2 EvaluateCubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }

        private static Vector2 EvaluateCubicBezierTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        }

        private void AnimateStarfieldDrift()
        {
            if (_starfieldLayerA != null)
            {
                Vector2 offset = new Vector2(Mathf.Sin(Time.time * 0.15f) * 18f, Mathf.Cos(Time.time * 0.12f) * 12f);
                _starfieldLayerA.anchoredPosition = offset;
            }
            if (_starfieldImageB != null)
            {
                float twinkle = 0.35f + 0.30f * Mathf.Sin(Time.time * 2.2f);
                _starfieldImageB.color = new Color(0.8f, 0.9f, 1f, twinkle);
            }
        }

        // =====================================================================
        // SPRITE SELECTION & PROCEDURAL 64x64 PLACEHOLDERS (FilterMode.Point)
        // =====================================================================

        private Sprite SelectRocketSprite(RocketOption rocket)
        {
            string id = rocket != null && rocket.Id != null ? rocket.Id : string.Empty;
            if (id.IndexOf("heavy", StringComparison.OrdinalIgnoreCase) >= 0) return heavyLiftRocketSprite;
            if (id.IndexOf("medium", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("reusable", StringComparison.OrdinalIgnoreCase) >= 0) return reusableRocketSprite;
            return smallRocketSprite;
        }

        private static string GetRocketShortTag(RocketOption rocket)
        {
            string id = rocket != null && rocket.Id != null ? rocket.Id : string.Empty;
            if (id.IndexOf("heavy", StringComparison.OrdinalIgnoreCase) >= 0) return "HEAVY\nROCKET";
            if (id.IndexOf("medium", StringComparison.OrdinalIgnoreCase) >= 0) return "REUSABLE\nROCKET";
            return "SMALL\nROCKET";
        }

        private Sprite SelectSatelliteSprite(List<SatelliteComponent> components, OrbitDefinition orbit)
        {
            if (components != null)
            {
                for (int i = 0; i < components.Count; i++)
                {
                    var c = components[i];
                    if (c == null || c.Id == null) continue;
                    if (c.Id.IndexOf("deepspace", StringComparison.OrdinalIgnoreCase) >= 0) return deepSpaceProbeSprite;
                    if (c.Id.IndexOf("smallsat", StringComparison.OrdinalIgnoreCase) >= 0) return satelliteSprite;
                    if (c.Id.IndexOf("cubesat", StringComparison.OrdinalIgnoreCase) >= 0) return cubesatSprite;
                }
            }
            if (orbit != null && (orbit.Type == OrbitType.Mars || orbit.Type == OrbitType.DeepSpace))
                return deepSpaceProbeSprite;
            if (orbit != null && orbit.Type == OrbitType.Lunar)
                return satelliteSprite;
            return cubesatSprite;
        }

        private static string GetSatelliteShortTag(List<SatelliteComponent> components)
        {
            if (components != null)
            {
                for (int i = 0; i < components.Count; i++)
                {
                    var c = components[i];
                    if (c == null || c.Id == null) continue;
                    if (c.Id.IndexOf("deepspace", StringComparison.OrdinalIgnoreCase) >= 0) return "PROBE";
                    if (c.Id.IndexOf("smallsat", StringComparison.OrdinalIgnoreCase) >= 0) return "SAT";
                }
            }
            return "CUBESAT";
        }

        private static void AssignSpriteWithPointFilter(Image img, Sprite customSprite, Sprite fallbackSprite, Color tint)
        {
            if (img == null) return;
            Sprite chosen = customSprite != null ? customSprite : fallbackSprite;
            if (chosen != null && chosen.texture != null)
            {
                chosen.texture.filterMode = FilterMode.Point;
            }
            img.sprite = chosen;
            img.color = tint;
        }

        private static void SetPlaceholderVisible(Text label, bool visible, string textContent)
        {
            if (label == null) return;
            label.gameObject.SetActive(visible);
            if (visible) label.text = textContent;
        }

        private static void SetImageAlpha(Image img, float alpha)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = Mathf.Clamp01(alpha);
            img.color = c;
        }

        private static void SetTextAlpha(Text txt, float alpha)
        {
            if (txt == null) return;
            Color c = txt.color;
            c.a = Mathf.Clamp01(alpha);
            txt.color = c;
        }

        private static void EnsureProceduralSprites()
        {
            if (_procPlanetCircleSprite != null) return;

            _procStarfieldSprite = CreateStarfieldSprite(128, 128);
            _procPlanetCircleSprite = CreateCircleSprite(64, true);
            _procDashedRingSprite = CreateDashedRingSprite(128);
            _procSectorArcSprite = CreateRingArcBandSprite(128, 0.86f, 1.0f);
            _procEclipseConeSprite = CreateRingArcBandSprite(128, 0.42f, 1.0f);
            _procLaunchPadSprite = CreateLaunchPadSprite(32);
            _procRocketSprite = CreateRocketPlaceholderSprite(64);
            _procFlameSprite = CreateFlamePlaceholderSprite(32);
            _procSmokeSprite = CreateCircleSprite(32, false);
            _procSatelliteSprite = CreateSatellitePlaceholderSprite(64);
            _procDotSprite = CreateCircleSprite(16, false);
        }

        private static Sprite CreateStarfieldSprite(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[w * h];
            Color bg = new Color(0.02f, 0.04f, 0.09f, 1f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            int seed = 1337;
            for (int s = 0; s < 95; s++)
            {
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                int x = seed % w;
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                int y = seed % h;
                float b = 0.45f + (s % 5) * 0.12f;
                pixels[y * w + x] = new Color(b, b * 1.05f, 1f, 1f);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateCircleSprite(int size, bool withPixelGridShading)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float r = size * 0.46f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    if (dist <= r)
                    {
                        float shade = 1f;
                        if (withPixelGridShading)
                        {
                            if (dist > r - 2.2f) shade = 1.18f;
                            else if ((x + y) % 7 == 0) shade = 0.86f;
                        }
                        tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateDashedRingSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float outerR = size * 0.48f;
            float innerR = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - center;
                    float dist = d.magnitude;
                    if (dist >= innerR && dist <= outerR)
                    {
                        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 180f;
                        bool dashOn = ((int)(angle / 5f) % 2) == 0;
                        tex.SetPixel(x, y, dashOn ? Color.white : Color.clear);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRingArcBandSprite(int size, float innerRatio, float outerRatio)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float outerR = size * 0.49f * outerRatio;
            float innerR = size * 0.49f * innerRatio;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    tex.SetPixel(x, y, (dist >= innerR && dist <= outerR) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateLaunchPadSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool basePad = y < 8 && x >= 4 && x < size - 4;
                    bool tower = x >= 6 && x <= 10 && y >= 8 && y < size - 4;
                    tex.SetPixel(x, y, (basePad || tower) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRocketPlaceholderSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            int mid = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int halfWidth = y > size * 0.65f
                        ? Mathf.Max(1, (int)((size - y) * 0.45f))
                        : 8;
                    bool body = y >= 8 && y < size - 4 && Mathf.Abs(x - mid) <= halfWidth;
                    bool fins = y >= 8 && y <= 20 && Mathf.Abs(x - mid) <= 15;
                    tex.SetPixel(x, y, (body || fins) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateFlamePlaceholderSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            int mid = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int maxW = (int)((y / (float)size) * (size * 0.35f));
                    bool inside = y > 2 && Mathf.Abs(x - mid) <= maxW;
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 1.0f));
        }

        private static Sprite CreateSatellitePlaceholderSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            int mid = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool coreBus = Mathf.Abs(x - mid) <= 9 && Mathf.Abs(y - mid) <= 9;
                    bool solarWings = Mathf.Abs(y - mid) <= 5 && Mathf.Abs(x - mid) >= 11 && Mathf.Abs(x - mid) <= 27;
                    tex.SetPixel(x, y, (coreBus || solarWings) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // =====================================================================
        // HIERARCHY GENERATION & BINDING
        // =====================================================================

        public void EnsureSpaceMapHierarchy()
        {
            EnsureProceduralSprites();

            Transform existing = transform.Find("SpaceMapContentRoot");
            if (existing != null)
            {
                BindExistingHierarchy(existing);
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject rootObj = CreateUIObject("SpaceMapContentRoot", transform);
            _mapViewportRect = rootObj.GetComponent<RectTransform>();
            StretchFull(_mapViewportRect);
            rootObj.AddComponent<RectMask2D>();

            // 1. Starfield Background Layers
            GameObject starA = CreateUIObject("StarfieldBg_Main", rootObj.transform);
            _starfieldLayerA = starA.GetComponent<RectTransform>();
            StretchFull(_starfieldLayerA);
            _starfieldLayerA.sizeDelta = new Vector2(60f, 60f);
            _starfieldImageA = starA.AddComponent<Image>();
            _starfieldImageA.sprite = _procStarfieldSprite;

            GameObject starB = CreateUIObject("StarfieldBg_Twinkle", rootObj.transform);
            _starfieldLayerB = starB.GetComponent<RectTransform>();
            StretchFull(_starfieldLayerB);
            _starfieldImageB = starB.AddComponent<Image>();
            _starfieldImageB.sprite = _procStarfieldSprite;
            _starfieldImageB.color = new Color(0.8f, 0.9f, 1f, 0.45f);

            // 2. Bezier Transfer Trajectory Container (Earth -> Moon/Mars/Venus)
            GameObject bezierObj = CreateUIObject("BezierTransferTrajectory", rootObj.transform);
            _bezierContainerRect = bezierObj.GetComponent<RectTransform>();
            StretchFull(_bezierContainerRect);

            _bezierDotRects.Clear();
            for (int i = 0; i < BezierDotCount; i++)
            {
                GameObject dotObj = CreateUIObject("BezierDot_" + i, _bezierContainerRect);
                RectTransform dotRt = dotObj.GetComponent<RectTransform>();
                dotRt.sizeDelta = new Vector2(7f, 7f);
                Image dotImg = dotObj.AddComponent<Image>();
                dotImg.sprite = _procDotSprite;
                dotImg.color = new Color(0.65f, 0.86f, 1f, 0.65f);
                _bezierDotRects.Add(dotRt);
            }

            // 3. Orbit Container (Dashed Orbit Ring + Eclipse Cone + Comm Window Green Arc)
            GameObject orbitObj = CreateUIObject("OrbitRingContainer", rootObj.transform);
            _orbitContainerRect = orbitObj.GetComponent<RectTransform>();
            _orbitContainerRect.sizeDelta = new Vector2(480f, 480f);

            // Eclipse Shadow Cone
            GameObject eclipseObj = CreateUIObject("EclipseShadowSector", _orbitContainerRect);
            StretchFull(eclipseObj.GetComponent<RectTransform>());
            _eclipseSectorImage = eclipseObj.AddComponent<Image>();
            _eclipseSectorImage.sprite = _procEclipseConeSprite;
            _eclipseSectorImage.color = new Color(0.02f, 0.03f, 0.09f, 0.68f);
            _eclipseSectorImage.type = Image.Type.Filled;
            _eclipseSectorImage.fillMethod = Image.FillMethod.Radial360;
            _eclipseSectorImage.fillOrigin = (int)Image.Origin360.Top;
            _eclipseSectorImage.fillClockwise = false;
            _eclipseSectorImage.fillAmount = 0.35f;

            // Comm Window Green Arc
            GameObject commObj = CreateUIObject("CommWindowArc", _orbitContainerRect);
            StretchFull(commObj.GetComponent<RectTransform>());
            _commSectorImage = commObj.AddComponent<Image>();
            _commSectorImage.sprite = _procSectorArcSprite;
            _commSectorImage.color = new Color(0.20f, 0.95f, 0.55f, 0.72f);
            _commSectorImage.type = Image.Type.Filled;
            _commSectorImage.fillMethod = Image.FillMethod.Radial360;
            _commSectorImage.fillOrigin = (int)Image.Origin360.Top;
            _commSectorImage.fillClockwise = true;
            _commSectorImage.fillAmount = 0.25f;

            // Thin Dashed Orbit Line
            GameObject ringObj = CreateUIObject("DashedOrbitLine", _orbitContainerRect);
            StretchFull(ringObj.GetComponent<RectTransform>());
            _orbitRingImage = ringObj.AddComponent<Image>();
            _orbitRingImage.sprite = _procDashedRingSprite;
            _orbitRingImage.color = new Color(0.75f, 0.88f, 1f, 0.75f);

            _commTagText = CreateText("CommSectorTag", _orbitContainerRect, font,
                "COMM WINDOW", 11, FontStyle.Bold, new Color(0.35f, 1f, 0.65f, 0.9f), TextAnchor.MiddleCenter);
            _commTagText.rectTransform.sizeDelta = new Vector2(120f, 22f);

            _eclipseTagText = CreateText("EclipseSectorTag", _orbitContainerRect, font,
                "ECLIPSE SHADOW", 11, FontStyle.Bold, new Color(0.62f, 0.75f, 1f, 0.85f), TextAnchor.MiddleCenter);
            _eclipseTagText.rectTransform.sizeDelta = new Vector2(130f, 22f);

            // 4. Earth + Launch Pad
            GameObject earthObj = CreateUIObject("EarthPlanet", rootObj.transform);
            _earthRect = earthObj.GetComponent<RectTransform>();
            _earthRect.sizeDelta = new Vector2(210f, 210f);
            _earthImage = earthObj.AddComponent<Image>();
            _earthImage.preserveAspect = true;
            _earthPlaceholderLabel = CreateText("EarthPlaceholderTag", earthObj.transform, font,
                "EARTH\n(earth.png)", 14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(_earthPlaceholderLabel.rectTransform);

            GameObject padObj = CreateUIObject("LaunchPad", rootObj.transform);
            _launchPadRect = padObj.GetComponent<RectTransform>();
            _launchPadRect.sizeDelta = new Vector2(38f, 38f);
            _launchPadImage = padObj.AddComponent<Image>();
            _launchPadImage.preserveAspect = true;
            _launchPadPlaceholderLabel = CreateText("PadPlaceholderTag", padObj.transform, font,
                "PAD", 10, FontStyle.Bold, new Color(1f, 0.9f, 0.4f, 1f), TextAnchor.MiddleCenter);
            StretchFull(_launchPadPlaceholderLabel.rectTransform);

            // 5. Target Planet (Moon / Mars / Venus)
            GameObject targetPlanetObj = CreateUIObject("TargetPlanet", rootObj.transform);
            _targetPlanetRect = targetPlanetObj.GetComponent<RectTransform>();
            _targetPlanetRect.sizeDelta = new Vector2(170f, 170f);
            _targetPlanetImage = targetPlanetObj.AddComponent<Image>();
            _targetPlanetImage.preserveAspect = true;
            _targetPlanetPlaceholderLabel = CreateText("TargetPlaceholderTag", targetPlanetObj.transform, font,
                "MOON / MARS", 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(_targetPlanetPlaceholderLabel.rectTransform);

            // 6. Launch Smoke Puffs (3)
            for (int i = 0; i < 3; i++)
            {
                GameObject smokeObj = CreateUIObject("LaunchSmoke_" + i, rootObj.transform);
                _smokeRects[i] = smokeObj.GetComponent<RectTransform>();
                _smokeRects[i].sizeDelta = new Vector2(36f, 36f);
                _smokeImages[i] = smokeObj.AddComponent<Image>();
                _smokeImages[i].sprite = _procSmokeSprite;
                smokeObj.SetActive(false);
            }

            // 7. Rocket + Nozzle Flame + Mars Aerobraking Glow
            GameObject rocketObj = CreateUIObject("RocketVehicle", rootObj.transform);
            _rocketRootRect = rocketObj.GetComponent<RectTransform>();
            _rocketRootRect.sizeDelta = new Vector2(54f, 54f);

            GameObject aeroGlowObj = CreateUIObject("MarsAerobrakeGlow", rocketObj.transform);
            RectTransform aeroRt = aeroGlowObj.GetComponent<RectTransform>();
            aeroRt.sizeDelta = new Vector2(76f, 76f);
            _aerobrakeGlowImage = aeroGlowObj.AddComponent<Image>();
            _aerobrakeGlowImage.sprite = _procPlanetCircleSprite;
            _aerobrakeGlowImage.color = new Color(1f, 0.45f, 0.1f, 0.8f);
            aeroGlowObj.SetActive(false);

            GameObject flameObj = CreateUIObject("RocketFlame", rocketObj.transform);
            _rocketFlameRect = flameObj.GetComponent<RectTransform>();
            _rocketFlameRect.anchorMin = new Vector2(0.5f, 0f);
            _rocketFlameRect.anchorMax = new Vector2(0.5f, 0f);
            _rocketFlameRect.pivot = new Vector2(0.5f, 1f);
            _rocketFlameRect.anchoredPosition = new Vector2(0f, 4f);
            _rocketFlameRect.sizeDelta = new Vector2(26f, 34f);
            _rocketFlameImage = flameObj.AddComponent<Image>();
            _rocketFlameImage.preserveAspect = true;

            _rocketBodyImage = rocketObj.AddComponent<Image>();
            _rocketBodyImage.preserveAspect = true;
            _rocketPlaceholderLabel = CreateText("RocketPlaceholderTag", rocketObj.transform, font,
                "ROCKET", 9, FontStyle.Bold, new Color(0.1f, 0.15f, 0.25f, 1f), TextAnchor.MiddleCenter);
            StretchFull(_rocketPlaceholderLabel.rectTransform);

            // 8. Player Satellite + Separation Flash
            GameObject flashObj = CreateUIObject("SeparationFlash", rootObj.transform);
            flashObj.GetComponent<RectTransform>().sizeDelta = new Vector2(68f, 68f);
            _deployFlashImage = flashObj.AddComponent<Image>();
            _deployFlashImage.sprite = _procPlanetCircleSprite;
            flashObj.SetActive(false);

            GameObject satObj = CreateUIObject("PlayerSatellite", rootObj.transform);
            _satelliteRootRect = satObj.GetComponent<RectTransform>();
            _satelliteRootRect.sizeDelta = new Vector2(52f, 52f);
            _satelliteBodyImage = satObj.AddComponent<Image>();
            _satelliteBodyImage.preserveAspect = true;
            _satellitePlaceholderLabel = CreateText("SatellitePlaceholderTag", satObj.transform, font,
                "CUBESAT", 9, FontStyle.Bold, new Color(0.05f, 0.1f, 0.2f, 1f), TextAnchor.MiddleCenter);
            StretchFull(_satellitePlaceholderLabel.rectTransform);
            satObj.SetActive(false);

            // 9. Top Status & Launch Controls Bar (2x Speed / Skip Launch)
            _launchControlsPanel = CreateUIObject("LaunchControlsBar", rootObj.transform);
            RectTransform lcRt = _launchControlsPanel.GetComponent<RectTransform>();
            lcRt.anchorMin = new Vector2(0f, 1f);
            lcRt.anchorMax = new Vector2(1f, 1f);
            lcRt.pivot = new Vector2(0.5f, 1f);
            lcRt.anchoredPosition = new Vector2(0f, -10f);
            lcRt.sizeDelta = new Vector2(-32f, 46f);

            Image lcBg = _launchControlsPanel.AddComponent<Image>();
            lcBg.color = new Color(0.05f, 0.10f, 0.18f, 0.90f);

            HorizontalLayoutGroup lcLayout = _launchControlsPanel.AddComponent<HorizontalLayoutGroup>();
            lcLayout.padding = new RectOffset(14, 14, 6, 6);
            lcLayout.spacing = 10f;
            lcLayout.childAlignment = TextAnchor.MiddleLeft;
            lcLayout.childControlWidth = true;
            lcLayout.childControlHeight = true;
            lcLayout.childForceExpandWidth = false;

            _statusBannerText = CreateText("StatusBannerText", _launchControlsPanel.transform, font,
                "SPACE MAP READY", 14, FontStyle.Bold, new Color(0.88f, 0.95f, 1f, 1f), TextAnchor.MiddleLeft);
            LayoutElement sbLe = _statusBannerText.gameObject.AddComponent<LayoutElement>();
            sbLe.flexibleWidth = 1f;

            Button speedBtn = CreateButton(_launchControlsPanel.transform, "Btn_Speed2x", "SPEED: 1x -> 2x",
                new Color(0.18f, 0.36f, 0.58f, 1f), 145f, 34f, font, ToggleLaunchSpeed2x);
            _speedButtonText = speedBtn.GetComponentInChildren<Text>();

            CreateButton(_launchControlsPanel.transform, "Btn_SkipLaunch", "SKIP LAUNCH >>",
                new Color(0.52f, 0.32f, 0.14f, 1f), 145f, 34f, font, SkipLaunchSequence);

            // 10. Bottom Sandbox Test Bar (for testing LEO / Moon / Mars / Venus in _Sandbox)
            _sandboxToolbarObj = CreateUIObject("SandboxTestBar", rootObj.transform);
            RectTransform stbRt = _sandboxToolbarObj.GetComponent<RectTransform>();
            stbRt.anchorMin = new Vector2(0f, 0f);
            stbRt.anchorMax = new Vector2(1f, 0f);
            stbRt.pivot = new Vector2(0.5f, 0f);
            stbRt.anchoredPosition = new Vector2(0f, 10f);
            stbRt.sizeDelta = new Vector2(-32f, 44f);

            Image stbBg = _sandboxToolbarObj.AddComponent<Image>();
            stbBg.color = new Color(0.05f, 0.09f, 0.16f, 0.88f);

            HorizontalLayoutGroup stbLayout = _sandboxToolbarObj.AddComponent<HorizontalLayoutGroup>();
            stbLayout.padding = new RectOffset(12, 12, 5, 5);
            stbLayout.spacing = 8f;
            stbLayout.childAlignment = TextAnchor.MiddleCenter;
            stbLayout.childControlWidth = true;
            stbLayout.childControlHeight = true;
            stbLayout.childForceExpandWidth = true;

            CreateButton(_sandboxToolbarObj.transform, "Btn_PresetLEO", "1. LEO (Earth)",
                new Color(0.16f, 0.32f, 0.52f, 1f), 130f, 32f, font, () => SetupDemoPreset(0));
            CreateButton(_sandboxToolbarObj.transform, "Btn_PresetMoon", "2. Moon (2 Planets)",
                new Color(0.16f, 0.32f, 0.52f, 1f), 150f, 32f, font, () => SetupDemoPreset(1));
            CreateButton(_sandboxToolbarObj.transform, "Btn_PresetMars", "3. Mars (Aerobrake)",
                new Color(0.48f, 0.24f, 0.16f, 1f), 150f, 32f, font, () => SetupDemoPreset(2));
            CreateButton(_sandboxToolbarObj.transform, "Btn_PresetVenus", "4. Venus",
                new Color(0.42f, 0.34f, 0.14f, 1f), 110f, 32f, font, () => SetupDemoPreset(3));
            CreateButton(_sandboxToolbarObj.transform, "Btn_PlayLaunch", "▶ PLAY LAUNCH + ORBIT",
                new Color(0.14f, 0.52f, 0.34f, 1f), 190f, 32f, font, StartDemoLaunchAndOrbit);

            _sandboxToolbarObj.SetActive(showSandboxTestBar);
        }

        private void BindExistingHierarchy(Transform root)
        {
            _mapViewportRect = root.GetComponent<RectTransform>();
            _starfieldLayerA = FindChild<RectTransform>(root, "StarfieldBg_Main");
            _starfieldLayerB = FindChild<RectTransform>(root, "StarfieldBg_Twinkle");
            _starfieldImageA = FindChild<Image>(root, "StarfieldBg_Main");
            _starfieldImageB = FindChild<Image>(root, "StarfieldBg_Twinkle");

            _bezierContainerRect = FindChild<RectTransform>(root, "BezierTransferTrajectory");
            _bezierDotRects.Clear();
            if (_bezierContainerRect != null)
            {
                for (int i = 0; i < BezierDotCount; i++)
                {
                    RectTransform d = FindChild<RectTransform>(_bezierContainerRect, "BezierDot_" + i);
                    if (d != null) _bezierDotRects.Add(d);
                }
            }

            _orbitContainerRect = FindChild<RectTransform>(root, "OrbitRingContainer");
            if (_orbitContainerRect != null)
            {
                _eclipseSectorImage = FindChild<Image>(_orbitContainerRect, "EclipseShadowSector");
                _commSectorImage = FindChild<Image>(_orbitContainerRect, "CommWindowArc");
                _orbitRingImage = FindChild<Image>(_orbitContainerRect, "DashedOrbitLine");
                _commTagText = FindChild<Text>(_orbitContainerRect, "CommSectorTag");
                _eclipseTagText = FindChild<Text>(_orbitContainerRect, "EclipseSectorTag");
            }

            _earthRect = FindChild<RectTransform>(root, "EarthPlanet");
            _earthImage = FindChild<Image>(root, "EarthPlanet");
            _earthPlaceholderLabel = FindChild<Text>(root, "EarthPlanet/EarthPlaceholderTag");

            _launchPadRect = FindChild<RectTransform>(root, "LaunchPad");
            _launchPadImage = FindChild<Image>(root, "LaunchPad");
            _launchPadPlaceholderLabel = FindChild<Text>(root, "LaunchPad/PadPlaceholderTag");

            _targetPlanetRect = FindChild<RectTransform>(root, "TargetPlanet");
            _targetPlanetImage = FindChild<Image>(root, "TargetPlanet");
            _targetPlanetPlaceholderLabel = FindChild<Text>(root, "TargetPlanet/TargetPlaceholderTag");

            for (int i = 0; i < 3; i++)
            {
                _smokeRects[i] = FindChild<RectTransform>(root, "LaunchSmoke_" + i);
                _smokeImages[i] = FindChild<Image>(root, "LaunchSmoke_" + i);
            }

            _rocketRootRect = FindChild<RectTransform>(root, "RocketVehicle");
            _rocketBodyImage = FindChild<Image>(root, "RocketVehicle");
            _rocketPlaceholderLabel = FindChild<Text>(root, "RocketVehicle/RocketPlaceholderTag");
            _rocketFlameRect = FindChild<RectTransform>(root, "RocketVehicle/RocketFlame");
            _rocketFlameImage = FindChild<Image>(root, "RocketVehicle/RocketFlame");
            _aerobrakeGlowImage = FindChild<Image>(root, "RocketVehicle/MarsAerobrakeGlow");

            _deployFlashImage = FindChild<Image>(root, "SeparationFlash");
            _satelliteRootRect = FindChild<RectTransform>(root, "PlayerSatellite");
            _satelliteBodyImage = FindChild<Image>(root, "PlayerSatellite");
            _satellitePlaceholderLabel = FindChild<Text>(root, "PlayerSatellite/SatellitePlaceholderTag");

            Transform lc = root.Find("LaunchControlsBar");
            if (lc != null)
            {
                _launchControlsPanel = lc.gameObject;
                _statusBannerText = FindChild<Text>(lc, "StatusBannerText");

                Button spdBtn = FindChild<Button>(lc, "Btn_Speed2x");
                if (spdBtn != null)
                {
                    spdBtn.onClick.RemoveAllListeners();
                    spdBtn.onClick.AddListener(ToggleLaunchSpeed2x);
                    _speedButtonText = spdBtn.GetComponentInChildren<Text>();
                }

                Button skpBtn = FindChild<Button>(lc, "Btn_SkipLaunch");
                if (skpBtn != null)
                {
                    skpBtn.onClick.RemoveAllListeners();
                    skpBtn.onClick.AddListener(SkipLaunchSequence);
                }
            }

            Transform stb = root.Find("SandboxTestBar");
            if (stb != null)
            {
                _sandboxToolbarObj = stb.gameObject;
                _sandboxToolbarObj.SetActive(showSandboxTestBar);

                WireButton(stb, "Btn_PresetLEO", () => SetupDemoPreset(0));
                WireButton(stb, "Btn_PresetMoon", () => SetupDemoPreset(1));
                WireButton(stb, "Btn_PresetMars", () => SetupDemoPreset(2));
                WireButton(stb, "Btn_PresetVenus", () => SetupDemoPreset(3));
                WireButton(stb, "Btn_PlayLaunch", StartDemoLaunchAndOrbit);
            }
        }

        private static void WireButton(Transform parent, string path, UnityEngine.Events.UnityAction action)
        {
            Button b = FindChild<Button>(parent, path);
            if (b != null)
            {
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(action);
            }
        }

        private static OrbitDefinition CreateFallbackOrbit(OrbitType type, string id, string name, float eclipse, float comm)
        {
            return new OrbitDefinition
            {
                Id = id,
                Name = name,
                Type = type,
                OrbitPeriodHours = 1.5f,
                EclipseFraction = eclipse,
                CommWindowFraction = comm,
                SolarFluxFactor = 1.0f,
                SignalDelaySeconds = 1.0f,
                BaseRadiation = 10.0f
            };
        }

        private static T FindChild<T>(Transform root, string path) where T : Component
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int size, FontStyle style, Color color, TextAnchor align)
        {
            GameObject go = CreateUIObject(name, parent);
            Text txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color bgColor, float width, float height, Font font, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            if (width > 0f)
            {
                le.minWidth = width;
                le.preferredWidth = width;
            }
            if (height > 0f)
            {
                le.minHeight = height;
                le.preferredHeight = height;
            }

            Text txt = CreateText("Text", btnObj.transform, font, label, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }
    }
}
