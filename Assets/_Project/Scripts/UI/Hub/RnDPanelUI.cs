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
        private RectTransform _viewportRt;
        private RectTransform _canvasRt;
        private Text _zoomBadgeText;

        private float _currentZoom = 0.82f;
        private float _targetZoom = 0.82f;
        private Vector2 _currentPan = Vector2.zero;
        private Vector2 _targetPan = Vector2.zero;
        private bool _isDragging;
        private bool _didDragThisPress;
        private float _dragDistance;
        private bool _hasInitializedView;
        private Vector2 _lastMousePos;
        private const float MinZoom = 0.50f;
        private const float MaxZoom = 2.20f;

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
            _isDragging = false;
            _didDragThisPress = false;
            _dragDistance = 0f;
            _hasInitializedView = false;
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

        private void Update()
        {
            if (_viewportRt == null || _canvasRt == null) return;

            Camera uiCam = GetCanvasCamera();
            bool pointerInViewport = RectTransformUtility.RectangleContainsScreenPoint(_viewportRt, Input.mousePosition, uiCam);

            // 1. Scroll-wheel zoom toward cursor
            float minZoom = GetMinZoom();
            float scroll = Input.mouseScrollDelta.y;
            if (pointerInViewport && Mathf.Abs(scroll) > 0.001f)
            {
                float oldTargetZoom = _targetZoom;
                float factor = scroll > 0f ? 1.14f : (1f / 1.14f);
                _targetZoom = Mathf.Clamp(_targetZoom * factor, minZoom, MaxZoom);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewportRt, Input.mousePosition, uiCam, out Vector2 localCursor))
                {
                    float ratio = _targetZoom / Mathf.Max(0.01f, oldTargetZoom);
                    _targetPan = localCursor - (localCursor - _targetPan) * ratio;
                }
            }

            // 2. Drag to pan (Left, Middle, or Right mouse button inside viewport)
            bool dragDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            bool dragHeld = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (dragDown && pointerInViewport)
            {
                _isDragging = true;
                _didDragThisPress = false;
                _dragDistance = 0f;
                _lastMousePos = Input.mousePosition;
            }
            else if (!dragHeld)
            {
                _isDragging = false;
            }

            if (_isDragging && dragHeld)
            {
                Vector2 curMouse = Input.mousePosition;
                Vector2 delta = curMouse - _lastMousePos;
                _lastMousePos = curMouse;

                if (delta.sqrMagnitude > 0.01f)
                {
                    _dragDistance += delta.magnitude;
                    if (_dragDistance > 6f)
                    {
                        _didDragThisPress = true;
                    }
                    float scaleFactor = GetCanvasScaleFactor();
                    Vector2 scaledDelta = delta / Mathf.Max(0.01f, scaleFactor);
                    _targetPan += scaledDelta;
                    ClampPan(ref _targetPan, _targetZoom);
                    _currentPan = _targetPan;
                }
            }

            // 3. Keyboard pan & zoom shortcuts
            float panSpeed = 750f * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) _targetPan.x += panSpeed;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) _targetPan.x -= panSpeed;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) _targetPan.y -= panSpeed;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) _targetPan.y += panSpeed;
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) AdjustZoom(0.20f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) AdjustZoom(-0.20f);
            if (Input.GetKeyDown(KeyCode.F)) FitTreeToViewport();
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) ResetView();

            ClampPan(ref _targetPan, _targetZoom);
            ClampPan(ref _currentPan, _currentZoom);
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, MaxZoom);

            // 4. Smooth interpolation
            float t = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, t);
            _currentPan = Vector2.Lerp(_currentPan, _targetPan, t);

            _canvasRt.localScale = new Vector3(_currentZoom, _currentZoom, 1f);
            _canvasRt.anchoredPosition = _currentPan;

            if (_zoomBadgeText != null)
            {
                _zoomBadgeText.text = string.Format("{0:0}%", _currentZoom * 100f);
            }
        }

        private Camera GetCanvasCamera()
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c == null || c.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return c.worldCamera;
        }

        private float GetCanvasScaleFactor()
        {
            Canvas c = GetComponentInParent<Canvas>();
            return c != null ? c.scaleFactor : 1f;
        }

        private void ClampPan(ref Vector2 pan, float zoom)
        {
            if (_viewportRt == null || _canvasRt == null) return;
            Rect vp = _viewportRt.rect;
            Rect content = _canvasRt.rect;
            if (vp.width <= 1f || vp.height <= 1f || content.width <= 1f || content.height <= 1f) return;
            float scaledW = content.width * zoom;
            float scaledH = content.height * zoom;
            float maxPanX = scaledW > vp.width ? (scaledW - vp.width) * 0.5f + 24f : 0f;
            float maxPanY = scaledH > vp.height ? (scaledH - vp.height) * 0.5f + 24f : 0f;
            pan.x = Mathf.Clamp(pan.x, -maxPanX, maxPanX);
            pan.y = Mathf.Clamp(pan.y, -maxPanY, maxPanY);
        }

        private float GetMinZoom()
        {
            if (_viewportRt == null || _canvasRt == null) return MinZoom;
            Rect vp = _viewportRt.rect;
            Rect content = _canvasRt.rect;
            if (content.width > 10f && content.height > 10f && vp.width > 10f && vp.height > 10f)
            {
                float zx = (vp.width - 40f) / content.width;
                float zy = (vp.height - 40f) / content.height;
                return Mathf.Clamp(Mathf.Min(zx, zy), MinZoom, 1.15f);
            }
            return 0.78f;
        }

        public void AdjustZoom(float delta)
        {
            _targetZoom = Mathf.Clamp(_targetZoom + delta, GetMinZoom(), MaxZoom);
        }

        public void ResetView()
        {
            _targetZoom = Mathf.Max(1.0f, GetMinZoom());
            _targetPan = Vector2.zero;
        }

        public void FitTreeToViewport()
        {
            if (_canvasRt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_canvasRt);
            }
            _targetZoom = GetMinZoom();
            _targetPan = Vector2.zero;
        }

        private void FocusOnRect(RectTransform targetRt, float desiredZoom = 1.25f)
        {
            if (_didDragThisPress || targetRt == null || _canvasRt == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_canvasRt);
            Vector2 localCenter = _canvasRt.InverseTransformPoint(targetRt.TransformPoint(targetRt.rect.center));
            _targetZoom = Mathf.Clamp(desiredZoom, GetMinZoom(), MaxZoom);
            _targetPan = -localCenter * _targetZoom;
            ClampPan(ref _targetPan, _targetZoom);
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
                TooltipTrigger.Attach(_capitalBadgeText.gameObject, "rnd_capital");
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
                Transform existingVp = existingRoot.Find("TechTreeViewport");
                if (existingVp != null)
                {
                    _capitalBadgeText = FindChild<Text>(existingRoot, "TopBar/CapitalGroup/CapitalBadge")
                        ?? FindChild<Text>(existingRoot, "TopBar/CapitalBadge");
                    _zoomBadgeText = FindChild<Text>(existingRoot, "TopBar/ZoomControls/Btn_ZoomReset/Text");
                    Button closeBtn = FindChild<Button>(existingRoot, "TopBar/Btn_CloseRnD");
                    if (closeBtn != null)
                    {
                        closeBtn.onClick.RemoveAllListeners();
                        closeBtn.onClick.AddListener(CloseModal);
                    }
                    _viewportRt = existingVp.GetComponent<RectTransform>();
                    _branchesContainer = existingVp.Find("TechTreeBranchesContainer");
                    _canvasRt = _branchesContainer != null ? _branchesContainer.GetComponent<RectTransform>() : null;
                    PixelUITheme.ApplyToHierarchy(existingRoot);
                    return;
                }
                if (Application.isPlaying) Destroy(existingRoot.gameObject);
                else DestroyImmediate(existingRoot.gameObject);
            }

            Font font = AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.GetPixelFont();

            Image rootBg = GetComponent<Image>();
            if (rootBg == null) rootBg = gameObject.AddComponent<Image>();
            rootBg.color = new Color(0.03f, 0.05f, 0.10f, 1f);

            GameObject rootObj = CreateUIObject("RnDContentRoot", transform);
            StretchFull(rootObj.GetComponent<RectTransform>());

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(20, 20, 14, 14);
            rootLayout.spacing = 8f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // 1. Top Header Bar
            GameObject topBar = CreateUIObject("TopBar", rootObj.transform);
            Image topBg = topBar.AddComponent<Image>();
            topBg.color = new Color(0.08f, 0.14f, 0.24f, 1f);
            LayoutElement topLe = topBar.AddComponent<LayoutElement>();
            topLe.minHeight = 54f;
            topLe.preferredHeight = 54f;
            topLe.flexibleHeight = 0f;

            HorizontalLayoutGroup topLayout = topBar.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(20, 18, 7, 7);
            topLayout.spacing = 12f;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;

            Text title = CreateText("Title", topBar.transform, font,
                "R&D TECHNOLOGY TREE",
                18, FontStyle.Bold, new Color(1f, 0.88f, 0.42f, 1f), TextAnchor.MiddleLeft);
            LayoutElement titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;

            GameObject capGroup = CreateUIObject("CapitalGroup", topBar.transform);
            LayoutElement capGroupLe = capGroup.AddComponent<LayoutElement>();
            capGroupLe.preferredWidth = 420f;
            HorizontalLayoutGroup capH = capGroup.AddComponent<HorizontalLayoutGroup>();
            capH.spacing = 8f;
            capH.childAlignment = TextAnchor.MiddleRight;
            capH.childControlWidth = true;
            capH.childControlHeight = true;
            capH.childForceExpandWidth = false;

            PixelUITheme.EnsureAssetsLoaded();
            if (PixelUITheme.CoinSprite != null)
            {
                GameObject coinObj = CreateUIObject("CapitalCoinIcon", capGroup.transform);
                LayoutElement coinLe = coinObj.AddComponent<LayoutElement>();
                coinLe.preferredWidth = 24f;
                coinLe.preferredHeight = 24f;
                Image coinImg = coinObj.AddComponent<Image>();
                coinImg.sprite = PixelUITheme.CoinSprite;
                coinImg.preserveAspect = true;
                coinImg.raycastTarget = false;
            }

            _capitalBadgeText = CreateText("CapitalBadge", capGroup.transform, font,
                "AGENCY CAPITAL: $50k", 17, FontStyle.Bold,
                new Color(0.45f, 0.95f, 0.65f, 1f), TextAnchor.MiddleRight);
            LayoutElement capLe = _capitalBadgeText.gameObject.AddComponent<LayoutElement>();
            capLe.flexibleWidth = 1f;

            // Zoom Controls (- | 100% | + | FIT)
            GameObject zoomGroup = CreateUIObject("ZoomControls", topBar.transform);
            LayoutElement zoomGroupLe = zoomGroup.AddComponent<LayoutElement>();
            zoomGroupLe.preferredHeight = 38f;
            zoomGroupLe.flexibleHeight = 0f;
            zoomGroupLe.flexibleWidth = 0f;
            HorizontalLayoutGroup zoomHl = zoomGroup.AddComponent<HorizontalLayoutGroup>();
            zoomHl.spacing = 5f;
            zoomHl.childAlignment = TextAnchor.MiddleCenter;
            zoomHl.childControlWidth = true;
            zoomHl.childControlHeight = true;
            zoomHl.childForceExpandWidth = false;
            zoomHl.childForceExpandHeight = false;

            CreateButton(zoomGroup.transform, "Btn_ZoomOut", "-",
                new Color(0.14f, 0.24f, 0.38f, 1f), 40f, 38f, font, () => AdjustZoom(-0.22f));
            Button resetZoomBtn = CreateButton(zoomGroup.transform, "Btn_ZoomReset", "82%",
                new Color(0.11f, 0.19f, 0.32f, 1f), 68f, 38f, font, ResetView);
            _zoomBadgeText = resetZoomBtn.GetComponentInChildren<Text>();
            CreateButton(zoomGroup.transform, "Btn_ZoomIn", "+",
                new Color(0.14f, 0.24f, 0.38f, 1f), 40f, 38f, font, () => AdjustZoom(0.22f));
            CreateButton(zoomGroup.transform, "Btn_ZoomFit", "FIT",
                new Color(0.16f, 0.42f, 0.32f, 1f), 58f, 38f, font, FitTreeToViewport);

            CreateButton(topBar.transform, "Btn_CloseRnD", "BACK TO HANGAR [X]",
                new Color(0.58f, 0.18f, 0.20f, 1f), 200f, 40f, font, CloseModal);

            // 2. Pannable & Zoomable Viewport for the 6 Horizontal Tech Tree Branches
            GameObject viewportObj = CreateUIObject("TechTreeViewport", rootObj.transform);
            _viewportRt = viewportObj.GetComponent<RectTransform>();
            LayoutElement vpLe = viewportObj.AddComponent<LayoutElement>();
            vpLe.flexibleHeight = 1f;
            Image vpBg = viewportObj.AddComponent<Image>();
            vpBg.color = new Color(0.02f, 0.04f, 0.08f, 1f);
            viewportObj.AddComponent<RectMask2D>();

            GameObject starBgObj = CreateUIObject("RnDStarfieldBackdrop", viewportObj.transform);
            StretchFull(starBgObj.GetComponent<RectTransform>());
            AstraLink.UI.Flight.SpaceMap.PixelStarfieldLayer starLayer = starBgObj.AddComponent<AstraLink.UI.Flight.SpaceMap.PixelStarfieldLayer>();
            starLayer.EnsureBuilt();

            GameObject branchesObj = CreateUIObject("TechTreeBranchesContainer", viewportObj.transform);
            _branchesContainer = branchesObj.transform;
            _canvasRt = branchesObj.GetComponent<RectTransform>();
            _canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
            _canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
            _canvasRt.pivot = new Vector2(0.5f, 0.5f);
            _canvasRt.anchoredPosition = Vector2.zero;

            ContentSizeFitter csf = branchesObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            VerticalLayoutGroup branchesLayout = branchesObj.AddComponent<VerticalLayoutGroup>();
            branchesLayout.padding = new RectOffset(28, 28, 24, 24);
            branchesLayout.spacing = 14f;
            branchesLayout.childControlWidth = true;
            branchesLayout.childControlHeight = true;
            branchesLayout.childForceExpandWidth = true;
            branchesLayout.childForceExpandHeight = false;

            // Controls Hint Overlay
            GameObject hintObj = CreateUIObject("ControlsHint", viewportObj.transform);
            RectTransform hintRt = hintObj.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(0f, 0f);
            hintRt.pivot = new Vector2(0f, 0f);
            hintRt.anchoredPosition = new Vector2(14f, 12f);
            hintRt.sizeDelta = new Vector2(620f, 28f);
            Image hintBg = hintObj.AddComponent<Image>();
            hintBg.color = new Color(0.04f, 0.08f, 0.15f, 0.85f);
            hintBg.raycastTarget = false;
            Text hintTxt = CreateText("HintText", hintObj.transform, font,
                "DRAG: PAN  •  SCROLL: ZOOM  •  WASD: MOVE  •  CLICK BRANCH/CARD: FOCUS  •  F: FIT",
                12, FontStyle.Bold, new Color(0.72f, 0.86f, 0.98f, 1f), TextAnchor.MiddleCenter);
            hintTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            hintTxt.raycastTarget = false;
            StretchFull(hintTxt.GetComponent<RectTransform>());

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

            Font font = AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.GetPixelFont();

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
            ApplyRnDCardFrames(_branchesContainer);
            if (_canvasRt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_canvasRt);
                if (!_hasInitializedView)
                {
                    _hasInitializedView = true;
                    FitTreeToViewport();
                    _currentZoom = _targetZoom;
                    _currentPan = _targetPan;
                    _canvasRt.localScale = new Vector3(_currentZoom, _currentZoom, 1f);
                    _canvasRt.anchoredPosition = _currentPan;
                }
            }
        }

        private static void ApplyRnDCardFrames(Transform container)
        {
            if (container == null) return;
            AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank art = AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.Load();
            if (art == null) return;

            for (int i = 0; i < container.childCount; i++)
            {
                Transform row = container.GetChild(i);
                if (art.inventoryPanel != null)
                {
                    Image rowImg = row.GetComponent<Image>();
                    if (rowImg != null)
                    {
                        AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.ApplyPanelFrame(
                            rowImg, art.inventoryPanel, new Vector4(12f, 12f, 12f, 12f), new Color(0.18f, 0.24f, 0.38f, 0.96f));
                    }
                }

                Transform header = row.Find("BranchHeader");
                if (header != null && art.dialogueBox != null)
                {
                    Image hdrImg = header.GetComponent<Image>();
                    if (hdrImg != null)
                    {
                        AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.ApplyPanelFrame(
                            hdrImg, art.dialogueBox, new Vector4(10f, 10f, 10f, 10f), new Color(0.16f, 0.28f, 0.48f, 0.98f));
                    }
                    Text hdrTxt = header.GetComponentInChildren<Text>();
                    if (hdrTxt != null)
                    {
                        hdrTxt.color = new Color(0.84f, 0.95f, 1f, 1f);
                    }
                }

                Transform track = row.Find("NodesTrack");
                if (track == null || art.dialogueBox == null) continue;
                for (int j = 0; j < track.childCount; j++)
                {
                    Transform node = track.GetChild(j);
                    if (!node.name.StartsWith("TechNode_") && !node.name.StartsWith("ChipNode_")) continue;
                    Image nodeImg = node.GetComponent<Image>();
                    if (nodeImg == null) continue;

                    Transform btnTr = node.Find("Btn_NodeAction") ?? node.Find("Btn_UpgradeChip_2") ?? node.Find("Btn_UpgradeChip_4") ?? node.Find("Btn_UpgradeChip_6");
                    string btnLabel = btnTr != null ? (btnTr.GetComponentInChildren<Text>()?.text ?? string.Empty) : string.Empty;
                    Color tint;
                    if (btnLabel == "UNLOCKED")
                        tint = new Color(0.14f, 0.38f, 0.28f, 0.98f);
                    else if (btnLabel.StartsWith("RESEARCH") || btnLabel.StartsWith("UPGRADE"))
                        tint = new Color(0.22f, 0.34f, 0.56f, 0.98f);
                    else
                        tint = new Color(0.12f, 0.15f, 0.24f, 0.94f);

                    AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.ApplyPanelFrame(
                        nodeImg, art.dialogueBox, new Vector4(10f, 10f, 10f, 10f), tint);
                }
            }
        }

        private GameObject CreateBranchRowObject(string objName, string branchLabel, Transform parent, Font font)
        {
            GameObject rowObj = CreateUIObject(objName, parent);
            Image rowBg = rowObj.AddComponent<Image>();
            rowBg.color = new Color(0.06f, 0.10f, 0.18f, 0.94f);

            LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.minHeight = 172f;
            rowLe.preferredHeight = 172f;
            rowLe.flexibleHeight = 0f;

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 10, 10);
            rowLayout.spacing = 14f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;

            // Left Branch Header Label (click to focus branch)
            GameObject labelBox = CreateUIObject("BranchHeader", rowObj.transform);
            Image labelBg = labelBox.AddComponent<Image>();
            labelBg.color = new Color(0.09f, 0.16f, 0.28f, 1f);
            Button labelBtn = labelBox.AddComponent<Button>();
            labelBtn.targetGraphic = labelBg;
            RectTransform rowRt = rowObj.GetComponent<RectTransform>();
            labelBtn.onClick.AddListener(() => FocusOnRect(rowRt, 1.15f));
            LayoutElement labelLe = labelBox.AddComponent<LayoutElement>();
            labelLe.minWidth = 260f;
            labelLe.preferredWidth = 260f;
            labelLe.flexibleWidth = 0f;

            VerticalLayoutGroup labelV = labelBox.AddComponent<VerticalLayoutGroup>();
            labelV.padding = new RectOffset(14, 14, 10, 10);
            labelV.childControlWidth = true;
            labelV.childControlHeight = true;
            labelV.childForceExpandWidth = true;
            labelV.childForceExpandHeight = true;

            CreateText("BranchTitle", labelBox.transform, font, branchLabel, 17, FontStyle.Bold,
                new Color(0.58f, 0.85f, 1f, 1f), TextAnchor.MiddleLeft);

            // Horizontal Track for Nodes + Connecting Lines
            GameObject trackObj = CreateUIObject("NodesTrack", rowObj.transform);
            LayoutElement trackLe = trackObj.AddComponent<LayoutElement>();
            trackLe.flexibleWidth = 0f;

            HorizontalLayoutGroup trackLayout = trackObj.AddComponent<HorizontalLayoutGroup>();
            trackLayout.spacing = 8f;
            trackLayout.childAlignment = TextAnchor.MiddleLeft;
            trackLayout.childControlWidth = true;
            trackLayout.childControlHeight = true;
            trackLayout.childForceExpandWidth = false;
            trackLayout.childForceExpandHeight = true;

            return rowObj;
        }

        private static void CreateConnectorLine(Transform parent, bool targetUnlocked, bool prevUnlocked, Font font)
        {
            GameObject connObj = CreateUIObject("TreeConnectorLine", parent);
            LayoutElement le = connObj.AddComponent<LayoutElement>();
            le.minWidth = 32f;
            le.preferredWidth = 36f;
            le.flexibleWidth = 0f;

            HorizontalLayoutGroup hLayout = connObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(2, 2, 0, 0);
            hLayout.spacing = 0f;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            Color lineColor = targetUnlocked
                ? new Color(0.25f, 0.82f, 0.45f, 1f)
                : (prevUnlocked ? new Color(0.95f, 0.75f, 0.25f, 1f) : new Color(0.32f, 0.38f, 0.48f, 0.9f));

            GameObject lineObj = CreateUIObject("ConnectorBar", connObj.transform);
            Image lineImg = lineObj.AddComponent<Image>();
            lineImg.color = lineColor;
            lineImg.raycastTarget = false;
            LayoutElement lineLe = lineObj.AddComponent<LayoutElement>();
            lineLe.flexibleWidth = 1f;
            lineLe.minWidth = 14f;
            lineLe.preferredHeight = 3f;

            Text arrow = CreateText("LineArrow", connObj.transform, font, ">", 14, FontStyle.Bold, lineColor, TextAnchor.MiddleLeft);
            arrow.horizontalOverflow = HorizontalWrapMode.Overflow;
            arrow.verticalOverflow = VerticalWrapMode.Overflow;
            LayoutElement arrowLe = arrow.gameObject.AddComponent<LayoutElement>();
            arrowLe.preferredWidth = 10f;
            arrowLe.preferredHeight = 16f;
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
            TooltipTrigger.AttachDynamic(nodeObj, () => DescriptionTips.GetComponentTip(comp));
            Image nodeBg = nodeObj.AddComponent<Image>();

            if (isUnlocked)
                nodeBg.color = new Color(0.09f, 0.24f, 0.17f, 0.96f);
            else if (prevUnlocked)
                nodeBg.color = new Color(0.11f, 0.18f, 0.30f, 0.96f);
            else
                nodeBg.color = new Color(0.06f, 0.08f, 0.13f, 0.88f);

            LayoutElement nodeLe = nodeObj.AddComponent<LayoutElement>();
            nodeLe.flexibleWidth = 0f;
            nodeLe.minWidth = 410f;
            nodeLe.preferredWidth = 410f;

            Button focusNodeBtn = nodeObj.AddComponent<Button>();
            focusNodeBtn.targetGraphic = nodeBg;
            focusNodeBtn.transition = Selectable.Transition.None;
            RectTransform nodeRt = nodeObj.GetComponent<RectTransform>();
            focusNodeBtn.onClick.AddListener(() => FocusOnRect(nodeRt, 1.45f));

            VerticalLayoutGroup nodeV = nodeObj.AddComponent<VerticalLayoutGroup>();
            nodeV.padding = new RectOffset(14, 14, 10, 10);
            nodeV.spacing = 6f;
            nodeV.childControlWidth = true;
            nodeV.childControlHeight = true;
            nodeV.childForceExpandWidth = true;
            nodeV.childForceExpandHeight = false;

            // Top row inside node: [Icon Slot] + [Name & Short Stats]
            GameObject topRow = CreateUIObject("NodeTopRow", nodeObj.transform);
            LayoutElement topLe = topRow.AddComponent<LayoutElement>();
            topLe.preferredHeight = 72f;
            topLe.flexibleHeight = 1f;

            HorizontalLayoutGroup topH = topRow.AddComponent<HorizontalLayoutGroup>();
            topH.spacing = 10f;
            topH.childAlignment = TextAnchor.MiddleLeft;
            topH.childControlWidth = true;
            topH.childControlHeight = true;
            topH.childForceExpandWidth = false;

            // Dedicated Icon Slot for Sprites (auto-resolved from project art)
            CreateIconSlot(topRow.transform, "PartIconSlot_" + comp.Id, "ICON", 54f, font,
                isUnlocked ? new Color(0.14f, 0.36f, 0.24f, 1f) : new Color(0.10f, 0.15f, 0.24f, 1f),
                PixelUITheme.ResolvePartIconSprite(comp.Id));

            GameObject infoCol = CreateUIObject("NodeInfo", topRow.transform);
            LayoutElement infoLe = infoCol.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f;

            VerticalLayoutGroup infoV = infoCol.AddComponent<VerticalLayoutGroup>();
            infoV.spacing = 4f;
            infoV.childControlWidth = true;
            infoV.childControlHeight = true;
            infoV.childForceExpandWidth = true;
            infoV.childForceExpandHeight = false;

            CreateText("NodeTitle", infoCol.transform, font,
                string.Format("T{0}: {1}", tierIndex, comp.Name), 16, FontStyle.Bold,
                isUnlocked ? new Color(0.55f, 0.98f, 0.70f, 1f) : new Color(1f, 0.90f, 0.45f, 1f),
                TextAnchor.MiddleLeft);

            CreateText("NodeStats", infoCol.transform, font,
                BuilderPanelUI.FormatComponentShortStats(comp), 13, FontStyle.Normal,
                new Color(0.84f, 0.91f, 0.98f, 1f), TextAnchor.UpperLeft);

            // Action Button at bottom of node
            string btnText;
            Color btnColor;
            if (isUnlocked)
            {
                btnText = "UNLOCKED";
                btnColor = new Color(0.15f, 0.44f, 0.26f, 1f);
            }
            else if (!prevUnlocked)
            {
                btnText = "LOCKED (REQ T" + (tierIndex - 1) + ")";
                btnColor = new Color(0.18f, 0.20f, 0.26f, 1f);
            }
            else if (canAfford)
            {
                btnText = string.Format("RESEARCH (${0}k)", comp.UnlockCostThousands);
                btnColor = new Color(0.58f, 0.38f, 0.10f, 1f);
            }
            else
            {
                btnText = string.Format("NEED ${0}k", comp.UnlockCostThousands);
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
            TooltipTrigger.Attach(nodeObj, "rnd_chip_upgrade");
            Image nodeBg = nodeObj.AddComponent<Image>();
            nodeBg.color = isUnlocked
                ? new Color(0.09f, 0.24f, 0.17f, 0.96f)
                : (prevUnlocked ? new Color(0.11f, 0.18f, 0.30f, 0.96f) : new Color(0.06f, 0.08f, 0.13f, 0.88f));

            LayoutElement nodeLe = nodeObj.AddComponent<LayoutElement>();
            nodeLe.flexibleWidth = 0f;
            nodeLe.minWidth = 410f;
            nodeLe.preferredWidth = 410f;

            Button focusNodeBtn = nodeObj.AddComponent<Button>();
            focusNodeBtn.targetGraphic = nodeBg;
            focusNodeBtn.transition = Selectable.Transition.None;
            RectTransform nodeRt = nodeObj.GetComponent<RectTransform>();
            focusNodeBtn.onClick.AddListener(() => FocusOnRect(nodeRt, 1.45f));

            VerticalLayoutGroup nodeV = nodeObj.AddComponent<VerticalLayoutGroup>();
            nodeV.padding = new RectOffset(14, 14, 10, 10);
            nodeV.spacing = 6f;
            nodeV.childControlWidth = true;
            nodeV.childControlHeight = true;
            nodeV.childForceExpandWidth = true;
            nodeV.childForceExpandHeight = false;

            GameObject topRow = CreateUIObject("NodeTopRow", nodeObj.transform);
            LayoutElement topLe = topRow.AddComponent<LayoutElement>();
            topLe.preferredHeight = 72f;
            topLe.flexibleHeight = 1f;

            HorizontalLayoutGroup topH = topRow.AddComponent<HorizontalLayoutGroup>();
            topH.spacing = 10f;
            topH.childAlignment = TextAnchor.MiddleLeft;
            topH.childControlWidth = true;
            topH.childControlHeight = true;
            topH.childForceExpandWidth = false;

            CreateIconSlot(topRow.transform, "ChipIconSlot_" + targetSlots, "CPU", 54f, font,
                new Color(0.22f, 0.16f, 0.38f, 1f),
                PixelUITheme.ResolvePartIconSprite("chip"));

            GameObject infoCol = CreateUIObject("NodeInfo", topRow.transform);
            LayoutElement infoLe = infoCol.AddComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f;

            VerticalLayoutGroup infoV = infoCol.AddComponent<VerticalLayoutGroup>();
            infoV.spacing = 4f;
            infoV.childControlWidth = true;
            infoV.childControlHeight = true;
            infoV.childForceExpandWidth = true;
            infoV.childForceExpandHeight = false;

            CreateText("NodeTitle", infoCol.transform, font, title, 16, FontStyle.Bold,
                isUnlocked ? new Color(0.55f, 0.98f, 0.70f, 1f) : new Color(1f, 0.90f, 0.45f, 1f),
                TextAnchor.MiddleLeft);

            CreateText("NodeDesc", infoCol.transform, font, desc, 13, FontStyle.Normal,
                new Color(0.84f, 0.91f, 0.98f, 1f), TextAnchor.UpperLeft);

            string btnText = isUnlocked
                ? "UNLOCKED"
                : (!prevUnlocked ? "LOCKED (REQ PREV)" : string.Format("UPGRADE (${0}k)", costThousands));

            Color btnColor = isUnlocked
                ? new Color(0.15f, 0.44f, 0.26f, 1f)
                : (canResearch ? new Color(0.58f, 0.38f, 0.10f, 1f) : new Color(0.22f, 0.22f, 0.28f, 1f));

            int capturedTarget = targetSlots;
            int capturedCost = costThousands;
            Button btn = CreateButton(nodeObj.transform, "Btn_UpgradeChip_" + targetSlots, btnText, btnColor, 0f, 32f, font,
                () => TryUnlockChipTier(capturedTarget, capturedCost));
            btn.interactable = canResearch;
        }

        private static GameObject CreateIconSlot(Transform parent, string name, string placeholderTag, float size, Font font, Color frameColor, Sprite iconSprite = null)
        {
            GameObject iconFrame = CreateUIObject(name, parent);
            Image frameImg = iconFrame.AddComponent<Image>();
            PixelUITheme.EnsureAssetsLoaded();
            if (PixelUITheme.SlotFrameSprite != null)
            {
                frameImg.sprite = PixelUITheme.SlotFrameSprite;
                frameImg.type = Image.Type.Sliced;
                frameImg.pixelsPerUnitMultiplier = 0.35f;
                frameImg.color = Color.white;
            }
            else
            {
                frameImg.color = frameColor;
            }

            LayoutElement le = iconFrame.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            le.minWidth = size;
            le.minHeight = size;

            // Child Image for module / CPU sprite
            GameObject spriteObj = CreateUIObject("IconSprite", iconFrame.transform);
            RectTransform spriteRt = spriteObj.GetComponent<RectTransform>();
            StretchFull(spriteRt);
            spriteRt.offsetMin = new Vector2(6f, 6f);
            spriteRt.offsetMax = new Vector2(-6f, -6f);

            Image spriteImg = spriteObj.AddComponent<Image>();
            spriteImg.preserveAspect = true;
            spriteImg.raycastTarget = false;
            spriteImg.sprite = iconSprite;
            spriteImg.color = iconSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);

            Text tagTxt = CreateText("PlaceholderTag", iconFrame.transform, font, placeholderTag, 11, FontStyle.Bold,
                new Color(0.75f, 0.88f, 1f, 0.65f), TextAnchor.MiddleCenter);
            StretchFull(tagTxt.GetComponent<RectTransform>());
            if (iconSprite != null)
            {
                tagTxt.gameObject.SetActive(false);
            }
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
            if (width > 0f)
            {
                le.minWidth = width;
                le.preferredWidth = width;
                le.flexibleWidth = 0f;
            }
            if (height > 0f)
            {
                le.minHeight = height;
                le.preferredHeight = height;
                le.flexibleHeight = 0f;
            }

            Text txt = CreateText("Text", btnObj.transform, font, label, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }
    }
}
