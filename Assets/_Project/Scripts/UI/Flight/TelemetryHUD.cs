using System;
using System.Collections.Generic;
using System.Globalization;
using AstraLink.Core;
using AstraLink.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// Live Flight Telemetry HUD and Autopilot Rule Highlighting Controller (Task S2-04).
    /// Updates <see cref="Slider"/> and <see cref="Image.fillAmount"/> gauges alongside numeric counters on each simulation tick:
    /// 1) Battery Charge (<see cref="SimulationTickSnapshot.BatteryPercent"/> / <see cref="SimulationTickSnapshot.BatteryWh"/>),
    /// 2) SSD Buffer (<see cref="SimulationTickSnapshot.SsdUsedPercent"/> / <see cref="SimulationTickSnapshot.SsdUsedGb"/>),
    /// 3) Electronics Health (<see cref="SimulationTickSnapshot.HealthPercent"/>),
    /// 4) Delivered Science (<see cref="SimulationTickSnapshot.DeliveredScience"/> / <see cref="MissionGoal.TargetSciencePoints"/>),
    /// 5) Radiation Level (<see cref="SimulationTickSnapshot.RadiationLevel"/>),
    /// 6) Phase Indicators (<see cref="SimulationTickSnapshot.IsInEclipse"/>, <see cref="SimulationTickSnapshot.IsInCommWindow"/>, <see cref="SimulationTickSnapshot.SafeModeActive"/>),
    /// 7) Green highlighting of active autopilot rules (<see cref="SimulationTickSnapshot.TriggeredRuleIds"/>).
    /// </summary>
    public class TelemetryHUD : MonoBehaviour
    {
        [Header("Connected Flight UI Components")]
        [SerializeField] private OrbitRadarUI linkedRadarUI;
        [SerializeField] private EventPopupUI eventPopupUI;

        [Header("1. Battery Charge Telemetry (BatteryPercent / BatteryWh)")]
        [SerializeField] private Slider batterySlider;
        [SerializeField] private Image batteryFillImage;
        [SerializeField] private Text batteryValueText;
        [SerializeField] private Text batteryDetailText;

        [Header("2. SSD Storage Telemetry (SsdUsedPercent / SsdUsedGb)")]
        [SerializeField] private Slider ssdSlider;
        [SerializeField] private Image ssdFillImage;
        [SerializeField] private Text ssdValueText;
        [SerializeField] private Text ssdDetailText;

        [Header("3. Electronics Health Telemetry (HealthPercent)")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Text healthValueText;
        [SerializeField] private Text healthDetailText;

        [Header("4. Delivered Science Telemetry (DeliveredScience / TargetSciencePoints)")]
        [SerializeField] private Slider scienceSlider;
        [SerializeField] private Image scienceFillImage;
        [SerializeField] private Text scienceValueText;
        [SerializeField] private Text scienceDetailText;

        [Header("5. Radiation Level Telemetry (RadiationLevel)")]
        [SerializeField] private Slider radiationSlider;
        [SerializeField] private Image radiationFillImage;
        [SerializeField] private Text radiationValueText;
        [SerializeField] private Text radiationDetailText;

        [Header("6. Orbital Phase Indicators (IsInEclipse, IsInCommWindow, SafeModeActive)")]
        [SerializeField] private Image eclipseIndicatorBg;
        [SerializeField] private Text eclipseIndicatorText;
        [SerializeField] private Image commWindowIndicatorBg;
        [SerializeField] private Text commWindowIndicatorText;
        [SerializeField] private Image safeModeIndicatorBg;
        [SerializeField] private Text safeModeIndicatorText;
        [SerializeField] private Text subsystemStatusText;

        [Header("7. Autopilot Rules Green Highlighting Container")]
        [SerializeField] private Transform autopilotRulesContainer;

        private readonly List<Image> _ruleRowBackgrounds = new List<Image>();
        private readonly List<Image> _ruleBadgeBackgrounds = new List<Image>();
        private readonly List<Text> _ruleBadgeTexts = new List<Text>();
        private readonly List<Text> _ruleFormulaTexts = new List<Text>();

        private Image _batteryLedBar;
        private Image _ssdLedBar;
        private Image _healthLedBar;
        private Image _scienceLedBar;
        private Image _radiationLedBar;

        private List<AutopilotRule> _activeRules = new List<AutopilotRule>();
        private bool _hasExplicitRules;
        private ResourceSummary _activeResources;
        private MissionGoal _activeGoal;
        private OrbitDefinition _activeOrbit;

        private void Awake()
        {
            EnsureTelemetryHierarchy();
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
        /// Initializes the live telemetry HUD with mission parameters, autopilot rules, and optional radar/event popup links.
        /// </summary>
        public void Initialize(
            MissionGoal goal,
            ResourceSummary resources,
            OrbitDefinition orbit,
            List<AutopilotRule> rules,
            OrbitRadarUI radar = null,
            EventPopupUI popup = null)
        {
            EnsureTelemetryHierarchy();

            _activeGoal = goal;
            _activeResources = resources;
            _activeOrbit = orbit;

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

            if (popup != null)
            {
                eventPopupUI = popup;
            }

            SetAutopilotRules(rules);
        }

        /// <summary>
        /// Sets the list of autopilot rules to display and highlight on each simulation tick.
        /// </summary>
        public void SetAutopilotRules(List<AutopilotRule> rules)
        {
            if (rules != null)
            {
                _hasExplicitRules = true;
                _activeRules = new List<AutopilotRule>(rules);
            }
            else if (GameManager.Instance != null && !(GameManager.Instance.Simulator is MockMissionSimulator))
            {
                _hasExplicitRules = true;
                _activeRules = new List<AutopilotRule>();
            }
            else
            {
                _hasExplicitRules = false;
                _activeRules = BuildDefaultSampleRules();
            }

            EnsureTelemetryHierarchy();
            RebuildAutopilotRuleRows();
        }

        /// <summary>
        /// Updates all telemetry gauges (<see cref="Slider"/> + <see cref="Image.fillAmount"/>), phase indicators,
        /// event popup, and green autopilot rule highlights from the given <see cref="SimulationTickSnapshot"/>.
        /// </summary>
        public void UpdateFromTick(SimulationTickSnapshot snapshot, int tickIndex = 0, int totalTicks = 20)
        {
            if (snapshot == null) return;

            EnsureTelemetryHierarchy();

            float maxBatteryWh = (_activeResources != null && _activeResources.MaxBatteryWh > 0.01f)
                ? _activeResources.MaxBatteryWh
                : Mathf.Max(45.0f, snapshot.BatteryWh);
            float maxSsdGb = (_activeResources != null && _activeResources.MaxSsdGb > 0.01f)
                ? _activeResources.MaxSsdGb
                : Mathf.Max(16.0f, snapshot.SsdUsedGb);
            float targetScience = (_activeGoal != null && _activeGoal.TargetSciencePoints > 0.01f)
                ? _activeGoal.TargetSciencePoints
                : 100.0f;

            // 1. Battery Charge (BatteryPercent / BatteryWh)
            float batPct = Mathf.Clamp(snapshot.BatteryPercent, 0f, 100f);
            float batNormalized = batPct / 100.0f;
            Color batColor = batPct <= 15.0f
                ? new Color(0.95f, 0.28f, 0.28f, 1f)
                : (batPct <= 35.0f ? new Color(0.96f, 0.72f, 0.20f, 1f) : new Color(0.22f, 0.86f, 0.48f, 1f));

            UpdateGauge(batterySlider, batteryFillImage, _batteryLedBar, batPct <= 35.0f ? LedSheetKind.Brown : LedSheetKind.Cyan, batNormalized, batColor);
            if (batteryValueText != null)
            {
                batteryValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0}%  ({1:0.1} / {2:0.1} Wh)",
                    batPct,
                    snapshot.BatteryWh,
                    maxBatteryWh);
                batteryValueText.color = batColor;
            }
            if (batteryDetailText != null)
            {
                float netWatts = snapshot.SolarGenWatts - snapshot.PowerDrawWatts;
                batteryDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Solar Gen: +{0:0.1} W   •   Load Draw: -{1:0.1} W   •   Net: {2:+0.1;-0.1;0.0} W",
                    snapshot.SolarGenWatts,
                    snapshot.PowerDrawWatts,
                    netWatts);
            }

            // 2. SSD Storage (SsdUsedPercent / SsdUsedGb)
            float ssdPct = Mathf.Clamp(snapshot.SsdUsedPercent, 0f, 100f);
            float ssdNormalized = ssdPct / 100.0f;
            Color ssdColor = ssdPct >= 90.0f
                ? new Color(0.95f, 0.30f, 0.28f, 1f)
                : (ssdPct >= 75.0f ? new Color(0.96f, 0.74f, 0.22f, 1f) : new Color(0.24f, 0.76f, 0.98f, 1f));

            UpdateGauge(ssdSlider, ssdFillImage, _ssdLedBar, ssdPct >= 75.0f ? LedSheetKind.Brown : LedSheetKind.Blue, ssdNormalized, ssdColor);
            if (ssdValueText != null)
            {
                ssdValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0}%  ({1:0.2} / {2:0.1} Gb)",
                    ssdPct,
                    snapshot.SsdUsedGb,
                    maxSsdGb);
                ssdValueText.color = ssdColor;
            }
            if (ssdDetailText != null)
            {
                ssdDetailText.text = snapshot.IsInCommWindow && snapshot.CommsActive
                    ? "DOWNLINK ACTIVE: Transmitting buffered data to NASA Ground Station"
                    : (snapshot.ScienceActive ? "RECORDING: Science payload writing telemetry to SSD" : "STANDBY: SSD buffer holding stored telemetry");
            }

            // 3. Electronics Health (HealthPercent)
            float healthPct = Mathf.Clamp(snapshot.HealthPercent, 0f, 100f);
            float healthNormalized = healthPct / 100.0f;
            Color healthColor = healthPct < 50.0f
                ? new Color(0.95f, 0.26f, 0.26f, 1f)
                : (healthPct < 80.0f ? new Color(0.96f, 0.72f, 0.20f, 1f) : new Color(0.24f, 0.88f, 0.56f, 1f));

            UpdateGauge(healthSlider, healthFillImage, _healthLedBar, healthPct < 80.0f ? LedSheetKind.Pink : LedSheetKind.Cyan, healthNormalized, healthColor);
            if (healthValueText != null)
            {
                healthValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", healthPct);
                healthValueText.color = healthColor;
            }
            if (healthDetailText != null)
            {
                float shielding = _activeResources != null ? _activeResources.TotalShielding : 15.0f;
                healthDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Shielding Rating: {0:0.0} rads   •   Bonus Threshold: >= 80.0%",
                    shielding);
            }

            // 4. Delivered Science (DeliveredScience / TargetSciencePoints)
            float sciRatio = Mathf.Clamp01(snapshot.DeliveredScience / Mathf.Max(1.0f, targetScience));
            bool sciMet = snapshot.DeliveredScience + 0.01f >= targetScience;
            Color sciColor = sciMet
                ? new Color(0.26f, 0.92f, 0.54f, 1f)
                : new Color(0.38f, 0.82f, 1.0f, 1f);

            UpdateGauge(scienceSlider, scienceFillImage, _scienceLedBar, sciMet ? LedSheetKind.Cyan : LedSheetKind.Blue, sciRatio, sciColor);
            if (scienceValueText != null)
            {
                scienceValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.1} / {1:0.0} pts  ({2:0}%)",
                    snapshot.DeliveredScience,
                    targetScience,
                    sciRatio * 100.0f);
                scienceValueText.color = sciColor;
            }
            if (scienceDetailText != null)
            {
                scienceDetailText.text = sciMet
                    ? "CONTRACT TARGET ACHIEVED: Required science quota downlinked!"
                    : string.Format(CultureInfo.InvariantCulture, "Remaining Science Quota: {0:0.1} pts", Mathf.Max(0f, targetScience - snapshot.DeliveredScience));
            }

            // 5. Radiation Level (RadiationLevel)
            float radLevel = Mathf.Max(0f, snapshot.RadiationLevel);
            float radNormalized = Mathf.Clamp01(radLevel / 120.0f);
            bool radHigh = radLevel > 50.0f;
            Color radColor = radHigh
                ? new Color(0.96f, 0.24f, 0.26f, 1f)
                : (radLevel > 25.0f ? new Color(0.96f, 0.72f, 0.20f, 1f) : new Color(0.32f, 0.86f, 0.76f, 1f));

            UpdateGauge(radiationSlider, radiationFillImage, _radiationLedBar, radHigh ? LedSheetKind.Pink : LedSheetKind.Purple, radNormalized, radColor);
            if (radiationValueText != null)
            {
                radiationValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0} rads  {1}",
                    radLevel,
                    radHigh ? "(STORM DANGER > 50)" : "(NOMINAL)");
                radiationValueText.color = radColor;
            }
            if (radiationDetailText != null)
            {
                radiationDetailText.text = radHigh
                    ? (snapshot.SafeModeActive
                        ? "HIGH RADIATION: Safe Mode is protecting onboard electronics"
                        : "CRITICAL RADIATION (> 50): Enable Safe Mode to prevent avionics damage!")
                    : "Radiation within safe operating envelope (<= 50.0 rads)";
            }

            // 6. Current Phase Indicators (IsInEclipse, IsInCommWindow, SafeModeActive)
            UpdatePhaseIndicators(snapshot);

            // 7. Highlight Triggered Autopilot Rules in Green
            HighlightTriggeredAutopilotRules(snapshot.TriggeredRuleIds);

            // 8. Update EventPopupUI if connected
            if (eventPopupUI != null)
            {
                eventPopupUI.UpdateFromTick(snapshot);
            }
        }

        private void UpdatePhaseIndicators(SimulationTickSnapshot snapshot)
        {
            if (eclipseIndicatorBg != null)
            {
                eclipseIndicatorBg.color = snapshot.IsInEclipse
                    ? new Color(0.72f, 0.58f, 1.0f, 1f)
                    : new Color(1.0f, 0.88f, 0.50f, 1f);
            }
            if (eclipseIndicatorText != null)
            {
                eclipseIndicatorText.text = snapshot.IsInEclipse
                    ? "ECLIPSE: IN SHADOW"
                    : "SOLAR: IN SUNLIGHT";
                eclipseIndicatorText.color = Color.white;
            }

            if (commWindowIndicatorBg != null)
            {
                commWindowIndicatorBg.color = snapshot.IsInCommWindow
                    ? UITheme.ButtonTintSuccess
                    : UITheme.ButtonTintMuted;
            }
            if (commWindowIndicatorText != null)
            {
                commWindowIndicatorText.text = snapshot.IsInCommWindow
                    ? "COMM WINDOW: ACTIVE"
                    : "COMM WINDOW: CLOSED";
                commWindowIndicatorText.color = Color.white;
            }

            if (safeModeIndicatorBg != null)
            {
                safeModeIndicatorBg.color = snapshot.SafeModeActive
                    ? new Color(1.0f, 0.62f, 0.50f, 1f)
                    : new Color(0.64f, 0.98f, 0.76f, 1f);
            }
            if (safeModeIndicatorText != null)
            {
                safeModeIndicatorText.text = snapshot.SafeModeActive
                    ? "SAFE MODE: ENGAGED"
                    : "SAFE MODE: OFF (NOMINAL)";
                safeModeIndicatorText.color = Color.white;
            }

            if (subsystemStatusText != null)
            {
                subsystemStatusText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Subsystems:   Instruments (Science): <b><color={0}>{1}</color></b>   •   Transmitter (Comms): <b><color={2}>{3}</color></b>",
                    snapshot.ScienceActive ? "#58F287" : "#FF7A7A",
                    snapshot.ScienceActive ? "ON" : "OFF",
                    snapshot.CommsActive ? "#58F287" : "#FF7A7A",
                    snapshot.CommsActive ? "ON" : "OFF");
            }
        }

        /// <summary>
        /// Compares <paramref name="triggeredRuleIds"/> with the active <see cref="AutopilotRule"/> list
        /// and highlights every triggered rule row in bright green.
        /// </summary>
        public void HighlightTriggeredAutopilotRules(List<string> triggeredRuleIds)
        {
            if (_activeRules == null || _activeRules.Count == 0)
            {
                return;
            }

            if (_ruleRowBackgrounds.Count != _activeRules.Count)
            {
                RebuildAutopilotRuleRows();
            }

            for (int i = 0; i < _activeRules.Count && i < _ruleRowBackgrounds.Count; i++)
            {
                AutopilotRule rule = _activeRules[i];
                bool isTriggered = IsRuleTriggered(rule, i, triggeredRuleIds);

                if (_ruleRowBackgrounds[i] != null)
                {
                    _ruleRowBackgrounds[i].color = isTriggered
                        ? UITheme.ButtonTintSuccess
                        : Color.white;
                }

                if (i < _ruleBadgeBackgrounds.Count && _ruleBadgeBackgrounds[i] != null)
                {
                    _ruleBadgeBackgrounds[i].color = isTriggered
                        ? UITheme.ButtonTintSuccess
                        : UITheme.ButtonTintMuted;
                }

                if (i < _ruleBadgeTexts.Count && _ruleBadgeTexts[i] != null)
                {
                    _ruleBadgeTexts[i].text = isTriggered ? "ACTIVE (THEN)" : "STANDBY (ELSE)";
                    _ruleBadgeTexts[i].color = Color.white;
                }

                if (i < _ruleFormulaTexts.Count && _ruleFormulaTexts[i] != null)
                {
                    _ruleFormulaTexts[i].color = isTriggered
                        ? new Color(0.78f, 1.0f, 0.84f, 1.0f)
                        : UITheme.TextPrimary;
                }
            }
        }

        /// <summary>
        /// Checks whether the specified <see cref="AutopilotRule"/> is present in <paramref name="triggeredRuleIds"/>.
        /// Matches both explicit <see cref="AutopilotRule.RuleId"/> and positional fallback <c>"rule_" + (index + 1)</c> used by <see cref="AutopilotEvaluator"/>.
        /// </summary>
        public static bool IsRuleTriggered(AutopilotRule rule, int ruleIndex, List<string> triggeredRuleIds)
        {
            if (rule == null || !rule.Enabled || triggeredRuleIds == null || triggeredRuleIds.Count == 0)
            {
                return false;
            }

            string fallbackId = "rule_" + (ruleIndex + 1).ToString(CultureInfo.InvariantCulture);

            for (int i = 0; i < triggeredRuleIds.Count; i++)
            {
                string id = triggeredRuleIds[i];
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!string.IsNullOrWhiteSpace(rule.RuleId) &&
                    string.Equals(id.Trim(), rule.RuleId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(id.Trim(), fallbackId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Formats an <see cref="AutopilotRule"/> into a readable IF-THEN-ELSE engineering string.
        /// </summary>
        public static string FormatAutopilotRule(AutopilotRule rule, int index)
        {
            if (rule == null) return string.Empty;
            string opStr = rule.Operator == ComparisonOp.LessThan ? "<" : ">";
            string unit = GetMetricUnit(rule.Metric);
            string elsePart = rule.ElseAction != AutopilotAction.None
                ? string.Format(CultureInfo.InvariantCulture, "  ELSE <b>{0}</b>", rule.ElseAction)
                : string.Empty;

            return string.Format(
                CultureInfo.InvariantCulture,
                "#{0}: IF <b>{1} {2} {3:0.#}{4}</b> THEN <b>{5}</b>{6}",
                index + 1,
                rule.Metric,
                opStr,
                rule.Threshold,
                unit,
                rule.ThenAction,
                elsePart);
        }

        private static string GetMetricUnit(SensorMetric metric)
        {
            switch (metric)
            {
                case SensorMetric.SolarWatts: return " W";
                case SensorMetric.BatteryPercent:
                case SensorMetric.SsdUsedPercent: return "%";
                case SensorMetric.RadiationLevel: return " rad";
                default: return string.Empty;
            }
        }

        private static void UpdateGauge(Slider slider, Image fillImage, Image ledBarImage, LedSheetKind ledKind, float normalizedValue, Color fillColor)
        {
            float clamped = Mathf.Clamp01(normalizedValue);
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = clamped;
            }
            if (fillImage != null)
            {
                fillImage.fillAmount = clamped;
                fillImage.color = new Color(fillColor.r, fillColor.g, fillColor.b, 0.18f);
            }
            if (ledBarImage != null)
            {
                Sprite ledSprite = UITheme.GetLedBarSprite(ledKind, clamped);
                if (ledSprite != null)
                {
                    ledBarImage.sprite = ledSprite;
                    ledBarImage.color = Color.white;
                }
            }
        }

        private void HandleRadarTickUpdated(SimulationTickSnapshot snapshot, int tickIndex)
        {
            int total = linkedRadarUI != null ? linkedRadarUI.TotalTicks : 20;
            UpdateFromTick(snapshot, tickIndex, total);
        }

        /// <summary>
        /// Builds default sample autopilot rules covering Eclipse power saving, SSD management, and Radiation Safe Mode.
        /// </summary>
        public static List<AutopilotRule> BuildDefaultSampleRules()
        {
            return new List<AutopilotRule>
            {
                new AutopilotRule
                {
                    RuleId = "rule_eclipse_power_save",
                    Enabled = true,
                    Metric = SensorMetric.SolarWatts,
                    Operator = ComparisonOp.LessThan,
                    Threshold = 5.0f,
                    ThenAction = AutopilotAction.ScienceOff,
                    ElseAction = AutopilotAction.ScienceOn
                },
                new AutopilotRule
                {
                    RuleId = "rule_ssd_buffer_guard",
                    Enabled = true,
                    Metric = SensorMetric.SsdUsedPercent,
                    Operator = ComparisonOp.GreaterThan,
                    Threshold = 85.0f,
                    ThenAction = AutopilotAction.ScienceOff,
                    ElseAction = AutopilotAction.None
                },
                new AutopilotRule
                {
                    RuleId = "rule_cme_safe_mode",
                    Enabled = true,
                    Metric = SensorMetric.RadiationLevel,
                    Operator = ComparisonOp.GreaterThan,
                    Threshold = 50.0f,
                    ThenAction = AutopilotAction.SafeModeOn,
                    ElseAction = AutopilotAction.SafeModeOff
                }
            };
        }

        /// <summary>
        /// Ensures the telemetry HUD UI hierarchy exists and is bound.
        /// </summary>
        public void EnsureTelemetryHierarchy()
        {
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
            {
                rootRt = gameObject.AddComponent<RectTransform>();
            }

            Transform existingRoot = rootRt.Find("TelemetryHUDRoot");
            if (existingRoot != null)
            {
                return;
            }

            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
            }
            if (bg.sprite == null)
            {
                UITheme.ApplyPanelFrame(bg, useDialogueBox: false);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UIThemeAssets assets = UITheme.Assets;

            GameObject contentRoot = CreateUIObject("TelemetryHUDRoot", rootRt);
            StretchFull(contentRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup rootLayout = contentRoot.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(14, 14, 12, 12);
            rootLayout.spacing = 8f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            Text header = CreateText(
                "TelemetryHeader",
                contentRoot.transform,
                font,
                "LIVE SPACECRAFT TELEMETRY & AUTOPILOT",
                16,
                FontStyle.Bold,
                UITheme.TextGold,
                TextAnchor.MiddleLeft);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            // Phase Indicators Row (IsInEclipse, IsInCommWindow, SafeModeActive)
            GameObject phasesRow = CreateUIObject("PhaseIndicatorsRow", contentRoot.transform);
            phasesRow.AddComponent<LayoutElement>().preferredHeight = 32f;
            HorizontalLayoutGroup phasesLayout = phasesRow.AddComponent<HorizontalLayoutGroup>();
            phasesLayout.spacing = 8f;
            phasesLayout.childControlWidth = true;
            phasesLayout.childControlHeight = true;
            phasesLayout.childForceExpandWidth = true;
            phasesLayout.childForceExpandHeight = true;

            CreatePhaseBadge(phasesRow.transform, "EclipseBadge", "SOLAR: IN SUNLIGHT", new Color(1.0f, 0.88f, 0.50f, 1f), font, out eclipseIndicatorBg, out eclipseIndicatorText);
            CreatePhaseBadge(phasesRow.transform, "CommWindowBadge", "COMM WINDOW: ACTIVE", UITheme.ButtonTintSuccess, font, out commWindowIndicatorBg, out commWindowIndicatorText);
            CreatePhaseBadge(phasesRow.transform, "SafeModeBadge", "SAFE MODE: OFF", new Color(0.64f, 0.98f, 0.76f, 1f), font, out safeModeIndicatorBg, out safeModeIndicatorText);

            subsystemStatusText = CreateText(
                "SubsystemStatusText",
                contentRoot.transform,
                font,
                "Subsystems:   Instruments (Science): <b><color=#58F287>ON</color></b>   •   Transmitter (Comms): <b><color=#58F287>ON</color></b>",
                13,
                FontStyle.Normal,
                UITheme.TextPrimary,
                TextAnchor.MiddleLeft);
            subsystemStatusText.supportRichText = true;
            subsystemStatusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            // 5 Live Telemetry Gauges (Slider + Image.fillAmount + Segmented Pixel-Art LEDs + Category Icons)
            BuildTelemetryGaugeCard(
                contentRoot.transform,
                "Gauge_Battery",
                "BATTERY CHARGE",
                "100.0% (45.0 / 45.0 Wh)",
                "Solar Gen: +45.0 W   •   Load Draw: -26.0 W",
                new Color(0.22f, 0.86f, 0.48f, 1f),
                assets != null ? assets.iconEnergyCell : null,
                LedSheetKind.Cyan,
                font,
                out batterySlider,
                out batteryFillImage,
                out _batteryLedBar,
                out batteryValueText,
                out batteryDetailText);

            BuildTelemetryGaugeCard(
                contentRoot.transform,
                "Gauge_SSD",
                "ONBOARD SSD BUFFER",
                "5.0% (0.80 / 16.0 Gb)",
                "RECORDING: Science payload writing telemetry to SSD",
                new Color(0.24f, 0.76f, 0.98f, 1f),
                assets != null ? assets.iconDataChip : null,
                LedSheetKind.Blue,
                font,
                out ssdSlider,
                out ssdFillImage,
                out _ssdLedBar,
                out ssdValueText,
                out ssdDetailText);

            BuildTelemetryGaugeCard(
                contentRoot.transform,
                "Gauge_Health",
                "ELECTRONICS HEALTH",
                "100.0%",
                "Shielding Rating: 15.0 rads   •   Bonus Threshold: >= 80.0%",
                new Color(0.24f, 0.88f, 0.56f, 1f),
                assets != null ? assets.iconShield : null,
                LedSheetKind.Cyan,
                font,
                out healthSlider,
                out healthFillImage,
                out _healthLedBar,
                out healthValueText,
                out healthDetailText);

            BuildTelemetryGaugeCard(
                contentRoot.transform,
                "Gauge_Science",
                "DELIVERED SCIENCE",
                "0.0 / 100.0 pts (0%)",
                "Remaining Science Quota: 100.0 pts",
                new Color(0.38f, 0.82f, 1.0f, 1f),
                assets != null ? assets.iconStarChart : null,
                LedSheetKind.Blue,
                font,
                out scienceSlider,
                out scienceFillImage,
                out _scienceLedBar,
                out scienceValueText,
                out scienceDetailText);

            BuildTelemetryGaugeCard(
                contentRoot.transform,
                "Gauge_Radiation",
                "RADIATION ENVIRONMENT",
                "5.0 rads (NOMINAL)",
                "Radiation within safe operating envelope (<= 50.0 rads)",
                new Color(0.32f, 0.86f, 0.76f, 1f),
                assets != null ? assets.celestialSupernova : null,
                LedSheetKind.Purple,
                font,
                out radiationSlider,
                out radiationFillImage,
                out _radiationLedBar,
                out radiationValueText,
                out radiationDetailText);

            // Autopilot Rules Section
            Text rulesHeader = CreateText(
                "AutopilotRulesHeader",
                contentRoot.transform,
                font,
                "ONBOARD AUTOPILOT RULES (Green = Triggered on Current Tick)",
                15,
                FontStyle.Bold,
                new Color(0.55f, 0.92f, 0.72f, 1.0f),
                TextAnchor.MiddleLeft);
            rulesHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            GameObject rulesBox = CreateUIObject("AutopilotRulesContainer", contentRoot.transform);
            LayoutElement rulesBoxLe = rulesBox.AddComponent<LayoutElement>();
            rulesBoxLe.flexibleHeight = 1f;
            rulesBoxLe.minHeight = 96f;
            VerticalLayoutGroup rulesLayout = rulesBox.AddComponent<VerticalLayoutGroup>();
            rulesLayout.spacing = 5f;
            rulesLayout.childControlWidth = true;
            rulesLayout.childControlHeight = true;
            rulesLayout.childForceExpandWidth = true;
            rulesLayout.childForceExpandHeight = false;

            autopilotRulesContainer = rulesBox.transform;

            if (!_hasExplicitRules && (_activeRules == null || _activeRules.Count == 0))
            {
                _activeRules = (GameManager.Instance != null && !(GameManager.Instance.Simulator is MockMissionSimulator))
                    ? new List<AutopilotRule>()
                    : BuildDefaultSampleRules();
            }
            RebuildAutopilotRuleRows();
        }

        private void RebuildAutopilotRuleRows()
        {
            if (autopilotRulesContainer == null) return;

            for (int i = autopilotRulesContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(autopilotRulesContainer.GetChild(i).gameObject);
            }

            _ruleRowBackgrounds.Clear();
            _ruleBadgeBackgrounds.Clear();
            _ruleBadgeTexts.Clear();
            _ruleFormulaTexts.Clear();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (_activeRules == null || _activeRules.Count == 0)
            {
                GameObject emptyObj = CreateUIObject("NoRulesRow", autopilotRulesContainer);
                Image emptyBg = emptyObj.AddComponent<Image>();
                emptyBg.color = new Color(0.10f, 0.14f, 0.22f, 0.90f);
                emptyObj.AddComponent<LayoutElement>().preferredHeight = 32f;

                Text emptyTxt = CreateText(
                    "EmptyRulesText",
                    emptyObj.transform,
                    font,
                    "No active autopilot rules programmed (Manual / Unregulated power mode).",
                    13,
                    FontStyle.Italic,
                    new Color(0.75f, 0.82f, 0.92f, 1.0f),
                    TextAnchor.MiddleCenter);
                StretchFull(emptyTxt.GetComponent<RectTransform>());
                return;
            }

            for (int i = 0; i < _activeRules.Count; i++)
            {
                AutopilotRule rule = _activeRules[i];
                GameObject rowObj = CreateUIObject("RuleRow_" + i, autopilotRulesContainer);
                Image rowBg = rowObj.AddComponent<Image>();
                UITheme.ApplyPanelFrame(rowBg, useDialogueBox: true);
                LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
                rowLe.preferredHeight = 32f;

                HorizontalLayoutGroup hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
                hLayout.padding = new RectOffset(10, 10, 4, 4);
                hLayout.spacing = 8f;
                hLayout.childAlignment = TextAnchor.MiddleLeft;
                hLayout.childControlWidth = true;
                hLayout.childControlHeight = true;
                hLayout.childForceExpandWidth = false;
                hLayout.childForceExpandHeight = true;

                GameObject badgeObj = CreateUIObject("TriggerBadge", rowObj.transform);
                Image badgeBg = badgeObj.AddComponent<Image>();
                UITheme.ApplyNameTab(badgeBg, new Color(0.42f, 0.54f, 0.72f, 1.0f));
                LayoutElement badgeLe = badgeObj.AddComponent<LayoutElement>();
                badgeLe.preferredWidth = 112f;
                badgeLe.minWidth = 104f;

                Text badgeTxt = CreateText("BadgeText", badgeObj.transform, font, "STANDBY (ELSE)", 11, FontStyle.Bold,
                    new Color(0.70f, 0.80f, 0.92f, 1.0f), TextAnchor.MiddleCenter);
                StretchFull(badgeTxt.GetComponent<RectTransform>());

                Text formulaTxt = CreateText(
                    "RuleFormulaText",
                    rowObj.transform,
                    font,
                    FormatAutopilotRule(rule, i),
                    13,
                    FontStyle.Normal,
                    new Color(0.84f, 0.90f, 0.98f, 1.0f),
                    TextAnchor.MiddleLeft);
                formulaTxt.supportRichText = true;
                formulaTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

                _ruleRowBackgrounds.Add(rowBg);
                _ruleBadgeBackgrounds.Add(badgeBg);
                _ruleBadgeTexts.Add(badgeTxt);
                _ruleFormulaTexts.Add(formulaTxt);
            }
        }

        private static void CreatePhaseBadge(
            Transform parent,
            string name,
            string label,
            Color bgColor,
            Font font,
            out Image badgeBg,
            out Text badgeText)
        {
            GameObject badgeObj = CreateUIObject(name, parent);
            badgeBg = badgeObj.AddComponent<Image>();
            UITheme.ApplyNameTab(badgeBg, bgColor);

            badgeText = CreateText("Label", badgeObj.transform, font, label, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(badgeText.GetComponent<RectTransform>());
        }

        private static void BuildTelemetryGaugeCard(
            Transform parent,
            string name,
            string title,
            string initialValue,
            string initialDetail,
            Color barColor,
            Sprite iconSprite,
            LedSheetKind ledKind,
            Font font,
            out Slider slider,
            out Image fillImage,
            out Image ledBarImage,
            out Text valueText,
            out Text detailText)
        {
            GameObject cardObj = CreateUIObject(name, parent);
            Image cardBg = cardObj.AddComponent<Image>();
            UITheme.ApplyPanelFrame(cardBg, useDialogueBox: true);
            LayoutElement cardLe = cardObj.AddComponent<LayoutElement>();
            cardLe.preferredHeight = 64f;

            HorizontalLayoutGroup cardRow = cardObj.AddComponent<HorizontalLayoutGroup>();
            cardRow.padding = new RectOffset(10, 10, 6, 6);
            cardRow.spacing = 10f;
            cardRow.childAlignment = TextAnchor.MiddleLeft;
            cardRow.childControlWidth = true;
            cardRow.childControlHeight = true;
            cardRow.childForceExpandWidth = false;
            cardRow.childForceExpandHeight = true;

            GameObject iconFrame = CreateUIObject("IconFrame", cardObj.transform);
            UITheme.ApplySlotFrame(iconFrame.AddComponent<Image>(), selected: false);
            LayoutElement iconLe = iconFrame.AddComponent<LayoutElement>();
            iconLe.minWidth = 42f;
            iconLe.preferredWidth = 42f;
            if (iconSprite != null)
            {
                GameObject iconObj = CreateUIObject("IconSprite", iconFrame.transform);
                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.12f, 0.12f);
                iconRt.anchorMax = new Vector2(0.88f, 0.88f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = iconSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            GameObject contentCol = CreateUIObject("GaugeContent", cardObj.transform);
            contentCol.AddComponent<LayoutElement>().flexibleWidth = 1f;

            VerticalLayoutGroup vLayout = contentCol.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 3f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            GameObject topRow = CreateUIObject("TopRow", contentCol.transform);
            topRow.AddComponent<LayoutElement>().preferredHeight = 18f;
            HorizontalLayoutGroup topLayout = topRow.AddComponent<HorizontalLayoutGroup>();
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;

            Text titleTxt = CreateText("Title", topRow.transform, font, title, 12, FontStyle.Bold,
                UITheme.TextPrimary, TextAnchor.MiddleLeft);
            titleTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            valueText = CreateText("ValueText", topRow.transform, font, initialValue, 13, FontStyle.Bold,
                barColor, TextAnchor.MiddleRight);
            valueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 220f;

            // Progress Bar Track with Slider, Image.fillAmount, and segmented Pixel-Art LED Overlay
            GameObject trackObj = CreateUIObject("SliderTrack", contentCol.transform);
            Image trackBg = trackObj.AddComponent<Image>();
            UITheme.ApplyNameTab(trackBg, new Color(0.55f, 0.65f, 0.82f, 1.0f));
            trackObj.AddComponent<LayoutElement>().preferredHeight = 15f;

            GameObject fillObj = CreateUIObject("Fill", trackObj.transform);
            RectTransform fillRt = fillObj.GetComponent<RectTransform>();
            StretchFull(fillRt);
            fillImage = fillObj.AddComponent<Image>();
            fillImage.sprite = RadarProceduralSprites.GetDiscSprite();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1.0f;
            fillImage.color = new Color(barColor.r, barColor.g, barColor.b, 0.18f);

            GameObject ledObj = CreateUIObject("LedBarOverlay", trackObj.transform);
            StretchFull(ledObj.GetComponent<RectTransform>());
            ledBarImage = ledObj.AddComponent<Image>();
            ledBarImage.sprite = UITheme.GetLedBarSprite(ledKind, 1.0f);
            ledBarImage.type = Image.Type.Sliced;
            ledBarImage.color = Color.white;
            ledBarImage.raycastTarget = false;

            slider = trackObj.AddComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            detailText = CreateText("DetailText", contentCol.transform, font, initialDetail, 11, FontStyle.Normal,
                UITheme.TextSecondary, TextAnchor.MiddleLeft);
            detailText.gameObject.AddComponent<LayoutElement>().preferredHeight = 15f;
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
