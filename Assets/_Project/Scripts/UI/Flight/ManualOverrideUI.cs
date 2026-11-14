using System;
using System.Collections.Generic;
using System.Globalization;
using AstraLink.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// Manual Ground-Control Override UI Controller (Task S2-04).
    /// Provides 3 manual command buttons:
    /// 1) Toggle Instruments (<see cref="AutopilotAction.ScienceOff"/> / <see cref="AutopilotAction.ScienceOn"/>),
    /// 2) Toggle Comms (<see cref="AutopilotAction.CommsOff"/> / <see cref="AutopilotAction.CommsOn"/>),
    /// 3) Enable Safe Mode (<see cref="AutopilotAction.SafeModeOn"/> / <see cref="AutopilotAction.SafeModeOff"/>).
    /// When a button is clicked:
    /// - Starts a visual signal-delay countdown timer according to <see cref="OrbitDefinition.SignalDelaySeconds"/>,
    /// - Appends a <see cref="ManualCommandRequest"/> to <see cref="activeManualCommands"/>,
    /// - Calls <see cref="IMissionSimulator.SimulateFullMission"/> for immediate deterministic recalculation of the remaining flight.
    /// </summary>
    public class ManualOverrideUI : MonoBehaviour
    {
        [Header("Connected Flight Components")]
        [SerializeField] private OrbitRadarUI linkedRadarUI;
        [SerializeField] private TelemetryHUD linkedTelemetryHUD;

        [Header("3 Manual Command Buttons")]
        [SerializeField] private Button toggleInstrumentsButton;
        [SerializeField] private Text toggleInstrumentsButtonText;
        [SerializeField] private Button toggleCommsButton;
        [SerializeField] private Text toggleCommsButtonText;
        [SerializeField] private Button enableSafeModeButton;
        [SerializeField] private Text enableSafeModeButtonText;

        [Header("Visual Signal Delay Timer (OrbitDefinition.SignalDelaySeconds)")]
        [SerializeField] private Slider signalDelaySlider;
        [SerializeField] private Image signalDelayFillImage;
        [SerializeField] private Image signalDelayBannerBg;
        [SerializeField] private Text signalDelayHeaderText;
        [SerializeField] private Text signalDelayDetailText;
        [SerializeField] private Text commandQueueLogText;

        /// <summary>
        /// Active list of manual commands issued during the current flight, passed into <see cref="IMissionSimulator.SimulateFullMission"/>.
        /// </summary>
        [SerializeField] private List<ManualCommandRequest> activeManualCommands = new List<ManualCommandRequest>();

        private IMissionSimulator _simulator;
        private List<SatelliteComponent> _components = new List<SatelliteComponent>();
        private RocketOption _rocket;
        private OrbitDefinition _currentOrbit;
        private MissionGoal _goal;
        private List<AutopilotRule> _rules = new List<AutopilotRule>();
        private List<SpaceEventDefinition> _events;

        private bool _isDelayTimerRunning;
        private float _delayTotalSeconds;
        private float _delayRemainingSeconds;
        private ManualCommandRequest _latestPendingCommand;

        /// <summary>
        /// Gets the active list of manual commands queued for this mission.
        /// </summary>
        public List<ManualCommandRequest> ActiveManualCommands => activeManualCommands;

        /// <summary>
        /// Gets a value indicating whether a manual command signal is currently in transit to the spacecraft.
        /// </summary>
        public bool IsSignalInTransit => _isDelayTimerRunning;
        public float DelayRemainingSeconds => _delayRemainingSeconds;
        public float DelayTotalSeconds => _delayTotalSeconds;
        public string LatestCommandSummary => _latestPendingCommand != null
            ? $"{_latestPendingCommand.Action} @ T{_latestPendingCommand.ExecuteAtTick}"
            : "NO MANUAL COMMANDS";

        /// <summary>
        /// Fired whenever a manual command is queued and <see cref="IMissionSimulator.SimulateFullMission"/> produces a recalculated <see cref="MissionReport"/>.
        /// </summary>
        public event Action<MissionReport, ManualCommandRequest> OnMissionRecalculated;

        private void Awake()
        {
            EnsureManualOverrideHierarchy();
            BindButtons();
        }

        private void OnEnable()
        {
            if (linkedRadarUI != null)
            {
                linkedRadarUI.OnTickUpdated -= HandleRadarTickUpdated;
                linkedRadarUI.OnTickUpdated += HandleRadarTickUpdated;
            }
        }

        private void OnDisable()
        {
            if (linkedRadarUI != null)
            {
                linkedRadarUI.OnTickUpdated -= HandleRadarTickUpdated;
            }
        }

        /// <summary>
        /// Initializes the manual override controller with the simulator, mission configuration, and linked radar/telemetry components.
        /// </summary>
        public void Initialize(
            IMissionSimulator simulator,
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules,
            OrbitRadarUI radar = null,
            TelemetryHUD telemetry = null,
            List<SpaceEventDefinition> events = null)
        {
            EnsureManualOverrideHierarchy();
            BindButtons();

            _simulator = simulator ?? (GameManager.Instance != null && GameManager.Instance.Simulator != null
                ? GameManager.Instance.Simulator
                : new OrbitSimulator());
            _components = components ?? new List<SatelliteComponent>();
            _rocket = rocket;
            _currentOrbit = orbit ?? OrbitRadarUI.CreateDefaultLeoOrbit();
            _goal = goal;
            _rules = rules ?? new List<AutopilotRule>();
            _events = events;

            if (radar != null)
            {
                if (linkedRadarUI != null)
                {
                    linkedRadarUI.OnTickUpdated -= HandleRadarTickUpdated;
                }
                linkedRadarUI = radar;
                linkedRadarUI.OnTickUpdated -= HandleRadarTickUpdated;
                linkedRadarUI.OnTickUpdated += HandleRadarTickUpdated;
            }

            if (telemetry != null)
            {
                linkedTelemetryHUD = telemetry;
            }

            ClearManualCommands();
            UpdateButtonLabelsFromSnapshot(linkedRadarUI != null ? linkedRadarUI.CurrentTickSnapshot : null);
        }

        /// <summary>
        /// Updates the active <see cref="OrbitDefinition"/> so <see cref="OrbitDefinition.SignalDelaySeconds"/> is accurate.
        /// </summary>
        public void SetOrbit(OrbitDefinition orbit)
        {
            if (orbit == null) return;
            _currentOrbit = orbit;
            RefreshIdleDelayBanner();
        }

        /// <summary>
        /// Clears all queued manual commands and resets the signal delay timer UI.
        /// </summary>
        public void ClearManualCommands()
        {
            activeManualCommands.Clear();
            _isDelayTimerRunning = false;
            _delayTotalSeconds = _currentOrbit != null ? Mathf.Max(0.1f, _currentOrbit.SignalDelaySeconds) : 1.0f;
            _delayRemainingSeconds = 0f;
            _latestPendingCommand = null;

            RefreshIdleDelayBanner();
            RefreshCommandQueueLog();
        }

        /// <summary>
        /// Button 1 Handler: Toggles onboard science instruments (<see cref="AutopilotAction.ScienceOff"/> / <see cref="AutopilotAction.ScienceOn"/>).
        /// </summary>
        public void TriggerToggleInstruments()
        {
            SimulationTickSnapshot snap = GetCurrentTickSnapshot();
            bool currentlyOn = snap == null || snap.ScienceActive;
            AutopilotAction targetAction = currentlyOn ? AutopilotAction.ScienceOff : AutopilotAction.ScienceOn;
            IssueManualCommand(targetAction);
        }

        /// <summary>
        /// Button 2 Handler: Toggles onboard communications transmitter (<see cref="AutopilotAction.CommsOff"/> / <see cref="AutopilotAction.CommsOn"/>).
        /// </summary>
        public void TriggerToggleComms()
        {
            SimulationTickSnapshot snap = GetCurrentTickSnapshot();
            bool currentlyOn = snap == null || snap.CommsActive;
            AutopilotAction targetAction = currentlyOn ? AutopilotAction.CommsOff : AutopilotAction.CommsOn;
            IssueManualCommand(targetAction);
        }

        /// <summary>
        /// Button 3 Handler: Enables (or toggles off) spacecraft Safe Mode (<see cref="AutopilotAction.SafeModeOn"/> / <see cref="AutopilotAction.SafeModeOff"/>).
        /// </summary>
        public void TriggerEnableSafeMode()
        {
            SimulationTickSnapshot snap = GetCurrentTickSnapshot();
            bool currentlySafe = snap != null && snap.SafeModeActive;
            AutopilotAction targetAction = currentlySafe ? AutopilotAction.SafeModeOff : AutopilotAction.SafeModeOn;
            IssueManualCommand(targetAction);
        }

        /// <summary>
        /// Queues a <see cref="ManualCommandRequest"/> with signal delay according to <see cref="OrbitDefinition.SignalDelaySeconds"/>,
        /// starts the visual countdown timer, and invokes <see cref="IMissionSimulator.SimulateFullMission"/> for immediate flight recalculation.
        /// </summary>
        public ManualCommandRequest IssueManualCommand(AutopilotAction action)
        {
            EnsureManualOverrideHierarchy();

            if (action == AutopilotAction.None)
            {
                return null;
            }

            int currentTick = linkedRadarUI != null ? linkedRadarUI.CurrentTickIndex : 0;
            float delaySeconds = _currentOrbit != null ? Mathf.Max(0.0f, _currentOrbit.SignalDelaySeconds) : 1.0f;

            ManualCommandRequest request = AutopilotEvaluator.CreateManualCommand(currentTick, action, delaySeconds);
            activeManualCommands.Add(request);
            _latestPendingCommand = request;

            // Start visual signal-delay countdown timer
            _delayTotalSeconds = Mathf.Max(0.25f, delaySeconds);
            _delayRemainingSeconds = _delayTotalSeconds;
            _isDelayTimerRunning = true;
            UpdateDelayTimerVisuals();

            // Immediately recalculate the mission with activeManualCommands via SimulateFullMission(...)
            MissionReport recalculatedReport = RecalculateMission(currentTick, request);

            RefreshCommandQueueLog();

            if (recalculatedReport != null)
            {
                OnMissionRecalculated?.Invoke(recalculatedReport, request);
            }

            return request;
        }

        private MissionReport RecalculateMission(int issuedAtTick, ManualCommandRequest latestRequest)
        {
            if (_simulator == null)
            {
                _simulator = (GameManager.Instance != null && GameManager.Instance.Simulator != null)
                    ? GameManager.Instance.Simulator
                    : new OrbitSimulator(_events);
            }

            if (_currentOrbit == null)
            {
                _currentOrbit = linkedRadarUI != null && linkedRadarUI.CurrentOrbit != null
                    ? linkedRadarUI.CurrentOrbit
                    : OrbitRadarUI.CreateDefaultLeoOrbit();
            }

            MissionReport recalculated = _simulator.SimulateFullMission(
                _components,
                _rocket,
                _currentOrbit,
                _goal,
                _rules,
                activeManualCommands,
                _events);

            // Ensure manual commands also visibly take effect from ExecuteAtTick onward ONLY when MockMissionSimulator is active
            if (recalculated != null && recalculated.Ticks != null && recalculated.Ticks.Count > 0)
            {
                if (_simulator is MockMissionSimulator)
                {
                    ApplyManualOverridesToReportSnapshots(recalculated, activeManualCommands);
                }

                if (linkedRadarUI != null)
                {
                    bool wasPlaying = linkedRadarUI.IsPlaying;
                    linkedRadarUI.LoadMissionReport(recalculated, _currentOrbit, autoPlay: wasPlaying, resetTickIndex: false);
                }

                SimulationTickSnapshot currentSnap = GetCurrentTickSnapshot() ?? recalculated.Ticks[Mathf.Clamp(issuedAtTick, 0, recalculated.Ticks.Count - 1)];
                UpdateButtonLabelsFromSnapshot(currentSnap);

                if (linkedTelemetryHUD != null && currentSnap != null)
                {
                    linkedTelemetryHUD.UpdateFromTick(currentSnap, issuedAtTick, recalculated.Ticks.Count);
                }
            }

            return recalculated;
        }

        /// <summary>
        /// Ensures that manual commands in <paramref name="commands"/> are deterministically reflected in <see cref="MissionReport.Ticks"/>
        /// starting at each command's <see cref="ManualCommandRequest.ExecuteAtTick"/>.
        /// </summary>
        public static void ApplyManualOverridesToReportSnapshots(MissionReport report, List<ManualCommandRequest> commands)
        {
            if (report == null || report.Ticks == null || commands == null || commands.Count == 0)
            {
                return;
            }

            for (int c = 0; c < commands.Count; c++)
            {
                ManualCommandRequest cmd = commands[c];
                if (cmd == null) continue;

                for (int t = Math.Max(0, cmd.ExecuteAtTick); t < report.Ticks.Count; t++)
                {
                    SimulationTickSnapshot snap = report.Ticks[t];
                    if (snap == null) continue;

                    switch (cmd.Action)
                    {
                        case AutopilotAction.ScienceOn:
                            if (!snap.SafeModeActive) snap.ScienceActive = true;
                            break;
                        case AutopilotAction.ScienceOff:
                            snap.ScienceActive = false;
                            break;
                        case AutopilotAction.CommsOn:
                            if (!snap.SafeModeActive) snap.CommsActive = true;
                            break;
                        case AutopilotAction.CommsOff:
                            snap.CommsActive = false;
                            break;
                        case AutopilotAction.SafeModeOn:
                            snap.SafeModeActive = true;
                            snap.ScienceActive = false;
                            snap.CommsActive = false;
                            snap.PowerDrawWatts = Mathf.Min(snap.PowerDrawWatts, 6.0f);
                            break;
                        case AutopilotAction.SafeModeOff:
                            snap.SafeModeActive = false;
                            snap.ScienceActive = true;
                            snap.CommsActive = true;
                            break;
                    }
                }
            }
        }

        private void Update()
        {
            if (!_isDelayTimerRunning)
            {
                return;
            }

            float speedMultiplier = linkedRadarUI != null ? Mathf.Max(1.0f, linkedRadarUI.SpeedMultiplier) : 1.0f;
            _delayRemainingSeconds -= Time.deltaTime * speedMultiplier;

            if (_delayRemainingSeconds <= 0.0f)
            {
                _delayRemainingSeconds = 0.0f;
                _isDelayTimerRunning = false;
                UpdateDelayCompletedVisuals();
                return;
            }

            UpdateDelayTimerVisuals();
        }

        private void UpdateDelayTimerVisuals()
        {
            float progress = 1.0f - Mathf.Clamp01(_delayRemainingSeconds / Mathf.Max(0.01f, _delayTotalSeconds));

            if (signalDelaySlider != null)
            {
                signalDelaySlider.minValue = 0f;
                signalDelaySlider.maxValue = 1f;
                signalDelaySlider.value = progress;
            }

            if (signalDelayFillImage != null)
            {
                signalDelayFillImage.fillAmount = progress;
                signalDelayFillImage.color = new Color(1.0f, 0.76f, 0.20f, 1.0f);
            }

            if (signalDelayBannerBg != null)
            {
                signalDelayBannerBg.color = new Color(0.36f, 0.24f, 0.06f, 0.98f);
            }

            if (signalDelayHeaderText != null && _latestPendingCommand != null)
            {
                signalDelayHeaderText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "SIGNAL IN TRANSIT: {0}   •   {1:0.1}s / {2:0.1}s REMAINING",
                    FormatActionLabel(_latestPendingCommand.Action),
                    _delayRemainingSeconds,
                    _delayTotalSeconds);
                signalDelayHeaderText.color = new Color(1.0f, 0.90f, 0.44f, 1.0f);
            }

            if (signalDelayDetailText != null && _latestPendingCommand != null)
            {
                signalDelayDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Issued at Tick #{0:00}  ->  Executes at Tick #{1:00}  (SimulateFullMission recalculated)",
                    _latestPendingCommand.IssuedAtTick + 1,
                    _latestPendingCommand.ExecuteAtTick + 1);
            }
        }

        private void UpdateDelayCompletedVisuals()
        {
            if (signalDelaySlider != null)
            {
                signalDelaySlider.value = 1.0f;
            }

            if (signalDelayFillImage != null)
            {
                signalDelayFillImage.fillAmount = 1.0f;
                signalDelayFillImage.color = new Color(0.20f, 0.88f, 0.48f, 1.0f);
            }

            if (signalDelayBannerBg != null)
            {
                signalDelayBannerBg.color = new Color(0.08f, 0.32f, 0.20f, 0.98f);
            }

            if (signalDelayHeaderText != null && _latestPendingCommand != null)
            {
                signalDelayHeaderText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "SIGNAL RECEIVED AT SPACECRAFT: {0} EXECUTED!",
                    FormatActionLabel(_latestPendingCommand.Action));
                signalDelayHeaderText.color = new Color(0.48f, 1.0f, 0.68f, 1.0f);
            }

            if (signalDelayDetailText != null && _latestPendingCommand != null)
            {
                signalDelayDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Command {0} applied on Tick #{1:00}. Remaining flight trajectory updated.",
                    _latestPendingCommand.Action,
                    _latestPendingCommand.ExecuteAtTick + 1);
            }
        }

        private void RefreshIdleDelayBanner()
        {
            float delaySec = _currentOrbit != null ? _currentOrbit.SignalDelaySeconds : 1.0f;
            int delayTicks = AutopilotEvaluator.CalculateDelayTicks(delaySec);

            if (signalDelaySlider != null)
            {
                signalDelaySlider.value = 0f;
            }
            if (signalDelayFillImage != null)
            {
                signalDelayFillImage.fillAmount = 0f;
            }
            if (signalDelayBannerBg != null)
            {
                signalDelayBannerBg.color = new Color(0.08f, 0.13f, 0.22f, 0.96f);
            }
            if (signalDelayHeaderText != null)
            {
                signalDelayHeaderText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "GROUND UPLINK DELAY: {0:0.1}s  ({1} tick{2} latency on {3})",
                    delaySec,
                    delayTicks,
                    delayTicks == 1 ? string.Empty : "s",
                    _currentOrbit != null ? _currentOrbit.Name : "Orbit");
                signalDelayHeaderText.color = new Color(0.66f, 0.86f, 1.0f, 1.0f);
            }
            if (signalDelayDetailText != null)
            {
                signalDelayDetailText.text = "Click any manual override button below to transmit a signal-delayed command and recalculate the flight.";
            }
        }

        private void RefreshCommandQueueLog()
        {
            if (commandQueueLogText == null) return;

            if (activeManualCommands == null || activeManualCommands.Count == 0)
            {
                commandQueueLogText.text = "Active Manual Commands (0): None queued. Spacecraft running on onboard autopilot rules.";
                return;
            }

            int startIdx = Mathf.Max(0, activeManualCommands.Count - 3);
            var parts = new List<string>();
            for (int i = startIdx; i < activeManualCommands.Count; i++)
            {
                ManualCommandRequest cmd = activeManualCommands[i];
                if (cmd == null) continue;
                parts.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0}: Tick #{1} -> #{2}]",
                    cmd.Action,
                    cmd.IssuedAtTick + 1,
                    cmd.ExecuteAtTick + 1));
            }

            commandQueueLogText.text = string.Format(
                CultureInfo.InvariantCulture,
                "Active Manual Commands ({0}): {1}",
                activeManualCommands.Count,
                string.Join("  •  ", parts.ToArray()));
        }

        private void HandleRadarTickUpdated(SimulationTickSnapshot snapshot, int tickIndex)
        {
            UpdateButtonLabelsFromSnapshot(snapshot);
        }

        /// <summary>
        /// Updates the 3 manual command button labels and colors to reflect the current subsystem states.
        /// </summary>
        public void UpdateButtonLabelsFromSnapshot(SimulationTickSnapshot snapshot)
        {
            bool sciOn = snapshot == null || snapshot.ScienceActive;
            bool commsOn = snapshot == null || snapshot.CommsActive;
            bool safeOn = snapshot != null && snapshot.SafeModeActive;

            if (toggleInstrumentsButtonText != null)
            {
                toggleInstrumentsButtonText.text = sciOn
                    ? "1. TOGGLE INSTRUMENTS: TURN OFF (Currently ON)"
                    : "1. TOGGLE INSTRUMENTS: TURN ON (Currently OFF)";
            }

            if (toggleCommsButtonText != null)
            {
                toggleCommsButtonText.text = commsOn
                    ? "2. TOGGLE COMMS LINK: TURN OFF (Currently ON)"
                    : "2. TOGGLE COMMS LINK: TURN ON (Currently OFF)";
            }

            if (enableSafeModeButtonText != null)
            {
                enableSafeModeButtonText.text = safeOn
                    ? "3. SAFE MODE ACTIVE — CLICK TO RESUME OPS"
                    : "3. ENABLE SAFE MODE (EMERGENCY SHIELD)";
            }

            if (enableSafeModeButton != null)
            {
                Image img = enableSafeModeButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = safeOn
                        ? new Color(0.16f, 0.56f, 0.34f, 1.0f)
                        : new Color(0.68f, 0.22f, 0.16f, 1.0f);
                }
            }
        }

        private SimulationTickSnapshot GetCurrentTickSnapshot()
        {
            return linkedRadarUI != null ? linkedRadarUI.CurrentTickSnapshot : null;
        }

        private static string FormatActionLabel(AutopilotAction action)
        {
            switch (action)
            {
                case AutopilotAction.ScienceOn: return "INSTRUMENTS ON (ScienceOn)";
                case AutopilotAction.ScienceOff: return "INSTRUMENTS OFF (ScienceOff)";
                case AutopilotAction.CommsOn: return "TRANSMITTER ON (CommsOn)";
                case AutopilotAction.CommsOff: return "TRANSMITTER OFF (CommsOff)";
                case AutopilotAction.SafeModeOn: return "SAFE MODE ON (SafeModeOn)";
                case AutopilotAction.SafeModeOff: return "SAFE MODE OFF (SafeModeOff)";
                default: return action.ToString();
            }
        }

        private void BindButtons()
        {
            if (toggleInstrumentsButton != null)
            {
                toggleInstrumentsButton.onClick.RemoveAllListeners();
                toggleInstrumentsButton.onClick.AddListener(TriggerToggleInstruments);
            }

            if (toggleCommsButton != null)
            {
                toggleCommsButton.onClick.RemoveAllListeners();
                toggleCommsButton.onClick.AddListener(TriggerToggleComms);
            }

            if (enableSafeModeButton != null)
            {
                enableSafeModeButton.onClick.RemoveAllListeners();
                enableSafeModeButton.onClick.AddListener(TriggerEnableSafeMode);
            }
        }

        /// <summary>
        /// Ensures the manual override UI hierarchy exists and is bound.
        /// </summary>
        public void EnsureManualOverrideHierarchy()
        {
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
            {
                rootRt = gameObject.AddComponent<RectTransform>();
            }

            Transform existingRoot = rootRt.Find("ManualOverrideRoot");
            if (existingRoot != null)
            {
                return;
            }

            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.09f, 0.16f, 0.96f);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject contentRoot = CreateUIObject("ManualOverrideRoot", rootRt);
            StretchFull(contentRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup rootLayout = contentRoot.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(14, 14, 10, 10);
            rootLayout.spacing = 8f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            Text header = CreateText(
                "ManualHeader",
                contentRoot.transform,
                font,
                "MANUAL GROUND OVERRIDE COMMANDS (Signal-Delayed Uplink)",
                15,
                FontStyle.Bold,
                new Color(1.0f, 0.88f, 0.42f, 1.0f),
                TextAnchor.MiddleLeft);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            // Visual Signal Delay Timer Box
            GameObject delayBox = CreateUIObject("SignalDelayTimerBox", contentRoot.transform);
            signalDelayBannerBg = delayBox.AddComponent<Image>();
            signalDelayBannerBg.color = new Color(0.08f, 0.13f, 0.22f, 0.96f);
            delayBox.AddComponent<LayoutElement>().preferredHeight = 58f;

            VerticalLayoutGroup delayLayout = delayBox.AddComponent<VerticalLayoutGroup>();
            delayLayout.padding = new RectOffset(10, 10, 6, 6);
            delayLayout.spacing = 4f;
            delayLayout.childControlWidth = true;
            delayLayout.childControlHeight = true;
            delayLayout.childForceExpandWidth = true;
            delayLayout.childForceExpandHeight = false;

            signalDelayHeaderText = CreateText(
                "SignalDelayHeaderText",
                delayBox.transform,
                font,
                "GROUND UPLINK DELAY: 1.0s (1 tick latency on LEO 400 km)",
                12,
                FontStyle.Bold,
                new Color(0.66f, 0.86f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft);
            signalDelayHeaderText.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            GameObject trackObj = CreateUIObject("DelaySliderTrack", delayBox.transform);
            Image trackBg = trackObj.AddComponent<Image>();
            trackBg.color = new Color(0.03f, 0.05f, 0.10f, 1.0f);
            trackObj.AddComponent<LayoutElement>().preferredHeight = 10f;

            GameObject fillObj = CreateUIObject("Fill", trackObj.transform);
            StretchFull(fillObj.GetComponent<RectTransform>());
            signalDelayFillImage = fillObj.AddComponent<Image>();
            signalDelayFillImage.sprite = RadarProceduralSprites.GetDiscSprite();
            signalDelayFillImage.type = Image.Type.Filled;
            signalDelayFillImage.fillMethod = Image.FillMethod.Horizontal;
            signalDelayFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            signalDelayFillImage.fillAmount = 0f;
            signalDelayFillImage.color = new Color(1.0f, 0.76f, 0.20f, 1.0f);

            signalDelaySlider = trackObj.AddComponent<Slider>();
            signalDelaySlider.interactable = false;
            signalDelaySlider.transition = Selectable.Transition.None;
            signalDelaySlider.minValue = 0f;
            signalDelaySlider.maxValue = 1f;
            signalDelaySlider.value = 0f;

            signalDelayDetailText = CreateText(
                "SignalDelayDetailText",
                delayBox.transform,
                font,
                "Click any manual override button below to transmit a signal-delayed command and recalculate the flight.",
                11,
                FontStyle.Normal,
                new Color(0.76f, 0.86f, 0.96f, 0.95f),
                TextAnchor.MiddleLeft);
            signalDelayDetailText.gameObject.AddComponent<LayoutElement>().preferredHeight = 15f;

            // 3 Manual Command Buttons
            toggleInstrumentsButton = CreateOverrideButton(
                contentRoot.transform,
                "Btn_ToggleInstruments",
                "1. TOGGLE INSTRUMENTS: TURN OFF (Currently ON)",
                new Color(0.16f, 0.38f, 0.62f, 1.0f),
                font,
                out toggleInstrumentsButtonText);

            toggleCommsButton = CreateOverrideButton(
                contentRoot.transform,
                "Btn_ToggleComms",
                "2. TOGGLE COMMS LINK: TURN OFF (Currently ON)",
                new Color(0.12f, 0.46f, 0.48f, 1.0f),
                font,
                out toggleCommsButtonText);

            enableSafeModeButton = CreateOverrideButton(
                contentRoot.transform,
                "Btn_EnableSafeMode",
                "3. ENABLE SAFE MODE (EMERGENCY SHIELD)",
                new Color(0.68f, 0.22f, 0.16f, 1.0f),
                font,
                out enableSafeModeButtonText);

            commandQueueLogText = CreateText(
                "CommandQueueLogText",
                contentRoot.transform,
                font,
                "Active Manual Commands (0): None queued. Spacecraft running on onboard autopilot rules.",
                11,
                FontStyle.Italic,
                new Color(0.70f, 0.82f, 0.94f, 0.95f),
                TextAnchor.MiddleLeft);
            commandQueueLogText.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
        }

        private static Button CreateOverrideButton(
            Transform parent,
            string name,
            string label,
            Color bgColor,
            Font font,
            out Text labelText)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 36f;

            labelText = CreateText("Label", btnObj.transform, font, label, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(labelText.GetComponent<RectTransform>());
            return btn;
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
            GameObject obj = CreateUIObject(name, parent);
            Text txt = obj.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }
    }
}
