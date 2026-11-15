using System;
using System.Collections;
using System.Collections.Generic;
using AstraLink.Core;
using AstraLink.UI.Hub;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace AstraLink.UI
{
    /// <summary>
    /// KSP-style Space Center Map controller (Ivan V).
    /// Attach to SpaceCenterMapPanel inside MainCanvas.
    /// Automatically projects Scene-View-placed building buttons (over the 2D Grid) into Game-View Canvas coordinates
    /// via Main Camera, highlights buildings on hover, shows a KSP-style tooltip, and opens Hub / Hangar / Flight on click.
    /// </summary>
    public class SpaceCenterMapUI : MonoBehaviour
    {
        [Header("Startup")]
        [SerializeField] private bool openMapOnStart = true;

        [Header("Building Click Zones (under BuildingsLayer)")]
        [SerializeField] private Button missionControlButton;
        [SerializeField] private Button hangarButton;
        [SerializeField] private Button launchPadButton;
        [SerializeField] private Button rndButton;

        [Header("Optional Tilemap Layers for Hover Highlight (under Grid)")]
        [SerializeField] private Tilemap missionControlTilemap;
        [SerializeField] private Tilemap hangarTilemap;
        [SerializeField] private Tilemap launchPadTilemap;
        [SerializeField] private Tilemap rndTilemap;

        [Header("Hover Visuals")]
        [SerializeField] private Color tilemapNormalColor = Color.white;
        [SerializeField] private Color tilemapHoverColor = new Color(0.55f, 0.82f, 1.0f, 1.0f);
        [SerializeField] private Color buttonZoneHoverTint = new Color(0.20f, 0.62f, 1.0f, 0.28f);

        [Header("Overlay HUD (optional — auto-created in MapOverlayHUD if null)")]
        [SerializeField] private RectTransform mapOverlayHudRoot;
        [SerializeField] private Text tooltipTitleText;
        [SerializeField] private Text tooltipSubtitleText;

        [Header("Screen Panels (optional — auto-found under MainCanvas if null)")]
        [SerializeField] private GameObject hubPanel;
        [SerializeField] private GameObject workshopPanel;
        [SerializeField] private GameObject flightPanel;
        [SerializeField] private GameObject debriefPanel;

        private class BuildingBinding
        {
            public Button Button;
            public RectTransform ButtonRect;
            public Image ButtonImage;
            public Tilemap TargetTilemap;
            public string Title;
            public string Subtitle;
            public UnityEngine.Events.UnityAction OnClick;
            public bool HasWorldBounds;
            public Rect WorldBounds;
            public bool IsHovered;
        }

        private readonly List<BuildingBinding> _bindings = new List<BuildingBinding>();
        private GameObject _backToMapButtonObj;
        private GameObject _tooltipBannerObj;
        private GameObject _topNavBarObj;
        private bool _isMapOpen;

        private void Awake()
        {
            // Upgrade old serialized yellow hover colors to blue automatically
            if (tilemapHoverColor.r > 0.9f && tilemapHoverColor.b < 0.65f)
            {
                tilemapHoverColor = new Color(0.55f, 0.82f, 1.0f, 1.0f);
            }
            if (buttonZoneHoverTint.r > 0.9f && buttonZoneHoverTint.b < 0.65f)
            {
                buttonZoneHoverTint = new Color(0.20f, 0.62f, 1.0f, 0.28f);
            }

            EnsureStretchFullScreen(GetComponent<RectTransform>());
            AutoResolveReferences();
            WireBuildingButtons();
            EnsureTooltipBanner();
            EnsureBackToMapButton();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScreenChanged -= HandleGameScreenChanged;
                GameManager.Instance.OnScreenChanged += HandleGameScreenChanged;
            }

            if (openMapOnStart)
            {
                // Wait 1 frame so GameManager.Start() finishes initializing Hub/Workshop controllers first.
                StartCoroutine(OpenMapAfterStartupFrame());
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScreenChanged -= HandleGameScreenChanged;
            }
        }

        private void Update()
        {
            if (!_isMapOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ShowMap();
                }
                return;
            }

            UpdateProjectedButtonRectsAndHover();
        }

        private IEnumerator OpenMapAfterStartupFrame()
        {
            yield return null;
            ShowMap();
        }

        // =====================================================================
        // PUBLIC NAVIGATION API
        // =====================================================================

        public void ShowMap()
        {
            AutoResolveReferences();
            _isMapOpen = true;

            SetPanelActive(hubPanel, false);
            SetPanelActive(workshopPanel, false);
            SetPanelActive(flightPanel, false);
            SetPanelActive(debriefPanel, false);

            if (_topNavBarObj == null && transform.parent != null)
            {
                Transform nav = transform.parent.Find("TopNavBar");
                if (nav != null) _topNavBarObj = nav.gameObject;
            }
            SetPanelActive(_topNavBarObj, false);

            gameObject.SetActive(true);
            if (_backToMapButtonObj != null)
            {
                _backToMapButtonObj.SetActive(false);
            }

            ResetAllHoverStates();
            ClearTooltip();
            UpdateProjectedButtonRectsAndHover();
        }

        public void OpenMissionControl()
        {
            LeaveMapForScreen();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowHub();
            }
            else
            {
                SetPanelActive(hubPanel, true);
            }
        }

        public void OpenHangar()
        {
            LeaveMapForScreen();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowWorkshop();
            }
            else
            {
                SetPanelActive(workshopPanel, true);
            }
        }

        public void OpenLaunchPad()
        {
            LeaveMapForScreen();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowFlight();
            }
            else
            {
                SetPanelActive(flightPanel, true);
            }
        }

        public void OpenRnD()
        {
            LeaveMapForScreen();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowHub();
            }
            else
            {
                SetPanelActive(hubPanel, true);
            }

            RnDPanelUI rnd = FindObjectOfType<RnDPanelUI>(true);
            if (rnd != null)
            {
                rnd.gameObject.SetActive(true);
            }
        }

        private void LeaveMapForScreen()
        {
            _isMapOpen = false;
            ResetAllHoverStates();
            ClearTooltip();
            SetPanelActive(_topNavBarObj, true);
            gameObject.SetActive(false);
            if (_backToMapButtonObj != null)
            {
                _backToMapButtonObj.SetActive(true);
                _backToMapButtonObj.transform.SetAsLastSibling();
            }
        }

        private void HandleGameScreenChanged(GameScreen screen)
        {
            if (gameObject.activeSelf && _isMapOpen)
            {
                LeaveMapForScreen();
            }
            else if (_backToMapButtonObj != null)
            {
                _backToMapButtonObj.SetActive(!_isMapOpen);
                _backToMapButtonObj.transform.SetAsLastSibling();
            }
        }

        // =====================================================================
        // WORLD-TO-CANVAS PROJECTION & DIRECT MOUSE HOVER/CLICK
        // =====================================================================

        private void UpdateProjectedButtonRectsAndHover()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 mouseScreen = Input.mousePosition;
            Vector3 mouseWorld3D = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, Mathf.Abs(cam.transform.position.z)));
            Vector2 mouseWorld2D = new Vector2(mouseWorld3D.x, mouseWorld3D.y);

            BuildingBinding hoveredBinding = null;

            for (int i = 0; i < _bindings.Count; i++)
            {
                BuildingBinding b = _bindings[i];
                if (b.Button == null || !b.Button.gameObject.activeInHierarchy) continue;

                // Project 2D Grid WorldBounds onto the Canvas RectTransform so UI buttons match Game View!
                if (b.HasWorldBounds && b.ButtonRect != null && b.ButtonRect.parent is RectTransform parentRt)
                {
                    Vector2 screenMin = cam.WorldToScreenPoint(new Vector3(b.WorldBounds.xMin, b.WorldBounds.yMin, 0f));
                    Vector2 screenMax = cam.WorldToScreenPoint(new Vector3(b.WorldBounds.xMax, b.WorldBounds.yMax, 0f));

                    RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenMin, null, out Vector2 localMin);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenMax, null, out Vector2 localMax);

                    Vector2 center = (localMin + localMax) * 0.5f;
                    Vector2 size = new Vector2(Mathf.Abs(localMax.x - localMin.x), Mathf.Abs(localMax.y - localMin.y));

                    b.ButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
                    b.ButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
                    b.ButtonRect.pivot = new Vector2(0.5f, 0.5f);
                    b.ButtonRect.anchoredPosition = center;
                    b.ButtonRect.sizeDelta = size;
                }

                bool isMouseInside = false;
                if (b.HasWorldBounds && b.WorldBounds.Contains(mouseWorld2D))
                {
                    isMouseInside = true;
                }
                else if (b.ButtonRect != null && RectTransformUtility.RectangleContainsScreenPoint(b.ButtonRect, mouseScreen, null))
                {
                    isMouseInside = true;
                }

                if (isMouseInside && hoveredBinding == null)
                {
                    hoveredBinding = b;
                }
            }

            // Update hover visuals & tooltip
            for (int i = 0; i < _bindings.Count; i++)
            {
                BuildingBinding b = _bindings[i];
                bool shouldHover = (b == hoveredBinding);
                if (b.IsHovered != shouldHover)
                {
                    b.IsHovered = shouldHover;
                    ApplyBuildingHoverVisual(b, shouldHover);
                }
            }

            if (hoveredBinding != null)
            {
                ShowTooltip(hoveredBinding.Title, hoveredBinding.Subtitle);

                // Direct click fallback in case UI raycast is blocked by another transparent layer
                if (Input.GetMouseButtonDown(0))
                {
                    hoveredBinding.OnClick?.Invoke();
                }
            }
            else
            {
                ClearTooltip();
            }
        }

        private void ApplyBuildingHoverVisual(BuildingBinding b, bool hovered)
        {
            if (b.TargetTilemap != null)
            {
                b.TargetTilemap.color = hovered ? tilemapHoverColor : tilemapNormalColor;
            }

            if (b.ButtonImage != null)
            {
                b.ButtonImage.color = hovered
                    ? buttonZoneHoverTint
                    : new Color(1f, 1f, 1f, 0.005f);
            }
        }

        private void ResetAllHoverStates()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                _bindings[i].IsHovered = false;
                ApplyBuildingHoverVisual(_bindings[i], false);
            }
        }

        // =====================================================================
        // REFERENCE RESOLUTION & BUTTON WIRING
        // =====================================================================

        private void AutoResolveReferences()
        {
            Transform parentCanvas = transform.parent;
            if (parentCanvas != null)
            {
                if (hubPanel == null) hubPanel = FindSibling(parentCanvas, "HubPanel");
                if (workshopPanel == null) workshopPanel = FindSibling(parentCanvas, "WorkshopPanel");
                if (flightPanel == null) flightPanel = FindSibling(parentCanvas, "FlightPanel");
                if (debriefPanel == null) debriefPanel = FindSibling(parentCanvas, "DebriefPanel");
            }

            Transform buildingsLayer = transform.Find("BuildingsLayer");
            if (buildingsLayer != null)
            {
                EnsureStretchFullScreen(buildingsLayer.GetComponent<RectTransform>());
            }

            Transform searchRoot = buildingsLayer != null ? buildingsLayer : transform;

            if (missionControlButton == null)
                missionControlButton = FindButton(searchRoot, "Btn_MissionControl", "Building_MissionControl", "MissionControl");
            if (hangarButton == null)
                hangarButton = FindButton(searchRoot, "Btn_Hangar", "Building_Hangar", "Hangar");
            if (launchPadButton == null)
                launchPadButton = FindButton(searchRoot, "Btn_LaunchPad", "Building_LaunchPad", "LaunchPad");
            if (rndButton == null)
                rndButton = FindButton(searchRoot, "Btn_RnD", "Building_RnD", "RnD");

            if (mapOverlayHudRoot == null)
            {
                Transform hud = transform.Find("MapOverlayHUD");
                if (hud != null)
                {
                    mapOverlayHudRoot = hud.GetComponent<RectTransform>();
                    EnsureStretchFullScreen(mapOverlayHudRoot);
                }
            }

            // Optional Tilemaps in Grid
            if (missionControlTilemap == null) missionControlTilemap = FindTilemapByName("Tilemap_MissionControl");
            if (hangarTilemap == null) hangarTilemap = FindTilemapByName("Tilemap_Hangar");
            if (launchPadTilemap == null) launchPadTilemap = FindTilemapByName("Tilemap_LaunchPad");
            if (rndTilemap == null) rndTilemap = FindTilemapByName("Tilemap_RnD");
        }

        private void WireBuildingButtons()
        {
            _bindings.Clear();

            RegisterBuilding(
                missionControlButton,
                missionControlTilemap,
                "MISSION CONTROL",
                "Select space contracts, inspect mission goals & rewards",
                OpenMissionControl);

            RegisterBuilding(
                hangarButton,
                hangarTilemap,
                "SPACECRAFT HANGAR (VAB)",
                "Assemble satellite modules, choose launch vehicle & configure autopilot",
                OpenHangar);

            RegisterBuilding(
                launchPadButton,
                launchPadTilemap,
                "LAUNCH PAD",
                "Validate build and launch active mission to orbit",
                OpenLaunchPad);

            RegisterBuilding(
                rndButton,
                rndTilemap,
                "R&D RESEARCH LAB",
                "Unlock advanced spacecraft modules and autopilot slots",
                OpenRnD);
        }

        private void RegisterBuilding(
            Button btn,
            Tilemap targetTilemap,
            string title,
            string subtitle,
            UnityEngine.Events.UnityAction onClick)
        {
            if (btn == null) return;

            RectTransform rt = btn.GetComponent<RectTransform>();

            // Disable Standard UI Button onClick because UpdateProjectedButtonRectsAndHover handles both UI & World clicks cleanly without double-firing
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();

            CanvasRenderer cr = btn.GetComponent<CanvasRenderer>();
            if (cr != null)
            {
                cr.cullTransparentMesh = false;
            }

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                // Replace transparent tile sprite with solid UI quad so hover highlight tint is visible across the whole building rect
                img.sprite = null;
                img.raycastTarget = true;
                img.color = new Color(1f, 1f, 1f, 0.005f);
            }

            var binding = new BuildingBinding
            {
                Button = btn,
                ButtonRect = rt,
                ButtonImage = img,
                TargetTilemap = targetTilemap,
                Title = title,
                Subtitle = subtitle,
                OnClick = onClick
            };

            // Determine 2D World Bounds of this building:
            // 1) If the user framed the building in Scene View on the 2D Grid (where sizeDelta is in Grid world units < 80 and anchoredPosition is near (-960, -540)):
            if (rt != null && rt.sizeDelta.x > 0.5f && rt.sizeDelta.x < 80f && rt.sizeDelta.y > 0.5f && rt.sizeDelta.y < 80f)
            {
                Vector2 worldCenter = new Vector2(960f + rt.anchoredPosition.x, 540f + rt.anchoredPosition.y);
                Vector2 size = rt.sizeDelta;
                binding.WorldBounds = new Rect(worldCenter - size * 0.5f, size);
                binding.HasWorldBounds = true;
            }
            // 2) Otherwise, if the Tilemap itself has painted tiles, use its compressed bounds:
            else if (targetTilemap != null && targetTilemap.GetUsedTilesCount() > 0)
            {
                targetTilemap.CompressBounds();
                Bounds local = targetTilemap.localBounds;
                Vector3 wMin = targetTilemap.transform.TransformPoint(local.min);
                Vector3 wMax = targetTilemap.transform.TransformPoint(local.max);
                binding.WorldBounds = Rect.MinMaxRect(wMin.x, wMin.y, wMax.x, wMax.y);
                binding.HasWorldBounds = true;
            }

            _bindings.Add(binding);
        }

        // =====================================================================
        // KSP-STYLE HOVER TOOLTIP & BACK TO MAP BUTTON
        // =====================================================================

        internal void ShowTooltip(string title, string subtitle)
        {
            EnsureTooltipBanner();
            if (_tooltipBannerObj != null && !_tooltipBannerObj.activeSelf)
                _tooltipBannerObj.SetActive(true);
            if (tooltipTitleText != null) tooltipTitleText.text = title;
            if (tooltipSubtitleText != null) tooltipSubtitleText.text = subtitle;
        }

        internal void ClearTooltip()
        {
            if (_tooltipBannerObj != null && _tooltipBannerObj.activeSelf)
                _tooltipBannerObj.SetActive(false);
        }

        private void EnsureTooltipBanner()
        {
            if (tooltipTitleText != null && _tooltipBannerObj != null) return;

            Transform hudParent = mapOverlayHudRoot != null ? (Transform)mapOverlayHudRoot : transform;
            Transform existing = hudParent.Find("BuildingTooltipBanner");
            if (existing != null)
            {
                _tooltipBannerObj = existing.gameObject;
                tooltipTitleText = existing.Find("Title")?.GetComponent<Text>();
                tooltipSubtitleText = existing.Find("Subtitle")?.GetComponent<Text>();
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _tooltipBannerObj = new GameObject("BuildingTooltipBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _tooltipBannerObj.transform.SetParent(hudParent, false);
            RectTransform rt = _tooltipBannerObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 18f);
            rt.sizeDelta = new Vector2(620f, 70f);

            Image bg = _tooltipBannerObj.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.09f, 0.16f, 0.92f);
            bg.raycastTarget = false;

            tooltipTitleText = CreateText("Title", _tooltipBannerObj.transform, font, string.Empty, 18, FontStyle.Bold,
                new Color(0.45f, 0.82f, 1f, 1f), new Vector2(14f, 34f), new Vector2(-14f, -8f));
            tooltipSubtitleText = CreateText("Subtitle", _tooltipBannerObj.transform, font, string.Empty, 13, FontStyle.Normal,
                new Color(0.86f, 0.92f, 1f, 1f), new Vector2(14f, 8f), new Vector2(-14f, -38f));

            _tooltipBannerObj.SetActive(false);
        }

        private void EnsureBackToMapButton()
        {
            Transform canvasTransform = transform.parent;
            if (canvasTransform == null) return;

            Transform existing = canvasTransform.Find("Btn_BackToSpaceCenter");
            if (existing != null)
            {
                _backToMapButtonObj = existing.gameObject;
                Button existingBtn = existing.GetComponent<Button>();
                if (existingBtn != null)
                {
                    existingBtn.onClick.RemoveAllListeners();
                    existingBtn.onClick.AddListener(ShowMap);
                }
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _backToMapButtonObj = new GameObject("Btn_BackToSpaceCenter", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _backToMapButtonObj.transform.SetParent(canvasTransform, false);

            RectTransform rt = _backToMapButtonObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(14f, -14f);
            rt.sizeDelta = new Vector2(180f, 42f);

            Image bg = _backToMapButtonObj.GetComponent<Image>();
            bg.color = new Color(0.14f, 0.42f, 0.68f, 0.98f);

            Button btn = _backToMapButtonObj.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(ShowMap);

            Text label = CreateText("Text", _backToMapButtonObj.transform, font, "<< SPACE CENTER", 13, FontStyle.Bold,
                Color.white, Vector2.zero, Vector2.zero);
            label.alignment = TextAnchor.MiddleCenter;

            _backToMapButtonObj.SetActive(false);
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static void EnsureStretchFullScreen(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject FindSibling(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            return t != null ? t.gameObject : null;
        }

        private static Button FindButton(Transform root, params string[] candidateNames)
        {
            for (int i = 0; i < candidateNames.Length; i++)
            {
                Transform t = root.Find(candidateNames[i]);
                if (t != null)
                {
                    Button b = t.GetComponent<Button>();
                    if (b != null) return b;
                }
            }
            return null;
        }

        private static Tilemap FindTilemapByName(string tilemapName)
        {
            Tilemap[] maps = FindObjectsOfType<Tilemap>(true);
            for (int i = 0; i < maps.Length; i++)
            {
                if (string.Equals(maps[i].gameObject.name, tilemapName, StringComparison.OrdinalIgnoreCase))
                {
                    return maps[i];
                }
            }
            return null;
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            return txt;
        }
    }
}
