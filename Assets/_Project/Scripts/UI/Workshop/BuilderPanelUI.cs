using System;
using System.Collections.Generic;
using AstraLink.Core;
using AstraLink.UI.Hub;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Workshop
{
    /// <summary>
    /// Screen 2 Controller: Spacecraft Hangar & 5-Slot Builder (Task S1-07, Ivan V).
    /// Displays the active contract (KSP-style header), 5 fixed component category slots
    /// with dedicated Icon Frames for module sprites, launch vehicle selector with rocket icon slot,
    /// R&D Lab toggle, and the live 6-resource NASA telemetry panel (ResourceGaugesUI).
    /// </summary>
    public class BuilderPanelUI : MonoBehaviour
    {
        public static BuilderPanelUI Instance { get; private set; }

        [Serializable]
        public class IconSpriteMapping
        {
            public string ItemId;
            public Sprite IconSprite;
        }

        [Header("Connected Sub-Panels (Auto-created if empty)")]
        [SerializeField] private ResourceGaugesUI resourceGaugesUI;
        [SerializeField] private RnDPanelUI rndPanelUI;

        [Header("Module & Rocket Icon Sprites (Night Sprint N-01)")]
        [SerializeField] private Image[] slotIconImages = new Image[5];
        [SerializeField] private Image rocketIconImage;
        [SerializeField] private List<IconSpriteMapping> itemIcons = new List<IconSpriteMapping>();

        private IGameDataRepository _repository;
        private IMissionSimulator _simulator;
        private AgencyState _agencyState;
        private MissionGoal _activeMission;

        private List<SatelliteComponent> _allComponents = new List<SatelliteComponent>();
        private List<RocketOption> _allRockets = new List<RocketOption>();
        private List<OrbitDefinition> _allOrbits = new List<OrbitDefinition>();

        private static readonly ComponentCategory[] SlotCategories = new ComponentCategory[]
        {
            ComponentCategory.CorePlatform,
            ComponentCategory.PowerGeneration,
            ComponentCategory.EnergyStorage,
            ComponentCategory.CommsAndStorage,
            ComponentCategory.SciencePayload
        };

        private static readonly string[] SlotTitles = new string[]
        {
            "SLOT 1: CORE PLATFORM (BUS)",
            "SLOT 2: POWER GENERATION",
            "SLOT 3: ENERGY STORAGE (BATTERY)",
            "SLOT 4: COMMS & SSD STORAGE",
            "SLOT 5: SCIENCE PAYLOAD (SENSOR)"
        };

        private static readonly string[] SlotIconPlaceholders = new string[]
        {
            "BUS\nICON",
            "PWR\nICON",
            "BAT\nICON",
            "COMM\nICON",
            "SCI\nICON"
        };

        private readonly SatelliteComponent[] _selectedSlots = new SatelliteComponent[5];
        private int _selectedRocketIndex;

        // UI Widgets
        private Text _activeContractBannerText;
        private Text _agencyCapitalButtonText;
        private readonly Text[] _slotPartNameTexts = new Text[5];
        private readonly Text[] _slotPartStatsTexts = new Text[5];
        private readonly Text[] _slotIconPlaceholderTexts = new Text[5];
        private Text _rocketNameText;
        private Text _rocketStatsText;
        private Text _rocketIconPlaceholderText;

        public event Action<List<SatelliteComponent>, RocketOption, ResourceSummary> OnConfigurationChanged;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (_allComponents.Count == 0)
            {
                IMissionSimulator sim = GameManager.Instance != null ? GameManager.Instance.Simulator : new MockMissionSimulator();
                AgencyState state = GameManager.Instance != null ? GameManager.Instance.CurrentAgencyState : new AgencyState();
                Initialize(_repository, sim, state);
            }
        }

        private void OnEnable()
        {
            if (_allComponents.Count > 0)
            {
                RefreshWorkshopUI();
            }
        }

        /// <summary>
        /// Single entry point for prefab initialization by GameManager.
        /// </summary>
        public void Initialize(
            IGameDataRepository repository,
            IMissionSimulator simulator,
            AgencyState agencyState,
            MissionGoal activeMission = null)
        {
            _repository = repository;
            _simulator = simulator ?? (GameManager.Instance != null ? GameManager.Instance.Simulator : new MockMissionSimulator());
            _agencyState = agencyState ?? (GameManager.Instance != null ? GameManager.Instance.CurrentAgencyState : new AgencyState());
            if (activeMission != null)
            {
                _activeMission = activeMission;
            }

            _allComponents = (_repository != null && _repository.GetComponents() != null && _repository.GetComponents().Count > 0)
                ? _repository.GetComponents()
                : BuildDefaultComponentsFromSpec();

            _allRockets = (_repository != null && _repository.GetRockets() != null && _repository.GetRockets().Count > 0)
                ? _repository.GetRockets()
                : BuildDefaultRocketsFromSpec();

            _allOrbits = (_repository != null && _repository.GetOrbits() != null && _repository.GetOrbits().Count > 0)
                ? _repository.GetOrbits()
                : BuildDefaultOrbitsFromSpec();

            EnsureWorkshopUIHierarchy();

            if (_selectedSlots[0] == null)
            {
                ApplyMission1StarterTemplate();
            }
            else
            {
                RefreshWorkshopUI();
            }
        }

        [ContextMenu("Generate Workshop UI In Scene")]
        public void BuildUIInEditor()
        {
            _allComponents = BuildDefaultComponentsFromSpec();
            _allRockets = BuildDefaultRocketsFromSpec();
            _allOrbits = BuildDefaultOrbitsFromSpec();
            if (_agencyState == null) _agencyState = new AgencyState();
            if (_simulator == null) _simulator = new MockMissionSimulator();

            Transform existing = transform.Find("WorkshopContentRoot");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }
            Transform existingModal = transform.Find("RnDModalOverlay");
            if (existingModal != null)
            {
                DestroyImmediate(existingModal.gameObject);
            }

            EnsureWorkshopUIHierarchy();
            ApplyMission1StarterTemplate();
            Debug.Log("[BuilderPanelUI] Hangar Builder (with Icon Slots + 6 NASA Gauges + KSP Branching R&D Tree) generated!");
        }

        public void ApplyMission1StarterTemplate()
        {
            for (int slot = 0; slot < 5; slot++)
            {
                List<SatelliteComponent> options = GetUnlockedComponentsForCategory(SlotCategories[slot]);
                if (options.Count > 0)
                {
                    _selectedSlots[slot] = options[0];
                }
            }
            _selectedRocketIndex = 0;
            RefreshWorkshopUI();
        }

        public void CycleSlotComponent(int slotIndex, int direction)
        {
            if (slotIndex < 0 || slotIndex >= 5) return;

            List<SatelliteComponent> options = GetUnlockedComponentsForCategory(SlotCategories[slotIndex]);
            if (options.Count == 0) return;

            int currentIdx = 0;
            if (_selectedSlots[slotIndex] != null)
            {
                currentIdx = options.FindIndex(c => c.Id == _selectedSlots[slotIndex].Id);
                if (currentIdx < 0) currentIdx = 0;
            }

            int nextIdx = (currentIdx + direction + options.Count) % options.Count;
            _selectedSlots[slotIndex] = options[nextIdx];

            RefreshWorkshopUI();
        }

        public void CycleRocket(int direction)
        {
            if (_allRockets == null || _allRockets.Count == 0) return;
            _selectedRocketIndex = (_selectedRocketIndex + direction + _allRockets.Count) % _allRockets.Count;
            RefreshWorkshopUI();
        }

        public void OpenRnDOverlay()
        {
            if (rndPanelUI != null)
            {
                rndPanelUI.gameObject.SetActive(true);
                rndPanelUI.Initialize(_agencyState, _allComponents);
            }
        }

        public List<SatelliteComponent> GetSelectedComponentsList()
        {
            var list = new List<SatelliteComponent>(5);
            for (int i = 0; i < 5; i++)
            {
                if (_selectedSlots[i] != null) list.Add(_selectedSlots[i]);
            }
            return list;
        }

        public RocketOption GetSelectedRocket()
        {
            if (_allRockets == null || _allRockets.Count == 0) return null;
            int idx = Mathf.Clamp(_selectedRocketIndex, 0, _allRockets.Count - 1);
            return _allRockets[idx];
        }

        public void SetActiveMission(MissionGoal goal)
        {
            _activeMission = goal;
            RefreshWorkshopUI();
        }

        public MissionGoal GetActiveMissionGoal()
        {
            if (_activeMission != null)
            {
                return _activeMission;
            }
            if (ContractHubUI.Instance != null && ContractHubUI.Instance.SelectedMission != null)
            {
                return ContractHubUI.Instance.SelectedMission;
            }
            return ContractHubUI.BuildDefaultMissionsFromSpec()[0];
        }

        public OrbitDefinition GetActiveOrbit(MissionGoal goal)
        {
            string orbitId = goal != null ? goal.OrbitId : "orbit_leo";
            for (int i = 0; i < _allOrbits.Count; i++)
            {
                if (_allOrbits[i].Id == orbitId) return _allOrbits[i];
            }
            return _allOrbits.Count > 0 ? _allOrbits[0] : null;
        }

        public void RefreshWorkshopUI()
        {
            EnsureWorkshopUIHierarchy();

            if (_agencyState == null && GameManager.Instance != null)
            {
                _agencyState = GameManager.Instance.CurrentAgencyState;
            }
            if (_agencyState == null) _agencyState = new AgencyState();

            MissionGoal activeGoal = GetActiveMissionGoal();
            OrbitDefinition activeOrbit = GetActiveOrbit(activeGoal);
            RocketOption rocket = GetSelectedRocket();
            List<SatelliteComponent> selectedList = GetSelectedComponentsList();

            if (_activeContractBannerText != null && activeGoal != null)
            {
                string reqSensor = string.IsNullOrEmpty(activeGoal.RequiredSensorTag) ? "Any" : activeGoal.RequiredSensorTag;
                string orbitName = activeOrbit != null ? activeOrbit.Name : activeGoal.OrbitId;
                float delaySec = activeOrbit != null ? activeOrbit.SignalDelaySeconds : 1.0f;

                _activeContractBannerText.text = string.Format(
                    "ACTIVE CONTRACT: <b>{0}</b>   |   Orbit: <b>{1}</b> (Delay {2:0.0}s)   |   Budget Limit: <b>${3:0}k</b>   |   Target: <b>{4:0} Sci</b>   |   Required Sensor: <b>{5}</b>",
                    activeGoal.Title, orbitName, delaySec, activeGoal.BudgetLimitThousands, activeGoal.TargetSciencePoints, reqSensor);
            }

            if (_agencyCapitalButtonText != null)
            {
                _agencyCapitalButtonText.text = string.Format("R&D Tech Tree (${0:0}k)", _agencyState.CapitalThousands);
            }

            // Update 5 Component Slot Cards + Icon Slots
            for (int i = 0; i < 5; i++)
            {
                SatelliteComponent comp = _selectedSlots[i];
                List<SatelliteComponent> unlockedInCat = GetUnlockedComponentsForCategory(SlotCategories[i]);

                if (comp == null && unlockedInCat.Count > 0)
                {
                    comp = unlockedInCat[0];
                    _selectedSlots[i] = comp;
                }

                if (_slotPartNameTexts[i] != null && comp != null)
                {
                    int idx = unlockedInCat.FindIndex(c => c.Id == comp.Id) + 1;
                    _slotPartNameTexts[i].text = string.Format("{0}  ({1}/{2})", comp.Name, Mathf.Max(1, idx), unlockedInCat.Count);
                }

                if (_slotPartStatsTexts[i] != null && comp != null)
                {
                    _slotPartStatsTexts[i].text = FormatComponentShortStats(comp);
                }

                if (comp != null)
                {
                    UpdateIconDisplay(slotIconImages != null && i < slotIconImages.Length ? slotIconImages[i] : null,
                        _slotIconPlaceholderTexts[i], comp.Id, SlotIconPlaceholders[i]);
                }
            }

            // Update Rocket Selector + Rocket Icon Slot
            if (rocket != null)
            {
                float maxMass = ResourceGaugesUI.GetMaxRocketMassForOrbit(rocket, activeOrbit);
                if (_rocketNameText != null)
                {
                    _rocketNameText.text = string.Format("{0}  ({1}/{2})", rocket.Name, _selectedRocketIndex + 1, _allRockets.Count);
                }
                if (_rocketStatsText != null)
                {
                    _rocketStatsText.text = string.Format(
                        "Cost: ${0:0}k   |   Max Mass on Orbit: {1:0} kg   |   Base Reliability: {2:0}%",
                        rocket.CostThousands, maxMass, rocket.BaseReliability * 100f);
                }

                UpdateIconDisplay(rocketIconImage, _rocketIconPlaceholderText, rocket.Id, "ROCKET\nICON");
            }

            ResourceSummary summary = ComputeLiveResourceSummary(selectedList, rocket, activeOrbit, activeGoal);

            if (resourceGaugesUI != null)
            {
                resourceGaugesUI.UpdateGauges(summary, selectedList, rocket, activeOrbit, activeGoal);
            }

            OnConfigurationChanged?.Invoke(selectedList, rocket, summary);
        }

        private void UpdateIconDisplay(Image targetImage, Text placeholderText, string itemId, string fallbackLabel)
        {
            if (targetImage == null) return;

            Sprite mappedSprite = FindMappedSprite(itemId);
            targetImage.sprite = mappedSprite;

            bool hasSprite = targetImage.sprite != null;
            targetImage.color = hasSprite ? Color.white : new Color(1f, 1f, 1f, 0.08f);

            if (placeholderText != null)
            {
                placeholderText.gameObject.SetActive(!hasSprite);
                if (!hasSprite) placeholderText.text = fallbackLabel;
            }
        }

        private Sprite FindMappedSprite(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || itemIcons == null) return null;
            for (int i = 0; i < itemIcons.Count; i++)
            {
                if (itemIcons[i] != null && itemIcons[i].ItemId == itemId && itemIcons[i].IconSprite != null)
                {
                    return itemIcons[i].IconSprite;
                }
            }
            return null;
        }

        private ResourceSummary ComputeLiveResourceSummary(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal)
        {
            if (_simulator == null && GameManager.Instance != null)
            {
                _simulator = GameManager.Instance.Simulator;
            }

            ResourceSummary simResult = _simulator != null
                ? _simulator.CalculateResources(components, rocket, orbit, goal)
                : null;

            if (_simulator != null && !(_simulator is MockMissionSimulator) && simResult != null)
            {
                return simResult;
            }

            float totalMass = 0f;
            float totalCost = rocket != null ? rocket.CostThousands : 0f;
            float maxSolar = 0f;
            float rtgWatts = 0f;
            float maxBattery = 0f;
            float maxSsd = 0f;
            float downlink = 0f;
            float shielding = 0f;
            float reliability = rocket != null ? rocket.BaseReliability : 0.95f;

            if (components != null)
            {
                for (int i = 0; i < components.Count; i++)
                {
                    var c = components[i];
                    if (c == null) continue;
                    totalMass += c.MassKg;
                    totalCost += c.CostThousands;
                    maxSolar += c.SolarWatts;
                    rtgWatts += c.RtgWatts;
                    maxBattery += c.BatteryCapacityWh;
                    maxSsd += c.SsdCapacityGb;
                    downlink += c.DownlinkRateGbPerTick;
                    shielding += c.RadiationShielding;
                    reliability *= c.BaseReliability;
                }
            }

            float maxOrbitMass = ResourceGaugesUI.GetMaxRocketMassForOrbit(rocket, orbit);
            float budgetLimit = goal != null ? goal.BudgetLimitThousands : 120f;

            return new ResourceSummary
            {
                TotalMassKg = totalMass,
                TotalCostThousands = totalCost,
                MaxSolarWatts = maxSolar,
                RtgWatts = rtgWatts,
                MaxBatteryWh = maxBattery,
                MaxSsdGb = maxSsd,
                DownlinkGbPerTick = downlink,
                OverallReliability = Mathf.Clamp01(reliability),
                TotalShielding = shielding,
                IsMassValid = totalMass <= maxOrbitMass + 0.001f,
                IsBudgetValid = totalCost <= budgetLimit + 0.001f
            };
        }

        private List<SatelliteComponent> GetUnlockedComponentsForCategory(ComponentCategory category)
        {
            var result = new List<SatelliteComponent>();
            for (int i = 0; i < _allComponents.Count; i++)
            {
                var c = _allComponents[i];
                if (c != null && c.Category == category && RnDPanelUI.IsComponentUnlocked(c, _agencyState))
                {
                    result.Add(c);
                }
            }

            if (result.Count == 0)
            {
                for (int i = 0; i < _allComponents.Count; i++)
                {
                    if (_allComponents[i] != null && _allComponents[i].Category == category)
                    {
                        result.Add(_allComponents[i]);
                        break;
                    }
                }
            }
            return result;
        }

        private void EnsureWorkshopUIHierarchy()
        {
            Transform placeholder = transform.Find("PlaceholderTitle");
            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(false);
            }

            Transform existingRoot = transform.Find("WorkshopContentRoot");
            if (existingRoot != null)
            {
                BindExistingHierarchy(existingRoot);
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject rootObj = CreateUIObject("WorkshopContentRoot", transform);
            StretchFull(rootObj.GetComponent<RectTransform>());

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(28, 28, 18, 18);
            rootLayout.spacing = 14f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // 1. TOP KSP-STYLE ACTIVE CONTRACT BAR
            GameObject contractBar = CreateUIObject("ActiveContractBar", rootObj.transform);
            Image barBg = contractBar.AddComponent<Image>();
            barBg.color = new Color(0.06f, 0.11f, 0.20f, 0.96f);
            LayoutElement barLe = contractBar.AddComponent<LayoutElement>();
            barLe.preferredHeight = 62f;

            HorizontalLayoutGroup barLayout = contractBar.AddComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(18, 18, 8, 8);
            barLayout.spacing = 14f;
            barLayout.childAlignment = TextAnchor.MiddleLeft;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false;

            _activeContractBannerText = CreateText("ContractBannerText", contractBar.transform, font,
                "ACTIVE CONTRACT: 1. LEO Weather CubeSat", 17, FontStyle.Normal,
                new Color(0.92f, 0.96f, 1f, 1f), TextAnchor.MiddleLeft);
            _activeContractBannerText.supportRichText = true;
            LayoutElement bannerLe = _activeContractBannerText.gameObject.AddComponent<LayoutElement>();
            bannerLe.flexibleWidth = 1f;

            CreateButton(contractBar.transform, "Btn_ResetTemplate", "Reset M1 Template",
                new Color(0.20f, 0.34f, 0.52f, 1f), 190f, 44f, font, ApplyMission1StarterTemplate);

            Button rndBtn = CreateButton(contractBar.transform, "Btn_OpenRnD", "R&D Tech Tree ($50k)",
                new Color(0.55f, 0.36f, 0.10f, 1f), 250f, 44f, font, OpenRnDOverlay);
            _agencyCapitalButtonText = rndBtn.GetComponentInChildren<Text>();

            // 2. MAIN WORKSPACE COLUMNS
            GameObject columnsRow = CreateUIObject("MainColumns", rootObj.transform);
            LayoutElement colsLe = columnsRow.AddComponent<LayoutElement>();
            colsLe.flexibleHeight = 1f;

            HorizontalLayoutGroup colsLayout = columnsRow.AddComponent<HorizontalLayoutGroup>();
            colsLayout.spacing = 20f;
            colsLayout.childControlWidth = true;
            colsLayout.childControlHeight = true;
            colsLayout.childForceExpandWidth = false;
            colsLayout.childForceExpandHeight = true;

            // LEFT COLUMN: 5 Spacecraft Slots + Rocket Selector (with Icon Frames)
            GameObject leftCol = CreateUIObject("SlotsColumn", columnsRow.transform);
            Image leftBg = leftCol.AddComponent<Image>();
            leftBg.color = new Color(0.06f, 0.10f, 0.17f, 0.94f);
            LayoutElement leftLe = leftCol.AddComponent<LayoutElement>();
            leftLe.flexibleWidth = 1.25f;
            leftLe.preferredWidth = 1020f;

            VerticalLayoutGroup leftLayout = leftCol.AddComponent<VerticalLayoutGroup>();
            leftLayout.padding = new RectOffset(18, 18, 14, 14);
            leftLayout.spacing = 10f;
            leftLayout.childControlWidth = true;
            leftLayout.childControlHeight = true;
            leftLayout.childForceExpandWidth = true;
            leftLayout.childForceExpandHeight = false;

            Text leftHeader = CreateText("SlotsHeader", leftCol.transform, font,
                "SPACECRAFT ASSEMBLY — 5 FIXED MODULE SLOTS & LAUNCH VEHICLE", 20, FontStyle.Bold,
                new Color(1f, 0.88f, 0.42f, 1f), TextAnchor.MiddleLeft);
            LayoutElement lhLe = leftHeader.gameObject.AddComponent<LayoutElement>();
            lhLe.preferredHeight = 28f;

            if (slotIconImages == null || slotIconImages.Length != 5)
            {
                slotIconImages = new Image[5];
            }

            for (int i = 0; i < 5; i++)
            {
                BuildSlotRow(i, SlotTitles[i], SlotIconPlaceholders[i], leftCol.transform, font);
            }

            BuildRocketSelectorRow(leftCol.transform, font);

            // RIGHT COLUMN: 6 NASA Resource Gauges
            GameObject rightCol = CreateUIObject("ResourceGaugesColumn", columnsRow.transform);
            Image rightBg = rightCol.AddComponent<Image>();
            rightBg.color = new Color(0.06f, 0.10f, 0.17f, 0.94f);
            LayoutElement rightLe = rightCol.AddComponent<LayoutElement>();
            rightLe.flexibleWidth = 1f;
            rightLe.preferredWidth = 820f;

            resourceGaugesUI = rightCol.AddComponent<ResourceGaugesUI>();
            resourceGaugesUI.EnsureGaugesUIHierarchy();

            // 3. MODAL R&D TECH TREE OVERLAY
            GameObject rndOverlay = CreateUIObject("RnDModalOverlay", transform);
            StretchFull(rndOverlay.GetComponent<RectTransform>());
            rndPanelUI = rndOverlay.AddComponent<RnDPanelUI>();
            rndPanelUI.Initialize(_agencyState, _allComponents);
            rndPanelUI.OnRnDStateChanged += RefreshWorkshopUI;
            rndOverlay.SetActive(false);
        }

        private void BindExistingHierarchy(Transform root)
        {
            _activeContractBannerText = FindChild<Text>(root, "ActiveContractBar/ContractBannerText");

            Button resetBtn = FindChild<Button>(root, "ActiveContractBar/Btn_ResetTemplate");
            if (resetBtn != null)
            {
                resetBtn.onClick.RemoveAllListeners();
                resetBtn.onClick.AddListener(ApplyMission1StarterTemplate);
            }

            Button rndBtn = FindChild<Button>(root, "ActiveContractBar/Btn_OpenRnD");
            if (rndBtn != null)
            {
                rndBtn.onClick.RemoveAllListeners();
                rndBtn.onClick.AddListener(OpenRnDOverlay);
                _agencyCapitalButtonText = rndBtn.GetComponentInChildren<Text>();
            }

            if (slotIconImages == null || slotIconImages.Length != 5)
            {
                slotIconImages = new Image[5];
            }

            Transform leftCol = root.Find("MainColumns/SlotsColumn");
            if (leftCol != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    Transform slotRow = leftCol.Find("SlotRow_" + i);
                    if (slotRow == null) continue;

                    _slotPartNameTexts[i] = FindChild<Text>(slotRow, "InfoBox/PartName");
                    _slotPartStatsTexts[i] = FindChild<Text>(slotRow, "InfoBox/PartStats");
                    slotIconImages[i] = FindChild<Image>(slotRow, "IconFrame/PartIconSprite");
                    _slotIconPlaceholderTexts[i] = FindChild<Text>(slotRow, "IconFrame/PlaceholderTag");

                    int captured = i;
                    Button prevBtn = FindChild<Button>(slotRow, "Btn_Prev");
                    if (prevBtn != null)
                    {
                        prevBtn.onClick.RemoveAllListeners();
                        prevBtn.onClick.AddListener(() => CycleSlotComponent(captured, -1));
                    }

                    Button nextBtn = FindChild<Button>(slotRow, "Btn_Next");
                    if (nextBtn != null)
                    {
                        nextBtn.onClick.RemoveAllListeners();
                        nextBtn.onClick.AddListener(() => CycleSlotComponent(captured, 1));
                    }
                }

                Transform rocketRow = leftCol.Find("RocketSelectorRow");
                if (rocketRow != null)
                {
                    _rocketNameText = FindChild<Text>(rocketRow, "InfoBox/RocketName");
                    _rocketStatsText = FindChild<Text>(rocketRow, "InfoBox/RocketStats");
                    rocketIconImage = FindChild<Image>(rocketRow, "IconFrame/RocketIconSprite");
                    _rocketIconPlaceholderText = FindChild<Text>(rocketRow, "IconFrame/PlaceholderTag");

                    Button prevRkt = FindChild<Button>(rocketRow, "Btn_PrevRocket");
                    if (prevRkt != null)
                    {
                        prevRkt.onClick.RemoveAllListeners();
                        prevRkt.onClick.AddListener(() => CycleRocket(-1));
                    }

                    Button nextRkt = FindChild<Button>(rocketRow, "Btn_NextRocket");
                    if (nextRkt != null)
                    {
                        nextRkt.onClick.RemoveAllListeners();
                        nextRkt.onClick.AddListener(() => CycleRocket(1));
                    }
                }
            }

            Transform rightCol = root.Find("MainColumns/ResourceGaugesColumn");
            if (rightCol != null)
            {
                resourceGaugesUI = rightCol.GetComponent<ResourceGaugesUI>();
                if (resourceGaugesUI == null) resourceGaugesUI = rightCol.gameObject.AddComponent<ResourceGaugesUI>();
            }

            Transform rndModal = transform.Find("RnDModalOverlay");
            if (rndModal != null)
            {
                rndPanelUI = rndModal.GetComponent<RnDPanelUI>();
                if (rndPanelUI != null)
                {
                    rndPanelUI.OnRnDStateChanged -= RefreshWorkshopUI;
                    rndPanelUI.OnRnDStateChanged += RefreshWorkshopUI;
                }
            }
        }

        private void BuildSlotRow(int slotIndex, string slotTitle, string iconPlaceholder, Transform parent, Font font)
        {
            GameObject rowObj = CreateUIObject("SlotRow_" + slotIndex, parent);
            Image bg = rowObj.AddComponent<Image>();
            bg.color = new Color(0.09f, 0.15f, 0.25f, 0.96f);

            LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 104f;

            HorizontalLayoutGroup hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(14, 14, 10, 10);
            hLayout.spacing = 14f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;

            int captured = slotIndex;
            CreateButton(rowObj.transform, "Btn_Prev", "< PREV",
                new Color(0.18f, 0.34f, 0.56f, 1f), 90f, 78f, font,
                () => CycleSlotComponent(captured, -1));

            // Dedicated Module Icon Box (78x78) for easy Sprite drag-and-drop
            GameObject iconFrame = CreateUIObject("IconFrame", rowObj.transform);
            Image frameBg = iconFrame.AddComponent<Image>();
            frameBg.color = new Color(0.05f, 0.09f, 0.16f, 1f);
            LayoutElement iconLe = iconFrame.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 78f;
            iconLe.preferredHeight = 78f;
            iconLe.minWidth = 78f;
            iconLe.minHeight = 78f;

            GameObject spriteObj = CreateUIObject("PartIconSprite", iconFrame.transform);
            StretchFull(spriteObj.GetComponent<RectTransform>());
            Image partIconImg = spriteObj.AddComponent<Image>();
            partIconImg.preserveAspect = true;
            partIconImg.color = new Color(1f, 1f, 1f, 0.08f);
            slotIconImages[slotIndex] = partIconImg;

            _slotIconPlaceholderTexts[slotIndex] = CreateText("PlaceholderTag", iconFrame.transform, font,
                iconPlaceholder, 12, FontStyle.Bold, new Color(0.65f, 0.84f, 1f, 0.7f), TextAnchor.MiddleCenter);
            StretchFull(_slotIconPlaceholderTexts[slotIndex].GetComponent<RectTransform>());

            // Info Box
            GameObject infoBox = CreateUIObject("InfoBox", rowObj.transform);
            LayoutElement infoLe = infoBox.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f;

            VerticalLayoutGroup infoLayout = infoBox.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 3f;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;

            CreateText("SlotCategoryTitle", infoBox.transform, font, slotTitle, 14, FontStyle.Bold,
                new Color(0.55f, 0.82f, 1f, 1f), TextAnchor.MiddleLeft);

            _slotPartNameTexts[slotIndex] = CreateText("PartName", infoBox.transform, font, "Component Name", 19, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);

            _slotPartStatsTexts[slotIndex] = CreateText("PartStats", infoBox.transform, font, "Component Stats", 15, FontStyle.Normal,
                new Color(0.80f, 0.94f, 0.84f, 1f), TextAnchor.MiddleLeft);

            CreateButton(rowObj.transform, "Btn_Next", "NEXT >",
                new Color(0.18f, 0.34f, 0.56f, 1f), 90f, 78f, font,
                () => CycleSlotComponent(captured, 1));
        }

        private void BuildRocketSelectorRow(Transform parent, Font font)
        {
            GameObject rowObj = CreateUIObject("RocketSelectorRow", parent);
            Image bg = rowObj.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.18f, 0.30f, 0.98f);

            LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 110f;
            rowLe.flexibleHeight = 1f;

            HorizontalLayoutGroup hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(14, 14, 10, 10);
            hLayout.spacing = 14f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;

            CreateButton(rowObj.transform, "Btn_PrevRocket", "< ROCKET",
                new Color(0.48f, 0.28f, 0.14f, 1f), 95f, 82f, font,
                () => CycleRocket(-1));

            // Dedicated Rocket Icon Box (82x82)
            GameObject iconFrame = CreateUIObject("IconFrame", rowObj.transform);
            Image frameBg = iconFrame.AddComponent<Image>();
            frameBg.color = new Color(0.08f, 0.09f, 0.16f, 1f);
            LayoutElement iconLe = iconFrame.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 82f;
            iconLe.preferredHeight = 82f;
            iconLe.minWidth = 82f;
            iconLe.minHeight = 82f;

            GameObject spriteObj = CreateUIObject("RocketIconSprite", iconFrame.transform);
            StretchFull(spriteObj.GetComponent<RectTransform>());
            rocketIconImage = spriteObj.AddComponent<Image>();
            rocketIconImage.preserveAspect = true;
            rocketIconImage.color = new Color(1f, 1f, 1f, 0.08f);

            _rocketIconPlaceholderText = CreateText("PlaceholderTag", iconFrame.transform, font,
                "ROCKET\nICON", 12, FontStyle.Bold, new Color(1f, 0.82f, 0.50f, 0.75f), TextAnchor.MiddleCenter);
            StretchFull(_rocketIconPlaceholderText.GetComponent<RectTransform>());

            // Info Box
            GameObject infoBox = CreateUIObject("InfoBox", rowObj.transform);
            LayoutElement infoLe = infoBox.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f;

            VerticalLayoutGroup infoLayout = infoBox.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 4f;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;

            CreateText("RocketHeader", infoBox.transform, font, "LAUNCH VEHICLE (ROCKET SELECTION)", 14, FontStyle.Bold,
                new Color(1f, 0.80f, 0.45f, 1f), TextAnchor.MiddleLeft);

            _rocketNameText = CreateText("RocketName", infoBox.transform, font, "Light Rocket 'Arrow-1'", 20, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);

            _rocketStatsText = CreateText("RocketStats", infoBox.transform, font, "Rocket Stats", 15, FontStyle.Normal,
                new Color(0.95f, 0.88f, 0.72f, 1f), TextAnchor.MiddleLeft);

            CreateButton(rowObj.transform, "Btn_NextRocket", "ROCKET >",
                new Color(0.48f, 0.28f, 0.14f, 1f), 95f, 82f, font,
                () => CycleRocket(1));
        }

        public static string FormatComponentShortStats(SatelliteComponent c)
        {
            if (c == null) return "";
            var parts = new List<string>
            {
                string.Format("Mass: {0:0.0} kg", c.MassKg),
                string.Format("Cost: ${0:0}k", c.CostThousands)
            };

            if (c.BasePowerDrawWatts > 0f) parts.Add(string.Format("Base Draw: {0:0}W", c.BasePowerDrawWatts));
            if (c.ActivePowerDrawWatts > 0f) parts.Add(string.Format("Active Draw: {0:0}W", c.ActivePowerDrawWatts));
            if (c.SolarWatts > 0f) parts.Add(string.Format("Solar: +{0:0}W", c.SolarWatts));
            if (c.RtgWatts > 0f) parts.Add(string.Format("RTG: +{0:0}W (24/7)", c.RtgWatts));
            if (c.BatteryCapacityWh > 0f) parts.Add(string.Format("Battery: {0:0} Wh", c.BatteryCapacityWh));
            if (c.SsdCapacityGb > 0f) parts.Add(string.Format("SSD: {0:0} Gb", c.SsdCapacityGb));
            if (c.DownlinkRateGbPerTick > 0f) parts.Add(string.Format("Downlink: {0:0.0} Gb/t", c.DownlinkRateGbPerTick));
            if (c.SciencePointsPerTick > 0f) parts.Add(string.Format("Sci: +{0:0.0}/t ({1:0.00} Gb/t)", c.SciencePointsPerTick, c.DataGenGbPerTick));
            if (!string.IsNullOrEmpty(c.SensorTag)) parts.Add("Tag: " + c.SensorTag);
            if (c.RadiationShielding > 0f) parts.Add(string.Format("Shield: {0:0}", c.RadiationShielding));
            parts.Add(string.Format("Rel: {0:0}%", c.BaseReliability * 100f));

            return string.Join("  •  ", parts.ToArray());
        }

        // --- Exact Spec Catalog (18 Components, 3 Rockets, 4 Orbits from Content Tab) ---

        public static List<SatelliteComponent> BuildDefaultComponentsFromSpec()
        {
            return new List<SatelliteComponent>
            {
                // 1. CorePlatform (3)
                new SatelliteComponent { Id = "bus_cubesat_3u", Name = "CubeSat 3U Bus", Description = "Lightweight modular frame for LEO tests.", Category = ComponentCategory.CorePlatform, MassKg = 4f, CostThousands = 15f, BasePowerDrawWatts = 5f, RadiationShielding = 10f, BaseReliability = 0.96f, UnlockCostThousands = 0, UnlockedByDefault = true },
                new SatelliteComponent { Id = "bus_smallsat", Name = "SmallSat Bus 'Orion-S'", Description = "Reinforced frame with lunar radiation shielding.", Category = ComponentCategory.CorePlatform, MassKg = 14f, CostThousands = 60f, BasePowerDrawWatts = 12f, RadiationShielding = 25f, BaseReliability = 0.98f, UnlockCostThousands = 30, UnlockedByDefault = false },
                new SatelliteComponent { Id = "bus_deepspace", Name = "DeepSpace Bus 'Voyager-M'", Description = "Heavy radiation-hardened bus for Mars & deep space.", Category = ComponentCategory.CorePlatform, MassKg = 28f, CostThousands = 130f, BasePowerDrawWatts = 18f, RadiationShielding = 45f, BaseReliability = 0.99f, UnlockCostThousands = 60, UnlockedByDefault = false },

                // 2. PowerGeneration (4)
                new SatelliteComponent { Id = "pwr_solar_mini", Name = "Basic 3U Solar Panels", Description = "Compact solar cells for Low Earth Orbit.", Category = ComponentCategory.PowerGeneration, MassKg = 2f, CostThousands = 10f, SolarWatts = 25f, BaseReliability = 0.97f, UnlockCostThousands = 0, UnlockedByDefault = true },
                new SatelliteComponent { Id = "pwr_solar_deployable", Name = "Deployable S-Class Arrays", Description = "Expanded solar arrays for high-power lunar radars.", Category = ComponentCategory.PowerGeneration, MassKg = 6f, CostThousands = 35f, SolarWatts = 75f, BaseReliability = 0.97f, UnlockCostThousands = 25, UnlockedByDefault = false },
                new SatelliteComponent { Id = "pwr_solar_wings", Name = "Solar Wings 'Ares'", Description = "Large solar wings to compensate for 43% sun at Mars.", Category = ComponentCategory.PowerGeneration, MassKg = 14f, CostThousands = 110f, SolarWatts = 180f, BaseReliability = 0.96f, UnlockCostThousands = 50, UnlockedByDefault = false },
                new SatelliteComponent { Id = "pwr_rtg", Name = "Radioisotope Generator (RTG)", Description = "Constant 65W nuclear power in sun and eclipse.", Category = ComponentCategory.PowerGeneration, MassKg = 16f, CostThousands = 180f, RtgWatts = 65f, RadiationShielding = 10f, BaseReliability = 0.99f, UnlockCostThousands = 80, UnlockedByDefault = false },

                // 3. EnergyStorage (3)
                new SatelliteComponent { Id = "bat_li_small", Name = "Li-Ion Battery 10 Wh", Description = "Starter battery; drains quickly in eclipse without power-saving rules.", Category = ComponentCategory.EnergyStorage, MassKg = 2f, CostThousands = 8f, BatteryCapacityWh = 10f, BaseReliability = 0.97f, UnlockCostThousands = 0, UnlockedByDefault = true },
                new SatelliteComponent { Id = "bat_li_medium", Name = "Li-Ion Battery 45 Wh", Description = "Balanced energy storage for longer eclipse periods.", Category = ComponentCategory.EnergyStorage, MassKg = 6f, CostThousands = 25f, BatteryCapacityWh = 45f, BaseReliability = 0.98f, UnlockCostThousands = 20, UnlockedByDefault = false },
                new SatelliteComponent { Id = "bat_solid_heavy", Name = "Solid-State Battery 110 Wh", Description = "High-capacity heavy battery for peak loads.", Category = ComponentCategory.EnergyStorage, MassKg = 12f, CostThousands = 75f, BatteryCapacityWh = 110f, BaseReliability = 0.99f, UnlockCostThousands = 40, UnlockedByDefault = false },

                // 4. CommsAndStorage (4)
                new SatelliteComponent { Id = "comms_uhf_basic", Name = "UHF Antenna + 4 Gb SSD", Description = "Basic low-rate radio link for small CubeSats.", Category = ComponentCategory.CommsAndStorage, MassKg = 2f, CostThousands = 12f, ActivePowerDrawWatts = 8f, SsdCapacityGb = 4f, DownlinkRateGbPerTick = 1.0f, BaseReliability = 0.97f, UnlockCostThousands = 0, UnlockedByDefault = true },
                new SatelliteComponent { Id = "comms_sband", Name = "S-Band Antenna + 16 Gb SSD", Description = "Medium link with 16 Gb SSD buffer for lunar mapping.", Category = ComponentCategory.CommsAndStorage, MassKg = 4f, CostThousands = 40f, ActivePowerDrawWatts = 16f, SsdCapacityGb = 16f, DownlinkRateGbPerTick = 4.5f, BaseReliability = 0.98f, UnlockCostThousands = 25, UnlockedByDefault = false },
                new SatelliteComponent { Id = "comms_xband", Name = "X-Band Dish + 30 Gb SSD", Description = "Directional deep-space dish for narrow comm windows.", Category = ComponentCategory.CommsAndStorage, MassKg = 8f, CostThousands = 80f, ActivePowerDrawWatts = 28f, SsdCapacityGb = 30f, DownlinkRateGbPerTick = 10.0f, BaseReliability = 0.98f, UnlockCostThousands = 45, UnlockedByDefault = false },
                new SatelliteComponent { Id = "comms_laser", Name = "Laser Comm + 50 Gb SSD", Description = "Ultra-fast optical downlink terminal; high power draw.", Category = ComponentCategory.CommsAndStorage, MassKg = 6f, CostThousands = 140f, ActivePowerDrawWatts = 40f, SsdCapacityGb = 50f, DownlinkRateGbPerTick = 18.0f, BaseReliability = 0.97f, UnlockCostThousands = 70, UnlockedByDefault = false },

                // 5. SciencePayload (4)
                new SatelliteComponent { Id = "sci_camera_basic", Name = "HD Weather Camera", Description = "Optical earth observation camera.", Category = ComponentCategory.SciencePayload, MassKg = 3f, CostThousands = 15f, ActivePowerDrawWatts = 14f, DataGenGbPerTick = 0.25f, SciencePointsPerTick = 2.5f, BaseReliability = 0.98f, UnlockCostThousands = 0, SensorTag = "Camera", UnlockedByDefault = true },
                new SatelliteComponent { Id = "sci_radar_sar", Name = "Synthetic Aperture Radar (SAR)", Description = "High-power mapping radar; generates heavy data streams.", Category = ComponentCategory.SciencePayload, MassKg = 9f, CostThousands = 65f, ActivePowerDrawWatts = 38f, DataGenGbPerTick = 1.2f, SciencePointsPerTick = 6.2f, BaseReliability = 0.97f, UnlockCostThousands = 30, SensorTag = "Radar", UnlockedByDefault = false },
                new SatelliteComponent { Id = "sci_spectrometer", Name = "Interplanetary Spectrometer", Description = "High-precision atmospheric & mineral analyzer for Mars.", Category = ComponentCategory.SciencePayload, MassKg = 11f, CostThousands = 110f, ActivePowerDrawWatts = 32f, DataGenGbPerTick = 1.6f, SciencePointsPerTick = 7.5f, BaseReliability = 0.98f, UnlockCostThousands = 45, SensorTag = "Spectrometer", UnlockedByDefault = false },
                new SatelliteComponent { Id = "sci_magnetometer", Name = "Plasma Magnetometer", Description = "Lightweight ultra-reliable magnetosphere sensor.", Category = ComponentCategory.SciencePayload, MassKg = 4f, CostThousands = 45f, ActivePowerDrawWatts = 12f, DataGenGbPerTick = 0.5f, SciencePointsPerTick = 3.5f, BaseReliability = 0.99f, UnlockCostThousands = 20, SensorTag = "Magnetometer", UnlockedByDefault = false }
            };
        }

        public static List<RocketOption> BuildDefaultRocketsFromSpec()
        {
            return new List<RocketOption>
            {
                new RocketOption { Id = "rocket_light", Name = "Light Rocket 'Arrow-1'", CostThousands = 30f, MaxMassLeoKg = 25f, MaxMassLunarKg = 8f, MaxMassMarsKg = 2f, MaxMassDeepSpaceKg = 2f, BaseReliability = 0.95f },
                new RocketOption { Id = "rocket_medium", Name = "Medium Rocket 'Falcon-M'", CostThousands = 120f, MaxMassLeoKg = 120f, MaxMassLunarKg = 45f, MaxMassMarsKg = 18f, MaxMassDeepSpaceKg = 15f, BaseReliability = 0.97f },
                new RocketOption { Id = "rocket_heavy", Name = "Heavy Interplanetary 'Titan-9'", CostThousands = 280f, MaxMassLeoKg = 500f, MaxMassLunarKg = 180f, MaxMassMarsKg = 75f, MaxMassDeepSpaceKg = 65f, BaseReliability = 0.98f }
            };
        }

        public static List<OrbitDefinition> BuildDefaultOrbitsFromSpec()
        {
            return new List<OrbitDefinition>
            {
                new OrbitDefinition { Id = "orbit_leo", Name = "LEO 400 km (Earth)", Type = OrbitType.LowEarth, OrbitPeriodHours = 1.5f, EclipseFraction = 0.35f, CommWindowFraction = 0.25f, SolarFluxFactor = 1.0f, SignalDelaySeconds = 1.0f, BaseRadiation = 5.0f },
                new OrbitDefinition { Id = "orbit_lunar", Name = "Lunar Orbit (100 km)", Type = OrbitType.Lunar, OrbitPeriodHours = 2.0f, EclipseFraction = 0.45f, CommWindowFraction = 0.15f, SolarFluxFactor = 1.0f, SignalDelaySeconds = 3.0f, BaseRadiation = 15.0f },
                new OrbitDefinition { Id = "orbit_mars", Name = "Mars Orbit (350 km)", Type = OrbitType.Mars, OrbitPeriodHours = 2.0f, EclipseFraction = 0.35f, CommWindowFraction = 0.15f, SolarFluxFactor = 0.43f, SignalDelaySeconds = 10.0f, BaseRadiation = 20.0f },
                new OrbitDefinition { Id = "orbit_venus", Name = "Venus Orbit (300 km)", Type = OrbitType.DeepSpace, OrbitPeriodHours = 1.6f, EclipseFraction = 0.40f, CommWindowFraction = 0.15f, SolarFluxFactor = 1.9f, SignalDelaySeconds = 12.0f, BaseRadiation = 35.0f }
            };
        }

        // --- UI Factory Helpers ---

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
            if (width > 0f) le.preferredWidth = width;
            if (height > 0f) le.preferredHeight = height;

            Text txt = CreateText("Text", btnObj.transform, font, label, 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }
    }
}
