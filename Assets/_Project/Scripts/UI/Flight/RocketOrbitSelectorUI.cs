using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AstraLink.Core;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// Rocket &amp; Orbit Selector UI Controller (Task S1-08).
    /// Displays 3 launch vehicle cards with payload mass limits per orbit and 3 target orbit cards (LEO, Moon, Mars).
    /// Exposes a clean <see cref="Initialize(IMissionSimulator, List{RocketOption}, List{OrbitDefinition}, OrbitRadarUI, List{SatelliteComponent}, MissionGoal)"/>
    /// entry point and C# events (<see cref="OnRocketSelected"/>, <see cref="OnOrbitSelected"/>) for MainScene integration.
    /// </summary>
    public class RocketOrbitSelectorUI : MonoBehaviour
    {
        [Serializable]
        private class RocketListWrapper
        {
            public List<RocketOption> rockets;
        }

        [Serializable]
        private class OrbitListWrapper
        {
            public List<OrbitDefinition> orbits;
        }

        [Header("Linked 2D Radar (Optional — auto-discovered if in same prefab/panel)")]
        [SerializeField] private OrbitRadarUI linkedOrbitRadar;

        [Header("UI Containers (Auto-bound if null)")]
        [SerializeField] private RectTransform rocketsContainer;
        [SerializeField] private RectTransform orbitsContainer;
        [SerializeField] private Text summaryStatusLabel;

        private IMissionSimulator _simulator;
        private List<RocketOption> _rockets = new List<RocketOption>();
        private List<OrbitDefinition> _orbits = new List<OrbitDefinition>();
        private List<SatelliteComponent> _components = new List<SatelliteComponent>();
        private MissionGoal _goal;

        private RocketOption _selectedRocket;
        private OrbitDefinition _selectedOrbit;

        private readonly List<Button> _rocketButtons = new List<Button>();
        private readonly List<Text> _rocketCardLabels = new List<Text>();
        private readonly List<Button> _orbitButtons = new List<Button>();
        private readonly List<Text> _orbitCardLabels = new List<Text>();

        /// <summary>
        /// Fired when the player selects a launch vehicle card.
        /// </summary>
        public event Action<RocketOption> OnRocketSelected;

        /// <summary>
        /// Fired when the player selects a target orbit card.
        /// </summary>
        public event Action<OrbitDefinition> OnOrbitSelected;

        /// <summary>
        /// Gets the currently selected <see cref="RocketOption"/>.
        /// </summary>
        public RocketOption SelectedRocket => _selectedRocket;

        /// <summary>
        /// Gets the currently selected <see cref="OrbitDefinition"/>.
        /// </summary>
        public OrbitDefinition SelectedOrbit => _selectedOrbit;

        /// <summary>
        /// Gets or sets the linked <see cref="OrbitRadarUI"/> that updates automatically when the selection changes.
        /// </summary>
        public OrbitRadarUI LinkedOrbitRadar
        {
            get => linkedOrbitRadar;
            set => linkedOrbitRadar = value;
        }

        private void Awake()
        {
            EnsureSelectorHierarchy();
        }

        private void Start()
        {
            if (_rockets == null || _rockets.Count == 0 || _orbits == null || _orbits.Count == 0)
            {
                IMissionSimulator sim = _simulator;
                if (sim == null)
                {
                    sim = (GameManager.Instance != null && GameManager.Instance.Simulator != null)
                        ? GameManager.Instance.Simulator
                        : new MockMissionSimulator();
                }

                Initialize(sim, null, null, linkedOrbitRadar, _components, _goal);
            }
        }

        /// <summary>
        /// Initializes the selector using an <see cref="IGameDataRepository"/> and <see cref="IMissionSimulator"/>.
        /// </summary>
        public void Initialize(
            IGameDataRepository repository,
            IMissionSimulator simulator,
            OrbitRadarUI radar = null,
            List<SatelliteComponent> components = null,
            MissionGoal goal = null)
        {
            List<RocketOption> rockets = repository != null ? repository.GetRockets() : null;
            List<OrbitDefinition> orbits = repository != null ? repository.GetOrbits() : null;
            Initialize(simulator, rockets, orbits, radar, components, goal);
        }

        /// <summary>
        /// Primary entry point used to initialize the Rocket &amp; Orbit selector prefab in MainScene.
        /// </summary>
        public void Initialize(
            IMissionSimulator simulator,
            List<RocketOption> rockets = null,
            List<OrbitDefinition> orbits = null,
            OrbitRadarUI radar = null,
            List<SatelliteComponent> components = null,
            MissionGoal goal = null)
        {
            EnsureSelectorHierarchy();

            if (simulator == null)
            {
                Debug.LogWarning("[RocketOrbitSelectorUI] Initialize received null simulator; using MockMissionSimulator.");
                simulator = new MockMissionSimulator();
            }

            _simulator = simulator;
            _components = components ?? new List<SatelliteComponent>();
            _goal = goal;

            if (radar != null)
            {
                linkedOrbitRadar = radar;
            }
            else if (linkedOrbitRadar == null && transform.parent != null)
            {
                linkedOrbitRadar = transform.parent.GetComponentInChildren<OrbitRadarUI>(true);
            }

            _rockets = (rockets != null && rockets.Count > 0) ? rockets : LoadOrCreateDefaultRockets();
            _orbits = (orbits != null && orbits.Count > 0) ? orbits : LoadOrCreateDefaultOrbits();

            _selectedOrbit = _orbits[0];
            _selectedRocket = _rockets[0];

            RebuildOrbitCardsUI();
            RebuildRocketCardsUI();
            RefreshSelectionVisuals();

            if (linkedOrbitRadar != null)
            {
                linkedOrbitRadar.Initialize(_simulator, _selectedOrbit, _selectedRocket, _goal, _components, null);
            }

            Debug.Log(string.Format(
                "[RocketOrbitSelectorUI] Initialized with {0} rockets and {1} orbits.",
                _rockets.Count,
                _orbits.Count));
        }

        /// <summary>
        /// Selects a launch vehicle by its index in the loaded rocket list and notifies listeners.
        /// </summary>
        public void SelectRocketByIndex(int index)
        {
            if (_rockets == null || index < 0 || index >= _rockets.Count)
            {
                Debug.LogWarning("[RocketOrbitSelectorUI] Invalid rocket index: " + index);
                return;
            }

            _selectedRocket = _rockets[index];
            RefreshSelectionVisuals();

            if (linkedOrbitRadar != null)
            {
                linkedOrbitRadar.SetRocket(_selectedRocket);
            }

            OnRocketSelected?.Invoke(_selectedRocket);
        }

        /// <summary>
        /// Selects a target orbit by its index in the loaded orbit list, updates rocket mass limits, and updates the 2D radar.
        /// </summary>
        public void SelectOrbitByIndex(int index)
        {
            if (_orbits == null || index < 0 || index >= _orbits.Count)
            {
                Debug.LogWarning("[RocketOrbitSelectorUI] Invalid orbit index: " + index);
                return;
            }

            _selectedOrbit = _orbits[index];
            RefreshSelectionVisuals();

            if (linkedOrbitRadar != null)
            {
                linkedOrbitRadar.SetOrbit(_selectedOrbit);
            }

            OnOrbitSelected?.Invoke(_selectedOrbit);
        }

        /// <summary>
        /// Returns the maximum payload mass (in kg) that the given rocket can deliver to the specified orbit.
        /// </summary>
        public static float GetMaxMassForOrbit(RocketOption rocket, OrbitDefinition orbit)
        {
            if (rocket == null) return 0f;
            if (orbit == null) return rocket.MaxMassLeoKg;

            switch (orbit.Type)
            {
                case OrbitType.LowEarth:
                    return rocket.MaxMassLeoKg;
                case OrbitType.Lunar:
                    return rocket.MaxMassLunarKg;
                case OrbitType.Mars:
                    return rocket.MaxMassMarsKg;
                case OrbitType.DeepSpace:
                    return rocket.MaxMassDeepSpaceKg;
                default:
                    return rocket.MaxMassLeoKg;
            }
        }

        private void RefreshSelectionVisuals()
        {
            float payloadMassKg = 15.0f;
            if (_simulator != null)
            {
                ResourceSummary res = _simulator.CalculateResources(_components, _selectedRocket, _selectedOrbit, _goal);
                if (res != null && res.TotalMassKg > 0f)
                {
                    payloadMassKg = res.TotalMassKg;
                }
            }

            // Refresh Orbit Cards
            for (int i = 0; i < _orbits.Count && i < _orbitButtons.Count; i++)
            {
                OrbitDefinition orb = _orbits[i];
                bool isSelected = (_selectedOrbit != null && orb.Id == _selectedOrbit.Id);

                Image img = _orbitButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = isSelected
                        ? new Color(0.12f, 0.48f, 0.76f, 1.0f)
                        : new Color(0.09f, 0.15f, 0.25f, 0.96f);
                }

                if (i < _orbitCardLabels.Count && _orbitCardLabels[i] != null)
                {
                    string marker = isSelected ? "● [SELECTED] " : "○ ";
                    float sunlightFrac = 1.0f - orb.EclipseFraction;
                    _orbitCardLabels[i].text = string.Format(
                        "{0}{1}\nSunlight: {2:0}% (u < {3:0.00})  |  Shadow: {4:0}% (u >= {3:0.00})  |  Comm: {5:0}% (u < {6:0.00})  |  Delay: {7:0.#}s",
                        marker,
                        orb.Name,
                        sunlightFrac * 100f,
                        sunlightFrac,
                        orb.EclipseFraction * 100f,
                        orb.CommWindowFraction * 100f,
                        orb.CommWindowFraction,
                        orb.SignalDelaySeconds);
                }
            }

            // Refresh Rocket Cards with dynamic mass limits for the selected orbit
            for (int i = 0; i < _rockets.Count && i < _rocketButtons.Count; i++)
            {
                RocketOption rkt = _rockets[i];
                bool isSelected = (_selectedRocket != null && rkt.Id == _selectedRocket.Id);
                float maxMassCurrentOrbit = GetMaxMassForOrbit(rkt, _selectedOrbit);
                bool canCarry = payloadMassKg <= maxMassCurrentOrbit;

                Image img = _rocketButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    if (isSelected)
                    {
                        img.color = canCarry
                            ? new Color(0.14f, 0.52f, 0.38f, 1.0f)
                            : new Color(0.62f, 0.22f, 0.22f, 1.0f);
                    }
                    else
                    {
                        img.color = new Color(0.09f, 0.15f, 0.25f, 0.96f);
                    }
                }

                if (i < _rocketCardLabels.Count && _rocketCardLabels[i] != null)
                {
                    string marker = isSelected ? "▲ [SELECTED] " : "△ ";
                    string orbitTag = GetShortOrbitTag(_selectedOrbit);
                    string capacityStatus = canCarry ? "VALID" : "OVERLOAD!";

                    _rocketCardLabels[i].text = string.Format(
                        "{0}{1}   —   ${2:0}k   |   Reliability: {3:0}%\n" +
                        "Mass Limit ({4}): up to {5:0} kg [{6}]   (LEO: {7:0} kg | Moon: {8:0} kg | Mars: {9:0} kg)",
                        marker,
                        rkt.Name,
                        rkt.CostThousands,
                        rkt.BaseReliability * 100f,
                        orbitTag,
                        maxMassCurrentOrbit,
                        capacityStatus,
                        rkt.MaxMassLeoKg,
                        rkt.MaxMassLunarKg,
                        rkt.MaxMassMarsKg);
                }
            }

            if (summaryStatusLabel != null && _selectedRocket != null && _selectedOrbit != null)
            {
                float maxMass = GetMaxMassForOrbit(_selectedRocket, _selectedOrbit);
                bool massOk = payloadMassKg <= maxMass;
                summaryStatusLabel.text = string.Format(
                    "Launch Config: {0}  ->  {1}  |  Payload Mass (Mock): {2:0.0} / {3:0.0} kg ({4})",
                    _selectedRocket.Name,
                    _selectedOrbit.Name,
                    payloadMassKg,
                    maxMass,
                    massOk ? "READY" : "MASS LIMIT EXCEEDED!");
                summaryStatusLabel.color = massOk
                    ? new Color(0.52f, 0.96f, 0.72f, 1.0f)
                    : new Color(1.0f, 0.48f, 0.48f, 1.0f);
            }
        }

        private static string GetShortOrbitTag(OrbitDefinition orbit)
        {
            if (orbit == null) return "LEO";
            switch (orbit.Type)
            {
                case OrbitType.LowEarth: return "LEO";
                case OrbitType.Lunar: return "Moon";
                case OrbitType.Mars: return "Mars";
                case OrbitType.DeepSpace: return "Deep Space";
                default: return "LEO";
            }
        }

        private void RebuildOrbitCardsUI()
        {
            if (orbitsContainer == null) return;

            ClearChildren(orbitsContainer);
            _orbitButtons.Clear();
            _orbitCardLabels.Clear();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int count = _orbits.Count;

            for (int i = 0; i < count; i++)
            {
                int capturedIndex = i;
                float step = 1.0f / Mathf.Max(1, count);
                float minY = 1.0f - (i + 1) * step + 0.015f;
                float maxY = 1.0f - i * step - 0.015f;

                Text cardText;
                Button btn = CreateCardButton(
                    orbitsContainer,
                    "OrbitCard_" + _orbits[i].Id,
                    new Vector2(0.0f, Mathf.Clamp01(minY)),
                    new Vector2(1.0f, Mathf.Clamp01(maxY)),
                    font,
                    out cardText);

                btn.onClick.AddListener(() => SelectOrbitByIndex(capturedIndex));
                _orbitButtons.Add(btn);
                _orbitCardLabels.Add(cardText);
            }
        }

        private void RebuildRocketCardsUI()
        {
            if (rocketsContainer == null) return;

            ClearChildren(rocketsContainer);
            _rocketButtons.Clear();
            _rocketCardLabels.Clear();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int count = _rockets.Count;

            for (int i = 0; i < count; i++)
            {
                int capturedIndex = i;
                float step = 1.0f / Mathf.Max(1, count);
                float minY = 1.0f - (i + 1) * step + 0.015f;
                float maxY = 1.0f - i * step - 0.015f;

                Text cardText;
                Button btn = CreateCardButton(
                    rocketsContainer,
                    "RocketCard_" + _rockets[i].Id,
                    new Vector2(0.0f, Mathf.Clamp01(minY)),
                    new Vector2(1.0f, Mathf.Clamp01(maxY)),
                    font,
                    out cardText);

                btn.onClick.AddListener(() => SelectRocketByIndex(capturedIndex));
                _rocketButtons.Add(btn);
                _rocketCardLabels.Add(cardText);
            }
        }

        /// <summary>
        /// Builds or binds the UI hierarchy for the rocket and orbit selector panel.
        /// </summary>
        public void EnsureSelectorHierarchy()
        {
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
            {
                rootRt = gameObject.AddComponent<RectTransform>();
            }

            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.09f, 0.16f, 0.96f);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Section 1: Orbits Header & Container
            EnsureLabel(
                rootRt,
                "OrbitsHeader",
                new Vector2(0.04f, 0.91f),
                new Vector2(0.96f, 0.98f),
                "1. SELECT TARGET ORBIT (LEO / MOON / MARS)",
                20,
                FontStyle.Bold,
                new Color(0.88f, 0.95f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft,
                font);

            if (orbitsContainer == null)
            {
                orbitsContainer = FindOrCreateContainer(
                    rootRt,
                    "OrbitsContainer",
                    new Vector2(0.04f, 0.56f),
                    new Vector2(0.96f, 0.90f));
            }

            // Section 2: Rockets Header & Container
            EnsureLabel(
                rootRt,
                "RocketsHeader",
                new Vector2(0.04f, 0.48f),
                new Vector2(0.96f, 0.55f),
                "2. SELECT LAUNCH VEHICLE (3 ROCKETS WITH MASS LIMITS)",
                20,
                FontStyle.Bold,
                new Color(0.88f, 0.95f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft,
                font);

            if (rocketsContainer == null)
            {
                rocketsContainer = FindOrCreateContainer(
                    rootRt,
                    "RocketsContainer",
                    new Vector2(0.04f, 0.10f),
                    new Vector2(0.96f, 0.47f));
            }

            // Bottom status bar
            if (summaryStatusLabel == null)
            {
                summaryStatusLabel = EnsureLabel(
                    rootRt,
                    "SummaryStatusLabel",
                    new Vector2(0.04f, 0.02f),
                    new Vector2(0.96f, 0.09f),
                    "Select a target orbit and launch vehicle.",
                    15,
                    FontStyle.Bold,
                    new Color(0.52f, 0.96f, 0.72f, 1.0f),
                    TextAnchor.MiddleLeft,
                    font);
            }
        }

        private static List<RocketOption> LoadOrCreateDefaultRockets()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/rockets");
            if (jsonAsset != null && !string.IsNullOrEmpty(jsonAsset.text))
            {
                try
                {
                    RocketListWrapper wrapper = JsonUtility.FromJson<RocketListWrapper>(jsonAsset.text);
                    if (wrapper != null && wrapper.rockets != null && wrapper.rockets.Count > 0)
                    {
                        return wrapper.rockets;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[RocketOrbitSelectorUI] Could not parse Data/rockets via JsonUtility: " + ex.Message);
                }
            }

            // Fallback rocket specifications matching the project Data Contract for Sprint 1 mock testing
            return new List<RocketOption>
            {
                new RocketOption
                {
                    Id = "rocket_light",
                    Name = "Light Rocket 'Arrow-1'",
                    CostThousands = 30.0f,
                    MaxMassLeoKg = 25.0f,
                    MaxMassLunarKg = 8.0f,
                    MaxMassMarsKg = 2.0f,
                    MaxMassDeepSpaceKg = 2.0f,
                    BaseReliability = 0.95f
                },
                new RocketOption
                {
                    Id = "rocket_medium",
                    Name = "Medium Rocket 'Falcon-M'",
                    CostThousands = 120.0f,
                    MaxMassLeoKg = 120.0f,
                    MaxMassLunarKg = 45.0f,
                    MaxMassMarsKg = 18.0f,
                    MaxMassDeepSpaceKg = 15.0f,
                    BaseReliability = 0.97f
                },
                new RocketOption
                {
                    Id = "rocket_heavy",
                    Name = "Heavy Interplanetary 'Titan-9'",
                    CostThousands = 280.0f,
                    MaxMassLeoKg = 500.0f,
                    MaxMassLunarKg = 180.0f,
                    MaxMassMarsKg = 75.0f,
                    MaxMassDeepSpaceKg = 65.0f,
                    BaseReliability = 0.98f
                }
            };
        }

        private static List<OrbitDefinition> LoadOrCreateDefaultOrbits()
        {
            return new List<OrbitDefinition>
            {
                new OrbitDefinition
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
                },
                new OrbitDefinition
                {
                    Id = "orbit_lunar",
                    Name = "Lunar Orbit (100 km)",
                    Type = OrbitType.Lunar,
                    OrbitPeriodHours = 2.0f,
                    EclipseFraction = 0.45f,
                    CommWindowFraction = 0.15f,
                    SolarFluxFactor = 1.0f,
                    SignalDelaySeconds = 3.0f,
                    BaseRadiation = 15.0f
                },
                new OrbitDefinition
                {
                    Id = "orbit_mars",
                    Name = "Mars Orbit (350 km)",
                    Type = OrbitType.Mars,
                    OrbitPeriodHours = 2.0f,
                    EclipseFraction = 0.35f,
                    CommWindowFraction = 0.15f,
                    SolarFluxFactor = 0.43f,
                    SignalDelaySeconds = 10.0f,
                    BaseRadiation = 20.0f
                }
            };
        }

        private static RectTransform FindOrCreateContainer(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<RectTransform>();
            }

            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Text EnsureLabel(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment,
            Font font)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Text existingText = existing.GetComponent<Text>();
                if (existingText != null)
                {
                    existingText.text = content;
                    return existingText;
                }
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
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateCardButton(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Font font,
            out Text labelText)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = obj.GetComponent<Image>();
            img.color = new Color(0.09f, 0.15f, 0.25f, 0.96f);

            var btn = obj.GetComponent<Button>();
            btn.targetGraphic = img;

            labelText = EnsureLabel(
                rt,
                "CardText",
                new Vector2(0.03f, 0.06f),
                new Vector2(0.97f, 0.94f),
                string.Empty,
                15,
                FontStyle.Normal,
                Color.white,
                TextAnchor.MiddleLeft,
                font);

            return btn;
        }

        private static void ClearChildren(RectTransform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }
    }
}
