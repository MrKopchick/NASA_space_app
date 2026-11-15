using System;
using System.Collections.Generic;
using AstraLink.Core;
using AstraLink.UI;
using AstraLink.UI.Workshop;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Hub
{
    /// <summary>
    /// KSP-Style Branching Tech Tree R&D UI Controller (Tasks S1-07 / S3-01, Ivan V).
    /// Displays 6 horizontal progression branches where nodes unlock sequentially along each branch:
    /// Tier 1 (Starter) ───► Tier 2 ───► Tier 3 ───► Tier 4
    /// Includes dedicated icon slots on every tech node for easy sprite placement in Night Sprint N-01.
    /// </summary>
    public class RnDPanelUI : MonoBehaviour
    {
        private AgencyState _agencyState;
        private List<SatelliteComponent> _allComponents = new List<SatelliteComponent>();

        private Text _capitalBadgeText;
        private Transform _branchesContainer;

        public event Action OnRnDStateChanged;

        private static readonly ComponentCategory[] BranchCategories = new ComponentCategory[]
        {
            ComponentCategory.CorePlatform,
            ComponentCategory.PowerGeneration,
            ComponentCategory.EnergyStorage,
            ComponentCategory.CommsAndStorage,
            ComponentCategory.SciencePayload
        };

        private static readonly string[] BranchNames = new string[]
        {
            "BRANCH 1: SPACECRAFT BUS",
            "BRANCH 2: POWER SYSTEMS",
            "BRANCH 3: BATTERY STORAGE",
            "BRANCH 4: COMMS & SSD",
            "BRANCH 5: SCIENCE PAYLOADS"
        };

        public void Initialize(AgencyState agencyState, List<SatelliteComponent> allComponents)
        {
            _agencyState = agencyState ?? (GameManager.Instance != null
                ? GameManager.Instance.CurrentAgencyState
                : new AgencyState());

            _allComponents = (allComponents != null && allComponents.Count > 0)
                ? allComponents
                : BuilderPanelUI.BuildDefaultComponentsFromSpec();

            EnsureRnDUIHierarchy();
            RefreshRnDView();
        }

        [ContextMenu("Generate R&D UI In Scene")]
        public void BuildUIInEditor()
        {
            if (_agencyState == null) _agencyState = new AgencyState();
            if (_allComponents == null || _allComponents.Count == 0)
            {
                _allComponents = BuilderPanelUI.BuildDefaultComponentsFromSpec();
            }

            Transform existing = transform.Find("RnDContentRoot");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            EnsureRnDUIHierarchy();
            RefreshRnDView();
            Debug.Log("[RnDPanelUI] KSP-Style Branching Tech Tree UI generated!");
        }

        private void OnEnable()
        {
            if (_allComponents == null || _allComponents.Count == 0)
            {
                AgencyState state = GameManager.Instance != null
                    ? GameManager.Instance.CurrentAgencyState
                    : new AgencyState();
                Initialize(state, BuilderPanelUI.BuildDefaultComponentsFromSpec());
            }
            else
            {
                RefreshRnDView();
            }
        }

        public static bool IsComponentUnlocked(SatelliteComponent comp, AgencyState state)
        {
            if (comp == null) return false;
            if (comp.UnlockedByDefault || comp.UnlockCostThousands <= 0) return true;
            if (ContractHubUI.Instance != null && ContractHubUI.Instance.IsSandboxModeSelected) return true;
            return state != null && state.UnlockedComponentIds != null && state.UnlockedComponentIds.Contains(comp.Id);
        }

        public void TryUnlockChipTier(int targetSlots, int costThousands)
        {
            if (_agencyState == null) return;
            if (_agencyState.AutopilotSlotsUnlocked >= targetSlots) return;
            if (_agencyState.CapitalThousands < costThousands) return;

            _agencyState.CapitalThousands -= costThousands;
            _agencyState.AutopilotSlotsUnlocked = targetSlots;

            if (GameManager.Instance != null) GameManager.Instance.SaveAgencyState();

            RefreshRnDView();
            OnRnDStateChanged?.Invoke();
        }

        public void TryUnlockComponent(SatelliteComponent comp, SatelliteComponent prerequisite)
        {
            if (comp == null || _agencyState == null) return;
            if (IsComponentUnlocked(comp, _agencyState)) return;
            if (prerequisite != null && !IsComponentUnlocked(prerequisite, _agencyState)) return;
            if (_agencyState.CapitalThousands < comp.UnlockCostThousands) return;

            _agencyState.CapitalThousands -= comp.UnlockCostThousands;
            if (_agencyState.UnlockedComponentIds == null)
            {
                _agencyState.UnlockedComponentIds = new List<string>();
            }
            _agencyState.UnlockedComponentIds.Add(comp.Id);

            if (GameManager.Instance != null) GameManager.Instance.SaveAgencyState();

            RefreshRnDView();
            OnRnDStateChanged?.Invoke();
        }

        public void CloseModal()
        {
            gameObject.SetActive(false);
        }

        public void RefreshRnDView()
        {
            EnsureRnDUIHierarchy();

            if (_agencyState == null && GameManager.Instance != null)
            {
                _agencyState = GameManager.Instance.CurrentAgencyState;
            }
            if (_agencyState == null) _agencyState = new AgencyState();

            if (_capitalBadgeText != null)
            {
                _capitalBadgeText.text = string.Format("AGENCY CAPITAL: ${0:0}k   |   PASSIVE INCOME: +${1}k / mission",
                    _agencyState.CapitalThousands, _agencyState.TotalPassiveIncomeThousands);
            }

            RebuildTechTreeBranches();
        }

        private void EnsureRnDUIHierarchy()
        {
            Transform existingRoot = transform.Find("RnDContentRoot");
            if (existingRoot != null)
            {
                _capitalBadgeText = FindChild<Text>(existingRoot, "TopBar/CapitalBadge");
                Button closeBtn = FindChild<Button>(existingRoot, "TopBar/Btn_CloseRnD");
                if (closeBtn != null)
                {
                    closeBtn.onClick.RemoveAllListeners();
                    closeBtn.onClick.AddListener(CloseModal);
                }
                _branchesContainer = existingRoot.Find("TechTreeBranchesContainer");
                PixelUITheme.ApplyToHierarchy(existingRoot);
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Image rootBg = GetComponent<Image>();
            if (rootBg == null) rootBg = gameObject.AddComponent<Image>();
            rootBg.color = new Color(0.04f, 0.07f, 0.13f, 0.99f);

            GameObject rootObj = CreateUIObject("RnDContentRoot", transform);
            StretchFull(rootObj.GetComponent<RectTransform>());

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(26, 26, 18, 18);
            rootLayout.spacing = 10f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // 1. Top Header Bar
            GameObject topBar = CreateUIObject("TopBar", rootObj.transform);
            Image topBg = topBar.AddComponent<Image>();
            topBg.color = new Color(0.08f, 0.14f, 0.24f, 1f);
            LayoutElement topLe = topBar.AddComponent<LayoutElement>();
            topLe.preferredHeight = 58f;

            HorizontalLayoutGroup topLayout = topBar.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(18, 18, 8, 8);
            topLayout.spacing = 16f;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;

            Text title = CreateText("Title", topBar.transform, font,
                "R&D TECHNOLOGY TREE — Research nodes sequentially from left to right along each branch",
                20, FontStyle.Bold, new Color(1f, 0.88f, 0.42f, 1f), TextAnchor.MiddleLeft);
            LayoutElement titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;

            _capitalBadgeText = CreateText("CapitalBadge", topBar.transform, font,
                "AGENCY CAPITAL: $50k", 18, FontStyle.Bold,
                new Color(0.45f, 0.95f, 0.65f, 1f), TextAnchor.MiddleRight);
            LayoutElement capLe = _capitalBadgeText.gameObject.AddComponent<LayoutElement>();
            capLe.preferredWidth = 420f;

            CreateButton(topBar.transform, "Btn_CloseRnD", "BACK TO HANGAR [X]",
                new Color(0.58f, 0.18f, 0.20f, 1f), 210f, 42f, font, CloseModal);

            // 2. Container for the 6 Horizontal Tech Tree Branches
            GameObject branchesObj = CreateUIObject("TechTreeBranchesContainer", rootObj.transform);
            _branchesContainer = branchesObj.transform;
            LayoutElement branchesLe = branchesObj.AddComponent<LayoutElement>();
            branchesLe.flexibleHeight = 1f;

            VerticalLayoutGroup branchesLayout = branchesObj.AddComponent<VerticalLayoutGroup>();
            branchesLayout.spacing = 8f;
            branchesLayout.childControlWidth = true;
            branchesLayout.childControlHeight = true;
            branchesLayout.childForceExpandWidth = true;
            branchesLayout.childForceExpandHeight = true;

            PixelUITheme.ApplyToHierarchy(rootObj.transform);
        }

        private void RebuildTechTreeBranches()
        {
            if (_branchesContainer == null || _allComponents == null) return;

            for (int i = _branchesContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = _branchesContainer.GetChild(i).gameObject;
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

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Branches 1..5: Component Categories (sorted by unlock cost)
            for (int b = 0; b < BranchCategories.Length; b++)
            {
                ComponentCategory cat = BranchCategories[b];
                List<SatelliteComponent> branchNodes = new List<SatelliteComponent>();
                for (int i = 0; i < _allComponents.Count; i++)
                {
                    if (_allComponents[i] != null && _allComponents[i].Category == cat)
                    {
                        branchNodes.Add(_allComponents[i]);
                    }
                }
                branchNodes.Sort((a, c) => a.UnlockCostThousands.CompareTo(c.UnlockCostThousands));

                GameObject branchRow = CreateBranchRowObject("Branch_" + cat, BranchNames[b], _branchesContainer, font);
                Transform nodesTrack = branchRow.transform.Find("NodesTrack");

                for (int n = 0; n < branchNodes.Count; n++)
                {
                    SatelliteComponent comp = branchNodes[n];
                    SatelliteComponent prevComp = n > 0 ? branchNodes[n - 1] : null;

                    bool isUnlocked = IsComponentUnlocked(comp, _agencyState);
                    bool prevUnlocked = prevComp == null || IsComponentUnlocked(prevComp, _agencyState);

                    if (n > 0)
                    {
                        CreateConnectorLine(nodesTrack, isUnlocked, prevUnlocked, font);
                    }

                    BuildComponentTechNode(nodesTrack, comp, prevComp, n + 1, isUnlocked, prevUnlocked, font);
                }
            }

            // Branch 6: Onboard Autopilot Chip Progression (2 Slots ───► 4 Slots ───► 6 Slots)
            BuildAutopilotChipBranch(_branchesContainer, font);
            PixelUITheme.ApplyToHierarchy(_branchesContainer);
        }

        private static GameObject CreateBranchRowObject(string objName, string branchLabel, Transform parent, Font font)
        {
            GameObject rowObj = CreateUIObject(objName, parent);
            Image rowBg = rowObj.AddComponent<Image>();
            rowBg.color = new Color(0.06f, 0.10f, 0.18f, 0.94f);

            LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 136f;
            rowLe.flexibleHeight = 1f;

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(14, 14, 8, 8);
            rowLayout.spacing = 12f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;

            // Left Branch Header Label
            GameObject labelBox = CreateUIObject("BranchHeader", rowObj.transform);
            Image labelBg = labelBox.AddComponent<Image>();
            labelBg.color = new Color(0.09f, 0.16f, 0.28f, 1f);
            LayoutElement labelLe = labelBox.AddComponent<LayoutElement>();
            labelLe.preferredWidth = 225f;

            VerticalLayoutGroup labelV = labelBox.AddComponent<VerticalLayoutGroup>();
            labelV.padding = new RectOffset(12, 12, 8, 8);
            labelV.childControlWidth = true;
            labelV.childControlHeight = true;
            labelV.childForceExpandWidth = true;
            labelV.childForceExpandHeight = true;

            CreateText("BranchTitle", labelBox.transform, font, branchLabel, 15, FontStyle.Bold,
                new Color(0.58f, 0.85f, 1f, 1f), TextAnchor.MiddleLeft);

            // Horizontal Track for Nodes + Connecting Lines
            GameObject trackObj = CreateUIObject("NodesTrack", rowObj.transform);
            LayoutElement trackLe = trackObj.AddComponent<LayoutElement>();
            trackLe.flexibleWidth = 1f;

            HorizontalLayoutGroup trackLayout = trackObj.AddComponent<HorizontalLayoutGroup>();
            trackLayout.spacing = 6f;
            trackLayout.childAlignment = TextAnchor.MiddleLeft;
            trackLayout.childControlWidth = true;
            trackLayout.childControlHeight = true;
            trackLayout.childForceExpandWidth = true;
            trackLayout.childForceExpandHeight = true;

            return rowObj;
        }

        private static void CreateConnectorLine(Transform parent, bool targetUnlocked, bool prevUnlocked, Font font)
        {
            GameObject connObj = CreateUIObject("TreeConnectorLine", parent);
            LayoutElement le = connObj.AddComponent<LayoutElement>();
            le.preferredWidth = 42f;
            le.flexibleWidth = 0f;

            VerticalLayoutGroup vLayout = connObj.AddComponent<VerticalLayoutGroup>();
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;

            Color lineColor = targetUnlocked
                ? new Color(0.25f, 0.82f, 0.45f, 1f)
                : (prevUnlocked ? new Color(0.95f, 0.75f, 0.25f, 1f) : new Color(0.28f, 0.34f, 0.44f, 0.8f));

            Text arrow = CreateText("LineArrow", connObj.transform, font, "───►", 18, FontStyle.Bold, lineColor, TextAnchor.MiddleCenter);
            RectTransform rt = arrow.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(42f, 30f);
        }

        private void BuildComponentTechNode(
            Transform parent,
            SatelliteComponent comp,
            SatelliteComponent prevComp,
            int tierIndex,
            bool isUnlocked,
            bool prevUnlocked,
            Font font)
        {
            bool canAfford = _agencyState != null && _agencyState.CapitalThousands >= comp.UnlockCostThousands;
            bool canResearchNow = !isUnlocked && prevUnlocked && canAfford;

            GameObject nodeObj = CreateUIObject("TechNode_" + comp.Id, parent);
            Image nodeBg = nodeObj.AddComponent<Image>();

            if (isUnlocked)
                nodeBg.color = new Color(0.09f, 0.24f, 0.17f, 0.96f);
            else if (prevUnlocked)
                nodeBg.color = new Color(0.11f, 0.18f, 0.30f, 0.96f);
            else
                nodeBg.color = new Color(0.06f, 0.08f, 0.13f, 0.88f);

            LayoutElement nodeLe = nodeObj.AddComponent<LayoutElement>();
            nodeLe.flexibleWidth = 1f;
            nodeLe.preferredWidth = 340f;

            VerticalLayoutGroup nodeV = nodeObj.AddComponent<VerticalLayoutGroup>();
            nodeV.padding = new RectOffset(10, 10, 8, 8);
            nodeV.spacing = 6f;
            nodeV.childControlWidth = true;
            nodeV.childControlHeight = true;
            nodeV.childForceExpandWidth = true;
            nodeV.childForceExpandHeight = false;

            // Top row inside node: [Icon Slot] + [Name & Short Stats]
            GameObject topRow = CreateUIObject("NodeTopRow", nodeObj.transform);
            LayoutElement topLe = topRow.AddComponent<LayoutElement>();
            topLe.preferredHeight = 64f;
            topLe.flexibleHeight = 1f;

            HorizontalLayoutGroup topH = topRow.AddComponent<HorizontalLayoutGroup>();
            topH.spacing = 10f;
            topH.childAlignment = TextAnchor.MiddleLeft;
            topH.childControlWidth = true;
            topH.childControlHeight = true;
            topH.childForceExpandWidth = false;

            // Dedicated Icon Slot for Sprites (Night Sprint N-01)
            CreateIconSlot(topRow.transform, "PartIconSlot_" + comp.Id, "ICON", 54f, font,
                isUnlocked ? new Color(0.14f, 0.36f, 0.24f, 1f) : new Color(0.10f, 0.15f, 0.24f, 1f));

            GameObject infoCol = CreateUIObject("NodeInfo", topRow.transform);
            LayoutElement infoLe = infoCol.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f;

            VerticalLayoutGroup infoV = infoCol.AddComponent<VerticalLayoutGroup>();
            infoV.spacing = 2f;
            infoV.childControlWidth = true;
            infoV.childControlHeight = true;
            infoV.childForceExpandWidth = true;
            infoV.childForceExpandHeight = false;

            CreateText("NodeTitle", infoCol.transform, font,
                string.Format("T{0}: {1}", tierIndex, comp.Name), 15, FontStyle.Bold,
                isUnlocked ? new Color(0.55f, 0.98f, 0.70f, 1f) : new Color(1f, 0.90f, 0.45f, 1f),
                TextAnchor.MiddleLeft);

            CreateText("NodeStats", infoCol.transform, font,
                BuilderPanelUI.FormatComponentShortStats(comp), 12, FontStyle.Normal,
                new Color(0.80f, 0.88f, 0.96f, 1f), TextAnchor.UpperLeft);

            // Action Button at bottom of node
            string btnText;
            Color btnColor;
            if (isUnlocked)
            {
                btnText = "✓ UNLOCKED";
                btnColor = new Color(0.15f, 0.44f, 0.26f, 1f);
            }
            else if (!prevUnlocked)
            {
                btnText = "LOCKED (Requires T" + (tierIndex - 1) + ")";
                btnColor = new Color(0.18f, 0.20f, 0.26f, 1f);
            }
            else if (canAfford)
            {
                btnText = string.Format("RESEARCH (${0}k)", comp.UnlockCostThousands);
                btnColor = new Color(0.58f, 0.38f, 0.10f, 1f);
            }
            else
            {
                btnText = string.Format("NEED ${0}k CAPITAL", comp.UnlockCostThousands);
                btnColor = new Color(0.32f, 0.22f, 0.22f, 1f);
            }

            SatelliteComponent capturedComp = comp;
            SatelliteComponent capturedPrev = prevComp;
            Button btn = CreateButton(nodeObj.transform, "Btn_NodeAction", btnText, btnColor, 0f, 32f, font,
                () => TryUnlockComponent(capturedComp, capturedPrev));
            btn.interactable = canResearchNow;
        }

        private void BuildAutopilotChipBranch(Transform parent, Font font)
        {
            GameObject branchRow = CreateBranchRowObject("Branch_AutopilotChip", "BRANCH 6: AUTOPILOT CPU", parent, font);
            Transform track = branchRow.transform.Find("NodesTrack");

            int unlockedSlots = _agencyState != null ? _agencyState.AutopilotSlotsUnlocked : 2;
            float capital = _agencyState != null ? _agencyState.CapitalThousands : 50f;

            // Tier 1: 2 Slots (Default)
            BuildChipNode(track, "T1: Basic 2-Slot CPU", "2 IF-THEN-ELSE rules for LEO CubeSats.",
                true, true, 0, 2, capital, font);

            CreateConnectorLine(track, unlockedSlots >= 4, true, font);

            // Tier 2: 4 Slots ($40k)
            BuildChipNode(track, "T2: Lunar 4-Slot CPU", "Expands memory to 4 autopilot rules for Lunar orbit.",
                unlockedSlots >= 4, true, GameManager.UpgradeTo4SlotsCostThousands, 4, capital, font);

            CreateConnectorLine(track, unlockedSlots >= 6, unlockedSlots >= 4, font);

            // Tier 3: 6 Slots ($80k)
            BuildChipNode(track, "T3: DeepSpace 6-Slot CPU", "Full 6-rule processor for Mars CME storm protection.",
                unlockedSlots >= 6, unlockedSlots >= 4, GameManager.UpgradeTo6SlotsCostThousands, 6, capital, font);
        }

        private void BuildChipNode(
            Transform parent,
            string title,
            string desc,
            bool isUnlocked,
            bool prevUnlocked,
            int costThousands,
            int targetSlots,
            float capital,
            Font font)
        {
            bool canAfford = capital >= costThousands;
            bool canResearch = !isUnlocked && prevUnlocked && canAfford;

            GameObject nodeObj = CreateUIObject("ChipNode_" + targetSlots, parent);
            Image nodeBg = nodeObj.AddComponent<Image>();
            nodeBg.color = isUnlocked
                ? new Color(0.09f, 0.24f, 0.17f, 0.96f)
                : (prevUnlocked ? new Color(0.11f, 0.18f, 0.30f, 0.96f) : new Color(0.06f, 0.08f, 0.13f, 0.88f));

            LayoutElement nodeLe = nodeObj.AddComponent<LayoutElement>();
            nodeLe.flexibleWidth = 1f;
            nodeLe.preferredWidth = 340f;

            VerticalLayoutGroup nodeV = nodeObj.AddComponent<VerticalLayoutGroup>();
            nodeV.padding = new RectOffset(10, 10, 8, 8);
            nodeV.spacing = 6f;
            nodeV.childControlWidth = true;
            nodeV.childControlHeight = true;
            nodeV.childForceExpandWidth = true;
            nodeV.childForceExpandHeight = false;

            GameObject topRow = CreateUIObject("NodeTopRow", nodeObj.transform);
            LayoutElement topLe = topRow.AddComponent<LayoutElement>();
            topLe.preferredHeight = 64f;
            topLe.flexibleHeight = 1f;

            HorizontalLayoutGroup topH = topRow.AddComponent<HorizontalLayoutGroup>();
            topH.spacing = 10f;
            topH.childAlignment = TextAnchor.MiddleLeft;
            topH.childControlWidth = true;
            topH.childControlHeight = true;
            topH.childForceExpandWidth = false;

            CreateIconSlot(topRow.transform, "ChipIconSlot_" + targetSlots, "CPU", 54f, font,
                new Color(0.22f, 0.16f, 0.38f, 1f));

            GameObject infoCol = CreateUIObject("NodeInfo", topRow.transform);
            LayoutElement infoLe = infoCol.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f;

            VerticalLayoutGroup infoV = infoCol.AddComponent<VerticalLayoutGroup>();
            infoV.spacing = 2f;
            infoV.childControlWidth = true;
            infoV.childControlHeight = true;
            infoV.childForceExpandWidth = true;
            infoV.childForceExpandHeight = false;

            CreateText("NodeTitle", infoCol.transform, font, title, 15, FontStyle.Bold,
                isUnlocked ? new Color(0.55f, 0.98f, 0.70f, 1f) : new Color(1f, 0.90f, 0.45f, 1f),
                TextAnchor.MiddleLeft);

            CreateText("NodeDesc", infoCol.transform, font, desc, 13, FontStyle.Normal,
                new Color(0.80f, 0.88f, 0.96f, 1f), TextAnchor.UpperLeft);

            string btnText = isUnlocked
                ? "✓ UNLOCKED"
                : (!prevUnlocked ? "LOCKED (Requires Prev CPU)" : string.Format("UPGRADE (${0}k)", costThousands));

            Color btnColor = isUnlocked
                ? new Color(0.15f, 0.44f, 0.26f, 1f)
                : (canResearch ? new Color(0.58f, 0.38f, 0.10f, 1f) : new Color(0.22f, 0.22f, 0.28f, 1f));

            int capturedTarget = targetSlots;
            int capturedCost = costThousands;
            Button btn = CreateButton(nodeObj.transform, "Btn_UpgradeChip_" + targetSlots, btnText, btnColor, 0f, 32f, font,
                () => TryUnlockChipTier(capturedTarget, capturedCost));
            btn.interactable = canResearch;
        }

        private static GameObject CreateIconSlot(Transform parent, string name, string placeholderTag, float size, Font font, Color frameColor)
        {
            GameObject iconFrame = CreateUIObject(name, parent);
            Image frameImg = iconFrame.AddComponent<Image>();
            frameImg.color = frameColor;

            LayoutElement le = iconFrame.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            le.minWidth = size;
            le.minHeight = size;

            // Child Image where artist/developer can drag-and-drop the actual Sprite in Inspector
            GameObject spriteObj = CreateUIObject("IconSprite", iconFrame.transform);
            StretchFull(spriteObj.GetComponent<RectTransform>());
            Image spriteImg = spriteObj.AddComponent<Image>();
            spriteImg.preserveAspect = true;
            spriteImg.color = new Color(1f, 1f, 1f, 0.15f);

            Text tagTxt = CreateText("PlaceholderTag", iconFrame.transform, font, placeholderTag, 11, FontStyle.Bold,
                new Color(0.75f, 0.88f, 1f, 0.65f), TextAnchor.MiddleCenter);
            StretchFull(tagTxt.GetComponent<RectTransform>());
            return iconFrame;
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
