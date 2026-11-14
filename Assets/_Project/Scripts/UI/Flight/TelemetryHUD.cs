using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using AstraLink.Core;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// Live Flight Telemetry HUD &amp; Autopilot Rule Highlight Controller (Task S2-04).
    /// Updates real-time gauges (<see cref="Slider"/> and <see cref="Image.fillAmount"/>) and text counters for:
    /// Battery (<c>BatteryPercent / BatteryWh</c>), SSD (<c>SsdUsedPercent / SsdUsedGb</c>),
    /// Electronics Health (<c>HealthPercent</c>), Delivered Science (<c>DeliveredScience / TargetSciencePoints</c>),
    /// and Radiation (<c>RadiationLevel</c>).
    /// Also displays phase indicators (<c>IsInEclipse</c>, <c>IsInCommWindow</c>, <c>SafeModeActive</c>)
    /// and highlights triggered autopilot rules in green based on <see cref="SimulationTickSnapshot.TriggeredRuleIds"/>.
    /// </summary>
    public class TelemetryHUD : MonoBehaviour
    {
        [Header("Battery Telemetry (Slider + Fill Image + Text)")]
        [SerializeField] private Slider batterySlider;
        [SerializeField] private Image batteryFillImage;
        [SerializeField] private Text batteryValueLabel;

        [Header("SSD Storage Telemetry (Slider + Fill Image + Text)")]
        [SerializeField] private Slider ssdSlider;
        [SerializeField] private Image ssdFillImage;
        [SerializeField] private Text ssdValueLabel;

        [Header("Electronics Health Telemetry (Slider + Fill Image + Text)")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Text healthValueLabel;

        [Header("Delivered Science Telemetry (Slider + Fill Image + Text)")]
        [SerializeField] private Slider scienceSlider;
        [SerializeField] private Image scienceFillImage;
        [SerializeField] private Text scienceValueLabel;

        [Header("Radiation Level Telemetry (Slider + Fill Image + Text)")]
        [SerializeField] private Slider radiationSlider;
        [SerializeField] private Image radiationFillImage;
        [SerializeField] private Text radiationValueLabel;

        [Header("Current Orbital Phase Indicators")]
        [SerializeField] private Image eclipseIndicatorBg;
        [SerializeField] private Text eclipseIndicatorLabel;
        [SerializeField] private Image commWindowIndicatorBg;
        [SerializeField] private Text commWindowIndicatorLabel;
        [SerializeField] private Image safeModeIndicatorBg;
        [SerializeField] private Text safeModeIndicatorLabel;

        [Header("Autopilot Rules Highlight List")]
        [SerializeField] private RectTransform rulesListContainer;
        [SerializeField] private Text rulesSummaryLabel;

        private float _targetSciencePoints = 100.0f;
        private float _maxBatteryWh = 45.0f;
        private float _maxSsdGb = 16.0f;
        private List<AutopilotRule> _rules = new List<AutopilotRule>();

        private readonly List<Image> _ruleRowBackgrounds = new List<Image>();
        private readonly List<Text> _ruleRowTexts = new List<Text>();
        private readonly List<Image> _ruleStatusBadges = new List<Image>();
        private readonly List<Text> _ruleStatusLabels = new List<Text>();

        private static Sprite _solidBarSprite;

        private static readonly Color TriggeredRuleGreenBg = new Color(0.12f, 0.58f, 0.30f, 0.98f);
        private static readonly Color TriggeredRuleGreenBadge = new Color(0.18f, 0.82f, 0.42f, 1.0f);
        private static readonly Color StandbyRuleSlateBg = new Color(0.08f, 0.13f, 0.22f, 0.94f);
        private static readonly Color StandbyRuleSlateBadge = new Color(0.16f, 0.24f, 0.36f, 0.96f);
        private static readonly Color DisabledRuleBg = new Color(0.06f, 0.08f, 0.13f, 0.70f);

        private void Awake()
        {
            EnsureHudHierarchy();
        }

        /// <summary>
        /// Initializes the live telemetry HUD with mission goal targets, resource capacities, and active autopilot rules.
        /// </summary>
        public void Initialize(
            MissionGoal goal,
            ResourceSummary resources,
            List<AutopilotRule> rules)
        {
            EnsureHudHierarchy();

            _targetSciencePoints = (goal != null && goal.TargetSciencePoints > 0.0f)
                ? goal.TargetSciencePoints
                : 100.0f;

            _maxBatteryWh = (resources != null && resources.MaxBatteryWh > 0.0f)
                ? resources.MaxBatteryWh
                : 45.0f;

            _maxSsdGb = (resources != null && resources.MaxSsdGb > 0.0f)
                ? resources.MaxSsdGb
                : 16.0f;

            SetAutopilotRules(rules);
        }

        /// <summary>
        /// Sets the list of autopilot rules to display and highlight during flight playback.
        /// </summary>
        public void SetAutopilotRules(List<AutopilotRule> rules)
        {
            EnsureHudHierarchy();
            _rules = (rules != null && rules.Count > 0) ? new List<AutopilotRule>(rules) : CreateDefaultRules();
            RebuildRulesListUI();
            HighlightTriggeredRules(null);
        }

        /// <summary>
        /// Updates all telemetry bars, text counters, phase indicators, and autopilot rule green highlights for the given tick snapshot.
        /// </summary>
        public void UpdateTelemetry(SimulationTickSnapshot snapshot)
        {
            if (snapshot == null) return;
            EnsureHudHierarchy();

            // 1. Battery Charge (BatteryPercent / BatteryWh)
            float battRatio = Mathf.Clamp01(snapshot.BatteryPercent / 100.0f);
            float effectiveMaxWh = Mathf.Max(_maxBatteryWh, battRatio > 0.01f ? (snapshot.BatteryWh / battRatio) : _maxBatteryWh);
            Color battColor = snapshot.BatteryPercent > 40.0f
                ? new Color(0.18f, 0.84f, 0.48f, 1.0f)
                : (snapshot.BatteryPercent >= 15.0f
                    ? new Color(0.96f, 0.72f, 0.18f, 1.0f)
                    : new Color(0.96f, 0.26f, 0.28f, 1.0f));

            ApplyBarValue(batterySlider, batteryFillImage, battRatio, battColor);
            if (batteryValueLabel != null)
            {
                float netWatts = snapshot.SolarGenWatts - snapshot.PowerDrawWatts;
                string netSign = netWatts >= 0f ? "+" : "";
                batteryValueLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0}% ({1:0.0} / {2:0.0} Wh)   [{3}{4:0.0} W  |  Gen {5:0}W / Draw {6:0}W]",
                    snapshot.BatteryPercent,
                    snapshot.BatteryWh,
                    effectiveMaxWh,
                    netSign,
                    netWatts,
                    snapshot.SolarGenWatts,
                    snapshot.PowerDrawWatts);
                batteryValueLabel.color = snapshot.BatteryPercent < 15.0f
                    ? new Color(1.0f, 0.52f, 0.52f, 1.0f)
                    : Color.white;
            }

            // 2. SSD Storage (SsdUsedPercent / SsdUsedGb)
            float ssdRatio = Mathf.Clamp01(snapshot.SsdUsedPercent / 100.0f);
            float effectiveMaxGb = Mathf.Max(_maxSsdGb, ssdRatio > 0.01f ? (snapshot.SsdUsedGb / ssdRatio) : _maxSsdGb);
            Color ssdColor = snapshot.SsdUsedPercent < 75.0f
                ? new Color(0.18f, 0.72f, 0.96f, 1.0f)
                : (snapshot.SsdUsedPercent < 92.0f
                    ? new Color(0.96f, 0.72f, 0.18f, 1.0f)
                    : new Color(0.96f, 0.26f, 0.28f, 1.0f));

            ApplyBarValue(ssdSlider, ssdFillImage, ssdRatio, ssdColor);
            if (ssdValueLabel != null)
            {
                ssdValueLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0}% ({1:0.00} / {2:0.0} GB)   [{3}]",
                    snapshot.SsdUsedPercent,
                    snapshot.SsdUsedGb,
                    effectiveMaxGb,
                    snapshot.SsdUsedPercent >= 95.0f ? "OVERFLOW RISK!" : "BUFFER OK");
                ssdValueLabel.color = snapshot.SsdUsedPercent >= 95.0f
                    ? new Color(1.0f, 0.52f, 0.52f, 1.0f)
                    : Color.white;
            }

            // 3. Electronics Health (HealthPercent)
            float healthRatio = Mathf.Clamp01(snapshot.HealthPercent / 100.0f);
            Color healthColor = snapshot.HealthPercent >= 80.0f
                ? new Color(0.22f, 0.88f, 0.62f, 1.0f)
                : (snapshot.HealthPercent >= 40.0f
                    ? new Color(0.96f, 0.72f, 0.18f, 1.0f)
                    : new Color(0.96f, 0.24f, 0.26f, 1.0f));

            ApplyBarValue(healthSlider, healthFillImage, healthRatio, healthColor);
            if (healthValueLabel != null)
            {
                healthValueLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0}% Integrity   [{1}]",
                    snapshot.HealthPercent,
                    snapshot.HealthPercent >= 80.0f ? "NOMINAL" : (snapshot.HealthPercent > 0f ? "DAMAGED" : "FAILURE"));
                healthValueLabel.color = snapshot.HealthPercent < 50.0f
                    ? new Color(1.0f, 0.52f, 0.52f, 1.0f)
                    : Color.white;
            }

            // 4. Delivered Science (DeliveredScience / TargetSciencePoints)
            float sciRatio = _targetSciencePoints > 0.001f
                ? Mathf.Clamp01(snapshot.DeliveredScience / _targetSciencePoints)
                : 0.0f;
            Color sciColor = snapshot.DeliveredScience >= _targetSciencePoints
                ? new Color(0.24f, 0.94f, 0.58f, 1.0f)
                : new Color(0.38f, 0.68f, 1.0f, 1.0f);

            ApplyBarValue(scienceSlider, scienceFillImage, sciRatio, sciColor);
            if (scienceValueLabel != null)
            {
                scienceValueLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0} / {1:0.0} pts ({2:0}%)   [{3}]",
                    snapshot.DeliveredScience,
                    _targetSciencePoints,
                    sciRatio * 100.0f,
                    snapshot.DeliveredScience >= _targetSciencePoints ? "TARGET MET" : "COLLECTING");
                scienceValueLabel.color = snapshot.DeliveredScience >= _targetSciencePoints
                    ? new Color(0.56f, 1.0f, 0.76f, 1.0f)
                    : Color.white;
            }

            // 5. Radiation Level (RadiationLevel)
            float radRatio = Mathf.Clamp01(snapshot.RadiationLevel / 120.0f);
            bool highRadiation = snapshot.RadiationLevel > 50.0f;
            Color radColor = highRadiation
                ? new Color(0.98f, 0.24f, 0.34f, 1.0f)
                : (snapshot.RadiationLevel > 25.0f
                    ? new Color(0.96f, 0.68f, 0.18f, 1.0f)
                    : new Color(0.58f, 0.46f, 0.96f, 1.0f));

            ApplyBarValue(radiationSlider, radiationFillImage, radRatio, radColor);
            if (radiationValueLabel != null)
            {
                radiationValueLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0} rad   [{1}]",
                    snapshot.RadiationLevel,
                    highRadiation ? "DANGER (>50 RAD — SAFE MODE REQUIRED)" : "SAFE LEVEL");
                radiationValueLabel.color = highRadiation
                    ? new Color(1.0f, 0.48f, 0.52f, 1.0f)
                    : Color.white;
            }

            // 6. Phase Indicators (IsInEclipse, IsInCommWindow, SafeModeActive)
            UpdatePhaseIndicators(snapshot);

            // 7. Autopilot Rules Green Highlighting (TriggeredRuleIds)
            HighlightTriggeredRules(snapshot.TriggeredRuleIds);
        }

        private void UpdatePhaseIndicators(SimulationTickSnapshot snapshot)
        {
            if (eclipseIndicatorBg != null)
            {
                eclipseIndicatorBg.color = snapshot.IsInEclipse
                    ? new Color(0.32f, 0.20f, 0.64f, 0.98f)
                    : new Color(0.56f, 0.42f, 0.08f, 0.96f);
            }
            if (eclipseIndicatorLabel != null)
            {
                eclipseIndicatorLabel.text = snapshot.IsInEclipse
                    ? "ECLIPSE: IN SHADOW (Solar 0W)"
                    : "SUNLIGHT: ILLUMINATED";
                eclipseIndicatorLabel.color = snapshot.IsInEclipse
                    ? new Color(0.86f, 0.80f, 1.0f, 1.0f)
                    : new Color(1.0f, 0.92f, 0.56f, 1.0f);
            }

            if (commWindowIndicatorBg != null)
            {
                commWindowIndicatorBg.color = snapshot.IsInCommWindow
                    ? new Color(0.08f, 0.52f, 0.44f, 0.98f)
                    : new Color(0.12f, 0.18f, 0.28f, 0.94f);
            }
            if (commWindowIndicatorLabel != null)
            {
                commWindowIndicatorLabel.text = snapshot.IsInCommWindow
                    ? "COMM WINDOW: LINK ACTIVE"
                    : "COMM WINDOW: OUT OF RANGE";
                commWindowIndicatorLabel.color = snapshot.IsInCommWindow
                    ? new Color(0.52f, 1.0f, 0.88f, 1.0f)
                    : new Color(0.64f, 0.74f, 0.86f, 0.92f);
            }

            if (safeModeIndicatorBg != null)
            {
                safeModeIndicatorBg.color = snapshot.SafeModeActive
                    ? new Color(0.68f, 0.34f, 0.08f, 0.98f)
                    : new Color(0.10f, 0.32f, 0.46f, 0.96f);
            }
            if (safeModeIndicatorLabel != null)
            {
                safeModeIndicatorLabel.text = snapshot.SafeModeActive
                    ? "SAFE MODE: ON (Sci/Comms OFF)"
                    : string.Format(
                        "SAFE MODE: OFF (Sci:{0} | Comms:{1})",
                        snapshot.ScienceActive ? "ON" : "OFF",
                        snapshot.CommsActive ? "ON" : "OFF");
                safeModeIndicatorLabel.color = snapshot.SafeModeActive
                    ? new Color(1.0f, 0.88f, 0.48f, 1.0f)
                    : new Color(0.76f, 0.94f, 1.0f, 1.0f);
            }
        }

        /// <summary>
        /// Compares <paramref name="triggeredRuleIds"/> with the active autopilot rule list and highlights triggered rules in green.
        /// </summary>
        public void HighlightTriggeredRules(List<string> triggeredRuleIds)
        {
            int triggeredCount = 0;

            for (int i = 0; i < _rules.Count && i < _ruleRowBackgrounds.Count; i++)
            {
                AutopilotRule rule = _rules[i];
                bool isEnabled = rule != null && rule.Enabled;
                bool isTriggered = isEnabled && IsRuleTriggered(rule, i, triggeredRuleIds);

                if (isTriggered)
                {
                    triggeredCount++;
                }

                if (_ruleRowBackgrounds[i] != null)
                {
                    if (!isEnabled)
                    {
                        _ruleRowBackgrounds[i].color = DisabledRuleBg;
                    }
                    else if (isTriggered)
                    {
                        _ruleRowBackgrounds[i].color = TriggeredRuleGreenBg;
                    }
                    else
                    {
                        _ruleRowBackgrounds[i].color = StandbyRuleSlateBg;
                    }
                }

                if (i < _ruleStatusBadges.Count && _ruleStatusBadges[i] != null)
                {
                    _ruleStatusBadges[i].color = isTriggered
                        ? TriggeredRuleGreenBadge
                        : StandbyRuleSlateBadge;
                }

                if (i < _ruleStatusLabels.Count && _ruleStatusLabels[i] != null)
                {
                    if (!isEnabled)
                    {
                        _ruleStatusLabels[i].text = "DISABLED";
                        _ruleStatusLabels[i].color = new Color(0.55f, 0.60f, 0.68f, 0.85f);
                    }
                    else if (isTriggered)
                    {
                        _ruleStatusLabels[i].text = "● TRIGGERED";
                        _ruleStatusLabels[i].color = Color.white;
                    }
                    else
                    {
                        _ruleStatusLabels[i].text = "○ STANDBY";
                        _ruleStatusLabels[i].color = new Color(0.72f, 0.82f, 0.94f, 0.92f);
                    }
                }

                if (i < _ruleRowTexts.Count && _ruleRowTexts[i] != null)
                {
                    _ruleRowTexts[i].text = FormatRuleText(rule, i);
                    _ruleRowTexts[i].color = isTriggered
                        ? Color.white
                        : new Color(0.86f, 0.92f, 1.0f, isEnabled ? 0.96f : 0.55f);
                }
            }

            if (rulesSummaryLabel != null)
            {
                rulesSummaryLabel.text = string.Format(
                    "AUTOPILOT RULES ({0} Active | {1} Triggered on Current Tick)",
                    _rules.Count,
                    triggeredCount);
                rulesSummaryLabel.color = triggeredCount > 0
                    ? new Color(0.46f, 1.0f, 0.64f, 1.0f)
                    : new Color(0.84f, 0.92f, 1.0f, 1.0f);
            }
        }

        private static bool IsRuleTriggered(AutopilotRule rule, int index, List<string> triggeredRuleIds)
        {
            if (rule == null || triggeredRuleIds == null || triggeredRuleIds.Count == 0)
            {
                return false;
            }

            string explicitId = !string.IsNullOrWhiteSpace(rule.RuleId)
                ? rule.RuleId.Trim()
                : string.Empty;
            string fallbackId = "rule_" + (index + 1).ToString(CultureInfo.InvariantCulture);

            for (int i = 0; i < triggeredRuleIds.Count; i++)
            {
                string id = triggeredRuleIds[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                string trimmed = id.Trim();

                if ((!string.IsNullOrEmpty(explicitId) && string.Equals(trimmed, explicitId, StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(trimmed, fallbackId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatRuleText(AutopilotRule rule, int index)
        {
            if (rule == null)
            {
                return string.Format("#{0}: <Empty Rule>", index + 1);
            }

            string opSymbol = rule.Operator == ComparisonOp.LessThan ? "<" : ">";
            string unit = GetMetricUnit(rule.Metric);
            string elsePart = rule.ElseAction != AutopilotAction.None
                ? string.Format("  ELSE  {0}", FormatActionName(rule.ElseAction))
                : string.Empty;

            return string.Format(
                CultureInfo.InvariantCulture,
                "#{0} [{1}]   IF  {2} {3} {4:0.#}{5}   THEN  {6}{7}",
                index + 1,
                !string.IsNullOrWhiteSpace(rule.RuleId) ? rule.RuleId : ("rule_" + (index + 1)),
                rule.Metric,
                opSymbol,
                rule.Threshold,
                unit,
                FormatActionName(rule.ThenAction),
                elsePart);
        }

        private static string GetMetricUnit(SensorMetric metric)
        {
            switch (metric)
            {
                case SensorMetric.SolarWatts: return " W";
                case SensorMetric.BatteryPercent: return "%";
                case SensorMetric.SsdUsedPercent: return "%";
                case SensorMetric.RadiationLevel: return " rad";
                default: return string.Empty;
            }
        }

        private static string FormatActionName(AutopilotAction action)
        {
            switch (action)
            {
                case AutopilotAction.ScienceOn: return "Science ON";
                case AutopilotAction.ScienceOff: return "Science OFF";
                case AutopilotAction.CommsOn: return "Comms ON";
                case AutopilotAction.CommsOff: return "Comms OFF";
                case AutopilotAction.SafeModeOn: return "SafeMode ON";
                case AutopilotAction.SafeModeOff: return "SafeMode OFF";
                default: return "None";
            }
        }

        private static void ApplyBarValue(Slider slider, Image fillImage, float normalizedValue, Color barColor)
        {
            float clamped = Mathf.Clamp01(normalizedValue);
            if (slider != null)
            {
                slider.normalizedValue = clamped;
            }

            if (fillImage != null)
            {
                if (fillImage.sprite == null)
                {
                    fillImage.sprite = GetSolidBarSprite();
                }
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImage.fillAmount = clamped;
                fillImage.color = barColor;
            }
        }

        private static Sprite GetSolidBarSprite()
        {
            if (_solidBarSprite != null) return _solidBarSprite;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                name = "Procedural_TelemetryBar",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            _solidBarSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            return _solidBarSprite;
        }

        private void RebuildRulesListUI()
        {
            if (rulesListContainer == null) return;

            for (int i = rulesListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(rulesListContainer.GetChild(i).gameObject);
            }

            _ruleRowBackgrounds.Clear();
            _ruleRowTexts.Clear();
            _ruleStatusBadges.Clear();
            _ruleStatusLabels.Clear();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int count = Mathf.Max(1, _rules.Count);

            for (int i = 0; i < _rules.Count; i++)
            {
                float step = 1.0f / count;
                float minY = 1.0f - (i + 1) * step + 0.02f;
                float maxY = 1.0f - i * step - 0.02f;

                var rowObj = new GameObject("RuleRow_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                rowObj.transform.SetParent(rulesListContainer, false);
                var rowRt = rowObj.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0.0f, Mathf.Clamp01(minY));
                rowRt.anchorMax = new Vector2(1.0f, Mathf.Clamp01(maxY));
                rowRt.offsetMin = Vector2.zero;
                rowRt.offsetMax = Vector2.zero;

                var rowBg = rowObj.GetComponent<Image>();
                rowBg.color = StandbyRuleSlateBg;
                _ruleRowBackgrounds.Add(rowBg);

                Text ruleTxt = FindOrCreateText(
                    rowRt,
                    "RuleFormulaText",
                    new Vector2(0.02f, 0.05f),
                    new Vector2(0.75f, 0.95f),
                    FormatRuleText(_rules[i], i),
                    13,
                    FontStyle.Bold,
                    Color.white,
                    TextAnchor.MiddleLeft,
                    font);
                _ruleRowTexts.Add(ruleTxt);

                var badgeObj = new GameObject("StatusBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeObj.transform.SetParent(rowRt, false);
                var badgeRt = badgeObj.GetComponent<RectTransform>();
                badgeRt.anchorMin = new Vector2(0.76f, 0.14f);
                badgeRt.anchorMax = new Vector2(0.985f, 0.86f);
                badgeRt.offsetMin = Vector2.zero;
                badgeRt.offsetMax = Vector2.zero;

                var badgeImg = badgeObj.GetComponent<Image>();
                badgeImg.color = StandbyRuleSlateBadge;
                _ruleStatusBadges.Add(badgeImg);

                Text badgeTxt = FindOrCreateText(
                    badgeRt,
                    "StatusLabel",
                    Vector2.zero,
                    Vector2.one,
                    "○ STANDBY",
                    12,
                    FontStyle.Bold,
                    Color.white,
                    TextAnchor.MiddleCenter,
                    font);
                _ruleStatusLabels.Add(badgeTxt);
            }
        }

        /// <summary>
        /// Creates a default set of autopilot rules for standalone testing when no rules are passed.
        /// </summary>
        public static List<AutopilotRule> CreateDefaultRules()
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
                    RuleId = "rule_ssd_downlink_guard",
                    Enabled = true,
                    Metric = SensorMetric.SsdUsedPercent,
                    Operator = ComparisonOp.GreaterThan,
                    Threshold = 80.0f,
                    ThenAction = AutopilotAction.CommsOn,
                    ElseAction = AutopilotAction.None
                },
                new AutopilotRule
                {
                    RuleId = "rule_radiation_safemode",
                    Enabled = true,
                    Metric = SensorMetric.RadiationLevel,
                    Operator = ComparisonOp.GreaterThan,
                    Threshold = 50.0f,
                    ThenAction = AutopilotAction.SafeModeOn,
                    ElseAction = AutopilotAction.None
                }
            };
        }

        /// <summary>
        /// Builds or binds the UI hierarchy for the live telemetry gauges, phase indicators, and autopilot rule rows.
        /// </summary>
        public void EnsureHudHierarchy()
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

            FindOrCreateText(
                rootRt,
                "TelemetrySectionTitle",
                new Vector2(0.03f, 0.93f),
                new Vector2(0.97f, 0.99f),
                "LIVE SPACECRAFT TELEMETRY & AUTOPILOT MONITOR",
                16,
                FontStyle.Bold,
                new Color(0.88f, 0.95f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft,
                font);

            // Phase Indicator Badges Row (Eclipse | Comm Window | Safe Mode)
            EnsurePhaseBadge(
                rootRt,
                "EclipsePhaseBadge",
                new Vector2(0.03f, 0.84f),
                new Vector2(0.33f, 0.92f),
                "SUNLIGHT: ILLUMINATED",
                new Color(0.56f, 0.42f, 0.08f, 0.96f),
                font,
                ref eclipseIndicatorBg,
                ref eclipseIndicatorLabel);

            EnsurePhaseBadge(
                rootRt,
                "CommWindowPhaseBadge",
                new Vector2(0.345f, 0.84f),
                new Vector2(0.645f, 0.92f),
                "COMM WINDOW: LINK ACTIVE",
                new Color(0.08f, 0.52f, 0.44f, 0.98f),
                font,
                ref commWindowIndicatorBg,
                ref commWindowIndicatorLabel);

            EnsurePhaseBadge(
                rootRt,
                "SafeModePhaseBadge",
                new Vector2(0.66f, 0.84f),
                new Vector2(0.97f, 0.92f),
                "SAFE MODE: OFF (Sci:ON | Comms:ON)",
                new Color(0.10f, 0.32f, 0.46f, 0.96f),
                font,
                ref safeModeIndicatorBg,
                ref safeModeIndicatorLabel);

            // 5 Live Telemetry Bars (Slider + Image.fillAmount + Counter Label)
            EnsureGaugeRow(
                rootRt,
                "Gauge_Battery",
                "1. BATTERY CHARGE:",
                new Vector2(0.03f, 0.74f),
                new Vector2(0.97f, 0.825f),
                new Color(0.18f, 0.84f, 0.48f, 1.0f),
                "100% (45.0 / 45.0 Wh)",
                font,
                ref batterySlider,
                ref batteryFillImage,
                ref batteryValueLabel);

            EnsureGaugeRow(
                rootRt,
                "Gauge_SSD",
                "2. ONBOARD SSD:",
                new Vector2(0.03f, 0.645f),
                new Vector2(0.97f, 0.73f),
                new Color(0.18f, 0.72f, 0.96f, 1.0f),
                "5% (0.80 / 16.0 GB)",
                font,
                ref ssdSlider,
                ref ssdFillImage,
                ref ssdValueLabel);

            EnsureGaugeRow(
                rootRt,
                "Gauge_Health",
                "3. ELECTRONICS HEALTH:",
                new Vector2(0.03f, 0.55f),
                new Vector2(0.97f, 0.635f),
                new Color(0.22f, 0.88f, 0.62f, 1.0f),
                "100% Integrity [NOMINAL]",
                font,
                ref healthSlider,
                ref healthFillImage,
                ref healthValueLabel);

            EnsureGaugeRow(
                rootRt,
                "Gauge_Science",
                "4. DELIVERED SCIENCE:",
                new Vector2(0.03f, 0.455f),
                new Vector2(0.97f, 0.54f),
                new Color(0.38f, 0.68f, 1.0f, 1.0f),
                "0.0 / 100.0 pts (0%)",
                font,
                ref scienceSlider,
                ref scienceFillImage,
                ref scienceValueLabel);

            EnsureGaugeRow(
                rootRt,
                "Gauge_Radiation",
                "5. RADIATION LEVEL:",
                new Vector2(0.03f, 0.36f),
                new Vector2(0.97f, 0.445f),
                new Color(0.58f, 0.46f, 0.96f, 1.0f),
                "5.0 rad [SAFE LEVEL]",
                font,
                ref radiationSlider,
                ref radiationFillImage,
                ref radiationValueLabel);

            // Autopilot Rules Section
            if (rulesSummaryLabel == null)
            {
                rulesSummaryLabel = FindOrCreateText(
                    rootRt,
                    "RulesSummaryHeader",
                    new Vector2(0.03f, 0.285f),
                    new Vector2(0.97f, 0.35f),
                    "AUTOPILOT RULES (Green = Triggered on Current Tick)",
                    14,
                    FontStyle.Bold,
                    new Color(0.84f, 0.92f, 1.0f, 1.0f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (rulesListContainer == null)
            {
                Transform existingContainer = rootRt.Find("RulesListContainer");
                if (existingContainer != null)
                {
                    rulesListContainer = existingContainer.GetComponent<RectTransform>();
                }
                else
                {
                    var containerObj = new GameObject("RulesListContainer", typeof(RectTransform));
                    containerObj.transform.SetParent(rootRt, false);
                    rulesListContainer = containerObj.GetComponent<RectTransform>();
                    rulesListContainer.anchorMin = new Vector2(0.03f, 0.02f);
                    rulesListContainer.anchorMax = new Vector2(0.97f, 0.28f);
                    rulesListContainer.offsetMin = Vector2.zero;
                    rulesListContainer.offsetMax = Vector2.zero;
                }
            }

            if (_rules == null || _rules.Count == 0)
            {
                _rules = CreateDefaultRules();
                RebuildRulesListUI();
            }
        }

        private static void EnsurePhaseBadge(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string defaultText,
            Color bgColor,
            Font font,
            ref Image badgeBg,
            ref Text badgeLabel)
        {
            if (badgeBg != null && badgeLabel != null) return;

            Transform existing = parent.Find(name);
            RectTransform rt;
            if (existing != null)
            {
                rt = existing.GetComponent<RectTransform>();
                badgeBg = existing.GetComponent<Image>();
                badgeLabel = existing.GetComponentInChildren<Text>();
                return;
            }

            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            badgeBg = obj.GetComponent<Image>();
            badgeBg.color = bgColor;

            badgeLabel = FindOrCreateText(
                rt,
                "Label",
                new Vector2(0.04f, 0.05f),
                new Vector2(0.96f, 0.95f),
                defaultText,
                12,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter,
                font);
        }

        private static void EnsureGaugeRow(
            RectTransform parent,
            string rowName,
            string title,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color barColor,
            string defaultValueText,
            Font font,
            ref Slider sliderRef,
            ref Image fillImageRef,
            ref Text valueLabelRef)
        {
            if (sliderRef != null && fillImageRef != null && valueLabelRef != null) return;

            Transform existing = parent.Find(rowName);
            RectTransform rowRt;
            if (existing != null)
            {
                rowRt = existing.GetComponent<RectTransform>();
            }
            else
            {
                var rowObj = new GameObject(rowName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                rowObj.transform.SetParent(parent, false);
                rowRt = rowObj.GetComponent<RectTransform>();
                rowRt.anchorMin = anchorMin;
                rowRt.anchorMax = anchorMax;
                rowRt.offsetMin = Vector2.zero;
                rowRt.offsetMax = Vector2.zero;
                rowObj.GetComponent<Image>().color = new Color(0.07f, 0.12f, 0.20f, 0.90f);
            }

            FindOrCreateText(
                rowRt,
                "TitleLabel",
                new Vector2(0.02f, 0.52f),
                new Vector2(0.38f, 0.96f),
                title,
                12,
                FontStyle.Bold,
                new Color(0.76f, 0.88f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft,
                font);

            if (valueLabelRef == null)
            {
                valueLabelRef = FindOrCreateText(
                    rowRt,
                    "ValueLabel",
                    new Vector2(0.38f, 0.52f),
                    new Vector2(0.98f, 0.96f),
                    defaultValueText,
                    12,
                    FontStyle.Bold,
                    Color.white,
                    TextAnchor.MiddleRight,
                    font);
            }

            Transform existingTrack = rowRt.Find("BarTrack");
            RectTransform trackRt;
            if (existingTrack != null)
            {
                trackRt = existingTrack.GetComponent<RectTransform>();
                sliderRef = existingTrack.GetComponent<Slider>();
            }
            else
            {
                var trackObj = new GameObject("BarTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
                trackObj.transform.SetParent(rowRt, false);
                trackRt = trackObj.GetComponent<RectTransform>();
                trackRt.anchorMin = new Vector2(0.02f, 0.12f);
                trackRt.anchorMax = new Vector2(0.98f, 0.46f);
                trackRt.offsetMin = Vector2.zero;
                trackRt.offsetMax = Vector2.zero;
                trackObj.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.96f);

                sliderRef = trackObj.GetComponent<Slider>();
                sliderRef.interactable = false;
                sliderRef.transition = Selectable.Transition.None;
                sliderRef.minValue = 0f;
                sliderRef.maxValue = 1f;
                sliderRef.value = 1f;
            }

            if (fillImageRef == null)
            {
                Transform existingFill = trackRt.Find("Fill");
                if (existingFill != null)
                {
                    fillImageRef = existingFill.GetComponent<Image>();
                }
                else
                {
                    var fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    fillObj.transform.SetParent(trackRt, false);
                    var fillRt = fillObj.GetComponent<RectTransform>();
                    fillRt.anchorMin = Vector2.zero;
                    fillRt.anchorMax = Vector2.one;
                    fillRt.offsetMin = Vector2.zero;
                    fillRt.offsetMax = Vector2.zero;

                    fillImageRef = fillObj.GetComponent<Image>();
                    fillImageRef.sprite = GetSolidBarSprite();
                    fillImageRef.type = Image.Type.Filled;
                    fillImageRef.fillMethod = Image.FillMethod.Horizontal;
                    fillImageRef.fillOrigin = (int)Image.OriginHorizontal.Left;
                    fillImageRef.fillAmount = 1.0f;
                    fillImageRef.color = barColor;
                }
            }
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
