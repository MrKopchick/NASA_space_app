using System;
using System.Collections;
using System.Collections.Generic;
using AstraLink.Core;
using AstraLink.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Workshop
{
    /// <summary>
    /// Task S2-03 (Ivan V): A.S.T.R.A. Pre-Flight Advisory Panel & 3-Second (20-Tick) Dry Run Bench Visualizer.
    /// Opens as a dedicated modal window from the Hangar (like R&D Tech Tree).
    /// - Calls IAstraAuditor.AuditConfiguration(...) automatically or on button click and renders
    ///   AstraAuditMessage cards with Critical / Warning / Info visual highlighting.
    /// - Calls IMissionSimulator.RunDryRun(...) and animates BatteryPercent & SsdUsedPercent curves
    ///   across 1 test orbit (20 ticks) over 3.0 seconds prior to rocket launch.
    /// </summary>
    public class DryRunUI : MonoBehaviour
    {
        public const int DryRunOrbitTicks = 20;
        public const float DryRunDurationSeconds = 3.0f;

        private IAstraAuditor _auditor;
        private IMissionSimulator _simulator;
        private IGameDataRepository _repository;
        private AutopilotUI _autopilotUI;

        private List<SatelliteComponent> _currentComponents = new List<SatelliteComponent>();
        private RocketOption _currentRocket;
        private OrbitDefinition _currentOrbit;
        private MissionGoal _currentGoal;
        private List<AutopilotRule> _currentRules = new List<AutopilotRule>();

        // UI References — A.S.T.R.A. Advisory Panel
        private Text _astraSummaryBadgeText;
        private Transform _auditMessagesContainer;

        // UI References — Dry Run Bench Visualizer (20 ticks / 3 seconds)
        private Button _runDryRunButton;
        private Text _runDryRunButtonText;
        private Text _dryRunStatusText;
        private Text _dryRunTelemetryText;
        private readonly Image[] _tickColumnBgs = new Image[DryRunOrbitTicks];
        private readonly Image[] _batteryBarFills = new Image[DryRunOrbitTicks];
        private readonly Image[] _ssdBarFills = new Image[DryRunOrbitTicks];

        private Coroutine _dryRunCoroutine;
        private bool _isRunningDryRun;

        public event Action<MissionReport> OnDryRunCompleted;
        public event Action OnRequestOpenAutopilot;

        public void Initialize(
            IAstraAuditor auditor,
            IMissionSimulator simulator,
            IGameDataRepository repository,
            AutopilotUI autopilotUI = null)
        {
            _repository = repository;
            _auditor = auditor ?? new AstraAuditor(new ResourceCalculator(), _repository);
            _simulator = simulator ?? (GameManager.Instance != null ? GameManager.Instance.Simulator : new MockMissionSimulator());
            _autopilotUI = autopilotUI;

            EnsureDryRunUIHierarchy();
        }

        public void CloseModal()
        {
            if (_dryRunCoroutine != null)
            {
                StopCoroutine(_dryRunCoroutine);
                _dryRunCoroutine = null;
                _isRunningDryRun = false;
                if (_runDryRunButton != null) _runDryRunButton.interactable = true;
                if (_runDryRunButtonText != null) _runDryRunButtonText.text = "▶ RUN 3s DRY RUN (20 TICKS)";
            }
            gameObject.SetActive(false);
        }

        [ContextMenu("Generate A.S.T.R.A. & DryRun UI In Scene")]
        public void BuildUIInEditor()
        {
            if (_auditor == null) _auditor = new AstraAuditor();
            if (_simulator == null) _simulator = new MockMissionSimulator();

            Transform existing = transform.Find("DryRunContentRoot");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            EnsureDryRunUIHierarchy();

            var defaultComps = BuilderPanelUI.BuildDefaultComponentsFromSpec();
            var starterFive = new List<SatelliteComponent>
            {
                defaultComps[0], defaultComps[3], defaultComps[7], defaultComps[10], defaultComps[14]
            };
            var rocket = BuilderPanelUI.BuildDefaultRocketsFromSpec()[0];
            var orbit = BuilderPanelUI.BuildDefaultOrbitsFromSpec()[0];
            var goal = Hub.ContractHubUI.BuildDefaultMissionsFromSpec()[0];

            UpdateConfigurationAndAudit(starterFive, rocket, orbit, goal, new List<AutopilotRule>());
            Debug.Log("[DryRunUI] A.S.T.R.A. Advisory Panel & 3s Dry Run Modal Window generated!");
        }

        public void UpdateConfigurationAndAudit(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules)
        {
            EnsureDryRunUIHierarchy();

            _currentComponents = components ?? new List<SatelliteComponent>();
            _currentRocket = rocket;
            _currentOrbit = orbit;
            _currentGoal = goal;
            _currentRules = rules ?? (_autopilotUI != null ? _autopilotUI.GetActiveRules() : new List<AutopilotRule>());

            RunAstraAuditNow();

            if (!_isRunningDryRun)
            {
                PreviewStaticCurve(_currentComponents, _currentRocket, _currentOrbit, _currentGoal, _currentRules);
            }
        }

        public List<AstraAuditMessage> RunAstraAuditNow()
        {
            EnsureDryRunUIHierarchy();

            if (_auditor == null)
            {
                _auditor = new AstraAuditor(new ResourceCalculator(), _repository);
            }

            if (_autopilotUI != null)
            {
                _currentRules = _autopilotUI.GetActiveRules();
            }

            List<AstraAuditMessage> messages = _auditor.AuditConfiguration(
                _currentComponents,
                _currentRocket,
                _currentOrbit,
                _currentGoal,
                _currentRules);

            RenderAuditMessages(messages);
            return messages;
        }

        public void StartDryRunBenchTest()
        {
            EnsureDryRunUIHierarchy();

            if (_autopilotUI != null)
            {
                _currentRules = _autopilotUI.GetActiveRules();
            }

            if (_simulator == null)
            {
                _simulator = GameManager.Instance != null ? GameManager.Instance.Simulator : new MockMissionSimulator();
            }

            MissionReport simReport = _simulator != null
                ? _simulator.RunDryRun(_currentComponents, _currentRocket, _currentOrbit, _currentGoal, _currentRules)
                : null;

            if (_simulator == null || _simulator is MockMissionSimulator)
            {
                simReport = SimulateDeterministicDryRunOrbit(
                    _currentComponents,
                    _currentRocket,
                    _currentOrbit,
                    _currentGoal,
                    _currentRules);
            }
            else if (simReport == null)
            {
                simReport = new MissionReport
                {
                    Status = MissionStatus.Failed,
                    FailureTitle = "Simulation Unavailable",
                    FailureCause = "OrbitSimulator returned no report."
                };
            }

            if (_dryRunCoroutine != null)
            {
                StopCoroutine(_dryRunCoroutine);
                _dryRunCoroutine = null;
            }

            if (Application.isPlaying && gameObject.activeInHierarchy)
            {
                _dryRunCoroutine = StartCoroutine(AnimateDryRunRoutine(simReport));
            }
            else
            {
                RenderInstantDryRunResult(simReport);
            }
        }

        private IEnumerator AnimateDryRunRoutine(MissionReport report)
        {
            _isRunningDryRun = true;
            if (_runDryRunButton != null) _runDryRunButton.interactable = false;
            if (_runDryRunButtonText != null) _runDryRunButtonText.text = "RUNNING 3s BENCH TEST...";

            ResetChartBars();

            List<SimulationTickSnapshot> ticks = report != null && report.Ticks != null
                ? report.Ticks
                : new List<SimulationTickSnapshot>();

            int count = Mathf.Min(DryRunOrbitTicks, ticks.Count);
            float stepDelay = count > 0 ? (DryRunDurationSeconds / count) : 0.15f;

            for (int i = 0; i < count; i++)
            {
                SimulationTickSnapshot tick = ticks[i];
                ApplyTickToChartColumn(i, tick, true);

                if (_autopilotUI != null)
                {
                    _autopilotUI.HighlightTriggeredRules(tick.TriggeredRuleIds);
                }

                if (_dryRunStatusText != null)
                {
                    string phase = tick.IsInEclipse ? "<color=#88AAFF>ECLIPSE (PLANET SHADOW)</color>" : "<color=#FFE066>SUNLIGHT</color>";
                    string commTag = tick.IsInCommWindow ? "<color=#55E6C1>COMM WINDOW (DOWNLINK)</color>" : "NO LINK (BUFFERING TO SSD)";
                    _dryRunStatusText.text = string.Format(
                        "DRY RUN TICK <b>{0:00}/{1:00}</b>   |   {2}   |   {3}",
                        i + 1, DryRunOrbitTicks, phase, commTag);
                }

                if (_dryRunTelemetryText != null)
                {
                    _dryRunTelemetryText.text = string.Format(
                        "Battery: <b>{0:0}%</b> ({1:0.0} Wh)   |   SSD Used: <b>{2:0}%</b> ({3:0.0} Gb)   |   Solar/RTG: <b>+{4:0}W</b>   |   Draw: <b>-{5:0}W</b>",
                        tick.BatteryPercent, tick.BatteryWh, tick.SsdUsedPercent, tick.SsdUsedGb, tick.SolarGenWatts, tick.PowerDrawWatts);
                }

                yield return new WaitForSeconds(stepDelay);

                ApplyTickToChartColumn(i, tick, false);
            }

            if (_autopilotUI != null)
            {
                _autopilotUI.HighlightTriggeredRules(null);
            }

            RenderFinalDryRunSummary(report);

            if (_runDryRunButton != null) _runDryRunButton.interactable = true;
            if (_runDryRunButtonText != null) _runDryRunButtonText.text = "▶ RUN 3s DRY RUN (20 TICKS)";
            _isRunningDryRun = false;
            _dryRunCoroutine = null;

            OnDryRunCompleted?.Invoke(report);
        }

        private void RenderInstantDryRunResult(MissionReport report)
        {
            ResetChartBars();
            if (report != null && report.Ticks != null)
            {
                int count = Mathf.Min(DryRunOrbitTicks, report.Ticks.Count);
                for (int i = 0; i < count; i++)
                {
                    ApplyTickToChartColumn(i, report.Ticks[i], false);
                }
            }
            RenderFinalDryRunSummary(report);
            OnDryRunCompleted?.Invoke(report);
        }

        private void PreviewStaticCurve(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules)
        {
            if (_simulator == null && GameManager.Instance != null)
            {
                _simulator = GameManager.Instance.Simulator;
            }

            MissionReport preview = (_simulator != null && !(_simulator is MockMissionSimulator))
                ? _simulator.RunDryRun(components, rocket, orbit, goal, rules)
                : SimulateDeterministicDryRunOrbit(components, rocket, orbit, goal, rules);

            ResetChartBars();
            if (preview != null && preview.Ticks != null && preview.Ticks.Count > 0)
            {
                int count = Mathf.Min(DryRunOrbitTicks, preview.Ticks.Count);
                for (int i = 0; i < count; i++)
                {
                    ApplyTickToChartColumn(i, preview.Ticks[i], false);
                }

                if (_dryRunStatusText != null)
                {
                    _dryRunStatusText.text = string.Format(
                        "BENCH READY — Min Battery: <b>{0:0}%</b>  |  Orbit 1 Sci: <b>{1:0.0} pts</b>  (Click Dry Run for 3s animated test)",
                        preview.MinBatteryPercent, preview.DeliveredScience);
                }
            }
            else if (_dryRunStatusText != null)
            {
                string title = preview != null && !string.IsNullOrEmpty(preview.FailureTitle) ? preview.FailureTitle : "Launch Validation Blocked";
                string cause = preview != null && !string.IsNullOrEmpty(preview.FailureCause) ? preview.FailureCause : "Check configuration in Workshop.";
                _dryRunStatusText.text = string.Format("<color=#FF5964><b>{0}:</b></color> {1}", title.ToUpperInvariant(), cause);
            }

            if (_dryRunTelemetryText != null)
            {
                _dryRunTelemetryText.text = "Green/Red Bars = Battery Charge % (0–100%)   |   Purple Bars = SSD Fill % (0–100%)   |   Dark Columns = Planet Shadow (Eclipse)";
            }
        }

        private void RenderFinalDryRunSummary(MissionReport report)
        {
            if (report == null) return;

            bool survivedOrbit = report.Status != MissionStatus.Failed &&
                                 report.Ticks != null &&
                                 report.Ticks.Count > 0 &&
                                 report.MinBatteryPercent > 0.01f &&
                                 report.FinalHealthPercent > 0.01f;
            if (_dryRunStatusText != null)
            {
                if (survivedOrbit)
                {
                    _dryRunStatusText.text = string.Format(
                        "<color=#4CE68A><b>DRY RUN PASSED (20/20 TICKS)</b></color>   |   Min Battery: <b>{0:0}%</b>   |   Health: <b>{1:0}%</b>",
                        report.MinBatteryPercent, report.FinalHealthPercent);
                }
                else
                {
                    string reason = !string.IsNullOrEmpty(report.FailureTitle) ? report.FailureTitle : "Battery Depleted (0%)";
                    _dryRunStatusText.text = string.Format(
                        "<color=#FF5964><b>DRY RUN FAILED: {0}</b></color>   |   {1}",
                        reason.ToUpperInvariant(), report.FailureCause);
                }
            }

            if (_dryRunTelemetryText != null)
            {
                _dryRunTelemetryText.text = string.Format(
                    "1-Orbit Science Delivered: <b>{0:0.0} pts</b>   |   Data Lost (SSD Overflow): <b>{1:0.0} Gb</b>",
                    report.DeliveredScience, report.LostDataGb);
            }
        }

        private void ResetChartBars()
        {
            for (int i = 0; i < DryRunOrbitTicks; i++)
            {
                if (_tickColumnBgs[i] != null)
                {
                    _tickColumnBgs[i].color = new Color(0.08f, 0.13f, 0.22f, 0.85f);
                }
                if (_batteryBarFills[i] != null)
                {
                    SetBarHeightFraction(_batteryBarFills[i].rectTransform, 0.02f);
                    _batteryBarFills[i].color = new Color(0.22f, 0.78f, 0.95f, 0.35f);
                }
                if (_ssdBarFills[i] != null)
                {
                    SetBarHeightFraction(_ssdBarFills[i].rectTransform, 0.02f);
                    _ssdBarFills[i].color = new Color(0.75f, 0.42f, 1.0f, 0.35f);
                }
            }
        }

        private void ApplyTickToChartColumn(int index, SimulationTickSnapshot tick, bool isCurrentStep)
        {
            if (index < 0 || index >= DryRunOrbitTicks || tick == null) return;

            if (_tickColumnBgs[index] != null)
            {
                if (isCurrentStep)
                {
                    _tickColumnBgs[index].color = new Color(0.28f, 0.44f, 0.68f, 0.98f);
                }
                else if (tick.IsInEclipse)
                {
                    _tickColumnBgs[index].color = new Color(0.03f, 0.05f, 0.11f, 0.95f);
                }
                else
                {
                    _tickColumnBgs[index].color = new Color(0.10f, 0.17f, 0.28f, 0.85f);
                }
            }

            if (_batteryBarFills[index] != null)
            {
                float batNorm = Mathf.Clamp01(tick.BatteryPercent / 100f);
                SetBarHeightFraction(_batteryBarFills[index].rectTransform, Mathf.Max(0.04f, batNorm));

                if (tick.BatteryPercent <= 0.1f)
                    _batteryBarFills[index].color = new Color(1.0f, 0.25f, 0.30f, 1f);
                else if (tick.BatteryPercent < 25f)
                    _batteryBarFills[index].color = new Color(1.0f, 0.72f, 0.18f, 1f);
                else
                    _batteryBarFills[index].color = new Color(0.22f, 0.88f, 0.62f, 0.95f);
            }

            if (_ssdBarFills[index] != null)
            {
                float ssdNorm = Mathf.Clamp01(tick.SsdUsedPercent / 100f);
                SetBarHeightFraction(_ssdBarFills[index].rectTransform, Mathf.Max(0.04f, ssdNorm));

                if (tick.SsdUsedPercent >= 95f)
                    _ssdBarFills[index].color = new Color(1.0f, 0.35f, 0.42f, 0.95f);
                else
                    _ssdBarFills[index].color = new Color(0.72f, 0.45f, 1.0f, 0.92f);
            }
        }

        private static void SetBarHeightFraction(RectTransform rt, float fraction01)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, Mathf.Clamp01(fraction01));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static MissionReport SimulateDeterministicDryRunOrbit(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules)
        {
            var report = new MissionReport
            {
                Status = MissionStatus.InProgress,
                Ticks = new List<SimulationTickSnapshot>(DryRunOrbitTicks)
            };

            if (components == null || orbit == null)
            {
                report.Status = MissionStatus.Failed;
                report.FailureTitle = "Incomplete Configuration";
                report.FailureCause = "Missing components or orbit.";
                return report;
            }

            var calc = new ResourceCalculator();
            ResourceSummary summary = calc.CalculateResources(components, rocket, orbit, goal);
            var evaluator = new AutopilotEvaluator();

            float baseDraw = 0f;
            float sciDraw = 0f;
            float commsDraw = 0f;
            float dataPerTick = 0f;
            float sciPerTick = 0f;

            for (int i = 0; i < components.Count; i++)
            {
                var c = components[i];
                if (c == null) continue;
                baseDraw += c.BasePowerDrawWatts;
                if (c.Category == ComponentCategory.SciencePayload)
                {
                    sciDraw += c.ActivePowerDrawWatts;
                    dataPerTick += c.DataGenGbPerTick;
                    sciPerTick += c.SciencePointsPerTick;
                }
                else if (c.Category == ComponentCategory.CommsAndStorage)
                {
                    commsDraw += c.ActivePowerDrawWatts;
                }
            }

            float maxBatteryWh = Mathf.Max(1f, summary.MaxBatteryWh);
            float maxSsdGb = Mathf.Max(0.5f, summary.MaxSsdGb);
            float batteryWh = maxBatteryWh;
            float ssdUsedGb = 0f;
            float pendingScience = 0f;
            float deliveredScience = 0f;
            float lostDataGb = 0f;
            float health = 100f;
            float minBatteryPercent = 100f;

            bool scienceActive = true;
            bool commsActive = true;
            bool safeModeActive = false;

            float tickHours = Mathf.Max(0.02f, orbit.OrbitPeriodHours / DryRunOrbitTicks);
            int sunTicks = Mathf.RoundToInt(DryRunOrbitTicks * (1f - Mathf.Clamp(orbit.EclipseFraction, 0.1f, 0.8f)));
            int commTicks = Mathf.Max(2, Mathf.RoundToInt(DryRunOrbitTicks * Mathf.Clamp(orbit.CommWindowFraction, 0.1f, 0.9f)));

            for (int t = 0; t < DryRunOrbitTicks; t++)
            {
                bool isInEclipse = t >= sunTicks;
                bool isInCommWindow = t >= (DryRunOrbitTicks - commTicks);

                float solarGen = isInEclipse ? 0f : (summary.MaxSolarWatts * orbit.SolarFluxFactor);
                float totalGenWatts = solarGen + summary.RtgWatts;
                float currentBatPercent = (batteryWh / maxBatteryWh) * 100f;
                float currentSsdPercent = (ssdUsedGb / maxSsdGb) * 100f;
                float radiation = orbit.BaseRadiation;

                var triggeredIds = new List<string>();
                evaluator.EvaluateRulesForTick(
                    rules,
                    solarGen,
                    currentBatPercent,
                    currentSsdPercent,
                    radiation,
                    ref scienceActive,
                    ref commsActive,
                    ref safeModeActive,
                    triggeredIds);

                bool effSci;
                bool effComms;
                AutopilotEvaluator.ResolveEffectiveSubsystems(scienceActive, commsActive, safeModeActive, out effSci, out effComms);

                float drawWatts = safeModeActive
                    ? baseDraw * 0.5f
                    : baseDraw + (effSci ? sciDraw : 0f) + (effComms && isInCommWindow ? commsDraw : 0f);

                batteryWh = Mathf.Clamp(batteryWh + (totalGenWatts - drawWatts) * tickHours, 0f, maxBatteryWh);
                float endBatPercent = (batteryWh / maxBatteryWh) * 100f;
                minBatteryPercent = Mathf.Min(minBatteryPercent, endBatPercent);

                if (batteryWh > 0.001f)
                {
                    if (effSci)
                    {
                        ssdUsedGb += dataPerTick;
                        pendingScience += sciPerTick;
                        if (ssdUsedGb > maxSsdGb)
                        {
                            lostDataGb += (ssdUsedGb - maxSsdGb);
                            pendingScience *= (maxSsdGb / ssdUsedGb);
                            ssdUsedGb = maxSsdGb;
                        }
                    }

                    if (effComms && isInCommWindow && ssdUsedGb > 0.0001f)
                    {
                        float sentGb = Mathf.Min(ssdUsedGb, summary.DownlinkGbPerTick);
                        float deliveredNow = pendingScience * (sentGb / ssdUsedGb);
                        deliveredScience += deliveredNow;
                        pendingScience -= deliveredNow;
                        ssdUsedGb -= sentGb;
                    }
                }

                float endSsdPercent = Mathf.Clamp((ssdUsedGb / maxSsdGb) * 100f, 0f, 100f);

                report.Ticks.Add(new SimulationTickSnapshot
                {
                    TickIndex = t,
                    OrbitNumber = 1,
                    OrbitProgress = (float)t / DryRunOrbitTicks,
                    IsInEclipse = isInEclipse,
                    IsInCommWindow = isInCommWindow,
                    SafeModeActive = safeModeActive,
                    ScienceActive = effSci,
                    CommsActive = effComms,
                    SolarGenWatts = totalGenWatts,
                    PowerDrawWatts = drawWatts,
                    BatteryWh = batteryWh,
                    BatteryPercent = endBatPercent,
                    SsdUsedGb = ssdUsedGb,
                    SsdUsedPercent = endSsdPercent,
                    RadiationLevel = radiation,
                    HealthPercent = health,
                    DeliveredScience = deliveredScience,
                    TriggeredRuleIds = triggeredIds
                });

                if (batteryWh <= 0.001f && report.Status != MissionStatus.Failed)
                {
                    report.Status = MissionStatus.Failed;
                    report.FailureTitle = "Battery Depleted in Eclipse";
                    report.FailureCause = string.Format("0% charge at Tick {0}/20. Add IF SolarWatts < 5 THEN ScienceOff.", t + 1);
                }
            }

            report.DeliveredScience = deliveredScience;
            report.LostDataGb = lostDataGb;
            report.MinBatteryPercent = minBatteryPercent;
            report.FinalHealthPercent = health;
            if (report.Status != MissionStatus.Failed)
            {
                report.Status = MissionStatus.Success;
            }

            return report;
        }

        private void RenderAuditMessages(List<AstraAuditMessage> messages)
        {
            if (_auditMessagesContainer == null) return;

            for (int i = _auditMessagesContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = _auditMessagesContainer.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    child.SetActive(false);
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            if (messages == null || messages.Count == 0) return;

            int criticalCount = 0;
            int warningCount = 0;
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Severity == AuditSeverity.Critical) criticalCount++;
                else if (messages[i].Severity == AuditSeverity.Warning) warningCount++;
            }

            if (_astraSummaryBadgeText != null)
            {
                if (criticalCount > 0)
                {
                    _astraSummaryBadgeText.text = string.Format("STATUS: {0} CRITICAL, {1} WARNING", criticalCount, warningCount);
                    _astraSummaryBadgeText.color = new Color(1.0f, 0.42f, 0.45f, 1f);
                }
                else if (warningCount > 0)
                {
                    _astraSummaryBadgeText.text = string.Format("STATUS: {0} WARNING(S)", warningCount);
                    _astraSummaryBadgeText.color = new Color(1.0f, 0.82f, 0.32f, 1f);
                }
                else
                {
                    _astraSummaryBadgeText.text = "STATUS: READY FOR LAUNCH";
                    _astraSummaryBadgeText.color = new Color(0.35f, 0.95f, 0.58f, 1f);
                }
            }

            Font font = AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.GetPixelFont();
            int maxCardsToShow = Mathf.Min(messages.Count, 4);

            for (int i = 0; i < maxCardsToShow; i++)
            {
                AstraAuditMessage msg = messages[i];
                CreateAuditCard(msg, _auditMessagesContainer, font, i);
            }
        }

        private void CreateAuditCard(AstraAuditMessage msg, Transform parent, Font font, int index)
        {
            Color bgCol;
            Color badgeCol;
            string prefix;

            switch (msg.Severity)
            {
                case AuditSeverity.Critical:
                    bgCol = new Color(0.28f, 0.08f, 0.11f, 0.96f);
                    badgeCol = new Color(1.0f, 0.36f, 0.40f, 1f);
                    prefix = "[CRITICAL] ";
                    break;
                case AuditSeverity.Warning:
                    bgCol = new Color(0.26f, 0.19f, 0.06f, 0.96f);
                    badgeCol = new Color(1.0f, 0.80f, 0.28f, 1f);
                    prefix = "[WARNING] ";
                    break;
                default:
                    bgCol = new Color(0.07f, 0.22f, 0.15f, 0.96f);
                    badgeCol = new Color(0.35f, 0.95f, 0.58f, 1f);
                    prefix = "[OPTIMAL] ";
                    break;
            }

            GameObject cardObj = CreateUIObject("AuditCard_" + index, parent);
            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.color = bgCol;

            VerticalLayoutGroup vLayout = cardObj.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(14, 14, 10, 10);
            vLayout.spacing = 4f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            CreateText("Title", cardObj.transform, font, prefix + msg.Title, 16, FontStyle.Bold, badgeCol, TextAnchor.MiddleLeft);
            CreateText("Advice", cardObj.transform, font, msg.Advice, 14, FontStyle.Normal, new Color(0.90f, 0.94f, 0.98f, 1f), TextAnchor.MiddleLeft);
            PixelUITheme.ApplyToHierarchy(cardObj.transform);
        }

        public void EnsureDryRunUIHierarchy()
        {
            Transform existing = transform.Find("DryRunContentRoot");
            if (existing != null)
            {
                BindExistingHierarchy(existing);
                BindUX6DryRunTooltips(existing);
                PixelUITheme.ApplyToHierarchy(existing);
                return;
            }

            Font font = AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.GetPixelFont();

            // Modal Dark Backdrop
            GameObject rootObj = CreateUIObject("DryRunContentRoot", transform);
            StretchFull(rootObj.GetComponent<RectTransform>());
            Image modalBg = rootObj.AddComponent<Image>();
            modalBg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(36, 36, 24, 24);
            rootLayout.spacing = 14f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // --- SECTION 1: TOP MODAL HEADER BAR ---
            GameObject astraHeader = CreateUIObject("AstraHeaderRow", rootObj.transform);
            Image headerBg = astraHeader.AddComponent<Image>();
            headerBg.color = new Color(0.07f, 0.13f, 0.24f, 0.98f);
            LayoutElement ahLe = astraHeader.AddComponent<LayoutElement>();
            ahLe.preferredHeight = 62f;

            HorizontalLayoutGroup ahLayout = astraHeader.AddComponent<HorizontalLayoutGroup>();
            ahLayout.padding = new RectOffset(18, 18, 8, 8);
            ahLayout.spacing = 12f;
            ahLayout.childAlignment = TextAnchor.MiddleLeft;
            ahLayout.childControlWidth = true;
            ahLayout.childControlHeight = true;
            ahLayout.childForceExpandWidth = false;

            Text titleTxt = CreateText("AstraTitle", astraHeader.transform, font,
                "A.S.T.R.A. PRE-FLIGHT AUDITOR & 3s ORBIT BENCH TEST", 18, FontStyle.Bold,
                new Color(1f, 0.88f, 0.42f, 1f), TextAnchor.MiddleLeft);
            LayoutElement ttLe = titleTxt.gameObject.AddComponent<LayoutElement>();
            ttLe.flexibleWidth = 1f;

            _astraSummaryBadgeText = CreateText("AstraSummaryBadge", astraHeader.transform, font,
                "STATUS: AUDITING...", 15, FontStyle.Bold,
                new Color(0.65f, 0.90f, 1f, 1f), TextAnchor.MiddleRight);
            LayoutElement sbLe = _astraSummaryBadgeText.gameObject.AddComponent<LayoutElement>();
            sbLe.preferredWidth = 260f;

            CreateButton(astraHeader.transform, "Btn_RefreshAudit", "Audit Now",
                new Color(0.18f, 0.34f, 0.54f, 1f), 115f, 42f, font, () => RunAstraAuditNow());

            CreateButton(astraHeader.transform, "Btn_OpenAutopilot", "⚙ EDIT AUTOPILOT RULES",
                new Color(0.22f, 0.40f, 0.64f, 1f), 220f, 42f, font, () => OnRequestOpenAutopilot?.Invoke());

            CreateButton(astraHeader.transform, "Btn_CloseModal", "✕ CLOSE",
                new Color(0.55f, 0.16f, 0.20f, 1f), 120f, 42f, font, CloseModal);

            // --- SECTION 2: A.S.T.R.A. ADVISORY MESSAGES ---
            GameObject messagesBox = CreateUIObject("AuditMessagesContainer", rootObj.transform);
            _auditMessagesContainer = messagesBox.transform;
            VerticalLayoutGroup msgLayout = messagesBox.AddComponent<VerticalLayoutGroup>();
            msgLayout.spacing = 8f;
            msgLayout.childControlWidth = true;
            msgLayout.childControlHeight = true;
            msgLayout.childForceExpandWidth = true;
            msgLayout.childForceExpandHeight = false;

            // --- SECTION 3: 3-SECOND (20-TICK) DRY RUN BENCH VISUALIZER ---
            GameObject dryRunBox = CreateUIObject("DryRunBenchBox", rootObj.transform);
            Image drBg = dryRunBox.AddComponent<Image>();
            drBg.color = new Color(0.05f, 0.08f, 0.14f, 0.96f);
            LayoutElement drLe = dryRunBox.AddComponent<LayoutElement>();
            drLe.preferredHeight = 460f;
            drLe.flexibleHeight = 1f;

            VerticalLayoutGroup drLayout = dryRunBox.AddComponent<VerticalLayoutGroup>();
            drLayout.padding = new RectOffset(20, 20, 14, 14);
            drLayout.spacing = 10f;
            drLayout.childControlWidth = true;
            drLayout.childControlHeight = true;
            drLayout.childForceExpandWidth = true;
            drLayout.childForceExpandHeight = false;

            // Top Controls Row inside DryRunBox
            GameObject drTopRow = CreateUIObject("DryRunControlsRow", dryRunBox.transform);
            LayoutElement drTopLe = drTopRow.AddComponent<LayoutElement>();
            drTopLe.preferredHeight = 46f;

            HorizontalLayoutGroup drTopLayout = drTopRow.AddComponent<HorizontalLayoutGroup>();
            drTopLayout.spacing = 16f;
            drTopLayout.childAlignment = TextAnchor.MiddleLeft;
            drTopLayout.childControlWidth = true;
            drTopLayout.childControlHeight = true;
            drTopLayout.childForceExpandWidth = false;

            _runDryRunButton = CreateButton(drTopRow.transform, "Btn_RunDryRun", "▶ RUN 3s DRY RUN (20 TICKS)",
                new Color(0.14f, 0.52f, 0.34f, 1f), 270f, 44f, font, StartDryRunBenchTest);
            _runDryRunButtonText = _runDryRunButton.GetComponentInChildren<Text>();

            _dryRunStatusText = CreateText("DryRunStatusText", drTopRow.transform, font,
                "BENCH READY — 1 Test Orbit (20 Ticks / 3.0s)", 16, FontStyle.Normal,
                new Color(0.90f, 0.95f, 1f, 1f), TextAnchor.MiddleLeft);
            _dryRunStatusText.supportRichText = true;
            LayoutElement stLe = _dryRunStatusText.gameObject.AddComponent<LayoutElement>();
            stLe.flexibleWidth = 1f;

            // 20-Tick Dual-Bar Curve Area
            GameObject chartRow = CreateUIObject("CurveChartRow", dryRunBox.transform);
            Image chartBg = chartRow.AddComponent<Image>();
            chartBg.color = new Color(0.03f, 0.05f, 0.09f, 0.95f);
            LayoutElement chartLe = chartRow.AddComponent<LayoutElement>();
            chartLe.preferredHeight = 320f;
            chartLe.flexibleHeight = 1f;

            HorizontalLayoutGroup chartLayout = chartRow.AddComponent<HorizontalLayoutGroup>();
            chartLayout.padding = new RectOffset(12, 12, 12, 12);
            chartLayout.spacing = 8f;
            chartLayout.childControlWidth = true;
            chartLayout.childControlHeight = true;
            chartLayout.childForceExpandWidth = true;
            chartLayout.childForceExpandHeight = true;

            for (int i = 0; i < DryRunOrbitTicks; i++)
            {
                GameObject colObj = CreateUIObject("TickCol_" + i, chartRow.transform);
                Image colBg = colObj.AddComponent<Image>();
                colBg.color = new Color(0.08f, 0.13f, 0.22f, 0.85f);
                _tickColumnBgs[i] = colBg;

                LayoutElement colLe = colObj.AddComponent<LayoutElement>();
                colLe.flexibleWidth = 1f;
                colLe.flexibleHeight = 1f;

                // Left Half of column: Battery % Bar
                GameObject batBar = CreateUIObject("BatBar", colObj.transform);
                RectTransform batRt = batBar.GetComponent<RectTransform>();
                batRt.anchorMin = new Vector2(0.08f, 0f);
                batRt.anchorMax = new Vector2(0.48f, 0.5f);
                batRt.offsetMin = Vector2.zero;
                batRt.offsetMax = Vector2.zero;
                Image batImg = batBar.AddComponent<Image>();
                batImg.color = new Color(0.22f, 0.88f, 0.62f, 0.95f);
                _batteryBarFills[i] = batImg;

                // Right Half of column: SSD % Bar
                GameObject ssdBar = CreateUIObject("SsdBar", colObj.transform);
                RectTransform ssdRt = ssdBar.GetComponent<RectTransform>();
                ssdRt.anchorMin = new Vector2(0.52f, 0f);
                ssdRt.anchorMax = new Vector2(0.92f, 0.2f);
                ssdRt.offsetMin = Vector2.zero;
                ssdRt.offsetMax = Vector2.zero;
                Image ssdImg = ssdBar.AddComponent<Image>();
                ssdImg.color = new Color(0.72f, 0.45f, 1.0f, 0.92f);
                _ssdBarFills[i] = ssdImg;
            }

            _dryRunTelemetryText = CreateText("DryRunTelemetryText", dryRunBox.transform, font,
                "Green/Red Bars = Battery Charge % (0–100%)   |   Purple Bars = SSD Fill % (0–100%)   |   Dark Background = Eclipse Shadow",
                14, FontStyle.Normal, new Color(0.72f, 0.84f, 0.96f, 1f), TextAnchor.MiddleLeft);
            _dryRunTelemetryText.supportRichText = true;
            LayoutElement telLe = _dryRunTelemetryText.gameObject.AddComponent<LayoutElement>();
            telLe.preferredHeight = 24f;

            BindUX6DryRunTooltips(rootObj.transform);
            PixelUITheme.ApplyToHierarchy(rootObj.transform);
        }

        private static void BindUX6DryRunTooltips(Transform root)
        {
            if (root == null) return;
            BindDryRunChild(root, "AstraHeaderRow/Btn_RefreshAudit", "dryrun_audit");
            BindDryRunChild(root, "AstraHeaderRow/AstraSummaryBadge", "dryrun_audit");
            BindDryRunChild(root, "DryRunBenchBox/DryRunControlsRow/Btn_RunDryRun", "dryrun_test");
            BindDryRunChild(root, "DryRunBenchBox/CurveChartRow", "dryrun_chart");
            BindDryRunChild(root, "DryRunBenchBox/DryRunTelemetryText", "dryrun_chart");
        }

        private static void BindDryRunChild(Transform root, string path, string key)
        {
            Transform child = root.Find(path);
            if (child != null) TooltipTrigger.Attach(child.gameObject, key);
        }

        private void BindExistingHierarchy(Transform root)
        {
            _astraSummaryBadgeText = FindChild<Text>(root, "AstraHeaderRow/AstraSummaryBadge");
            _auditMessagesContainer = root.Find("AuditMessagesContainer");

            Button auditBtn = FindChild<Button>(root, "AstraHeaderRow/Btn_RefreshAudit");
            if (auditBtn != null)
            {
                auditBtn.onClick.RemoveAllListeners();
                auditBtn.onClick.AddListener(() => RunAstraAuditNow());
            }

            Button apBtn = FindChild<Button>(root, "AstraHeaderRow/Btn_OpenAutopilot");
            if (apBtn != null)
            {
                apBtn.onClick.RemoveAllListeners();
                apBtn.onClick.AddListener(() => OnRequestOpenAutopilot?.Invoke());
            }

            Button closeBtn = FindChild<Button>(root, "AstraHeaderRow/Btn_CloseModal");
            if (closeBtn != null)
            {
                closeBtn.onClick.RemoveAllListeners();
                closeBtn.onClick.AddListener(CloseModal);
            }

            _runDryRunButton = FindChild<Button>(root, "DryRunBenchBox/DryRunControlsRow/Btn_RunDryRun");
            if (_runDryRunButton != null)
            {
                _runDryRunButton.onClick.RemoveAllListeners();
                _runDryRunButton.onClick.AddListener(StartDryRunBenchTest);
                _runDryRunButtonText = _runDryRunButton.GetComponentInChildren<Text>();
            }

            _dryRunStatusText = FindChild<Text>(root, "DryRunBenchBox/DryRunControlsRow/DryRunStatusText");
            _dryRunTelemetryText = FindChild<Text>(root, "DryRunBenchBox/DryRunTelemetryText");

            Transform chartRow = root.Find("DryRunBenchBox/CurveChartRow");
            if (chartRow != null)
            {
                for (int i = 0; i < DryRunOrbitTicks; i++)
                {
                    Transform col = chartRow.Find("TickCol_" + i);
                    if (col == null) continue;
                    _tickColumnBgs[i] = col.GetComponent<Image>();
                    _batteryBarFills[i] = FindChild<Image>(col, "BatBar");
                    _ssdBarFills[i] = FindChild<Image>(col, "SsdBar");
                }
            }
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
            if (width > 0f) le.preferredWidth = width;
            if (height > 0f) le.preferredHeight = height;

            Text txt = CreateText("Text", btnObj.transform, font, label, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }
    }
}
