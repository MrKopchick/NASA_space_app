using System;
using System.Collections.Generic;
using AstraLink.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Hub
{
    /// <summary>
    /// Screen 1 Controller: Contract Selection Hub (Ivan V).
    /// Handcrafted indie pixel-art layout using project sprites from Assets/_Project/Art/UI & Planets.
    /// </summary>
    public class ContractHubUI : MonoBehaviour
    {
        public static ContractHubUI Instance { get; private set; }

        [Header("Progression Background Sprites")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite garageBackgroundSprite;
        [SerializeField] private Sprite officeBackgroundSprite;
        [SerializeField] private Sprite missionControlBackgroundSprite;

        [Header("Pixel-Art UI Assets (Auto-loaded from Assets/_Project/Art & Fonts)")]
        [SerializeField] private Font headerFont;
        [SerializeField] private Font bodyFont;
        [SerializeField] private Sprite starfieldBgSprite;
        [SerializeField] private Sprite cardPanelSprite;
        [SerializeField] private Sprite innerBoxSprite;
        [SerializeField] private Sprite iconSlotSprite;
        [SerializeField] private Sprite buttonNormalSprite;
        [SerializeField] private Sprite buttonHoverSprite;
        [SerializeField] private Sprite buttonPressedSprite;
        [SerializeField] private Sprite buttonDisabledSprite;
        [SerializeField] private Sprite missionBadgeSprite;
        [SerializeField] private Sprite creditsCoinSprite;
        [SerializeField] private Sprite earthSprite;
        [SerializeField] private Sprite moonSprite;
        [SerializeField] private Sprite marsSprite;

        private IGameDataRepository _repository;
        private AgencyState _agencyState;
        private List<MissionGoal> _missions = new List<MissionGoal>();

        private readonly List<Image> _cardBackgrounds = new List<Image>();
        private readonly List<Text> _cardButtonLabels = new List<Text>();
        private Text _agencyBudgetText;

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

        public void Initialize(IGameDataRepository repository, AgencyState agencyState)
        {
            _repository = repository ?? AstraLink.UI.Workshop.BuilderPanelUI.TryLoadJsonRepositoryFromResources();
            _agencyState = agencyState ?? (GameManager.Instance != null
                ? GameManager.Instance.CurrentAgencyState
                : new AgencyState());

            _missions = (_repository != null && _repository.GetMissions() != null && _repository.GetMissions().Count > 0)
                ? _repository.GetMissions()
                : BuildDefaultMissionsFromSpec();

            EnsureProjectArtLoaded();
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

            EnsureProjectArtLoaded();

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
            Debug.Log("[ContractHubUI] Pixel-Art Hub UI generated!");
        }

        public void SelectContract(int index)
        {
            if (_missions == null || index < 0 || index >= _missions.Count) return;

            IsSandboxModeSelected = false;
            SelectedMission = _missions[index];

            RefreshCards();
            OnMissionSelected?.Invoke(SelectedMission);
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

            if (_agencyBudgetText != null)
            {
                _agencyBudgetText.text = string.Format("${0:0}k", _agencyState.CapitalThousands);
            }

            for (int i = 0; i < _missions.Count && i < _cardBackgrounds.Count; i++)
            {
                bool isSelected = SelectedMission != null && SelectedMission.Id == _missions[i].Id;
                bool isCompleted = _agencyState.CompletedMissionIds != null && _agencyState.CompletedMissionIds.Contains(_missions[i].Id);

                if (_cardBackgrounds[i] != null)
                {
                    if (_cardBackgrounds[i].sprite != null)
                    {
                        _cardBackgrounds[i].color = isSelected
                            ? Color.white
                            : new Color(0.82f, 0.86f, 0.94f, 0.96f);
                    }
                    else
                    {
                        _cardBackgrounds[i].color = isSelected
                            ? new Color(0.16f, 0.22f, 0.36f, 0.98f)
                            : new Color(0.11f, 0.14f, 0.24f, 0.95f);
                    }
                }

                if (i < _cardButtonLabels.Count && _cardButtonLabels[i] != null)
                {
                    if (isSelected)
                    {
                        _cardButtonLabels[i].text = isCompleted ? "SELECTED (DONE)" : "SELECTED";
                    }
                    else
                    {
                        _cardButtonLabels[i].text = isCompleted ? "REPLAY MISSION" : "ACCEPT MISSION";
                    }
                }
            }
        }

        private void UpdateBackgroundProgression()
        {
            if (backgroundImage == null) return;

            Sprite bg = starfieldBgSprite ?? missionControlBackgroundSprite ?? officeBackgroundSprite ?? garageBackgroundSprite;
            if (bg != null)
            {
                if (bg.texture != null) bg.texture.filterMode = FilterMode.Point;
                backgroundImage.sprite = bg;
                backgroundImage.color = new Color(0.65f, 0.72f, 0.88f, 1f);
            }
            else
            {
                backgroundImage.color = new Color(0.06f, 0.08f, 0.15f, 1f);
            }
        }

        // =====================================================================
        // AUTO-LOADING REAL PROJECT ASSETS & 9-SLICE CONFIGURATION
        // =====================================================================

        private void EnsureProjectArtLoaded()
        {
#if UNITY_EDITOR
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/UI/inventory_panel.png", new Vector4(6, 6, 6, 6));
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/UI/dialogue_box.png", new Vector4(6, 6, 6, 6));
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/UI/item_slot.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/UI/button_normal.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/UI/button_hover.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/UI/button_pressed.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/UI/button_disabled.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/Planets/earth.png", Vector4.zero);
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/Planets/moon.png", Vector4.zero);
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/Planets/mars.png", Vector4.zero);
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/UI/mission_badge.png", Vector4.zero);
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/UI/space_credits_coin.png", Vector4.zero);
            ConfigureSpriteBorderInEditor("Assets/_Project/Art/Planets/Backgrounds/orbital_starfield.png", Vector4.zero);

            if (headerFont == null)
                headerFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/Kenney Future.ttf");
            if (bodyFont == null)
                bodyFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/Kenney Mini Square Mono.ttf");

            if (starfieldBgSprite == null)
                starfieldBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Planets/Backgrounds/orbital_starfield.png");
            if (cardPanelSprite == null)
                cardPanelSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/inventory_panel.png");
            if (innerBoxSprite == null)
                innerBoxSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/dialogue_box.png");
            if (iconSlotSprite == null)
                iconSlotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/item_slot.png");

            if (buttonNormalSprite == null)
                buttonNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_normal.png");
            if (buttonHoverSprite == null)
                buttonHoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_hover.png");
            if (buttonPressedSprite == null)
                buttonPressedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_pressed.png");
            if (buttonDisabledSprite == null)
                buttonDisabledSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_disabled.png");

            if (missionBadgeSprite == null)
                missionBadgeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/mission_badge.png");
            if (creditsCoinSprite == null)
                creditsCoinSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/space_credits_coin.png");

            if (earthSprite == null)
                earthSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Planets/earth.png");
            if (moonSprite == null)
                moonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Planets/moon.png");
            if (marsSprite == null)
                marsSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Planets/mars.png");
#endif
        }

#if UNITY_EDITOR
        private static void ConfigureSpriteBorderInEditor(string assetPath, Vector4 border)
        {
            var importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }
            if (importer.textureCompression != UnityEditor.TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (border != Vector4.zero && importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }
#endif

        // =====================================================================
        // HANDCRAFTED PIXEL-ART HUB LAYOUT
        // =====================================================================

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
                // Rebuild automatically if existing HubContentRoot is from an older layout version
                if (existingRoot.Find("TopHeaderBar") != null && existingRoot.Find("LayoutV3Marker") != null)
                {
                    StretchFull(existingRoot.GetComponent<RectTransform>());
                    BindExistingHierarchy(existingRoot);
                    return;
                }

                if (Application.isPlaying)
                    Destroy(existingRoot.gameObject);
                else
                    DestroyImmediate(existingRoot.gameObject);
            }

            Font fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font titleFont = headerFont != null ? headerFont : fallbackFont;
            Font textFont = bodyFont != null ? bodyFont : titleFont;

            GameObject rootObj = CreateUIObject("HubContentRoot", hubRoot);
            StretchFull(rootObj.GetComponent<RectTransform>());

            GameObject marker = CreateUIObject("LayoutV3Marker", rootObj.transform);
            marker.AddComponent<LayoutElement>().ignoreLayout = true;

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(48, 48, 28, 34);
            rootLayout.spacing = 24f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // --- 1. TOP HEADER BAR ---
            GameObject topBar = CreateSlicedPanel("TopHeaderBar", rootObj.transform, innerBoxSprite,
                new Color(0.08f, 0.11f, 0.20f, 0.96f), 68f);

            HorizontalLayoutGroup topBarLayout = topBar.AddComponent<HorizontalLayoutGroup>();
            topBarLayout.padding = new RectOffset(28, 32, 12, 12);
            topBarLayout.spacing = 10f;
            topBarLayout.childAlignment = TextAnchor.MiddleLeft;
            topBarLayout.childControlWidth = true;
            topBarLayout.childControlHeight = true;
            topBarLayout.childForceExpandWidth = false;
            topBarLayout.childForceExpandHeight = false;

            if (missionBadgeSprite != null)
            {
                CreateIcon("BadgeIcon", topBar.transform, missionBadgeSprite, 38f, 38f);
            }

            Text headerTitle = CreateText("HeaderTitle", topBar.transform, titleFont,
                "MISSION CONTRACTS", 22, FontStyle.Bold, new Color(0.94f, 0.97f, 1f, 1f), TextAnchor.MiddleLeft);
            LayoutElement htLe = headerTitle.gameObject.AddComponent<LayoutElement>();
            htLe.flexibleWidth = 1f;
            htLe.preferredHeight = 38f;

            // Agency Credits pill on the right of Top Bar
            if (creditsCoinSprite != null)
            {
                CreateIcon("CoinIcon", topBar.transform, creditsCoinSprite, 30f, 30f);
            }

            float budgetVal = _agencyState != null ? _agencyState.CapitalThousands : 50f;
            _agencyBudgetText = CreateText("AgencyBudgetText", topBar.transform, titleFont,
                string.Format("${0:0}k", budgetVal), 20, FontStyle.Bold,
                new Color(1f, 0.84f, 0.36f, 1f), TextAnchor.MiddleLeft);
            LayoutElement abLe = _agencyBudgetText.gameObject.AddComponent<LayoutElement>();
            abLe.preferredWidth = 84f;
            abLe.minWidth = 84f;
            abLe.preferredHeight = 38f;

            // --- 2. THREE CONTRACT CARDS ---
            GameObject cardsRow = CreateUIObject("MissionCardsContainer", rootObj.transform);
            LayoutElement cardsRowLe = cardsRow.AddComponent<LayoutElement>();
            cardsRowLe.flexibleHeight = 1f;

            HorizontalLayoutGroup cardsLayout = cardsRow.AddComponent<HorizontalLayoutGroup>();
            cardsLayout.spacing = 30f;
            cardsLayout.childControlWidth = true;
            cardsLayout.childControlHeight = true;
            cardsLayout.childForceExpandWidth = true;
            cardsLayout.childForceExpandHeight = true;

            _cardBackgrounds.Clear();
            _cardButtonLabels.Clear();

            for (int i = 0; i < _missions.Count; i++)
            {
                BuildMissionCard(i, _missions[i], cardsRow.transform, titleFont, textFont);
            }
        }

        private void BuildMissionCard(int index, MissionGoal mission, Transform parent, Font titleFont, Font textFont)
        {
            GameObject cardObj = CreateSlicedPanel("MissionCard_" + mission.Id, parent, cardPanelSprite,
                new Color(0.12f, 0.15f, 0.26f, 0.96f), 0f);
            _cardBackgrounds.Add(cardObj.GetComponent<Image>());

            VerticalLayoutGroup cardLayout = cardObj.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(38, 38, 36, 32);
            cardLayout.spacing = 12f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            // --- A. Card Top Row: Planet Icon Slot + Title & Orbit ---
            GameObject headerRow = CreateUIObject("CardHeaderRow", cardObj.transform);
            LayoutElement hrLe = headerRow.AddComponent<LayoutElement>();
            hrLe.preferredHeight = 76f;

            HorizontalLayoutGroup hrLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            hrLayout.spacing = 14f;
            hrLayout.childAlignment = TextAnchor.MiddleLeft;
            hrLayout.childControlWidth = true;
            hrLayout.childControlHeight = true;
            hrLayout.childForceExpandWidth = false;
            hrLayout.childForceExpandHeight = false;

            // Planet Slot (74x74)
            GameObject planetSlot = CreateSlicedPanel("PlanetSlot", headerRow.transform, iconSlotSprite,
                new Color(0.06f, 0.08f, 0.15f, 1f), 74f);
            LayoutElement slotLe = planetSlot.GetComponent<LayoutElement>();
            slotLe.preferredWidth = 74f;
            slotLe.minWidth = 74f;
            slotLe.minHeight = 74f;

            Sprite planetSprite = GetMissionPlanetSprite(mission.OrbitId);
            if (planetSprite != null)
            {
                GameObject planetObj = CreateUIObject("PlanetIcon", planetSlot.transform);
                RectTransform pRt = planetObj.GetComponent<RectTransform>();
                pRt.anchorMin = new Vector2(0.5f, 0.5f);
                pRt.anchorMax = new Vector2(0.5f, 0.5f);
                pRt.sizeDelta = new Vector2(48f, 48f);
                Image pImg = planetObj.AddComponent<Image>();
                if (planetSprite.texture != null) planetSprite.texture.filterMode = FilterMode.Point;
                pImg.sprite = planetSprite;
                pImg.preserveAspect = true;
            }

            // Title + Orbit Column
            GameObject titleCol = CreateUIObject("TitleColumn", headerRow.transform);
            LayoutElement tcLe = titleCol.AddComponent<LayoutElement>();
            tcLe.flexibleWidth = 1f;
            tcLe.preferredHeight = 74f;

            VerticalLayoutGroup tcLayout = titleCol.AddComponent<VerticalLayoutGroup>();
            tcLayout.spacing = 4f;
            tcLayout.childAlignment = TextAnchor.MiddleLeft;
            tcLayout.childControlWidth = true;
            tcLayout.childControlHeight = true;
            tcLayout.childForceExpandWidth = true;
            tcLayout.childForceExpandHeight = false;

            Text titleTxt = CreateText("Title", titleCol.transform, titleFont, mission.Title.ToUpperInvariant(), 18, FontStyle.Bold,
                Color.white, TextAnchor.LowerLeft);
            titleTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            Text orbitTxt = CreateText("OrbitSummary", titleCol.transform, textFont, GetShortOrbitTag(mission.OrbitId), 14, FontStyle.Normal,
                new Color(0.48f, 0.86f, 0.96f, 1f), TextAnchor.UpperLeft);
            orbitTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            // --- B. Short Brief Description ---
            Text briefTxt = CreateText("Brief", cardObj.transform, textFont, mission.Brief, 14, FontStyle.Normal,
                new Color(0.82f, 0.87f, 0.94f, 1f), TextAnchor.UpperLeft);
            briefTxt.lineSpacing = 1.15f;
            LayoutElement briefLe = briefTxt.gameObject.AddComponent<LayoutElement>();
            briefLe.preferredHeight = 84f;

            // --- C. Clean 5-Row Stat Box (dialogue_box.png) ---
            GameObject statsBox = CreateSlicedPanel("SpecsBox", cardObj.transform, innerBoxSprite,
                new Color(0.06f, 0.08f, 0.15f, 0.96f), 192f);

            VerticalLayoutGroup statsLayout = statsBox.AddComponent<VerticalLayoutGroup>();
            statsLayout.padding = new RectOffset(28, 24, 22, 22);
            statsLayout.spacing = 6f;
            statsLayout.childControlWidth = true;
            statsLayout.childControlHeight = true;
            statsLayout.childForceExpandWidth = true;
            statsLayout.childForceExpandHeight = true;

            string payloadTag = string.IsNullOrEmpty(mission.RequiredSensorTag) ? "ANY" : mission.RequiredSensorTag.ToUpperInvariant();
            CreateStatRow(statsBox.transform, textFont, "PAYLOAD", payloadTag, new Color(0.92f, 0.96f, 1f, 1f));
            CreateStatRow(statsBox.transform, textFont, "SCIENCE GOAL",
                string.Format("{0:0} PTS ({1} ORBITS)", mission.TargetSciencePoints, mission.RequiredOrbits),
                new Color(0.55f, 0.92f, 0.98f, 1f));
            CreateStatRow(statsBox.transform, textFont, "MAX BUDGET",
                string.Format("${0:0}K", mission.BudgetLimitThousands),
                new Color(0.92f, 0.96f, 1f, 1f));
            CreateStatRow(statsBox.transform, textFont, "REWARD",
                string.Format("+${0:0}K", mission.RewardThousands),
                new Color(1f, 0.85f, 0.38f, 1f));
            CreateStatRow(statsBox.transform, textFont, "CONDITIONS",
                GetShortHazardTag(mission.EventId),
                string.IsNullOrEmpty(mission.EventId) ? new Color(0.52f, 0.90f, 0.65f, 1f) : new Color(1f, 0.64f, 0.42f, 1f));

            // Spacer
            GameObject spacer = CreateUIObject("Spacer", cardObj.transform);
            spacer.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // --- D. Pixel-Art Button ---
            int capturedIndex = index;
            Button acceptBtn = CreatePixelButton(cardObj.transform, "Btn_AcceptContract_" + index,
                "ACCEPT MISSION", 62f, titleFont, () => SelectContract(capturedIndex));

            Text btnLabel = acceptBtn.GetComponentInChildren<Text>();
            _cardButtonLabels.Add(btnLabel);
        }

        private static void CreateStatRow(Transform parent, Font font, string label, string value, Color valueColor)
        {
            GameObject row = CreateUIObject("Row_" + label, parent);
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            Text lblTxt = CreateText("Label", row.transform, font, label, 14, FontStyle.Normal,
                new Color(0.58f, 0.66f, 0.80f, 1f), TextAnchor.MiddleLeft);
            LayoutElement lblLe = lblTxt.gameObject.AddComponent<LayoutElement>();
            lblLe.preferredWidth = 148f;
            lblLe.minWidth = 148f;

            Text valTxt = CreateText("Value", row.transform, font, value, 15, FontStyle.Bold,
                valueColor, TextAnchor.MiddleLeft);
            valTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void BindExistingHierarchy(Transform root)
        {
            _cardBackgrounds.Clear();
            _cardButtonLabels.Clear();

            Transform topBar = root.Find("TopHeaderBar");
            if (topBar != null)
            {
                _agencyBudgetText = FindChildComponent<Text>(topBar, "AgencyBudgetText");
            }

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

        private Sprite GetMissionPlanetSprite(string orbitId)
        {
            switch (orbitId)
            {
                case "orbit_lunar": return moonSprite;
                case "orbit_mars": return marsSprite;
                default: return earthSprite;
            }
        }

        private static string GetShortOrbitTag(string orbitId)
        {
            switch (orbitId)
            {
                case "orbit_leo": return "LOW EARTH ORBIT • 400 KM";
                case "orbit_lunar": return "LUNAR ORBIT • 100 KM";
                case "orbit_mars": return "MARS ORBIT • 350 KM";
                default: return "DEEP SPACE ORBIT";
            }
        }

        private static string GetShortHazardTag(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return "CLEAR WEATHER";
            switch (eventId)
            {
                case "evt_tx_overheat": return "TX OVERHEAT RISK";
                case "evt_cme_storm": return "SOLAR FLARE STORM";
                case "evt_micrometeoroid": return "MICROMETEOROIDS";
                default: return eventId.ToUpperInvariant();
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
                    Brief = "Launch a weather satellite to 400 km LEO, deliver 100 science points, and survive 4 orbits. Save battery power during eclipse.",
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
                    Brief = "Map lunar craters using a SAR Radar. Eclipse lasts 45% of each orbit, and a transmitter overheat occurs on Orbit 2.",
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
                    Brief = "Analyze Martian atmosphere with a Spectrometer. Solar flux is 43%, a solar flare hits on Orbit 3, and signal delay is 10s.",
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

        // =====================================================================
        // UI FACTORY HELPERS
        // =====================================================================

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

        private static GameObject CreateSlicedPanel(string name, Transform parent, Sprite sprite, Color fallbackColor, float preferredHeight)
        {
            GameObject panel = CreateUIObject(name, parent);
            Image img = panel.AddComponent<Image>();

            if (sprite != null)
            {
                if (sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
                img.sprite = sprite;
                img.color = Color.white;
                if (sprite.border != Vector4.zero)
                {
                    img.type = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = 0.35f;
                }
            }
            else
            {
                img.color = fallbackColor;
            }

            if (preferredHeight > 0f)
            {
                LayoutElement le = panel.AddComponent<LayoutElement>();
                le.preferredHeight = preferredHeight;
            }
            return panel;
        }

        private static Image CreateIcon(string name, Transform parent, Sprite sprite, float width, float height)
        {
            GameObject go = CreateUIObject(name, parent);
            Image img = go.AddComponent<Image>();
            if (sprite != null && sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = Color.white;

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;
            le.minHeight = height;
            return img;
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

        private Button CreatePixelButton(Transform parent, string name, string label, float height, Font font, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            Image img = btnObj.AddComponent<Image>();

            if (buttonNormalSprite != null)
            {
                if (buttonNormalSprite.texture != null) buttonNormalSprite.texture.filterMode = FilterMode.Point;
                img.sprite = buttonNormalSprite;
                img.color = Color.white;
                if (buttonNormalSprite.border != Vector4.zero)
                {
                    img.type = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = 0.35f;
                }
            }
            else
            {
                img.color = new Color(0.18f, 0.56f, 0.68f, 1f);
            }

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            if (buttonHoverSprite != null || buttonPressedSprite != null || buttonDisabledSprite != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                SpriteState st = btn.spriteState;
                if (buttonHoverSprite != null) st.highlightedSprite = buttonHoverSprite;
                if (buttonPressedSprite != null) st.pressedSprite = buttonPressedSprite;
                if (buttonDisabledSprite != null) st.disabledSprite = buttonDisabledSprite;
                btn.spriteState = st;
            }

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;

            Text txt = CreateText("Text", btnObj.transform, font, label, 18, FontStyle.Bold,
                new Color(0.06f, 0.12f, 0.18f, 1f), TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }
    }
}
