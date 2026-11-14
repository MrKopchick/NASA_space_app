using System;
using System.Collections.Generic;
using AstraLink.Core;
using AstraLink.UI.Common;
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
                    UITheme.ApplySlotFrame(_cardBackgrounds[i], isSelected,
                        isSelected ? Color.white : new Color(0.86f, 0.90f, 0.98f, 0.96f));
                }

                if (i < _cardButtonLabels.Count && _cardButtonLabels[i] != null)
                {
                    Button btn = _cardButtonLabels[i].GetComponentInParent<Button>();
                    if (isSelected)
                    {
                        _cardButtonLabels[i].text = isCompleted ? "COMPLETED • OPEN HANGAR ▶" : "ACTIVE • OPEN HANGAR ▶";
                        if (btn != null) UITheme.ApplyButtonStyle(btn, null, UITheme.ButtonTintSuccess);
                    }
                    else
                    {
                        _cardButtonLabels[i].text = isCompleted ? "COMPLETED • SELECT & BUILD ▶" : "SELECT & BUILD ▶";
                        if (btn != null) UITheme.ApplyButtonStyle(btn, null, UITheme.ButtonTintPrimary);
                    }
                }
            }
        }

        private void OnCardActionClicked(int index)
        {
            if (_missions == null || index < 0 || index >= _missions.Count) return;

            bool alreadySelected = SelectedMission != null && SelectedMission.Id == _missions[index].Id;
            SelectContract(index);
            if (alreadySelected && GameManager.Instance != null)
            {
                GameManager.Instance.ShowWorkshop();
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

            UIThemeAssets assets = UITheme.Assets;
            Color fallbackTint;
            Sprite targetSprite;

            if (stageIndex <= 0)
            {
                fallbackTint = new Color(0.08f, 0.12f, 0.18f, 1f);
                targetSprite = garageBackgroundSprite != null ? garageBackgroundSprite : (assets != null ? assets.bgStarfield : null);
            }
            else if (stageIndex == 1)
            {
                fallbackTint = new Color(0.07f, 0.13f, 0.23f, 1f);
                targetSprite = officeBackgroundSprite != null ? officeBackgroundSprite : (assets != null ? assets.bgPlanetApproach : null);
            }
            else
            {
                fallbackTint = new Color(0.05f, 0.09f, 0.20f, 1f);
                targetSprite = missionControlBackgroundSprite != null ? missionControlBackgroundSprite : (assets != null ? assets.bgRingedWorld : null);
            }

            if (backgroundImage != null)
            {
                backgroundImage.sprite = targetSprite;
                backgroundImage.color = targetSprite != null ? new Color(0.32f, 0.38f, 0.48f, 1f) : fallbackTint;
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
            UIThemeAssets assets = UITheme.Assets;

            GameObject rootObj = CreateUIObject("HubContentRoot", hubRoot);
            RectTransform rootRt = rootObj.GetComponent<RectTransform>();
            StretchFull(rootRt);

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(32, 32, 22, 24);
            rootLayout.spacing = 18f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // Dispatcher Briefing Bar
            GameObject briefingBar = CreatePanel("BriefingBar", rootObj.transform, Color.white, 92f);
            UITheme.ApplyPanelFrame(briefingBar.GetComponent<Image>(), useDialogueBox: true);
            HorizontalLayoutGroup briefHlg = briefingBar.AddComponent<HorizontalLayoutGroup>();
            briefHlg.padding = new RectOffset(16, 16, 10, 10);
            briefHlg.spacing = 16f;
            briefHlg.childAlignment = TextAnchor.MiddleLeft;
            briefHlg.childControlWidth = true;
            briefHlg.childControlHeight = true;
            briefHlg.childForceExpandWidth = false;
            briefHlg.childForceExpandHeight = false;

            UITheme.CreateFramedPortrait(briefingBar.transform, "AstraBriefPortrait",
                assets != null ? assets.astraNormal : null, 70f);

            Text sectionTitle = CreateText("SectionTitle", briefingBar.transform, defaultFont,
                "MISSION CONTROL  —  Pick a NASA contract card below to design, test, and launch your spacecraft!",
                20, FontStyle.Bold, UITheme.TextPrimary, TextAnchor.MiddleLeft);
            LayoutElement titleLe = sectionTitle.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;
            titleLe.preferredHeight = 60f;

            Button goHangarBtn = CreateActionButton(briefingBar.transform, "Btn_OpenHangar",
                "OPEN HANGAR ▶", UITheme.ButtonTintSuccess, 220f, 52f, defaultFont,
                () => GameManager.Instance?.ShowWorkshop());
            UITheme.ApplyButtonStyle(goHangarBtn, null, UITheme.ButtonTintSuccess);

            // 3 Mission Cards Row
            GameObject cardsRow = CreateUIObject("MissionCardsContainer", rootObj.transform);
            LayoutElement cardsRowLe = cardsRow.AddComponent<LayoutElement>();
            cardsRowLe.flexibleHeight = 1f;

            HorizontalLayoutGroup cardsLayout = cardsRow.AddComponent<HorizontalLayoutGroup>();
            cardsLayout.spacing = 24f;
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
                        acceptBtn.onClick.AddListener(() => OnCardActionClicked(capturedIndex));
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
            UIThemeAssets assets = UITheme.Assets;
            GameObject cardObj = CreateUIObject("MissionCard_" + mission.Id, parent);
            Image cardBg = cardObj.AddComponent<Image>();
            UITheme.ApplySlotFrame(cardBg, selected: false);
            _cardBackgrounds.Add(cardBg);

            VerticalLayoutGroup cardLayout = cardObj.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(20, 20, 20, 20);
            cardLayout.spacing = 12f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            // 1. Visual Planet + Client Officer Showcase Box
            GameObject heroBox = CreatePanel("PlanetHeroBox", cardObj.transform, Color.white, 118f);
            UITheme.ApplyPanelFrame(heroBox.GetComponent<Image>(), useDialogueBox: true);
            HorizontalLayoutGroup heroHlg = heroBox.AddComponent<HorizontalLayoutGroup>();
            heroHlg.padding = new RectOffset(14, 14, 10, 10);
            heroHlg.spacing = 14f;
            heroHlg.childAlignment = TextAnchor.MiddleLeft;
            heroHlg.childControlWidth = true;
            heroHlg.childControlHeight = true;
            heroHlg.childForceExpandWidth = false;
            heroHlg.childForceExpandHeight = false;

            GameObject planetObj = CreateUIObject("PlanetHero", heroBox.transform);
            LayoutElement planetLe = planetObj.AddComponent<LayoutElement>();
            planetLe.preferredWidth = 92f;
            planetLe.preferredHeight = 92f;
            Image planetImg = planetObj.AddComponent<Image>();
            planetImg.sprite = UITheme.GetPlanetSprite(mission.OrbitId);
            planetImg.preserveAspect = true;

            GameObject titleStack = CreateUIObject("TitleStack", heroBox.transform);
            LayoutElement stackLe = titleStack.AddComponent<LayoutElement>();
            stackLe.flexibleWidth = 1f;
            VerticalLayoutGroup stackV = titleStack.AddComponent<VerticalLayoutGroup>();
            stackV.spacing = 4f;
            stackV.childControlWidth = true;
            stackV.childControlHeight = true;
            stackV.childForceExpandWidth = true;
            stackV.childForceExpandHeight = false;

            string cleanTitle = StripNumericPrefix(mission.Title);
            Text titleTxt = CreateText("Title", titleStack.transform, font, cleanTitle, 23, FontStyle.Bold,
                UITheme.TextGold, TextAnchor.MiddleLeft);
            titleTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

            string tierLabel = mission.OrbitId == "orbit_leo"
                ? "STARTER • LEO 400 KM"
                : (mission.OrbitId == "orbit_lunar" ? "INTERMEDIATE • MOON 100 KM" : "ADVANCED • MARS 350 KM");
            Text tierTxt = CreateText("TierTag", titleStack.transform, font, tierLabel, 14, FontStyle.Bold,
                UITheme.AccentCyan, TextAnchor.MiddleLeft);
            tierTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            UITheme.CreateFramedPortrait(heroBox.transform, "ClientPortrait",
                UITheme.GetDispatcherForMission(mission.Id), 74f);

            // 2. Concise Mission Brief
            Text briefTxt = CreateText("Brief", cardObj.transform, font, mission.Brief, 17, FontStyle.Normal,
                UITheme.TextPrimary, TextAnchor.UpperLeft);
            LayoutElement briefLe = briefTxt.gameObject.AddComponent<LayoutElement>();
            briefLe.preferredHeight = 82f;

            // 3. Visual Icon Stat Rows (Simple & Easy to Scan)
            GameObject statsContainer = CreatePanel("StatsBox", cardObj.transform, Color.white, 210f);
            UITheme.ApplyPanelFrame(statsBoxImage(statsContainer), useDialogueBox: true);
            VerticalLayoutGroup statsV = statsContainer.AddComponent<VerticalLayoutGroup>();
            statsV.padding = new RectOffset(14, 14, 12, 12);
            statsV.spacing = 8f;
            statsV.childControlWidth = true;
            statsV.childControlHeight = true;
            statsV.childForceExpandWidth = true;
            statsV.childForceExpandHeight = true;

            BuildStatRow(statsContainer.transform, font,
                assets != null ? assets.spaceCreditsCoin : null,
                string.Format("Budget Limit: <b>${0:0}k</b>   •   Reward: <b>+${1}k</b>", mission.BudgetLimitThousands, mission.RewardThousands),
                UITheme.TextGold);

            BuildStatRow(statsContainer.transform, font,
                assets != null ? assets.iconStarChart : null,
                string.Format("Goal: <b>{0:0} Science</b> across <b>{1} Orbits</b>", mission.TargetSciencePoints, mission.RequiredOrbits),
                UITheme.TextPrimary);

            BuildStatRow(statsContainer.transform, font,
                assets != null ? assets.iconDataChip : null,
                string.Format("Sensor: <b>{0}</b>   •   Autopilot: <b>{1}+ Slots</b>",
                    string.IsNullOrEmpty(mission.RequiredSensorTag) ? "Any" : mission.RequiredSensorTag,
                    mission.MinChipSlots),
                UITheme.AccentCyan);

            BuildStatRow(statsContainer.transform, font,
                UITheme.GetHazardSprite(mission.EventId),
                string.Format("Space Weather: <b>{0}</b>", GetEventHumanSummary(mission.EventId)),
                string.IsNullOrEmpty(mission.EventId) ? UITheme.AccentGreen : UITheme.AccentAmber);

            // Spacer pushes the button to the bottom of the card
            GameObject spacer = CreateUIObject("Spacer", cardObj.transform);
            spacer.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // 4. Action Button
            int capturedIndex = index;
            Button acceptBtn = CreateActionButton(cardObj.transform, "Btn_AcceptContract_" + index,
                "SELECT & BUILD ▶", UITheme.ButtonTintPrimary, 0f, 58f, font,
                () => OnCardActionClicked(capturedIndex));
            UITheme.ApplyButtonStyle(acceptBtn, null, UITheme.ButtonTintPrimary);

            Text btnLabel = acceptBtn.GetComponentInChildren<Text>();
            _cardButtonLabels.Add(btnLabel);
        }

        private static Image statsBoxImage(GameObject go) => go.GetComponent<Image>();

        private static void BuildStatRow(Transform parent, Font font, Sprite icon, string text, Color textColor)
        {
            GameObject row = CreatePanel("StatRow", parent, Color.white, 38f);
            UITheme.ApplyNameTab(row.GetComponent<Image>(), new Color(0.22f, 0.30f, 0.45f, 0.72f));
            HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 4, 4);
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;

            if (icon != null)
            {
                GameObject ic = CreateUIObject("Icon", row.transform);
                LayoutElement icLe = ic.AddComponent<LayoutElement>();
                icLe.preferredWidth = 26f;
                icLe.preferredHeight = 26f;
                Image icImg = ic.AddComponent<Image>();
                icImg.sprite = icon;
                icImg.preserveAspect = true;
            }

            Text txt = CreateText("Label", row.transform, font, text, 16, FontStyle.Normal, textColor, TextAnchor.MiddleLeft);
            txt.supportRichText = true;
            txt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private static string StripNumericPrefix(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            int dotIdx = raw.IndexOf(". ", StringComparison.Ordinal);
            return (dotIdx > 0 && dotIdx <= 3 && dotIdx + 2 < raw.Length) ? raw.Substring(dotIdx + 2) : raw;
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
