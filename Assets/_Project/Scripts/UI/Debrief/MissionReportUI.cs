using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using AstraLink.Core;
using AstraLink.UI.Flight;

namespace AstraLink.UI.Debrief
{
    /// <summary>
    /// Mission Flight Playback, Live Telemetry, Manual Telecommand Override &amp; Post-Flight Debrief Controller (Tasks S2-04 &amp; S2-05).
    /// Connects step-by-step playback of <see cref="MissionReport.Ticks"/> (with 1x, 5x, 10x speed controls)
    /// to <see cref="OrbitRadarUI"/>, <see cref="TelemetryHUD"/>, <see cref="EventPopupUI"/>, and <see cref="ManualOverrideUI"/>,
    /// and displays the A.S.T.R.A. audit &amp; <c>Why did it fail?</c> breakdown (<see cref="MissionReport.FailureTitle"/>,
    /// <see cref="MissionReport.FailureCause"/>, <see cref="MissionReport.FailureRecommendation"/>) from Task S2-02.
    /// </summary>
    public class MissionReportUI : MonoBehaviour
    {
        [Header("Linked Sub-Controllers (Auto-bound or created if null)")]
        [SerializeField] private OrbitRadarUI orbitRadarUI;
        [SerializeField] private TelemetryHUD telemetryHUD;
        [SerializeField] private EventPopupUI eventPopupUI;
        [SerializeField] private ManualOverrideUI manualOverrideUI;

        [Header("Playback Speed & Transport Controls (1x / 5x / 10x)")]
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Text playPauseButtonLabel;
        [SerializeField] private Button restartFlightButton;
        [SerializeField] private Button speed1xButton;
        [SerializeField] private Button speed5xButton;
        [SerializeField] private Button speed10xButton;
        [SerializeField] private Text playbackHeaderLabel;
        [SerializeField] private Text playbackTickCounterLabel;

        [Header("Debrief & 'Why Did It Fail?' Summary Panel (S2-02 / S2-05)")]
        [SerializeField] private Image debriefBannerBg;
        [SerializeField] private Text debriefStatusTitleLabel;
        [SerializeField] private Text debriefMetricsSummaryLabel;
        [SerializeField] private Text failureTitleLabel;
        [SerializeField] private Text failureCauseLabel;
        [SerializeField] private Text failureRecommendationLabel;
        [SerializeField] private Text astraAuditSummaryLabel;

        [Header("Playback Settings")]
        [SerializeField] private float baseSecondsPerTick = 1.0f;
        [SerializeField] private bool autoPlayOnInitialize = true;

        private IMissionSimulator _simulator;
        private IAstraAuditor _auditor;
        private List<SatelliteComponent> _components = new List<SatelliteComponent>();
        private RocketOption _rocket;
        private OrbitDefinition _orbit;
        private MissionGoal _goal;
        private List<AutopilotRule> _rules = new List<AutopilotRule>();
        private readonly List<ManualCommandRequest> _activeManualCommands = new List<ManualCommandRequest>();
        private List<SpaceEventDefinition> _overrideEvents;

        private MissionReport _currentReport;
        private List<AstraAuditMessage> _auditMessages = new List<AstraAuditMessage>();

        private bool _isPlaying;
        private int _currentTickIndex;
        private float _tickElapsedSeconds;
        private float _speedMultiplier = 1.0f;
        private bool _initialized;

        /// <summary>
        /// Fired on each simulation tick during playback.
        /// </summary>
        public event Action<SimulationTickSnapshot, int> OnTickAdvanced;

        /// <summary>
        /// Fired when the mission simulation recalculates after a manual telecommand is queued.
        /// </summary>
        public event Action<MissionReport, ManualCommandRequest> OnMissionRecalculated;

        /// <summary>
        /// Fired when playback reaches the final tick of the mission report.
        /// </summary>
        public event Action<MissionReport> OnPlaybackCompleted;

        /// <summary>
        /// Gets the active <see cref="MissionReport"/> being played back and analyzed.
        /// </summary>
        public MissionReport CurrentReport => _currentReport;

        /// <summary>
        /// Gets the list of manual commands issued during the current flight (`activeManualCommands`).
        /// </summary>
        public List<ManualCommandRequest> ActiveManualCommands => _activeManualCommands;

        /// <summary>
        /// Gets the current simulation playback speed multiplier (1x, 5x, or 10x).
        /// </summary>
        public float SpeedMultiplier => _speedMultiplier;

        /// <summary>
        /// Gets the linked <see cref="OrbitRadarUI"/> instance.
        /// </summary>
        public OrbitRadarUI RadarUI => orbitRadarUI;

        /// <summary>
        /// Gets the linked <see cref="TelemetryHUD"/> instance.
        /// </summary>
        public TelemetryHUD TelemetryHud => telemetryHUD;

        /// <summary>
        /// Gets the linked <see cref="EventPopupUI"/> instance.
        /// </summary>
        public EventPopupUI EventPopup => eventPopupUI;

        /// <summary>
        /// Gets the linked <see cref="ManualOverrideUI"/> instance.
        /// </summary>
        public ManualOverrideUI ManualOverride => manualOverrideUI;

        private void Awake()
        {
            EnsureMissionReportHierarchy();
            BindControlButtons();
        }

        private void Start()
        {
            if (!_initialized)
            {
                IMissionSimulator sim = ResolveDefaultSimulator();
                IAstraAuditor auditor = TryCreateDefaultAuditor();
                Initialize(
                    sim,
                    CreateDefaultComponents(),
                    CreateDefaultRocket(),
                    OrbitRadarUI.CreateDefaultLeoOrbit(),
                    CreateDefaultGoal(),
                    TelemetryHUD.CreateDefaultRules(),
                    orbitRadarUI,
                    auditor,
                    null);
            }
        }

        /// <summary>
        /// Primary entry point for initializing full flight playback, live telemetry, manual override, and debrief (Tasks S2-04 &amp; S2-05).
        /// </summary>
        public void Initialize(
            IMissionSimulator simulator,
            List<SatelliteComponent> components = null,
            RocketOption rocket = null,
            OrbitDefinition orbit = null,
            MissionGoal goal = null,
            List<AutopilotRule> rules = null,
            OrbitRadarUI externalRadar = null,
            IAstraAuditor auditor = null,
            List<SpaceEventDefinition> overrideEvents = null)
        {
            EnsureMissionReportHierarchy();
            BindControlButtons();

            _initialized = true;
            _simulator = simulator ?? ResolveDefaultSimulator();
            _auditor = auditor ?? TryCreateDefaultAuditor();

            _components = (components != null && components.Count > 0) ? components : CreateDefaultComponents();
            _rocket = rocket ?? CreateDefaultRocket();
            _orbit = orbit ?? OrbitRadarUI.CreateDefaultLeoOrbit();
            _goal = goal ?? CreateDefaultGoal();
            _rules = (rules != null && rules.Count > 0) ? rules : TelemetryHUD.CreateDefaultRules();
            _overrideEvents = overrideEvents;
            _activeManualCommands.Clear();

            if (externalRadar != null)
            {
                orbitRadarUI = externalRadar;
                WireRadarCallbacks();
            }

            ResourceSummary resources = _simulator.CalculateResources(_components, _rocket, _orbit, _goal);

            if (_auditor != null)
            {
                try
                {
                    _auditMessages = _auditor.AuditConfiguration(_components, _rocket, _orbit, _goal, _rules)
                                     ?? new List<AstraAuditMessage>();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MissionReportUI] IAstraAuditor.AuditConfiguration threw an exception: " + ex.Message);
                    _auditMessages = new List<AstraAuditMessage>();
                }
            }
            else
            {
                _auditMessages = BuildFallbackAuditMessages(_components, _rocket, _orbit, _goal, _rules);
            }

            if (telemetryHUD != null)
            {
                telemetryHUD.Initialize(_goal, resources, _rules);
            }

            if (eventPopupUI != null)
            {
                eventPopupUI.Initialize(_overrideEvents);
            }

            if (manualOverrideUI != null)
            {
                manualOverrideUI.Initialize(_orbit, _activeManualCommands);
                manualOverrideUI.SetPlaybackSpeed(_speedMultiplier);
            }

            RecalculateMissionReport(resetPlaybackToStart: true);
            SetPlaybackSpeed(1.0f);

            if (autoPlayOnInitialize)
            {
                StartPlayback();
            }
        }

        /// <summary>
        /// Initializes the UI directly with a precomputed <see cref="MissionReport"/> (e.g. when transitioning to Debrief screen).
        /// </summary>
        public void Initialize(
            MissionReport report,
            OrbitDefinition orbit = null,
            MissionGoal goal = null,
            List<AutopilotRule> rules = null,
            OrbitRadarUI externalRadar = null,
            List<AstraAuditMessage> auditMessages = null)
        {
            EnsureMissionReportHierarchy();
            BindControlButtons();

            _initialized = true;
            _simulator = _simulator ?? ResolveDefaultSimulator();
            _orbit = orbit ?? _orbit ?? OrbitRadarUI.CreateDefaultLeoOrbit();
            _goal = goal ?? _goal ?? CreateDefaultGoal();
            _rules = (rules != null && rules.Count > 0) ? rules : (_rules.Count > 0 ? _rules : TelemetryHUD.CreateDefaultRules());
            _currentReport = report ?? _simulator.SimulateFullMission(_components, _rocket, _orbit, _goal, _rules, _activeManualCommands, _overrideEvents);

            if (auditMessages != null)
            {
                _auditMessages = auditMessages;
            }

            if (externalRadar != null)
            {
                orbitRadarUI = externalRadar;
                WireRadarCallbacks();
            }

            ResourceSummary resources = _simulator.CalculateResources(_components, _rocket, _orbit, _goal);

            if (telemetryHUD != null)
            {
                telemetryHUD.Initialize(_goal, resources, _rules);
            }

            if (eventPopupUI != null)
            {
                eventPopupUI.Initialize(_overrideEvents);
            }

            if (manualOverrideUI != null)
            {
                manualOverrideUI.Initialize(_orbit, _activeManualCommands);
            }

            if (orbitRadarUI != null)
            {
                orbitRadarUI.Initialize(_orbit, _currentReport);
            }

            _currentTickIndex = 0;
            _tickElapsedSeconds = 0.0f;

            if (_currentReport != null && _currentReport.Ticks != null && _currentReport.Ticks.Count > 0)
            {
                ApplySnapshotToAllPanels(_currentReport.Ticks[0], 0, _currentReport.Ticks.Count);
            }

            RefreshDebriefSummaryPanel();
            UpdateSpeedButtonsVisuals();
        }

        /// <summary>
        /// Sets the simulation playback speed multiplier (<c>1x</c>, <c>5x</c>, or <c>10x</c>) and synchronizes <see cref="OrbitRadarUI"/>.
        /// </summary>
        public void SetPlaybackSpeed(float multiplier)
        {
            _speedMultiplier = Mathf.Clamp(multiplier, 0.25f, 20.0f);

            if (orbitRadarUI != null)
            {
                orbitRadarUI.SetPlaybackSpeed(_speedMultiplier);
            }

            if (manualOverrideUI != null)
            {
                manualOverrideUI.SetPlaybackSpeed(_speedMultiplier);
            }

            UpdateSpeedButtonsVisuals();
            RefreshPlaybackHeaderText();
        }

        /// <summary>
        /// Toggles step-by-step mission playback between playing and paused states.
        /// </summary>
        public void TogglePlayPause()
        {
            if (_isPlaying)
            {
                PausePlayback();
            }
            else
            {
                if (_currentReport != null && _currentReport.Ticks != null &&
                    _currentTickIndex >= _currentReport.Ticks.Count - 1)
                {
                    RestartPlayback();
                }
                else
                {
                    StartPlayback();
                }
            }
        }

        /// <summary>
        /// Starts or resumes step-by-step playback of <see cref="MissionReport.Ticks"/>.
        /// </summary>
        public void StartPlayback()
        {
            if (_currentReport == null || _currentReport.Ticks == null || _currentReport.Ticks.Count == 0)
            {
                RecalculateMissionReport(resetPlaybackToStart: true);
            }

            if (_currentReport == null || _currentReport.Ticks == null || _currentReport.Ticks.Count == 0)
            {
                return;
            }

            // Ensure OrbitRadarUI's internal standalone timer is paused so MissionReportUI drives it cleanly
            if (orbitRadarUI != null && orbitRadarUI.IsPlaying)
            {
                orbitRadarUI.StopTestFlight(resetToStart: false);
            }

            _isPlaying = true;
            UpdatePlayPauseButtonVisual();
            ApplySnapshotToAllPanels(_currentReport.Ticks[_currentTickIndex], _currentTickIndex, _currentReport.Ticks.Count);
        }

        /// <summary>
        /// Pauses step-by-step mission playback at the current tick.
        /// </summary>
        public void PausePlayback()
        {
            _isPlaying = false;
            UpdatePlayPauseButtonVisual();
            RefreshPlaybackHeaderText();
        }

        /// <summary>
        /// Resets manual commands, recalculates the mission from Tick 0, and restarts step-by-step playback.
        /// </summary>
        public void RestartPlayback()
        {
            _activeManualCommands.Clear();
            if (manualOverrideUI != null)
            {
                manualOverrideUI.ResetCommands();
            }

            RecalculateMissionReport(resetPlaybackToStart: true);
            StartPlayback();
        }

        private void Update()
        {
            if (!_isPlaying || _currentReport == null || _currentReport.Ticks == null || _currentReport.Ticks.Count == 0)
            {
                return;
            }

            int totalTicks = _currentReport.Ticks.Count;
            float secondsPerTick = Mathf.Max(0.03f, baseSecondsPerTick / Mathf.Max(0.1f, _speedMultiplier));

            _tickElapsedSeconds += Time.deltaTime;

            while (_tickElapsedSeconds >= secondsPerTick && _isPlaying)
            {
                _tickElapsedSeconds -= secondsPerTick;
                _currentTickIndex++;

                if (_currentTickIndex >= totalTicks)
                {
                    _isPlaying = false;
                    _currentTickIndex = totalTicks - 1;

                    SimulationTickSnapshot finalSnap = _currentReport.Ticks[_currentTickIndex];
                    ApplySnapshotToAllPanels(finalSnap, _currentTickIndex, totalTicks);

                    if (orbitRadarUI != null)
                    {
                        orbitRadarUI.SetSatelliteOrbitProgress(1.0f);
                    }

                    UpdatePlayPauseButtonVisual();
                    RefreshDebriefSummaryPanel();
                    OnPlaybackCompleted?.Invoke(_currentReport);
                    return;
                }

                SimulationTickSnapshot currentSnap = _currentReport.Ticks[_currentTickIndex];
                ApplySnapshotToAllPanels(currentSnap, _currentTickIndex, totalTicks);
            }

            // Smoothly interpolate satellite position on the 2D Orbit Radar between ticks
            if (_isPlaying && orbitRadarUI != null && _currentTickIndex < totalTicks)
            {
                SimulationTickSnapshot snap = _currentReport.Ticks[_currentTickIndex];
                float startU = snap.OrbitProgress;
                float stepU = 1.0f / 20.0f;
                float endU = startU + stepU;

                if (_currentTickIndex + 1 < totalTicks)
                {
                    float nextU = _currentReport.Ticks[_currentTickIndex + 1].OrbitProgress;
                    if (nextU > startU)
                    {
                        endU = nextU;
                    }
                }

                float t = Mathf.Clamp01(_tickElapsedSeconds / secondsPerTick);
                float smoothU = Mathf.Lerp(startU, endU, t);
                orbitRadarUI.SetSatelliteOrbitProgress(smoothU);
            }
        }

        private void HandleManualCommandIssued(ManualCommandRequest newRequest, List<ManualCommandRequest> allCommands)
        {
            RecalculateMissionReport(resetPlaybackToStart: false);
            OnMissionRecalculated?.Invoke(_currentReport, newRequest);
        }

        /// <summary>
        /// Calls <see cref="IMissionSimulator.SimulateFullMission"/> with <see cref="ActiveManualCommands"/>
        /// to deterministically recalculate the flight trajectory and telemetry in real time.
        /// </summary>
        public void RecalculateMissionReport(bool resetPlaybackToStart)
        {
            if (_simulator == null)
            {
                _simulator = ResolveDefaultSimulator();
            }

            _currentReport = _simulator.SimulateFullMission(
                _components,
                _rocket,
                _orbit,
                _goal,
                _rules,
                _activeManualCommands,
                _overrideEvents);

            // If running on the Sprint 1 MockMissionSimulator stub, enrich the mock ticks so manual commands
            // and override events visibly affect telemetry after ExecuteAtTick during standalone testing.
            if (_simulator is MockMissionSimulator && _currentReport != null)
            {
                ApplyMockManualCommandsAndEvents(_currentReport, _activeManualCommands, _overrideEvents, _rules);
            }

            if (orbitRadarUI != null)
            {
                bool wasPlaying = _isPlaying;
                orbitRadarUI.SetOrbit(_orbit);
                if (_rocket != null)
                {
                    orbitRadarUI.SetRocket(_rocket);
                }
                orbitRadarUI.StopTestFlight(resetToStart: resetPlaybackToStart);
                _isPlaying = wasPlaying;
            }

            if (resetPlaybackToStart)
            {
                _currentTickIndex = 0;
                _tickElapsedSeconds = 0.0f;
            }

            if (_currentReport != null && _currentReport.Ticks != null && _currentReport.Ticks.Count > 0)
            {
                _currentTickIndex = Mathf.Clamp(_currentTickIndex, 0, _currentReport.Ticks.Count - 1);
                ApplySnapshotToAllPanels(_currentReport.Ticks[_currentTickIndex], _currentTickIndex, _currentReport.Ticks.Count);
            }

            RefreshDebriefSummaryPanel();
        }

        private void ApplySnapshotToAllPanels(SimulationTickSnapshot snapshot, int tickIndex, int totalTicks)
        {
            if (snapshot == null) return;

            if (orbitRadarUI != null)
            {
                orbitRadarUI.SetSatelliteOrbitProgress(snapshot.OrbitProgress);
            }

            if (telemetryHUD != null)
            {
                telemetryHUD.UpdateTelemetry(snapshot);
            }

            if (eventPopupUI != null)
            {
                eventPopupUI.UpdateFromSnapshot(snapshot);
            }

            if (manualOverrideUI != null)
            {
                manualOverrideUI.UpdateFromSnapshot(snapshot, tickIndex);
            }

            RefreshPlaybackHeaderText(snapshot, tickIndex, totalTicks);
            OnTickAdvanced?.Invoke(snapshot, tickIndex);
        }

        private void RefreshPlaybackHeaderText(SimulationTickSnapshot snapshot = null, int tickIndex = -1, int totalTicks = -1)
        {
            if (_currentReport != null && _currentReport.Ticks != null && _currentReport.Ticks.Count > 0)
            {
                if (tickIndex < 0) tickIndex = Mathf.Clamp(_currentTickIndex, 0, _currentReport.Ticks.Count - 1);
                if (totalTicks < 0) totalTicks = _currentReport.Ticks.Count;
                if (snapshot == null) snapshot = _currentReport.Ticks[tickIndex];
            }

            if (playbackHeaderLabel != null)
            {
                string missionTitle = _goal != null && !string.IsNullOrWhiteSpace(_goal.Title)
                    ? _goal.Title
                    : "1. LEO Weather CubeSat";
                string orbitName = _orbit != null ? _orbit.Name : "LEO 400 km";
                string playState = _isPlaying ? "SIMULATING" : "PAUSED / READY";

                playbackHeaderLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "MISSION CONTROL TELEMETRY & DEBRIEF   |   {0}   |   {1}   [{2}]",
                    missionTitle,
                    orbitName,
                    playState);
            }

            if (playbackTickCounterLabel != null && snapshot != null && totalTicks > 0)
            {
                playbackTickCounterLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Orbit #{0}   |   Tick: {1:00} / {2:00}   |   u = {3:0.00}   |   Speed: {4:0.#}x   |   Manual Cmds: {5}",
                    snapshot.OrbitNumber,
                    tickIndex + 1,
                    totalTicks,
                    snapshot.OrbitProgress,
                    _speedMultiplier,
                    _activeManualCommands.Count);
            }
        }

        /// <summary>
        /// Refreshes the Debrief &amp; <c>Why did it fail?</c> summary card using <see cref="MissionReport"/> and <see cref="AstraAuditMessage"/> data.
        /// </summary>
        public void RefreshDebriefSummaryPanel()
        {
            if (_currentReport == null) return;

            bool isSuccess = _currentReport.Status == MissionStatus.Success;
            float targetSci = _goal != null && _goal.TargetSciencePoints > 0f ? _goal.TargetSciencePoints : 100.0f;

            if (debriefBannerBg != null)
            {
                debriefBannerBg.color = isSuccess
                    ? new Color(0.07f, 0.24f, 0.18f, 0.96f)
                    : new Color(0.28f, 0.10f, 0.14f, 0.96f);
            }

            if (debriefStatusTitleLabel != null)
            {
                debriefStatusTitleLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "MISSION DEBRIEF: {0}   |   FINAL SCORE: {1:0.0} PTS",
                    isSuccess ? "SUCCESS (CONTRACT COMPLETED)" : "FAILED (WHY DID IT FAIL?)",
                    _currentReport.FinalScore);
                debriefStatusTitleLabel.color = isSuccess
                    ? new Color(0.48f, 1.0f, 0.72f, 1.0f)
                    : new Color(1.0f, 0.56f, 0.58f, 1.0f);
            }

            if (debriefMetricsSummaryLabel != null)
            {
                debriefMetricsSummaryLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Delivered Science: {0:0.0} / {1:0.0} pts   |   Lost SSD Data: {2:0.00} GB   |   Min Battery: {3:0}%   |   Final Health: {4:0}%   |   Cost: ${5:0}k",
                    _currentReport.DeliveredScience,
                    targetSci,
                    _currentReport.LostDataGb,
                    _currentReport.MinBatteryPercent,
                    _currentReport.FinalHealthPercent,
                    _currentReport.BudgetSpentThousands);
            }

            // Populate the 3 S2-02 FailureDebriefBuilder fields (or positive engineering summary if mission succeeded)
            string fTitle = _currentReport.FailureTitle;
            string fCause = _currentReport.FailureCause;
            string fRec = _currentReport.FailureRecommendation;

            if (!isSuccess && string.IsNullOrWhiteSpace(fTitle))
            {
                BuildFallbackFailureDebrief(_currentReport, _goal, out fTitle, out fCause, out fRec);
            }
            else if (isSuccess && string.IsNullOrWhiteSpace(fTitle))
            {
                fTitle = "Nominal Mission Completion — All NASA Resource Constraints Satisfied";
                fCause = string.Format(
                    CultureInfo.InvariantCulture,
                    "Delivered {0:0.0} / {1:0.0} science points with {2:0}% minimum battery reserve and {3:0}% final electronics health.",
                    _currentReport.DeliveredScience,
                    targetSci,
                    _currentReport.MinBatteryPercent,
                    _currentReport.FinalHealthPercent);
                fRec = "Engineering margin verified. Ready to unlock next R&D tier or advance to deeper orbital contracts.";
            }

            if (failureTitleLabel != null)
            {
                failureTitleLabel.text = (isSuccess ? "SUMMARY: " : "FAILURE TITLE: ") + fTitle;
                failureTitleLabel.color = isSuccess
                    ? new Color(0.76f, 0.98f, 0.84f, 1.0f)
                    : new Color(1.0f, 0.78f, 0.42f, 1.0f);
            }

            if (failureCauseLabel != null)
            {
                failureCauseLabel.text = (isSuccess ? "TELEMETRY ANALYSIS: " : "CAUSE: ") + fCause;
            }

            if (failureRecommendationLabel != null)
            {
                failureRecommendationLabel.text = (isSuccess ? "NEXT STEP: " : "RECOMMENDATION: ") + fRec;
            }

            if (astraAuditSummaryLabel != null)
            {
                if (_auditMessages == null || _auditMessages.Count == 0)
                {
                    astraAuditSummaryLabel.text = "A.S.T.R.A. Pre-Flight Audit: 0 warnings — Configuration passed all 7 deterministic checks.";
                    astraAuditSummaryLabel.color = new Color(0.56f, 0.94f, 0.76f, 1.0f);
                }
                else
                {
                    AstraAuditMessage topMsg = _auditMessages[0];
                    for (int i = 0; i < _auditMessages.Count; i++)
                    {
                        if (_auditMessages[i] != null && _auditMessages[i].Severity >= topMsg.Severity)
                        {
                            topMsg = _auditMessages[i];
                        }
                    }

                    astraAuditSummaryLabel.text = string.Format(
                        CultureInfo.InvariantCulture,
                        "A.S.T.R.A. Audit ({0} notices)  |  [{1}] {2}: {3}",
                        _auditMessages.Count,
                        topMsg.Severity.ToString().ToUpperInvariant(),
                        topMsg.Title,
                        topMsg.Advice);

                    astraAuditSummaryLabel.color = topMsg.Severity == AuditSeverity.Critical
                        ? new Color(1.0f, 0.48f, 0.48f, 1.0f)
                        : (topMsg.Severity == AuditSeverity.Warning
                            ? new Color(1.0f, 0.84f, 0.38f, 1.0f)
                            : new Color(0.68f, 0.90f, 1.0f, 1.0f));
                }
            }
        }

        private static void BuildFallbackFailureDebrief(
            MissionReport report,
            MissionGoal goal,
            out string failureTitle,
            out string failureCause,
            out string failureRecommendation)
        {
            SimulationTickSnapshot lastTick = (report.Ticks != null && report.Ticks.Count > 0)
                ? report.Ticks[report.Ticks.Count - 1]
                : null;

            int orbitNum = lastTick != null ? Mathf.Max(1, lastTick.OrbitNumber) : 1;
            float targetScience = (goal != null && goal.TargetSciencePoints > 0f) ? goal.TargetSciencePoints : 100.0f;

            if (lastTick != null && lastTick.BatteryPercent <= 0.01f)
            {
                float deficitWatts = Mathf.Max(1.0f, lastTick.PowerDrawWatts - lastTick.SolarGenWatts);
                failureTitle = "Total Battery Depletion in Eclipse";
                failureCause = string.Format(
                    CultureInfo.InvariantCulture,
                    "Battery dropped to 0.0 Wh on Orbit #{0} (Tick #{1}) with a net power deficit of -{2:0.0} W during eclipse.",
                    orbitNum,
                    lastTick.TickIndex + 1,
                    deficitWatts);
                failureRecommendation = "Install a higher-capacity battery or add rule: IF SolarWatts < 5 THEN ScienceOff ELSE ScienceOn.";
            }
            else if (lastTick != null && lastTick.HealthPercent <= 0.01f)
            {
                failureTitle = "Critical Radiation Damage to Onboard Electronics";
                failureCause = string.Format(
                    CultureInfo.InvariantCulture,
                    "Electronics health reached 0% on Orbit #{0} (Tick #{1}) under {2:0.0} rad radiation without Safe Mode protection.",
                    orbitNum,
                    lastTick.TickIndex + 1,
                    lastTick.RadiationLevel);
                failureRecommendation = "Upgrade to a shielded chassis or add autopilot rule: IF RadiationLevel > 50 THEN SafeModeOn.";
            }
            else
            {
                float deficitSci = Mathf.Max(0.1f, targetScience - report.DeliveredScience);
                failureTitle = "Insufficient Science Data Delivered to Ground Station";
                failureCause = string.Format(
                    CultureInfo.InvariantCulture,
                    "Delivered {0:0.0} / {1:0.0} pts (shortfall of {2:0.0} pts; {3:0.00} GB lost to SSD overflow).",
                    report.DeliveredScience,
                    targetScience,
                    deficitSci,
                    report.LostDataGb);
                failureRecommendation = "Use a higher-rate antenna or add rule: IF SsdUsedPercent > 80 THEN CommsOn.";
            }
        }

        private static List<AstraAuditMessage> BuildFallbackAuditMessages(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules)
        {
            var list = new List<AstraAuditMessage>();
            if (components == null || orbit == null) return list;

            float totalSolar = 0f;
            float totalRtg = 0f;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] == null) continue;
                totalSolar += components[i].SolarWatts;
                totalRtg += components[i].RtgWatts;
            }

            // Check #7: RTG + SolarWatts trap
            if (totalRtg > 0.0f && totalSolar <= 0.01f && rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    if (rules[i] != null && rules[i].Enabled && rules[i].Metric == SensorMetric.SolarWatts)
                    {
                        list.Add(new AstraAuditMessage
                        {
                            Severity = AuditSeverity.Warning,
                            Title = "RTG + SolarWatts Rule Trap Detected",
                            Advice = "Build uses an RTG without solar panels (SolarWatts is always 0). Use BatteryPercent instead of SolarWatts."
                        });
                        break;
                    }
                }
            }

            return list;
        }

        private static void ApplyMockManualCommandsAndEvents(
            MissionReport report,
            List<ManualCommandRequest> manualCommands,
            List<SpaceEventDefinition> overrideEvents,
            List<AutopilotRule> rules)
        {
            if (report == null || report.Ticks == null || report.Ticks.Count == 0) return;
            if ((manualCommands == null || manualCommands.Count == 0) &&
                (overrideEvents == null || overrideEvents.Count == 0))
            {
                return;
            }

            bool sciState = true;
            bool commsState = true;
            bool safeState = false;
            float batteryWh = 45.0f;
            const float maxBatteryWh = 45.0f;

            for (int t = 0; t < report.Ticks.Count; t++)
            {
                SimulationTickSnapshot snap = report.Ticks[t];
                if (snap == null) continue;

                if (t == 0)
                {
                    sciState = snap.ScienceActive;
                    commsState = snap.CommsActive;
                    safeState = snap.SafeModeActive;
                }

                if (manualCommands != null)
                {
                    for (int c = 0; c < manualCommands.Count; c++)
                    {
                        ManualCommandRequest cmd = manualCommands[c];
                        if (cmd != null && cmd.ExecuteAtTick == t)
                        {
                            switch (cmd.Action)
                            {
                                case AutopilotAction.ScienceOn:
                                    sciState = true;
                                    break;
                                case AutopilotAction.ScienceOff:
                                    sciState = false;
                                    break;
                                case AutopilotAction.CommsOn:
                                    commsState = true;
                                    break;
                                case AutopilotAction.CommsOff:
                                    commsState = false;
                                    break;
                                case AutopilotAction.SafeModeOn:
                                    safeState = true;
                                    sciState = false;
                                    commsState = false;
                                    break;
                                case AutopilotAction.SafeModeOff:
                                    safeState = false;
                                    sciState = true;
                                    commsState = true;
                                    break;
                            }
                        }
                    }
                }

                if (safeState)
                {
                    sciState = false;
                    commsState = false;
                }

                snap.SafeModeActive = safeState;
                snap.ScienceActive = sciState;
                snap.CommsActive = commsState;

                if (safeState)
                {
                    snap.PowerDrawWatts = 6.0f;
                }
                else
                {
                    snap.PowerDrawWatts = 8.0f + (sciState ? 12.0f : 0.0f) + (commsState && snap.IsInCommWindow ? 6.0f : 0.0f);
                }

                float netW = snap.SolarGenWatts - snap.PowerDrawWatts;
                batteryWh = Mathf.Clamp(batteryWh + netW * 0.075f, 0.0f, maxBatteryWh);
                snap.BatteryWh = batteryWh;
                snap.BatteryPercent = (batteryWh / maxBatteryWh) * 100.0f;
            }
        }

        private static IMissionSimulator ResolveDefaultSimulator()
        {
            if (GameManager.Instance != null && GameManager.Instance.Simulator != null)
            {
                return GameManager.Instance.Simulator;
            }

            // Try to instantiate the Sprint 2 real simulator from CoreLogic if present, else fallback to MockMissionSimulator
            IMissionSimulator realSim = TryCreateCoreInstance<IMissionSimulator>("AstraLink.Core.OrbitSimulator")
                                        ?? TryCreateCoreInstance<IMissionSimulator>("AstraLink.Core.MissionSimulator");
            return realSim ?? new MockMissionSimulator();
        }

        private static IAstraAuditor TryCreateDefaultAuditor()
        {
            return TryCreateCoreInstance<IAstraAuditor>("AstraLink.Core.AstraAuditor");
        }

        private static T TryCreateCoreInstance<T>(string fullTypeName) where T : class
        {
            try
            {
                Type type = Type.GetType(fullTypeName + ", AstraLink.Core");
                if (type != null && typeof(T).IsAssignableFrom(type))
                {
                    return Activator.CreateInstance(type) as T;
                }
            }
            catch
            {
                // Ignore if S2-01 / S2-02 class has no parameterless constructor or is not yet merged
            }
            return null;
        }

        private void WireRadarCallbacks()
        {
            if (orbitRadarUI == null) return;

            orbitRadarUI.OnTestFlightStarted -= HandleRadarTestFlightButtonClicked;
            orbitRadarUI.OnTestFlightStarted += HandleRadarTestFlightButtonClicked;
        }

        private void HandleRadarTestFlightButtonClicked()
        {
            // When the user clicks 'Start Flight Test' inside the embedded OrbitRadarUI, run the synchronized S2-04 playback
            if (orbitRadarUI != null && orbitRadarUI.IsPlaying)
            {
                orbitRadarUI.StopTestFlight(resetToStart: false);
            }
            RestartPlayback();
        }

        private void BindControlButtons()
        {
            if (playPauseButton != null)
            {
                playPauseButton.onClick.RemoveAllListeners();
                playPauseButton.onClick.AddListener(TogglePlayPause);
            }

            if (restartFlightButton != null)
            {
                restartFlightButton.onClick.RemoveAllListeners();
                restartFlightButton.onClick.AddListener(RestartPlayback);
            }

            if (speed1xButton != null)
            {
                speed1xButton.onClick.RemoveAllListeners();
                speed1xButton.onClick.AddListener(() => SetPlaybackSpeed(1.0f));
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

            if (manualOverrideUI != null)
            {
                manualOverrideUI.OnManualCommandIssued -= HandleManualCommandIssued;
                manualOverrideUI.OnManualCommandIssued += HandleManualCommandIssued;
            }

            WireRadarCallbacks();
        }

        private void UpdatePlayPauseButtonVisual()
        {
            if (playPauseButtonLabel != null)
            {
                playPauseButtonLabel.text = _isPlaying ? "Pause Flight" : "Play Flight";
            }

            if (playPauseButton != null)
            {
                Image img = playPauseButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = _isPlaying
                        ? new Color(0.76f, 0.48f, 0.12f, 1.0f)
                        : new Color(0.14f, 0.58f, 0.36f, 1.0f);
                }
            }
        }

        private void UpdateSpeedButtonsVisuals()
        {
            HighlightSpeedButton(speed1xButton, Mathf.Approximately(_speedMultiplier, 1.0f));
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

        private static List<SatelliteComponent> CreateDefaultComponents()
        {
            return new List<SatelliteComponent>
            {
                new SatelliteComponent
                {
                    Id = "bus_cubesat_3u",
                    Name = "CubeSat 3U Platform",
                    Category = ComponentCategory.CorePlatform,
                    MassKg = 4.0f,
                    CostThousands = 15.0f,
                    BasePowerDrawWatts = 5.0f,
                    RadiationShielding = 10.0f,
                    BaseReliability = 0.96f
                },
                new SatelliteComponent
                {
                    Id = "pwr_solar_deployable",
                    Name = "S-Class Deployable Solar Panels",
                    Category = ComponentCategory.PowerGeneration,
                    MassKg = 6.0f,
                    CostThousands = 35.0f,
                    SolarWatts = 45.0f,
                    BaseReliability = 0.97f
                },
                new SatelliteComponent
                {
                    Id = "bat_liion_pack",
                    Name = "Li-Ion 45 Wh Battery Pack",
                    Category = ComponentCategory.EnergyStorage,
                    MassKg = 4.0f,
                    CostThousands = 20.0f,
                    BatteryCapacityWh = 45.0f,
                    BaseReliability = 0.98f
                },
                new SatelliteComponent
                {
                    Id = "comm_sband_16gb",
                    Name = "S-Band Transceiver + 16 GB SSD",
                    Category = ComponentCategory.CommsAndStorage,
                    MassKg = 3.0f,
                    CostThousands = 18.0f,
                    BasePowerDrawWatts = 2.0f,
                    ActivePowerDrawWatts = 6.0f,
                    SsdCapacityGb = 16.0f,
                    DownlinkRateGbPerTick = 2.0f,
                    BaseReliability = 0.97f
                },
                new SatelliteComponent
                {
                    Id = "sci_optical_cam",
                    Name = "Multispectral Weather Camera",
                    Category = ComponentCategory.SciencePayload,
                    MassKg = 3.5f,
                    CostThousands = 22.0f,
                    BasePowerDrawWatts = 1.0f,
                    ActivePowerDrawWatts = 12.0f,
                    DataGenGbPerTick = 0.8f,
                    SciencePointsPerTick = 6.0f,
                    SensorTag = "Camera",
                    BaseReliability = 0.96f
                }
            };
        }

        private static RocketOption CreateDefaultRocket()
        {
            return new RocketOption
            {
                Id = "rocket_light",
                Name = "Strila-1 Light Launch Vehicle",
                CostThousands = 30.0f,
                MaxMassLeoKg = 25.0f,
                MaxMassLunarKg = 8.0f,
                MaxMassMarsKg = 2.0f,
                MaxMassDeepSpaceKg = 2.0f,
                BaseReliability = 0.95f
            };
        }

        private static MissionGoal CreateDefaultGoal()
        {
            return new MissionGoal
            {
                Id = "m1_leo_cubesat",
                Title = "1. LEO Weather CubeSat",
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

        /// <summary>
        /// Builds or binds the complete S2-04 &amp; S2-05 UI hierarchy so the prefab works out-of-the-box without null references.
        /// </summary>
        public void EnsureMissionReportHierarchy()
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
                bg.color = new Color(0.03f, 0.06f, 0.11f, 1.0f);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. Top Playback & Speed Controls Bar (1x / 5x / 10x)
            EnsureTopPlaybackBar(rootRt, font);

            // 2. Left Column: 2D Orbit Radar (OrbitRadarUI) + EventPopupUI
            if (orbitRadarUI == null)
            {
                Transform existingRadar = rootRt.Find("OrbitRadar");
                if (existingRadar != null)
                {
                    orbitRadarUI = existingRadar.GetComponent<OrbitRadarUI>();
                }
                else
                {
                    var radarObj = new GameObject("OrbitRadar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(OrbitRadarUI));
                    radarObj.transform.SetParent(rootRt, false);
                    var rRt = radarObj.GetComponent<RectTransform>();
                    rRt.anchorMin = new Vector2(0.015f, 0.28f);
                    rRt.anchorMax = new Vector2(0.46f, 0.91f);
                    rRt.offsetMin = Vector2.zero;
                    rRt.offsetMax = Vector2.zero;
                    orbitRadarUI = radarObj.GetComponent<OrbitRadarUI>();
                }
            }

            if (eventPopupUI == null)
            {
                Transform existingPopup = rootRt.Find("EventPopupUI");
                if (existingPopup != null)
                {
                    eventPopupUI = existingPopup.GetComponent<EventPopupUI>();
                }
                else
                {
                    var popupObj = new GameObject("EventPopupUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(EventPopupUI));
                    popupObj.transform.SetParent(rootRt, false);
                    var pRt = popupObj.GetComponent<RectTransform>();
                    pRt.anchorMin = new Vector2(0.015f, 0.185f);
                    pRt.anchorMax = new Vector2(0.46f, 0.27f);
                    pRt.offsetMin = Vector2.zero;
                    pRt.offsetMax = Vector2.zero;
                    eventPopupUI = popupObj.GetComponent<EventPopupUI>();
                }
            }

            // 3. Right Column: TelemetryHUD (5 Sliders/FillBars + Phase Badges + Green Autopilot Rules) & ManualOverrideUI (3 Buttons + Delay Bar)
            if (telemetryHUD == null)
            {
                Transform existingHud = rootRt.Find("TelemetryHUD");
                if (existingHud != null)
                {
                    telemetryHUD = existingHud.GetComponent<TelemetryHUD>();
                }
                else
                {
                    var hudObj = new GameObject("TelemetryHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TelemetryHUD));
                    hudObj.transform.SetParent(rootRt, false);
                    var hRt = hudObj.GetComponent<RectTransform>();
                    hRt.anchorMin = new Vector2(0.475f, 0.39f);
                    hRt.anchorMax = new Vector2(0.985f, 0.91f);
                    hRt.offsetMin = Vector2.zero;
                    hRt.offsetMax = Vector2.zero;
                    telemetryHUD = hudObj.GetComponent<TelemetryHUD>();
                }
            }

            if (manualOverrideUI == null)
            {
                Transform existingManual = rootRt.Find("ManualOverrideUI");
                if (existingManual != null)
                {
                    manualOverrideUI = existingManual.GetComponent<ManualOverrideUI>();
                }
                else
                {
                    var manObj = new GameObject("ManualOverrideUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ManualOverrideUI));
                    manObj.transform.SetParent(rootRt, false);
                    var mRt = manObj.GetComponent<RectTransform>();
                    mRt.anchorMin = new Vector2(0.475f, 0.185f);
                    mRt.anchorMax = new Vector2(0.985f, 0.38f);
                    mRt.offsetMin = Vector2.zero;
                    mRt.offsetMax = Vector2.zero;
                    manualOverrideUI = manObj.GetComponent<ManualOverrideUI>();
                }
            }

            // 4. Bottom Panel: Post-Flight Debrief & 'Why Did It Fail?' Breakdown (S2-02 / S2-05)
            EnsureDebriefBottomPanel(rootRt, font);
        }

        private void EnsureTopPlaybackBar(RectTransform rootRt, Font font)
        {
            Transform existingBar = rootRt.Find("TopPlaybackBar");
            RectTransform barRt;
            if (existingBar != null)
            {
                barRt = existingBar.GetComponent<RectTransform>();
            }
            else
            {
                var barObj = new GameObject("TopPlaybackBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                barObj.transform.SetParent(rootRt, false);
                barRt = barObj.GetComponent<RectTransform>();
                barRt.anchorMin = new Vector2(0.015f, 0.92f);
                barRt.anchorMax = new Vector2(0.985f, 0.985f);
                barRt.offsetMin = Vector2.zero;
                barRt.offsetMax = Vector2.zero;
                barObj.GetComponent<Image>().color = new Color(0.06f, 0.11f, 0.19f, 0.98f);
            }

            if (playbackHeaderLabel == null)
            {
                playbackHeaderLabel = FindOrCreateText(
                    barRt,
                    "PlaybackHeaderLabel",
                    new Vector2(0.015f, 0.50f),
                    new Vector2(0.55f, 0.96f),
                    "MISSION CONTROL TELEMETRY & DEBRIEF",
                    14,
                    FontStyle.Bold,
                    new Color(0.88f, 0.95f, 1.0f, 1.0f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (playbackTickCounterLabel == null)
            {
                playbackTickCounterLabel = FindOrCreateText(
                    barRt,
                    "PlaybackTickCounterLabel",
                    new Vector2(0.015f, 0.06f),
                    new Vector2(0.55f, 0.50f),
                    "Orbit #1   |   Tick: 01 / 20   |   u = 0.00   |   Speed: 1x",
                    12,
                    FontStyle.Normal,
                    new Color(0.68f, 0.86f, 1.0f, 0.96f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (playPauseButton == null)
            {
                playPauseButton = CreateBarButton(
                    barRt,
                    "Btn_PlayPause",
                    "Pause Flight",
                    new Vector2(0.56f, 0.12f),
                    new Vector2(0.67f, 0.88f),
                    new Color(0.76f, 0.48f, 0.12f, 1.0f),
                    font,
                    out playPauseButtonLabel);
            }

            if (restartFlightButton == null)
            {
                Text dummy;
                restartFlightButton = CreateBarButton(
                    barRt,
                    "Btn_RestartFlight",
                    "Restart Flight",
                    new Vector2(0.68f, 0.12f),
                    new Vector2(0.79f, 0.88f),
                    new Color(0.16f, 0.34f, 0.54f, 1.0f),
                    font,
                    out dummy);
            }

            if (speed1xButton == null)
            {
                Text dummy;
                speed1xButton = CreateBarButton(
                    barRt,
                    "Btn_Speed1x",
                    "1x",
                    new Vector2(0.805f, 0.12f),
                    new Vector2(0.865f, 0.88f),
                    new Color(0.14f, 0.62f, 0.88f, 1.0f),
                    font,
                    out dummy);
            }

            if (speed5xButton == null)
            {
                Text dummy;
                speed5xButton = CreateBarButton(
                    barRt,
                    "Btn_Speed5x",
                    "5x",
                    new Vector2(0.87f, 0.12f),
                    new Vector2(0.93f, 0.88f),
                    new Color(0.13f, 0.20f, 0.32f, 1.0f),
                    font,
                    out dummy);
            }

            if (speed10xButton == null)
            {
                Text dummy;
                speed10xButton = CreateBarButton(
                    barRt,
                    "Btn_Speed10x",
                    "10x",
                    new Vector2(0.935f, 0.12f),
                    new Vector2(0.992f, 0.88f),
                    new Color(0.13f, 0.20f, 0.32f, 1.0f),
                    font,
                    out dummy);
            }
        }

        private void EnsureDebriefBottomPanel(RectTransform rootRt, Font font)
        {
            Transform existingDebrief = rootRt.Find("DebriefSummaryPanel");
            RectTransform panelRt;
            if (existingDebrief != null)
            {
                panelRt = existingDebrief.GetComponent<RectTransform>();
                debriefBannerBg = existingDebrief.GetComponent<Image>();
            }
            else
            {
                var panelObj = new GameObject("DebriefSummaryPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panelObj.transform.SetParent(rootRt, false);
                panelRt = panelObj.GetComponent<RectTransform>();
                panelRt.anchorMin = new Vector2(0.015f, 0.015f);
                panelRt.anchorMax = new Vector2(0.985f, 0.175f);
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
                debriefBannerBg = panelObj.GetComponent<Image>();
                debriefBannerBg.color = new Color(0.07f, 0.20f, 0.18f, 0.96f);
            }

            if (debriefStatusTitleLabel == null)
            {
                debriefStatusTitleLabel = FindOrCreateText(
                    panelRt,
                    "DebriefStatusTitleLabel",
                    new Vector2(0.015f, 0.76f),
                    new Vector2(0.48f, 0.97f),
                    "MISSION DEBRIEF: SUCCESS   |   FINAL SCORE: 162.5 PTS",
                    14,
                    FontStyle.Bold,
                    new Color(0.48f, 1.0f, 0.72f, 1.0f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (debriefMetricsSummaryLabel == null)
            {
                debriefMetricsSummaryLabel = FindOrCreateText(
                    panelRt,
                    "DebriefMetricsSummaryLabel",
                    new Vector2(0.48f, 0.76f),
                    new Vector2(0.985f, 0.97f),
                    "Delivered Science: 120.0 / 100.0 pts   |   Lost SSD Data: 0.00 GB   |   Min Battery: 60%   |   Final Health: 100%",
                    12,
                    FontStyle.Bold,
                    Color.white,
                    TextAnchor.MiddleRight,
                    font);
            }

            if (failureTitleLabel == null)
            {
                failureTitleLabel = FindOrCreateText(
                    panelRt,
                    "FailureTitleLabel",
                    new Vector2(0.015f, 0.52f),
                    new Vector2(0.985f, 0.75f),
                    "SUMMARY: Nominal Mission Completion — All NASA Resource Constraints Satisfied",
                    13,
                    FontStyle.Bold,
                    new Color(0.76f, 0.98f, 0.84f, 1.0f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (failureCauseLabel == null)
            {
                failureCauseLabel = FindOrCreateText(
                    panelRt,
                    "FailureCauseLabel",
                    new Vector2(0.015f, 0.28f),
                    new Vector2(0.65f, 0.52f),
                    "TELEMETRY ANALYSIS: Delivered 120.0 / 100.0 science points with 60% minimum battery reserve.",
                    12,
                    FontStyle.Normal,
                    new Color(0.88f, 0.94f, 1.0f, 0.96f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (failureRecommendationLabel == null)
            {
                failureRecommendationLabel = FindOrCreateText(
                    panelRt,
                    "FailureRecommendationLabel",
                    new Vector2(0.015f, 0.04f),
                    new Vector2(0.65f, 0.28f),
                    "NEXT STEP: Engineering margin verified. Ready for next orbital contract.",
                    12,
                    FontStyle.Bold,
                    new Color(0.64f, 0.90f, 1.0f, 0.98f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (astraAuditSummaryLabel == null)
            {
                astraAuditSummaryLabel = FindOrCreateText(
                    panelRt,
                    "AstraAuditSummaryLabel",
                    new Vector2(0.66f, 0.06f),
                    new Vector2(0.985f, 0.50f),
                    "A.S.T.R.A. Pre-Flight Audit: 0 warnings — Configuration passed all 7 deterministic checks.",
                    12,
                    FontStyle.Bold,
                    new Color(0.56f, 0.94f, 0.76f, 1.0f),
                    TextAnchor.MiddleRight,
                    font);
            }
        }

        private static Button CreateBarButton(
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
                13,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter,
                font);

            return btn;
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
    }
}
