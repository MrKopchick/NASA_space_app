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
        private Text _carouselCounterText;
        private Button _prevMissionButton;
        private Button _nextMissionButton;
        private int _carouselIndex;

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
            rt.offsetMax = new Vector2(0f, -GameManager.GetReservedTopBarHeight(panelTransform));
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

            if (_missions.Count > 0)
            {
                _carouselIndex = Mathf.Clamp(_carouselIndex, 0, _missions.Count - 1);
            }
            else
            {
                _carouselIndex = 0;
            }

            for (int i = 0; i < _missions.Count && i < _cardBackgrounds.Count; i++)
            {
                bool isSelected = SelectedMission != null && SelectedMission.Id == _missions[i].Id;
                bool isCompleted = _agencyState.CompletedMissionIds != null && _agencyState.CompletedMissionIds.Contains(_missions[i].Id);

                if (_cardBackgrounds[i] != null)
                {
                    _cardBackgrounds[i].gameObject.SetActive(i == _carouselIndex);

                    if (_cardBackgrounds[i].sprite != null)
                    {
                        _cardBackgrounds[i].color = isSelected
                            ? new Color(0.88f, 0.94f, 1f, 0.54f)
                            : new Color(0.74f, 0.82f, 0.96f, 0.48f);
                    }
                    else
                    {
                        _cardBackgrounds[i].color = isSelected
                            ? new Color(0.14f, 0.19f, 0.32f, 0.56f)
                            : new Color(0.10f, 0.13f, 0.22f, 0.50f);
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

            if (_carouselCounterText != null)
            {
                _carouselCounterText.text = _missions.Count > 0
                    ? string.Format("MISSION {0}/{1}", _carouselIndex + 1, _missions.Count)
                    : "NO MISSIONS";
            }

            SetCarouselButtonEnabled(_prevMissionButton, _carouselIndex > 0);
            SetCarouselButtonEnabled(_nextMissionButton, _carouselIndex < _missions.Count - 1);
        }

        private void StepCarousel(int delta)
        {
            if (_missions.Count == 0) return;
            int next = Mathf.Clamp(_carouselIndex + delta, 0, _missions.Count - 1);
            if (next == _carouselIndex) return;
            _carouselIndex = next;
            RefreshCards();
        }

        private void SetCarouselButtonEnabled(Button btn, bool enabled)
        {
            if (btn == null) return;
            btn.interactable = enabled;

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                if (!enabled && buttonDisabledSprite != null)
                    img.sprite = buttonDisabledSprite;
                else if (enabled && buttonNormalSprite != null)
                    img.sprite = buttonNormalSprite;

                img.color = enabled ? Color.white : new Color(0.65f, 0.68f, 0.74f, 0.85f);
            }

            Text txt = btn.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                txt.color = enabled
                    ? new Color(0.06f, 0.12f, 0.18f, 1f)
                    : new Color(0.75f, 0.78f, 0.84f, 0.70f);
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
                backgroundImage.color = new Color(0.92f, 0.95f, 1f, 1f);
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
                if (existingRoot.Find("TopHeaderBar") != null && existingRoot.Find("LayoutV18Marker") != null)
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

            GameObject marker = CreateUIObject("LayoutV18Marker", rootObj.transform);
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
            topBarLayout.spacing = 12f;
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

            _carouselCounterText = CreateText("MissionCounterText", topBar.transform, titleFont,
                "MISSION 1/3", 16, FontStyle.Bold, new Color(0.55f, 0.90f, 1f, 1f), TextAnchor.MiddleRight);
            LayoutElement mcLe = _carouselCounterText.gameObject.AddComponent<LayoutElement>();
            mcLe.preferredWidth = 160f;
            mcLe.preferredHeight = 38f;

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

            // --- 2. FULL-SCREEN SINGLE-MISSION CAROUSEL (< | MISSION CARD | >) ---
            GameObject carouselRow = CreateUIObject("MissionCarouselRow", rootObj.transform);
            LayoutElement carouselLe = carouselRow.AddComponent<LayoutElement>();
            carouselLe.flexibleHeight = 1f;

            HorizontalLayoutGroup carouselLayout = carouselRow.AddComponent<HorizontalLayoutGroup>();
            carouselLayout.spacing = 24f;
            carouselLayout.childAlignment = TextAnchor.MiddleCenter;
            carouselLayout.childControlWidth = true;
            carouselLayout.childControlHeight = true;
            carouselLayout.childForceExpandWidth = false;
            carouselLayout.childForceExpandHeight = false;

            _prevMissionButton = CreatePixelButton(carouselRow.transform, "Btn_PrevMission", "<", 72f, titleFont,
                () => StepCarousel(-1));
            LayoutElement prevLe = _prevMissionButton.GetComponent<LayoutElement>();
            prevLe.preferredWidth = 72f;
            prevLe.minWidth = 72f;
            prevLe.preferredHeight = 72f;
            prevLe.minHeight = 72f;
            prevLe.flexibleWidth = 0f;
            Text prevTxt = _prevMissionButton.GetComponentInChildren<Text>();
            if (prevTxt != null) { prevTxt.fontSize = 20; prevTxt.horizontalOverflow = HorizontalWrapMode.Overflow; }

            GameObject cardsRow = CreateUIObject("MissionCardsContainer", carouselRow.transform);
            LayoutElement cardsRowLe = cardsRow.AddComponent<LayoutElement>();
            cardsRowLe.flexibleWidth = 1f;
            cardsRowLe.flexibleHeight = 1f;
            cardsRowLe.minHeight = 480f;
            cardsRowLe.preferredHeight = 640f;

            HorizontalLayoutGroup cardsLayout = cardsRow.AddComponent<HorizontalLayoutGroup>();
            cardsLayout.spacing = 0f;
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

            _nextMissionButton = CreatePixelButton(carouselRow.transform, "Btn_NextMission", ">", 72f, titleFont,
                () => StepCarousel(1));
            LayoutElement nextLe = _nextMissionButton.GetComponent<LayoutElement>();
            nextLe.preferredWidth = 72f;
            nextLe.minWidth = 72f;
            nextLe.preferredHeight = 72f;
            nextLe.minHeight = 72f;
            nextLe.flexibleWidth = 0f;
            Text nextTxt = _nextMissionButton.GetComponentInChildren<Text>();
            if (nextTxt != null) { nextTxt.fontSize = 20; nextTxt.horizontalOverflow = HorizontalWrapMode.Overflow; }

            ApplyCustomThemeToExistingUI(rootObj.transform);
        }

        [ContextMenu("Apply Custom Font & Sprites To Hub UI")]
        public void ApplyCustomThemeInEditor()
        {
            Transform hubRoot = GetHubRootTransform();
            Transform contentRoot = hubRoot != null ? hubRoot.Find("HubContentRoot") : null;
            if (contentRoot == null) contentRoot = transform.Find("HubContentRoot");
            if (contentRoot != null)
            {
                BindExistingHierarchy(contentRoot);
                ApplyCustomThemeToExistingUI(contentRoot);
                RefreshCards();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Assign Kenney Font & UI Sprites From Project")]
        public void AutoAssignKenneyFontAndSpritesInEditor()
        {
            if (headerFont == null)
                headerFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/Kenney Future.ttf");
            if (bodyFont == null)
                bodyFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/Kenney Mini Square Mono.ttf");
            if (cardPanelSprite == null)
                cardPanelSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/inventory_panel.png");
            if (innerBoxSprite == null)
                innerBoxSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/dialogue_box.png");
            if (buttonNormalSprite == null)
                buttonNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_normal.png");
            if (buttonHoverSprite == null)
                buttonHoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_hover.png");
            if (buttonPressedSprite == null)
                buttonPressedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_pressed.png");
            if (buttonDisabledSprite == null)
                buttonDisabledSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_disabled.png");

            UnityEditor.EditorUtility.SetDirty(this);
            ApplyCustomThemeInEditor();
            Debug.Log("[ContractHubUI] Assigned Kenney font & pixel UI sprites to Hub!");
        }
#endif

        private void ApplyCustomThemeToExistingUI(Transform contentRoot)
        {
            if (contentRoot == null) return;

            if (headerFont != null)
            {
                Text[] allTexts = contentRoot.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < allTexts.Length; i++)
                {
                    allTexts[i].font = headerFont;
                }
            }

            if (cardPanelSprite != null)
            {
                if (cardPanelSprite.texture != null) cardPanelSprite.texture.filterMode = FilterMode.Point;
                for (int i = 0; i < _cardBackgrounds.Count; i++)
                {
                    if (_cardBackgrounds[i] != null)
                    {
                        _cardBackgrounds[i].sprite = cardPanelSprite;
                        _cardBackgrounds[i].color = new Color(0.88f, 0.94f, 1f, 0.54f);
                    }
                }
            }

            if (innerBoxSprite != null)
            {
                if (innerBoxSprite.texture != null) innerBoxSprite.texture.filterMode = FilterMode.Point;
                Transform carouselRow = contentRoot.Find("MissionCarouselRow");
                Transform cardsContainer = carouselRow != null
                    ? carouselRow.Find("MissionCardsContainer")
                    : contentRoot.Find("MissionCardsContainer");
                if (cardsContainer != null)
                {
                    Image[] innerImages = cardsContainer.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < innerImages.Length; i++)
                    {
                        string objName = innerImages[i].gameObject.name;
                        if (objName == "PlanetBox" || objName == "BriefBox" || objName == "SpecsBox" || objName == "OrbitBox")
                        {
                            innerImages[i].sprite = innerBoxSprite;
                            innerImages[i].color = new Color(0.75f, 0.84f, 0.98f, 0.50f);
                        }
                    }
                }
            }

            if (buttonNormalSprite != null)
            {
                if (buttonNormalSprite.texture != null) buttonNormalSprite.texture.filterMode = FilterMode.Point;
                Button[] allButtons = contentRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < allButtons.Length; i++)
                {
                    Image btnImg = allButtons[i].GetComponent<Image>();
                    if (btnImg != null)
                    {
                        btnImg.sprite = buttonNormalSprite;
                        btnImg.color = Color.white;
                    }

                    if (buttonHoverSprite != null || buttonPressedSprite != null || buttonDisabledSprite != null)
                    {
                        allButtons[i].transition = Selectable.Transition.SpriteSwap;
                        SpriteState st = allButtons[i].spriteState;
                        if (buttonHoverSprite != null) st.highlightedSprite = buttonHoverSprite;
                        if (buttonPressedSprite != null) st.pressedSprite = buttonPressedSprite;
                        if (buttonDisabledSprite != null) st.disabledSprite = buttonDisabledSprite;
                        allButtons[i].spriteState = st;
                    }
                }
            }
        }

        private void BuildMissionCard(int index, MissionGoal mission, Transform parent, Font titleFont, Font textFont)
        {
            GameObject cardObj = CreateSlicedPanel("MissionCard_" + mission.Id, parent, cardPanelSprite,
                new Color(0.84f, 0.92f, 1f, 0.72f), 0f);
            _cardBackgrounds.Add(cardObj.GetComponent<Image>());

            VerticalLayoutGroup cardLayout = cardObj.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(58, 58, 28, 28);
            cardLayout.spacing = 14f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            // --- A. Top Centered Title & Orbit Subtitle ---
            GameObject headerCol = CreateUIObject("CardHeaderColumn", cardObj.transform);
            LayoutElement hcLe = headerCol.AddComponent<LayoutElement>();
            hcLe.preferredHeight = 62f;

            VerticalLayoutGroup hcLayout = headerCol.AddComponent<VerticalLayoutGroup>();
            hcLayout.spacing = 4f;
            hcLayout.childAlignment = TextAnchor.MiddleCenter;
            hcLayout.childControlWidth = true;
            hcLayout.childControlHeight = true;
            hcLayout.childForceExpandWidth = true;
            hcLayout.childForceExpandHeight = false;

            Text titleTxt = CreateText("Title", headerCol.transform, titleFont, mission.Title.ToUpperInvariant(), 22, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            titleTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

            Text orbitTxt = CreateText("OrbitSummary", headerCol.transform, textFont, GetShortOrbitTag(mission.OrbitId), 14, FontStyle.Normal,
                new Color(0.48f, 0.86f, 0.96f, 1f), TextAnchor.MiddleCenter);
            orbitTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            // --- B. Middle 3-Column Body Row: Planet (Left) | Brief (Center) | Specs & Telemetry (Right) ---
            GameObject bodyRow = CreateUIObject("BodyRow", cardObj.transform);
            LayoutElement bodyLe = bodyRow.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1f;
            bodyLe.minHeight = 320f;

            HorizontalLayoutGroup bodyH = bodyRow.AddComponent<HorizontalLayoutGroup>();
            bodyH.spacing = 18f;
            bodyH.childAlignment = TextAnchor.MiddleCenter;
            bodyH.childControlWidth = true;
            bodyH.childControlHeight = true;
            bodyH.childForceExpandWidth = false;
            bodyH.childForceExpandHeight = true;

            // 1. Left Column: Frameless Planet Icon shifted slightly to the right
            GameObject planetCol = CreateUIObject("PlanetColumn", bodyRow.transform);
            LayoutElement planetColLe = planetCol.AddComponent<LayoutElement>();
            planetColLe.preferredWidth = 310f;
            planetColLe.minWidth = 310f;
            planetColLe.flexibleWidth = 0f;
            planetColLe.flexibleHeight = 1f;

            VerticalLayoutGroup planetColLayout = planetCol.AddComponent<VerticalLayoutGroup>();
            planetColLayout.padding = new RectOffset(38, 4, 24, 24);
            planetColLayout.childAlignment = TextAnchor.MiddleCenter;
            planetColLayout.childControlWidth = true;
            planetColLayout.childControlHeight = true;
            planetColLayout.childForceExpandWidth = true;
            planetColLayout.childForceExpandHeight = true;

            Sprite planetSprite = GetMissionPlanetSprite(mission.OrbitId);
            if (planetSprite != null)
            {
                GameObject planetObj = CreateUIObject("PlanetIcon", planetCol.transform);
                LayoutElement pLe = planetObj.AddComponent<LayoutElement>();
                pLe.flexibleWidth = 1f;
                pLe.flexibleHeight = 1f;
                Image pImg = planetObj.AddComponent<Image>();
                if (planetSprite.texture != null) planetSprite.texture.filterMode = FilterMode.Point;
                pImg.sprite = planetSprite;
                pImg.preserveAspect = true;
            }

            OrbitDefinition orbit = _repository != null ? _repository.GetOrbit(mission.OrbitId) : null;

            // 2. Center Column: Compact Mission Brief Box with Extended Briefing
            GameObject briefBox = CreateSlicedPanel("BriefBox", bodyRow.transform, innerBoxSprite,
                new Color(0.75f, 0.84f, 0.98f, 0.50f), 0f);
            Image briefBoxImg = briefBox.GetComponent<Image>();
            if (briefBoxImg != null) briefBoxImg.color = new Color(0.75f, 0.84f, 0.98f, 0.50f);
            LayoutElement briefBoxLe = briefBox.AddComponent<LayoutElement>();
            briefBoxLe.preferredWidth = 360f;
            briefBoxLe.minWidth = 300f;
            briefBoxLe.flexibleWidth = 0.8f;
            briefBoxLe.flexibleHeight = 1f;

            VerticalLayoutGroup briefLayout = briefBox.AddComponent<VerticalLayoutGroup>();
            briefLayout.padding = new RectOffset(22, 22, 16, 16);
            briefLayout.spacing = 8f;
            briefLayout.childControlWidth = true;
            briefLayout.childControlHeight = true;
            briefLayout.childForceExpandWidth = true;
            briefLayout.childForceExpandHeight = false;

            Text briefHeader = CreateText("BriefHeader", briefBox.transform, titleFont, "MISSION BRIEF", 12, FontStyle.Bold,
                new Color(0.48f, 0.86f, 0.96f, 1f), TextAnchor.MiddleLeft);
            briefHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            string extendedBrief = BuildExtendedMissionBrief(mission, orbit);
            Text briefTxt = CreateText("Brief", briefBox.transform, textFont, extendedBrief, 14, FontStyle.Normal,
                new Color(0.92f, 0.95f, 1f, 1f), TextAnchor.UpperLeft);
            briefTxt.lineSpacing = 1.28f;
            briefTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            briefTxt.verticalOverflow = VerticalWrapMode.Truncate;
            LayoutElement briefTxtLe = briefTxt.gameObject.AddComponent<LayoutElement>();
            briefTxtLe.flexibleHeight = 1f;

            // 3. Right Column: Stacked Contract Specs & Orbital Telemetry Boxes
            GameObject infoCol = CreateUIObject("InfoColumn", bodyRow.transform);
            LayoutElement infoColLe = infoCol.AddComponent<LayoutElement>();
            infoColLe.preferredWidth = 440f;
            infoColLe.minWidth = 380f;
            infoColLe.flexibleWidth = 1.15f;
            infoColLe.flexibleHeight = 1f;

            VerticalLayoutGroup infoColLayout = infoCol.AddComponent<VerticalLayoutGroup>();
            infoColLayout.spacing = 12f;
            infoColLayout.childControlWidth = true;
            infoColLayout.childControlHeight = true;
            infoColLayout.childForceExpandWidth = true;
            infoColLayout.childForceExpandHeight = true;

            // Top-Right: Contract Specs Box
            GameObject statsBox = CreateSlicedPanel("SpecsBox", infoCol.transform, innerBoxSprite,
                new Color(0.75f, 0.84f, 0.98f, 0.50f), 0f);
            Image statsBoxImg = statsBox.GetComponent<Image>();
            if (statsBoxImg != null) statsBoxImg.color = new Color(0.75f, 0.84f, 0.98f, 0.50f);
            statsBox.AddComponent<LayoutElement>().flexibleHeight = 1f;

            VerticalLayoutGroup statsLayout = statsBox.AddComponent<VerticalLayoutGroup>();
            statsLayout.padding = new RectOffset(22, 20, 14, 14);
            statsLayout.spacing = 4f;
            statsLayout.childControlWidth = true;
            statsLayout.childControlHeight = true;
            statsLayout.childForceExpandWidth = true;
            statsLayout.childForceExpandHeight = true;

            CreateText("SpecsHeader", statsBox.transform, titleFont, "CONTRACT SPECS", 12, FontStyle.Bold,
                new Color(0.48f, 0.86f, 0.96f, 1f), TextAnchor.MiddleLeft);

            string payloadTag = string.IsNullOrEmpty(mission.RequiredSensorTag) ? "ANY" : mission.RequiredSensorTag.ToUpperInvariant();
            int totalTicks = mission.RequiredOrbits * 20;
            CreateStatRow(statsBox.transform, textFont, "PAYLOAD", payloadTag, new Color(0.92f, 0.96f, 1f, 1f));
            CreateStatRow(statsBox.transform, textFont, "SCIENCE GOAL",
                string.Format("{0:0} PTS ({1} ORB / {2}T)", mission.TargetSciencePoints, mission.RequiredOrbits, totalTicks),
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

            // Bottom-Right: Orbital Telemetry Box
            GameObject orbitBox = CreateSlicedPanel("OrbitBox", infoCol.transform, innerBoxSprite,
                new Color(0.75f, 0.84f, 0.98f, 0.50f), 0f);
            Image orbitBoxImg = orbitBox.GetComponent<Image>();
            if (orbitBoxImg != null) orbitBoxImg.color = new Color(0.75f, 0.84f, 0.98f, 0.50f);
            orbitBox.AddComponent<LayoutElement>().flexibleHeight = 1f;

            VerticalLayoutGroup orbitLayout = orbitBox.AddComponent<VerticalLayoutGroup>();
            orbitLayout.padding = new RectOffset(22, 20, 14, 14);
            orbitLayout.spacing = 4f;
            orbitLayout.childControlWidth = true;
            orbitLayout.childControlHeight = true;
            orbitLayout.childForceExpandWidth = true;
            orbitLayout.childForceExpandHeight = true;

            CreateText("OrbitHeader", orbitBox.transform, titleFont, "ORBITAL TELEMETRY", 12, FontStyle.Bold,
                new Color(1f, 0.82f, 0.38f, 1f), TextAnchor.MiddleLeft);

            float solarPct = orbit != null ? orbit.SolarFluxFactor * 100f : 100f;
            float eclipsePct = orbit != null ? orbit.EclipseFraction * 100f : 35f;
            float commPct = orbit != null ? orbit.CommWindowFraction * 100f : 25f;
            float delaySec = orbit != null ? orbit.SignalDelaySeconds : 1f;
            float radVal = orbit != null ? orbit.BaseRadiation : 5f;

            CreateStatRow(orbitBox.transform, textFont, "SOLAR FLUX",
                string.Format("{0:0}%", solarPct),
                solarPct < 60f ? new Color(1f, 0.68f, 0.38f, 1f) : new Color(1f, 0.90f, 0.45f, 1f));
            CreateStatRow(orbitBox.transform, textFont, "ECLIPSE",
                string.Format("{0:0}% / ORBIT", eclipsePct),
                new Color(0.75f, 0.84f, 1f, 1f));
            CreateStatRow(orbitBox.transform, textFont, "COMM WINDOW",
                string.Format("{0:0}% / ORBIT", commPct),
                new Color(0.55f, 0.92f, 0.98f, 1f));
            CreateStatRow(orbitBox.transform, textFont, "SIGNAL DELAY",
                string.Format("{0:0.0} S", delaySec),
                delaySec >= 5f ? new Color(1f, 0.72f, 0.42f, 1f) : new Color(0.92f, 0.96f, 1f, 1f));
            CreateStatRow(orbitBox.transform, textFont, "RADIATION",
                string.Format("{0:0} RAD/T", radVal),
                radVal >= 15f ? new Color(1f, 0.58f, 0.48f, 1f) : new Color(0.52f, 0.90f, 0.65f, 1f));

            // --- C. Pixel-Art Button ---
            int capturedIndex = index;
            Button acceptBtn = CreatePixelButton(cardObj.transform, "Btn_AcceptContract_" + index,
                "ACCEPT MISSION", 56f, titleFont, () => SelectContract(capturedIndex));

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
                _carouselCounterText = FindChildComponent<Text>(topBar, "MissionCounterText");
            }

            Transform carouselRow = root.Find("MissionCarouselRow");
            if (carouselRow != null)
            {
                _prevMissionButton = FindChildComponent<Button>(carouselRow, "Btn_PrevMission");
                if (_prevMissionButton != null)
                {
                    _prevMissionButton.onClick.RemoveAllListeners();
                    _prevMissionButton.onClick.AddListener(() => StepCarousel(-1));
                }

                _nextMissionButton = FindChildComponent<Button>(carouselRow, "Btn_NextMission");
                if (_nextMissionButton != null)
                {
                    _nextMissionButton.onClick.RemoveAllListeners();
                    _nextMissionButton.onClick.AddListener(() => StepCarousel(1));
                }
            }

            Transform cardsContainer = carouselRow != null
                ? carouselRow.Find("MissionCardsContainer")
                : root.Find("MissionCardsContainer");
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
                case "orbit_leo": return "LOW EARTH ORBIT • 400 KM • 20 TICKS/ORBIT";
                case "orbit_lunar": return "LUNAR ORBIT • 100 KM • 20 TICKS/ORBIT";
                case "orbit_mars": return "MARS ORBIT • 350 KM • 20 TICKS/ORBIT";
                default: return "DEEP SPACE ORBIT • 20 TICKS/ORBIT";
            }
        }

        private static string BuildExtendedMissionBrief(MissionGoal mission, OrbitDefinition orbit)
        {
            string baseBrief = mission != null && !string.IsNullOrEmpty(mission.Brief)
                ? mission.Brief.Trim()
                : "Deploy spacecraft to target orbit and complete scientific telemetry objectives.";

            string sensor = mission != null && !string.IsNullOrEmpty(mission.RequiredSensorTag)
                ? mission.RequiredSensorTag.ToUpperInvariant()
                : "SCIENCE";

            float solarPct = orbit != null ? orbit.SolarFluxFactor * 100f : 100f;
            float eclipsePct = orbit != null ? orbit.EclipseFraction * 100f : 35f;
            float commPct = orbit != null ? orbit.CommWindowFraction * 100f : 25f;

            string advisory;
            switch (mission != null ? mission.OrbitId : string.Empty)
            {
                case "orbit_lunar":
                    advisory = "FLIGHT ADVISORY: Extended lunar eclipse windows cut solar generation nearly in half each pass. Pair high-capacity Li-Ion storage with thermal/TX autopilot rules.";
                    break;
                case "orbit_mars":
                    advisory = "FLIGHT ADVISORY: Deep-space solar flux drops to " + solarPct.ToString("0") + "% and 10s signal delay prevents manual intervention. Autonomous fault-protection rules are mandatory.";
                    break;
                default:
                    advisory = "FLIGHT ADVISORY: Standard 400 km LEO thermal and radiation profile. Balance solar array output against " + eclipsePct.ToString("0") + "% eclipse phases and downlink during the " + commPct.ToString("0") + "% comm window.";
                    break;
            }

            string opsNote = "OPERATIONS: Equip a " + sensor + " payload, verify mass & power margins in the Hangar, and configure A.S.T.R.A. autopilot before launch.";
            return baseBrief + "\n\n" + advisory + "\n\n" + opsNote;
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
