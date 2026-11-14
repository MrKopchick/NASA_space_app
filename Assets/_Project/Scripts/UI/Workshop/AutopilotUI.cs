using System;
using System.Collections.Generic;
using System.Globalization;
using AstraLink.Core;
using AstraLink.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Workshop
{
    /// <summary>
    /// Task S2-03 (Ivan V): Construct-Style Vertical Autopilot Rule Editor (2–6 rule slots depending on AgencyState.AutopilotSlotsUnlocked).
    /// Opens as a dedicated modal window from the Hangar (like R&D Tech Tree).
    /// Each rule row is structured like a Construct Event Block:
    ///   [Enabled Toggle] -> [OBJECT / SENSOR (TMP_Dropdown #1 + Installed Module Name)] ->
    ///   [IF CONDITION: ComparisonOp (TMP_Dropdown #2) + Threshold (InputField)] ->
    ///   [THEN CHANGE OBJECT (TMP_Dropdown #3)] -> [ELSE CHANGE OBJECT (TMP_Dropdown #4)]
    /// </summary>
    public class AutopilotUI : MonoBehaviour
    {
        public const int MaxTotalRuleRows = 6;

        [Serializable]
        private class RuleRowWidgets
        {
            public string RuleId;
            public int SlotIndex;
            public GameObject RowRoot;
            public Image RowBackground;
            public Toggle EnabledToggle;
            public TMP_Dropdown MetricDropdown;
            public Text InstalledObjectSubText;
            public TMP_Dropdown OperatorDropdown;
            public InputField ThresholdInput;
            public TMP_Dropdown ThenDropdown;
            public TMP_Dropdown ElseDropdown;
            public Text LockedOverlayText;
            public bool IsUnlocked;
        }

        private AgencyState _agencyState;
        private List<SatelliteComponent> _installedComponents = new List<SatelliteComponent>();
        private readonly List<RuleRowWidgets> _rowWidgets = new List<RuleRowWidgets>();
        private readonly List<AutopilotRule> _rules = new List<AutopilotRule>();
        private Text _headerStatusText;
        private bool _suppressCallbacks;

        private static TMP_FontAsset _cachedTmpFont;

        // Construct-style Object/Sensor labels mapped 1:1 to SensorMetric enum (0..3)
        private static readonly List<string> MetricOptions = new List<string>
        {
            "Solar Panel: SolarWatts (W)",
            "Battery Pack: BatteryPercent (%)",
            "SSD Storage: SsdUsedPercent (%)",
            "Rad Dosimeter: RadiationLevel (rad)"
        };

        // ComparisonOp enum (0..1)
        private static readonly List<string> OperatorOptions = new List<string>
        {
            "< (LessThan)",
            "> (GreaterThan)"
        };

        // Construct-style Target Object + Action labels mapped 1:1 to AutopilotAction enum (0..6)
        private static readonly List<string> ActionOptions = new List<string>
        {
            "None (No Change)",
            "Payload: ScienceOn",
            "Payload: ScienceOff",
            "Antenna: CommsOn",
            "Antenna: CommsOff",
            "Bus: SafeModeOn",
            "Bus: SafeModeOff"
        };

        public event Action<List<AutopilotRule>> OnRulesChanged;
        public event Action OnRequestOpenDryRun;

        public void Initialize(AgencyState agencyState, List<SatelliteComponent> installedComponents = null)
        {
            _agencyState = agencyState ?? (GameManager.Instance != null ? GameManager.Instance.CurrentAgencyState : new AgencyState());
            if (installedComponents != null)
            {
                _installedComponents = installedComponents;
            }
            EnsureDefaultRulesList();
            EnsureAutopilotUIHierarchy();
            SyncUIFromRules();
        }

        public void SetInstalledComponents(List<SatelliteComponent> installedComponents)
        {
            _installedComponents = installedComponents ?? new List<SatelliteComponent>();
            UpdateInstalledObjectLabels();
        }

        public void CloseModal()
        {
            gameObject.SetActive(false);
        }

        [ContextMenu("Generate Autopilot UI In Scene")]
        public void BuildUIInEditor()
        {
            if (_agencyState == null) _agencyState = new AgencyState();
            EnsureDefaultRulesList();

            Transform existing = transform.Find("AutopilotContentRoot");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            EnsureAutopilotUIHierarchy();
            SyncUIFromRules();
            Debug.Log("[AutopilotUI] Construct-style 6-Slot Autopilot Rule Editor generated!");
        }

        /// <summary>
        /// Refreshes which slots (2, 4, or 6) are unlocked based on AgencyState.AutopilotSlotsUnlocked.
        /// </summary>
        public void RefreshSlotLocks(AgencyState state = null, List<SatelliteComponent> installedComponents = null)
        {
            if (state != null) _agencyState = state;
            if (installedComponents != null) _installedComponents = installedComponents;
            EnsureDefaultRulesList();
            EnsureAutopilotUIHierarchy();
            SyncUIFromRules();
        }

        /// <summary>
        /// Returns all rules in unlocked slots (2–6 rules depending on AgencyState.AutopilotSlotsUnlocked).
        /// </summary>
        public List<AutopilotRule> GetActiveRules()
        {
            EnsureDefaultRulesList();
            int unlocked = GetUnlockedSlotsCount();
            var result = new List<AutopilotRule>(unlocked);

            for (int i = 0; i < unlocked && i < _rules.Count; i++)
            {
                AutopilotRule src = _rules[i];
                result.Add(new AutopilotRule
                {
                    RuleId = src.RuleId,
                    Enabled = src.Enabled,
                    Metric = src.Metric,
                    Operator = src.Operator,
                    Threshold = src.Threshold,
                    ThenAction = src.ThenAction,
                    ElseAction = src.ElseAction
                });
            }
            return result;
        }

        /// <summary>
        /// Highlights rules that triggered on the current Dry Run / Flight tick.
        /// </summary>
        public void HighlightTriggeredRules(List<string> triggeredRuleIds)
        {
            for (int i = 0; i < _rowWidgets.Count; i++)
            {
                RuleRowWidgets w = _rowWidgets[i];
                if (w == null || w.RowBackground == null) continue;

                if (!w.IsUnlocked)
                {
                    w.RowBackground.color = new Color(0.05f, 0.07f, 0.12f, 0.88f);
                    continue;
                }

                bool isTriggered = triggeredRuleIds != null && triggeredRuleIds.Contains(w.RuleId);
                bool isEnabled = i < _rules.Count && _rules[i].Enabled;

                if (isTriggered && isEnabled)
                {
                    w.RowBackground.color = new Color(0.14f, 0.42f, 0.28f, 0.98f);
                }
                else if (isEnabled)
                {
                    w.RowBackground.color = new Color(0.09f, 0.16f, 0.27f, 0.96f);
                }
                else
                {
                    w.RowBackground.color = new Color(0.06f, 0.10f, 0.17f, 0.90f);
                }
            }
        }

        public void ApplyEclipsePreset()
        {
            EnsureDefaultRulesList();
            _rules[0] = new AutopilotRule
            {
                RuleId = "rule_1",
                Enabled = true,
                Metric = SensorMetric.SolarWatts,
                Operator = ComparisonOp.LessThan,
                Threshold = 5f,
                ThenAction = AutopilotAction.ScienceOff,
                ElseAction = AutopilotAction.ScienceOn
            };
            SyncUIFromRules();
            NotifyRulesChanged();
        }

        public void ApplyStormSafeModePreset()
        {
            EnsureDefaultRulesList();
            int targetIdx = GetUnlockedSlotsCount() > 1 ? 1 : 0;
            _rules[targetIdx] = new AutopilotRule
            {
                RuleId = "rule_" + (targetIdx + 1),
                Enabled = true,
                Metric = SensorMetric.RadiationLevel,
                Operator = ComparisonOp.GreaterThan,
                Threshold = 50f,
                ThenAction = AutopilotAction.SafeModeOn,
                ElseAction = AutopilotAction.SafeModeOff
            };
            SyncUIFromRules();
            NotifyRulesChanged();
        }

        public void ClearAllRules()
        {
            _rules.Clear();
            EnsureDefaultRulesList();
            for (int i = 0; i < _rules.Count; i++)
            {
                _rules[i].Enabled = false;
            }
            SyncUIFromRules();
            NotifyRulesChanged();
        }

        private int GetUnlockedSlotsCount()
        {
            if (_agencyState == null && GameManager.Instance != null)
            {
                _agencyState = GameManager.Instance.CurrentAgencyState;
            }
            int unlocked = _agencyState != null ? _agencyState.AutopilotSlotsUnlocked : 2;
            return Mathf.Clamp(unlocked, 2, MaxTotalRuleRows);
        }

        private void EnsureDefaultRulesList()
        {
            while (_rules.Count < MaxTotalRuleRows)
            {
                int idx = _rules.Count;
                _rules.Add(CreateDefaultRuleForSlot(idx));
            }
        }

        private static AutopilotRule CreateDefaultRuleForSlot(int index)
        {
            if (index == 0)
            {
                return new AutopilotRule
                {
                    RuleId = "rule_1",
                    Enabled = true,
                    Metric = SensorMetric.SolarWatts,
                    Operator = ComparisonOp.LessThan,
                    Threshold = 0f,
                    ThenAction = AutopilotAction.ScienceOff,
                    ElseAction = AutopilotAction.ScienceOn
                };
            }
            if (index == 1)
            {
                return new AutopilotRule
                {
                    RuleId = "rule_2",
                    Enabled = false,
                    Metric = SensorMetric.RadiationLevel,
                    Operator = ComparisonOp.GreaterThan,
                    Threshold = 0f,
                    ThenAction = AutopilotAction.SafeModeOn,
                    ElseAction = AutopilotAction.SafeModeOff
                };
            }
            if (index == 2)
            {
                return new AutopilotRule
                {
                    RuleId = "rule_3",
                    Enabled = false,
                    Metric = SensorMetric.SsdUsedPercent,
                    Operator = ComparisonOp.GreaterThan,
                    Threshold = 0f,
                    ThenAction = AutopilotAction.CommsOn,
                    ElseAction = AutopilotAction.None
                };
            }
            return new AutopilotRule
            {
                RuleId = "rule_" + (index + 1),
                Enabled = false,
                Metric = SensorMetric.BatteryPercent,
                Operator = ComparisonOp.LessThan,
                Threshold = 0f,
                ThenAction = AutopilotAction.ScienceOff,
                ElseAction = AutopilotAction.None
            };
        }

        private void OnRowChanged(int index)
        {
            if (_suppressCallbacks || index < 0 || index >= _rowWidgets.Count || index >= _rules.Count) return;

            RuleRowWidgets w = _rowWidgets[index];
            AutopilotRule r = _rules[index];
            if (w == null || r == null) return;

            if (w.EnabledToggle != null) r.Enabled = w.EnabledToggle.isOn;
            if (w.MetricDropdown != null) r.Metric = (SensorMetric)Mathf.Clamp(w.MetricDropdown.value, 0, 3);
            if (w.OperatorDropdown != null) r.Operator = (ComparisonOp)Mathf.Clamp(w.OperatorDropdown.value, 0, 1);

            if (w.ThresholdInput != null)
            {
                bool isPercentage = IsPercentageMetric(r.Metric);
                string raw = w.ThresholdInput.text ?? string.Empty;
                if (raw.Length > 0)
                {
                    float normalizedValue;
                    bool shouldRewriteText = TryNormalizeThresholdInput(raw, isPercentage, out normalizedValue);
                    r.Threshold = normalizedValue;

                    if (shouldRewriteText)
                    {
                        _suppressCallbacks = true;
                        string formatted = normalizedValue.ToString("0.#", CultureInfo.InvariantCulture);
                        w.ThresholdInput.SetTextWithoutNotify(formatted);
                        w.ThresholdInput.caretPosition = formatted.Length;
                        _suppressCallbacks = false;
                    }
                }
            }

            if (w.ThenDropdown != null) r.ThenAction = (AutopilotAction)Mathf.Clamp(w.ThenDropdown.value, 0, 6);
            if (w.ElseDropdown != null) r.ElseAction = (AutopilotAction)Mathf.Clamp(w.ElseDropdown.value, 0, 6);

            UpdateInstalledObjectLabels();
            HighlightTriggeredRules(null);
            NotifyRulesChanged();
        }

        private void SyncUIFromRules()
        {
            _suppressCallbacks = true;
            int unlockedSlots = GetUnlockedSlotsCount();

            if (_headerStatusText != null)
            {
                _headerStatusText.text = string.Format(
                    "CONSTRUCT-STYLE AUTOPILOT EVENT SHEET   •   CPU SLOTS UNLOCKED: {0} / {1}",
                    unlockedSlots, MaxTotalRuleRows);
            }

            for (int i = 0; i < _rowWidgets.Count && i < _rules.Count; i++)
            {
                RuleRowWidgets w = _rowWidgets[i];
                AutopilotRule r = _rules[i];
                if (w == null || r == null) continue;

                bool isUnlocked = i < unlockedSlots;
                w.IsUnlocked = isUnlocked;

                if (w.EnabledToggle != null)
                {
                    w.EnabledToggle.interactable = isUnlocked;
                    w.EnabledToggle.isOn = isUnlocked && r.Enabled;
                }
                if (w.MetricDropdown != null)
                {
                    w.MetricDropdown.interactable = isUnlocked;
                    w.MetricDropdown.value = (int)r.Metric;
                    w.MetricDropdown.RefreshShownValue();
                }
                if (w.OperatorDropdown != null)
                {
                    w.OperatorDropdown.interactable = isUnlocked;
                    w.OperatorDropdown.value = (int)r.Operator;
                    w.OperatorDropdown.RefreshShownValue();
                }
                if (w.ThresholdInput != null)
                {
                    w.ThresholdInput.interactable = isUnlocked;
                    w.ThresholdInput.text = r.Threshold.ToString("0.#", CultureInfo.InvariantCulture);
                }
                if (w.ThenDropdown != null)
                {
                    w.ThenDropdown.interactable = isUnlocked;
                    w.ThenDropdown.value = (int)r.ThenAction;
                    w.ThenDropdown.RefreshShownValue();
                }
                if (w.ElseDropdown != null)
                {
                    w.ElseDropdown.interactable = isUnlocked;
                    w.ElseDropdown.value = (int)r.ElseAction;
                    w.ElseDropdown.RefreshShownValue();
                }

                if (w.LockedOverlayText != null && w.LockedOverlayText.transform.parent != null)
                {
                    w.LockedOverlayText.transform.parent.gameObject.SetActive(!isUnlocked);
                    if (!isUnlocked)
                    {
                        int reqSlots = i < 4 ? 4 : 6;
                        int cost = i < 4 ? GameManager.UpgradeTo4SlotsCostThousands : GameManager.UpgradeTo6SlotsCostThousands;
                        w.LockedOverlayText.text = string.Format(
                            "EVENT SLOT #{0} LOCKED — Upgrade Onboard CPU to {1} Slots (${2}k) in R&D Tech Tree",
                            i + 1, reqSlots, cost);
                    }
                }
            }

            _suppressCallbacks = false;
            UpdateInstalledObjectLabels();
            HighlightTriggeredRules(null);
        }

        private void UpdateInstalledObjectLabels()
        {
            for (int i = 0; i < _rowWidgets.Count && i < _rules.Count; i++)
            {
                RuleRowWidgets w = _rowWidgets[i];
                if (w == null || w.InstalledObjectSubText == null) continue;

                SensorMetric metric = _rules[i].Metric;
                w.InstalledObjectSubText.text = GetInstalledModuleDescriptionForMetric(metric);
            }
        }

        private string GetInstalledModuleDescriptionForMetric(SensorMetric metric)
        {
            ComponentCategory targetCat;
            switch (metric)
            {
                case SensorMetric.SolarWatts:
                    targetCat = ComponentCategory.PowerGeneration;
                    break;
                case SensorMetric.BatteryPercent:
                    targetCat = ComponentCategory.EnergyStorage;
                    break;
                case SensorMetric.SsdUsedPercent:
                    targetCat = ComponentCategory.CommsAndStorage;
                    break;
                case SensorMetric.RadiationLevel:
                default:
                    targetCat = ComponentCategory.CorePlatform;
                    break;
            }

            if (_installedComponents != null)
            {
                for (int i = 0; i < _installedComponents.Count; i++)
                {
                    SatelliteComponent c = _installedComponents[i];
                    if (c != null && c.Category == targetCat)
                    {
                        return "Hangar Part: " + c.Name;
                    }
                }
            }

            switch (metric)
            {
                case SensorMetric.SolarWatts: return "Hangar Part: Power Generation Slot";
                case SensorMetric.BatteryPercent: return "Hangar Part: Energy Storage Slot";
                case SensorMetric.SsdUsedPercent: return "Hangar Part: Comms & SSD Slot";
                default: return "Hangar Part: Bus Radiation Sensor";
            }
        }

        private void NotifyRulesChanged()
        {
            OnRulesChanged?.Invoke(GetActiveRules());
        }

        public void EnsureAutopilotUIHierarchy()
        {
            Transform existingRoot = transform.Find("AutopilotContentRoot");
            if (existingRoot != null)
            {
                BindExistingHierarchy(existingRoot);
                return;
            }

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TMP_FontAsset tmpFont = GetOrCreateTmpFont(legacyFont);

            // Modal Dark Backdrop
            GameObject rootObj = CreateUIObject("AutopilotContentRoot", transform);
            StretchFull(rootObj.GetComponent<RectTransform>());
            Image modalBg = rootObj.AddComponent<Image>();
            modalBg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(36, 36, 24, 24);
            rootLayout.spacing = 12f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // 1. TOP MODAL HEADER BAR
            GameObject topBar = CreateUIObject("HeaderBar", rootObj.transform);
            Image topBarBg = topBar.AddComponent<Image>();
            UITheme.ApplyPanelFrame(topBarBg, useDialogueBox: true);
            LayoutElement topLe = topBar.AddComponent<LayoutElement>();
            topLe.preferredHeight = 62f;

            HorizontalLayoutGroup topH = topBar.AddComponent<HorizontalLayoutGroup>();
            topH.padding = new RectOffset(18, 18, 8, 8);
            topH.spacing = 12f;
            topH.childAlignment = TextAnchor.MiddleLeft;
            topH.childControlWidth = true;
            topH.childControlHeight = true;
            topH.childForceExpandWidth = false;

            _headerStatusText = CreateText("HeaderStatus", topBar.transform, legacyFont,
                "CONSTRUCT-STYLE AUTOPILOT EVENT SHEET   •   CPU SLOTS UNLOCKED: 2 / 6", 18, FontStyle.Bold,
                new Color(1f, 0.88f, 0.42f, 1f), TextAnchor.MiddleLeft);
            LayoutElement hLe = _headerStatusText.gameObject.AddComponent<LayoutElement>();
            hLe.flexibleWidth = 1f;

            CreateButton(topBar.transform, "Btn_PresetEclipse", "+ Eclipse Rule",
                new Color(0.16f, 0.42f, 0.62f, 1f), 145f, 42f, legacyFont, ApplyEclipsePreset);

            CreateButton(topBar.transform, "Btn_PresetStorm", "+ Storm SafeMode",
                new Color(0.48f, 0.30f, 0.14f, 1f), 165f, 42f, legacyFont, ApplyStormSafeModePreset);

            CreateButton(topBar.transform, "Btn_ClearRules", "Reset All",
                new Color(0.38f, 0.18f, 0.22f, 1f), 105f, 42f, legacyFont, ClearAllRules);

            CreateButton(topBar.transform, "Btn_OpenDryRun", "▶ TEST IN A.S.T.R.A. / DRY RUN",
                new Color(0.14f, 0.52f, 0.34f, 1f), 255f, 42f, legacyFont, () => OnRequestOpenDryRun?.Invoke());

            CreateButton(topBar.transform, "Btn_CloseModal", "✕ CLOSE",
                new Color(0.55f, 0.16f, 0.20f, 1f), 120f, 42f, legacyFont, CloseModal);

            // 2. CONSTRUCT COLUMN HEADERS ROW
            GameObject colHeaderRow = CreateUIObject("ConstructColumnHeaders", rootObj.transform);
            Image colHeaderBg = colHeaderRow.AddComponent<Image>();
            UITheme.ApplyNameTab(colHeaderBg, new Color(0.75f, 0.85f, 1f, 0.95f));
            LayoutElement chLe = colHeaderRow.AddComponent<LayoutElement>();
            chLe.preferredHeight = 36f;

            HorizontalLayoutGroup chLayout = colHeaderRow.AddComponent<HorizontalLayoutGroup>();
            chLayout.padding = new RectOffset(16, 16, 4, 4);
            chLayout.spacing = 12f;
            chLayout.childAlignment = TextAnchor.MiddleLeft;
            chLayout.childControlWidth = true;
            chLayout.childControlHeight = true;
            chLayout.childForceExpandWidth = false;

            CreateColumnHeaderLabel(colHeaderRow.transform, legacyFont, "ACTIVE", 115f, new Color(0.75f, 0.88f, 1f, 1f));
            CreateColumnHeaderLabel(colHeaderRow.transform, legacyFont, "1. SELECT OBJECT / SENSOR (FROM HANGAR)", 340f, new Color(0.55f, 0.85f, 1f, 1f));
            CreateColumnHeaderLabel(colHeaderRow.transform, legacyFont, "2. IF CONDITION (< / > VALUE)", 285f, new Color(1f, 0.88f, 0.45f, 1f));
            CreateColumnHeaderLabel(colHeaderRow.transform, legacyFont, "3. THEN (CHANGE SUBSYSTEM)", 365f, new Color(0.50f, 0.95f, 0.68f, 1f));
            CreateColumnHeaderLabel(colHeaderRow.transform, legacyFont, "4. ELSE (OTHERWISE)", 365f, new Color(0.98f, 0.75f, 0.45f, 1f));

            // 3. SIX CONSTRUCT-STYLE EVENT ROWS
            _rowWidgets.Clear();
            for (int i = 0; i < MaxTotalRuleRows; i++)
            {
                _rowWidgets.Add(BuildRuleRow(i, rootObj.transform, legacyFont, tmpFont));
            }
        }

        private static void CreateColumnHeaderLabel(Transform parent, Font font, string text, float width, Color color)
        {
            Text lbl = CreateText("ColHdr_" + text, parent, font, text, 13, FontStyle.Bold, color, TextAnchor.MiddleLeft);
            LayoutElement le = lbl.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
        }

        private void BindExistingHierarchy(Transform root)
        {
            _headerStatusText = FindChild<Text>(root, "HeaderBar/HeaderStatus");

            Button btnEclipse = FindChild<Button>(root, "HeaderBar/Btn_PresetEclipse");
            if (btnEclipse != null)
            {
                btnEclipse.onClick.RemoveAllListeners();
                btnEclipse.onClick.AddListener(ApplyEclipsePreset);
            }

            Button btnStorm = FindChild<Button>(root, "HeaderBar/Btn_PresetStorm");
            if (btnStorm != null)
            {
                btnStorm.onClick.RemoveAllListeners();
                btnStorm.onClick.AddListener(ApplyStormSafeModePreset);
            }

            Button btnClear = FindChild<Button>(root, "HeaderBar/Btn_ClearRules");
            if (btnClear != null)
            {
                btnClear.onClick.RemoveAllListeners();
                btnClear.onClick.AddListener(ClearAllRules);
            }

            Button btnOpenDry = FindChild<Button>(root, "HeaderBar/Btn_OpenDryRun");
            if (btnOpenDry != null)
            {
                btnOpenDry.onClick.RemoveAllListeners();
                btnOpenDry.onClick.AddListener(() => OnRequestOpenDryRun?.Invoke());
            }

            Button btnClose = FindChild<Button>(root, "HeaderBar/Btn_CloseModal");
            if (btnClose != null)
            {
                btnClose.onClick.RemoveAllListeners();
                btnClose.onClick.AddListener(CloseModal);
            }

            _rowWidgets.Clear();
            for (int i = 0; i < MaxTotalRuleRows; i++)
            {
                Transform row = root.Find("RuleRow_" + i);
                if (row == null) continue;

                var w = new RuleRowWidgets
                {
                    RuleId = "rule_" + (i + 1),
                    SlotIndex = i,
                    RowRoot = row.gameObject,
                    RowBackground = row.GetComponent<Image>(),
                    EnabledToggle = FindChild<Toggle>(row, "ControlsRow/Toggle_Enabled"),
                    MetricDropdown = FindChild<TMP_Dropdown>(row, "ControlsRow/ObjectBlock/Dropdown_Metric"),
                    InstalledObjectSubText = FindChild<Text>(row, "ControlsRow/ObjectBlock/InstalledPartSubText"),
                    OperatorDropdown = FindChild<TMP_Dropdown>(row, "ControlsRow/ConditionBlock/CondInnerRow/Dropdown_Operator")
                                       ?? FindChild<TMP_Dropdown>(row, "ControlsRow/ConditionBlock/Dropdown_Operator"),
                    ThresholdInput = FindChild<InputField>(row, "ControlsRow/ConditionBlock/CondInnerRow/Input_Threshold")
                                     ?? FindChild<InputField>(row, "ControlsRow/ConditionBlock/Input_Threshold"),
                    ThenDropdown = FindChild<TMP_Dropdown>(row, "ControlsRow/ThenBlock/Dropdown_Then"),
                    ElseDropdown = FindChild<TMP_Dropdown>(row, "ControlsRow/ElseBlock/Dropdown_Else"),
                    LockedOverlayText = FindChild<Text>(row, "LockedOverlay/LockedText")
                };

                WireRowCallbacks(i, w);
                _rowWidgets.Add(w);
            }
        }

        private RuleRowWidgets BuildRuleRow(int index, Transform parent, Font legacyFont, TMP_FontAsset tmpFont)
        {
            GameObject rowObj = CreateUIObject("RuleRow_" + index, parent);
            Image rowBg = rowObj.AddComponent<Image>();
            UITheme.ApplyPanelFrame(rowBg, useDialogueBox: true);

            LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 122f;
            rowLe.flexibleHeight = 1f;

            // Main horizontal Construct Event Block container
            GameObject controlsRow = CreateUIObject("ControlsRow", rowObj.transform);
            StretchFull(controlsRow.GetComponent<RectTransform>());

            HorizontalLayoutGroup hLayout = controlsRow.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(16, 16, 10, 10);
            hLayout.spacing = 12f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;

            // BLOCK 0: Toggle Enabled ("EVENT #1")
            Toggle enabledToggle = CreateRuleToggle(controlsRow.transform, "Toggle_Enabled",
                string.Format("EVENT #{0}", index + 1), legacyFont, 115f);

            // BLOCK 1: CONSTRUCT OBJECT / SENSOR SELECTOR (TMP_Dropdown #1 + Installed Hangar Part subtitle)
            GameObject objectBlock = CreateConstructSubBlock("ObjectBlock", controlsRow.transform,
                new Color(0.06f, 0.11f, 0.20f, 0.95f), 340f);

            CreateText("BlockTag", objectBlock.transform, legacyFont,
                "OBJECT / SENSOR METRIC (IF):", 11, FontStyle.Bold,
                new Color(0.55f, 0.85f, 1f, 1f), TextAnchor.MiddleLeft);

            TMP_Dropdown metricDd = CreateTmpDropdown(objectBlock.transform, "Dropdown_Metric",
                MetricOptions, tmpFont, 316f, 38f);

            Text installedPartSubText = CreateText("InstalledPartSubText", objectBlock.transform, legacyFont,
                "Hangar Part: Basic 3U Solar Panels", 12, FontStyle.Italic,
                new Color(0.75f, 0.92f, 0.82f, 1f), TextAnchor.MiddleLeft);

            // BLOCK 2: CONDITION (ComparisonOp TMP_Dropdown #2 + Threshold InputField)
            GameObject condBlock = CreateConstructSubBlock("ConditionBlock", controlsRow.transform,
                new Color(0.06f, 0.11f, 0.20f, 0.95f), 285f);

            CreateText("BlockTag", condBlock.transform, legacyFont,
                "COMPARISON & THRESHOLD:", 11, FontStyle.Bold,
                new Color(1f, 0.88f, 0.45f, 1f), TextAnchor.MiddleLeft);

            GameObject condInnerRow = CreateUIObject("CondInnerRow", condBlock.transform);
            LayoutElement cirLe = condInnerRow.AddComponent<LayoutElement>();
            cirLe.preferredHeight = 40f;
            HorizontalLayoutGroup cirH = condInnerRow.AddComponent<HorizontalLayoutGroup>();
            cirH.spacing = 8f;
            cirH.childControlWidth = true;
            cirH.childControlHeight = true;
            cirH.childForceExpandWidth = false;

            TMP_Dropdown opDd = CreateTmpDropdown(condInnerRow.transform, "Dropdown_Operator",
                OperatorOptions, tmpFont, 165f, 38f);

            InputField thresholdInput = CreateThresholdInputField(condInnerRow.transform, "Input_Threshold",
                "0", legacyFont, 88f, 38f);

            CreateText("CondHint", condBlock.transform, legacyFont,
                "Evaluated every orbital tick", 11, FontStyle.Normal,
                new Color(0.68f, 0.76f, 0.88f, 0.85f), TextAnchor.MiddleLeft);

            // BLOCK 3: THEN ACTION (TMP_Dropdown #3 — Target Object & Action)
            GameObject thenBlock = CreateConstructSubBlock("ThenBlock", controlsRow.transform,
                new Color(0.06f, 0.16f, 0.14f, 0.95f), 365f);

            CreateText("BlockTag", thenBlock.transform, legacyFont,
                "THEN DO (TARGET SUBSYSTEM):", 11, FontStyle.Bold,
                new Color(0.50f, 0.95f, 0.68f, 1f), TextAnchor.MiddleLeft);

            TMP_Dropdown thenDd = CreateTmpDropdown(thenBlock.transform, "Dropdown_Then",
                ActionOptions, tmpFont, 340f, 38f);

            CreateText("ThenHint", thenBlock.transform, legacyFont,
                "Executes when IF condition is TRUE", 11, FontStyle.Normal,
                new Color(0.65f, 0.90f, 0.75f, 0.85f), TextAnchor.MiddleLeft);

            // BLOCK 4: ELSE ACTION (TMP_Dropdown #4 — Fallback Action)
            GameObject elseBlock = CreateConstructSubBlock("ElseBlock", controlsRow.transform,
                new Color(0.16f, 0.12f, 0.07f, 0.95f), 365f);
            LayoutElement elseBlockLe = elseBlock.GetComponent<LayoutElement>();
            elseBlockLe.flexibleWidth = 1f;

            CreateText("BlockTag", elseBlock.transform, legacyFont,
                "ELSE DO (WHEN CONDITION IS FALSE):", 11, FontStyle.Bold,
                new Color(0.98f, 0.78f, 0.45f, 1f), TextAnchor.MiddleLeft);

            TMP_Dropdown elseDd = CreateTmpDropdown(elseBlock.transform, "Dropdown_Else",
                ActionOptions, tmpFont, 340f, 38f);

            CreateText("ElseHint", elseBlock.transform, legacyFont,
                "Restores normal state when FALSE", 11, FontStyle.Normal,
                new Color(0.92f, 0.80f, 0.62f, 0.85f), TextAnchor.MiddleLeft);

            // Locked Slot Overlay (shown when slot >= AutopilotSlotsUnlocked)
            GameObject lockedOverlay = CreateUIObject("LockedOverlay", rowObj.transform);
            StretchFull(lockedOverlay.GetComponent<RectTransform>());
            Image lockBg = lockedOverlay.AddComponent<Image>();
            lockBg.color = new Color(0.03f, 0.05f, 0.10f, 0.93f);

            Text lockedTxt = CreateText("LockedText", lockedOverlay.transform, legacyFont,
                "EVENT SLOT LOCKED — Upgrade Onboard CPU in R&D Tech Tree", 16, FontStyle.Bold,
                new Color(0.78f, 0.86f, 0.98f, 0.95f), TextAnchor.MiddleCenter);
            StretchFull(lockedTxt.GetComponent<RectTransform>());

            var widgets = new RuleRowWidgets
            {
                RuleId = "rule_" + (index + 1),
                SlotIndex = index,
                RowRoot = rowObj,
                RowBackground = rowBg,
                EnabledToggle = enabledToggle,
                MetricDropdown = metricDd,
                InstalledObjectSubText = installedPartSubText,
                OperatorDropdown = opDd,
                ThresholdInput = thresholdInput,
                ThenDropdown = thenDd,
                ElseDropdown = elseDd,
                LockedOverlayText = lockedTxt
            };

            WireRowCallbacks(index, widgets);
            return widgets;
        }

        private static GameObject CreateConstructSubBlock(string name, Transform parent, Color bgColor, float width)
        {
            GameObject block = CreateUIObject(name, parent);
            Image bg = block.AddComponent<Image>();
            UITheme.ApplySlotFrame(bg, selected: false);

            LayoutElement le = block.AddComponent<LayoutElement>();
            le.preferredWidth = width;

            VerticalLayoutGroup v = block.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(10, 10, 6, 6);
            v.spacing = 4f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            return block;
        }

        private void WireRowCallbacks(int index, RuleRowWidgets w)
        {
            int captured = index;
            if (w.EnabledToggle != null)
            {
                w.EnabledToggle.onValueChanged.RemoveAllListeners();
                w.EnabledToggle.onValueChanged.AddListener(_ => OnRowChanged(captured));
            }
            if (w.MetricDropdown != null)
            {
                w.MetricDropdown.onValueChanged.RemoveAllListeners();
                w.MetricDropdown.onValueChanged.AddListener(_ => OnRowChanged(captured));
            }
            if (w.OperatorDropdown != null)
            {
                w.OperatorDropdown.onValueChanged.RemoveAllListeners();
                w.OperatorDropdown.onValueChanged.AddListener(_ => OnRowChanged(captured));
            }
            if (w.ThresholdInput != null)
            {
                w.ThresholdInput.contentType = InputField.ContentType.Standard;
                w.ThresholdInput.onValidateInput = null;
                w.ThresholdInput.onEndEdit.RemoveAllListeners();
                w.ThresholdInput.onEndEdit.AddListener(_ => OnThresholdEndEdit(captured));
                w.ThresholdInput.onValueChanged.RemoveAllListeners();
                w.ThresholdInput.onValueChanged.AddListener(_ => OnRowChanged(captured));
            }
            if (w.ThenDropdown != null)
            {
                w.ThenDropdown.onValueChanged.RemoveAllListeners();
                w.ThenDropdown.onValueChanged.AddListener(_ => OnRowChanged(captured));
            }
            if (w.ElseDropdown != null)
            {
                w.ElseDropdown.onValueChanged.RemoveAllListeners();
                w.ElseDropdown.onValueChanged.AddListener(_ => OnRowChanged(captured));
            }
        }

        // --- UI Factory Helpers (with full TMP_Dropdown template builder) ---

        private static bool IsPercentageMetric(SensorMetric metric)
        {
            return metric == SensorMetric.BatteryPercent || metric == SensorMetric.SsdUsedPercent;
        }

        private static bool TryNormalizeThresholdInput(string raw, bool isPercentageMetric, out float result)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                result = 0f;
                return true;
            }

            int separatorCount = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (ch >= '0' && ch <= '9')
                {
                    continue;
                }

                if (ch == '.' || ch == ',')
                {
                    separatorCount++;
                    if (separatorCount <= 1)
                    {
                        continue;
                    }
                }

                // Negative sign or any unsuitable character resets the value to 0
                result = 0f;
                return true;
            }

            string normalized = raw.Replace(',', '.');
            if (normalized == ".")
            {
                result = 0f;
                return false;
            }

            float parsed;
            if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                float.IsNaN(parsed) ||
                float.IsInfinity(parsed) ||
                parsed < 0f)
            {
                result = 0f;
                return true;
            }

            if (isPercentageMetric && parsed > 100f)
            {
                result = 99f;
                return true;
            }

            result = parsed;
            return false;
        }

        private void OnThresholdEndEdit(int index)
        {
            if (_suppressCallbacks || index < 0 || index >= _rowWidgets.Count || index >= _rules.Count) return;

            RuleRowWidgets w = _rowWidgets[index];
            AutopilotRule r = _rules[index];
            if (w == null || r == null || w.ThresholdInput == null) return;

            float normalizedValue;
            TryNormalizeThresholdInput(w.ThresholdInput.text ?? string.Empty, IsPercentageMetric(r.Metric), out normalizedValue);
            r.Threshold = normalizedValue;

            _suppressCallbacks = true;
            string formatted = r.Threshold.ToString("0.#", CultureInfo.InvariantCulture);
            w.ThresholdInput.SetTextWithoutNotify(formatted);
            _suppressCallbacks = false;

            NotifyRulesChanged();
        }

        private static TMP_FontAsset GetOrCreateTmpFont(Font legacyFont)
        {
            if (_cachedTmpFont != null) return _cachedTmpFont;

            if (TMP_Settings.defaultFontAsset != null)
            {
                _cachedTmpFont = TMP_Settings.defaultFontAsset;
                return _cachedTmpFont;
            }

            TMP_FontAsset loaded = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (loaded != null)
            {
                _cachedTmpFont = loaded;
                return _cachedTmpFont;
            }

            if (legacyFont != null)
            {
                _cachedTmpFont = TMP_FontAsset.CreateFontAsset(legacyFont);
            }
            return _cachedTmpFont;
        }

        private static Toggle CreateRuleToggle(Transform parent, string name, string label, Font font, float width)
        {
            GameObject toggleObj = CreateUIObject(name, parent);
            Image bg = toggleObj.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.11f, 0.20f, 0.95f);

            LayoutElement le = toggleObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;

            HorizontalLayoutGroup h = toggleObj.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(8, 8, 8, 8);
            h.spacing = 6f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;

            GameObject boxObj = CreateUIObject("Checkbox", toggleObj.transform);
            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.12f, 0.22f, 0.36f, 1f);
            LayoutElement boxLe = boxObj.AddComponent<LayoutElement>();
            boxLe.preferredWidth = 24f;
            boxLe.preferredHeight = 24f;
            boxLe.minWidth = 24f;
            boxLe.minHeight = 24f;

            GameObject checkObj = CreateUIObject("Checkmark", boxObj.transform);
            StretchFull(checkObj.GetComponent<RectTransform>());
            Image checkImg = checkObj.AddComponent<Image>();
            checkImg.color = new Color(0.25f, 0.92f, 0.52f, 1f);

            Text lbl = CreateText("Label", toggleObj.transform, font, label, 13, FontStyle.Bold,
                new Color(0.85f, 0.95f, 1f, 1f), TextAnchor.MiddleLeft);
            LayoutElement lblLe = lbl.gameObject.AddComponent<LayoutElement>();
            lblLe.flexibleWidth = 1f;

            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;

            return toggle;
        }

        private static InputField CreateThresholdInputField(Transform parent, string name, string defaultVal, Font font, float width, float height)
        {
            GameObject inputObj = CreateUIObject(name, parent);
            Image bg = inputObj.AddComponent<Image>();
            UITheme.ApplySlotFrame(bg, selected: false);

            LayoutElement le = inputObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            GameObject textObj = CreateUIObject("Text", inputObj.transform);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(6f, 2f);
            textRt.offsetMax = new Vector2(-6f, -2f);

            Text txt = textObj.AddComponent<Text>();
            txt.font = font;
            txt.text = defaultVal;
            txt.fontSize = 15;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(1f, 0.92f, 0.45f, 1f);
            txt.alignment = TextAnchor.MiddleCenter;

            InputField input = inputObj.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = txt;
            input.contentType = InputField.ContentType.Standard;
            input.text = defaultVal;

            return input;
        }

        private static TMP_Dropdown CreateTmpDropdown(Transform parent, string name, List<string> options, TMP_FontAsset tmpFont, float width, float height)
        {
            GameObject ddObj = CreateUIObject(name, parent);
            Image ddBg = ddObj.AddComponent<Image>();
            UITheme.ApplyNameTab(ddBg, new Color(0.85f, 0.94f, 1f, 1f));

            LayoutElement le = ddObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            TMP_Dropdown dropdown = ddObj.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = ddBg;

            // Caption Label
            GameObject labelObj = CreateUIObject("Label", ddObj.transform);
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(10f, 2f);
            labelRt.offsetMax = new Vector2(-24f, -2f);

            TextMeshProUGUI captionTmp = labelObj.AddComponent<TextMeshProUGUI>();
            if (tmpFont != null) captionTmp.font = tmpFont;
            captionTmp.fontSize = 14f;
            captionTmp.color = Color.white;
            captionTmp.alignment = TextAlignmentOptions.MidlineLeft;
            captionTmp.enableWordWrapping = false;
            captionTmp.overflowMode = TextOverflowModes.Ellipsis;

            // Arrow indicator
            GameObject arrowObj = CreateUIObject("Arrow", ddObj.transform);
            RectTransform arrowRt = arrowObj.GetComponent<RectTransform>();
            arrowRt.anchorMin = new Vector2(1f, 0.5f);
            arrowRt.anchorMax = new Vector2(1f, 0.5f);
            arrowRt.sizeDelta = new Vector2(18f, 18f);
            arrowRt.anchoredPosition = new Vector2(-12f, 0f);
            TextMeshProUGUI arrowTmp = arrowObj.AddComponent<TextMeshProUGUI>();
            if (tmpFont != null) arrowTmp.font = tmpFont;
            arrowTmp.text = "▼";
            arrowTmp.fontSize = 11f;
            arrowTmp.color = new Color(0.65f, 0.85f, 1f, 1f);
            arrowTmp.alignment = TextAlignmentOptions.Center;

            // Dropdown Template
            GameObject templateObj = CreateUIObject("Template", ddObj.transform);
            RectTransform templateRt = templateObj.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(1f, 0f);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = new Vector2(0f, 2f);
            templateRt.sizeDelta = new Vector2(0f, 220f);

            Image templateBg = templateObj.AddComponent<Image>();
            templateBg.color = new Color(0.06f, 0.11f, 0.20f, 0.99f);

            ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportObj = CreateUIObject("Viewport", templateObj.transform);
            StretchFull(viewportObj.GetComponent<RectTransform>());
            Image vpImg = viewportObj.AddComponent<Image>();
            vpImg.color = Color.white;
            Mask vpMask = viewportObj.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;

            GameObject contentObj = CreateUIObject("Content", viewportObj.transform);
            RectTransform contentRt = contentObj.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 32f);

            GameObject itemObj = CreateUIObject("Item", contentObj.transform);
            RectTransform itemRt = itemObj.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0f, 0.5f);
            itemRt.anchorMax = new Vector2(1f, 0.5f);
            itemRt.sizeDelta = new Vector2(0f, 30f);

            Image itemBg = itemObj.AddComponent<Image>();
            itemBg.color = new Color(0.12f, 0.20f, 0.34f, 1f);

            Toggle itemToggle = itemObj.AddComponent<Toggle>();
            itemToggle.targetGraphic = itemBg;

            GameObject itemLabelObj = CreateUIObject("Item Label", itemObj.transform);
            RectTransform itemLabelRt = itemLabelObj.GetComponent<RectTransform>();
            itemLabelRt.anchorMin = Vector2.zero;
            itemLabelRt.anchorMax = Vector2.one;
            itemLabelRt.offsetMin = new Vector2(10f, 1f);
            itemLabelRt.offsetMax = new Vector2(-6f, -1f);

            TextMeshProUGUI itemLabelTmp = itemLabelObj.AddComponent<TextMeshProUGUI>();
            if (tmpFont != null) itemLabelTmp.font = tmpFont;
            itemLabelTmp.fontSize = 14f;
            itemLabelTmp.color = Color.white;
            itemLabelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            itemLabelTmp.enableWordWrapping = false;

            scrollRect.content = contentRt;
            scrollRect.viewport = viewportObj.GetComponent<RectTransform>();

            dropdown.template = templateRt;
            dropdown.captionText = captionTmp;
            dropdown.itemText = itemLabelTmp;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            templateObj.SetActive(false);
            return dropdown;
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
            Button btn = btnObj.AddComponent<Button>();

            Color tint = Color.white;
            if (name.Contains("Close") || name.Contains("Clear")) tint = UITheme.ButtonTintDanger;
            else if (name.Contains("DryRun")) tint = UITheme.ButtonTintSuccess;
            else if (name.Contains("Preset")) tint = UITheme.ButtonTintMuted;
            UITheme.ApplyButtonStyle(btn, img, tint);

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
