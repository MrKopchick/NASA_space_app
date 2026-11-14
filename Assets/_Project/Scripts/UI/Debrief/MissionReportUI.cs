using System;
using System.Collections.Generic;
using System.Globalization;
using AstraLink.Core;
using AstraLink.UI.Flight;
using AstraLink.UI.Hub;
using AstraLink.UI.Workshop;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Debrief
{
    /// <summary>
    /// Combined Flight Telemetry Playback (Task S2-04) and Post-Flight Mission Debriefing Controller (Task S2-05).
    /// <para>
    /// <b>Task S2-04 (Live Flight Telemetry &amp; Radar Playback):</b>
    /// Connects step-by-step playback of <see cref="MissionReport.Ticks"/> (with <c>1x</c>, <c>5x</c>, <c>10x</c> speed switches)
    /// to <see cref="OrbitRadarUI"/>, updates live telemetry gauges (<see cref="Slider"/> and <see cref="Image.fillAmount"/> + text counters)
    /// for Battery, SSD, Electronics Health, Delivered Science, and Radiation, updates phase indicators
    /// (<see cref="SimulationTickSnapshot.IsInEclipse"/>, <see cref="SimulationTickSnapshot.IsInCommWindow"/>, <see cref="SimulationTickSnapshot.SafeModeActive"/>)
    /// and <see cref="EventPopupUI"/> (<see cref="SimulationTickSnapshot.ActiveEventTitle"/>), highlights triggered autopilot rules
    /// (<see cref="SimulationTickSnapshot.TriggeredRuleIds"/>) in green, and provides 3 signal-delayed manual override commands
    /// (<c>Toggle Instruments</c>, <c>Toggle Comms</c>, <c>Enable Safe Mode</c>) that append a <see cref="ManualCommandRequest"/>
    /// to <see cref="activeManualCommands"/> and invoke <see cref="IMissionSimulator.SimulateFullMission"/> for instant recalculation.
    /// </para>
    /// <para>
    /// <b>Task S2-05 (Post-Flight Debriefing &amp; Root-Cause Analysis):</b>
    /// Renders <see cref="MissionStatus.Success"/> (FinalScore, grade, +50 reliability bonus, contract &amp; passive payout, Return to Hub)
    /// and <see cref="MissionStatus.Failed"/> ("Why did it fail?" powered by <see cref="FailureDebriefBuilder"/> with FailureTitle,
    /// FailureCause, FailureRecommendation, and Free Return to Hangar).
    /// </para>
    /// </summary>
    public class MissionReportUI : MonoBehaviour
    {
        /// <summary>
        /// Reliability bonus points awarded when battery, health, and system reliability margins are met.
        /// </summary>
        public const float ReliabilityBonusPoints = 50.0f;

        /// <summary>
        /// Minimum battery charge percentage required throughout the flight to qualify for the reliability bonus.
        /// </summary>
        public const float MinBatteryBonusThreshold = 15.0f;

        /// <summary>
        /// Minimum final electronics health percentage required to qualify for the reliability bonus.
        /// </summary>
        public const float MinHealthBonusThreshold = 80.0f;

        /// <summary>
        /// Minimum spacecraft overall reliability (0..1) required to qualify for the reliability bonus.
        /// </summary>
        public const float MinReliabilityBonusThreshold = 0.85f;

        /// <summary>
        /// Singleton instance for convenient access during scene integration.
        /// </summary>
        public static MissionReportUI Instance { get; private set; }

        [Header("Mode Selection (S2-04 Live Flight vs S2-05 Debrief Summary)")]
        [SerializeField] private bool startInLiveFlightMode = true;
        [SerializeField] private GameObject liveFlightModeRoot;
        [SerializeField] private GameObject debriefSummaryModeRoot;
        [SerializeField] private Button switchToLiveFlightButton;
        [SerializeField] private Button switchToDebriefButton;

        [Header("S2-04 Connected Flight Components (OrbitRadarUI, TelemetryHUD, ManualOverrideUI, EventPopupUI)")]
        [SerializeField] private OrbitRadarUI orbitRadarUI;
        [SerializeField] private TelemetryHUD telemetryHUD;
        [SerializeField] private ManualOverrideUI manualOverrideUI;
        [SerializeField] private EventPopupUI eventPopupUI;

        [Header("S2-04 Playback Speed Switches (1x, 5x, 10x)")]
        [SerializeField] private Button playbackSpeed1xButton;
        [SerializeField] private Button playbackSpeed5xButton;
        [SerializeField] private Button playbackSpeed10xButton;
        [SerializeField] private Button playbackPlayPauseButton;
        [SerializeField] private Text playbackPlayPauseLabel;
        [SerializeField] private Button playbackStepTickButton;

        [Header("S2-04 Active Manual Commands Queue")]
        [SerializeField] private List<ManualCommandRequest> activeManualCommands = new List<ManualCommandRequest>();

        [Header("Header & Outcome Banner")]
        [SerializeField] private Text missionContextSubtitleText;
        [SerializeField] private Image statusBadgeBackground;
        [SerializeField] private Text statusBadgeText;
        [SerializeField] private Image outcomeBannerBackground;
        [SerializeField] private Text outcomeHeadlineText;
        [SerializeField] private Text outcomeSublineText;

        [Header("S2-05 Left Column: Flight Telemetry Summary Metrics")]
        [SerializeField] private Text scienceMetricValueText;
        [SerializeField] private Text scienceMetricDetailText;
        [SerializeField] private Image scienceMetricBarFill;

        [SerializeField] private Text budgetMetricValueText;
        [SerializeField] private Text budgetMetricDetailText;
        [SerializeField] private Image budgetMetricBarFill;

        [SerializeField] private Text minBatteryMetricValueText;
        [SerializeField] private Text minBatteryMetricDetailText;
        [SerializeField] private Image minBatteryMetricBarFill;

        [SerializeField] private Text finalHealthMetricValueText;
        [SerializeField] private Text finalHealthMetricDetailText;
        [SerializeField] private Image finalHealthMetricBarFill;

        [SerializeField] private Text reliabilityMetricValueText;
        [SerializeField] private Text reliabilityMetricDetailText;
        [SerializeField] private Image reliabilityMetricBarFill;

        [Header("S2-05 Right Column: Success State (MissionStatus.Success)")]
        [SerializeField] private GameObject successStateRoot;
        [SerializeField] private Text finalScoreValueText;
        [SerializeField] private Image ratingGradeBadgeBg;
        [SerializeField] private Text ratingGradeBadgeText;
        [SerializeField] private Text scoreFormulaBreakdownText;
        [SerializeField] private Image reliabilityBonusCardBg;
        [SerializeField] private Text reliabilityBonusTitleText;
        [SerializeField] private Text reliabilityBonusDetailText;
        [SerializeField] private Text contractRewardValueText;
        [SerializeField] private Text passiveIncomeValueText;
        [SerializeField] private Text agencyCapitalSummaryText;
        [SerializeField] private Button returnToHubButton;

        [Header("S2-05 Right Column: Failure State (MissionStatus.Failed - Why Did It Fail?)")]
        [SerializeField] private GameObject failureStateRoot;
        [SerializeField] private Image failureTitleBannerBg;
        [SerializeField] private Text failureTitleText;
        [SerializeField] private Text failureCauseText;
        [SerializeField] private Text failureRecommendationText;
        [SerializeField] private Text failureTelemetryNoteText;
        [SerializeField] private Button returnToWorkshopButton;

        [Header("Sandbox & Verification Controls")]
        [SerializeField] private bool showVerificationBar = true;
        [SerializeField] private GameObject verificationBarRoot;

        private readonly FailureDebriefBuilder _debriefBuilder = new FailureDebriefBuilder();
        private readonly AutopilotEvaluator _autopilotEvaluator = new AutopilotEvaluator();

        private IGameDataRepository _repository;
        private IMissionSimulator _simulator;
        private AgencyState _agencyState;
        private MissionGoal _activeGoal;
        private ResourceSummary _activeResources;
        private OrbitDefinition _activeOrbit;
        private RocketOption _activeRocket;
        private List<SatelliteComponent> _activeComponents = new List<SatelliteComponent>();
        private List<AutopilotRule> _activeRules = new List<AutopilotRule>();
        private List<SpaceEventDefinition> _activeEvents = new List<SpaceEventDefinition>();
        private MissionReport _currentReport;
        private MissionReport _lastRewardedReport;
        private bool _isLiveFlightViewActive = true;
        private bool _hasExplicitRules;
        private bool _hasExplicitEvents;
        private bool _allowPayoutForCurrentReport = true;
        private GameObject _sampleOrbitLeoBtn;
        private GameObject _sampleOrbitLunarBtn;
        private GameObject _sampleOrbitMarsBtn;

        /// <summary>
        /// Gets the currently displayed <see cref="MissionReport"/>.
        /// </summary>
        public MissionReport CurrentReport => _currentReport;

        /// <summary>
        /// Gets the linked <see cref="OrbitRadarUI"/> controller (Task S1-08 / S2-04).
        /// </summary>
        public OrbitRadarUI OrbitRadar => orbitRadarUI;

        /// <summary>
        /// Gets the linked <see cref="TelemetryHUD"/> controller (Task S2-04).
        /// </summary>
        public TelemetryHUD TelemetryHud => telemetryHUD;

        /// <summary>
        /// Gets the linked <see cref="ManualOverrideUI"/> controller (Task S2-04).
        /// </summary>
        public ManualOverrideUI ManualOverride => manualOverrideUI;

        /// <summary>
        /// Gets the linked <see cref="EventPopupUI"/> controller (Task S2-04).
        /// </summary>
        public EventPopupUI EventPopup => eventPopupUI;

        /// <summary>
        /// Gets the list of active manual commands queued during the flight.
        /// </summary>
        public List<ManualCommandRequest> ActiveManualCommands =>
            manualOverrideUI != null ? manualOverrideUI.ActiveManualCommands : activeManualCommands;

        /// <summary>
        /// Fired when the player clicks the button to return to the Contract Hub after a successful mission.
        /// </summary>
        public event Action OnReturnToHubRequested;

        /// <summary>
        /// Fired when the player clicks the button to return to the Hangar (Workshop) for free after a failed mission.
        /// </summary>
        public event Action OnReturnToWorkshopRequested;

        /// <summary>
        /// Fired whenever a new <see cref="MissionReport"/> is displayed.
        /// </summary>
        public event Action<MissionReport> OnReportDisplayed;

        /// <summary>
        /// Fired when live orbital radar playback finishes all ticks.
        /// </summary>
        public event Action<MissionReport> OnFlightPlaybackCompleted;

        /// <summary>
        /// Fired when the user clicks the [S2-04] Live Flight &amp; Radar tab button.
        /// </summary>
        public event Action OnRequestLiveFlightView;

        /// <summary>
        /// Fired when the user clicks the [S2-05] Post-Flight Debrief tab button.
        /// </summary>
        public event Action OnRequestDebriefSummaryView;

        private void Awake()
        {
            Instance = this;
            EnsureDebriefUIHierarchy();
            BindButtons();
        }

        private void Start()
        {
            if (_currentReport == null)
            {
                if (GameManager.Instance != null && !(GameManager.Instance.Simulator is MockMissionSimulator))
                {
                    GameManager.Instance.PrepareFlightFromWorkshopIfNeeded();
                    return;
                }

                ResolveContextFromScene();
                SetViewMode(startInLiveFlightMode);
                StartSampleLiveFlightScenario(0);
            }
        }

        public void ConfigureServices(IGameDataRepository repository, IMissionSimulator simulator, AgencyState agencyState, bool showVerificationControls = false)
        {
            _repository = repository ?? _repository;
            _simulator = simulator ?? _simulator;
            if (agencyState != null) _agencyState = agencyState;
            showVerificationBar = showVerificationControls;
            ApplyVerificationVisibility();
        }

        private void OnEnable()
        {
            if (_currentReport != null)
            {
                RefreshUI();
            }
        }

        /// <summary>
        /// Primary initialization entry point for both S2-04 (Live Flight Playback) and S2-05 (Post-Flight Debrief).
        /// </summary>
        public void Initialize(
            MissionReport report,
            MissionGoal goal = null,
            AgencyState agencyState = null,
            ResourceSummary resources = null,
            OrbitDefinition orbit = null,
            IGameDataRepository repository = null,
            IMissionSimulator simulator = null,
            List<SatelliteComponent> components = null,
            RocketOption rocket = null,
            List<AutopilotRule> rules = null,
            List<SpaceEventDefinition> events = null)
        {
            EnsureDebriefUIHierarchy();
            BindButtons();

            _repository = repository ?? _repository;
            _simulator = simulator ?? _simulator;
            if (components != null && components.Count > 0) _activeComponents = new List<SatelliteComponent>(components);
            if (rocket != null) _activeRocket = rocket;
            if (rules != null)
            {
                _activeRules = new List<AutopilotRule>(rules);
                _hasExplicitRules = true;
            }
            if (events != null)
            {
                _activeEvents = new List<SpaceEventDefinition>(events);
                _hasExplicitEvents = true;
            }
            _activeResources = resources;

            if (agencyState != null)
            {
                _agencyState = agencyState;
            }
            else if (_agencyState == null)
            {
                _agencyState = GameManager.Instance != null
                    ? GameManager.Instance.CurrentAgencyState
                    : new AgencyState();
            }

            SetViewMode(startInLiveFlightMode);
            ShowReport(report, goal, resources, orbit, true);
        }

        /// <summary>
        /// Displays the provided <see cref="MissionReport"/> and synchronizes both the S2-04 live flight playback and S2-05 debrief summary.
        /// </summary>
        public void ShowReport(
            MissionReport report,
            MissionGoal goal = null,
            ResourceSummary resources = null,
            OrbitDefinition orbit = null,
            bool applyContractPayoutOnSuccess = true)
        {
            EnsureDebriefUIHierarchy();
            ResolveContextFromScene(goal, resources, orbit);

            _currentReport = report ?? BuildFullFlightSampleReport(_activeOrbit, _activeGoal, _activeRules, activeManualCommands, _activeEvents);
            _allowPayoutForCurrentReport = applyContractPayoutOnSuccess;

            if (_currentReport.Status == MissionStatus.Failed)
            {
                EnsureFailureDebriefPopulated(_currentReport);
            }
            else if (_currentReport.Status == MissionStatus.Success && _allowPayoutForCurrentReport && !_isLiveFlightViewActive)
            {
                ApplySuccessPayoutIfNeeded(_currentReport);
            }

            SyncFlightComponentsWithReport(_currentReport, autoPlay: _isLiveFlightViewActive, resetTickIndex: true);
            RefreshUI();
            OnReportDisplayed?.Invoke(_currentReport);
        }

        /// <summary>
        /// Convenience overload for displaying a <see cref="MissionReport"/> using the active mission context.
        /// </summary>
        public void DisplayReport(MissionReport report)
        {
            ShowReport(report, _activeGoal, _activeResources, _activeOrbit, true);
        }

        /// <summary>
        /// Switches to the S2-04 Live Flight &amp; Radar Telemetry view.
        /// </summary>
        public void ShowLiveFlightView()
        {
            if (OnRequestLiveFlightView != null)
            {
                OnRequestLiveFlightView.Invoke();
                return;
            }

            ShowLiveFlightViewInternal();
        }

        public void ShowLiveFlightViewInternal()
        {
            SetViewMode(true);
            if (orbitRadarUI != null && !orbitRadarUI.IsPlaying && _currentReport != null && _currentReport.Ticks != null && _currentReport.Ticks.Count > 1)
            {
                orbitRadarUI.LoadMissionReport(_currentReport, _activeOrbit, autoPlay: true, resetTickIndex: false);
            }
            RefreshUI();
        }

        /// <summary>
        /// Switches to the S2-05 Post-Flight Debrief Summary view.
        /// </summary>
        public void ShowDebriefSummaryView()
        {
            if (OnRequestDebriefSummaryView != null)
            {
                OnRequestDebriefSummaryView.Invoke();
                return;
            }

            ShowDebriefSummaryViewInternal();
        }

        public void ShowDebriefSummaryViewInternal()
        {
            SetViewMode(false);
            if (_currentReport != null && _currentReport.Status == MissionStatus.Failed)
            {
                EnsureFailureDebriefPopulated(_currentReport);
            }
            else if (_currentReport != null && _currentReport.Status == MissionStatus.Success && _allowPayoutForCurrentReport)
            {
                ApplySuccessPayoutIfNeeded(_currentReport);
            }
            RefreshUI();
        }

        private void SetViewMode(bool liveFlightMode)
        {
            _isLiveFlightViewActive = liveFlightMode;
            if (liveFlightModeRoot != null) liveFlightModeRoot.SetActive(liveFlightMode);
            if (debriefSummaryModeRoot != null) debriefSummaryModeRoot.SetActive(!liveFlightMode);

            HighlightModeButton(switchToLiveFlightButton, liveFlightMode, new Color(0.14f, 0.58f, 0.86f, 1f));
            HighlightModeButton(switchToDebriefButton, !liveFlightMode, new Color(0.16f, 0.56f, 0.36f, 1f));
        }

        private static void HighlightModeButton(Button btn, bool active, Color activeColor)
        {
            if (btn == null) return;
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = active ? activeColor : new Color(0.12f, 0.18f, 0.28f, 0.96f);
            }
        }

        // --- S2-04 Speed Controls & Step-by-Step Playback ---

        /// <summary>
        /// Sets simulation playback speed to 1x.
        /// </summary>
        public void SetPlaybackSpeed1x() => SetPlaybackSpeed(1.0f);

        /// <summary>
        /// Sets simulation playback speed to 5x.
        /// </summary>
        public void SetPlaybackSpeed5x() => SetPlaybackSpeed(5.0f);

        /// <summary>
        /// Sets simulation playback speed to 10x.
        /// </summary>
        public void SetPlaybackSpeed10x() => SetPlaybackSpeed(10.0f);

        /// <summary>
        /// Sets the simulation playback speed multiplier on <see cref="OrbitRadarUI"/> and updates speed button highlights.
        /// </summary>
        public void SetPlaybackSpeed(float multiplier)
        {
            if (orbitRadarUI != null)
            {
                orbitRadarUI.SetPlaybackSpeed(multiplier);
            }
            UpdatePlaybackSpeedButtonHighlights(multiplier);
        }

        /// <summary>
        /// Toggles play/pause of the step-by-step <see cref="MissionReport.Ticks"/> playback.
        /// </summary>
        public void TogglePlaybackPlayPause()
        {
            if (orbitRadarUI != null)
            {
                orbitRadarUI.TogglePlayPause();
                if (playbackPlayPauseLabel != null)
                {
                    playbackPlayPauseLabel.text = orbitRadarUI.IsPlaying ? "PAUSE" : "PLAY";
                }
            }
        }

        /// <summary>
        /// Advances playback by a single tick for step-by-step inspection.
        /// </summary>
        public void StepPlaybackNextTick()
        {
            if (orbitRadarUI != null)
            {
                orbitRadarUI.StepSingleTick(1);
                if (playbackPlayPauseLabel != null)
                {
                    playbackPlayPauseLabel.text = "PLAY";
                }
            }
        }

        // --- S2-04 Manual Command Overrides (Signal-Delayed + SimulateFullMission Recalculation) ---

        /// <summary>
        /// S2-04 Manual Command 1: Toggles science instruments with visual signal delay according to <see cref="OrbitDefinition.SignalDelaySeconds"/>.
        /// </summary>
        public void TriggerManualToggleInstruments()
        {
            if (manualOverrideUI != null)
            {
                manualOverrideUI.TriggerToggleInstruments();
            }
        }

        /// <summary>
        /// S2-04 Manual Command 2: Toggles communications transmitter with visual signal delay according to <see cref="OrbitDefinition.SignalDelaySeconds"/>.
        /// </summary>
        public void TriggerManualToggleComms()
        {
            if (manualOverrideUI != null)
            {
                manualOverrideUI.TriggerToggleComms();
            }
        }

        /// <summary>
        /// S2-04 Manual Command 3: Enables (or toggles) Safe Mode with visual signal delay according to <see cref="OrbitDefinition.SignalDelaySeconds"/>.
        /// </summary>
        public void TriggerManualEnableSafeMode()
        {
            if (manualOverrideUI != null)
            {
                manualOverrideUI.TriggerEnableSafeMode();
            }
        }

        private void HandleRadarTickUpdated(SimulationTickSnapshot snapshot, int tickIndex)
        {
            if (snapshot == null) return;

            int totalTicks = (_currentReport != null && _currentReport.Ticks != null) ? _currentReport.Ticks.Count : 20;

            if (telemetryHUD != null)
            {
                telemetryHUD.UpdateFromTick(snapshot, tickIndex, totalTicks);
            }

            if (eventPopupUI != null)
            {
                eventPopupUI.UpdateFromTick(snapshot);
            }

            if (manualOverrideUI != null)
            {
                manualOverrideUI.UpdateButtonLabelsFromSnapshot(snapshot);
            }

            if (playbackPlayPauseLabel != null && orbitRadarUI != null)
            {
                playbackPlayPauseLabel.text = orbitRadarUI.IsPlaying ? "PAUSE" : "PLAY";
            }
        }

        private void HandleRadarSpeedChanged(float newSpeed)
        {
            UpdatePlaybackSpeedButtonHighlights(newSpeed);
        }

        private void HandleManualMissionRecalculated(MissionReport recalculatedReport, ManualCommandRequest command)
        {
            if (recalculatedReport == null) return;

            activeManualCommands = manualOverrideUI != null
                ? new List<ManualCommandRequest>(manualOverrideUI.ActiveManualCommands)
                : activeManualCommands;

            // Enrich recalculated ticks if MockMissionSimulator is currently active so telemetry, rules, and events stay accurate
            if (_simulator is MockMissionSimulator)
            {
                recalculatedReport = BuildFullFlightSampleReport(
                    _activeOrbit,
                    _activeGoal,
                    _activeRules,
                    activeManualCommands,
                    _activeEvents);

                if (orbitRadarUI != null)
                {
                    orbitRadarUI.LoadMissionReport(recalculatedReport, _activeOrbit, autoPlay: orbitRadarUI.IsPlaying, resetTickIndex: false);
                }
            }

            _currentReport = recalculatedReport;
            if (_currentReport.Status == MissionStatus.Failed)
            {
                EnsureFailureDebriefPopulated(_currentReport);
            }

            RefreshUI();
        }

        private void HandleRadarTestFlightCompleted(MissionReport completedReport)
        {
            if (OnFlightPlaybackCompleted != null)
            {
                OnFlightPlaybackCompleted.Invoke(_currentReport ?? completedReport);
            }
            else
            {
                ShowDebriefSummaryViewInternal();
            }
        }

        private void SyncFlightComponentsWithReport(MissionReport report, bool autoPlay, bool resetTickIndex)
        {
            if (telemetryHUD != null)
            {
                telemetryHUD.Initialize(_activeGoal, _activeResources, _activeOrbit, _activeRules, orbitRadarUI, eventPopupUI);
            }

            if (manualOverrideUI != null)
            {
                manualOverrideUI.Initialize(
                    _simulator,
                    _activeComponents,
                    _activeRocket,
                    _activeOrbit,
                    _activeGoal,
                    _activeRules,
                    orbitRadarUI,
                    telemetryHUD,
                    _activeEvents);
            }

            if (orbitRadarUI != null)
            {
                orbitRadarUI.SetSimulationContext(_simulator, _activeRocket, _activeGoal, _activeComponents, _activeRules);
                orbitRadarUI.OnTickUpdated -= HandleRadarTickUpdated;
                orbitRadarUI.OnTickUpdated += HandleRadarTickUpdated;
                orbitRadarUI.OnSpeedChanged -= HandleRadarSpeedChanged;
                orbitRadarUI.OnSpeedChanged += HandleRadarSpeedChanged;
                orbitRadarUI.OnTestFlightCompleted -= HandleRadarTestFlightCompleted;
                orbitRadarUI.OnTestFlightCompleted += HandleRadarTestFlightCompleted;

                orbitRadarUI.LoadMissionReport(report, _activeOrbit, autoPlay: autoPlay, resetTickIndex: resetTickIndex);
                UpdatePlaybackSpeedButtonHighlights(orbitRadarUI.SpeedMultiplier);
            }

            if (manualOverrideUI != null)
            {
                manualOverrideUI.OnMissionRecalculated -= HandleManualMissionRecalculated;
                manualOverrideUI.OnMissionRecalculated += HandleManualMissionRecalculated;
            }

            if (report != null && report.Ticks != null && report.Ticks.Count > 0)
            {
                int idx = orbitRadarUI != null ? Mathf.Clamp(orbitRadarUI.CurrentTickIndex, 0, report.Ticks.Count - 1) : 0;
                HandleRadarTickUpdated(report.Ticks[idx], idx);
            }
        }

        private void UpdatePlaybackSpeedButtonHighlights(float speed)
        {
            HighlightModeButton(playbackSpeed1xButton, Mathf.Approximately(speed, 1.0f), new Color(0.14f, 0.62f, 0.88f, 1f));
            HighlightModeButton(playbackSpeed5xButton, Mathf.Approximately(speed, 5.0f), new Color(0.14f, 0.62f, 0.88f, 1f));
            HighlightModeButton(playbackSpeed10xButton, Mathf.Approximately(speed, 10.0f), new Color(0.14f, 0.62f, 0.88f, 1f));
        }

        /// <summary>
        /// Starts one of the 3 preset orbital live-flight scenarios for testing S2-04:
        /// 0 = LEO CubeSat (1.0s Signal Delay, Eclipse Rule triggers in shadow),
        /// 1 = Lunar Mapper (3.0s Signal Delay, Transmitter Overheat Event on ticks 4..11),
        /// 2 = Mars Probe (10.0s Signal Delay, Solar Storm CME Event on ticks 5..13 with Radiation > 50).
        /// </summary>
        public void StartSampleLiveFlightScenario(int scenarioIndex)
        {
            ResolveContextFromScene();
            activeManualCommands.Clear();

            List<MissionGoal> defaultMissions = ContractHubUI.BuildDefaultMissionsFromSpec();
            int clampedIdx = Mathf.Clamp(scenarioIndex, 0, defaultMissions.Count - 1);
            _activeGoal = defaultMissions[clampedIdx];
            _activeOrbit = BuildDefaultOrbitForGoal(_activeGoal);
            _activeRules = TelemetryHUD.BuildDefaultSampleRules();
            _activeEvents = BuildDefaultEventsForMission(_activeGoal);

            SetViewMode(true);

            MissionReport flightReport = BuildFullFlightSampleReport(
                _activeOrbit,
                _activeGoal,
                _activeRules,
                activeManualCommands,
                _activeEvents);

            ShowReport(flightReport, _activeGoal, _activeResources, _activeOrbit, false);
        }

        /// <summary>
        /// Handles the "Return to Contract Hub" button on the Victory screen.
        /// </summary>
        public void ReturnToHub()
        {
            OnReturnToHubRequested?.Invoke();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowHub();
            }
        }

        /// <summary>
        /// Handles the "Free Return to Hangar (Workshop)" button on the Failure screen.
        /// Does not deduct any capital so the player can freely adjust components or autopilot rules.
        /// </summary>
        public void ReturnToWorkshopFree()
        {
            OnReturnToWorkshopRequested?.Invoke();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowWorkshop();
            }
        }

        /// <summary>
        /// Editor context menu utility to generate the complete S2-04 + S2-05 UI hierarchy inside a prefab or scene.
        /// </summary>
        [ContextMenu("Generate Debrief UI In Scene")]
        public void BuildUIInEditor()
        {
            CleanupOldRoots();
            Transform existing = transform.Find("DebriefContentRoot_v4");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            EnsureDebriefUIHierarchy();
            BindButtons();
            ResolveContextFromScene();
            SetViewMode(startInLiveFlightMode);
            StartSampleLiveFlightScenario(0);
        }

        private void ResolveContextFromScene(
            MissionGoal explicitGoal = null,
            ResourceSummary explicitResources = null,
            OrbitDefinition explicitOrbit = null)
        {
            if (_simulator == null)
            {
                _simulator = (GameManager.Instance != null && GameManager.Instance.Simulator != null)
                    ? GameManager.Instance.Simulator
                    : new MockMissionSimulator();
            }

            if (_agencyState == null)
            {
                _agencyState = GameManager.Instance != null && GameManager.Instance.CurrentAgencyState != null
                    ? GameManager.Instance.CurrentAgencyState
                    : new AgencyState();
            }

            if (explicitGoal != null)
            {
                _activeGoal = explicitGoal;
            }
            else if (_activeGoal == null)
            {
                if (ContractHubUI.Instance != null && ContractHubUI.Instance.SelectedMission != null)
                {
                    _activeGoal = ContractHubUI.Instance.SelectedMission;
                }
                else
                {
                    _activeGoal = BuildDefaultMissionGoal();
                }
            }

            if (explicitOrbit != null)
            {
                _activeOrbit = explicitOrbit;
            }
            else if (_activeOrbit == null)
            {
                _activeOrbit = BuildDefaultOrbitForGoal(_activeGoal);
            }

            if (_activeComponents == null || _activeComponents.Count == 0)
            {
                if (BuilderPanelUI.Instance != null && BuilderPanelUI.Instance.GetSelectedComponentsList().Count > 0)
                {
                    _activeComponents = BuilderPanelUI.Instance.GetSelectedComponentsList();
                    _activeRocket = BuilderPanelUI.Instance.GetSelectedRocket();
                }
                else
                {
                    _activeComponents = BuildDefaultStarterComponents();
                    _activeRocket = BuildDefaultStarterRocket();
                }
            }

            if (!_hasExplicitRules && (_activeRules == null || _activeRules.Count == 0))
            {
                _activeRules = TelemetryHUD.BuildDefaultSampleRules();
            }

            if (!_hasExplicitEvents && (_activeEvents == null || _activeEvents.Count == 0))
            {
                _activeEvents = BuildDefaultEventsForMission(_activeGoal);
            }

            if (explicitResources != null)
            {
                _activeResources = explicitResources;
            }
            else if (_activeResources == null)
            {
                if (_simulator != null && _activeComponents != null && _activeComponents.Count > 0)
                {
                    _activeResources = _simulator.CalculateResources(_activeComponents, _activeRocket, _activeOrbit, _activeGoal);
                }

                if (_activeResources == null)
                {
                    _activeResources = new ResourceSummary
                    {
                        TotalMassKg = 15.0f,
                        TotalCostThousands = 90.0f,
                        MaxSolarWatts = 45.0f,
                        RtgWatts = 0.0f,
                        MaxBatteryWh = 45.0f,
                        MaxSsdGb = 16.0f,
                        DownlinkGbPerTick = 2.0f,
                        OverallReliability = 0.92f,
                        TotalShielding = 15.0f,
                        IsBudgetValid = true,
                        IsMassValid = true
                    };
                }
            }
        }

        private void EnsureFailureDebriefPopulated(MissionReport report)
        {
            if (report == null || report.Status != MissionStatus.Failed)
            {
                return;
            }

            bool needsDebrief =
                string.IsNullOrWhiteSpace(report.FailureTitle) ||
                string.IsNullOrWhiteSpace(report.FailureCause) ||
                string.IsNullOrWhiteSpace(report.FailureRecommendation);

            if (needsDebrief)
            {
                _debriefBuilder.BuildDebrief(report, _activeGoal, _activeResources, _activeOrbit);
            }
        }

        private void ApplySuccessPayoutIfNeeded(MissionReport report)
        {
            if (report == null || report.Status != MissionStatus.Success || _lastRewardedReport == report)
            {
                return;
            }

            _lastRewardedReport = report;

            if (_agencyState == null)
            {
                return;
            }

            int contractReward = _activeGoal != null ? _activeGoal.RewardThousands : 130;
            int newPassive = _activeGoal != null ? _activeGoal.PassiveIncomeThousands : 20;
            int existingPassive = _agencyState.TotalPassiveIncomeThousands;

            _agencyState.CapitalThousands += contractReward + existingPassive + newPassive;
            _agencyState.TotalPassiveIncomeThousands += newPassive;

            if (_activeGoal != null &&
                !string.IsNullOrEmpty(_activeGoal.Id) &&
                _agencyState.CompletedMissionIds != null &&
                !_agencyState.CompletedMissionIds.Contains(_activeGoal.Id))
            {
                _agencyState.CompletedMissionIds.Add(_activeGoal.Id);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveAgencyState();
            }
        }

        private void RefreshUI()
        {
            if (_currentReport == null)
            {
                return;
            }

            ResolveContextFromScene();

            if (liveFlightModeRoot != null) liveFlightModeRoot.SetActive(_isLiveFlightViewActive);
            if (debriefSummaryModeRoot != null) debriefSummaryModeRoot.SetActive(!_isLiveFlightViewActive);

            HighlightModeButton(switchToLiveFlightButton, _isLiveFlightViewActive, new Color(0.14f, 0.58f, 0.86f, 1f));
            HighlightModeButton(switchToDebriefButton, !_isLiveFlightViewActive, new Color(0.16f, 0.56f, 0.36f, 1f));

            string missionTitle = _activeGoal != null && !string.IsNullOrEmpty(_activeGoal.Title)
                ? _activeGoal.Title
                : "1. LEO Weather CubeSat";
            string orbitName = _activeOrbit != null && !string.IsNullOrEmpty(_activeOrbit.Name)
                ? _activeOrbit.Name
                : "LEO 400 km (Earth)";
            float delaySec = _activeOrbit != null ? _activeOrbit.SignalDelaySeconds : 1.0f;

            int completedOrbits = GetCompletedOrbits(_currentReport);
            int requiredOrbits = _activeGoal != null && _activeGoal.RequiredOrbits > 0
                ? _activeGoal.RequiredOrbits
                : Math.Max(1, completedOrbits);

            if (missionContextSubtitleText != null)
            {
                missionContextSubtitleText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Contract: {0}   •   Orbit: {1} (Delay: {2:0.1}s)   •   Orbits: {3} / {4}",
                    missionTitle,
                    orbitName,
                    delaySec,
                    completedOrbits,
                    requiredOrbits);
            }

            RefreshTelemetryMetricsColumn(_currentReport);

            bool isSuccess = _currentReport.Status == MissionStatus.Success;
            if (successStateRoot != null) successStateRoot.SetActive(isSuccess);
            if (failureStateRoot != null) failureStateRoot.SetActive(!isSuccess);
            ApplyVerificationVisibility();

            if (isSuccess)
            {
                RefreshSuccessState(_currentReport);
            }
            else
            {
                RefreshFailureState(_currentReport);
            }
        }

        private void RefreshTelemetryMetricsColumn(MissionReport report)
        {
            float targetScience = _activeGoal != null && _activeGoal.TargetSciencePoints > 0f
                ? _activeGoal.TargetSciencePoints
                : 100.0f;
            float scienceRatio = Mathf.Clamp01(report.DeliveredScience / Mathf.Max(1f, targetScience));
            bool scienceMet = report.DeliveredScience + 0.01f >= targetScience;

            if (scienceMetricValueText != null)
            {
                scienceMetricValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0} / {1:0.0} pts",
                    report.DeliveredScience,
                    targetScience);
                scienceMetricValueText.color = scienceMet
                    ? new Color(0.35f, 0.96f, 0.58f, 1f)
                    : new Color(1.0f, 0.45f, 0.42f, 1f);
            }
            if (scienceMetricDetailText != null)
            {
                scienceMetricDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Downlinked Quota: {0:0}%   •   Lost SSD Overflow Data: {1:0.0} Gb",
                    scienceRatio * 100.0f,
                    report.LostDataGb);
            }
            if (scienceMetricBarFill != null)
            {
                scienceMetricBarFill.fillAmount = scienceRatio;
                scienceMetricBarFill.color = scienceMet
                    ? new Color(0.22f, 0.84f, 0.48f, 1f)
                    : new Color(0.92f, 0.32f, 0.30f, 1f);
            }

            float budgetLimit = _activeGoal != null && _activeGoal.BudgetLimitThousands > 0f
                ? _activeGoal.BudgetLimitThousands
                : 120.0f;
            float budgetSpent = report.BudgetSpentThousands > 0f
                ? report.BudgetSpentThousands
                : (_activeResources != null ? _activeResources.TotalCostThousands : 90.0f);
            float budgetSaved = Mathf.Max(0f, budgetLimit - budgetSpent);
            float budgetRatio = Mathf.Clamp01(budgetSpent / Mathf.Max(1f, budgetLimit));
            bool budgetValid = budgetSpent <= budgetLimit + 0.01f;

            if (budgetMetricValueText != null)
            {
                budgetMetricValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "${0:0.0}k / ${1:0.0}k",
                    budgetSpent,
                    budgetLimit);
                budgetMetricValueText.color = budgetValid
                    ? new Color(0.45f, 0.88f, 1.0f, 1f)
                    : new Color(1.0f, 0.45f, 0.42f, 1f);
            }
            if (budgetMetricDetailText != null)
            {
                budgetMetricDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Unspent Margin: ${0:0.0}k   •   Efficiency Score Bonus: +{1:0.0} pts",
                    budgetSaved,
                    budgetSaved * 0.5f);
            }
            if (budgetMetricBarFill != null)
            {
                budgetMetricBarFill.fillAmount = budgetRatio;
                budgetMetricBarFill.color = budgetValid
                    ? new Color(0.24f, 0.72f, 0.96f, 1f)
                    : new Color(0.92f, 0.32f, 0.30f, 1f);
            }

            float minBat = Mathf.Clamp(report.MinBatteryPercent, 0f, 100f);
            bool batMarginMet = minBat >= MinBatteryBonusThreshold;
            if (minBatteryMetricValueText != null)
            {
                minBatteryMetricValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", minBat);
                minBatteryMetricValueText.color = minBat <= 0.01f
                    ? new Color(1.0f, 0.36f, 0.36f, 1f)
                    : (batMarginMet ? new Color(0.35f, 0.96f, 0.58f, 1f) : new Color(1.0f, 0.78f, 0.28f, 1f));
            }
            if (minBatteryMetricDetailText != null)
            {
                minBatteryMetricDetailText.text = minBat <= 0.01f
                    ? "CRITICAL: Battery depleted to 0% during orbital eclipse"
                    : (batMarginMet
                        ? "PASS: Above 15.0% NASA reserve threshold (Reliability Bonus OK)"
                        : "WARNING: Dropped below 15.0% reserve threshold (No +50 Bonus)");
            }
            if (minBatteryMetricBarFill != null)
            {
                minBatteryMetricBarFill.fillAmount = minBat / 100.0f;
                minBatteryMetricBarFill.color = minBat <= 0.01f
                    ? new Color(0.92f, 0.26f, 0.26f, 1f)
                    : (batMarginMet ? new Color(0.22f, 0.84f, 0.48f, 1f) : new Color(0.95f, 0.68f, 0.20f, 1f));
            }

            float finalHealth = Mathf.Clamp(report.FinalHealthPercent, 0f, 100f);
            bool healthMarginMet = finalHealth >= MinHealthBonusThreshold;
            if (finalHealthMetricValueText != null)
            {
                finalHealthMetricValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", finalHealth);
                finalHealthMetricValueText.color = finalHealth <= 0.01f
                    ? new Color(1.0f, 0.36f, 0.36f, 1f)
                    : (healthMarginMet ? new Color(0.35f, 0.96f, 0.58f, 1f) : new Color(1.0f, 0.78f, 0.28f, 1f));
            }
            if (finalHealthMetricDetailText != null)
            {
                finalHealthMetricDetailText.text = finalHealth <= 0.01f
                    ? "CRITICAL: Avionics destroyed by radiation (0% Health)"
                    : (healthMarginMet
                        ? "PASS: Above 80.0% electronics integrity threshold (Reliability Bonus OK)"
                        : "WARNING: Degraded below 80.0% integrity threshold (No +50 Bonus)");
            }
            if (finalHealthMetricBarFill != null)
            {
                finalHealthMetricBarFill.fillAmount = finalHealth / 100.0f;
                finalHealthMetricBarFill.color = finalHealth <= 0.01f
                    ? new Color(0.92f, 0.26f, 0.26f, 1f)
                    : (healthMarginMet ? new Color(0.22f, 0.84f, 0.48f, 1f) : new Color(0.95f, 0.68f, 0.20f, 1f));
            }

            float reliability = _activeResources != null && _activeResources.OverallReliability > 0f
                ? _activeResources.OverallReliability
                : 0.92f;
            float reliabilityPercent = Mathf.Clamp(reliability * 100.0f, 0f, 100f);
            bool relMarginMet = reliability >= MinReliabilityBonusThreshold;

            if (reliabilityMetricValueText != null)
            {
                reliabilityMetricValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", reliabilityPercent);
                reliabilityMetricValueText.color = relMarginMet
                    ? new Color(0.35f, 0.96f, 0.58f, 1f)
                    : new Color(1.0f, 0.78f, 0.28f, 1f);
            }
            if (reliabilityMetricDetailText != null)
            {
                reliabilityMetricDetailText.text = relMarginMet
                    ? "PASS: Spacecraft assembly reliability >= 85.0% NASA standard"
                    : "WARNING: Spacecraft assembly reliability < 85.0% threshold";
            }
            if (reliabilityMetricBarFill != null)
            {
                reliabilityMetricBarFill.fillAmount = reliabilityPercent / 100.0f;
                reliabilityMetricBarFill.color = relMarginMet
                    ? new Color(0.22f, 0.84f, 0.48f, 1f)
                    : new Color(0.95f, 0.68f, 0.20f, 1f);
            }
        }

        private void RefreshSuccessState(MissionReport report)
        {
            float budgetLimit = _activeGoal != null && _activeGoal.BudgetLimitThousands > 0f
                ? _activeGoal.BudgetLimitThousands
                : 120.0f;
            float budgetSpent = report.BudgetSpentThousands > 0f
                ? report.BudgetSpentThousands
                : (_activeResources != null ? _activeResources.TotalCostThousands : 90.0f);
            float budgetBonus = Mathf.Max(0f, (budgetLimit - budgetSpent) * 0.5f);

            float overallReliability = _activeResources != null && _activeResources.OverallReliability > 0f
                ? _activeResources.OverallReliability
                : 0.92f;

            bool reliabilityBonusEarned =
                report.MinBatteryPercent >= MinBatteryBonusThreshold &&
                report.FinalHealthPercent >= MinHealthBonusThreshold &&
                overallReliability >= MinReliabilityBonusThreshold;

            float bonusPoints = reliabilityBonusEarned ? ReliabilityBonusPoints : 0.0f;
            float calculatedScore = report.DeliveredScience + budgetBonus + bonusPoints;
            float displayScore = report.FinalScore > 0.01f ? report.FinalScore : calculatedScore;

            string gradeLetter = GetMissionGrade(displayScore, reliabilityBonusEarned);

            if (statusBadgeBackground != null)
            {
                statusBadgeBackground.color = new Color(0.12f, 0.48f, 0.28f, 1f);
            }
            if (statusBadgeText != null)
            {
                statusBadgeText.text = "STATUS: MISSION SUCCESS";
                statusBadgeText.color = new Color(0.85f, 1.0f, 0.90f, 1f);
            }

            if (outcomeBannerBackground != null)
            {
                outcomeBannerBackground.color = new Color(0.08f, 0.26f, 0.18f, 0.96f);
            }
            if (outcomeHeadlineText != null)
            {
                outcomeHeadlineText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "MISSION ACCOMPLISHED  •  FINAL SCORE: {0:0.0} PTS  ({1})",
                    displayScore,
                    gradeLetter);
                outcomeHeadlineText.color = new Color(0.42f, 0.98f, 0.64f, 1f);
            }
            if (outcomeSublineText != null)
            {
                outcomeSublineText.text =
                    "All orbital objectives verified by NASA Ground Control. Science data downlinked and contract reward credited.";
                outcomeSublineText.color = new Color(0.82f, 0.95f, 0.88f, 1f);
            }

            if (finalScoreValueText != null)
            {
                finalScoreValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.0} PTS", displayScore);
            }

            if (ratingGradeBadgeBg != null)
            {
                ratingGradeBadgeBg.color = reliabilityBonusEarned
                    ? new Color(0.14f, 0.54f, 0.32f, 1f)
                    : new Color(0.20f, 0.44f, 0.68f, 1f);
            }
            if (ratingGradeBadgeText != null)
            {
                ratingGradeBadgeText.text = gradeLetter;
            }

            if (scoreFormulaBreakdownText != null)
            {
                scoreFormulaBreakdownText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "• Delivered Science Points: <b>+{0:0.0} pts</b>\n" +
                    "• Budget Efficiency Bonus (50% of ${1:0.0}k saved): <b>+{2:0.0} pts</b>\n" +
                    "• Engineering Reliability Margin Bonus: <b>+{3:0.0} pts</b>",
                    report.DeliveredScience,
                    Mathf.Max(0f, budgetLimit - budgetSpent),
                    budgetBonus,
                    bonusPoints);
            }

            if (reliabilityBonusCardBg != null)
            {
                reliabilityBonusCardBg.color = reliabilityBonusEarned
                    ? new Color(0.09f, 0.30f, 0.20f, 0.95f)
                    : new Color(0.28f, 0.22f, 0.10f, 0.95f);
            }
            if (reliabilityBonusTitleText != null)
            {
                reliabilityBonusTitleText.text = reliabilityBonusEarned
                    ? "RELIABILITY MARGIN BONUS: +50.0 PTS AWARDED"
                    : "RELIABILITY MARGIN BONUS: +0.0 PTS (THRESHOLD MISSED)";
                reliabilityBonusTitleText.color = reliabilityBonusEarned
                    ? new Color(0.42f, 0.98f, 0.64f, 1f)
                    : new Color(1.0f, 0.82f, 0.38f, 1f);
            }
            if (reliabilityBonusDetailText != null)
            {
                reliabilityBonusDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Conditions: Min Battery {0:0.0}% (req >= 15%)  •  Final Health {1:0.0}% (req >= 80%)  •  Reliability {2:0.0}% (req >= 85%)",
                    report.MinBatteryPercent,
                    report.FinalHealthPercent,
                    overallReliability * 100.0f);
            }

            int rewardThousands = _activeGoal != null ? _activeGoal.RewardThousands : 130;
            int passiveThousands = _activeGoal != null ? _activeGoal.PassiveIncomeThousands : 20;
            float currentCapital = _agencyState != null ? _agencyState.CapitalThousands : 200.0f;
            int totalPassive = _agencyState != null ? _agencyState.TotalPassiveIncomeThousands : passiveThousands;

            if (contractRewardValueText != null)
            {
                contractRewardValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "+${0}k Contract Payout",
                    rewardThousands);
            }
            if (passiveIncomeValueText != null)
            {
                passiveIncomeValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "+${0}k / Flight Passive Income",
                    passiveThousands);
            }
            if (agencyCapitalSummaryText != null)
            {
                agencyCapitalSummaryText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Total Payout Credited: <b>+${0}k</b> (Contract +${1}k + Passive Income +${2}k)\n" +
                    "Agency Balance: <b>${3:0}k</b>   •   Active Passive Income Stream: <b>+${4}k / mission</b>",
                    rewardThousands + passiveThousands,
                    rewardThousands,
                    passiveThousands,
                    currentCapital,
                    totalPassive);
            }
        }

        private void RefreshFailureState(MissionReport report)
        {
            EnsureFailureDebriefPopulated(report);

            string failTitle = !string.IsNullOrWhiteSpace(report.FailureTitle)
                ? report.FailureTitle
                : FailureDebriefBuilder.GetTitle(FailureScenarioType.InsufficientScienceNoLoss);

            string failCause = !string.IsNullOrWhiteSpace(report.FailureCause)
                ? report.FailureCause
                : "Telemetry stream ended before mission objectives were satisfied.";

            string failRecommendation = !string.IsNullOrWhiteSpace(report.FailureRecommendation)
                ? report.FailureRecommendation
                : FailureDebriefBuilder.GetRecommendation(FailureScenarioType.InsufficientScienceNoLoss);

            if (statusBadgeBackground != null)
            {
                statusBadgeBackground.color = new Color(0.58f, 0.14f, 0.16f, 1f);
            }
            if (statusBadgeText != null)
            {
                statusBadgeText.text = "STATUS: MISSION FAILED";
                statusBadgeText.color = new Color(1.0f, 0.88f, 0.88f, 1f);
            }

            if (outcomeBannerBackground != null)
            {
                outcomeBannerBackground.color = new Color(0.32f, 0.09f, 0.12f, 0.96f);
            }
            if (outcomeHeadlineText != null)
            {
                outcomeHeadlineText.text = "WHY DID IT FAIL?  •  A.S.T.R.A. POST-FLIGHT ANOMALY INVESTIGATION";
                outcomeHeadlineText.color = new Color(1.0f, 0.48f, 0.46f, 1f);
            }
            if (outcomeSublineText != null)
            {
                outcomeSublineText.text =
                    "Review the exact orbital telemetry numbers below and return to the Hangar for free to update your satellite or autopilot rules.";
                outcomeSublineText.color = new Color(0.96f, 0.84f, 0.84f, 1f);
            }

            if (failureTitleBannerBg != null)
            {
                failureTitleBannerBg.color = new Color(0.42f, 0.11f, 0.14f, 0.98f);
            }
            if (failureTitleText != null)
            {
                failureTitleText.text = failTitle;
            }
            if (failureCauseText != null)
            {
                failureCauseText.text = failCause;
            }
            if (failureRecommendationText != null)
            {
                failureRecommendationText.text = failRecommendation;
            }

            if (failureTelemetryNoteText != null)
            {
                int lastOrbit = GetCompletedOrbits(report);
                int lastTick = GetLastTickIndex(report);
                failureTelemetryNoteText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Last Recorded Telemetry: Orbit <b>{0}</b> (Tick <b>{1}</b>)   •   Delivered Science: <b>{2:0.0} pts</b>   •   " +
                    "Lost SSD Data: <b>{3:0.0} Gb</b>\n" +
                    "<color=#7CE89A><b>FREE RETRY POLICY:</b> $0k deducted from Agency Capital. Modify your components or autopilot rules in the Hangar at no penalty.</color>",
                    lastOrbit,
                    lastTick,
                    report.DeliveredScience,
                    report.LostDataGb);
            }
        }

        private static string GetMissionGrade(float score, bool reliabilityBonusEarned)
        {
            if (reliabilityBonusEarned && score >= 150.0f)
            {
                return "GRADE S  •  EXEMPLARY";
            }
            if (score >= 120.0f)
            {
                return "GRADE A  •  OPTIMAL";
            }
            return "GRADE B  •  QUALIFIED";
        }

        private static int GetCompletedOrbits(MissionReport report)
        {
            if (report == null || report.Ticks == null || report.Ticks.Count == 0)
            {
                return 1;
            }
            SimulationTickSnapshot last = report.Ticks[report.Ticks.Count - 1];
            return last != null && last.OrbitNumber > 0 ? last.OrbitNumber : 1;
        }

        private static int GetLastTickIndex(MissionReport report)
        {
            if (report == null || report.Ticks == null || report.Ticks.Count == 0)
            {
                return 0;
            }
            SimulationTickSnapshot last = report.Ticks[report.Ticks.Count - 1];
            return last != null ? last.TickIndex : (report.Ticks.Count - 1);
        }

        // --- Sample Scenarios Powered by AutopilotEvaluator (S2-01) and FailureDebriefBuilder (S2-02) ---

        /// <summary>
        /// Triggers a sample successful mission report (with or without the +50 reliability bonus) and switches to the S2-05 Debrief Summary view.
        /// </summary>
        public void TriggerSampleSuccessScenario(bool withReliabilityBonus)
        {
            ResolveContextFromScene();
            SetViewMode(false);
            MissionReport report = BuildSampleSuccessReport(withReliabilityBonus);
            ShowReport(report, _activeGoal, _activeResources, _activeOrbit, false);
        }

        /// <summary>
        /// Triggers one of the deterministic failure scenarios, populates it using <see cref="FailureDebriefBuilder"/> (S2-02),
        /// and switches to the S2-05 Debrief Summary view.
        /// </summary>
        public void TriggerSampleFailureScenario(FailureScenarioType scenarioType)
        {
            ResolveContextFromScene();
            SetViewMode(false);
            MissionReport report = BuildSampleFailureReport(scenarioType);
            ShowReport(report, _activeGoal, _activeResources, _activeOrbit, false);
        }

        private MissionReport BuildFullFlightSampleReport(
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules,
            List<ManualCommandRequest> manualCommands,
            List<SpaceEventDefinition> events)
        {
            const int ticksPerOrbit = 20;
            OrbitDefinition activeOrbit = orbit ?? OrbitRadarUI.CreateDefaultLeoOrbit();
            MissionGoal activeGoal = goal ?? BuildDefaultMissionGoal();

            // Always invoke IMissionSimulator.SimulateFullMission directly when real simulator is available
            if (_simulator != null && !(_simulator is MockMissionSimulator))
            {
                MissionReport simReport = _simulator.SimulateFullMission(
                    _activeComponents,
                    _activeRocket,
                    activeOrbit,
                    activeGoal,
                    rules,
                    manualCommands,
                    events);

                if (simReport != null)
                {
                    return simReport;
                }
            }

            float maxBatWh = (_activeResources != null && _activeResources.MaxBatteryWh > 0f) ? _activeResources.MaxBatteryWh : 45.0f;
            float maxSsdGb = (_activeResources != null && _activeResources.MaxSsdGb > 0f) ? _activeResources.MaxSsdGb : 16.0f;
            float maxSolarW = (_activeResources != null && _activeResources.MaxSolarWatts > 0f) ? _activeResources.MaxSolarWatts : 45.0f;
            float downlinkPerTick = (_activeResources != null && _activeResources.DownlinkGbPerTick > 0f) ? _activeResources.DownlinkGbPerTick : 2.5f;
            float shielding = (_activeResources != null && _activeResources.TotalShielding > 0f) ? _activeResources.TotalShielding : 15.0f;
            float tickHours = activeOrbit.OrbitPeriodHours / ticksPerOrbit;

            float batteryWh = maxBatWh;
            float minBatteryPct = 100.0f;
            float ssdUsedGb = 1.2f;
            float pendingScience = 12.0f;
            float deliveredScience = 0.0f;
            float health = 100.0f;
            bool scienceActive = true;
            bool commsActive = true;
            bool safeModeActive = false;

            var report = new MissionReport
            {
                Status = MissionStatus.Success,
                BudgetSpentThousands = 90.0f,
                Ticks = new List<SimulationTickSnapshot>(ticksPerOrbit)
            };

            for (int tick = 0; tick < ticksPerOrbit; tick++)
            {
                float progress = tick / (float)ticksPerOrbit;
                bool isInEclipse = progress >= (1.0f - activeOrbit.EclipseFraction - 0.0001f);
                bool isInCommWindow = progress < (activeOrbit.CommWindowFraction - 0.0001f);

                float eventRadBoost = 0f;
                float eventSolarMult = 1f;
                float eventDownlinkMult = 1f;
                string activeEventTitle = string.Empty;

                if (events != null)
                {
                    for (int e = 0; e < events.Count; e++)
                    {
                        SpaceEventDefinition ev = events[e];
                        if (ev == null) continue;
                        int startTick = 4;
                        int duration = ev.DurationTicks > 0 ? ev.DurationTicks : 7;
                        if (tick >= startTick && tick < startTick + duration)
                        {
                            eventRadBoost += ev.RadiationBoost;
                            eventSolarMult *= Mathf.Max(0.1f, ev.SolarMultiplier);
                            eventDownlinkMult *= Mathf.Max(0.1f, ev.DownlinkMultiplier);
                            activeEventTitle = !string.IsNullOrEmpty(ev.Title) ? ev.Title : ev.Id;
                        }
                    }
                }

                if (manualCommands != null && manualCommands.Count > 0)
                {
                    _autopilotEvaluator.ApplyManualCommandsForTick(tick, manualCommands, ref scienceActive, ref commsActive, ref safeModeActive);
                }

                float solarGenBeforeRules = isInEclipse ? 0.0f : (maxSolarW * activeOrbit.SolarFluxFactor * eventSolarMult);
                float batPctBeforeRules = 100.0f * batteryWh / maxBatWh;
                float ssdPctBeforeRules = 100.0f * ssdUsedGb / maxSsdGb;
                float radiation = activeOrbit.BaseRadiation + eventRadBoost;

                var triggeredRuleIds = new List<string>();
                if (rules != null && rules.Count > 0)
                {
                    _autopilotEvaluator.EvaluateRulesForTick(
                        rules,
                        solarGenBeforeRules,
                        batPctBeforeRules,
                        ssdPctBeforeRules,
                        radiation,
                        ref scienceActive,
                        ref commsActive,
                        ref safeModeActive,
                        triggeredRuleIds);
                }

                // Re-apply manual commands at or after ExecuteAtTick so manual toggles remain authoritative
                if (manualCommands != null && manualCommands.Count > 0)
                {
                    for (int m = 0; m < manualCommands.Count; m++)
                    {
                        ManualCommandRequest cmd = manualCommands[m];
                        if (cmd != null && tick >= cmd.ExecuteAtTick)
                        {
                            AutopilotEvaluator.ApplyAction(cmd.Action, ref scienceActive, ref commsActive, ref safeModeActive);
                        }
                    }
                }

                AutopilotEvaluator.ResolveEffectiveSubsystems(scienceActive, commsActive, safeModeActive, out bool effScience, out bool effComms);

                float drawWatts = safeModeActive
                    ? 5.0f
                    : (8.0f + (effScience ? 14.0f : 0.0f) + (effComms && isInCommWindow ? 10.0f : 0.0f));

                batteryWh = Mathf.Clamp(batteryWh + (solarGenBeforeRules - drawWatts) * tickHours, 0.0f, maxBatWh);
                float batteryPct = 100.0f * batteryWh / maxBatWh;
                minBatteryPct = Mathf.Min(minBatteryPct, batteryPct);

                if (effScience && batteryWh > 0.0f)
                {
                    ssdUsedGb = Mathf.Min(maxSsdGb, ssdUsedGb + 1.1f);
                    pendingScience += 9.5f;
                }

                if (effComms && isInCommWindow && ssdUsedGb > 0.01f && batteryWh > 0.0f)
                {
                    float sentGb = Mathf.Min(ssdUsedGb, downlinkPerTick * eventDownlinkMult);
                    float deliveredNow = pendingScience * (sentGb / ssdUsedGb);
                    deliveredScience += deliveredNow;
                    pendingScience = Mathf.Max(0f, pendingScience - deliveredNow);
                    ssdUsedGb = Mathf.Max(0f, ssdUsedGb - sentGb);
                }

                if (radiation > 50.0f && !safeModeActive && batteryWh > 0.0f)
                {
                    health = Mathf.Max(0.0f, health - Mathf.Max(0.0f, (radiation - shielding) * 0.25f));
                }

                report.Ticks.Add(new SimulationTickSnapshot
                {
                    TickIndex = tick,
                    OrbitNumber = 1,
                    OrbitProgress = progress,
                    IsInEclipse = isInEclipse,
                    IsInCommWindow = isInCommWindow,
                    SafeModeActive = safeModeActive,
                    ScienceActive = effScience,
                    CommsActive = effComms,
                    SolarGenWatts = solarGenBeforeRules,
                    PowerDrawWatts = drawWatts,
                    BatteryWh = batteryWh,
                    BatteryPercent = batteryPct,
                    SsdUsedGb = ssdUsedGb,
                    SsdUsedPercent = 100.0f * ssdUsedGb / maxSsdGb,
                    RadiationLevel = radiation,
                    HealthPercent = health,
                    DeliveredScience = deliveredScience,
                    ActiveEventTitle = activeEventTitle,
                    TriggeredRuleIds = triggeredRuleIds
                });
            }

            report.DeliveredScience = Mathf.Max(deliveredScience, activeGoal.TargetSciencePoints + 20.0f);
            report.MinBatteryPercent = minBatteryPct;
            report.FinalHealthPercent = health;
            report.FinalScore = report.DeliveredScience + Mathf.Max(0f, (activeGoal.BudgetLimitThousands - report.BudgetSpentThousands) * 0.5f) + ReliabilityBonusPoints;

            if (report.Ticks.Count > 0)
            {
                report.Ticks[report.Ticks.Count - 1].DeliveredScience = report.DeliveredScience;
            }

            return report;
        }

        private MissionReport BuildSampleSuccessReport(bool withReliabilityBonus)
        {
            MissionReport report = BuildFullFlightSampleReport(_activeOrbit, _activeGoal, _activeRules, activeManualCommands, _activeEvents);
            float targetScience = _activeGoal != null && _activeGoal.TargetSciencePoints > 0f
                ? _activeGoal.TargetSciencePoints
                : 100.0f;
            float budgetLimit = _activeGoal != null && _activeGoal.BudgetLimitThousands > 0f
                ? _activeGoal.BudgetLimitThousands
                : 120.0f;
            float budgetSpent = 90.0f;
            float deliveredScience = targetScience + 20.0f;
            float minBattery = withReliabilityBonus ? 60.0f : 9.5f;
            float finalHealth = withReliabilityBonus ? 100.0f : 76.0f;
            float bonus = withReliabilityBonus ? ReliabilityBonusPoints : 0.0f;
            float finalScore = deliveredScience + Mathf.Max(0f, (budgetLimit - budgetSpent) * 0.5f) + bonus;

            report.Status = MissionStatus.Success;
            report.FinalScore = finalScore;
            report.DeliveredScience = deliveredScience;
            report.LostDataGb = 0.0f;
            report.BudgetSpentThousands = budgetSpent;
            report.MinBatteryPercent = minBattery;
            report.FinalHealthPercent = finalHealth;
            report.FailureTitle = string.Empty;
            report.FailureCause = string.Empty;
            report.FailureRecommendation = string.Empty;
            return report;
        }

        private MissionReport BuildSampleFailureReport(FailureScenarioType scenarioType)
        {
            float targetScience = _activeGoal != null && _activeGoal.TargetSciencePoints > 0f
                ? _activeGoal.TargetSciencePoints
                : 100.0f;
            int requiredOrbits = _activeGoal != null && _activeGoal.RequiredOrbits > 0
                ? _activeGoal.RequiredOrbits
                : 4;

            var report = new MissionReport
            {
                Status = MissionStatus.Failed,
                FinalScore = 0.0f,
                BudgetSpentThousands = 90.0f,
                Ticks = new List<SimulationTickSnapshot>()
            };

            switch (scenarioType)
            {
                case FailureScenarioType.BatteryDepleted:
                    report.DeliveredScience = 32.5f;
                    report.LostDataGb = 0.0f;
                    report.MinBatteryPercent = 0.0f;
                    report.FinalHealthPercent = 100.0f;
                    report.Ticks.Add(new SimulationTickSnapshot
                    {
                        TickIndex = 11,
                        OrbitNumber = 1,
                        OrbitProgress = 0.75f,
                        IsInEclipse = true,
                        SolarGenWatts = 0.0f,
                        PowerDrawWatts = 27.0f,
                        BatteryWh = 0.0f,
                        BatteryPercent = 0.0f,
                        HealthPercent = 100.0f,
                        DeliveredScience = 32.5f
                    });
                    break;

                case FailureScenarioType.RadiationDamage:
                    report.DeliveredScience = 64.0f;
                    report.LostDataGb = 0.0f;
                    report.MinBatteryPercent = 48.0f;
                    report.FinalHealthPercent = 0.0f;
                    report.Ticks.Add(new SimulationTickSnapshot
                    {
                        TickIndex = 47,
                        OrbitNumber = 3,
                        OrbitProgress = 0.35f,
                        IsInEclipse = false,
                        SafeModeActive = false,
                        SolarGenWatts = 45.0f,
                        PowerDrawWatts = 24.0f,
                        BatteryWh = 22.0f,
                        BatteryPercent = 48.0f,
                        RadiationLevel = 105.0f,
                        HealthPercent = 0.0f,
                        DeliveredScience = 64.0f,
                        ActiveEventTitle = "Solar Storm (CME)"
                    });
                    break;

                case FailureScenarioType.InsufficientScienceWithSsdLoss:
                    report.DeliveredScience = targetScience * 0.62f;
                    report.LostDataGb = 6.4f;
                    report.MinBatteryPercent = 42.0f;
                    report.FinalHealthPercent = 96.0f;
                    report.Ticks.Add(new SimulationTickSnapshot
                    {
                        TickIndex = requiredOrbits * 20 - 1,
                        OrbitNumber = requiredOrbits,
                        OrbitProgress = 0.95f,
                        BatteryWh = 30.0f,
                        BatteryPercent = 66.0f,
                        SsdUsedGb = 16.0f,
                        SsdUsedPercent = 100.0f,
                        HealthPercent = 96.0f,
                        DeliveredScience = report.DeliveredScience
                    });
                    break;

                case FailureScenarioType.InsufficientScienceNoLoss:
                default:
                    report.DeliveredScience = targetScience * 0.70f;
                    report.LostDataGb = 0.0f;
                    report.MinBatteryPercent = 55.0f;
                    report.FinalHealthPercent = 100.0f;
                    report.Ticks.Add(new SimulationTickSnapshot
                    {
                        TickIndex = requiredOrbits * 20 - 1,
                        OrbitNumber = requiredOrbits,
                        OrbitProgress = 0.95f,
                        BatteryWh = 36.0f,
                        BatteryPercent = 80.0f,
                        SsdUsedGb = 2.0f,
                        SsdUsedPercent = 12.5f,
                        HealthPercent = 100.0f,
                        DeliveredScience = report.DeliveredScience
                    });
                    break;
            }

            _debriefBuilder.BuildDebrief(report, _activeGoal, _activeResources, _activeOrbit);
            return report;
        }

        private static List<SpaceEventDefinition> BuildDefaultEventsForMission(MissionGoal goal)
        {
            var list = new List<SpaceEventDefinition>();
            if (goal == null || string.IsNullOrWhiteSpace(goal.EventId))
            {
                return list;
            }

            if (string.Equals(goal.EventId, "evt_tx_overheat", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new SpaceEventDefinition
                {
                    Id = "evt_tx_overheat",
                    Title = "Transmitter Overheat (Downlink 45%)",
                    Description = "Thermal spike throttles ground downlink bandwidth to 45%.",
                    TriggerOrbit = 1,
                    DurationTicks = 8,
                    RadiationBoost = 0.0f,
                    SolarMultiplier = 1.0f,
                    DownlinkMultiplier = 0.45f
                });
            }
            else if (string.Equals(goal.EventId, "evt_cme_storm", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new SpaceEventDefinition
                {
                    Id = "evt_cme_storm",
                    Title = "Solar Storm (CME) Radiation Surge",
                    Description = "Coronal Mass Ejection increases orbital radiation by +85 rads.",
                    TriggerOrbit = 1,
                    DurationTicks = 8,
                    RadiationBoost = 85.0f,
                    SolarMultiplier = 1.0f,
                    DownlinkMultiplier = 1.0f
                });
            }

            return list;
        }

        private static List<SatelliteComponent> BuildDefaultStarterComponents()
        {
            return new List<SatelliteComponent>
            {
                new SatelliteComponent { Id = "bus_1", Name = "CubeSat 3U Bus", Category = ComponentCategory.CorePlatform, MassKg = 3f, CostThousands = 12f, BasePowerDrawWatts = 4f, RadiationShielding = 15f, BaseReliability = 0.98f },
                new SatelliteComponent { Id = "pwr_1", Name = "Deployable Solar Array", Category = ComponentCategory.PowerGeneration, MassKg = 3f, CostThousands = 15f, SolarWatts = 45f, BaseReliability = 0.98f },
                new SatelliteComponent { Id = "bat_1", Name = "Li-Ion Battery Pack", Category = ComponentCategory.EnergyStorage, MassKg = 3f, CostThousands = 14f, BatteryCapacityWh = 45f, BaseReliability = 0.99f },
                new SatelliteComponent { Id = "com_1", Name = "S-Band Transceiver + 16GB SSD", Category = ComponentCategory.CommsAndStorage, MassKg = 2f, CostThousands = 14f, ActivePowerDrawWatts = 8f, SsdCapacityGb = 16f, DownlinkRateGbPerTick = 2.5f, BaseReliability = 0.98f },
                new SatelliteComponent { Id = "sci_1", Name = "Optical Weather Camera", Category = ComponentCategory.SciencePayload, MassKg = 3f, CostThousands = 15f, ActivePowerDrawWatts = 12f, DataGenGbPerTick = 1.0f, SciencePointsPerTick = 9f, SensorTag = "Camera", BaseReliability = 0.98f }
            };
        }

        private static RocketOption BuildDefaultStarterRocket()
        {
            return new RocketOption
            {
                Id = "rkt_light",
                Name = "Arrow-1 Light Launcher",
                CostThousands = 20f,
                MaxMassLeoKg = 25f,
                MaxMassLunarKg = 15f,
                MaxMassMarsKg = 10f,
                MaxMassDeepSpaceKg = 6f,
                BaseReliability = 0.96f
            };
        }

        private static MissionGoal BuildDefaultMissionGoal()
        {
            return new MissionGoal
            {
                Id = "m1_leo_cubesat",
                Title = "1. LEO Weather CubeSat",
                Brief = "Launch a weather satellite to 400 km LEO, deliver 100 science points, and survive 4 orbits.",
                OrbitId = "orbit_leo",
                BudgetLimitThousands = 120.0f,
                TargetSciencePoints = 100.0f,
                RequiredOrbits = 4,
                RewardThousands = 130,
                PassiveIncomeThousands = 20,
                MinChipSlots = 2,
                RequiredSensorTag = "Camera",
                EventId = string.Empty
            };
        }

        private static OrbitDefinition BuildDefaultOrbitForGoal(MissionGoal goal)
        {
            string orbitId = goal != null ? goal.OrbitId : "orbit_leo";
            if (string.Equals(orbitId, "orbit_lunar", StringComparison.OrdinalIgnoreCase))
            {
                return new OrbitDefinition
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
                };
            }
            if (string.Equals(orbitId, "orbit_mars", StringComparison.OrdinalIgnoreCase))
            {
                return new OrbitDefinition
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
                };
            }
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

        // --- UI Hierarchy Builder & Binding ---

        private void BindButtons()
        {
            if (returnToHubButton != null)
            {
                returnToHubButton.onClick.RemoveListener(ReturnToHub);
                returnToHubButton.onClick.AddListener(ReturnToHub);
            }

            if (returnToWorkshopButton != null)
            {
                returnToWorkshopButton.onClick.RemoveListener(ReturnToWorkshopFree);
                returnToWorkshopButton.onClick.AddListener(ReturnToWorkshopFree);
            }

            if (switchToLiveFlightButton != null)
            {
                switchToLiveFlightButton.onClick.RemoveListener(ShowLiveFlightView);
                switchToLiveFlightButton.onClick.AddListener(ShowLiveFlightView);
            }

            if (switchToDebriefButton != null)
            {
                switchToDebriefButton.onClick.RemoveListener(ShowDebriefSummaryView);
                switchToDebriefButton.onClick.AddListener(ShowDebriefSummaryView);
            }

            if (playbackSpeed1xButton != null)
            {
                playbackSpeed1xButton.onClick.RemoveListener(SetPlaybackSpeed1x);
                playbackSpeed1xButton.onClick.AddListener(SetPlaybackSpeed1x);
            }

            if (playbackSpeed5xButton != null)
            {
                playbackSpeed5xButton.onClick.RemoveListener(SetPlaybackSpeed5x);
                playbackSpeed5xButton.onClick.AddListener(SetPlaybackSpeed5x);
            }

            if (playbackSpeed10xButton != null)
            {
                playbackSpeed10xButton.onClick.RemoveListener(SetPlaybackSpeed10x);
                playbackSpeed10xButton.onClick.AddListener(SetPlaybackSpeed10x);
            }

            if (playbackPlayPauseButton != null)
            {
                playbackPlayPauseButton.onClick.RemoveListener(TogglePlaybackPlayPause);
                playbackPlayPauseButton.onClick.AddListener(TogglePlaybackPlayPause);
            }

            if (playbackStepTickButton != null)
            {
                playbackStepTickButton.onClick.RemoveListener(StepPlaybackNextTick);
                playbackStepTickButton.onClick.AddListener(StepPlaybackNextTick);
            }
        }

        private void ApplyVerificationVisibility()
        {
            if (verificationBarRoot != null) verificationBarRoot.SetActive(showVerificationBar);
            if (_sampleOrbitLeoBtn != null) _sampleOrbitLeoBtn.SetActive(showVerificationBar);
            if (_sampleOrbitLunarBtn != null) _sampleOrbitLunarBtn.SetActive(showVerificationBar);
            if (_sampleOrbitMarsBtn != null) _sampleOrbitMarsBtn.SetActive(showVerificationBar);
        }

        private void Update()
        {
            if (!showVerificationBar) return;

            if (Input.GetKeyDown(KeyCode.F5)) TriggerSampleSuccessScenario(true);
            else if (Input.GetKeyDown(KeyCode.F6)) TriggerSampleSuccessScenario(false);
            else if (Input.GetKeyDown(KeyCode.F7)) TriggerSampleFailureScenario(FailureScenarioType.BatteryDepleted);
            else if (Input.GetKeyDown(KeyCode.F8)) TriggerSampleFailureScenario(FailureScenarioType.RadiationDamage);
            else if (Input.GetKeyDown(KeyCode.F9)) TriggerSampleFailureScenario(FailureScenarioType.InsufficientScienceWithSsdLoss);
            else if (Input.GetKeyDown(KeyCode.F10)) TriggerSampleFailureScenario(FailureScenarioType.InsufficientScienceNoLoss);
        }

        private void CleanupOldRoots()
        {
            string[] oldNames = { "DebriefContentRoot", "DebriefContentRoot_v2", "DebriefContentRoot_v3" };
            for (int i = 0; i < oldNames.Length; i++)
            {
                Transform old = transform.Find(oldNames[i]);
                if (old != null)
                {
                    DestroyImmediate(old.gameObject);
                }
            }
        }

        private void EnsureDebriefUIHierarchy()
        {
            CleanupOldRoots();

            Transform root = transform.Find("DebriefContentRoot_v4");
            if (root != null &&
                orbitRadarUI != null &&
                telemetryHUD != null &&
                manualOverrideUI != null &&
                eventPopupUI != null &&
                finalScoreValueText != null &&
                failureTitleText != null &&
                returnToWorkshopButton != null)
            {
                return;
            }

            if (root != null)
            {
                DestroyImmediate(root.gameObject);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            RectTransform selfRt = GetComponent<RectTransform>();
            if (selfRt == null)
            {
                selfRt = gameObject.AddComponent<RectTransform>();
            }
            StretchFull(selfRt);

            Image baseBg = GetComponent<Image>();
            if (baseBg == null)
            {
                baseBg = gameObject.AddComponent<Image>();
            }
            baseBg.color = new Color(0.04f, 0.07f, 0.13f, 1f);

            GameObject contentRoot = CreateUIObject("DebriefContentRoot_v4", transform);
            StretchFull(contentRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup rootLayout = contentRoot.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(18, 18, 8, 8);
            rootLayout.spacing = 6f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // 1. Top Header Bar (with S2-04 Live Flight vs S2-05 Debrief Mode Switcher)
            BuildTopHeaderBar(contentRoot.transform, font);

            // 2. Top Verification & Scenario Switcher Bar
            BuildVerificationSwitcherBar(contentRoot.transform, font);

            // 3. Mode Container (holds S2-04 Live Flight View and S2-05 Post-Flight Debrief View)
            GameObject modesBody = CreateUIObject("ModesBodyContainer", contentRoot.transform);
            LayoutElement modesBodyLe = modesBody.AddComponent<LayoutElement>();
            modesBodyLe.flexibleHeight = 1f;
            modesBodyLe.preferredHeight = 820f;

            // 3A. S2-04 Live Flight Telemetry & Radar View
            BuildLiveFlightModeView(modesBody.transform, font);

            // 3B. S2-05 Post-Flight Debrief Summary View
            BuildDebriefSummaryModeView(modesBody.transform, font);
        }

        private void BuildTopHeaderBar(Transform parent, Font font)
        {
            GameObject headerBar = CreatePanel("HeaderBar", parent, new Color(0.07f, 0.12f, 0.21f, 0.98f), 50f);
            HorizontalLayoutGroup headerLayout = headerBar.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(14, 14, 5, 5);
            headerLayout.spacing = 10f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;

            GameObject titleStack = CreateUIObject("TitleStack", headerBar.transform);
            LayoutElement titleStackLe = titleStack.AddComponent<LayoutElement>();
            titleStackLe.flexibleWidth = 1f;

            VerticalLayoutGroup titleStackLayout = titleStack.AddComponent<VerticalLayoutGroup>();
            titleStackLayout.spacing = 1f;
            titleStackLayout.childControlWidth = true;
            titleStackLayout.childControlHeight = true;
            titleStackLayout.childForceExpandWidth = true;
            titleStackLayout.childForceExpandHeight = false;

            CreateText(
                "ScreenTitle",
                titleStack.transform,
                font,
                "ASTRA-LINK // FLIGHT TELEMETRY PLAYBACK (S2-04) & MISSION DEBRIEF (S2-05)",
                17,
                FontStyle.Bold,
                new Color(0.92f, 0.97f, 1.0f, 1f),
                TextAnchor.MiddleLeft);

            missionContextSubtitleText = CreateText(
                "MissionSubtitle",
                titleStack.transform,
                font,
                "Contract: 1. LEO Weather CubeSat   •   Orbit: LEO 400 km (Delay: 1.0s)",
                12,
                FontStyle.Normal,
                new Color(0.65f, 0.80f, 0.95f, 1f),
                TextAnchor.MiddleLeft);

            switchToLiveFlightButton = CreateActionButton(
                headerBar.transform,
                "Btn_Mode_LiveFlight_S204",
                "[S2-04] LIVE FLIGHT & RADAR",
                new Color(0.14f, 0.58f, 0.86f, 1f),
                245f,
                38f,
                font,
                ShowLiveFlightView);

            switchToDebriefButton = CreateActionButton(
                headerBar.transform,
                "Btn_Mode_Debrief_S205",
                "[S2-05] POST-FLIGHT DEBRIEF",
                new Color(0.12f, 0.18f, 0.28f, 0.96f),
                245f,
                38f,
                font,
                ShowDebriefSummaryView);

            GameObject badgeObj = CreatePanel("StatusBadge", headerBar.transform, new Color(0.12f, 0.48f, 0.28f, 1f), 38f);
            LayoutElement badgeLe = badgeObj.GetComponent<LayoutElement>();
            badgeLe.preferredWidth = 230f;
            statusBadgeBackground = badgeObj.GetComponent<Image>();

            statusBadgeText = CreateText(
                "StatusBadgeText",
                badgeObj.transform,
                font,
                "STATUS: MISSION SUCCESS",
                14,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            StretchFull(statusBadgeText.GetComponent<RectTransform>());
        }

        private void BuildLiveFlightModeView(Transform parent, Font font)
        {
            liveFlightModeRoot = CreateUIObject("LiveFlightModeView_S204", parent);
            StretchFull(liveFlightModeRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup vLayout = liveFlightModeRoot.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 6f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            // Top S2-04 Controls & Event Popup Row
            GameObject topControlsRow = CreateUIObject("FlightTopControlsAndEventRow", liveFlightModeRoot.transform);
            topControlsRow.AddComponent<LayoutElement>().preferredHeight = 52f;
            HorizontalLayoutGroup topRowLayout = topControlsRow.AddComponent<HorizontalLayoutGroup>();
            topRowLayout.spacing = 10f;
            topRowLayout.childControlWidth = true;
            topRowLayout.childControlHeight = true;
            topRowLayout.childForceExpandWidth = false;
            topRowLayout.childForceExpandHeight = true;

            // EventPopupUI (ActiveEventTitle notification banner)
            GameObject eventPopupObj = CreateUIObject("EventPopupUI", topControlsRow.transform);
            LayoutElement popupLe = eventPopupObj.AddComponent<LayoutElement>();
            popupLe.flexibleWidth = 1f;
            eventPopupUI = eventPopupObj.AddComponent<EventPopupUI>();
            eventPopupUI.EnsurePopupHierarchy();

            // Speed & Playback Bar (1x, 5x, 10x, Play/Pause, Step +1, Scenario Presets)
            GameObject speedBar = CreatePanel("PlaybackSpeedAndOrbitBar", topControlsRow.transform, new Color(0.07f, 0.12f, 0.21f, 0.98f), 52f);
            LayoutElement speedBarLe = speedBar.GetComponent<LayoutElement>();
            speedBarLe.preferredWidth = 820f;

            HorizontalLayoutGroup speedLayout = speedBar.AddComponent<HorizontalLayoutGroup>();
            speedLayout.padding = new RectOffset(10, 10, 6, 6);
            speedLayout.spacing = 6f;
            speedLayout.childAlignment = TextAnchor.MiddleCenter;
            speedLayout.childControlWidth = true;
            speedLayout.childControlHeight = true;
            speedLayout.childForceExpandWidth = false;
            speedLayout.childForceExpandHeight = true;

            _sampleOrbitLeoBtn = CreateActionButton(
                speedBar.transform,
                "Btn_Orbit_LEO",
                "LEO (1s Delay)",
                new Color(0.14f, 0.38f, 0.58f, 1f),
                122f,
                38f,
                font,
                () => StartSampleLiveFlightScenario(0)).gameObject;

            _sampleOrbitLunarBtn = CreateActionButton(
                speedBar.transform,
                "Btn_Orbit_Lunar",
                "MOON + OVERHEAT (3s)",
                new Color(0.42f, 0.30f, 0.14f, 1f),
                168f,
                38f,
                font,
                () => StartSampleLiveFlightScenario(1)).gameObject;

            _sampleOrbitMarsBtn = CreateActionButton(
                speedBar.transform,
                "Btn_Orbit_Mars",
                "MARS + CME STORM (10s)",
                new Color(0.56f, 0.20f, 0.18f, 1f),
                178f,
                38f,
                font,
                () => StartSampleLiveFlightScenario(2)).gameObject;

            ApplyVerificationVisibility();

            playbackPlayPauseButton = CreateActionButton(
                speedBar.transform,
                "Btn_PlayPause",
                "PAUSE",
                new Color(0.14f, 0.54f, 0.34f, 1f),
                74f,
                38f,
                font,
                TogglePlaybackPlayPause);
            playbackPlayPauseLabel = playbackPlayPauseButton.GetComponentInChildren<Text>();

            playbackStepTickButton = CreateActionButton(
                speedBar.transform,
                "Btn_StepTick",
                "STEP +1",
                new Color(0.22f, 0.32f, 0.48f, 1f),
                76f,
                38f,
                font,
                StepPlaybackNextTick);

            playbackSpeed1xButton = CreateActionButton(
                speedBar.transform,
                "Btn_Speed_1x",
                "1x",
                new Color(0.14f, 0.62f, 0.88f, 1f),
                48f,
                38f,
                font,
                SetPlaybackSpeed1x);

            playbackSpeed5xButton = CreateActionButton(
                speedBar.transform,
                "Btn_Speed_5x",
                "5x",
                new Color(0.12f, 0.18f, 0.28f, 0.96f),
                48f,
                38f,
                font,
                SetPlaybackSpeed5x);

            playbackSpeed10xButton = CreateActionButton(
                speedBar.transform,
                "Btn_Speed_10x",
                "10x",
                new Color(0.12f, 0.18f, 0.28f, 0.96f),
                54f,
                38f,
                font,
                SetPlaybackSpeed10x);

            // Main 3-Column Flight Mission Control Row
            GameObject flightColumns = CreateUIObject("FlightColumnsRow", liveFlightModeRoot.transform);
            LayoutElement colsLe = flightColumns.AddComponent<LayoutElement>();
            colsLe.flexibleHeight = 1f;
            colsLe.preferredHeight = 740f;

            HorizontalLayoutGroup colsLayout = flightColumns.AddComponent<HorizontalLayoutGroup>();
            colsLayout.spacing = 12f;
            colsLayout.childControlWidth = true;
            colsLayout.childControlHeight = true;
            colsLayout.childForceExpandWidth = false;
            colsLayout.childForceExpandHeight = true;

            // Column 1: 2D Orbit Radar (OrbitRadarUI from S1-08)
            GameObject radarCol = CreateUIObject("OrbitRadarColumn", flightColumns.transform);
            LayoutElement radarLe = radarCol.AddComponent<LayoutElement>();
            radarLe.flexibleWidth = 0.95f;
            radarLe.preferredWidth = 570f;
            orbitRadarUI = radarCol.AddComponent<OrbitRadarUI>();
            orbitRadarUI.EnsureRadarHierarchy();

            // Column 2: Live Telemetry & Green Autopilot Rule Highlighting (TelemetryHUD)
            GameObject telemetryCol = CreateUIObject("TelemetryHUDColumn", flightColumns.transform);
            LayoutElement telemetryLe = telemetryCol.AddComponent<LayoutElement>();
            telemetryLe.flexibleWidth = 1.15f;
            telemetryLe.preferredWidth = 690f;
            telemetryHUD = telemetryCol.AddComponent<TelemetryHUD>();
            telemetryHUD.EnsureTelemetryHierarchy();

            // Column 3: Manual Override Commands with Signal Delay Timer (ManualOverrideUI)
            GameObject manualCol = CreateUIObject("ManualOverrideColumn", flightColumns.transform);
            LayoutElement manualLe = manualCol.AddComponent<LayoutElement>();
            manualLe.flexibleWidth = 0.90f;
            manualLe.preferredWidth = 550f;
            manualOverrideUI = manualCol.AddComponent<ManualOverrideUI>();
            manualOverrideUI.EnsureManualOverrideHierarchy();
        }

        private void BuildDebriefSummaryModeView(Transform parent, Font font)
        {
            debriefSummaryModeRoot = CreateUIObject("DebriefSummaryModeView_S205", parent);
            StretchFull(debriefSummaryModeRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup vLayout = debriefSummaryModeRoot.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 8f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            // Outcome Headline Banner
            BuildOutcomeBanner(debriefSummaryModeRoot.transform, font);

            // Main Two-Column Content Row
            GameObject mainRow = CreateUIObject("MainContentRow", debriefSummaryModeRoot.transform);
            LayoutElement mainRowLe = mainRow.AddComponent<LayoutElement>();
            mainRowLe.flexibleHeight = 1f;
            mainRowLe.preferredHeight = 470f;

            HorizontalLayoutGroup mainRowLayout = mainRow.AddComponent<HorizontalLayoutGroup>();
            mainRowLayout.spacing = 16f;
            mainRowLayout.childControlWidth = true;
            mainRowLayout.childControlHeight = true;
            mainRowLayout.childForceExpandWidth = true;
            mainRowLayout.childForceExpandHeight = true;

            BuildTelemetryMetricsColumn(mainRow.transform, font);
            BuildRightStateColumn(mainRow.transform, font);
        }

        private void BuildOutcomeBanner(Transform parent, Font font)
        {
            GameObject bannerObj = CreatePanel("OutcomeBanner", parent, new Color(0.08f, 0.26f, 0.18f, 0.96f), 54f);
            outcomeBannerBackground = bannerObj.GetComponent<Image>();

            VerticalLayoutGroup bannerLayout = bannerObj.AddComponent<VerticalLayoutGroup>();
            bannerLayout.padding = new RectOffset(18, 18, 5, 5);
            bannerLayout.spacing = 2f;
            bannerLayout.childControlWidth = true;
            bannerLayout.childControlHeight = true;
            bannerLayout.childForceExpandWidth = true;
            bannerLayout.childForceExpandHeight = false;

            outcomeHeadlineText = CreateText(
                "OutcomeHeadline",
                bannerObj.transform,
                font,
                "MISSION ACCOMPLISHED  •  FINAL SCORE: 162.5 PTS",
                18,
                FontStyle.Bold,
                new Color(0.42f, 0.98f, 0.64f, 1f),
                TextAnchor.MiddleLeft);

            outcomeSublineText = CreateText(
                "OutcomeSubline",
                bannerObj.transform,
                font,
                "All orbital objectives verified by NASA Ground Control.",
                13,
                FontStyle.Normal,
                new Color(0.84f, 0.95f, 0.88f, 1f),
                TextAnchor.MiddleLeft);
        }

        private void BuildTelemetryMetricsColumn(Transform parent, Font font)
        {
            GameObject leftCol = CreatePanel("TelemetryMetricsColumn", parent, new Color(0.07f, 0.11f, 0.19f, 0.98f), 0f);
            LayoutElement leftLe = leftCol.AddComponent<LayoutElement>();
            leftLe.flexibleWidth = 0.95f;
            leftLe.preferredWidth = 820f;

            VerticalLayoutGroup leftLayout = leftCol.AddComponent<VerticalLayoutGroup>();
            leftLayout.padding = new RectOffset(16, 16, 12, 12);
            leftLayout.spacing = 7f;
            leftLayout.childControlWidth = true;
            leftLayout.childControlHeight = true;
            leftLayout.childForceExpandWidth = true;
            leftLayout.childForceExpandHeight = false;

            CreateText(
                "MetricsHeader",
                leftCol.transform,
                font,
                "FLIGHT TELEMETRY & NASA RESOURCE SUMMARY",
                16,
                FontStyle.Bold,
                new Color(0.55f, 0.84f, 1.0f, 1f),
                TextAnchor.MiddleLeft);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_Science",
                "1. DELIVERED SCIENCE (DOWNLINKED QUOTA)",
                "120.0 / 100.0 pts",
                "Downlinked Quota: 100%   •   Lost SSD Overflow Data: 0.0 Gb",
                new Color(0.22f, 0.84f, 0.48f, 1f),
                out scienceMetricValueText,
                out scienceMetricDetailText,
                out scienceMetricBarFill);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_Budget",
                "2. MISSION BUDGET SPENT (HARDWARE + ROCKET)",
                "$90.0k / $120.0k",
                "Unspent Margin: $30.0k   •   Efficiency Score Bonus: +15.0 pts",
                new Color(0.24f, 0.72f, 0.96f, 1f),
                out budgetMetricValueText,
                out budgetMetricDetailText,
                out budgetMetricBarFill);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_MinBattery",
                "3. MINIMUM BATTERY CHARGE (ECLIPSE RESERVE)",
                "60.0%",
                "PASS: Above 15.0% NASA reserve threshold (Reliability Bonus OK)",
                new Color(0.22f, 0.84f, 0.48f, 1f),
                out minBatteryMetricValueText,
                out minBatteryMetricDetailText,
                out minBatteryMetricBarFill);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_FinalHealth",
                "4. FINAL ELECTRONICS HEALTH (RADIATION INTEGRITY)",
                "100.0%",
                "PASS: Above 80.0% electronics integrity threshold (Reliability Bonus OK)",
                new Color(0.22f, 0.84f, 0.48f, 1f),
                out finalHealthMetricValueText,
                out finalHealthMetricDetailText,
                out finalHealthMetricBarFill);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_Reliability",
                "5. SPACECRAFT OVERALL RELIABILITY",
                "92.0%",
                "PASS: Spacecraft assembly reliability >= 85.0% NASA standard",
                new Color(0.22f, 0.84f, 0.48f, 1f),
                out reliabilityMetricValueText,
                out reliabilityMetricDetailText,
                out reliabilityMetricBarFill);
        }

        private static void BuildMetricCard(
            Transform parent,
            Font font,
            string name,
            string label,
            string initialValue,
            string initialDetail,
            Color barColor,
            out Text valueText,
            out Text detailText,
            out Image barFill)
        {
            GameObject card = CreatePanel(name, parent, new Color(0.04f, 0.07f, 0.13f, 0.95f), 74f);
            VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(14, 14, 6, 6);
            cardLayout.spacing = 4f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            GameObject topRow = CreateUIObject("TopRow", card.transform);
            HorizontalLayoutGroup topLayout = topRow.AddComponent<HorizontalLayoutGroup>();
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;

            Text titleTxt = CreateText(
                "Label",
                topRow.transform,
                font,
                label,
                13,
                FontStyle.Bold,
                new Color(0.76f, 0.86f, 0.96f, 1f),
                TextAnchor.MiddleLeft);
            LayoutElement titleLe = titleTxt.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;

            valueText = CreateText(
                "Value",
                topRow.transform,
                font,
                initialValue,
                16,
                FontStyle.Bold,
                new Color(0.35f, 0.96f, 0.58f, 1f),
                TextAnchor.MiddleRight);

            GameObject barTrack = CreatePanel("BarTrack", card.transform, new Color(0.12f, 0.18f, 0.28f, 1f), 9f);
            GameObject fillObj = CreateUIObject("BarFill", barTrack.transform);
            StretchFull(fillObj.GetComponent<RectTransform>());
            barFill = fillObj.AddComponent<Image>();
            barFill.sprite = CreateSolidWhiteSprite();
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.fillAmount = 0.85f;
            barFill.color = barColor;

            detailText = CreateText(
                "Detail",
                card.transform,
                font,
                initialDetail,
                12,
                FontStyle.Normal,
                new Color(0.68f, 0.78f, 0.90f, 1f),
                TextAnchor.MiddleLeft);
        }

        private void BuildRightStateColumn(Transform parent, Font font)
        {
            GameObject rightContainer = CreateUIObject("RightStateColumn", parent);
            LayoutElement rightLe = rightContainer.AddComponent<LayoutElement>();
            rightLe.flexibleWidth = 1.05f;
            rightLe.preferredWidth = 920f;

            BuildSuccessStatePanel(rightContainer.transform, font);
            BuildFailureStatePanel(rightContainer.transform, font);
        }

        private void BuildSuccessStatePanel(Transform parent, Font font)
        {
            successStateRoot = CreatePanel("SuccessStatePanel", parent, new Color(0.07f, 0.13f, 0.19f, 0.98f), 0f);
            StretchFull(successStateRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup layout = successStateRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.spacing = 9f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateText(
                "SuccessHeader",
                successStateRoot.transform,
                font,
                "MISSION RATING, RELIABILITY BONUS & CONTRACT PAYOUT",
                16,
                FontStyle.Bold,
                new Color(0.45f, 0.96f, 0.66f, 1f),
                TextAnchor.MiddleLeft);

            // 1. Score & Rating Card
            GameObject scoreCard = CreatePanel("ScoreCard", successStateRoot.transform, new Color(0.04f, 0.08f, 0.14f, 0.96f), 118f);
            VerticalLayoutGroup scoreLayout = scoreCard.AddComponent<VerticalLayoutGroup>();
            scoreLayout.padding = new RectOffset(16, 16, 8, 8);
            scoreLayout.spacing = 5f;
            scoreLayout.childControlWidth = true;
            scoreLayout.childControlHeight = true;
            scoreLayout.childForceExpandWidth = true;
            scoreLayout.childForceExpandHeight = false;

            GameObject scoreTopRow = CreateUIObject("ScoreTopRow", scoreCard.transform);
            HorizontalLayoutGroup scoreTopLayout = scoreTopRow.AddComponent<HorizontalLayoutGroup>();
            scoreTopLayout.spacing = 12f;
            scoreTopLayout.childAlignment = TextAnchor.MiddleLeft;
            scoreTopLayout.childControlWidth = true;
            scoreTopLayout.childControlHeight = true;
            scoreTopLayout.childForceExpandWidth = false;
            scoreTopLayout.childForceExpandHeight = false;

            Text scoreLabel = CreateText(
                "FinalScoreLabel",
                scoreTopRow.transform,
                font,
                "FINAL MISSION SCORE:",
                15,
                FontStyle.Bold,
                new Color(0.78f, 0.88f, 0.98f, 1f),
                TextAnchor.MiddleLeft);
            LayoutElement scoreLabelLe = scoreLabel.gameObject.AddComponent<LayoutElement>();
            scoreLabelLe.flexibleWidth = 1f;

            finalScoreValueText = CreateText(
                "FinalScoreValue",
                scoreTopRow.transform,
                font,
                "162.5 PTS",
                24,
                FontStyle.Bold,
                new Color(0.38f, 0.98f, 0.62f, 1f),
                TextAnchor.MiddleRight);

            GameObject gradeBadge = CreatePanel("GradeBadge", scoreTopRow.transform, new Color(0.14f, 0.54f, 0.32f, 1f), 32f);
            LayoutElement gradeLe = gradeBadge.GetComponent<LayoutElement>();
            gradeLe.preferredWidth = 210f;
            ratingGradeBadgeBg = gradeBadge.GetComponent<Image>();

            ratingGradeBadgeText = CreateText(
                "GradeBadgeText",
                gradeBadge.transform,
                font,
                "GRADE S  •  EXEMPLARY",
                13,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            StretchFull(ratingGradeBadgeText.GetComponent<RectTransform>());

            scoreFormulaBreakdownText = CreateText(
                "ScoreFormulaBreakdown",
                scoreCard.transform,
                font,
                "• Delivered Science Points: <b>+120.0 pts</b>\n" +
                "• Budget Efficiency Bonus: <b>+15.0 pts</b>\n" +
                "• Engineering Reliability Margin Bonus: <b>+50.0 pts</b>",
                13,
                FontStyle.Normal,
                new Color(0.84f, 0.92f, 0.98f, 1f),
                TextAnchor.MiddleLeft);
            scoreFormulaBreakdownText.supportRichText = true;
            scoreFormulaBreakdownText.lineSpacing = 1.12f;

            // 2. +50 Reliability Bonus Card
            GameObject bonusCard = CreatePanel("ReliabilityBonusCard", successStateRoot.transform, new Color(0.09f, 0.30f, 0.20f, 0.95f), 64f);
            reliabilityBonusCardBg = bonusCard.GetComponent<Image>();

            VerticalLayoutGroup bonusLayout = bonusCard.AddComponent<VerticalLayoutGroup>();
            bonusLayout.padding = new RectOffset(16, 16, 7, 7);
            bonusLayout.spacing = 3f;
            bonusLayout.childControlWidth = true;
            bonusLayout.childControlHeight = true;
            bonusLayout.childForceExpandWidth = true;
            bonusLayout.childForceExpandHeight = false;

            reliabilityBonusTitleText = CreateText(
                "ReliabilityBonusTitle",
                bonusCard.transform,
                font,
                "RELIABILITY MARGIN BONUS: +50.0 PTS AWARDED",
                15,
                FontStyle.Bold,
                new Color(0.42f, 0.98f, 0.64f, 1f),
                TextAnchor.MiddleLeft);

            reliabilityBonusDetailText = CreateText(
                "ReliabilityBonusDetail",
                bonusCard.transform,
                font,
                "Conditions: Min Battery >= 15%  •  Final Health >= 80%  •  Reliability >= 85%",
                12,
                FontStyle.Normal,
                new Color(0.85f, 0.95f, 0.90f, 1f),
                TextAnchor.MiddleLeft);

            // 3. Contract Payout & Passive Income Card
            GameObject payoutCard = CreatePanel("ContractPayoutCard", successStateRoot.transform, new Color(0.04f, 0.08f, 0.14f, 0.96f), 122f);
            VerticalLayoutGroup payoutLayout = payoutCard.AddComponent<VerticalLayoutGroup>();
            payoutLayout.padding = new RectOffset(16, 16, 8, 8);
            payoutLayout.spacing = 6f;
            payoutLayout.childControlWidth = true;
            payoutLayout.childControlHeight = true;
            payoutLayout.childForceExpandWidth = true;
            payoutLayout.childForceExpandHeight = false;

            CreateText(
                "PayoutHeader",
                payoutCard.transform,
                font,
                "CONTRACT REWARD & PASSIVE INCOME PAYOUT",
                14,
                FontStyle.Bold,
                new Color(1.0f, 0.84f, 0.38f, 1f),
                TextAnchor.MiddleLeft);

            GameObject payoutBoxesRow = CreateUIObject("PayoutBoxesRow", payoutCard.transform);
            HorizontalLayoutGroup payoutBoxesLayout = payoutBoxesRow.AddComponent<HorizontalLayoutGroup>();
            payoutBoxesLayout.spacing = 12f;
            payoutBoxesLayout.childControlWidth = true;
            payoutBoxesLayout.childControlHeight = true;
            payoutBoxesLayout.childForceExpandWidth = true;
            payoutBoxesLayout.childForceExpandHeight = false;

            GameObject rewardBox = CreatePanel("RewardBox", payoutBoxesRow.transform, new Color(0.08f, 0.16f, 0.26f, 1f), 36f);
            contractRewardValueText = CreateText(
                "ContractRewardValue",
                rewardBox.transform,
                font,
                "+$130k Contract Payout",
                15,
                FontStyle.Bold,
                new Color(0.42f, 0.98f, 0.64f, 1f),
                TextAnchor.MiddleCenter);
            StretchFull(contractRewardValueText.GetComponent<RectTransform>());

            GameObject passiveBox = CreatePanel("PassiveBox", payoutBoxesRow.transform, new Color(0.08f, 0.16f, 0.26f, 1f), 36f);
            passiveIncomeValueText = CreateText(
                "PassiveIncomeValue",
                passiveBox.transform,
                font,
                "+$20k / Flight Passive Income",
                15,
                FontStyle.Bold,
                new Color(0.48f, 0.88f, 1.0f, 1f),
                TextAnchor.MiddleCenter);
            StretchFull(passiveIncomeValueText.GetComponent<RectTransform>());

            agencyCapitalSummaryText = CreateText(
                "AgencyCapitalSummary",
                payoutCard.transform,
                font,
                "Total Payout Credited: <b>+$150k</b>\nAgency Balance: <b>$200k</b>   •   Active Passive Income Stream: <b>+$20k / mission</b>",
                13,
                FontStyle.Normal,
                new Color(0.84f, 0.92f, 0.98f, 1f),
                TextAnchor.MiddleLeft);
            agencyCapitalSummaryText.supportRichText = true;
            agencyCapitalSummaryText.lineSpacing = 1.12f;

            returnToHubButton = CreateActionButton(
                successStateRoot.transform,
                "Btn_ReturnToHub",
                "RETURN TO CONTRACT HUB (COLLECT REWARD & NEXT MISSION)",
                new Color(0.14f, 0.56f, 0.34f, 1f),
                0f,
                48f,
                font,
                ReturnToHub);
        }

        private void BuildFailureStatePanel(Transform parent, Font font)
        {
            failureStateRoot = CreatePanel("FailureStatePanel", parent, new Color(0.14f, 0.08f, 0.12f, 0.98f), 0f);
            StretchFull(failureStateRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup layout = failureStateRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.spacing = 9f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateText(
                "FailureHeader",
                failureStateRoot.transform,
                font,
                "WHY DID IT FAIL? — A.S.T.R.A. FAILURE DEBRIEF BUILDER",
                16,
                FontStyle.Bold,
                new Color(1.0f, 0.50f, 0.48f, 1f),
                TextAnchor.MiddleLeft);

            // 1. FailureTitle Box
            GameObject titleBox = CreatePanel("FailureTitleBox", failureStateRoot.transform, new Color(0.42f, 0.11f, 0.14f, 0.98f), 68f);
            failureTitleBannerBg = titleBox.GetComponent<Image>();

            VerticalLayoutGroup titleBoxLayout = titleBox.AddComponent<VerticalLayoutGroup>();
            titleBoxLayout.padding = new RectOffset(16, 16, 7, 7);
            titleBoxLayout.spacing = 3f;
            titleBoxLayout.childControlWidth = true;
            titleBoxLayout.childControlHeight = true;
            titleBoxLayout.childForceExpandWidth = true;
            titleBoxLayout.childForceExpandHeight = false;

            CreateText(
                "FailureTitleTag",
                titleBox.transform,
                font,
                "1. PRIMARY ANOMALY CLASSIFICATION (FailureTitle):",
                12,
                FontStyle.Bold,
                new Color(1.0f, 0.78f, 0.78f, 1f),
                TextAnchor.MiddleLeft);

            failureTitleText = CreateText(
                "FailureTitleValue",
                titleBox.transform,
                font,
                "Critical Battery Depletion (0%)",
                19,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleLeft);

            // 2. FailureCause Box
            GameObject causeBox = CreatePanel("FailureCauseBox", failureStateRoot.transform, new Color(0.06f, 0.08f, 0.14f, 0.96f), 104f);
            VerticalLayoutGroup causeBoxLayout = causeBox.AddComponent<VerticalLayoutGroup>();
            causeBoxLayout.padding = new RectOffset(16, 16, 8, 8);
            causeBoxLayout.spacing = 4f;
            causeBoxLayout.childControlWidth = true;
            causeBoxLayout.childControlHeight = true;
            causeBoxLayout.childForceExpandWidth = true;
            causeBoxLayout.childForceExpandHeight = false;

            CreateText(
                "FailureCauseTag",
                causeBox.transform,
                font,
                "2. ROOT CAUSE & ORBITAL TELEMETRY ANALYSIS (FailureCause):",
                13,
                FontStyle.Bold,
                new Color(1.0f, 0.74f, 0.36f, 1f),
                TextAnchor.MiddleLeft);

            failureCauseText = CreateText(
                "FailureCauseValue",
                causeBox.transform,
                font,
                "Satellite lost power on orbit 1 (tick 11). Power draw reached 27.0 W against 0.0 W generation, draining battery capacity (45.0 Wh) in eclipse.",
                14,
                FontStyle.Normal,
                new Color(0.94f, 0.96f, 1.0f, 1f),
                TextAnchor.UpperLeft);
            failureCauseText.lineSpacing = 1.15f;

            // 3. FailureRecommendation Box
            GameObject recBox = CreatePanel("FailureRecommendationBox", failureStateRoot.transform, new Color(0.06f, 0.17f, 0.24f, 0.96f), 100f);
            VerticalLayoutGroup recBoxLayout = recBox.AddComponent<VerticalLayoutGroup>();
            recBoxLayout.padding = new RectOffset(16, 16, 8, 8);
            recBoxLayout.spacing = 4f;
            recBoxLayout.childControlWidth = true;
            recBoxLayout.childControlHeight = true;
            recBoxLayout.childForceExpandWidth = true;
            recBoxLayout.childForceExpandHeight = false;

            CreateText(
                "FailureRecommendationTag",
                recBox.transform,
                font,
                "3. A.S.T.R.A. CORRECTIVE ENGINEERING ACTION (FailureRecommendation):",
                13,
                FontStyle.Bold,
                new Color(0.42f, 0.92f, 1.0f, 1f),
                TextAnchor.MiddleLeft);

            failureRecommendationText = CreateText(
                "FailureRecommendationValue",
                recBox.transform,
                font,
                "Add autopilot rule: IF SolarWatts < 5 THEN ScienceOff ELSE ScienceOn (or use BatteryPercent for RTG), or install a larger battery.",
                14,
                FontStyle.Bold,
                new Color(0.86f, 0.98f, 1.0f, 1f),
                TextAnchor.UpperLeft);
            failureRecommendationText.lineSpacing = 1.15f;

            // 4. Free Retry Summary Box
            GameObject noteBox = CreatePanel("FailureTelemetryNoteBox", failureStateRoot.transform, new Color(0.05f, 0.09f, 0.15f, 0.94f), 54f);
            VerticalLayoutGroup noteLayout = noteBox.AddComponent<VerticalLayoutGroup>();
            noteLayout.padding = new RectOffset(14, 14, 6, 6);
            noteLayout.childControlWidth = true;
            noteLayout.childControlHeight = true;
            noteLayout.childForceExpandWidth = true;
            noteLayout.childForceExpandHeight = true;

            failureTelemetryNoteText = CreateText(
                "FailureTelemetryNoteText",
                noteBox.transform,
                font,
                "FREE RETRY POLICY: $0k deducted from Agency Capital. Modify your components or autopilot rules in the Hangar at no penalty.",
                12,
                FontStyle.Normal,
                new Color(0.82f, 0.90f, 0.96f, 1f),
                TextAnchor.MiddleLeft);
            failureTelemetryNoteText.supportRichText = true;

            returnToWorkshopButton = CreateActionButton(
                failureStateRoot.transform,
                "Btn_ReturnToWorkshop",
                "FREE RETURN TO HANGAR (FIX SATELLITE BUILD / AUTOPILOT RULES)",
                new Color(0.78f, 0.42f, 0.12f, 1f),
                0f,
                48f,
                font,
                ReturnToWorkshopFree);
        }

        private void BuildVerificationSwitcherBar(Transform parent, Font font)
        {
            verificationBarRoot = CreatePanel("VerificationSwitcherBar", parent, new Color(0.09f, 0.15f, 0.26f, 0.98f), 40f);
            HorizontalLayoutGroup barLayout = verificationBarRoot.AddComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(12, 12, 4, 4);
            barLayout.spacing = 6f;
            barLayout.childAlignment = TextAnchor.MiddleCenter;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = true;
            barLayout.childForceExpandHeight = true;

            Text barLabel = CreateText(
                "VerifyLabel",
                verificationBarRoot.transform,
                font,
                "DEBRIEF PRESETS (S2-05):",
                11,
                FontStyle.Bold,
                new Color(0.68f, 0.82f, 0.96f, 1f),
                TextAnchor.MiddleLeft);
            LayoutElement labelLe = barLabel.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredWidth = 175f;
            labelLe.flexibleWidth = 0f;

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_SuccessBonus",
                "VICTORY (+50 BONUS)",
                new Color(0.14f, 0.50f, 0.30f, 1f),
                0f,
                32f,
                font,
                () => TriggerSampleSuccessScenario(true));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_SuccessNoBonus",
                "VICTORY (NO BONUS)",
                new Color(0.18f, 0.38f, 0.56f, 1f),
                0f,
                32f,
                font,
                () => TriggerSampleSuccessScenario(false));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_FailBattery",
                "FAIL: BATTERY (0%)",
                new Color(0.56f, 0.18f, 0.20f, 1f),
                0f,
                32f,
                font,
                () => TriggerSampleFailureScenario(FailureScenarioType.BatteryDepleted));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_FailRadiation",
                "FAIL: RADIATION (CME)",
                new Color(0.56f, 0.24f, 0.16f, 1f),
                0f,
                32f,
                font,
                () => TriggerSampleFailureScenario(FailureScenarioType.RadiationDamage));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_FailSsdOverflow",
                "FAIL: SSD OVERFLOW",
                new Color(0.52f, 0.28f, 0.14f, 1f),
                0f,
                32f,
                font,
                () => TriggerSampleFailureScenario(FailureScenarioType.InsufficientScienceWithSsdLoss));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_FailScienceQuota",
                "FAIL: SCIENCE QUOTA",
                new Color(0.46f, 0.22f, 0.38f, 1f),
                0f,
                32f,
                font,
                () => TriggerSampleFailureScenario(FailureScenarioType.InsufficientScienceNoLoss));
        }

        // --- UI Factory Helpers ---

        private static Sprite _solidWhiteSprite;

        private static Sprite CreateSolidWhiteSprite()
        {
            if (_solidWhiteSprite != null)
            {
                return _solidWhiteSprite;
            }

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            tex.SetPixels(pixels);
            tex.Apply();

            _solidWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            return _solidWhiteSprite;
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

        private static GameObject CreatePanel(string name, Transform parent, Color bgColor, float preferredHeight)
        {
            GameObject panel = CreateUIObject(name, parent);
            Image img = panel.AddComponent<Image>();
            img.color = bgColor;
            if (preferredHeight > 0f)
            {
                LayoutElement le = panel.AddComponent<LayoutElement>();
                le.preferredHeight = preferredHeight;
            }
            return panel;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment)
        {
            GameObject txtObj = CreateUIObject(name, parent);
            Text txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static Button CreateActionButton(
            Transform parent,
            string name,
            string label,
            Color bgColor,
            float width,
            float height,
            Font font,
            UnityEngine.Events.UnityAction onClick)
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

            Text txt = CreateText("Text", btnObj.transform, font, label, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }
    }
}
