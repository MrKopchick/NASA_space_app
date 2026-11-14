using System;
using System.Collections.Generic;
using AstraLink.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Hub
{
    /// <summary>
    /// Screen 1 Controller: Contract Selection Hub (Task S1-07, Ivan V).
    /// Clean KSP-style contract board: pick one of the 3 NASA contracts and proceed to the Hangar.
    /// The selected contract stays stored in SelectedMission so the Hangar displays it.
    /// </summary>
    public class ContractHubUI : MonoBehaviour
    {
        public static ContractHubUI Instance { get; private set; }

        [Header("Progression Background Sprites (Night Sprint N-01)")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite garageBackgroundSprite;
        [SerializeField] private Sprite officeBackgroundSprite;
        [SerializeField] private Sprite missionControlBackgroundSprite;

        private IGameDataRepository _repository;
        private AgencyState _agencyState;
        private List<MissionGoal> _missions = new List<MissionGoal>();

        private readonly List<Image> _cardBackgrounds = new List<Image>();
        private readonly List<Text> _cardButtonLabels = new List<Text>();

        public MissionGoal SelectedMission { get; private set; }
        public bool IsSandboxModeSelected { get; private set; }

        public event Action<MissionGoal> OnMissionSelected;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (_missions.Count == 0)
            {
                IGameDataRepository repo = _repository ?? (GameManager.Instance != null ? GameManager.Instance.Repository : null);
                AgencyState state = GameManager.Instance != null
                    ? GameManager.Instance.CurrentAgencyState
                    : new AgencyState();
                Initialize(repo, state);
            }
        }

        private void OnEnable()
        {
            if (_missions.Count > 0)
            {
                RefreshCards();
            }
        }

        /// <summary>
        /// Single entry point for prefab initialization by GameManager.
        /// </summary>
        public void Initialize(IGameDataRepository repository, AgencyState agencyState)
        {
            _repository = repository ?? AstraLink.UI.Workshop.BuilderPanelUI.TryLoadJsonRepositoryFromResources();
            _agencyState = agencyState ?? (GameManager.Instance != null
                ? GameManager.Instance.CurrentAgencyState
                : new AgencyState());

            _missions = (_repository != null && _repository.GetMissions() != null && _repository.GetMissions().Count > 0)
                ? _repository.GetMissions()
                : BuildDefaultMissionsFromSpec();

            EnsureHubUIHierarchy();

            if (SelectedMission == null && _missions.Count > 0)
            {
                SelectedMission = _missions[0];
            }

            RefreshCards();
        }

        private Transform GetHubRootTransform()
        {
            if (string.Equals(gameObject.name, "HubPanel", StringComparison.OrdinalIgnoreCase))
            {
                return transform;
            }
            Transform foundChild = transform.Find("HubPanel");
            if (foundChild != null)
            {
                return foundChild;
            }
            return transform;
        }

        private static void EnsurePanelRectUnderNavBar(Transform panelTransform)
        {
            RectTransform rt = panelTransform != null ? panelTransform.GetComponent<RectTransform>() : null;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -70f);
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// Generates the clean contract selection UI in the Editor.
        /// </summary>
        [ContextMenu("Generate Hub UI In Scene")]
        public void BuildUIInEditor()
        {
            if (_missions == null || _missions.Count == 0)
            {
                _missions = BuildDefaultMissionsFromSpec();
            }
            if (_agencyState == null)
            {
                _agencyState = new AgencyState();
            }

            Transform hubRoot = GetHubRootTransform();
            EnsurePanelRectUnderNavBar(hubRoot);

            Transform existingOnSelf = transform.Find("HubContentRoot");
            if (existingOnSelf != null)
            {
                DestroyImmediate(existingOnSelf.gameObject);
            }
            if (hubRoot != transform)
            {
                Transform existingOnHub = hubRoot.Find("HubContentRoot");
                if (existingOnHub != null)
                {
                    DestroyImmediate(existingOnHub.gameObject);
                }
            }

            EnsureHubUIHierarchy();
            if (SelectedMission == null && _missions.Count > 0)
            {
                SelectedMission = _missions[0];
            }
            RefreshCards();
            Debug.Log("[ContractHubUI] Clean Contract Selection UI generated!");
        }

        public void SelectContract(int index)
        {
            if (_missions == null || index < 0 || index >= _missions.Count) return;

            IsSandboxModeSelected = false;
            SelectedMission = _missions[index];

            RefreshCards();
            OnMissionSelected?.Invoke(SelectedMission);

            Debug.Log("[ContractHubUI] Selected contract: " + SelectedMission.Title);
        }

        public void RefreshCards()
        {
            if (_agencyState == null && GameManager.Instance != null)
            {
                _agencyState = GameManager.Instance.CurrentAgencyState;
            }
            if (_agencyState == null)
            {
                _agencyState = new AgencyState();
            }

            UpdateBackgroundProgression();

            for (int i = 0; i < _missions.Count && i < _cardBackgrounds.Count; i++)
            {
                bool isSelected = SelectedMission != null && SelectedMission.Id == _missions[i].Id;
                bool isCompleted = _agencyState.CompletedMissionIds != null && _agencyState.CompletedMissionIds.Contains(_missions[i].Id);

                if (_cardBackgrounds[i] != null)
                {
                    _cardBackgrounds[i].color = isSelected
                        ? new Color(0.12f, 0.24f, 0.38f, 0.98f)
                        : new Color(0.08f, 0.13f, 0.21f, 0.94f);
                }

                if (i < _cardButtonLabels.Count && _cardButtonLabels[i] != null)
                {
                    if (isSelected)
                    {
                        _cardButtonLabels[i].text = isCompleted ? "ACTIVE CONTRACT (COMPLETED)" : "ACTIVE CONTRACT";
                    }
                    else
                    {
                        _cardButtonLabels[i].text = isCompleted ? "COMPLETED • SELECT CONTRACT" : "SELECT CONTRACT";
                    }
                }
            }
        }

        private void UpdateBackgroundProgression()
        {
            int completedCount = (_agencyState != null && _agencyState.CompletedMissionIds != null)
                ? _agencyState.CompletedMissionIds.Count
                : 0;

            int stageIndex = completedCount;
            if (SelectedMission != null)
            {
                if (SelectedMission.Id == "m2_lunar_mapper") stageIndex = Mathf.Max(stageIndex, 1);
                else if (SelectedMission.Id == "m3_mars_probe") stageIndex = Mathf.Max(stageIndex, 2);
            }

            Color fallbackTint;
            Sprite targetSprite = null;

            if (stageIndex <= 0)
            {
                fallbackTint = new Color(0.08f, 0.12f, 0.18f, 1f);
                targetSprite = garageBackgroundSprite;
            }
            else if (stageIndex == 1)
            {
                fallbackTint = new Color(0.07f, 0.13f, 0.23f, 1f);
                targetSprite = officeBackgroundSprite;
            }
            else
            {
                fallbackTint = new Color(0.05f, 0.09f, 0.20f, 1f);
                targetSprite = missionControlBackgroundSprite;
            }

            if (backgroundImage != null)
            {
                backgroundImage.sprite = targetSprite;
                backgroundImage.color = targetSprite != null ? Color.white : fallbackTint;
            }
        }

        private void EnsureHubUIHierarchy()
        {
            Transform hubRoot = GetHubRootTransform();
            EnsurePanelRectUnderNavBar(hubRoot);

            Transform placeholder = hubRoot.Find("PlaceholderTitle");
            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(false);
            }

            if (backgroundImage == null)
            {
                backgroundImage = hubRoot.GetComponent<Image>();
                if (backgroundImage == null) backgroundImage = hubRoot.gameObject.AddComponent<Image>();
            }

            Transform existingRoot = hubRoot.Find("HubContentRoot");
            if (existingRoot != null)
            {
                StretchFull(existingRoot.GetComponent<RectTransform>());
                BindExistingHierarchy(existingRoot);
                return;
            }

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject rootObj = CreateUIObject("HubContentRoot", hubRoot);
            RectTransform rootRt = rootObj.GetComponent<RectTransform>();
            StretchFull(rootRt);

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(36, 36, 28, 28);
            rootLayout.spacing = 20f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // Clean Section Header
            Text sectionTitle = CreateText("SectionTitle", rootObj.transform, defaultFont,
                "NASA MISSION CONTRACTS — Select a contract to open the Spacecraft Hangar",
                26, FontStyle.Bold, new Color(0.92f, 0.96f, 1f, 1f), TextAnchor.MiddleLeft);
            LayoutElement titleLe = sectionTitle.gameObject.AddComponent<LayoutElement>();
            titleLe.preferredHeight = 42f;

            // 3 Mission Cards Row
            GameObject cardsRow = CreateUIObject("MissionCardsContainer", rootObj.transform);
            LayoutElement cardsRowLe = cardsRow.AddComponent<LayoutElement>();
            cardsRowLe.flexibleHeight = 1f;

            HorizontalLayoutGroup cardsLayout = cardsRow.AddComponent<HorizontalLayoutGroup>();
            cardsLayout.spacing = 26f;
            cardsLayout.childControlWidth = true;
            cardsLayout.childControlHeight = true;
            cardsLayout.childForceExpandWidth = true;
            cardsLayout.childForceExpandHeight = true;

            _cardBackgrounds.Clear();
            _cardButtonLabels.Clear();

            for (int i = 0; i < _missions.Count; i++)
            {
                BuildMissionCard(i, _missions[i], cardsRow.transform, defaultFont);
            }
        }

        private void BindExistingHierarchy(Transform root)
        {
            _cardBackgrounds.Clear();
            _cardButtonLabels.Clear();

            Transform cardsContainer = root.Find("MissionCardsContainer");
            if (cardsContainer != null)
            {
                for (int i = 0; i < _missions.Count; i++)
                {
                    Transform card = cardsContainer.Find("MissionCard_" + _missions[i].Id);
                    if (card == null && i < cardsContainer.childCount)
                    {
                        card = cardsContainer.GetChild(i);
                    }
                    if (card == null) continue;

                    _cardBackgrounds.Add(card.GetComponent<Image>());

                    int capturedIndex = i;
                    Button acceptBtn = FindChildComponent<Button>(card, "Btn_AcceptContract_" + i);
                    if (acceptBtn != null)
                    {
                        acceptBtn.onClick.RemoveAllListeners();
                        acceptBtn.onClick.AddListener(() => SelectContract(capturedIndex));
                        _cardButtonLabels.Add(acceptBtn.GetComponentInChildren<Text>());
                    }
                    else
                    {
                        _cardButtonLabels.Add(null);
                    }
                }
            }
        }

        private void BuildMissionCard(int index, MissionGoal mission, Transform parent, Font font)
        {
            GameObject cardObj = CreateUIObject("MissionCard_" + mission.Id, parent);
            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.color = new Color(0.08f, 0.13f, 0.21f, 0.94f);
            _cardBackgrounds.Add(cardBg);

            VerticalLayoutGroup cardLayout = cardObj.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(24, 24, 24, 24);
            cardLayout.spacing = 14f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            // 1. Title
            Text titleTxt = CreateText("Title", cardObj.transform, font, mission.Title, 26, FontStyle.Bold,
                new Color(1f, 0.88f, 0.42f, 1f), TextAnchor.MiddleLeft);
            LayoutElement titleLe = titleTxt.gameObject.AddComponent<LayoutElement>();
            titleLe.preferredHeight = 40f;

            // 2. Orbit Summary
            string orbitInfo = GetOrbitHumanSummary(mission.OrbitId);
            Text orbitTxt = CreateText("OrbitSummary", cardObj.transform, font, orbitInfo, 17, FontStyle.Italic,
                new Color(0.58f, 0.84f, 1f, 1f), TextAnchor.UpperLeft);
            LayoutElement orbitLe = orbitTxt.gameObject.AddComponent<LayoutElement>();
            orbitLe.preferredHeight = 48f;

            // 3. Mission Brief
            Text briefTxt = CreateText("Brief", cardObj.transform, font, mission.Brief, 19, FontStyle.Normal,
                new Color(0.88f, 0.92f, 0.96f, 1f), TextAnchor.UpperLeft);
            LayoutElement briefLe = briefTxt.gameObject.AddComponent<LayoutElement>();
            briefLe.preferredHeight = 120f;

            // 4. Contract Requirements Box
            string hazardText = GetEventHumanSummary(mission.EventId);
            string specsContent = string.Format(
                "• Build Budget Limit: <b>${0:0}k</b>\n" +
                "• Science Target: <b>{1:0} pts</b> in <b>{2} orbits</b>\n" +
                "• Required Payload: <b>{3}</b>\n" +
                "• Recommended Chip: <b>{4} rule slots</b>\n" +
                "• Orbital Hazard: <b>{5}</b>\n" +
                "• Reward: <b>+${6}k</b> (Passive: <b>+${7}k</b>)",
                mission.BudgetLimitThousands,
                mission.TargetSciencePoints,
                mission.RequiredOrbits,
                string.IsNullOrEmpty(mission.RequiredSensorTag) ? "Any" : mission.RequiredSensorTag,
                mission.MinChipSlots,
                hazardText,
                mission.RewardThousands,
                mission.PassiveIncomeThousands);

            GameObject specsBox = CreatePanel("SpecsBox", cardObj.transform, new Color(0.05f, 0.08f, 0.14f, 0.92f), 250f);
            VerticalLayoutGroup specsLayout = specsBox.AddComponent<VerticalLayoutGroup>();
            specsLayout.padding = new RectOffset(18, 18, 16, 16);
            specsLayout.childControlWidth = true;
            specsLayout.childControlHeight = true;
            specsLayout.childForceExpandWidth = true;
            specsLayout.childForceExpandHeight = true;

            Text specsTxt = CreateText("SpecsText", specsBox.transform, font, specsContent, 18, FontStyle.Normal,
                new Color(0.84f, 0.95f, 0.88f, 1f), TextAnchor.MiddleLeft);
            specsTxt.supportRichText = true;
            specsTxt.lineSpacing = 1.25f;

            // Spacer pushes the button to the bottom of the card
            GameObject spacer = CreateUIObject("Spacer", cardObj.transform);
            LayoutElement spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleHeight = 1f;

            // 5. Single Select Contract Button
            int capturedIndex = index;
            Button acceptBtn = CreateActionButton(cardObj.transform, "Btn_AcceptContract_" + index,
                "SELECT CONTRACT", new Color(0.15f, 0.52f, 0.32f, 1f), 0f, 60f, font,
                () => SelectContract(capturedIndex));

            Text btnLabel = acceptBtn.GetComponentInChildren<Text>();
            _cardButtonLabels.Add(btnLabel);
        }

        private static string GetOrbitHumanSummary(string orbitId)
        {
            switch (orbitId)
            {
                case "orbit_leo":
                    return "Low Earth Orbit (400 km) • Eclipse 35% • Sun 100% • Delay 1.0 s";
                case "orbit_lunar":
                    return "Lunar Orbit (100 km) • Eclipse 45% • Comm Window 15% • Delay 3.0 s";
                case "orbit_mars":
                    return "Mars Orbit (350 km) • Sun Flux 43% • Eclipse 35% • Delay 10.0 s";
                default:
                    return "Deep Space • High Radiation & Signal Delay";
            }
        }

        private static string GetEventHumanSummary(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return "Nominal Space Weather (No storms)";
            switch (eventId)
            {
                case "evt_tx_overheat":
                    return "Transmitter Overheat (Downlink 45%)";
                case "evt_cme_storm":
                    return "Solar Flare CME (Radiation +85, Safe Mode!)";
                case "evt_micrometeoroid":
                    return "Micrometeoroid Noise (Sun 60%, Rad +15)";
                default:
                    return eventId;
            }
        }

        public static List<MissionGoal> BuildDefaultMissionsFromSpec()
        {
            return new List<MissionGoal>
            {
                new MissionGoal
                {
                    Id = "m1_leo_cubesat",
                    Title = "1. LEO Weather CubeSat",
                    Brief = "Launch a weather satellite to 400 km LEO, deliver 100 science points, and survive 4 orbits. Remember to save battery power during eclipse!",
                    OrbitId = "orbit_leo",
                    BudgetLimitThousands = 120.0f,
                    TargetSciencePoints = 100.0f,
                    RequiredOrbits = 4,
                    RewardThousands = 130,
                    PassiveIncomeThousands = 20,
                    MinChipSlots = 2,
                    RequiredSensorTag = "Camera",
                    EventId = ""
                },
                new MissionGoal
                {
                    Id = "m2_lunar_mapper",
                    Title = "2. Lunar Radar Mapper",
                    Brief = "Map lunar craters using a SAR Radar. Eclipse lasts 45% of each orbit, and a transmitter overheat is expected on Orbit 2.",
                    OrbitId = "orbit_lunar",
                    BudgetLimitThousands = 450.0f,
                    TargetSciencePoints = 250.0f,
                    RequiredOrbits = 5,
                    RewardThousands = 280,
                    PassiveIncomeThousands = 45,
                    MinChipSlots = 4,
                    RequiredSensorTag = "Radar",
                    EventId = "evt_tx_overheat"
                },
                new MissionGoal
                {
                    Id = "m3_mars_probe",
                    Title = "3. Mars Atmospheric Probe",
                    Brief = "Analyze Martian atmosphere with a Spectrometer. Solar flux drops to 43%, a solar flare hits on Orbit 3, and signal delay is 10 seconds.",
                    OrbitId = "orbit_mars",
                    BudgetLimitThousands = 900.0f,
                    TargetSciencePoints = 400.0f,
                    RequiredOrbits = 6,
                    RewardThousands = 300,
                    PassiveIncomeThousands = 80,
                    MinChipSlots = 6,
                    RequiredSensorTag = "Spectrometer",
                    EventId = "evt_cme_storm"
                }
            };
        }

        // --- UI Factory Helpers ---

        private static T FindChildComponent<T>(Transform root, string path) where T : Component
        {
            Transform target = root.Find(path);
            return target != null ? target.GetComponent<T>() : null;
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

        private static GameObject CreatePanel(string name, Transform parent, Color bgColor, float preferredHeight)
        {
            GameObject panel = CreateUIObject(name, parent);
            Image img = panel.AddComponent<Image>();
            img.color = bgColor;
            if (preferredHeight > 0f)
            {
                LayoutElement le = panel.AddComponent<LayoutElement>();
                le.preferredHeight = preferredHeight;
            }
            return panel;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, FontStyle style, Color color, TextAnchor alignment)
        {
            GameObject txtObj = CreateUIObject(name, parent);
            Text txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static Button CreateActionButton(Transform parent, string name, string label, Color bgColor, float width, float height, Font font, UnityEngine.Events.UnityAction onClick)
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

            Text txt = CreateText("Text", btnObj.transform, font, label, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }
    }
}
