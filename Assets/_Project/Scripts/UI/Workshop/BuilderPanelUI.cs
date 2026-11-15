using System;
using System.Collections.Generic;
using AstraLink.Core;
using AstraLink.UI;
using AstraLink.UI.Hub;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Workshop
{
    /// <summary>
    /// Screen 2 Controller: Spacecraft Hangar & 5-Slot Builder (Tasks S1-07 & S2-03, Ivan V).
    /// Main view displays the 5 fixed component slots + rocket selector (Left) and 6 NASA Resource Gauges (Right),
    /// with top bar buttons that open full-screen modals for:
    /// - Construct-Style Autopilot Rule Editor (AutopilotUI)
    /// - A.S.T.R.A. Pre-Flight Auditor & 3s Dry Run Bench Test (DryRunUI)
    /// - KSP-Style Branching R&D Tech Tree (RnDPanelUI)
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
        [SerializeField] private AutopilotUI autopilotUI;
        [SerializeField] private DryRunUI dryRunUI;
        [SerializeField] private RnDPanelUI rndPanelUI;

        [Header("Module & Rocket Icon Sprites (Night Sprint N-01)")]
        [SerializeField] private Image[] slotIconImages = new Image[5];
        [SerializeField] private Image rocketIconImage;
        [SerializeField] private List<IconSpriteMapping> itemIcons = new List<IconSpriteMapping>();

        private IGameDataRepository _repository;
        private IMissionSimulator _simulator;
        private IAstraAuditor _auditor;
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
        private Image _contractPlanetIcon;
        private Text _contractSubText;
        private Text _activeContractBannerText;
        private GameObject _launchErrorBannerObj;
        private Text _launchErrorBannerText;
        private Text _autopilotButtonText;
        private Text _dryRunButtonText;
        private Text _agencyCapitalButtonText;
        private readonly Text[] _slotPartNameTexts = new Text[5];
        private readonly Text[] _slotPartStatsTexts = new Text[5];
        private readonly Text[] _slotCounterTexts = new Text[5];
        private readonly Text[] _slotIconPlaceholderTexts = new Text[5];
        private readonly Button[] _slotPrevButtons = new Button[5];
        private readonly Button[] _slotNextButtons = new Button[5];
        private Text _rocketNameText;
        private Text _rocketStatsText;
        private Text _rocketCounterText;
        private Text _rocketIconPlaceholderText;
        private Button _rocketPrevButton;
        private Button _rocketNextButton;

        public event Action<List<SatelliteComponent>, RocketOption, ResourceSummary> OnConfigurationChanged;
        public event Action OnLaunchMissionRequested;

        public AutopilotUI AutopilotPanel => autopilotUI;
        public DryRunUI DryRunPanel => dryRunUI;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (_allComponents.Count == 0)
            {
                IGameDataRepository repo = _repository ?? (GameManager.Instance != null ? GameManager.Instance.Repository : null);
                IMissionSimulator sim = GameManager.Instance != null && GameManager.Instance.Simulator != null
                    ? GameManager.Instance.Simulator
                    : new MockMissionSimulator();
                AgencyState state = GameManager.Instance != null ? GameManager.Instance.CurrentAgencyState : new AgencyState();
                IAstraAuditor aud = _auditor ?? (GameManager.Instance != null ? GameManager.Instance.Auditor : null);
                Initialize(repo, sim, state, _activeMission, aud);
            }
        }

        private void OnEnable()
        {
            if (_allComponents.Count > 0)
            {
                RefreshWorkshopUI();
            }
        }

        public void Initialize(
            IGameDataRepository repository,
            IMissionSimulator simulator,
            AgencyState agencyState,
            MissionGoal activeMission = null,
            IAstraAuditor auditor = null)
        {
            _repository = repository ?? TryLoadJsonRepositoryFromResources();
            _simulator = simulator ?? (GameManager.Instance != null ? GameManager.Instance.Simulator : new MockMissionSimulator());
            _agencyState = agencyState ?? (GameManager.Instance != null ? GameManager.Instance.CurrentAgencyState : new AgencyState());
            _auditor = auditor ?? new AstraAuditor(new ResourceCalculator(), _repository);

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

        public static IGameDataRepository TryLoadJsonRepositoryFromResources()
        {
            try
            {
                TextAsset compAsset = Resources.Load<TextAsset>("Data/components");
                TextAsset rktAsset = Resources.Load<TextAsset>("Data/rockets");
                TextAsset orbAsset = Resources.Load<TextAsset>("Data/orbits");
                TextAsset misAsset = Resources.Load<TextAsset>("Data/missions");
                TextAsset evtAsset = Resources.Load<TextAsset>("Data/events");

                if (compAsset != null && rktAsset != null && orbAsset != null && misAsset != null && evtAsset != null)
                {
                    return new JsonDataRepository(
                        compAsset.text,
                        rktAsset.text,
                        orbAsset.text,
                        misAsset.text,
                        evtAsset.text);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BuilderPanelUI] Fallback to spec catalog (Resources/Data load failed): " + ex.Message);
            }
            return null;
        }

        private Transform GetWorkshopRootTransform()
        {
            if (string.Equals(gameObject.name, "WorkshopPanel", StringComparison.OrdinalIgnoreCase))
            {
                return transform;
            }
            Transform foundChild = transform.Find("WorkshopPanel");
            if (foundChild != null)
            {
                return foundChild;
            }
            return transform;
        }

        private static void EnsurePanelRectUnderNavBar(Transform panelTransform)
        {
            RectTransform rt = panelTransform != null ? panelTransform.GetComponent<RectTransform>() : null;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        [ContextMenu("Generate Workshop UI In Scene")]
        public void BuildUIInEditor()
        {
            _repository = TryLoadJsonRepositoryFromResources();
            _allComponents = (_repository != null && _repository.GetComponents().Count > 0)
                ? _repository.GetComponents()
                : BuildDefaultComponentsFromSpec();
            _allRockets = (_repository != null && _repository.GetRockets().Count > 0)
                ? _repository.GetRockets()
                : BuildDefaultRocketsFromSpec();
            _allOrbits = (_repository != null && _repository.GetOrbits().Count > 0)
                ? _repository.GetOrbits()
                : BuildDefaultOrbitsFromSpec();

            if (_agencyState == null) _agencyState = new AgencyState();
            if (_simulator == null) _simulator = new MockMissionSimulator();
            if (_auditor == null) _auditor = new AstraAuditor(new ResourceCalculator(), _repository);

            Transform targetRoot = GetWorkshopRootTransform();
            EnsurePanelRectUnderNavBar(targetRoot);

            string[] childRoots = { "WorkshopContentRoot", "AutopilotModalOverlay", "DryRunModalOverlay", "RnDModalOverlay" };
            for (int i = 0; i < childRoots.Length; i++)
            {
                Transform existingOnSelf = transform.Find(childRoots[i]);
                if (existingOnSelf != null)
                {
                    DestroyImmediate(existingOnSelf.gameObject);
                }
                if (targetRoot != transform)
                {
                    Transform existingOnTarget = targetRoot.Find(childRoots[i]);
                    if (existingOnTarget != null)
                    {
                        DestroyImmediate(existingOnTarget.gameObject);
                    }
                }
            }

            EnsureWorkshopUIHierarchy();
            ApplyMission1StarterTemplate();
            Debug.Log("[BuilderPanelUI] Hangar UI generated inside WorkshopPanel with modal buttons for Construct Autopilot, A.S.T.R.A./DryRun, and R&D Tech Tree!");
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

        public void OpenAutopilotOverlay()
        {
            if (dryRunUI != null) dryRunUI.gameObject.SetActive(false);
            if (rndPanelUI != null) rndPanelUI.gameObject.SetActive(false);

            if (autopilotUI != null)
            {
                autopilotUI.gameObject.SetActive(true);
                autopilotUI.RefreshSlotLocks(_agencyState, GetSelectedComponentsList());
            }
        }

        public void OpenDryRunOverlay()
        {
            if (autopilotUI != null) autopilotUI.gameObject.SetActive(false);
            if (rndPanelUI != null) rndPanelUI.gameObject.SetActive(false);

            if (dryRunUI != null)
            {
                dryRunUI.gameObject.SetActive(true);
                MissionGoal activeGoal = GetActiveMissionGoal();
                OrbitDefinition activeOrbit = GetActiveOrbit(activeGoal);
                dryRunUI.UpdateConfigurationAndAudit(
                    GetSelectedComponentsList(),
                    GetSelectedRocket(),
                    activeOrbit,
                    activeGoal,
                    GetActiveAutopilotRules());
            }
        }

        public void OpenRnDOverlay()
        {
            if (autopilotUI != null) autopilotUI.gameObject.SetActive(false);
            if (dryRunUI != null) dryRunUI.gameObject.SetActive(false);

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

        public List<AutopilotRule> GetActiveAutopilotRules()
        {
            return autopilotUI != null ? autopilotUI.GetActiveRules() : new List<AutopilotRule>();
        }

        public void SetActiveMission(MissionGoal goal)
        {
            _activeMission = goal;
            ClearLaunchValidationError();
            RefreshWorkshopUI();
        }

        public void TriggerLaunchMission()
        {
            if (OnLaunchMissionRequested != null)
            {
                OnLaunchMissionRequested.Invoke();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.LaunchActiveMission();
            }
        }

        public void ShowLaunchValidationError(string errorMessage)
        {
            EnsureWorkshopUIHierarchy();
            if (_launchErrorBannerObj != null)
            {
                _launchErrorBannerObj.SetActive(!string.IsNullOrWhiteSpace(errorMessage));
            }
            if (_launchErrorBannerText != null)
            {
                _launchErrorBannerText.text = string.IsNullOrWhiteSpace(errorMessage)
                    ? string.Empty
                    : "LAUNCH BLOCKED: " + errorMessage;
            }
        }

        public void ClearLaunchValidationError()
        {
            if (_launchErrorBannerObj != null)
            {
                _launchErrorBannerObj.SetActive(false);
            }
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
            if (_repository != null && _repository.GetMissions() != null && _repository.GetMissions().Count > 0)
            {
                return _repository.GetMissions()[0];
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

            if (activeGoal != null)
            {
                string reqSensor = string.IsNullOrEmpty(activeGoal.RequiredSensorTag) ? "Any" : activeGoal.RequiredSensorTag;
                string orbitName = activeOrbit != null ? activeOrbit.Name : activeGoal.OrbitId;
                float delaySec = activeOrbit != null ? activeOrbit.SignalDelaySeconds : 1.0f;

                if (_contractPlanetIcon != null)
                {
                    Sprite missionSprite = PixelUITheme.ResolveMissionIconSprite(activeGoal.OrbitId + "_" + activeGoal.Id);
                    _contractPlanetIcon.sprite = missionSprite;
                    _contractPlanetIcon.color = missionSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
                }

                if (_contractSubText != null)
                {
                    _contractSubText.text = string.Format("ACTIVE CONTRACT  •  {0}  •  BUDGET ${1:0}K",
                        orbitName.ToUpperInvariant(), activeGoal.BudgetLimitThousands);
                }

                if (_activeContractBannerText != null)
                {
                    _activeContractBannerText.text = string.Format(
                        "<b>{0}</b>   |   Goal {1:0} pts   |   {2}   |   {3:0.0}s delay",
                        activeGoal.Title, activeGoal.TargetSciencePoints, reqSensor, delaySec);
                }
            }

            if (_autopilotButtonText != null)
            {
                int unlocked = Mathf.Clamp(_agencyState.AutopilotSlotsUnlocked, 2, 6);
                _autopilotButtonText.text = string.Format("AUTOPILOT {0}/6", unlocked);
            }

            if (_agencyCapitalButtonText != null)
            {
                _agencyCapitalButtonText.text = string.Format("R&D (${0:0}k)", _agencyState.CapitalThousands);
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

                if (comp != null)
                {
                    int idx = Mathf.Max(1, unlockedInCat.FindIndex(c => c.Id == comp.Id) + 1);
                    if (_slotPartNameTexts[i] != null)
                    {
                        _slotPartNameTexts[i].text = comp.Name;
                    }
                    if (_slotCounterTexts[i] != null)
                    {
                        _slotCounterTexts[i].text = string.Format("{0}/{1}", idx, unlockedInCat.Count);
                    }
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

                bool canCycleSlot = unlockedInCat.Count > 1;
                SetArrowButtonEnabled(_slotPrevButtons[i], canCycleSlot);
                SetArrowButtonEnabled(_slotNextButtons[i], canCycleSlot);
            }

            // Update Rocket Selector + Rocket Icon Slot
            if (rocket != null)
            {
                float maxMass = ResourceGaugesUI.GetMaxRocketMassForOrbit(rocket, activeOrbit);
                if (_rocketNameText != null)
                {
                    _rocketNameText.text = rocket.Name;
                }
                if (_rocketCounterText != null)
                {
                    _rocketCounterText.text = string.Format("{0}/{1}", _selectedRocketIndex + 1, _allRockets.Count);
                }
                if (_rocketStatsText != null)
                {
                    _rocketStatsText.text = string.Format(
                        "Cost ${0:0}k  •  Max Orbit {1:0} kg  •  Rel {2:0}%",
                        rocket.CostThousands, maxMass, rocket.BaseReliability * 100f);
                }

                UpdateIconDisplay(rocketIconImage, _rocketIconPlaceholderText, rocket.Id, "ROCKET");

                bool canCycleRocket = _allRockets.Count > 1;
                SetArrowButtonEnabled(_rocketPrevButton, canCycleRocket);
                SetArrowButtonEnabled(_rocketNextButton, canCycleRocket);
            }

            ResourceSummary summary = ComputeLiveResourceSummary(selectedList, rocket, activeOrbit, activeGoal);

            if (resourceGaugesUI != null)
            {
                resourceGaugesUI.UpdateGauges(summary, selectedList, rocket, activeOrbit, activeGoal);
            }

            if (autopilotUI != null)
            {
                autopilotUI.RefreshSlotLocks(_agencyState, selectedList);
            }

            if (dryRunUI != null)
            {
                List<AutopilotRule> rules = GetActiveAutopilotRules();
                dryRunUI.UpdateConfigurationAndAudit(selectedList, rocket, activeOrbit, activeGoal, rules);
            }

            UpdateDryRunButtonBadge(selectedList, rocket, activeOrbit, activeGoal);

            ClearLaunchValidationError();
            OnConfigurationChanged?.Invoke(selectedList, rocket, summary);
        }

        private void HandleAutopilotRulesChanged(List<AutopilotRule> rules)
        {
            ClearLaunchValidationError();
            MissionGoal activeGoal = GetActiveMissionGoal();
            OrbitDefinition activeOrbit = GetActiveOrbit(activeGoal);
            List<SatelliteComponent> selectedList = GetSelectedComponentsList();
            RocketOption rocket = GetSelectedRocket();

            if (dryRunUI != null)
            {
                dryRunUI.UpdateConfigurationAndAudit(selectedList, rocket, activeOrbit, activeGoal, rules);
            }
            UpdateDryRunButtonBadge(selectedList, rocket, activeOrbit, activeGoal);
        }

        private void UpdateDryRunButtonBadge(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal)
        {
            if (_dryRunButtonText == null) return;

            if (_auditor == null)
            {
                _auditor = new AstraAuditor(new ResourceCalculator(), _repository);
            }

            List<AstraAuditMessage> msgs = _auditor.AuditConfiguration(
                components, rocket, orbit, goal, GetActiveAutopilotRules());

            int crits = 0;
            int warns = 0;
            for (int i = 0; i < msgs.Count; i++)
            {
                if (msgs[i].Severity == AuditSeverity.Critical) crits++;
                else if (msgs[i].Severity == AuditSeverity.Warning) warns++;
            }

            if (crits > 0)
                _dryRunButtonText.text = string.Format("A.S.T.R.A. ({0} CRIT)", crits);
            else if (warns > 0)
                _dryRunButtonText.text = string.Format("A.S.T.R.A. ({0} WARN)", warns);
            else
                _dryRunButtonText.text = "A.S.T.R.A. (OK)";
        }

        private void UpdateIconDisplay(Image targetImage, Text placeholderText, string itemId, string fallbackLabel)
        {
            if (targetImage == null) return;

            Sprite mappedSprite = FindMappedSprite(itemId);
            if (mappedSprite == null)
            {
                mappedSprite = PixelUITheme.ResolvePartIconSprite(itemId);
            }

            targetImage.sprite = mappedSprite;
            targetImage.preserveAspect = true;

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

            var calc = new ResourceCalculator();
            return calc.CalculateResources(components, rocket, orbit, goal);
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
            Transform workshopRoot = GetWorkshopRootTransform();
            EnsurePanelRectUnderNavBar(workshopRoot);

            Transform placeholder = workshopRoot.Find("PlaceholderTitle");
            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(false);
            }

            Transform existingRoot = workshopRoot.Find("WorkshopContentRoot");
            if (existingRoot != null)
            {
                if (existingRoot.Find("HangarUI_v5") != null)
                {
                    StretchFull(existingRoot.GetComponent<RectTransform>());
                    BindExistingHierarchy(existingRoot, workshopRoot);
                    PixelUITheme.ApplyToHierarchy(existingRoot);
                    return;
                }

                if (Application.isPlaying) Destroy(existingRoot.gameObject);
                else DestroyImmediate(existingRoot.gameObject);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject rootObj = CreateUIObject("WorkshopContentRoot", workshopRoot);
            StretchFull(rootObj.GetComponent<RectTransform>());
            CreateUIObject("HangarUI_v5", rootObj.transform);

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(22, 22, 12, 12);
            rootLayout.spacing = 12f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // 1. TOP BAR: Planet Icon + Contract Info + Compact Icon Action Buttons
            GameObject contractBar = CreateUIObject("ActiveContractBar", rootObj.transform);
            Image barBg = contractBar.AddComponent<Image>();
            barBg.color = new Color(0.06f, 0.11f, 0.20f, 0.96f);
            LayoutElement barLe = contractBar.AddComponent<LayoutElement>();
            barLe.minHeight = 60f;
            barLe.preferredHeight = 60f;

            HorizontalLayoutGroup barLayout = contractBar.AddComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(14, 14, 8, 8);
            barLayout.spacing = 10f;
            barLayout.childAlignment = TextAnchor.MiddleLeft;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = false;

            GameObject planetObj = CreateUIObject("ContractPlanetIcon", contractBar.transform);
            LayoutElement planetLe = planetObj.AddComponent<LayoutElement>();
            planetLe.preferredWidth = 40f;
            planetLe.preferredHeight = 40f;
            planetLe.minWidth = 40f;
            _contractPlanetIcon = planetObj.AddComponent<Image>();
            _contractPlanetIcon.preserveAspect = true;
            _contractPlanetIcon.raycastTarget = false;
            _contractPlanetIcon.sprite = PixelUITheme.ResolveMissionIconSprite("leo");

            GameObject titleCol = CreateUIObject("ContractTitleCol", contractBar.transform);
            LayoutElement titleColLe = titleCol.AddComponent<LayoutElement>();
            titleColLe.flexibleWidth = 1f;
            VerticalLayoutGroup titleV = titleCol.AddComponent<VerticalLayoutGroup>();
            titleV.spacing = 2f;
            titleV.childAlignment = TextAnchor.MiddleLeft;
            titleV.childControlWidth = true;
            titleV.childControlHeight = true;
            titleV.childForceExpandWidth = true;
            titleV.childForceExpandHeight = false;

            _contractSubText = CreateText("ContractSubText", titleCol.transform, font,
                "ACTIVE CONTRACT  •  LEO 400 KM  •  BUDGET $120K", 11, FontStyle.Bold,
                new Color(0.45f, 0.84f, 1f, 1f), TextAnchor.MiddleLeft);

            _activeContractBannerText = CreateText("ContractBannerText", titleCol.transform, font,
                "LEO Weather CubeSat", 14, FontStyle.Normal,
                new Color(0.95f, 0.98f, 1f, 1f), TextAnchor.MiddleLeft);
            _activeContractBannerText.supportRichText = true;

            Button drBtn = CreateIconButton(contractBar.transform, "Btn_OpenDryRun", "A.S.T.R.A.",
                PixelUITheme.ResolvePartIconSprite("sci_radar_sar"),
                new Color(0.18f, 0.42f, 0.65f, 1f), 152f, 38f, font, OpenDryRunOverlay);
            _dryRunButtonText = drBtn.GetComponentInChildren<Text>();

            Button apBtn = CreateIconButton(contractBar.transform, "Btn_OpenAutopilot", "AUTOPILOT 2/6",
                PixelUITheme.ResolvePartIconSprite("chip"),
                new Color(0.18f, 0.42f, 0.65f, 1f), 160f, 38f, font, OpenAutopilotOverlay);
            _autopilotButtonText = apBtn.GetComponentInChildren<Text>();

            Button rndBtn = CreateIconButton(contractBar.transform, "Btn_OpenRnD", "R&D ($50k)",
                PixelUITheme.ResolveTelemetryIconSprite(1),
                new Color(0.55f, 0.36f, 0.10f, 1f), 138f, 38f, font, OpenRnDOverlay);
            _agencyCapitalButtonText = rndBtn.GetComponentInChildren<Text>();

            CreateIconButton(contractBar.transform, "Btn_ResetTemplate", "RESET",
                PixelUITheme.ResolvePartIconSprite("reset"),
                new Color(0.75f, 0.16f, 0.18f, 1f), 112f, 38f, font, ApplyMission1StarterTemplate);

            // Launch validation banner (shown only when pre-launch validation fails)
            _launchErrorBannerObj = CreateUIObject("LaunchValidationBanner", rootObj.transform);
            Image errImg = _launchErrorBannerObj.AddComponent<Image>();
            errImg.color = new Color(0.42f, 0.10f, 0.12f, 0.96f);
            LayoutElement errLe = _launchErrorBannerObj.AddComponent<LayoutElement>();
            errLe.preferredHeight = 34f;
            _launchErrorBannerText = CreateText("ErrorText", _launchErrorBannerObj.transform, font,
                string.Empty, 14, FontStyle.Bold, new Color(1f, 0.88f, 0.88f, 1f), TextAnchor.MiddleCenter);
            StretchFull(_launchErrorBannerText.GetComponent<RectTransform>());
            _launchErrorBannerObj.SetActive(false);

            // 2. MAIN WORKSPACE COLUMNS (Left: Spacecraft Assembly | Right: Telemetry + Dry Run & Launch)
            GameObject columnsRow = CreateUIObject("MainColumns", rootObj.transform);
            LayoutElement colsLe = columnsRow.AddComponent<LayoutElement>();
            colsLe.flexibleHeight = 1f;

            HorizontalLayoutGroup colsLayout = columnsRow.AddComponent<HorizontalLayoutGroup>();
            colsLayout.spacing = 18f;
            colsLayout.childControlWidth = true;
            colsLayout.childControlHeight = true;
            colsLayout.childForceExpandWidth = false;
            colsLayout.childForceExpandHeight = true;

            // LEFT COLUMN: 5 Spacecraft Slots + Rocket Selector
            GameObject leftCol = CreateUIObject("SlotsColumn", columnsRow.transform);
            Image leftBg = leftCol.AddComponent<Image>();
            leftBg.color = new Color(0.06f, 0.10f, 0.17f, 0.94f);
            LayoutElement leftLe = leftCol.AddComponent<LayoutElement>();
            leftLe.flexibleWidth = 1.25f;
            leftLe.preferredWidth = 1020f;

            VerticalLayoutGroup leftLayout = leftCol.AddComponent<VerticalLayoutGroup>();
            leftLayout.padding = new RectOffset(18, 18, 14, 14);
            leftLayout.spacing = 8f;
            leftLayout.childControlWidth = true;
            leftLayout.childControlHeight = true;
            leftLayout.childForceExpandWidth = true;
            leftLayout.childForceExpandHeight = false;

            GameObject leftHeaderRow = CreateUIObject("SlotsHeaderRow", leftCol.transform);
            LayoutElement lhLe = leftHeaderRow.AddComponent<LayoutElement>();
            lhLe.preferredHeight = 28f;
            HorizontalLayoutGroup lhH = leftHeaderRow.AddComponent<HorizontalLayoutGroup>();
            lhH.childAlignment = TextAnchor.MiddleLeft;
            lhH.childControlWidth = true;
            lhH.childControlHeight = true;
            lhH.childForceExpandWidth = false;

            Text leftHeader = CreateText("SlotsHeader", leftHeaderRow.transform, font,
                "SPACECRAFT ASSEMBLY", 18, FontStyle.Bold,
                new Color(0.45f, 0.85f, 1f, 1f), TextAnchor.MiddleLeft);
            LayoutElement lhTitleLe = leftHeader.gameObject.AddComponent<LayoutElement>();
            lhTitleLe.flexibleWidth = 1f;

            Text leftSub = CreateText("SlotsSubHeader", leftHeaderRow.transform, font,
                "5 modules + launch vehicle", 13, FontStyle.Normal,
                new Color(0.55f, 0.68f, 0.84f, 1f), TextAnchor.MiddleRight);
            LayoutElement lhSubLe = leftSub.gameObject.AddComponent<LayoutElement>();
            lhSubLe.preferredWidth = 260f;

            if (slotIconImages == null || slotIconImages.Length != 5)
            {
                slotIconImages = new Image[5];
            }

            for (int i = 0; i < 5; i++)
            {
                BuildSlotRow(i, SlotTitles[i], SlotIconPlaceholders[i], leftCol.transform, font);
            }

            BuildRocketSelectorRow(leftCol.transform, font);

            // RIGHT COLUMN: Telemetry Panel + Bottom Dry Run & Launch Buttons
            GameObject rightWrap = CreateUIObject("RightColumn", columnsRow.transform);
            LayoutElement rightWrapLe = rightWrap.AddComponent<LayoutElement>();
            rightWrapLe.flexibleWidth = 1f;
            rightWrapLe.preferredWidth = 820f;

            VerticalLayoutGroup rightWrapV = rightWrap.AddComponent<VerticalLayoutGroup>();
            rightWrapV.spacing = 10f;
            rightWrapV.childControlWidth = true;
            rightWrapV.childControlHeight = true;
            rightWrapV.childForceExpandWidth = true;
            rightWrapV.childForceExpandHeight = false;

            GameObject rightCol = CreateUIObject("ResourceGaugesColumn", rightWrap.transform);
            Image rightBg = rightCol.AddComponent<Image>();
            rightBg.color = new Color(0.06f, 0.10f, 0.17f, 0.94f);
            LayoutElement rightLe = rightCol.AddComponent<LayoutElement>();
            rightLe.preferredHeight = 520f;
            rightLe.flexibleHeight = 1f;

            resourceGaugesUI = rightCol.AddComponent<ResourceGaugesUI>();
            resourceGaugesUI.EnsureGaugesUIHierarchy();

            GameObject footerRow = CreateUIObject("TelemetryFooter", rightWrap.transform);
            LayoutElement footerLe = footerRow.AddComponent<LayoutElement>();
            footerLe.minHeight = 56f;
            footerLe.preferredHeight = 56f;
            footerLe.flexibleHeight = 0f;
            HorizontalLayoutGroup footerH = footerRow.AddComponent<HorizontalLayoutGroup>();
            footerH.spacing = 12f;
            footerH.childControlWidth = true;
            footerH.childControlHeight = true;
            footerH.childForceExpandWidth = true;
            footerH.childForceExpandHeight = true;

            Button launchFooterBtn = CreateButton(footerRow.transform, "Btn_LaunchMission", "LAUNCH MISSION  >>",
                new Color(0.12f, 0.68f, 0.26f, 1f), 0f, 56f, font, TriggerLaunchMission);
            LayoutElement lnFootLe = launchFooterBtn.GetComponent<LayoutElement>();
            lnFootLe.flexibleWidth = 1f;
            Text lnTxt = launchFooterBtn.GetComponentInChildren<Text>();
            if (lnTxt != null) lnTxt.fontSize = 18;

            // 3. MODAL #1: CONSTRUCT-STYLE AUTOPILOT RULE EDITOR OVERLAY
            GameObject apOverlay = CreateUIObject("AutopilotModalOverlay", workshopRoot);
            StretchFull(apOverlay.GetComponent<RectTransform>());
            autopilotUI = apOverlay.AddComponent<AutopilotUI>();
            autopilotUI.Initialize(_agencyState, GetSelectedComponentsList());
            autopilotUI.OnRulesChanged -= HandleAutopilotRulesChanged;
            autopilotUI.OnRulesChanged += HandleAutopilotRulesChanged;
            autopilotUI.OnRequestOpenDryRun -= OpenDryRunOverlay;
            autopilotUI.OnRequestOpenDryRun += OpenDryRunOverlay;
            apOverlay.SetActive(false);

            // 4. MODAL #2: A.S.T.R.A. AUDITOR & 3s DRY RUN BENCH OVERLAY
            GameObject drOverlay = CreateUIObject("DryRunModalOverlay", workshopRoot);
            StretchFull(drOverlay.GetComponent<RectTransform>());
            dryRunUI = drOverlay.AddComponent<DryRunUI>();
            dryRunUI.Initialize(_auditor, _simulator, _repository, autopilotUI);
            dryRunUI.OnRequestOpenAutopilot -= OpenAutopilotOverlay;
            dryRunUI.OnRequestOpenAutopilot += OpenAutopilotOverlay;
            drOverlay.SetActive(false);

            // 5. MODAL #3: KSP-STYLE R&D TECH TREE OVERLAY
            GameObject rndOverlay = CreateUIObject("RnDModalOverlay", workshopRoot);
            StretchFull(rndOverlay.GetComponent<RectTransform>());
            rndPanelUI = rndOverlay.AddComponent<RnDPanelUI>();
            rndPanelUI.Initialize(_agencyState, _allComponents);
            rndPanelUI.OnRnDStateChanged -= RefreshWorkshopUI;
            rndPanelUI.OnRnDStateChanged += RefreshWorkshopUI;
            rndOverlay.SetActive(false);

            PixelUITheme.ApplyToHierarchy(rootObj.transform);
        }

        private void BindExistingHierarchy(Transform root, Transform workshopRoot)
        {
            _contractPlanetIcon = FindChild<Image>(root, "ActiveContractBar/ContractPlanetIcon");
            _contractSubText = FindChild<Text>(root, "ActiveContractBar/ContractTitleCol/ContractSubText");
            _activeContractBannerText = FindChild<Text>(root, "ActiveContractBar/ContractTitleCol/ContractBannerText");

            Button resetBtn = FindChild<Button>(root, "ActiveContractBar/Btn_ResetTemplate");
            if (resetBtn != null)
            {
                resetBtn.onClick.RemoveAllListeners();
                resetBtn.onClick.AddListener(ApplyMission1StarterTemplate);
            }

            Button apBtn = FindChild<Button>(root, "ActiveContractBar/Btn_OpenAutopilot");
            if (apBtn != null)
            {
                apBtn.onClick.RemoveAllListeners();
                apBtn.onClick.AddListener(OpenAutopilotOverlay);
                _autopilotButtonText = apBtn.GetComponentInChildren<Text>();
            }

            Button drBtn = FindChild<Button>(root, "ActiveContractBar/Btn_OpenDryRun");
            if (drBtn != null)
            {
                drBtn.onClick.RemoveAllListeners();
                drBtn.onClick.AddListener(OpenDryRunOverlay);
                _dryRunButtonText = drBtn.GetComponentInChildren<Text>();
            }

            Button rndBtn = FindChild<Button>(root, "ActiveContractBar/Btn_OpenRnD");
            if (rndBtn != null)
            {
                rndBtn.onClick.RemoveAllListeners();
                rndBtn.onClick.AddListener(OpenRnDOverlay);
                _agencyCapitalButtonText = rndBtn.GetComponentInChildren<Text>();
            }

            Button drFootBtn = FindChild<Button>(root, "MainColumns/RightColumn/TelemetryFooter/Btn_DryRunFooter");
            if (drFootBtn != null)
            {
                drFootBtn.onClick.RemoveAllListeners();
                drFootBtn.onClick.AddListener(OpenDryRunOverlay);
            }

            Button launchBtn = FindChild<Button>(root, "MainColumns/RightColumn/TelemetryFooter/Btn_LaunchMission");
            if (launchBtn != null)
            {
                launchBtn.onClick.RemoveAllListeners();
                launchBtn.onClick.AddListener(TriggerLaunchMission);
            }

            Transform errTransform = root.Find("LaunchValidationBanner");
            if (errTransform != null)
            {
                _launchErrorBannerObj = errTransform.gameObject;
                _launchErrorBannerText = FindChild<Text>(errTransform, "ErrorText");
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
                    _slotCounterTexts[i] = FindChild<Text>(slotRow, "SelectorBox/CounterText");
                    slotIconImages[i] = FindChild<Image>(slotRow, "IconFrame/PartIconSprite");
                    _slotIconPlaceholderTexts[i] = FindChild<Text>(slotRow, "IconFrame/PlaceholderTag");

                    int captured = i;
                    Button prevBtn = FindChild<Button>(slotRow, "SelectorBox/Btn_Prev");
                    _slotPrevButtons[i] = prevBtn;
                    if (prevBtn != null)
                    {
                        prevBtn.onClick.RemoveAllListeners();
                        prevBtn.onClick.AddListener(() => CycleSlotComponent(captured, -1));
                    }

                    Button nextBtn = FindChild<Button>(slotRow, "SelectorBox/Btn_Next");
                    _slotNextButtons[i] = nextBtn;
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
                    _rocketCounterText = FindChild<Text>(rocketRow, "SelectorBox/CounterText");
                    rocketIconImage = FindChild<Image>(rocketRow, "IconFrame/RocketIconSprite");
                    _rocketIconPlaceholderText = FindChild<Text>(rocketRow, "IconFrame/PlaceholderTag");

                    Button prevRkt = FindChild<Button>(rocketRow, "SelectorBox/Btn_PrevRocket");
                    _rocketPrevButton = prevRkt;
                    if (prevRkt != null)
                    {
                        prevRkt.onClick.RemoveAllListeners();
                        prevRkt.onClick.AddListener(() => CycleRocket(-1));
                    }

                    Button nextRkt = FindChild<Button>(rocketRow, "SelectorBox/Btn_NextRocket");
                    _rocketNextButton = nextRkt;
                    if (nextRkt != null)
                    {
                        nextRkt.onClick.RemoveAllListeners();
                        nextRkt.onClick.AddListener(() => CycleRocket(1));
                    }
                }
            }

            Transform rightCol = root.Find("MainColumns/RightColumn/ResourceGaugesColumn");
            if (rightCol != null)
            {
                resourceGaugesUI = rightCol.GetComponent<ResourceGaugesUI>();
                if (resourceGaugesUI == null) resourceGaugesUI = rightCol.gameObject.AddComponent<ResourceGaugesUI>();
                resourceGaugesUI.EnsureGaugesUIHierarchy();
            }

            Transform apModal = workshopRoot.Find("AutopilotModalOverlay") ?? transform.Find("AutopilotModalOverlay");
            if (apModal != null)
            {
                autopilotUI = apModal.GetComponent<AutopilotUI>();
                if (autopilotUI == null) autopilotUI = apModal.gameObject.AddComponent<AutopilotUI>();
                autopilotUI.Initialize(_agencyState, GetSelectedComponentsList());
                autopilotUI.OnRulesChanged -= HandleAutopilotRulesChanged;
                autopilotUI.OnRulesChanged += HandleAutopilotRulesChanged;
                autopilotUI.OnRequestOpenDryRun -= OpenDryRunOverlay;
                autopilotUI.OnRequestOpenDryRun += OpenDryRunOverlay;
            }

            Transform drModal = workshopRoot.Find("DryRunModalOverlay") ?? transform.Find("DryRunModalOverlay");
            if (drModal != null)
            {
                dryRunUI = drModal.GetComponent<DryRunUI>();
                if (dryRunUI == null) dryRunUI = drModal.gameObject.AddComponent<DryRunUI>();
                dryRunUI.Initialize(_auditor, _simulator, _repository, autopilotUI);
                dryRunUI.OnRequestOpenAutopilot -= OpenAutopilotOverlay;
                dryRunUI.OnRequestOpenAutopilot += OpenAutopilotOverlay;
            }

            Transform rndModal = workshopRoot.Find("RnDModalOverlay") ?? transform.Find("RnDModalOverlay");
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
            bg.color = new Color(0.08f, 0.13f, 0.22f, 0.94f);

            LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 92f;
            rowLe.flexibleHeight = 1f;

            HorizontalLayoutGroup hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(18, 34, 8, 8);
            hLayout.spacing = 12f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;

            // 1. Left: Icon inside item_slot frame
            GameObject iconFrame = CreateUIObject("IconFrame", rowObj.transform);
            Image frameBg = iconFrame.AddComponent<Image>();
            frameBg.color = new Color(0.05f, 0.09f, 0.16f, 1f);
            LayoutElement iconLe = iconFrame.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 68f;
            iconLe.preferredHeight = 68f;
            iconLe.minWidth = 68f;
            iconLe.minHeight = 68f;

            GameObject spriteObj = CreateUIObject("PartIconSprite", iconFrame.transform);
            RectTransform spriteRt = spriteObj.GetComponent<RectTransform>();
            StretchFull(spriteRt);
            spriteRt.offsetMin = new Vector2(6f, 6f);
            spriteRt.offsetMax = new Vector2(-6f, -6f);
            Image partIconImg = spriteObj.AddComponent<Image>();
            partIconImg.preserveAspect = true;
            partIconImg.raycastTarget = false;
            partIconImg.color = new Color(1f, 1f, 1f, 0.08f);
            slotIconImages[slotIndex] = partIconImg;

            _slotIconPlaceholderTexts[slotIndex] = CreateText("PlaceholderTag", iconFrame.transform, font,
                iconPlaceholder, 11, FontStyle.Bold, new Color(0.65f, 0.84f, 1f, 0.7f), TextAnchor.MiddleCenter);
            StretchFull(_slotIconPlaceholderTexts[slotIndex].GetComponent<RectTransform>());

            // 2. Middle: Category + Part Name + Concise 1-line Stats
            GameObject infoBox = CreateUIObject("InfoBox", rowObj.transform);
            LayoutElement infoLe = infoBox.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f;

            VerticalLayoutGroup infoLayout = infoBox.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 2f;
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;

            CreateText("SlotCategoryTitle", infoBox.transform, font, slotTitle, 11, FontStyle.Bold,
                new Color(0.45f, 0.82f, 1f, 1f), TextAnchor.MiddleLeft);

            _slotPartNameTexts[slotIndex] = CreateText("PartName", infoBox.transform, font, "Component Name", 17, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);

            _slotPartStatsTexts[slotIndex] = CreateText("PartStats", infoBox.transform, font, "Component Stats", 13, FontStyle.Normal,
                new Color(0.78f, 0.90f, 0.84f, 1f), TextAnchor.MiddleLeft);

            // 3. Right: Compact [ < ] 1/2 [ > ] Selector
            GameObject selBox = CreateUIObject("SelectorBox", rowObj.transform);
            LayoutElement selLe = selBox.AddComponent<LayoutElement>();
            selLe.preferredWidth = 128f;
            selLe.minWidth = 128f;

            HorizontalLayoutGroup selH = selBox.AddComponent<HorizontalLayoutGroup>();
            selH.spacing = 4f;
            selH.childAlignment = TextAnchor.MiddleRight;
            selH.childControlWidth = true;
            selH.childControlHeight = true;
            selH.childForceExpandWidth = false;
            selH.childForceExpandHeight = false;

            int captured = slotIndex;
            Button prevSlotBtn = CreateButton(selBox.transform, "Btn_Prev", "<",
                new Color(0.18f, 0.34f, 0.56f, 1f), 38f, 34f, font,
                () => CycleSlotComponent(captured, -1));
            ConfigureArrowButtonText(prevSlotBtn);
            _slotPrevButtons[slotIndex] = prevSlotBtn;

            _slotCounterTexts[slotIndex] = CreateText("CounterText", selBox.transform, font, "1/1", 13, FontStyle.Bold,
                new Color(0.86f, 0.94f, 1f, 1f), TextAnchor.MiddleCenter);
            LayoutElement cntLe = _slotCounterTexts[slotIndex].gameObject.AddComponent<LayoutElement>();
            cntLe.preferredWidth = 42f;
            cntLe.preferredHeight = 34f;

            Button nextSlotBtn = CreateButton(selBox.transform, "Btn_Next", ">",
                new Color(0.18f, 0.34f, 0.56f, 1f), 38f, 34f, font,
                () => CycleSlotComponent(captured, 1));
            ConfigureArrowButtonText(nextSlotBtn);
            _slotNextButtons[slotIndex] = nextSlotBtn;
        }

        private void BuildRocketSelectorRow(Transform parent, Font font)
        {
            GameObject rowObj = CreateUIObject("RocketSelectorRow", parent);
            Image bg = rowObj.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.14f, 0.08f, 0.96f);

            LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 96f;
            rowLe.flexibleHeight = 1f;

            HorizontalLayoutGroup hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(18, 34, 8, 8);
            hLayout.spacing = 12f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;

            // 1. Left: Rocket Icon inside item_slot frame
            GameObject iconFrame = CreateUIObject("IconFrame", rowObj.transform);
            Image frameBg = iconFrame.AddComponent<Image>();
            frameBg.color = new Color(0.08f, 0.09f, 0.16f, 1f);
            LayoutElement iconLe = iconFrame.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 68f;
            iconLe.preferredHeight = 68f;
            iconLe.minWidth = 68f;
            iconLe.minHeight = 68f;

            GameObject spriteObj = CreateUIObject("RocketIconSprite", iconFrame.transform);
            RectTransform spriteRt = spriteObj.GetComponent<RectTransform>();
            StretchFull(spriteRt);
            spriteRt.offsetMin = new Vector2(6f, 6f);
            spriteRt.offsetMax = new Vector2(-6f, -6f);
            rocketIconImage = spriteObj.AddComponent<Image>();
            rocketIconImage.preserveAspect = true;
            rocketIconImage.raycastTarget = false;
            rocketIconImage.color = new Color(1f, 1f, 1f, 0.08f);

            _rocketIconPlaceholderText = CreateText("PlaceholderTag", iconFrame.transform, font,
                "ROCKET", 11, FontStyle.Bold, new Color(1f, 0.82f, 0.50f, 0.75f), TextAnchor.MiddleCenter);
            StretchFull(_rocketIconPlaceholderText.GetComponent<RectTransform>());

            // 2. Middle: Info Box
            GameObject infoBox = CreateUIObject("InfoBox", rowObj.transform);
            LayoutElement infoLe = infoBox.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f;

            VerticalLayoutGroup infoLayout = infoBox.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 2f;
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;

            CreateText("RocketHeader", infoBox.transform, font, "LAUNCH VEHICLE", 11, FontStyle.Bold,
                new Color(1f, 0.78f, 0.36f, 1f), TextAnchor.MiddleLeft);

            _rocketNameText = CreateText("RocketName", infoBox.transform, font, "Light Rocket 'Arrow-1'", 17, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);

            _rocketStatsText = CreateText("RocketStats", infoBox.transform, font, "Rocket Stats", 13, FontStyle.Normal,
                new Color(0.95f, 0.88f, 0.72f, 1f), TextAnchor.MiddleLeft);

            // 3. Right: Compact [ < ] 1/3 [ > ] Selector
            GameObject selBox = CreateUIObject("SelectorBox", rowObj.transform);
            LayoutElement selLe = selBox.AddComponent<LayoutElement>();
            selLe.preferredWidth = 128f;
            selLe.minWidth = 128f;

            HorizontalLayoutGroup selH = selBox.AddComponent<HorizontalLayoutGroup>();
            selH.spacing = 4f;
            selH.childAlignment = TextAnchor.MiddleRight;
            selH.childControlWidth = true;
            selH.childControlHeight = true;
            selH.childForceExpandWidth = false;
            selH.childForceExpandHeight = false;

            Button prevRktBtn = CreateButton(selBox.transform, "Btn_PrevRocket", "<",
                new Color(0.18f, 0.34f, 0.56f, 1f), 38f, 34f, font,
                () => CycleRocket(-1));
            ConfigureArrowButtonText(prevRktBtn);
            _rocketPrevButton = prevRktBtn;

            _rocketCounterText = CreateText("CounterText", selBox.transform, font, "1/3", 13, FontStyle.Bold,
                new Color(1f, 0.90f, 0.70f, 1f), TextAnchor.MiddleCenter);
            LayoutElement rktCntLe = _rocketCounterText.gameObject.AddComponent<LayoutElement>();
            rktCntLe.preferredWidth = 42f;
            rktCntLe.preferredHeight = 34f;

            Button nextRktBtn = CreateButton(selBox.transform, "Btn_NextRocket", ">",
                new Color(0.18f, 0.34f, 0.56f, 1f), 38f, 34f, font,
                () => CycleRocket(1));
            ConfigureArrowButtonText(nextRktBtn);
            _rocketNextButton = nextRktBtn;
        }

        public static string FormatComponentShortStats(SatelliteComponent c)
        {
            if (c == null) return "";
            var parts = new List<string>
            {
                string.Format("{0:0.0} kg", c.MassKg),
                string.Format("${0:0}k", c.CostThousands)
            };

            if (c.BasePowerDrawWatts > 0f) parts.Add(string.Format("Draw {0:0}W", c.BasePowerDrawWatts));
            if (c.ActivePowerDrawWatts > 0f) parts.Add(string.Format("Draw {0:0}W", c.ActivePowerDrawWatts));
            if (c.SolarWatts > 0f) parts.Add(string.Format("+{0:0}W", c.SolarWatts));
            if (c.RtgWatts > 0f) parts.Add(string.Format("RTG +{0:0}W", c.RtgWatts));
            if (c.BatteryCapacityWh > 0f) parts.Add(string.Format("{0:0} Wh", c.BatteryCapacityWh));
            if (c.SsdCapacityGb > 0f) parts.Add(string.Format("SSD {0:0}Gb", c.SsdCapacityGb));
            if (c.DownlinkRateGbPerTick > 0f) parts.Add(string.Format("Down {0:0.0}Gb/t", c.DownlinkRateGbPerTick));
            if (c.SciencePointsPerTick > 0f) parts.Add(string.Format("Sci +{0:0.0}/t", c.SciencePointsPerTick));
            if (!string.IsNullOrEmpty(c.SensorTag)) parts.Add(c.SensorTag);
            if (c.RadiationShielding > 0f) parts.Add(string.Format("Shield {0:0}", c.RadiationShielding));
            parts.Add(string.Format("Rel {0:0}%", c.BaseReliability * 100f));

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

            Text txt = CreateText("Text", btnObj.transform, font, label, 14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }

        private static Button CreateIconButton(Transform parent, string name, string label, Sprite icon, Color bgColor, float width, float height, Font font, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            if (width > 0f) { le.minWidth = width; le.preferredWidth = width; }
            if (height > 0f) { le.minHeight = height; le.preferredHeight = height; }

            HorizontalLayoutGroup h = btnObj.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 4, 4);
            h.spacing = 6f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            if (icon != null)
            {
                GameObject iconObj = CreateUIObject("BtnIcon", btnObj.transform);
                LayoutElement iconLe = iconObj.AddComponent<LayoutElement>();
                iconLe.preferredWidth = 18f;
                iconLe.preferredHeight = 18f;
                iconLe.minWidth = 18f;
                iconLe.minHeight = 18f;
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            Text txt = CreateText("Text", btnObj.transform, font, label, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            LayoutElement txtLe = txt.gameObject.AddComponent<LayoutElement>();
            txtLe.flexibleWidth = 1f;
            return btn;
        }

        private static void ConfigureArrowButtonText(Button btn)
        {
            if (btn == null) return;
            Text txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.fontSize = 14;
                txt.fontStyle = FontStyle.Bold;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        private static void SetArrowButtonEnabled(Button btn, bool enabled)
        {
            if (btn == null) return;
            btn.interactable = enabled;

            PixelUITheme.EnsureAssetsLoaded();
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                if (!enabled && PixelUITheme.ButtonDisabledSprite != null)
                    img.sprite = PixelUITheme.ButtonDisabledSprite;
                else if (enabled && PixelUITheme.ButtonNormalSprite != null)
                    img.sprite = PixelUITheme.ButtonNormalSprite;

                img.color = enabled ? Color.white : new Color(0.65f, 0.68f, 0.74f, 0.85f);
            }

            Text txt = btn.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                txt.color = enabled
                    ? new Color(0.06f, 0.12f, 0.18f, 1f)
                    : new Color(0.75f, 0.78f, 0.84f, 0.70f);
            }
        }
    }
}
