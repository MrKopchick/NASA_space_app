using System;
using System.Collections.Generic;
using AstraLink.UI;
using AstraLink.UI.Debrief;
using AstraLink.UI.Hub;
using AstraLink.UI.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstraLink.UnityBridge
{
    // Namespace alias marker so both `using AstraLink.Core;` and `using AstraLink.UnityBridge;` compile cleanly.
    public static class BridgeMarker { }
}

namespace AstraLink.Core
{
    public enum GameScreen
    {
        Hub = 0,
        Workshop = 1,
        Flight = 2,
        Debrief = 3
    }

    /// <summary>
    /// Central coordinator in MainScene (Tasks S1-01 / S2-06).
    /// Manages screen transitions (Hub, Workshop, Flight, Debrief), AgencyState persistence,
    /// JsonDataRepository loading, IAstraAuditor diagnostics, and the real OrbitSimulator.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const float StartingCapitalThousands = 50.0f;
        public const int UpgradeTo4SlotsCostThousands = 40;
        public const int UpgradeTo6SlotsCostThousands = 80;
        public const string AgencyStatePrefsKey = "AstraLink_AgencyState_v1";

        public static GameManager Instance { get; private set; }

        [Header("Screen Panels (Hub, Workshop, Flight, Debrief)")]
        [SerializeField] private GameObject hubPanel;
        [SerializeField] private GameObject workshopPanel;
        [SerializeField] private GameObject flightPanel;
        [SerializeField] private GameObject debriefPanel;

        [Header("Core Bridge & Screen Controllers (S2-06)")]
        [SerializeField] private JsonDataLoader jsonDataLoader;
        [SerializeField] private ContractHubUI contractHubUI;
        [SerializeField] private BuilderPanelUI builderPanelUI;
        [SerializeField] private MissionReportUI missionReportUI;

        [Header("Initial Screen")]
        [SerializeField] private GameScreen initialScreen = GameScreen.Hub;

        private bool _controllersInitialized;
        private bool _isLaunchingOrPreparingFlight;
        private bool _hasShownInitialScreen;
        private GameObject _topNavBar;
        private CanvasGroup _transitionOverlayGroup;
        private Coroutine _screenTransitionCoroutine;

        public GameScreen CurrentScreen { get; private set; }
        public AgencyState CurrentAgencyState { get; private set; }
        public IGameDataRepository Repository { get; private set; }
        public IMissionSimulator Simulator { get; private set; }
        public IAstraAuditor Auditor { get; private set; }
        public MissionGoal ActiveMission { get; private set; }
        public MissionReport LastMissionReport { get; private set; }

        public ContractHubUI HubController => contractHubUI;
        public BuilderPanelUI WorkshopController => builderPanelUI;
        public MissionReportUI ReportController => missionReportUI;

        public event Action<GameScreen> OnScreenChanged;
        public event Action<MissionGoal> OnActiveMissionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            InitializeCoreServices();
            LoadOrCreateAgencyState();
            EnsureSceneUIStructure();
        }

        private void Start()
        {
            InitializeControllersAndEvents();
            ShowScreen(initialScreen);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveAgencyState();
            }
        }

        private void OnApplicationQuit()
        {
            SaveAgencyState();
        }

        private void Update()
        {
            if (IsTextInputFocused())
            {
                return;
            }

            // Quick keyboard shortcuts (F1-F4 or 1-4) for fast testing in the Editor
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Alpha1)) ShowHub();
            else if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.Alpha2)) ShowWorkshop();
            else if (Input.GetKeyDown(KeyCode.F3) || Input.GetKeyDown(KeyCode.Alpha3)) ShowFlight();
            else if (Input.GetKeyDown(KeyCode.F4) || Input.GetKeyDown(KeyCode.Alpha4)) ShowDebrief();
        }

        private static bool IsTextInputFocused()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null)
            {
                return false;
            }

            InputField legacyInput = selected.GetComponent<InputField>();
            if (legacyInput != null && legacyInput.isFocused)
            {
                return true;
            }

            TMP_InputField tmpInput = selected.GetComponent<TMP_InputField>();
            return tmpInput != null && tmpInput.isFocused;
        }

        private void InitializeCoreServices()
        {
            if (jsonDataLoader == null)
            {
                jsonDataLoader = GetComponent<JsonDataLoader>() ?? FindObjectOfType<JsonDataLoader>(true);
            }

            Repository = jsonDataLoader != null
                ? jsonDataLoader.GetOrLoadRepository()
                : JsonDataLoader.LoadRepository();

            if (Repository == null)
            {
                Debug.LogError("[GameManager] JsonDataRepository could not be loaded from Resources/Data/*.json.");
            }

            List<SpaceEventDefinition> events = Repository != null ? Repository.GetEvents() : null;
            Simulator = new OrbitSimulator(events);
            Auditor = new AstraAuditor(new ResourceCalculator(), Repository);
        }

        public void SetMissionSimulator(IMissionSimulator simulator)
        {
            if (simulator == null)
            {
                Debug.LogError("[GameManager] Attempted to assign a null IMissionSimulator.");
                return;
            }

            Simulator = simulator;
        }

        public void UpdateLastMissionReport(MissionReport report)
        {
            if (report != null)
            {
                LastMissionReport = report;
            }
        }

        public void ShowScreen(GameScreen screen)
        {
            InitializeControllersAndEvents();

            bool canAnimate = _hasShownInitialScreen
                && Application.isPlaying
                && gameObject.activeInHierarchy
                && screen != CurrentScreen;

            if (!canAnimate)
            {
                if (_screenTransitionCoroutine != null)
                {
                    StopCoroutine(_screenTransitionCoroutine);
                    _screenTransitionCoroutine = null;
                }
                if (_transitionOverlayGroup != null)
                {
                    _transitionOverlayGroup.alpha = 0f;
                }
                ApplyScreenImmediate(screen);
                _hasShownInitialScreen = true;
                return;
            }

            if (_screenTransitionCoroutine != null)
            {
                StopCoroutine(_screenTransitionCoroutine);
            }
            _screenTransitionCoroutine = StartCoroutine(SmoothScreenTransitionRoutine(screen));
        }

        private void ApplyScreenImmediate(GameScreen screen)
        {
            CurrentScreen = screen;

            if (screen == GameScreen.Flight || screen == GameScreen.Debrief)
            {
                EnsureReportContainerForScreen(screen);
            }

            if (hubPanel != null) hubPanel.SetActive(screen == GameScreen.Hub);
            if (workshopPanel != null) workshopPanel.SetActive(screen == GameScreen.Workshop);
            if (flightPanel != null) flightPanel.SetActive(screen == GameScreen.Flight);
            if (debriefPanel != null) debriefPanel.SetActive(screen == GameScreen.Debrief);

            RefreshControllerForScreen(screen);
            UpdateTopNavHighlight(screen);
            SaveAgencyState();

            Debug.Log("[GameManager] Switched active screen to: " + screen);
            OnScreenChanged?.Invoke(screen);
        }

        private System.Collections.IEnumerator SmoothScreenTransitionRoutine(GameScreen targetScreen)
        {
            CanvasGroup overlay = EnsureTransitionOverlay();
            if (overlay == null)
            {
                ApplyScreenImmediate(targetScreen);
                _screenTransitionCoroutine = null;
                yield break;
            }

            overlay.transform.SetAsLastSibling();
            if (_topNavBar != null)
            {
                _topNavBar.transform.SetAsLastSibling();
            }

            float fadeOutDuration = 0.13f;
            float startAlpha = overlay.alpha;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                overlay.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            overlay.alpha = 1f;

            ApplyScreenImmediate(targetScreen);
            _hasShownInitialScreen = true;

            float fadeInDuration = 0.20f;
            elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                overlay.alpha = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            overlay.alpha = 0f;
            _screenTransitionCoroutine = null;
        }

        private CanvasGroup EnsureTransitionOverlay()
        {
            if (_transitionOverlayGroup != null)
            {
                return _transitionOverlayGroup;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            Transform existing = canvas.transform.Find("ScreenTransitionOverlay");
            GameObject overlayObj;
            if (existing != null)
            {
                overlayObj = existing.gameObject;
            }
            else
            {
                overlayObj = new GameObject("ScreenTransitionOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                overlayObj.transform.SetParent(canvas.transform, false);
                var rt = overlayObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = new Vector2(0f, -70f);
            }

            var img = overlayObj.GetComponent<Image>();
            img.color = new Color(0.02f, 0.03f, 0.08f, 1f);
            img.raycastTarget = false;

            var cg = overlayObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = overlayObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            _transitionOverlayGroup = cg;
            return _transitionOverlayGroup;
        }

        public void ShowScreenByIndex(int screenIndex)
        {
            if (Enum.IsDefined(typeof(GameScreen), screenIndex))
            {
                ShowScreen((GameScreen)screenIndex);
            }
            else
            {
                Debug.LogWarning("[GameManager] Invalid screen index: " + screenIndex);
            }
        }

        public void ShowHub()
        {
            ShowScreen(GameScreen.Hub);
        }

        public void ShowWorkshop()
        {
            ShowScreen(GameScreen.Workshop);
        }

        public void ShowFlight()
        {
            InitializeControllersAndEvents();

            if (LastMissionReport == null)
            {
                TryLaunchActiveMission(transitionToFlight: true, stayInWorkshopOnInvalid: true);
                return;
            }

            ShowScreen(GameScreen.Flight);
        }

        public void ShowDebrief()
        {
            InitializeControllersAndEvents();

            if (LastMissionReport == null)
            {
                Debug.Log("[GameManager] Debrief is locked until a mission flight finishes.");
                return;
            }

            if (missionReportUI != null && !missionReportUI.IsFlightPlaybackCompleted)
            {
                Debug.Log("[GameManager] Debrief is locked until live flight playback completes.");
                return;
            }

            ShowScreen(GameScreen.Debrief);
        }

        /// <summary>
        /// Validates the active Workshop build via IMissionSimulator, simulates the full mission,
        /// and transitions to Live Flight playback.
        /// </summary>
        public void LaunchActiveMission()
        {
            TryLaunchActiveMission(transitionToFlight: true, stayInWorkshopOnInvalid: true);
        }

        public bool TryLaunchActiveMission(bool transitionToFlight = true, bool stayInWorkshopOnInvalid = true)
        {
            if (_isLaunchingOrPreparingFlight)
            {
                return false;
            }

            _isLaunchingOrPreparingFlight = true;
            try
            {
                InitializeControllersAndEvents();

                if (builderPanelUI == null || Simulator == null)
                {
                    Debug.LogError("[GameManager] Cannot launch mission because BuilderPanelUI or Simulator is missing.");
                    return false;
                }

                MissionGoal goal = builderPanelUI.GetActiveMissionGoal() ?? ActiveMission;
                if (goal == null && Repository != null)
                {
                    List<MissionGoal> missions = Repository.GetMissions();
                    if (missions != null && missions.Count > 0)
                    {
                        goal = missions[0];
                    }
                }

                if (goal == null)
                {
                    builderPanelUI.ShowLaunchValidationError("Select a contract before launching.");
                    if (stayInWorkshopOnInvalid && CurrentScreen != GameScreen.Workshop)
                    {
                        ShowScreen(GameScreen.Workshop);
                    }
                    return false;
                }

                OrbitDefinition orbit = builderPanelUI.GetActiveOrbit(goal);
                List<SatelliteComponent> components = builderPanelUI.GetSelectedComponentsList();
                RocketOption rocket = builderPanelUI.GetSelectedRocket();
                List<AutopilotRule> rules = builderPanelUI.GetActiveAutopilotRules() ?? new List<AutopilotRule>();

                if (!Simulator.ValidateLaunch(components, rocket, orbit, goal, out string errorMsg))
                {
                    string message = string.IsNullOrWhiteSpace(errorMsg) ? "Invalid satellite or rocket configuration." : errorMsg;
                    builderPanelUI.ShowLaunchValidationError(message);
                    Debug.LogWarning("[GameManager] Launch blocked: " + message);
                    if (stayInWorkshopOnInvalid && CurrentScreen != GameScreen.Workshop)
                    {
                        ShowScreen(GameScreen.Workshop);
                    }
                    return false;
                }

                builderPanelUI.ClearLaunchValidationError();
                SetActiveMissionInternal(goal, syncWorkshop: false);

                ResourceSummary resources = Simulator.CalculateResources(components, rocket, orbit, goal);
                List<SpaceEventDefinition> events = ResolveEventsForGoal(goal);
                MissionReport report = Simulator.SimulateFullMission(components, rocket, orbit, goal, rules, null, events);

                if (report == null)
                {
                    builderPanelUI.ShowLaunchValidationError("Simulation failed to generate a mission report.");
                    return false;
                }

                LastMissionReport = report;
                SaveAgencyState();

                if (missionReportUI != null)
                {
                    missionReportUI.ConfigureServices(Repository, Simulator, CurrentAgencyState, showVerificationControls: false);
                    missionReportUI.Initialize(
                        report,
                        goal,
                        CurrentAgencyState,
                        resources,
                        orbit,
                        Repository,
                        Simulator,
                        components,
                        rocket,
                        rules,
                        events);
                }

                if (transitionToFlight)
                {
                    ShowScreen(GameScreen.Flight);
                }

                return true;
            }
            finally
            {
                _isLaunchingOrPreparingFlight = false;
            }
        }

        public bool PrepareFlightFromWorkshopIfNeeded()
        {
            if (LastMissionReport != null && missionReportUI != null)
            {
                return true;
            }

            if (_isLaunchingOrPreparingFlight)
            {
                return false;
            }

            bool targetDebrief = CurrentScreen == GameScreen.Debrief;
            bool ok = TryLaunchActiveMission(transitionToFlight: !targetDebrief, stayInWorkshopOnInvalid: true);
            if (ok && targetDebrief)
            {
                ShowScreen(GameScreen.Debrief);
            }
            return ok;
        }

        public void SaveAgencyState()
        {
            if (CurrentAgencyState == null) return;
            SanitizeAgencyState(CurrentAgencyState);
            string json = JsonUtility.ToJson(CurrentAgencyState);
            PlayerPrefs.SetString(AgencyStatePrefsKey, json);
            PlayerPrefs.Save();

            if (string.IsNullOrEmpty(SaveManager.ActiveSlotId))
            {
                SaveManager.CreateNewSave("CAMPAIGN 01", json);
            }
            else
            {
                SaveManager.SaveToSlot(SaveManager.ActiveSlotId, explicitJson: json);
            }
        }

        public void ReloadStateFromActiveSave()
        {
            LoadOrCreateAgencyState();
            LastMissionReport = null;

            contractHubUI?.Initialize(Repository, CurrentAgencyState);
            builderPanelUI?.Initialize(Repository, Simulator, CurrentAgencyState, ActiveMission, Auditor);
            ShowHub();
        }
        

        public void LoadOrCreateAgencyState()
        {
            CurrentAgencyState = null;

            if (PlayerPrefs.HasKey(AgencyStatePrefsKey))
            {
                string json = PlayerPrefs.GetString(AgencyStatePrefsKey);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        CurrentAgencyState = JsonUtility.FromJson<AgencyState>(json);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[GameManager] Failed to parse saved AgencyState JSON, creating fresh state: " + ex.Message);
                    }
                }
            }

            if (CurrentAgencyState == null)
            {
                CurrentAgencyState = new AgencyState
                {
                    CapitalThousands = StartingCapitalThousands,
                    AutopilotSlotsUnlocked = AgencyState.InitialAutopilotSlots
                };
            }

            SanitizeAgencyState(CurrentAgencyState);
        }

        public void ResetAgencyState()
        {
            PlayerPrefs.DeleteKey(AgencyStatePrefsKey);
            PlayerPrefs.Save();
            CurrentAgencyState = new AgencyState
            {
                CapitalThousands = StartingCapitalThousands,
                AutopilotSlotsUnlocked = AgencyState.InitialAutopilotSlots
            };
            SanitizeAgencyState(CurrentAgencyState);
            LastMissionReport = null;

            contractHubUI?.Initialize(Repository, CurrentAgencyState);
            builderPanelUI?.Initialize(Repository, Simulator, CurrentAgencyState, ActiveMission, Auditor);
        }

        private static void SanitizeAgencyState(AgencyState state)
        {
            if (state == null) return;
            if (state.UnlockedComponentIds == null) state.UnlockedComponentIds = new List<string>();
            if (state.CompletedMissionIds == null) state.CompletedMissionIds = new List<string>();
            if (state.AutopilotSlotsUnlocked < AgencyState.InitialAutopilotSlots)
            {
                state.AutopilotSlotsUnlocked = AgencyState.InitialAutopilotSlots;
            }
            else if (state.AutopilotSlotsUnlocked > AgencyState.Tier3AutopilotSlots)
            {
                state.AutopilotSlotsUnlocked = AgencyState.Tier3AutopilotSlots;
            }
        }

        private void InitializeControllersAndEvents()
        {
            ResolveSceneControllers();

            if (_controllersInitialized)
            {
                return;
            }

            _controllersInitialized = true;

            if (contractHubUI != null)
            {
                contractHubUI.Initialize(Repository, CurrentAgencyState);
                contractHubUI.OnMissionSelected -= HandleMissionSelectedFromHub;
                contractHubUI.OnMissionSelected += HandleMissionSelectedFromHub;
            }

            if (ActiveMission == null)
            {
                MissionGoal initialMission = contractHubUI != null ? contractHubUI.SelectedMission : null;
                if (initialMission == null && Repository != null)
                {
                    List<MissionGoal> missions = Repository.GetMissions();
                    if (missions != null && missions.Count > 0)
                    {
                        initialMission = missions[0];
                    }
                }
                SetActiveMissionInternal(initialMission, syncWorkshop: false);
            }

            if (builderPanelUI != null)
            {
                builderPanelUI.Initialize(Repository, Simulator, CurrentAgencyState, ActiveMission, Auditor);
                builderPanelUI.OnLaunchMissionRequested -= LaunchActiveMission;
                builderPanelUI.OnLaunchMissionRequested += LaunchActiveMission;
            }

            if (missionReportUI != null)
            {
                missionReportUI.ConfigureServices(Repository, Simulator, CurrentAgencyState, showVerificationControls: false);
                missionReportUI.OnFlightPlaybackCompleted -= HandleFlightPlaybackCompleted;
                missionReportUI.OnFlightPlaybackCompleted += HandleFlightPlaybackCompleted;
                missionReportUI.OnRequestLiveFlightView -= ShowFlight;
                missionReportUI.OnRequestLiveFlightView += ShowFlight;
                missionReportUI.OnRequestDebriefSummaryView -= ShowDebrief;
                missionReportUI.OnRequestDebriefSummaryView += ShowDebrief;
                missionReportUI.OnReturnToHubRequested -= ShowHub;
                missionReportUI.OnReturnToHubRequested += ShowHub;
                missionReportUI.OnReturnToWorkshopRequested -= ShowWorkshop;
                missionReportUI.OnReturnToWorkshopRequested += ShowWorkshop;
            }
        }

        private void HandleMissionSelectedFromHub(MissionGoal mission)
        {
            if (mission == null) return;
            SetActiveMissionInternal(mission, syncWorkshop: true);
            SaveAgencyState();
            ShowWorkshop();
        }

        private void HandleFlightPlaybackCompleted(MissionReport completedReport)
        {
            if (completedReport != null)
            {
                LastMissionReport = completedReport;
            }

            SaveAgencyState();

            if (CurrentScreen == GameScreen.Flight)
            {
                ShowScreen(GameScreen.Debrief);
            }
            else
            {
                missionReportUI?.ShowDebriefSummaryViewInternal();
            }
        }

        private void SetActiveMissionInternal(MissionGoal mission, bool syncWorkshop)
        {
            if (mission == null) return;

            bool changed = ActiveMission == null || !string.Equals(ActiveMission.Id, mission.Id, StringComparison.Ordinal);
            ActiveMission = mission;

            if (changed)
            {
                LastMissionReport = null;
            }

            if (syncWorkshop && builderPanelUI != null)
            {
                builderPanelUI.SetActiveMission(mission);
            }

            if (changed)
            {
                OnActiveMissionChanged?.Invoke(mission);
            }
        }

        private List<SpaceEventDefinition> ResolveEventsForGoal(MissionGoal goal)
        {
            var resolved = new List<SpaceEventDefinition>();
            if (goal == null || Repository == null || string.IsNullOrWhiteSpace(goal.EventId))
            {
                return resolved;
            }

            SpaceEventDefinition ev = Repository.GetEvent(goal.EventId);
            if (ev != null)
            {
                resolved.Add(ev);
            }

            return resolved;
        }

        private void ResolveSceneControllers()
        {
            if (contractHubUI == null)
            {
                contractHubUI = hubPanel != null
                    ? hubPanel.GetComponentInChildren<ContractHubUI>(true)
                    : FindObjectOfType<ContractHubUI>(true);
            }

            if (builderPanelUI == null)
            {
                builderPanelUI = workshopPanel != null
                    ? workshopPanel.GetComponentInChildren<BuilderPanelUI>(true)
                    : FindObjectOfType<BuilderPanelUI>(true);
            }

            if (missionReportUI == null)
            {
                missionReportUI = (flightPanel != null ? flightPanel.GetComponentInChildren<MissionReportUI>(true) : null)
                    ?? (debriefPanel != null ? debriefPanel.GetComponentInChildren<MissionReportUI>(true) : null)
                    ?? FindObjectOfType<MissionReportUI>(true);
            }
        }

        private void EnsureReportContainerForScreen(GameScreen screen)
        {
            if (missionReportUI == null) return;

            GameObject targetPanel = screen == GameScreen.Flight ? flightPanel : debriefPanel;
            if (targetPanel == null) return;

            if (!targetPanel.activeSelf)
            {
                targetPanel.SetActive(true);
            }

            Transform reportTransform = missionReportUI.transform;
            if (reportTransform.parent != targetPanel.transform)
            {
                reportTransform.SetParent(targetPanel.transform, false);
                reportTransform.SetAsFirstSibling();
            }

            var rt = reportTransform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }
        }
        

        private void RefreshControllerForScreen(GameScreen screen)
        {
            switch (screen)
            {
                case GameScreen.Hub:
                    contractHubUI?.RefreshCards();
                    break;

                case GameScreen.Workshop:
                    if (builderPanelUI != null)
                    {
                        if (ActiveMission != null && (builderPanelUI.GetActiveMissionGoal() == null || !string.Equals(builderPanelUI.GetActiveMissionGoal().Id, ActiveMission.Id, StringComparison.Ordinal)))
                        {
                            builderPanelUI.SetActiveMission(ActiveMission);
                        }
                        else
                        {
                            builderPanelUI.RefreshWorkshopUI();
                        }
                    }
                    break;

                case GameScreen.Flight:
                    if (missionReportUI != null && LastMissionReport != null)
                    {
                        missionReportUI.ShowLiveFlightViewInternal();
                    }
                    break;

                case GameScreen.Debrief:
                    if (missionReportUI != null && LastMissionReport != null)
                    {
                        missionReportUI.ShowDebriefSummaryViewInternal();
                    }
                    break;
            }
        }

        private void EnsureSceneUIStructure()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("MainCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            Transform root = canvas.transform;
            if (hubPanel == null) hubPanel = FindOrCreatePanel(root, "HubPanel");
            if (workshopPanel == null) workshopPanel = FindOrCreatePanel(root, "WorkshopPanel");
            if (flightPanel == null) flightPanel = FindOrCreatePanel(root, "FlightPanel");
            if (debriefPanel == null) debriefPanel = FindOrCreatePanel(root, "DebriefPanel");

            ResolveSceneControllers();

            CleanupOrPopulatePanel(hubPanel, contractHubUI != null, "1. CONTRACT HUB & R&D (HubPanel)", new Color(0.08f, 0.13f, 0.22f, 1f));
            CleanupOrPopulatePanel(workshopPanel, builderPanelUI != null, "2. WORKSHOP & BUILDER (WorkshopPanel)", new Color(0.11f, 0.16f, 0.20f, 1f));
            CleanupOrPopulatePanel(flightPanel, missionReportUI != null, "3. ORBITAL FLIGHT (FlightPanel)", new Color(0.05f, 0.08f, 0.16f, 1f));
            CleanupOrPopulatePanel(debriefPanel, missionReportUI != null, "4. MISSION DEBRIEF (DebriefPanel)", new Color(0.14f, 0.11f, 0.20f, 1f));

            EnsureTopNavBar(root);
            EnsureSaveManagerInMainScene(root);
        }

        private static GameObject FindOrCreatePanel(Transform parent, string panelName)
        {
            Transform existing = parent.Find(panelName);
            if (existing != null) return existing.gameObject;

            var panelObj = new GameObject(panelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObj.transform.SetParent(parent, false);
            var rt = panelObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -70f);
            return panelObj;
        }

        private static void CleanupOrPopulatePanel(GameObject panel, bool hasScreenController, string titleText, Color bgColor)
        {
            if (panel == null) return;

            Image img = panel.GetComponent<Image>();
            if (img == null) img = panel.AddComponent<Image>();

            if (hasScreenController)
            {
                Transform stalePlaceholder = panel.transform.Find("PlaceholderTitle");
                if (stalePlaceholder != null)
                {
                    Destroy(stalePlaceholder.gameObject);
                }
                return;
            }

            img.color = bgColor;
            if (panel.transform.childCount > 0) return;

            var labelObj = new GameObject("PlaceholderTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObj.transform.SetParent(panel.transform, false);
            var rt = labelObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.4f);
            rt.anchorMax = new Vector2(0.9f, 0.6f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var txt = labelObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("TimesNewRoman.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = titleText + "\n[Switch screens: top buttons or keys 1 / 2 / 3 / 4]";
            txt.fontSize = 32;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.85f, 0.93f, 1f, 1f);
        }

        private void EnsureTopNavBar(Transform canvasRoot)
        {
            Transform navTransform = canvasRoot.Find("TopNavBar");
            GameObject navBar;
            if (navTransform == null)
            {
                navBar = new GameObject("TopNavBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup));
                navBar.transform.SetParent(canvasRoot, false);
            }
            else
            {
                navBar = navTransform.gameObject;
            }

            _topNavBar = navBar;

            var navRt = navBar.GetComponent<RectTransform>();
            navRt.anchorMin = new Vector2(0f, 1f);
            navRt.anchorMax = new Vector2(1f, 1f);
            navRt.pivot = new Vector2(0.5f, 1f);
            navRt.anchoredPosition = Vector2.zero;
            navRt.sizeDelta = new Vector2(0f, 70f);

            var navImg = navBar.GetComponent<Image>();
            if (navImg == null) navImg = navBar.AddComponent<Image>();
            navImg.color = new Color(0.04f, 0.06f, 0.11f, 0.96f);

            var layout = navBar.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = navBar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 10, 10);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            if (navBar.transform.childCount == 0)
            {
                CreateNavButton(navBar.transform, "Btn_Hub", "1. Hub", ShowHub);
                CreateNavButton(navBar.transform, "Btn_Workshop", "2. Workshop", ShowWorkshop);
                CreateNavButton(navBar.transform, "Btn_Flight", "3. Flight", ShowFlight);
                CreateNavButton(navBar.transform, "Btn_Debrief", "4. Debrief", ShowDebrief);
            }
        }

        private void EnsureSaveManagerInMainScene(Transform canvasRoot)
        {
            var savePanel = GetComponent<SaveManagerPanelUI>();
            if (savePanel == null)
            {
                savePanel = gameObject.AddComponent<SaveManagerPanelUI>();
            }

            Font timesFont = Resources.GetBuiltinResource<Font>("TimesNewRoman.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            savePanel.Initialize(canvasRoot, timesFont);
            savePanel.OnLoadRequested -= HandleSaveLoadedInMainScene;
            savePanel.OnLoadRequested += HandleSaveLoadedInMainScene;
        }

        private void HandleSaveLoadedInMainScene(SaveSlotMetadata _)
        {
            ReloadStateFromActiveSave();
        }

        private void UpdateTopNavHighlight(GameScreen activeScreen)
        {
            if (_topNavBar == null) return;

            bool debriefUnlocked = LastMissionReport != null && (missionReportUI == null || missionReportUI.IsFlightPlaybackCompleted);
            string[] btnNames = { "Btn_Hub", "Btn_Workshop", "Btn_Flight", "Btn_Debrief" };
            for (int i = 0; i < btnNames.Length; i++)
            {
                Transform btnTransform = _topNavBar.transform.Find(btnNames[i]);
                if (btnTransform == null) continue;
                bool isDebriefBtn = (i == (int)GameScreen.Debrief);
                Button btn = btnTransform.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = !isDebriefBtn || debriefUnlocked;
                }
                Image img = btnTransform.GetComponent<Image>();
                if (img != null)
                {
                    if (isDebriefBtn && !debriefUnlocked)
                    {
                        img.color = new Color(0.11f, 0.14f, 0.22f, 0.72f);
                    }
                    else
                    {
                        img.color = ((int)activeScreen == i)
                            ? new Color(0.20f, 0.52f, 0.78f, 1f)
                            : new Color(0.16f, 0.27f, 0.44f, 1f);
                    }
                }
            }
        }

        private static void CreateNavButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClickAction)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            var img = btnObj.GetComponent<Image>();
            img.color = new Color(0.16f, 0.27f, 0.44f, 1f);

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClickAction);

            var txtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var txt = txtObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("TimesNewRoman.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = label;
            txt.fontSize = 22;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }
    }
}
