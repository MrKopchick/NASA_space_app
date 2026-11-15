using System;
using System.Collections.Generic;
using System.Globalization;
using AstraLink.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstraLink.UI.Workshop
{
    /// <summary>
    /// Scratch / ArduBlock-style Visual Block Autopilot Editor (2-6 CPU slots depending on AgencyState.AutopilotSlotsUnlocked).
    /// Left 3 Categories -> Center Pop-out Block Drawer -> Right C-Block Workspace with Drag &amp; Drop + Click-to-Snap.
    /// Compiles 1:1 to List&lt;AutopilotRule&gt; so A.S.T.R.A. Dry Run and Flight Simulation stay 100% compatible.
    /// </summary>
    public class AutopilotUI : MonoBehaviour
    {
        public const int MaxTotalRuleRows = 6;
        private const string LayoutVersionMarker = "ScratchAutopilotV5Marker";

        private class RuleSlotState
        {
            public bool HasSensor;
            public bool HasOperator;
            public bool HasThenAction;
            public bool HasElseAction;
        }

        private enum BlockCategory
        {
            Control = 0,
            Condition = 1,
            Action = 2
        }

        private enum DragPayloadKind
        {
            ControlIfElse,
            SensorMetric,
            ComparisonOp,
            SubsystemAction
        }

        private enum DropSocketKind
        {
            WorkspaceAddRule,
            SensorSlot,
            OperatorSlot,
            ThenSlot,
            ElseSlot
        }

        private struct DragPayload
        {
            public DragPayloadKind Kind;
            public SensorMetric Metric;
            public ComparisonOp Operator;
            public AutopilotAction Action;
            public string Label;
            public Color Tint;
            public Sprite Icon;
        }

        private class WorkspaceBlockWidgets
        {
            public int SlotIndex;
            public string RuleId;
            public GameObject Root;
            public Image FrameHighlight;
            public Button SelectButton;
            public Button EnableToggleButton;
            public Text EnableToggleText;
            public Image EnableToggleBg;

            public Image SensorSlotBg;
            public Image SensorIcon;
            public Text SensorText;

            public Image OpSlotBg;
            public Text OpText;

            public InputField ThresholdInput;
            public Text ThresholdUnitText;
            public Text HangarPartHintText;

            public Image ThenSlotBg;
            public Image ThenIcon;
            public Text ThenText;

            public Image ElseSlotBg;
            public Image ElseIcon;
            public Text ElseText;

            public Text StatusBadgeText;
            public Text SummaryFooterText;

            public Button MoveUpBtn;
            public Button MoveDownBtn;
            public Button DeleteBtn;
        }

        private AgencyState _agencyState;
        private List<SatelliteComponent> _installedComponents = new List<SatelliteComponent>();
        private readonly List<AutopilotRule> _rules = new List<AutopilotRule>();
        private readonly RuleSlotState[] _slotStates = new RuleSlotState[MaxTotalRuleRows];
        private readonly List<WorkspaceBlockWidgets> _blockWidgets = new List<WorkspaceBlockWidgets>();

        private int _placedBlockCount = 0;
        private int _selectedRuleIndex = -1;
        private BlockCategory _activeCategory = BlockCategory.Control;
        private HashSet<string> _lastTriggeredIds = new HashSet<string>();

        private Text _headerStatusText;
        private Text _slotsRemainingBadgeText;
        private Text _drawerHeaderText;
        private Text _drawerSubText;
        private Transform _drawerContentRoot;
        private Transform _workspaceListRoot;
        private GameObject _workspaceDropPrompt;
        private Text _workspaceDropPromptText;

        private readonly Image[] _categoryTabImages = new Image[3];
        private readonly Text[] _categoryTabLabels = new Text[3];
        private readonly Text[] _categoryTabSubLabels = new Text[3];

        private GameObject _dragGhost;
        private RectTransform _dragGhostRt;
        private Canvas _rootCanvas;
        private bool _suppressCallbacks;

        public event Action<List<AutopilotRule>> OnRulesChanged;
        public event Action OnRequestOpenDryRun;

        // =====================================================================
        // PUBLIC API (100% compatible with BuilderPanelUI, DryRunUI & GameManager)
        // =====================================================================

        public void Initialize(AgencyState agencyState, List<SatelliteComponent> installedComponents = null)
        {
            _agencyState = agencyState ?? (GameManager.Instance != null ? GameManager.Instance.CurrentAgencyState : new AgencyState());
            if (installedComponents != null)
            {
                _installedComponents = installedComponents;
            }

            EnsureDefaultRulesList();
            SyncPlacedCountFromRules();
            EnsureAutopilotUIHierarchy();
            RebuildWorkspaceAndDrawer();
        }

        public void SetInstalledComponents(List<SatelliteComponent> installedComponents)
        {
            _installedComponents = installedComponents ?? new List<SatelliteComponent>();
            RefreshHangarPartHints();
        }

        public void CloseModal()
        {
            ClearDragGhost();
            gameObject.SetActive(false);
        }

        [ContextMenu("Generate Autopilot UI In Scene")]
        public void BuildUIInEditor()
        {
            if (_agencyState == null) _agencyState = new AgencyState();
            EnsureDefaultRulesList();
            SyncPlacedCountFromRules();

            Transform existing = transform.Find("AutopilotContentRoot");
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            EnsureAutopilotUIHierarchy();
            RebuildWorkspaceAndDrawer();
            Debug.Log("[AutopilotUI] Scratch-style Block Autopilot UI generated!");
        }

        public void RefreshSlotLocks(AgencyState state = null, List<SatelliteComponent> installedComponents = null)
        {
            if (state != null) _agencyState = state;
            if (installedComponents != null) _installedComponents = installedComponents;

            EnsureDefaultRulesList();
            _placedBlockCount = Mathf.Clamp(_placedBlockCount, 0, GetUnlockedSlotsCount());
            EnsureAutopilotUIHierarchy();
            RebuildWorkspaceAndDrawer();
        }

        public List<AutopilotRule> GetActiveRules()
        {
            EnsureDefaultRulesList();
            int unlocked = GetUnlockedSlotsCount();
            int count = Mathf.Clamp(_placedBlockCount, 0, unlocked);
            var result = new List<AutopilotRule>(count);

            for (int i = 0; i < count && i < _rules.Count; i++)
            {
                AutopilotRule src = _rules[i];
                RuleSlotState st = GetOrCreateSlotState(i);
                bool ready = src.Enabled && st.HasSensor && st.HasOperator;
                result.Add(new AutopilotRule
                {
                    RuleId = "rule_" + (i + 1),
                    Enabled = ready,
                    Metric = src.Metric,
                    Operator = src.Operator,
                    Threshold = src.Threshold,
                    ThenAction = st.HasThenAction ? src.ThenAction : AutopilotAction.None,
                    ElseAction = st.HasElseAction ? src.ElseAction : AutopilotAction.None
                });
            }
            return result;
        }

        public void HighlightTriggeredRules(List<string> triggeredRuleIds)
        {
            _lastTriggeredIds.Clear();
            if (triggeredRuleIds != null)
            {
                for (int i = 0; i < triggeredRuleIds.Count; i++)
                {
                    _lastTriggeredIds.Add(triggeredRuleIds[i]);
                }
            }

            for (int i = 0; i < _blockWidgets.Count; i++)
            {
                WorkspaceBlockWidgets w = _blockWidgets[i];
                if (w == null || w.FrameHighlight == null) continue;

                bool isSelected = i == _selectedRuleIndex;
                bool isEnabled = i < _rules.Count && _rules[i].Enabled;
                bool isTriggered = isEnabled && _lastTriggeredIds.Contains(w.RuleId);

                if (isTriggered)
                {
                    w.FrameHighlight.color = new Color(0.45f, 1f, 0.68f, 0.98f);
                }
                else if (isSelected)
                {
                    w.FrameHighlight.color = new Color(0.84f, 0.96f, 1f, 0.98f);
                }
                else if (isEnabled)
                {
                    w.FrameHighlight.color = new Color(0.64f, 0.84f, 1f, 0.92f);
                }
                else
                {
                    w.FrameHighlight.color = new Color(0.44f, 0.50f, 0.62f, 0.68f);
                }
            }
        }

        public void ApplyEclipsePreset()
        {
            EnsureDefaultRulesList();
            int unlocked = GetUnlockedSlotsCount();
            if (unlocked <= 0) return;

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
            SetSlotStateFilled(0, true);
            if (_placedBlockCount < 1) _placedBlockCount = 1;
            _selectedRuleIndex = 0;

            RebuildWorkspaceAndDrawer();
            NotifyRulesChanged();
        }

        public void ApplyStormSafeModePreset()
        {
            EnsureDefaultRulesList();
            int unlocked = GetUnlockedSlotsCount();
            int targetIdx = unlocked > 1 ? 1 : 0;

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
            SetSlotStateFilled(targetIdx, true);
            if (_placedBlockCount <= targetIdx) _placedBlockCount = targetIdx + 1;
            _selectedRuleIndex = targetIdx;

            RebuildWorkspaceAndDrawer();
            NotifyRulesChanged();
        }

        public void ClearAllRules()
        {
            _rules.Clear();
            EnsureDefaultRulesList();
            for (int i = 0; i < _rules.Count; i++)
            {
                _rules[i].Enabled = false;
                SetSlotStateFilled(i, false);
            }
            _placedBlockCount = 0;
            _selectedRuleIndex = -1;

            RebuildWorkspaceAndDrawer();
            NotifyRulesChanged();
        }

        private void OnDisable()
        {
            ClearDragGhost();
        }

        // =====================================================================
        // DATA HELPERS
        // =====================================================================

        private int GetUnlockedSlotsCount()
        {
            if (_agencyState == null && GameManager.Instance != null)
            {
                _agencyState = GameManager.Instance.CurrentAgencyState;
            }
            int unlocked = _agencyState != null ? _agencyState.AutopilotSlotsUnlocked : 2;
            return Mathf.Clamp(unlocked, 2, MaxTotalRuleRows);
        }

        private RuleSlotState GetOrCreateSlotState(int index)
        {
            int clamped = Mathf.Clamp(index, 0, MaxTotalRuleRows - 1);
            if (_slotStates[clamped] == null)
            {
                _slotStates[clamped] = new RuleSlotState();
            }
            return _slotStates[clamped];
        }

        private void SetSlotStateFilled(int index, bool filled)
        {
            RuleSlotState st = GetOrCreateSlotState(index);
            st.HasSensor = filled;
            st.HasOperator = filled;
            st.HasThenAction = filled;
            st.HasElseAction = filled;
        }

        private void EnsureDefaultRulesList()
        {
            while (_rules.Count < MaxTotalRuleRows)
            {
                int idx = _rules.Count;
                _rules.Add(CreateDefaultRuleForSlot(idx));
            }
            for (int i = 0; i < MaxTotalRuleRows; i++)
            {
                if (_slotStates[i] == null) _slotStates[i] = new RuleSlotState();
            }
            for (int i = 0; i < _rules.Count; i++)
            {
                _rules[i].RuleId = "rule_" + (i + 1);
            }
        }

        private void SyncPlacedCountFromRules()
        {
            int unlocked = GetUnlockedSlotsCount();
            int highestEnabledPlusOne = 0;
            for (int i = 0; i < unlocked && i < _rules.Count; i++)
            {
                if (_rules[i] != null && _rules[i].Enabled)
                {
                    highestEnabledPlusOne = i + 1;
                    SetSlotStateFilled(i, true);
                }
                else
                {
                    SetSlotStateFilled(i, false);
                }
            }
            _placedBlockCount = Mathf.Clamp(highestEnabledPlusOne, 0, unlocked);
            _selectedRuleIndex = _placedBlockCount > 0
                ? Mathf.Clamp(_selectedRuleIndex, 0, _placedBlockCount - 1)
                : -1;
        }

        private static AutopilotRule CreateDefaultRuleForSlot(int index)
        {
            if (index == 0)
            {
                return new AutopilotRule
                {
                    RuleId = "rule_1",
                    Enabled = false,
                    Metric = SensorMetric.SolarWatts,
                    Operator = ComparisonOp.LessThan,
                    Threshold = 5f,
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
                    Threshold = 50f,
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
                    Threshold = 60f,
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
                Threshold = 20f,
                ThenAction = AutopilotAction.ScienceOff,
                ElseAction = AutopilotAction.None
            };
        }

        private void NotifyRulesChanged()
        {
            OnRulesChanged?.Invoke(GetActiveRules());
        }

        // =====================================================================
        // HIERARCHY SETUP (Shared PixelUITheme Assets)
        // =====================================================================

        public void EnsureAutopilotUIHierarchy()
        {
            PixelUITheme.EnsureAssetsLoaded();
            _rootCanvas = GetComponentInParent<Canvas>();

            Transform existingRoot = transform.Find("AutopilotContentRoot");
            if (existingRoot != null)
            {
                if (existingRoot.Find(LayoutVersionMarker) != null)
                {
                    BindExistingHierarchy(existingRoot);
                    if (_drawerContentRoot != null && _workspaceListRoot != null)
                    {
                        return;
                    }
                }

                // Disable & detach immediately so repeated same-frame calls never re-find a dying object
                existingRoot.gameObject.SetActive(false);
                existingRoot.SetParent(null, false);
                if (Application.isPlaying)
                    Destroy(existingRoot.gameObject);
                else
                    DestroyImmediate(existingRoot.gameObject);
            }

            // Also sweep any leftover stale AutopilotContentRoot children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "AutopilotContentRoot")
                {
                    child.gameObject.SetActive(false);
                    child.SetParent(null, false);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            Font headerFont = PixelUITheme.HeaderFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font bodyFont = PixelUITheme.BodyFont ?? headerFont;

            // Modal Dark Backdrop
            GameObject rootObj = CreateUIObject("AutopilotContentRoot", transform);
            StretchFull(rootObj.GetComponent<RectTransform>());
            Image modalBg = rootObj.AddComponent<Image>();
            modalBg.color = new Color(0.02f, 0.04f, 0.09f, 0.94f);

            GameObject marker = CreateUIObject(LayoutVersionMarker, rootObj.transform);
            marker.AddComponent<LayoutElement>().ignoreLayout = true;

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(28, 28, 18, 20);
            rootLayout.spacing = 12f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // 1. TOP HEADER BAR (Fixed compact 60px height, never stretches vertically)
            GameObject topBar = CreateSlicedPanel("HeaderBar", rootObj.transform,
                PixelUITheme.PanelFrameSprite, new Color(0.84f, 0.92f, 1f, 0.96f), 60f);
            LayoutElement topLe = topBar.GetComponent<LayoutElement>();
            topLe.minHeight = 60f;
            topLe.preferredHeight = 60f;
            topLe.flexibleHeight = 0f;

            HorizontalLayoutGroup topH = topBar.AddComponent<HorizontalLayoutGroup>();
            topH.padding = new RectOffset(20, 20, 10, 10);
            topH.spacing = 8f;
            topH.childAlignment = TextAnchor.MiddleLeft;
            topH.childControlWidth = true;
            topH.childControlHeight = true;
            topH.childForceExpandWidth = false;
            topH.childForceExpandHeight = false;

            Sprite chipSprite = PixelUITheme.ResolvePartIconSprite("chip");
            if (chipSprite != null)
            {
                CreateIcon("HeaderChipIcon", topBar.transform, chipSprite, 26f, 26f);
            }

            _headerStatusText = CreateText("HeaderStatus", topBar.transform, headerFont,
                "A.S.T.R.A. BLOCK AUTOPILOT", 16, FontStyle.Bold,
                new Color(1f, 0.86f, 0.36f, 1f), TextAnchor.MiddleLeft);
            LayoutElement hLe = _headerStatusText.gameObject.AddComponent<LayoutElement>();
            hLe.flexibleWidth = 1f;
            hLe.preferredHeight = 36f;

            _slotsRemainingBadgeText = CreateText("SlotsBadge", topBar.transform, bodyFont,
                "BLOCKS: 0 / 2", 16, FontStyle.Bold,
                new Color(0.50f, 0.92f, 0.98f, 1f), TextAnchor.MiddleRight);
            _slotsRemainingBadgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            LayoutElement sbLe = _slotsRemainingBadgeText.gameObject.AddComponent<LayoutElement>();
            sbLe.preferredWidth = 175f;
            sbLe.minWidth = 150f;
            sbLe.preferredHeight = 38f;

            CreatePixelButton(topBar.transform, "Btn_PresetEclipse", "+ ECLIPSE", 112f, 38f,
                bodyFont, new Color(0.55f, 0.88f, 1f, 1f), new Color(0.06f, 0.14f, 0.22f, 1f),
                ApplyEclipsePreset, "ap_solar", fontSize: 15);

            CreatePixelButton(topBar.transform, "Btn_PresetStorm", "+ STORM", 100f, 38f,
                bodyFont, new Color(1f, 0.84f, 0.44f, 1f), new Color(0.16f, 0.10f, 0.04f, 1f),
                ApplyStormSafeModePreset, "ap_safemode", fontSize: 15);

            CreatePixelButton(topBar.transform, "Btn_ClearRules", "RESET", 88f, 38f,
                bodyFont, new Color(0.90f, 0.28f, 0.30f, 1f), Color.white,
                ClearAllRules, "ap_priority", useTintableSprite: true, fontSize: 15);

            CreatePixelButton(topBar.transform, "Btn_OpenDryRun", "TEST A.S.T.R.A.", 160f, 38f,
                bodyFont, new Color(0.20f, 0.78f, 0.38f, 1f), Color.white,
                () => OnRequestOpenDryRun?.Invoke(), "btn_dry_run", useTintableSprite: true, fontSize: 15);

            CreatePixelButton(topBar.transform, "Btn_CloseModal", "BACK [X]", 104f, 38f,
                bodyFont, new Color(0.75f, 0.20f, 0.24f, 1f), Color.white,
                CloseModal, null, useTintableSprite: true, fontSize: 15);

            // 2. MAIN SCRATCH SPLIT AREA (Categories | Block Drawer | Workspace)
            GameObject mainBody = CreateUIObject("MainBodyRow", rootObj.transform);
            LayoutElement bodyLe = mainBody.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1f;
            bodyLe.preferredHeight = 760f;
            bodyLe.minHeight = 420f;

            HorizontalLayoutGroup bodyH = mainBody.AddComponent<HorizontalLayoutGroup>();
            bodyH.spacing = 14f;
            bodyH.childAlignment = TextAnchor.MiddleCenter;
            bodyH.childControlWidth = true;
            bodyH.childControlHeight = true;
            bodyH.childForceExpandWidth = false;
            bodyH.childForceExpandHeight = true;

            // --- A. LEFT COLUMN: 3 SCRATCH BLOCK CATEGORIES ---
            GameObject catPanel = CreateSlicedPanel("CategorySidebar", mainBody.transform,
                PixelUITheme.PanelFrameSprite, new Color(0.76f, 0.88f, 1f, 0.94f), 0f);
            LayoutElement catLe = catPanel.AddComponent<LayoutElement>();
            catLe.preferredWidth = 235f;
            catLe.minWidth = 215f;
            catLe.flexibleWidth = 0f;

            VerticalLayoutGroup catV = catPanel.AddComponent<VerticalLayoutGroup>();
            catV.padding = new RectOffset(14, 14, 16, 16);
            catV.spacing = 12f;
            catV.childAlignment = TextAnchor.UpperCenter;
            catV.childControlWidth = true;
            catV.childControlHeight = true;
            catV.childForceExpandWidth = true;
            catV.childForceExpandHeight = false;

            Text catTitle = CreateText("CatSidebarTitle", catPanel.transform, bodyFont,
                "BLOCKS", 18, FontStyle.Bold, new Color(0.55f, 0.92f, 1f, 1f), TextAnchor.MiddleCenter);
            LayoutElement catTitleLe = catTitle.gameObject.AddComponent<LayoutElement>();
            catTitleLe.preferredHeight = 28f;
            catTitleLe.flexibleHeight = 0f;

            CreateCategoryTab(catPanel.transform, BlockCategory.Control, "1. CONTROL", "IF / THEN / ELSE",
                new Color(0.98f, 0.78f, 0.34f, 1f), bodyFont, bodyFont);
            CreateCategoryTab(catPanel.transform, BlockCategory.Condition, "2. CHECKS", "SENSOR  >  <",
                new Color(0.42f, 0.90f, 1f, 1f), bodyFont, bodyFont);
            CreateCategoryTab(catPanel.transform, BlockCategory.Action, "3. ACTIONS", "ON / OFF SWITCH",
                new Color(0.82f, 0.58f, 1f, 1f), bodyFont, bodyFont);

            GameObject catSpacer = CreateUIObject("CatSpacer", catPanel.transform);
            catSpacer.AddComponent<LayoutElement>().flexibleHeight = 1f;

            Text dragTip = CreateText("DragTipText", catPanel.transform, bodyFont,
                "DRAG BLOCKS INTO WORKSPACE SLOTS", 15, FontStyle.Normal,
                new Color(0.72f, 0.84f, 0.96f, 0.92f), TextAnchor.LowerCenter);
            dragTip.raycastTarget = false;
            LayoutElement dragTipLe = dragTip.gameObject.AddComponent<LayoutElement>();
            dragTipLe.preferredHeight = 52f;
            dragTipLe.minHeight = 48f;
            dragTipLe.flexibleHeight = 0f;

            // --- B. MIDDLE COLUMN: POP-OUT BLOCK DRAWER ---
            GameObject drawerPanel = CreateSlicedPanel("BlockDrawerPanel", mainBody.transform,
                PixelUITheme.PanelFrameSprite, new Color(0.76f, 0.88f, 1f, 0.94f), 0f);
            LayoutElement drawerLe = drawerPanel.AddComponent<LayoutElement>();
            drawerLe.preferredWidth = 420f;
            drawerLe.minWidth = 390f;
            drawerLe.flexibleWidth = 0f;

            VerticalLayoutGroup drawerV = drawerPanel.AddComponent<VerticalLayoutGroup>();
            drawerV.padding = new RectOffset(16, 16, 16, 16);
            drawerV.spacing = 8f;
            drawerV.childControlWidth = true;
            drawerV.childControlHeight = true;
            drawerV.childForceExpandWidth = true;
            drawerV.childForceExpandHeight = false;

            _drawerHeaderText = CreateText("DrawerHeader", drawerPanel.transform, bodyFont,
                "1. CONTROL (IF / ELSE)", 18, FontStyle.Bold, new Color(1f, 0.84f, 0.36f, 1f), TextAnchor.MiddleLeft);
            LayoutElement dhLe = _drawerHeaderText.gameObject.AddComponent<LayoutElement>();
            dhLe.preferredHeight = 28f;
            dhLe.flexibleHeight = 0f;

            _drawerSubText = CreateText("DrawerSub", drawerPanel.transform, bodyFont,
                "DRAG A BLOCK INTO WORKSPACE", 16, FontStyle.Normal, new Color(0.74f, 0.88f, 1f, 1f), TextAnchor.UpperLeft);
            LayoutElement dsLe = _drawerSubText.gameObject.AddComponent<LayoutElement>();
            dsLe.preferredHeight = 36f;
            dsLe.flexibleHeight = 0f;

            // Clipped vertical list for drawer blocks
            GameObject drawerScrollObj = CreateScrollableContainer("DrawerScroll", drawerPanel.transform, out Transform drawerContent);
            drawerScrollObj.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _drawerContentRoot = drawerContent;

            // --- C. RIGHT COLUMN: WORKSPACE CANVAS ---
            GameObject workspacePanel = CreateSlicedPanel("WorkspacePanel", mainBody.transform,
                PixelUITheme.PanelFrameSprite, new Color(0.76f, 0.88f, 1f, 0.92f), 0f);
            LayoutElement wsLe = workspacePanel.AddComponent<LayoutElement>();
            wsLe.flexibleWidth = 1f;
            wsLe.minWidth = 540f;

            VerticalLayoutGroup wsV = workspacePanel.AddComponent<VerticalLayoutGroup>();
            wsV.padding = new RectOffset(18, 18, 16, 16);
            wsV.spacing = 10f;
            wsV.childAlignment = TextAnchor.UpperCenter;
            wsV.childControlWidth = true;
            wsV.childControlHeight = true;
            wsV.childForceExpandWidth = true;
            wsV.childForceExpandHeight = false;

            GameObject wsHeaderRow = CreateUIObject("WorkspaceHeader", workspacePanel.transform);
            LayoutElement wsHeaderLe = wsHeaderRow.AddComponent<LayoutElement>();
            wsHeaderLe.preferredHeight = 28f;
            wsHeaderLe.minHeight = 28f;
            wsHeaderLe.flexibleHeight = 0f;

            HorizontalLayoutGroup wsHdrH = wsHeaderRow.AddComponent<HorizontalLayoutGroup>();
            wsHdrH.spacing = 12f;
            wsHdrH.childAlignment = TextAnchor.MiddleLeft;
            wsHdrH.childControlWidth = true;
            wsHdrH.childControlHeight = true;
            wsHdrH.childForceExpandWidth = false;
            wsHdrH.childForceExpandHeight = false;

            Text wsTitle = CreateText("WorkspaceTitle", wsHeaderRow.transform, headerFont,
                "ONBOARD FLIGHT SCRIPT (TOP TO BOTTOM)", 14, FontStyle.Bold,
                new Color(0.55f, 0.92f, 0.98f, 1f), TextAnchor.MiddleLeft);
            LayoutElement wsTitleLe = wsTitle.gameObject.AddComponent<LayoutElement>();
            wsTitleLe.flexibleWidth = 1f;
            wsTitleLe.preferredHeight = 26f;

            Text wsHint = CreateText("WorkspaceHint", wsHeaderRow.transform, bodyFont,
                "DRAG BLOCKS FROM LEFT INTO SLOTS", 15, FontStyle.Normal,
                new Color(0.75f, 0.88f, 0.98f, 0.90f), TextAnchor.MiddleRight);
            wsHint.horizontalOverflow = HorizontalWrapMode.Overflow;
            LayoutElement wsHintLe = wsHint.gameObject.AddComponent<LayoutElement>();
            wsHintLe.preferredWidth = 360f;
            wsHintLe.preferredHeight = 26f;

            GameObject wsScrollObj = CreateScrollableContainer("WorkspaceScroll", workspacePanel.transform, out Transform wsContent);
            wsScrollObj.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _workspaceListRoot = wsContent;

            // Attach drop target onto the workspace viewport so dropping an IF/THEN/ELSE block anywhere inside works!
            AttachDropTarget(wsScrollObj, DropSocketKind.WorkspaceAddRule, -1, wsScrollObj.GetComponent<Image>());
        }

        private void BindExistingHierarchy(Transform root)
        {
            _headerStatusText = FindChild<Text>(root, "HeaderBar/HeaderStatus");
            _slotsRemainingBadgeText = FindChild<Text>(root, "HeaderBar/SlotsBadge");

            RebindButton(root, "HeaderBar/Btn_PresetEclipse", ApplyEclipsePreset);
            RebindButton(root, "HeaderBar/Btn_PresetStorm", ApplyStormSafeModePreset);
            RebindButton(root, "HeaderBar/Btn_ClearRules", ClearAllRules);
            RebindButton(root, "HeaderBar/Btn_OpenDryRun", () => OnRequestOpenDryRun?.Invoke());
            RebindButton(root, "HeaderBar/Btn_CloseModal", CloseModal);

            Transform catSidebar = root.Find("MainBodyRow/CategorySidebar");
            if (catSidebar != null)
            {
                BindCategoryTab(catSidebar, BlockCategory.Control, "Btn_Cat_Control");
                BindCategoryTab(catSidebar, BlockCategory.Condition, "Btn_Cat_Condition");
                BindCategoryTab(catSidebar, BlockCategory.Action, "Btn_Cat_Action");
            }

            _drawerHeaderText = FindChild<Text>(root, "MainBodyRow/BlockDrawerPanel/DrawerHeader");
            _drawerSubText = FindChild<Text>(root, "MainBodyRow/BlockDrawerPanel/DrawerSub");
            _drawerContentRoot = root.Find("MainBodyRow/BlockDrawerPanel/DrawerScroll/Viewport/Content");
            _workspaceListRoot = root.Find("MainBodyRow/WorkspacePanel/WorkspaceScroll/Viewport/Content");

            Transform wsScroll = root.Find("MainBodyRow/WorkspacePanel/WorkspaceScroll");
            if (wsScroll != null)
            {
                AttachDropTarget(wsScroll.gameObject, DropSocketKind.WorkspaceAddRule, -1, wsScroll.GetComponent<Image>());
            }
        }

        // =====================================================================
        // CATEGORY TABS & DRAWER REBUILD
        // =====================================================================

        private void CreateCategoryTab(Transform parent, BlockCategory cat, string title, string subtitle,
            Color accentColor, Font headerFont, Font bodyFont)
        {
            string btnName = "Btn_Cat_" + cat;
            GameObject btnObj = CreateSlicedPanel(btnName, parent, PixelUITheme.BoxFrameSprite, accentColor, 90f);
            Image bg = btnObj.GetComponent<Image>();

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            BlockCategory captured = cat;
            btn.onClick.AddListener(() => SelectCategory(captured));

            VerticalLayoutGroup v = btnObj.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 12, 12);
            v.spacing = 6f;
            v.childAlignment = TextAnchor.MiddleLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            Text titleTxt = CreateText("Title", btnObj.transform, bodyFont, title, 18, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            titleTxt.raycastTarget = false;
            titleTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;

            Text subTxt = CreateText("Sub", btnObj.transform, bodyFont, subtitle, 15, FontStyle.Normal,
                new Color(0.86f, 0.94f, 1f, 0.95f), TextAnchor.MiddleLeft);
            subTxt.raycastTarget = false;
            subTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            int idx = (int)cat;
            _categoryTabImages[idx] = bg;
            _categoryTabLabels[idx] = titleTxt;
            _categoryTabSubLabels[idx] = subTxt;
        }

        private void BindCategoryTab(Transform catSidebar, BlockCategory cat, string childName)
        {
            Transform t = catSidebar.Find(childName);
            if (t == null) return;
            int idx = (int)cat;
            _categoryTabImages[idx] = t.GetComponent<Image>();
            _categoryTabLabels[idx] = FindChild<Text>(t, "Title");
            _categoryTabSubLabels[idx] = FindChild<Text>(t, "Sub");

            Button btn = t.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                BlockCategory captured = cat;
                btn.onClick.AddListener(() => SelectCategory(captured));
            }
        }

        private void SelectCategory(BlockCategory category)
        {
            _activeCategory = category;
            RebuildDrawer();
            UpdateCategoryTabVisuals();
        }

        private void UpdateCategoryTabVisuals()
        {
            int unlocked = GetUnlockedSlotsCount();
            int remaining = Mathf.Max(0, unlocked - _placedBlockCount);

            if (_categoryTabSubLabels[(int)BlockCategory.Control] != null)
            {
                _categoryTabSubLabels[(int)BlockCategory.Control].text = remaining > 0
                    ? string.Format("IF/ELSE ({0} LEFT)", remaining)
                    : "0 SLOTS LEFT";
            }

            Color[] baseColors =
            {
                new Color(0.95f, 0.72f, 0.24f, 1f),
                new Color(0.32f, 0.82f, 0.96f, 1f),
                new Color(0.72f, 0.50f, 0.98f, 1f)
            };

            for (int i = 0; i < 3; i++)
            {
                if (_categoryTabImages[i] == null) continue;
                bool active = i == (int)_activeCategory;
                Color c = baseColors[i];
                _categoryTabImages[i].color = active
                    ? c
                    : new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 0.82f);
            }
        }

        private void RebuildWorkspaceAndDrawer()
        {
            RebuildWorkspace();
            RebuildDrawer();
            UpdateCategoryTabVisuals();
            UpdateHeaderBadges();
        }

        private void UpdateHeaderBadges()
        {
            int unlocked = GetUnlockedSlotsCount();
            if (_slotsRemainingBadgeText != null)
            {
                _slotsRemainingBadgeText.text = string.Format("BLOCKS: {0} / {1}", _placedBlockCount, unlocked);
            }
        }

        private void RebuildDrawer()
        {
            if (_drawerContentRoot == null) return;
            ClearChildren(_drawerContentRoot);

            Font headerFont = PixelUITheme.HeaderFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font bodyFont = PixelUITheme.BodyFont ?? headerFont;
            int unlocked = GetUnlockedSlotsCount();
            int remaining = Mathf.Max(0, unlocked - _placedBlockCount);

            if (_activeCategory == BlockCategory.Control)
            {
                if (_drawerHeaderText != null)
                {
                    _drawerHeaderText.text = "1. CONTROL (IF / ELSE)";
                    _drawerHeaderText.color = new Color(1f, 0.86f, 0.42f, 1f);
                }
                if (_drawerSubText != null)
                {
                    _drawerSubText.text = remaining > 0
                        ? string.Format("DRAG A BLOCK RIGHT ({0} OF {1} FREE)", remaining, unlocked)
                        : string.Format("ALL {0} CHIP SLOTS IN WORKSPACE", unlocked);
                }

                // Exact single replenishing IF / THEN / ELSE block in list as requested!
                BuildDrawerControlBlock(remaining, unlocked, headerFont, bodyFont);
            }
            else if (_activeCategory == BlockCategory.Condition)
            {
                if (_drawerHeaderText != null)
                {
                    _drawerHeaderText.text = "2. CHECKS (SENSORS)";
                    _drawerHeaderText.color = new Color(0.45f, 0.92f, 1f, 1f);
                }
                if (_drawerSubText != null)
                {
                    _drawerSubText.text = "DRAG SENSOR OR > / < INTO IF ROW";
                }

                CreateDrawerSectionTitle(_drawerContentRoot, "HOW TO COMPARE (> / <)",
                    new Color(0.55f, 0.98f, 0.68f, 1f), bodyFont);

                GameObject opRow = CreateUIObject("DrawerOpRow", _drawerContentRoot);
                opRow.AddComponent<LayoutElement>().preferredHeight = 68f;
                HorizontalLayoutGroup opH = opRow.AddComponent<HorizontalLayoutGroup>();
                opH.spacing = 10f;
                opH.childControlWidth = true;
                opH.childControlHeight = true;
                opH.childForceExpandWidth = true;
                opH.childForceExpandHeight = true;

                BuildDrawerOperatorPill(opRow.transform, ComparisonOp.GreaterThan, "GREATER", headerFont, bodyFont);
                BuildDrawerOperatorPill(opRow.transform, ComparisonOp.LessThan, "LESS", headerFont, bodyFont);

                CreateDrawerSectionTitle(_drawerContentRoot, "WHAT TO CHECK (SENSORS)",
                    new Color(0.48f, 0.90f, 1f, 1f), bodyFont);

                BuildDrawerSensorPill(SensorMetric.SolarWatts, "SOLAR PANELS",
                    GetSensorDrawerHint(SensorMetric.SolarWatts), headerFont, bodyFont);
                BuildDrawerSensorPill(SensorMetric.BatteryPercent, "BATTERY CHARGE",
                    GetSensorDrawerHint(SensorMetric.BatteryPercent), headerFont, bodyFont);
                BuildDrawerSensorPill(SensorMetric.SsdUsedPercent, "SSD STORAGE",
                    GetSensorDrawerHint(SensorMetric.SsdUsedPercent), headerFont, bodyFont);
                BuildDrawerSensorPill(SensorMetric.RadiationLevel, "RADIATION LEVEL",
                    GetSensorDrawerHint(SensorMetric.RadiationLevel), headerFont, bodyFont);
            }
            else
            {
                if (_drawerHeaderText != null)
                {
                    _drawerHeaderText.text = "3. SUBSYSTEM ACTIONS";
                    _drawerHeaderText.color = new Color(0.84f, 0.68f, 1f, 1f);
                }
                if (_drawerSubText != null)
                {
                    _drawerSubText.text = "DRAG ACTION TO THEN OR ELSE";
                }

                CreateDrawerSectionTitle(_drawerContentRoot, "WHAT SPACECRAFT DOES",
                    new Color(0.88f, 0.74f, 1f, 1f), bodyFont);

                CreateDrawerActionPairRow(
                    AutopilotAction.ScienceOn, "SCIENCE", "TURN ON",
                    AutopilotAction.ScienceOff, "SCIENCE", "TURN OFF",
                    headerFont, bodyFont);

                CreateDrawerActionPairRow(
                    AutopilotAction.CommsOn, "COMMS TX", "TURN ON",
                    AutopilotAction.CommsOff, "COMMS TX", "TURN OFF",
                    headerFont, bodyFont);

                CreateDrawerActionPairRow(
                    AutopilotAction.SafeModeOn, "SAFE MODE", "ENABLE",
                    AutopilotAction.SafeModeOff, "SAFE MODE", "DISABLE",
                    headerFont, bodyFont);

                BuildDrawerActionPill(_drawerContentRoot, AutopilotAction.None, "NONE", "DO NOTHING",
                    headerFont, bodyFont, 64f);
            }
        }

        private void BuildDrawerControlBlock(int remaining, int unlocked, Font headerFont, Font bodyFont)
        {
            bool canAdd = remaining > 0;
            Color cardTint = canAdd
                ? new Color(0.82f, 0.92f, 1f, 0.98f)
                : new Color(0.46f, 0.50f, 0.62f, 0.82f);

            GameObject previewCard = CreateSlicedPanel("DrawerControlBlock", _drawerContentRoot,
                PixelUITheme.BoxFrameSprite, cardTint, 198f);
            VerticalLayoutGroup v = previewCard.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 10, 10);
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            string topText = canAdd
                ? "IF / THEN / ELSE RULE"
                : string.Format("CHIP FULL ({0} / {0})", unlocked);
            Text topArm = CreateText("TopArm", previewCard.transform, bodyFont, topText, 16, FontStyle.Bold,
                canAdd ? new Color(1f, 0.86f, 0.42f, 1f) : new Color(0.84f, 0.88f, 0.94f, 1f),
                TextAnchor.MiddleLeft);
            topArm.raycastTarget = false;
            topArm.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            // Mini IF preview row
            GameObject ifMini = CreateSlicedPanel("IfMini", previewCard.transform, PixelUITheme.SlotFrameSprite,
                canAdd ? new Color(0.32f, 0.62f, 0.88f, 0.90f) : new Color(0.24f, 0.28f, 0.36f, 0.8f), 34f);
            ifMini.GetComponent<Image>().raycastTarget = false;
            HorizontalLayoutGroup ifMiniH = ifMini.AddComponent<HorizontalLayoutGroup>();
            ifMiniH.padding = new RectOffset(10, 10, 4, 4);
            Text ifMiniLbl = CreateText("Lbl", ifMini.transform, bodyFont, "IF [SENSOR] [>/<] [VAL]", 15, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            ifMiniLbl.raycastTarget = false;

            // Mini THEN preview row
            GameObject thenBar = CreateSlicedPanel("ThenMini", previewCard.transform, PixelUITheme.SlotFrameSprite,
                canAdd ? new Color(0.30f, 0.72f, 0.58f, 0.90f) : new Color(0.24f, 0.28f, 0.36f, 0.8f), 34f);
            thenBar.GetComponent<Image>().raycastTarget = false;
            HorizontalLayoutGroup thH = thenBar.AddComponent<HorizontalLayoutGroup>();
            thH.padding = new RectOffset(10, 10, 4, 4);
            Text thLbl = CreateText("Lbl", thenBar.transform, bodyFont, "THEN  [ DROP ACTION ]", 15, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            thLbl.raycastTarget = false;

            // Mini ELSE preview row
            GameObject elseBar = CreateSlicedPanel("ElseMini", previewCard.transform, PixelUITheme.SlotFrameSprite,
                canAdd ? new Color(0.68f, 0.44f, 0.82f, 0.90f) : new Color(0.24f, 0.28f, 0.36f, 0.8f), 34f);
            elseBar.GetComponent<Image>().raycastTarget = false;
            HorizontalLayoutGroup elH = elseBar.AddComponent<HorizontalLayoutGroup>();
            elH.padding = new RectOffset(10, 10, 4, 4);
            Text elLbl = CreateText("Lbl", elseBar.transform, bodyFont, "ELSE  [ DROP ACTION ]", 15, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            elLbl.raycastTarget = false;

            string footerMsg = canAdd
                ? string.Format("DRAG TO WORKSPACE (+{0} FREE)", remaining)
                : "UPGRADE CPU IN TECH TREE";
            Text footTxt = CreateText("Footer", previewCard.transform, bodyFont, footerMsg, 15, FontStyle.Bold,
                canAdd ? new Color(1f, 0.92f, 0.55f, 1f) : new Color(0.84f, 0.88f, 0.94f, 1f),
                TextAnchor.MiddleCenter);
            footTxt.raycastTarget = false;
            footTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            if (canAdd)
            {
                var payload = new DragPayload
                {
                    Kind = DragPayloadKind.ControlIfElse,
                    Label = "IF / THEN / ELSE RULE",
                    Tint = new Color(0.45f, 0.85f, 1f, 0.98f),
                    Icon = PixelUITheme.ResolvePartIconSprite("chip")
                };
                AttachPaletteDrag(previewCard, payload, null);
            }
        }

        private void BuildDrawerOperatorPill(Transform parent, ComparisonOp op, string wordLabel, Font headerFont, Font bodyFont)
        {
            Color tint = new Color(0.42f, 0.92f, 0.62f, 0.98f);
            GameObject pill = CreateSlicedPanel("OpPill_" + op, parent, PixelUITheme.BoxFrameSprite, tint, 66f);
            HorizontalLayoutGroup h = pill.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 12, 8, 8);
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;

            Text badge = CreateText("Badge", pill.transform, headerFont, op == ComparisonOp.GreaterThan ? ">" : "<",
                24, FontStyle.Bold, new Color(0.55f, 1f, 0.72f, 1f), TextAnchor.MiddleCenter);
            badge.raycastTarget = false;
            badge.gameObject.AddComponent<LayoutElement>().preferredWidth = 28f;

            Text txt = CreateText("Label", pill.transform, bodyFont, wordLabel, 17, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            txt.raycastTarget = false;
            txt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var payload = new DragPayload
            {
                Kind = DragPayloadKind.ComparisonOp,
                Operator = op,
                Label = op == ComparisonOp.GreaterThan ? ">  GREATER THAN" : "<  LESS THAN",
                Tint = tint
            };
            AttachPaletteDrag(pill, payload, null);
        }

        private void BuildDrawerSensorPill(SensorMetric metric, string title, string subHint, Font headerFont, Font bodyFont)
        {
            Color tint = new Color(0.48f, 0.88f, 1f, 0.98f);
            Sprite icon = GetSensorIcon(metric);
            GameObject pill = CreateSlicedPanel("SensorPill_" + metric, _drawerContentRoot, PixelUITheme.BoxFrameSprite, tint, 72f);

            HorizontalLayoutGroup h = pill.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 12, 8, 8);
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;

            if (icon != null)
            {
                Image ic = CreateIcon("Icon", pill.transform, icon, 30f, 30f);
                ic.raycastTarget = false;
            }

            GameObject textCol = CreateUIObject("TextCol", pill.transform);
            textCol.AddComponent<LayoutElement>().flexibleWidth = 1f;
            VerticalLayoutGroup tv = textCol.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 2f;
            tv.childAlignment = TextAnchor.MiddleLeft;
            tv.childControlWidth = true;
            tv.childControlHeight = true;
            tv.childForceExpandWidth = true;
            tv.childForceExpandHeight = false;

            Text txt = CreateText("Label", textCol.transform, bodyFont, title, 18, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            txt.raycastTarget = false;
            txt.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            Text sub = CreateText("Sub", textCol.transform, bodyFont, subHint, 14, FontStyle.Normal,
                new Color(0.68f, 0.90f, 1f, 0.90f), TextAnchor.MiddleLeft);
            sub.raycastTarget = false;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            var payload = new DragPayload
            {
                Kind = DragPayloadKind.SensorMetric,
                Metric = metric,
                Label = GetShortSensorLabel(metric),
                Tint = tint,
                Icon = icon
            };
            AttachPaletteDrag(pill, payload, null);
        }

        private void CreateDrawerActionPairRow(
            AutopilotAction leftAct, string leftTitle, string leftSub,
            AutopilotAction rightAct, string rightTitle, string rightSub,
            Font headerFont, Font bodyFont)
        {
            GameObject row = CreateUIObject("ActionPair_" + leftTitle, _drawerContentRoot);
            row.AddComponent<LayoutElement>().preferredHeight = 72f;
            HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            BuildDrawerActionPill(row.transform, leftAct, leftTitle, leftSub, headerFont, bodyFont, 0f);
            BuildDrawerActionPill(row.transform, rightAct, rightTitle, rightSub, headerFont, bodyFont, 0f);
        }

        private void BuildDrawerActionPill(Transform parent, AutopilotAction action, string title, string stateSub,
            Font headerFont, Font bodyFont, float fixedHeight)
        {
            Color tint = GetActionColor(action);
            Sprite icon = GetActionIcon(action);
            GameObject pill = CreateSlicedPanel("ActionPill_" + action, parent, PixelUITheme.BoxFrameSprite, tint, fixedHeight);

            HorizontalLayoutGroup h = pill.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 8, 8);
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;

            if (icon != null)
            {
                Image ic = CreateIcon("Icon", pill.transform, icon, 24f, 24f);
                ic.raycastTarget = false;
            }

            GameObject textCol = CreateUIObject("TextCol", pill.transform);
            textCol.AddComponent<LayoutElement>().flexibleWidth = 1f;
            VerticalLayoutGroup tv = textCol.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 2f;
            tv.childAlignment = TextAnchor.MiddleLeft;
            tv.childControlWidth = true;
            tv.childControlHeight = true;
            tv.childForceExpandWidth = true;
            tv.childForceExpandHeight = false;

            Text txt = CreateText("Label", textCol.transform, bodyFont, title, 16, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            txt.raycastTarget = false;
            txt.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            Text sub = CreateText("Sub", textCol.transform, bodyFont, stateSub, 15, FontStyle.Bold,
                GetActionStateAccentColor(action), TextAnchor.MiddleLeft);
            sub.raycastTarget = false;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            var payload = new DragPayload
            {
                Kind = DragPayloadKind.SubsystemAction,
                Action = action,
                Label = GetShortActionLabel(action),
                Tint = tint,
                Icon = icon
            };
            AttachPaletteDrag(pill, payload, null);
        }

        private static void CreateDrawerSectionTitle(Transform parent, string title, Color color, Font font)
        {
            Text t = CreateText("SecHdr_" + title, parent, font, title, 16, FontStyle.Bold, color, TextAnchor.LowerLeft);
            t.raycastTarget = false;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
        }

        // =====================================================================
        // WORKSPACE C-BLOCKS BUILDER
        // =====================================================================

        private void RebuildWorkspace()
        {
            if (_workspaceListRoot == null) return;
            ClearChildren(_workspaceListRoot);
            _blockWidgets.Clear();

            Font headerFont = PixelUITheme.HeaderFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font bodyFont = PixelUITheme.BodyFont ?? headerFont;
            int unlocked = GetUnlockedSlotsCount();
            _placedBlockCount = Mathf.Clamp(_placedBlockCount, 0, unlocked);

            if (_selectedRuleIndex >= _placedBlockCount)
            {
                _selectedRuleIndex = _placedBlockCount - 1;
            }

            for (int i = 0; i < _placedBlockCount; i++)
            {
                _blockWidgets.Add(BuildWorkspaceCBlock(i, _rules[i], _workspaceListRoot, headerFont, bodyFont));
            }

            // Bottom drop zone prompt to add another IF/THEN/ELSE block
            int remaining = unlocked - _placedBlockCount;
            float dropHeight = _placedBlockCount == 0 ? 116f : 64f;
            _workspaceDropPrompt = CreateSlicedPanel("WorkspaceAddDropZone", _workspaceListRoot,
                PixelUITheme.BoxFrameSprite,
                remaining > 0 ? new Color(0.28f, 0.44f, 0.68f, 0.55f) : new Color(0.20f, 0.24f, 0.34f, 0.48f),
                dropHeight);

            HorizontalLayoutGroup dropH = _workspaceDropPrompt.AddComponent<HorizontalLayoutGroup>();
            dropH.padding = new RectOffset(18, 18, 10, 10);
            dropH.childAlignment = TextAnchor.MiddleCenter;

            string promptMsg = remaining > 0
                ? string.Format("+ DRAG [ IF / THEN / ELSE ] BLOCK HERE TO ADD RULE ({0} SLOT{1} FREE)",
                    remaining, remaining == 1 ? "" : "S")
                : string.Format("CPU FULL ({0} / {0} SLOTS)  •  UPGRADE ONBOARD CPU IN TECH TREE", unlocked);

            _workspaceDropPromptText = CreateText("DropPromptText", _workspaceDropPrompt.transform, bodyFont,
                promptMsg, 17, FontStyle.Bold,
                remaining > 0 ? new Color(0.70f, 0.94f, 1f, 0.95f) : new Color(0.75f, 0.80f, 0.90f, 0.85f),
                TextAnchor.MiddleCenter);
            _workspaceDropPromptText.raycastTarget = false;

            if (remaining > 0)
            {
                AttachDropTarget(_workspaceDropPrompt, DropSocketKind.WorkspaceAddRule, -1,
                    _workspaceDropPrompt.GetComponent<Image>());
                Button quickAddBtn = _workspaceDropPrompt.AddComponent<Button>();
                quickAddBtn.targetGraphic = _workspaceDropPrompt.GetComponent<Image>();
                quickAddBtn.onClick.AddListener(AddNewControlBlockToWorkspace);
            }

            HighlightTriggeredRules(new List<string>(_lastTriggeredIds));
        }

        private WorkspaceBlockWidgets BuildWorkspaceCBlock(int index, AutopilotRule rule, Transform parent,
            Font headerFont, Font bodyFont)
        {
            RuleSlotState st = GetOrCreateSlotState(index);
            bool isSelected = index == _selectedRuleIndex;
            Color frameTint = isSelected
                ? new Color(0.84f, 0.96f, 1f, 0.98f)
                : new Color(0.64f, 0.84f, 1f, 0.92f);

            // Outer Prototype-Style Rule Card Container
            GameObject cRoot = CreateSlicedPanel("RuleCBlock_" + index, parent,
                PixelUITheme.PanelFrameSprite, frameTint, 0f);
            LayoutElement rootLe = cRoot.AddComponent<LayoutElement>();
            rootLe.preferredHeight = 292f;
            rootLe.minHeight = 284f;

            Button selectBtn = cRoot.AddComponent<Button>();
            selectBtn.targetGraphic = cRoot.GetComponent<Image>();
            selectBtn.transition = Selectable.Transition.None;
            int capturedIdx = index;
            selectBtn.onClick.AddListener(() => SelectWorkspaceRule(capturedIdx));

            VerticalLayoutGroup cLayout = cRoot.AddComponent<VerticalLayoutGroup>();
            cLayout.padding = new RectOffset(16, 16, 14, 14);
            cLayout.spacing = 8f;
            cLayout.childControlWidth = true;
            cLayout.childControlHeight = true;
            cLayout.childForceExpandWidth = true;
            cLayout.childForceExpandHeight = false;

            // --- ROW 1: RULE HEADER BAR (#1 | RULE 1 | PRIORITY ROLE | STATUS | ^ v X) ---
            GameObject hdrRow = CreateUIObject("RuleHeaderRow", cRoot.transform);
            LayoutElement hdrLe = hdrRow.AddComponent<LayoutElement>();
            hdrLe.preferredHeight = 42f;
            hdrLe.minHeight = 42f;
            hdrLe.flexibleHeight = 0f;

            HorizontalLayoutGroup hdrH = hdrRow.AddComponent<HorizontalLayoutGroup>();
            hdrH.spacing = 10f;
            hdrH.childAlignment = TextAnchor.MiddleLeft;
            hdrH.childControlWidth = true;
            hdrH.childControlHeight = true;
            hdrH.childForceExpandWidth = false;
            hdrH.childForceExpandHeight = false;

            Color enColor = rule.Enabled
                ? new Color(0.94f, 0.72f, 0.22f, 1f)
                : new Color(0.45f, 0.48f, 0.56f, 1f);
            Button enableBtn = CreatePixelButton(hdrRow.transform, "Btn_Enable",
                string.Format("#{0} {1}", index + 1, rule.Enabled ? "ON" : "OFF"), 92f, 38f,
                bodyFont, enColor, Color.white, () => ToggleRuleEnabled(capturedIdx), "ap_priority", useTintableSprite: true, fontSize: 16);
            Image enableBg = enableBtn.GetComponent<Image>();
            Text enableTxt = enableBtn.GetComponentInChildren<Text>();

            Text ruleTitleTxt = CreateText("RuleTitle", hdrRow.transform, bodyFont,
                string.Format("RULE {0}", index + 1), 18, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            ruleTitleTxt.raycastTarget = false;
            ruleTitleTxt.gameObject.AddComponent<LayoutElement>().preferredWidth = 88f;

            string priorityNote = index == 0 ? "• RUNS FIRST" : "• CAN OVERRIDE #1";
            Text prioTxt = CreateText("PriorityNote", hdrRow.transform, bodyFont,
                priorityNote, 16, FontStyle.Normal,
                new Color(0.68f, 0.80f, 0.94f, 0.90f), TextAnchor.MiddleLeft);
            prioTxt.raycastTarget = false;
            prioTxt.gameObject.AddComponent<LayoutElement>().preferredWidth = 188f;

            Text statusBadgeTxt = CreateText("StatusBadge", hdrRow.transform, bodyFont,
                BuildRuleStatusLabel(rule, st, out Color statusColor), 16, FontStyle.Bold,
                statusColor, TextAnchor.MiddleLeft);
            statusBadgeTxt.raycastTarget = false;
            statusBadgeTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            Button upBtn = CreatePixelButton(hdrRow.transform, "Btn_Up", "^", 40f, 38f,
                bodyFont, Color.white, new Color(0.08f, 0.14f, 0.24f, 1f),
                () => MoveRule(capturedIdx, -1), null, fontSize: 18);
            upBtn.interactable = index > 0;

            Button downBtn = CreatePixelButton(hdrRow.transform, "Btn_Down", "v", 40f, 38f,
                bodyFont, Color.white, new Color(0.08f, 0.14f, 0.24f, 1f),
                () => MoveRule(capturedIdx, 1), null, fontSize: 18);
            downBtn.interactable = index < _placedBlockCount - 1;

            Button delBtn = CreatePixelButton(hdrRow.transform, "Btn_Delete", "X", 40f, 38f,
                bodyFont, new Color(0.88f, 0.26f, 0.28f, 1f), Color.white,
                () => RemoveRuleAt(capturedIdx), null, useTintableSprite: true, fontSize: 18);

            // --- ROW 2: IF CONDITION ROW (IF | SensorSocket | OpSocket | [- Val +]) ---
            GameObject ifRow = CreateUIObject("IfConditionRow", cRoot.transform);
            LayoutElement ifRowLe = ifRow.AddComponent<LayoutElement>();
            ifRowLe.preferredHeight = 52f;
            ifRowLe.minHeight = 52f;
            ifRowLe.flexibleHeight = 0f;

            HorizontalLayoutGroup ifH = ifRow.AddComponent<HorizontalLayoutGroup>();
            ifH.spacing = 10f;
            ifH.childAlignment = TextAnchor.MiddleLeft;
            ifH.childControlWidth = true;
            ifH.childControlHeight = true;
            ifH.childForceExpandWidth = false;
            ifH.childForceExpandHeight = false;

            Text ifLbl = CreateText("IfKeyword", ifRow.transform, bodyFont, "IF", 18, FontStyle.Bold,
                new Color(0.86f, 0.94f, 1f, 1f), TextAnchor.MiddleLeft);
            ifLbl.raycastTarget = false;
            LayoutElement ifLblLe = ifLbl.gameObject.AddComponent<LayoutElement>();
            ifLblLe.preferredWidth = 72f;
            ifLblLe.minWidth = 72f;

            // Sensor Socket (Drag-Drop ONLY + small [x] clear button when filled)
            Sprite sensorFrame = st.HasSensor ? PixelUITheme.BoxFrameSprite : PixelUITheme.SlotFrameSprite;
            Color sensorBgColor = st.HasSensor
                ? new Color(0.45f, 0.88f, 1f, 0.98f)
                : new Color(0.32f, 0.60f, 0.86f, 0.72f);
            GameObject sensorSlot = CreateSlicedPanel("SensorSlot", ifRow.transform, sensorFrame,
                sensorBgColor, 50f);
            LayoutElement sensLe = sensorSlot.AddComponent<LayoutElement>();
            sensLe.flexibleWidth = 1f;
            sensLe.minWidth = 210f;

            HorizontalLayoutGroup sensH = sensorSlot.AddComponent<HorizontalLayoutGroup>();
            sensH.padding = new RectOffset(12, 8, 4, 4);
            sensH.spacing = 8f;
            sensH.childAlignment = TextAnchor.MiddleLeft;
            sensH.childControlWidth = true;
            sensH.childControlHeight = true;
            sensH.childForceExpandWidth = false;

            Sprite sensorSprite = st.HasSensor ? GetSensorIcon(rule.Metric) : null;
            Image sensorIcon = CreateIcon("SensorIcon", sensorSlot.transform, sensorSprite, 26f, 26f);
            sensorIcon.raycastTarget = false;
            sensorIcon.gameObject.SetActive(st.HasSensor && sensorSprite != null);

            Text sensorText = CreateText("SensorLabel", sensorSlot.transform, bodyFont,
                st.HasSensor ? GetShortSensorLabel(rule.Metric) : "+ DROP SENSOR",
                18, FontStyle.Bold,
                st.HasSensor ? Color.white : new Color(0.55f, 0.90f, 1f, 0.96f),
                st.HasSensor ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            sensorText.raycastTarget = false;
            sensorText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            if (st.HasSensor)
            {
                CreateSocketClearButton(sensorSlot.transform, bodyFont,
                    () => ClearSocketAt(capturedIdx, DropSocketKind.SensorSlot));
            }
            AttachDropTarget(sensorSlot, DropSocketKind.SensorSlot, capturedIdx, sensorSlot.GetComponent<Image>());

            // Operator Socket (< or >, Drag-Drop ONLY + small [x] clear button when filled)
            Sprite opFrame = st.HasOperator ? PixelUITheme.BoxFrameSprite : PixelUITheme.SlotFrameSprite;
            Color opBgColor = st.HasOperator
                ? new Color(0.42f, 0.94f, 0.64f, 0.98f)
                : new Color(0.32f, 0.68f, 0.48f, 0.72f);
            GameObject opSlot = CreateSlicedPanel("OpSlot", ifRow.transform, opFrame,
                opBgColor, 50f);
            LayoutElement opLe = opSlot.AddComponent<LayoutElement>();
            opLe.preferredWidth = 220f;
            opLe.minWidth = 190f;

            HorizontalLayoutGroup opH = opSlot.AddComponent<HorizontalLayoutGroup>();
            opH.padding = new RectOffset(10, 8, 4, 4);
            opH.spacing = 6f;
            opH.childAlignment = TextAnchor.MiddleCenter;
            opH.childControlWidth = true;
            opH.childControlHeight = true;
            opH.childForceExpandWidth = false;

            Text opText = CreateText("OpLabel", opSlot.transform, bodyFont,
                st.HasOperator
                    ? (rule.Operator == ComparisonOp.LessThan ? "<  LESS THAN" : ">  GREATER THAN")
                    : "+ DROP  > / <",
                17, FontStyle.Bold,
                st.HasOperator ? Color.white : new Color(0.56f, 0.98f, 0.72f, 0.96f),
                TextAnchor.MiddleCenter);
            opText.raycastTarget = false;
            opText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            if (st.HasOperator)
            {
                CreateSocketClearButton(opSlot.transform, bodyFont,
                    () => ClearSocketAt(capturedIdx, DropSocketKind.OperatorSlot));
            }
            AttachDropTarget(opSlot, DropSocketKind.OperatorSlot, capturedIdx, opSlot.GetComponent<Image>());

            // Threshold Stepper Group: [ - | 50 | + ] (range 0..100, symmetric)
            GameObject threshGroup = CreateSlicedPanel("ThresholdStepper", ifRow.transform,
                PixelUITheme.BoxFrameSprite, new Color(0.44f, 0.90f, 0.64f, 0.96f), 50f);
            LayoutElement tgLe = threshGroup.AddComponent<LayoutElement>();
            tgLe.preferredWidth = 176f;
            tgLe.minWidth = 168f;

            HorizontalLayoutGroup tgH = threshGroup.AddComponent<HorizontalLayoutGroup>();
            tgH.padding = new RectOffset(8, 8, 5, 5);
            tgH.spacing = 6f;
            tgH.childAlignment = TextAnchor.MiddleCenter;
            tgH.childControlWidth = true;
            tgH.childControlHeight = true;
            tgH.childForceExpandWidth = false;

            CreatePixelButton(threshGroup.transform, "Btn_Minus", "-", 38f, 38f, bodyFont,
                new Color(0.18f, 0.34f, 0.30f, 1f), new Color(0.68f, 1f, 0.78f, 1f),
                () => AdjustThresholdStep(capturedIdx, -1), null, useTintableSprite: true, fontSize: 22);

            float clampedVal = Mathf.Clamp(rule.Threshold, 0f, 100f);
            InputField threshInput = CreateThresholdInputField(threshGroup.transform, "Input_Threshold",
                clampedVal.ToString("0.#", CultureInfo.InvariantCulture), bodyFont, 72f, 38f);
            threshInput.onValueChanged.AddListener(_ => OnThresholdEdited(capturedIdx, threshInput));
            threshInput.onEndEdit.AddListener(_ => OnThresholdEndEdit(capturedIdx, threshInput));

            CreatePixelButton(threshGroup.transform, "Btn_Plus", "+", 38f, 38f, bodyFont,
                new Color(0.18f, 0.34f, 0.30f, 1f), new Color(0.68f, 1f, 0.78f, 1f),
                () => AdjustThresholdStep(capturedIdx, 1), null, useTintableSprite: true, fontSize: 22);

            // --- ROW 3: THEN ACTION ROW ---
            GameObject thenRow = BuildPrototypeActionRow("ThenBranch", cRoot.transform,
                "THEN", new Color(0.45f, 0.95f, 0.84f, 1f),
                "+ ACTION WHEN CONDITION IS TRUE",
                rule.ThenAction, st.HasThenAction, capturedIdx, DropSocketKind.ThenSlot,
                headerFont, bodyFont,
                out Image thenSlotBg, out Image thenIcon, out Text thenText);

            // --- ROW 4: ELSE ACTION ROW ---
            GameObject elseRow = BuildPrototypeActionRow("ElseBranch", cRoot.transform,
                "ELSE", new Color(1f, 0.82f, 0.44f, 1f),
                "+ ACTION WHEN CONDITION IS FALSE",
                rule.ElseAction, st.HasElseAction, capturedIdx, DropSocketKind.ElseSlot,
                headerFont, bodyFont,
                out Image elseSlotBg, out Image elseIcon, out Text elseText);

            // --- ROW 5: HUMAN-READABLE LIVE SUMMARY BAR ---
            GameObject summaryBar = CreateSlicedPanel("RuleSummaryFooter", cRoot.transform,
                PixelUITheme.SlotFrameSprite, new Color(0.28f, 0.40f, 0.58f, 0.78f), 38f);
            HorizontalLayoutGroup sumH = summaryBar.AddComponent<HorizontalLayoutGroup>();
            sumH.padding = new RectOffset(14, 14, 4, 4);
            sumH.childAlignment = TextAnchor.MiddleLeft;

            Text summaryTxt = CreateText("SummaryText", summaryBar.transform, bodyFont,
                BuildRuleSummarySentence(rule, st), 16, FontStyle.Normal,
                new Color(0.84f, 0.92f, 1f, 0.94f), TextAnchor.MiddleLeft);
            summaryTxt.raycastTarget = false;
            summaryTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Tooltips
            TooltipTrigger.Attach(sensorSlot, "ap_solar");
            TooltipTrigger.Attach(opSlot, "ap_condition");
            TooltipTrigger.Attach(threshGroup, "ap_condition");
            TooltipTrigger.Attach(thenRow, "ap_then");
            TooltipTrigger.Attach(elseRow, "ap_else");

            return new WorkspaceBlockWidgets
            {
                SlotIndex = index,
                RuleId = rule.RuleId,
                Root = cRoot,
                FrameHighlight = cRoot.GetComponent<Image>(),
                SelectButton = selectBtn,
                EnableToggleButton = enableBtn,
                EnableToggleText = enableTxt,
                EnableToggleBg = enableBg,
                SensorSlotBg = sensorSlot.GetComponent<Image>(),
                SensorIcon = sensorIcon,
                SensorText = sensorText,
                OpSlotBg = opSlot.GetComponent<Image>(),
                OpText = opText,
                ThresholdInput = threshInput,
                ThresholdUnitText = null,
                HangarPartHintText = null,
                ThenSlotBg = thenSlotBg,
                ThenIcon = thenIcon,
                ThenText = thenText,
                ElseSlotBg = elseSlotBg,
                ElseIcon = elseIcon,
                ElseText = elseText,
                StatusBadgeText = statusBadgeTxt,
                SummaryFooterText = summaryTxt,
                MoveUpBtn = upBtn,
                MoveDownBtn = downBtn,
                DeleteBtn = delBtn
            };
        }

        private GameObject BuildPrototypeActionRow(string name, Transform parent, string branchTag, Color tagColor,
            string emptyPrompt, AutopilotAction currentAction, bool hasAction, int ruleIndex, DropSocketKind socketKind,
            Font headerFont, Font bodyFont,
            out Image slotBg, out Image actionIcon, out Text actionText)
        {
            GameObject branchRow = CreateUIObject(name, parent);
            LayoutElement rowLe = branchRow.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 48f;
            rowLe.minHeight = 48f;
            rowLe.flexibleHeight = 0f;

            HorizontalLayoutGroup h = branchRow.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            Text branchLbl = CreateText("BranchWord", branchRow.transform, bodyFont, branchTag, 18, FontStyle.Bold,
                tagColor, TextAnchor.MiddleLeft);
            branchLbl.raycastTarget = false;
            LayoutElement lblLe = branchLbl.gameObject.AddComponent<LayoutElement>();
            lblLe.preferredWidth = 72f;
            lblLe.minWidth = 72f;

            // Action Drop Socket Pill (Drag-Drop ONLY + small [x] clear button when filled)
            Sprite slotFrame = hasAction ? PixelUITheme.BoxFrameSprite : PixelUITheme.SlotFrameSprite;
            Color actionColor = hasAction
                ? GetActionColor(currentAction)
                : new Color(0.58f, 0.34f, 0.54f, 0.72f);
            Sprite iconSprite = hasAction ? GetActionIcon(currentAction) : null;
            GameObject actionSlot = CreateSlicedPanel("ActionSlot", branchRow.transform, slotFrame,
                actionColor, 46f);
            LayoutElement actLe = actionSlot.AddComponent<LayoutElement>();
            actLe.flexibleWidth = 1f;
            actLe.minWidth = 260f;

            HorizontalLayoutGroup actH = actionSlot.AddComponent<HorizontalLayoutGroup>();
            actH.padding = new RectOffset(14, 10, 4, 4);
            actH.spacing = 10f;
            actH.childAlignment = TextAnchor.MiddleLeft;
            actH.childControlWidth = true;
            actH.childControlHeight = true;
            actH.childForceExpandWidth = false;

            actionIcon = CreateIcon("ActionIcon", actionSlot.transform, iconSprite, 26f, 26f);
            actionIcon.raycastTarget = false;
            actionIcon.gameObject.SetActive(hasAction && iconSprite != null);

            actionText = CreateText("ActionLabel", actionSlot.transform, bodyFont,
                hasAction ? GetShortActionLabel(currentAction) : emptyPrompt,
                18, FontStyle.Bold,
                hasAction ? Color.white : new Color(0.95f, 0.56f, 0.74f, 0.96f),
                TextAnchor.MiddleLeft);
            actionText.raycastTarget = false;
            actionText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            if (hasAction)
            {
                CreateSocketClearButton(actionSlot.transform, bodyFont,
                    () => ClearSocketAt(ruleIndex, socketKind));
            }

            slotBg = actionSlot.GetComponent<Image>();
            AttachDropTarget(actionSlot, socketKind, ruleIndex, slotBg);

            return branchRow;
        }

        private static void CreateSocketClearButton(Transform parent, Font font, UnityEngine.Events.UnityAction onClear)
        {
            Button clr = CreatePixelButton(parent, "Btn_ClearSocket", "X", 30f, 30f,
                font, new Color(0.42f, 0.24f, 0.28f, 0.94f), new Color(1f, 0.88f, 0.90f, 1f),
                onClear, null, useTintableSprite: true, fontSize: 16);
            LayoutElement le = clr.GetComponent<LayoutElement>();
            if (le != null) le.flexibleWidth = 0f;
        }

        // =====================================================================
        // MUTATION & SNAP LOGIC
        // =====================================================================

        private void AddNewControlBlockToWorkspace()
        {
            int unlocked = GetUnlockedSlotsCount();
            if (_placedBlockCount >= unlocked) return;

            int newIdx = _placedBlockCount;
            _rules[newIdx] = new AutopilotRule
            {
                RuleId = "rule_" + (newIdx + 1),
                Enabled = true,
                Metric = SensorMetric.SolarWatts,
                Operator = ComparisonOp.LessThan,
                Threshold = 0f,
                ThenAction = AutopilotAction.None,
                ElseAction = AutopilotAction.None
            };
            SetSlotStateFilled(newIdx, false);
            _placedBlockCount++;
            _selectedRuleIndex = newIdx;

            RebuildWorkspaceAndDrawer();
            NotifyRulesChanged();
        }

        private void RemoveRuleAt(int index)
        {
            if (index < 0 || index >= _placedBlockCount) return;

            for (int i = index; i < _placedBlockCount - 1 && i + 1 < _rules.Count; i++)
            {
                _rules[i] = _rules[i + 1];
                _slotStates[i] = _slotStates[i + 1];
            }

            int lastIdx = _placedBlockCount - 1;
            _rules[lastIdx] = CreateDefaultRuleForSlot(lastIdx);
            _rules[lastIdx].Enabled = false;
            _slotStates[lastIdx] = new RuleSlotState();

            _placedBlockCount = Mathf.Max(0, _placedBlockCount - 1);
            for (int i = 0; i < _rules.Count; i++)
            {
                _rules[i].RuleId = "rule_" + (i + 1);
            }

            _selectedRuleIndex = _placedBlockCount > 0 ? Mathf.Clamp(index, 0, _placedBlockCount - 1) : -1;
            RebuildWorkspaceAndDrawer();
            NotifyRulesChanged();
        }

        private void MoveRule(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= _placedBlockCount || target < 0 || target >= _placedBlockCount) return;

            AutopilotRule tmp = _rules[index];
            _rules[index] = _rules[target];
            _rules[target] = tmp;

            RuleSlotState tmpSt = _slotStates[index];
            _slotStates[index] = _slotStates[target];
            _slotStates[target] = tmpSt;

            for (int i = 0; i < _rules.Count; i++)
            {
                _rules[i].RuleId = "rule_" + (i + 1);
            }
            _selectedRuleIndex = target;

            RebuildWorkspaceAndDrawer();
            NotifyRulesChanged();
        }

        private void SelectWorkspaceRule(int index)
        {
            if (index < 0 || index >= _placedBlockCount) return;
            _selectedRuleIndex = index;
            HighlightTriggeredRules(new List<string>(_lastTriggeredIds));
        }

        private void ToggleRuleEnabled(int index)
        {
            if (index < 0 || index >= _placedBlockCount) return;
            _rules[index].Enabled = !_rules[index].Enabled;
            _selectedRuleIndex = index;
            RebuildWorkspace();
            NotifyRulesChanged();
        }

        private void ApplyMetricToRule(int index, SensorMetric metric)
        {
            if (index < 0 || index >= _placedBlockCount) return;
            _rules[index].Metric = metric;
            GetOrCreateSlotState(index).HasSensor = true;
            _rules[index].Threshold = Mathf.Clamp(_rules[index].Threshold, 0f, 100f);
            _selectedRuleIndex = index;
            RebuildWorkspace();
            NotifyRulesChanged();
        }

        private void ApplyOperatorToRule(int index, ComparisonOp op)
        {
            if (index < 0 || index >= _placedBlockCount) return;
            _rules[index].Operator = op;
            GetOrCreateSlotState(index).HasOperator = true;
            _selectedRuleIndex = index;
            RebuildWorkspace();
            NotifyRulesChanged();
        }

        private void ApplyActionToRule(int index, AutopilotAction action, bool isElse)
        {
            if (index < 0 || index >= _placedBlockCount) return;
            RuleSlotState st = GetOrCreateSlotState(index);
            if (isElse)
            {
                _rules[index].ElseAction = action;
                st.HasElseAction = true;
            }
            else
            {
                _rules[index].ThenAction = action;
                st.HasThenAction = true;
            }

            _selectedRuleIndex = index;
            RebuildWorkspace();
            NotifyRulesChanged();
        }

        private void ClearSocketAt(int index, DropSocketKind socketKind)
        {
            if (index < 0 || index >= _placedBlockCount) return;
            RuleSlotState st = GetOrCreateSlotState(index);
            switch (socketKind)
            {
                case DropSocketKind.SensorSlot:
                    st.HasSensor = false;
                    break;
                case DropSocketKind.OperatorSlot:
                    st.HasOperator = false;
                    break;
                case DropSocketKind.ThenSlot:
                    st.HasThenAction = false;
                    _rules[index].ThenAction = AutopilotAction.None;
                    break;
                case DropSocketKind.ElseSlot:
                    st.HasElseAction = false;
                    _rules[index].ElseAction = AutopilotAction.None;
                    break;
            }
            _selectedRuleIndex = index;
            RebuildWorkspace();
            NotifyRulesChanged();
        }

        private void AdjustThresholdStep(int index, int sign)
        {
            if (index < 0 || index >= _placedBlockCount) return;
            AutopilotRule r = _rules[index];
            RuleSlotState st = GetOrCreateSlotState(index);

            float step = (st.HasSensor && r.Metric == SensorMetric.SolarWatts) ? 1f : 5f;
            float next = Mathf.Clamp(r.Threshold + sign * step, 0f, 100f);
            r.Threshold = next;

            if (index < _blockWidgets.Count && _blockWidgets[index] != null && _blockWidgets[index].ThresholdInput != null)
            {
                _suppressCallbacks = true;
                _blockWidgets[index].ThresholdInput.SetTextWithoutNotify(next.ToString("0.#", CultureInfo.InvariantCulture));
                _suppressCallbacks = false;
            }

            UpdateRuleLiveTexts(index);
            NotifyRulesChanged();
        }

        private void UpdateRuleLiveTexts(int index)
        {
            if (index < 0 || index >= _blockWidgets.Count || index >= _rules.Count) return;
            WorkspaceBlockWidgets w = _blockWidgets[index];
            AutopilotRule r = _rules[index];
            RuleSlotState st = GetOrCreateSlotState(index);

            if (w.SummaryFooterText != null)
            {
                w.SummaryFooterText.text = BuildRuleSummarySentence(r, st);
            }
            if (w.StatusBadgeText != null)
            {
                Color c;
                w.StatusBadgeText.text = BuildRuleStatusLabel(r, st, out c);
                w.StatusBadgeText.color = c;
            }
        }

        private void OnThresholdEdited(int index, InputField input)
        {
            if (_suppressCallbacks || index < 0 || index >= _placedBlockCount || input == null) return;
            AutopilotRule r = _rules[index];
            string raw = input.text ?? string.Empty;
            if (raw.Length > 0)
            {
                float normalized;
                bool rewrite = TryNormalizeThresholdInput(raw, out normalized);
                r.Threshold = normalized;
                if (rewrite)
                {
                    _suppressCallbacks = true;
                    string formatted = normalized.ToString("0.#", CultureInfo.InvariantCulture);
                    input.SetTextWithoutNotify(formatted);
                    input.caretPosition = formatted.Length;
                    _suppressCallbacks = false;
                }
            }
            UpdateRuleLiveTexts(index);
            NotifyRulesChanged();
        }

        private void OnThresholdEndEdit(int index, InputField input)
        {
            if (_suppressCallbacks || index < 0 || index >= _placedBlockCount || input == null) return;
            AutopilotRule r = _rules[index];
            float normalized;
            TryNormalizeThresholdInput(input.text ?? string.Empty, out normalized);
            r.Threshold = normalized;

            _suppressCallbacks = true;
            input.SetTextWithoutNotify(r.Threshold.ToString("0.#", CultureInfo.InvariantCulture));
            _suppressCallbacks = false;
            UpdateRuleLiveTexts(index);
            NotifyRulesChanged();
        }

        private void RefreshHangarPartHints()
        {
            for (int i = 0; i < _blockWidgets.Count && i < _rules.Count; i++)
            {
                WorkspaceBlockWidgets w = _blockWidgets[i];
                if (w != null && w.HangarPartHintText != null)
                {
                    RuleSlotState st = GetOrCreateSlotState(i);
                    w.HangarPartHintText.text = st.HasSensor ? GetShortInstalledPartTag(_rules[i].Metric) : string.Empty;
                }
            }
        }

        // =====================================================================
        // DRAG & DROP ENGINE (Clean uGUI Ghost + Highlighted Drop Targets)
        // =====================================================================

        private void AttachPaletteDrag(GameObject target, DragPayload payload, Action onClickFallback)
        {
            var handler = target.GetComponent<ScratchPaletteDrag>() ?? target.AddComponent<ScratchPaletteDrag>();
            handler.Owner = this;
            handler.Payload = payload;
            handler.OnClickAction = onClickFallback;
        }

        private void AttachDropTarget(GameObject target, DropSocketKind socketKind, int ruleIndex, Image highlightImage)
        {
            var handler = target.GetComponent<ScratchDropSocket>() ?? target.AddComponent<ScratchDropSocket>();
            handler.Owner = this;
            handler.SocketKind = socketKind;
            handler.RuleIndex = ruleIndex;
            handler.HighlightTarget = highlightImage;
        }

        private void BeginPaletteDrag(DragPayload payload, PointerEventData eventData)
        {
            ClearDragGhost();

            Transform ghostParent = _rootCanvas != null ? _rootCanvas.transform : transform;
            Font font = PixelUITheme.BodyFont ?? PixelUITheme.HeaderFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _dragGhost = CreateSlicedPanel("ScratchDragGhost", ghostParent,
                PixelUITheme.BoxFrameSprite, new Color(payload.Tint.r, payload.Tint.g, payload.Tint.b, 0.96f), 52f);
            _dragGhostRt = _dragGhost.GetComponent<RectTransform>();
            _dragGhostRt.sizeDelta = new Vector2(260f, 52f);
            _dragGhost.transform.SetAsLastSibling();

            CanvasGroup cg = _dragGhost.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            HorizontalLayoutGroup h = _dragGhost.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(14, 14, 6, 6);
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;

            if (payload.Icon != null)
            {
                Image ic = CreateIcon("Icon", _dragGhost.transform, payload.Icon, 24f, 24f);
                ic.raycastTarget = false;
            }

            Text txt = CreateText("Label", _dragGhost.transform, font, payload.Label, 16, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft);
            txt.raycastTarget = false;
            txt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _dragGhost.GetComponent<Image>().raycastTarget = false;
            UpdateDragGhostPosition(eventData);
        }

        private void UpdateDragGhostPosition(PointerEventData eventData)
        {
            if (_dragGhostRt == null || eventData == null) return;
            Transform ghostParent = _dragGhostRt.parent;
            if (ghostParent is RectTransform parentRt)
            {
                Vector3 worldPos;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRt, eventData.position, eventData.pressEventCamera, out worldPos))
                {
                    _dragGhostRt.position = worldPos;
                }
            }
        }

        private void ClearDragGhost()
        {
            if (_dragGhost != null)
            {
                if (Application.isPlaying)
                    Destroy(_dragGhost);
                else
                    DestroyImmediate(_dragGhost);
                _dragGhost = null;
                _dragGhostRt = null;
            }
        }

        private static bool IsPayloadCompatible(DragPayloadKind payloadKind, DropSocketKind socketKind)
        {
            switch (socketKind)
            {
                case DropSocketKind.WorkspaceAddRule:
                    return payloadKind == DragPayloadKind.ControlIfElse;
                case DropSocketKind.SensorSlot:
                    return payloadKind == DragPayloadKind.SensorMetric;
                case DropSocketKind.OperatorSlot:
                    return payloadKind == DragPayloadKind.ComparisonOp;
                case DropSocketKind.ThenSlot:
                case DropSocketKind.ElseSlot:
                    return payloadKind == DragPayloadKind.SubsystemAction;
                default:
                    return false;
            }
        }

        private void HandleDropOnSocket(DragPayload payload, DropSocketKind socketKind, int ruleIndex)
        {
            if (!IsPayloadCompatible(payload.Kind, socketKind)) return;

            switch (socketKind)
            {
                case DropSocketKind.WorkspaceAddRule:
                    AddNewControlBlockToWorkspace();
                    break;
                case DropSocketKind.SensorSlot:
                    ApplyMetricToRule(ruleIndex, payload.Metric);
                    break;
                case DropSocketKind.OperatorSlot:
                    ApplyOperatorToRule(ruleIndex, payload.Operator);
                    break;
                case DropSocketKind.ThenSlot:
                    ApplyActionToRule(ruleIndex, payload.Action, false);
                    break;
                case DropSocketKind.ElseSlot:
                    ApplyActionToRule(ruleIndex, payload.Action, true);
                    break;
            }
        }

        private class ScratchPaletteDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
        {
            public AutopilotUI Owner;
            public DragPayload Payload;
            public Action OnClickAction;
            private bool _isDragging;

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (Owner == null) return;
                _isDragging = true;
                Owner.BeginPaletteDrag(Payload, eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (Owner == null || !_isDragging) return;
                Owner.UpdateDragGhostPosition(eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                _isDragging = false;
                if (Owner != null) Owner.ClearDragGhost();
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (_isDragging) return;
                OnClickAction?.Invoke();
            }
        }

        private class ScratchDropSocket : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public AutopilotUI Owner;
            public DropSocketKind SocketKind;
            public int RuleIndex;
            public Image HighlightTarget;

            private Color _originalColor;
            private bool _hasSavedColor;

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (Owner == null || eventData == null || eventData.pointerDrag == null) return;
                var drag = eventData.pointerDrag.GetComponent<ScratchPaletteDrag>();
                if (drag == null || !IsPayloadCompatible(drag.Payload.Kind, SocketKind)) return;

                if (HighlightTarget != null)
                {
                    if (!_hasSavedColor)
                    {
                        _originalColor = HighlightTarget.color;
                        _hasSavedColor = true;
                    }
                    HighlightTarget.color = new Color(1f, 0.94f, 0.45f, 1f);
                }
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                RestoreHighlight();
            }

            private void OnDisable()
            {
                RestoreHighlight();
            }

            public void OnDrop(PointerEventData eventData)
            {
                RestoreHighlight();
                if (Owner == null || eventData == null || eventData.pointerDrag == null) return;
                var drag = eventData.pointerDrag.GetComponent<ScratchPaletteDrag>();
                if (drag == null) return;

                Owner.HandleDropOnSocket(drag.Payload, SocketKind, RuleIndex);
            }

            private void RestoreHighlight()
            {
                if (HighlightTarget != null && _hasSavedColor)
                {
                    HighlightTarget.color = _originalColor;
                    _hasSavedColor = false;
                }
            }
        }

        // =====================================================================
        // VALIDATION, LABELS, ICONS & COLORS
        // =====================================================================

        private static bool TryNormalizeThresholdInput(string raw, out float result)
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
                if (ch >= '0' && ch <= '9') continue;
                if (ch == '.' || ch == ',')
                {
                    separatorCount++;
                    if (separatorCount <= 1) continue;
                }
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
                float.IsNaN(parsed) || float.IsInfinity(parsed) || parsed < 0f)
            {
                result = 0f;
                return true;
            }

            if (parsed > 100f)
            {
                result = 100f;
                return true;
            }

            result = parsed;
            return false;
        }

        private static string GetShortSensorLabel(SensorMetric metric)
        {
            switch (metric)
            {
                case SensorMetric.SolarWatts: return "SOLAR POWER";
                case SensorMetric.BatteryPercent: return "BATTERY LEVEL";
                case SensorMetric.SsdUsedPercent: return "SSD USED";
                case SensorMetric.RadiationLevel: return "RADIATION";
                default: return "SENSOR";
            }
        }

        private static string GetMetricUnitLabel(SensorMetric metric)
        {
            switch (metric)
            {
                case SensorMetric.SolarWatts: return "W";
                case SensorMetric.BatteryPercent:
                case SensorMetric.SsdUsedPercent: return "%";
                case SensorMetric.RadiationLevel: return "RAD";
                default: return string.Empty;
            }
        }

        private string GetShortInstalledPartTag(SensorMetric metric)
        {
            ComponentCategory cat;
            switch (metric)
            {
                case SensorMetric.SolarWatts: cat = ComponentCategory.PowerGeneration; break;
                case SensorMetric.BatteryPercent: cat = ComponentCategory.EnergyStorage; break;
                case SensorMetric.SsdUsedPercent: cat = ComponentCategory.CommsAndStorage; break;
                default: cat = ComponentCategory.CorePlatform; break;
            }

            if (_installedComponents != null)
            {
                for (int i = 0; i < _installedComponents.Count; i++)
                {
                    if (_installedComponents[i] != null && _installedComponents[i].Category == cat)
                    {
                        return _installedComponents[i].Name.ToUpperInvariant();
                    }
                }
            }
            return string.Empty;
        }

        private static Sprite GetSensorIcon(SensorMetric metric)
        {
            switch (metric)
            {
                case SensorMetric.SolarWatts: return PixelUITheme.ResolvePartIconSprite("solar");
                case SensorMetric.BatteryPercent: return PixelUITheme.ResolvePartIconSprite("battery");
                case SensorMetric.SsdUsedPercent: return PixelUITheme.ResolvePartIconSprite("chip");
                case SensorMetric.RadiationLevel:
                    return PixelUITheme.ResolvePartIconSprite("shield") ?? PixelUITheme.ResolveTelemetryIconSprite(5);
                default: return PixelUITheme.ResolvePartIconSprite("chip");
            }
        }

        private static string GetShortActionLabel(AutopilotAction action)
        {
            switch (action)
            {
                case AutopilotAction.ScienceOn: return "SCIENCE PAYLOAD: ON";
                case AutopilotAction.ScienceOff: return "SCIENCE PAYLOAD: OFF";
                case AutopilotAction.CommsOn: return "COMMS TRANSMITTER: ON";
                case AutopilotAction.CommsOff: return "COMMS TRANSMITTER: OFF";
                case AutopilotAction.SafeModeOn: return "SAFE MODE: ENABLED";
                case AutopilotAction.SafeModeOff: return "SAFE MODE: DISABLED";
                default: return "NO CHANGE (NONE)";
            }
        }

        private static Sprite GetActionIcon(AutopilotAction action)
        {
            switch (action)
            {
                case AutopilotAction.ScienceOn:
                case AutopilotAction.ScienceOff:
                    return PixelUITheme.ResolvePartIconSprite("camera");
                case AutopilotAction.CommsOn:
                case AutopilotAction.CommsOff:
                    return PixelUITheme.ResolvePartIconSprite("comms");
                case AutopilotAction.SafeModeOn:
                case AutopilotAction.SafeModeOff:
                    return PixelUITheme.ResolvePartIconSprite("shield") ?? PixelUITheme.ResolveTelemetryIconSprite(5);
                default:
                    return null;
            }
        }

        private static string BuildRuleStatusLabel(AutopilotRule rule, RuleSlotState st, out Color color)
        {
            if (rule == null || !rule.Enabled)
            {
                color = new Color(0.62f, 0.68f, 0.78f, 0.90f);
                return "DISABLED";
            }

            bool missingSensor = st == null || !st.HasSensor;
            bool missingOp = st == null || !st.HasOperator;
            bool missingAction = st == null || (!st.HasThenAction && !st.HasElseAction);

            if (!missingSensor && !missingOp && !missingAction)
            {
                color = new Color(0.45f, 0.96f, 0.62f, 1f);
                return "READY";
            }

            var missing = new List<string>(3);
            if (missingSensor) missing.Add("SENSOR");
            if (missingOp) missing.Add(">/<");
            if (missingAction) missing.Add("ACTION");

            color = new Color(1f, 0.74f, 0.34f, 1f);
            return "MISSING: " + string.Join(", ", missing);
        }

        private static string BuildRuleSummarySentence(AutopilotRule rule, RuleSlotState st)
        {
            if (rule == null || st == null) return "IF ... -> ..., ELSE -> ...";

            string sensorPart = st.HasSensor ? GetShortSensorLabel(rule.Metric) : "...";
            string opPart = st.HasOperator
                ? (rule.Operator == ComparisonOp.LessThan ? "<" : ">")
                : "?";
            string unitPart = st.HasSensor ? " " + GetMetricUnitLabel(rule.Metric) : string.Empty;
            string valPart = rule.Threshold.ToString("0.#", CultureInfo.InvariantCulture) + unitPart;

            string thenPart = st.HasThenAction ? GetShortActionLabel(rule.ThenAction) : "...";
            string elsePart = st.HasElseAction ? GetShortActionLabel(rule.ElseAction) : "...";

            return string.Format("IF {0} {1} {2}  ->  {3},  ELSE  ->  {4}",
                sensorPart, opPart, valPart, thenPart, elsePart);
        }

        private string GetSensorDrawerHint(SensorMetric metric)
        {
            switch (metric)
            {
                case SensorMetric.SolarWatts:
                    if (_installedComponents != null)
                    {
                        for (int i = 0; i < _installedComponents.Count; i++)
                        {
                            if (_installedComponents[i] != null &&
                                _installedComponents[i].Category == ComponentCategory.PowerGeneration &&
                                _installedComponents[i].SolarWatts > 0f)
                            {
                                return string.Format("SUN ~{0:0}W • ECLIPSE 0W", _installedComponents[i].SolarWatts);
                            }
                        }
                    }
                    return "SUN ~25W • ECLIPSE 0W";
                case SensorMetric.BatteryPercent:
                    return "CHARGE: 0% TO 100%";
                case SensorMetric.SsdUsedPercent:
                    return "STORAGE: 0% TO 100%";
                case SensorMetric.RadiationLevel:
                    return "NORM ~5 RAD • STORM 85+";
                default:
                    return string.Empty;
            }
        }

        private static Color GetActionStateAccentColor(AutopilotAction action)
        {
            switch (action)
            {
                case AutopilotAction.ScienceOn:
                case AutopilotAction.CommsOn:
                    return new Color(0.50f, 1f, 0.78f, 1f);
                case AutopilotAction.ScienceOff:
                case AutopilotAction.CommsOff:
                    return new Color(1f, 0.60f, 0.72f, 1f);
                case AutopilotAction.SafeModeOn:
                    return new Color(1f, 0.84f, 0.44f, 1f);
                case AutopilotAction.SafeModeOff:
                    return new Color(0.72f, 0.86f, 1f, 1f);
                default:
                    return new Color(0.76f, 0.82f, 0.92f, 0.90f);
            }
        }

        private static Color GetActionColor(AutopilotAction action)
        {
            switch (action)
            {
                case AutopilotAction.ScienceOn:
                case AutopilotAction.CommsOn:
                    return new Color(0.38f, 0.94f, 0.76f, 0.98f);
                case AutopilotAction.ScienceOff:
                case AutopilotAction.CommsOff:
                    return new Color(0.96f, 0.50f, 0.68f, 0.98f);
                case AutopilotAction.SafeModeOn:
                    return new Color(1f, 0.76f, 0.38f, 0.98f);
                case AutopilotAction.SafeModeOff:
                    return new Color(0.62f, 0.78f, 1f, 0.98f);
                default:
                    return new Color(0.56f, 0.64f, 0.78f, 0.94f);
            }
        }

        // =====================================================================
        // UI PRIMITIVE FACTORIES (Clean 9-slice, No Text Overflow)
        // =====================================================================

        private static GameObject CreateScrollableContainer(string name, Transform parent, out Transform contentRoot)
        {
            GameObject scrollObj = CreateUIObject(name, parent);
            Image bg = scrollObj.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.06f, 0.12f, 0.55f);

            ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 28f;

            GameObject viewport = CreateUIObject("Viewport", scrollObj.transform);
            StretchFull(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            Image vpImg = viewport.AddComponent<Image>();
            vpImg.color = Color.clear;

            GameObject content = CreateUIObject("Content", viewport.transform);
            RectTransform cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.anchoredPosition = Vector2.zero;
            cRt.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup v = content.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(10, 10, 8, 12);
            v.spacing = 10f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.content = cRt;
            contentRoot = content.transform;
            return scrollObj;
        }

        private static GameObject CreateSlicedPanel(string name, Transform parent, Sprite sprite, Color tint, float preferredHeight)
        {
            GameObject panel = CreateUIObject(name, parent);
            Image img = panel.AddComponent<Image>();
            if (sprite != null)
            {
                if (sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
                img.sprite = sprite;
                img.color = tint;
                if (sprite.border != Vector4.zero)
                {
                    img.type = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = 0.42f;
                }
            }
            else
            {
                img.color = tint;
            }

            if (preferredHeight > 0f)
            {
                LayoutElement le = panel.AddComponent<LayoutElement>();
                le.preferredHeight = preferredHeight;
            }
            return panel;
        }

        private static InputField CreateThresholdInputField(Transform parent, string name, string defaultVal, Font font, float width, float height)
        {
            GameObject inputObj = CreateSlicedPanel(name, parent, PixelUITheme.SlotFrameSprite,
                new Color(0.18f, 0.28f, 0.42f, 0.92f), height);
            LayoutElement le = inputObj.GetComponent<LayoutElement>();
            le.preferredWidth = width;

            GameObject textObj = CreateUIObject("Text", inputObj.transform);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(6f, 2f);
            textRt.offsetMax = new Vector2(-6f, -2f);

            Text txt = textObj.AddComponent<Text>();
            txt.font = font;
            txt.text = defaultVal;
            txt.fontSize = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(1f, 0.94f, 0.50f, 1f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;

            InputField input = inputObj.AddComponent<InputField>();
            input.targetGraphic = inputObj.GetComponent<Image>();
            input.textComponent = txt;
            input.contentType = InputField.ContentType.Standard;
            input.text = defaultVal;

            return input;
        }

        private static Button CreatePixelButton(Transform parent, string name, string label, float width, float height,
            Font font, Color bgTint, Color textColor, UnityEngine.Events.UnityAction onClick, string tooltipKey,
            bool useTintableSprite = false, int fontSize = 15)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            Image img = btnObj.AddComponent<Image>();

            Sprite sprite = useTintableSprite && PixelUITheme.ButtonDisabledSprite != null
                ? PixelUITheme.ButtonDisabledSprite
                : PixelUITheme.ButtonNormalSprite;

            if (sprite != null)
            {
                if (sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = height <= 42f ? 0.65f : 0.42f;
                img.color = bgTint;
            }
            else
            {
                img.color = bgTint;
            }

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            if (!useTintableSprite && PixelUITheme.ButtonHoverSprite != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                SpriteState st = btn.spriteState;
                st.highlightedSprite = PixelUITheme.ButtonHoverSprite;
                if (PixelUITheme.ButtonPressedSprite != null) st.pressedSprite = PixelUITheme.ButtonPressedSprite;
                if (PixelUITheme.ButtonDisabledSprite != null) st.disabledSprite = PixelUITheme.ButtonDisabledSprite;
                btn.spriteState = st;
            }
            else
            {
                btn.transition = Selectable.Transition.ColorTint;
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
                cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
                btn.colors = cb;
            }

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = width;
            if (height > 0f)
            {
                le.preferredHeight = height;
                le.minHeight = height;
                le.flexibleHeight = 0f;
            }

            Text txt = CreateText("Text", btnObj.transform, font, label, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
            txt.raycastTarget = false;
            RectTransform txtRt = txt.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(6f, 2f);
            txtRt.offsetMax = new Vector2(-6f, -2f);

            if (!string.IsNullOrEmpty(tooltipKey))
            {
                TooltipTrigger.Attach(btnObj, tooltipKey);
            }

            return btn;
        }

        private static Image CreateIcon(string name, Transform parent, Sprite sprite, float w, float h)
        {
            GameObject go = CreateUIObject(name, parent);
            Image img = go.AddComponent<Image>();
            if (sprite != null && sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = Color.white;

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            le.minWidth = w;
            le.minHeight = h;
            return img;
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
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            return txt;
        }

        private static void RebindButton(Transform root, string path, UnityEngine.Events.UnityAction action)
        {
            Button btn = FindChild<Button>(root, path);
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(action);
            }
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
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
    }
}
