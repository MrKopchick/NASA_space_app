using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstraLink.UnityBridge
{
    // Namespace alias marker so both `using AstraLink.Core;` and `using AstraLink.UnityBridge;` compile cleanly.
    public static class BridgeMarker { }
}

namespace AstraLink.Core
{
    public enum GameScreen
    {
        Hub = 0,
        Workshop = 1,
        Flight = 2,
        Debrief = 3
    }

    /// <summary>
    /// Central coordinator in MainScene (Task S1-01).
    /// Manages screen transitions (Hub, Workshop, Flight, Debrief), AgencyState persistence,
    /// and holds the active IMissionSimulator instance.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const float StartingCapitalThousands = 50.0f;
        public const int UpgradeTo4SlotsCostThousands = 40;
        public const int UpgradeTo6SlotsCostThousands = 80;
        public const string AgencyStatePrefsKey = "AstraLink_AgencyState_v1";

        public static GameManager Instance { get; private set; }

        [Header("Screen Panels (Хаб, Ангар, Політ, Дебрифінг)")]
        [SerializeField] private GameObject hubPanel;
        [SerializeField] private GameObject workshopPanel;
        [SerializeField] private GameObject flightPanel;
        [SerializeField] private GameObject debriefPanel;

        [Header("Initial Screen")]
        [SerializeField] private GameScreen initialScreen = GameScreen.Hub;

        public GameScreen CurrentScreen { get; private set; }
        public AgencyState CurrentAgencyState { get; private set; }
        public IMissionSimulator Simulator { get; private set; }

        public event Action<GameScreen> OnScreenChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Sprint 1: Initialize with MockMissionSimulator; swap to real simulator in Sprint 2.
            Simulator = new MockMissionSimulator();
            LoadOrCreateAgencyState();
            EnsureSceneUIStructure();
        }

        private void Start()
        {
            ShowScreen(initialScreen);
        }

        private void Update()
        {
            // Quick keyboard shortcuts (F1-F4 or 1-4) for fast testing in the Editor
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Alpha1)) ShowHub();
            else if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.Alpha2)) ShowWorkshop();
            else if (Input.GetKeyDown(KeyCode.F3) || Input.GetKeyDown(KeyCode.Alpha3)) ShowFlight();
            else if (Input.GetKeyDown(KeyCode.F4) || Input.GetKeyDown(KeyCode.Alpha4)) ShowDebrief();
        }

        public void SetMissionSimulator(IMissionSimulator simulator)
        {
            if (simulator == null)
            {
                Debug.LogError("[GameManager] Attempted to assign a null IMissionSimulator.");
                return;
            }

            Simulator = simulator;
        }

        public void ShowScreen(GameScreen screen)
        {
            CurrentScreen = screen;

            if (hubPanel != null) hubPanel.SetActive(screen == GameScreen.Hub);
            if (workshopPanel != null) workshopPanel.SetActive(screen == GameScreen.Workshop);
            if (flightPanel != null) flightPanel.SetActive(screen == GameScreen.Flight);
            if (debriefPanel != null) debriefPanel.SetActive(screen == GameScreen.Debrief);

            Debug.Log("[GameManager] Switched active screen to: " + screen);
            OnScreenChanged?.Invoke(screen);
        }

        public void ShowScreenByIndex(int screenIndex)
        {
            if (Enum.IsDefined(typeof(GameScreen), screenIndex))
            {
                ShowScreen((GameScreen)screenIndex);
            }
            else
            {
                Debug.LogWarning("[GameManager] Invalid screen index: " + screenIndex);
            }
        }

        public void ShowHub()
        {
            ShowScreen(GameScreen.Hub);
        }

        public void ShowWorkshop()
        {
            ShowScreen(GameScreen.Workshop);
        }

        public void ShowFlight()
        {
            ShowScreen(GameScreen.Flight);
        }

        public void ShowDebrief()
        {
            ShowScreen(GameScreen.Debrief);
        }

        public void SaveAgencyState()
        {
            if (CurrentAgencyState == null) return;
            string json = JsonUtility.ToJson(CurrentAgencyState);
            PlayerPrefs.SetString(AgencyStatePrefsKey, json);
            PlayerPrefs.Save();
        }

        public void LoadOrCreateAgencyState()
        {
            if (PlayerPrefs.HasKey(AgencyStatePrefsKey))
            {
                string json = PlayerPrefs.GetString(AgencyStatePrefsKey);
                if (!string.IsNullOrEmpty(json))
                {
                    CurrentAgencyState = JsonUtility.FromJson<AgencyState>(json);
                }
            }

            if (CurrentAgencyState == null)
            {
                CurrentAgencyState = new AgencyState
                {
                    CapitalThousands = StartingCapitalThousands,
                    AutopilotSlotsUnlocked = 2
                };
            }
        }

        private void EnsureSceneUIStructure()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("MainCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            Transform root = canvas.transform;
            if (hubPanel == null) hubPanel = FindOrCreatePanel(root, "HubPanel");
            if (workshopPanel == null) workshopPanel = FindOrCreatePanel(root, "WorkshopPanel");
            if (flightPanel == null) flightPanel = FindOrCreatePanel(root, "FlightPanel");
            if (debriefPanel == null) debriefPanel = FindOrCreatePanel(root, "DebriefPanel");

            PopulatePlaceholderIfEmpty(hubPanel, "1. ХАБ КОНТРАКТІВ ТА R&D (HubPanel)", new Color(0.08f, 0.13f, 0.22f, 1f));
            PopulatePlaceholderIfEmpty(workshopPanel, "2. АНГАР ТА КОНСТРУКТОР (WorkshopPanel)", new Color(0.11f, 0.16f, 0.20f, 1f));
            PopulatePlaceholderIfEmpty(flightPanel, "3. ОРБІТАЛЬНИЙ ПОЛІТ (FlightPanel)", new Color(0.05f, 0.08f, 0.16f, 1f));
            PopulatePlaceholderIfEmpty(debriefPanel, "4. ДЕБРИФІНГ МІСІЇ (DebriefPanel)", new Color(0.14f, 0.11f, 0.20f, 1f));

            EnsureTopNavBar(root);
        }

        private static GameObject FindOrCreatePanel(Transform parent, string panelName)
        {
            Transform existing = parent.Find(panelName);
            if (existing != null) return existing.gameObject;

            var panelObj = new GameObject(panelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObj.transform.SetParent(parent, false);
            var rt = panelObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -70f);
            return panelObj;
        }

        private static void PopulatePlaceholderIfEmpty(GameObject panel, string titleText, Color bgColor)
        {
            if (panel == null) return;

            Image img = panel.GetComponent<Image>();
            if (img == null) img = panel.AddComponent<Image>();
            img.color = bgColor;

            if (panel.transform.childCount > 0) return;

            var labelObj = new GameObject("PlaceholderTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObj.transform.SetParent(panel.transform, false);
            var rt = labelObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.4f);
            rt.anchorMax = new Vector2(0.9f, 0.6f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var txt = labelObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = titleText + "\n[Перемикання: кнопки зверху або клавіші 1 / 2 / 3 / 4]";
            txt.fontSize = 32;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.85f, 0.93f, 1f, 1f);
        }

        private void EnsureTopNavBar(Transform canvasRoot)
        {
            Transform navTransform = canvasRoot.Find("TopNavBar");
            GameObject navBar;
            if (navTransform == null)
            {
                navBar = new GameObject("TopNavBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup));
                navBar.transform.SetParent(canvasRoot, false);
            }
            else
            {
                navBar = navTransform.gameObject;
            }

            var navRt = navBar.GetComponent<RectTransform>();
            navRt.anchorMin = new Vector2(0f, 1f);
            navRt.anchorMax = new Vector2(1f, 1f);
            navRt.pivot = new Vector2(0.5f, 1f);
            navRt.anchoredPosition = Vector2.zero;
            navRt.sizeDelta = new Vector2(0f, 70f);

            var navImg = navBar.GetComponent<Image>();
            if (navImg == null) navImg = navBar.AddComponent<Image>();
            navImg.color = new Color(0.04f, 0.06f, 0.11f, 0.96f);

            var layout = navBar.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = navBar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 10, 10);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            if (navBar.transform.childCount == 0)
            {
                CreateNavButton(navBar.transform, "Btn_Hub", "1. Хаб (Hub)", ShowHub);
                CreateNavButton(navBar.transform, "Btn_Workshop", "2. Ангар (Workshop)", ShowWorkshop);
                CreateNavButton(navBar.transform, "Btn_Flight", "3. Політ (Flight)", ShowFlight);
                CreateNavButton(navBar.transform, "Btn_Debrief", "4. Дебрифінг (Debrief)", ShowDebrief);
            }
        }

        private static void CreateNavButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClickAction)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            var img = btnObj.GetComponent<Image>();
            img.color = new Color(0.16f, 0.27f, 0.44f, 1f);

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClickAction);

            var txtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var txt = txtObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = label;
            txt.fontSize = 22;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }
    }
}
