using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Common
{
    /// <summary>
    /// Displays the interactive 30-second 3-step onboarding guide explaining Build, Autopilot/Dry Run, and Sensor-SSD-CommWindow mechanics (Task UX-05).
    /// </summary>
    public class OnboardingHelpUI : MonoBehaviour
    {
        public const string HideOnboardingPrefsKey = "AstraLink_HideOnboarding";

        private struct StepDefinition
        {
            public string TabLabel;
            public string Title;
            public string Badge;
            public Color AccentColor;
            public string Body;
        }

        private static readonly StepDefinition[] Steps =
        {
            new StepDefinition
            {
                TabLabel = "1. BUILD",
                Title = "BUILD YOUR SATELLITE",
                Badge = "MASS <= ROCKET LIMIT  |  BUDGET <= CONTRACT",
                AccentColor = new Color(0.16f, 0.44f, 0.72f, 1f),
                Body =
                    "Select a Contract in Hub and check its required Science Sensor.\n\n" +
                    "Keep total spacecraft mass within the Rocket payload limit and total cost within the Contract budget.\n\n" +
                    "Equip all 5 slots in Workshop: Bus, Power (Solar/RTG), Battery, Comms+SSD, and Science Sensor."
            },
            new StepDefinition
            {
                TabLabel = "2. AUTOPILOT & DRY RUN",
                Title = "AUTOPILOT & DRY RUN",
                Badge = "1 ORBIT = 20 TICKS  |  ECLIPSE = 0% SOLAR",
                AccentColor = new Color(0.68f, 0.44f, 0.14f, 1f),
                Body =
                    "Every orbit passes through an Eclipse phase (0% sunlight) where Solar panels stop charging.\n\n" +
                    "Add an Autopilot rule: IF In Eclipse -> Turn Science Sensor OFF to protect your battery.\n\n" +
                    "Run the 3s Dry Run bench test (20 ticks = 1 orbit) to verify survival before launching."
            },
            new StepDefinition
            {
                TabLabel = "3. FLIGHT & DATA",
                Title = "FLIGHT & SCIENCE DATA",
                Badge = "SENSOR -> SSD -> COMM WINDOW -> SCIENCE",
                AccentColor = new Color(0.14f, 0.56f, 0.36f, 1f),
                Body =
                    "Your active Science Sensor records raw data onto onboard SSD memory.\n\n" +
                    "Raw SSD data converts into Science Points ONLY during a Comm Window.\n\n" +
                    "If SSD reaches 100% full, new science data is lost — downlink in time!"
            }
        };

        private static OnboardingHelpUI _instance;
        private static bool _shownThisSession;

        private GameObject _modalRoot;
        private Toggle _dontShowToggle;
        private int _currentStepIndex;

        private Image _cardOutlineImage;
        private Outline _cardOutline;
        private Image _badgeBg;
        private Image _stepImage;
        private Outline _stepImageOutline;
        private Text _stepImagePlaceholderText;
        private Text _stepTitleText;
        private Text _stepBadgeText;
        private Text _stepBodyText;
        private Button _prevStepButton;
        private Button _nextStepButton;
        private readonly Image[] _tabBackgrounds = new Image[3];

        /// <summary>
        /// Gets a value indicating whether the user opted out of automatic onboarding display.
        /// </summary>
        public static bool IsAutoShowDisabled
        {
            get { return PlayerPrefs.GetInt(HideOnboardingPrefsKey, 0) == 1; }
        }

        /// <summary>
        /// Opens the onboarding guide modal on the specified canvas root.
        /// </summary>
        public static void Show(Transform canvasRoot)
        {
            if (canvasRoot == null)
            {
                return;
            }

            OnboardingHelpUI ui = EnsureInstance(canvasRoot);
            ui.OpenModal();
        }

        /// <summary>
        /// Automatically opens the onboarding guide once per session when starting a new campaign unless disabled by the user.
        /// </summary>
        public static void ShowIfFirstCampaign(Transform canvasRoot, bool isFreshCampaign)
        {
            if (_shownThisSession || !isFreshCampaign || IsAutoShowDisabled || canvasRoot == null)
            {
                return;
            }

            _shownThisSession = true;
            Show(canvasRoot);
        }

        /// <summary>
        /// Opens the onboarding modal on step 1 and synchronizes the checkbox state from PlayerPrefs.
        /// </summary>
        public void OpenModal()
        {
            EnsureModalHierarchy();
            if (_dontShowToggle != null)
            {
                _dontShowToggle.SetIsOnWithoutNotify(IsAutoShowDisabled);
            }

            ShowStep(0);

            if (_modalRoot != null)
            {
                _modalRoot.SetActive(true);
                _modalRoot.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// Closes the onboarding modal and persists the don't-show-again preference.
        /// </summary>
        public void CloseModal()
        {
            if (_dontShowToggle != null)
            {
                PlayerPrefs.SetInt(HideOnboardingPrefsKey, _dontShowToggle.isOn ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (_modalRoot != null)
            {
                _modalRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Advances to the previous or next onboarding step card.
        /// </summary>
        public void StepBy(int delta)
        {
            int nextIndex = (_currentStepIndex + delta + Steps.Length) % Steps.Length;
            ShowStep(nextIndex);
        }

        /// <summary>
        /// Displays the onboarding step card at the specified zero-based index.
        /// </summary>
        public void ShowStep(int index)
        {
            if (index < 0 || index >= Steps.Length)
            {
                return;
            }

            _currentStepIndex = index;
            StepDefinition step = Steps[index];

            if (_cardOutline != null)
            {
                _cardOutline.effectColor = step.AccentColor;
            }

            if (_badgeBg != null)
            {
                _badgeBg.color = step.AccentColor;
            }

            if (_stepTitleText != null)
            {
                _stepTitleText.text = step.Title;
            }

            if (_stepBadgeText != null)
            {
                _stepBadgeText.text = step.Badge;
            }

            if (_stepBodyText != null)
            {
                _stepBodyText.text = step.Body;
            }

            if (_stepImageOutline != null)
            {
                _stepImageOutline.effectColor = step.AccentColor;
            }

            if (_stepImage != null)
            {
                Sprite loaded = Resources.Load<Sprite>("Onboarding/step_" + (index + 1));
                if (loaded != null)
                {
                    _stepImage.sprite = loaded;
                    _stepImage.color = Color.white;
                    if (_stepImagePlaceholderText != null)
                    {
                        _stepImagePlaceholderText.gameObject.SetActive(false);
                    }
                }
                else
                {
                    _stepImage.sprite = null;
                    _stepImage.color = new Color(0.05f, 0.09f, 0.16f, 1f);
                    if (_stepImagePlaceholderText != null)
                    {
                        _stepImagePlaceholderText.gameObject.SetActive(true);
                        _stepImagePlaceholderText.text = "IMAGE PLACEHOLDER\n(" + step.TabLabel + ")";
                    }
                }
            }

            for (int i = 0; i < _tabBackgrounds.Length; i++)
            {
                if (_tabBackgrounds[i] != null)
                {
                    _tabBackgrounds[i].color = i == index
                        ? Steps[i].AccentColor
                        : new Color(0.11f, 0.16f, 0.25f, 1f);
                }
            }
        }

        private void Update()
        {
            if (_modalRoot == null || !_modalRoot.activeInHierarchy)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                StepBy(-1);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                StepBy(1);
            }
        }

        private static OnboardingHelpUI EnsureInstance(Transform canvasRoot)
        {
            if (_instance != null && _instance.transform.parent == canvasRoot)
            {
                return _instance;
            }

            Transform existing = canvasRoot.Find("OnboardingHelpOverlay");
            if (existing != null)
            {
                _instance = existing.GetComponent<OnboardingHelpUI>();
                if (_instance == null)
                {
                    _instance = existing.gameObject.AddComponent<OnboardingHelpUI>();
                }
                _instance._modalRoot = existing.gameObject;
                return _instance;
            }

            GameObject overlay = new GameObject("OnboardingHelpOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(canvasRoot, false);
            RectTransform rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = overlay.GetComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.08f, 0.86f);

            _instance = overlay.AddComponent<OnboardingHelpUI>();
            _instance._modalRoot = overlay;
            _instance.EnsureModalHierarchy();
            return _instance;
        }

        private void EnsureModalHierarchy()
        {
            if (_modalRoot == null)
            {
                _modalRoot = gameObject;
            }

            if (_modalRoot.transform.Find("OnboardingWindow") != null && _stepBodyText != null)
            {
                _dontShowToggle = _modalRoot.transform.Find("OnboardingWindow/FooterRow/DontShowAgainToggle")?.GetComponent<Toggle>();
                return;
            }

            Transform oldWindow = _modalRoot.transform.Find("OnboardingWindow");
            if (oldWindow != null)
            {
                Destroy(oldWindow.gameObject);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject window = CreatePanel("OnboardingWindow", _modalRoot.transform, new Color(0.06f, 0.10f, 0.17f, 0.99f));
            RectTransform winRt = window.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.10f, 0.10f);
            winRt.anchorMax = new Vector2(0.90f, 0.90f);
            winRt.offsetMin = Vector2.zero;
            winRt.offsetMax = Vector2.zero;

            Outline winOutline = window.AddComponent<Outline>();
            winOutline.effectColor = new Color(0.24f, 0.62f, 0.94f, 0.85f);
            winOutline.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup winLayout = window.AddComponent<VerticalLayoutGroup>();
            winLayout.padding = new RectOffset(28, 28, 22, 22);
            winLayout.spacing = 14f;
            winLayout.childControlWidth = true;
            winLayout.childControlHeight = true;
            winLayout.childForceExpandWidth = true;
            winLayout.childForceExpandHeight = false;

            GameObject headerBox = CreatePanel("HeaderBox", window.transform, new Color(0.04f, 0.07f, 0.13f, 0.95f));
            LayoutElement headerLe = headerBox.AddComponent<LayoutElement>();
            headerLe.preferredHeight = 86f;
            headerLe.flexibleHeight = 0f;
            VerticalLayoutGroup headerLayout = headerBox.AddComponent<VerticalLayoutGroup>();
            headerLayout.padding = new RectOffset(20, 20, 12, 12);
            headerLayout.spacing = 4f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = false;

            Text title = CreateText("Title", headerBox.transform, font,
                "HOW ASTRALINK WORKS — 30-SECOND MISSION GUIDE", 26, FontStyle.Bold,
                new Color(0.92f, 0.97f, 1f, 1f), TextAnchor.MiddleLeft);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

            Text subtitle = CreateText("Subtitle", headerBox.transform, font,
                "1 Orbit = 20 Ticks  |  Sensor -> SSD Storage -> Comm Window Downlink -> Science Points",
                18, FontStyle.Bold, new Color(0.42f, 0.86f, 0.96f, 1f), TextAnchor.MiddleLeft);
            subtitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;

            GameObject tabsRow = new GameObject("TabsRow", typeof(RectTransform));
            tabsRow.transform.SetParent(window.transform, false);
            LayoutElement tabsLe = tabsRow.AddComponent<LayoutElement>();
            tabsLe.preferredHeight = 42f;
            tabsLe.flexibleHeight = 0f;
            HorizontalLayoutGroup tabsLayout = tabsRow.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 12f;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            for (int i = 0; i < Steps.Length; i++)
            {
                int capturedIndex = i;
                Button tabBtn = CreateButton(tabsRow.transform, font, "Btn_StepTab_" + (i + 1), Steps[i].TabLabel,
                    new Color(0.11f, 0.16f, 0.25f, 1f), 0f, 16, () => ShowStep(capturedIndex));
                _tabBackgrounds[i] = tabBtn.GetComponent<Image>();
            }

            GameObject carouselRow = new GameObject("CarouselRow", typeof(RectTransform));
            carouselRow.transform.SetParent(window.transform, false);
            LayoutElement carouselLe = carouselRow.AddComponent<LayoutElement>();
            carouselLe.flexibleHeight = 1f;
            HorizontalLayoutGroup carouselLayout = carouselRow.AddComponent<HorizontalLayoutGroup>();
            carouselLayout.spacing = 16f;
            carouselLayout.childAlignment = TextAnchor.MiddleCenter;
            carouselLayout.childControlWidth = true;
            carouselLayout.childControlHeight = true;
            carouselLayout.childForceExpandWidth = false;
            carouselLayout.childForceExpandHeight = false;

            _prevStepButton = CreateButton(carouselRow.transform, font, "Btn_PrevOnboardingStep", "<",
                new Color(0.14f, 0.24f, 0.38f, 1f), 68f, 30, () => StepBy(-1));
            LayoutElement prevLe = _prevStepButton.gameObject.AddComponent<LayoutElement>();
            prevLe.preferredWidth = 68f;
            prevLe.preferredHeight = 88f;
            prevLe.flexibleWidth = 0f;
            prevLe.flexibleHeight = 0f;

            GameObject card = CreatePanel("ActiveStepCard", carouselRow.transform, new Color(0.08f, 0.13f, 0.22f, 0.96f));
            LayoutElement cardLe = card.AddComponent<LayoutElement>();
            cardLe.minWidth = 600f;
            cardLe.preferredWidth = 2000f;
            cardLe.flexibleWidth = 1f;
            cardLe.flexibleHeight = 1f;
            cardLe.minHeight = 360f;

            _cardOutlineImage = card.GetComponent<Image>();
            _cardOutline = card.AddComponent<Outline>();
            _cardOutline.effectColor = Steps[0].AccentColor;
            _cardOutline.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(24, 24, 22, 20);
            cardLayout.spacing = 16f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            GameObject badge = CreatePanel("Badge", card.transform, Steps[0].AccentColor);
            _badgeBg = badge.GetComponent<Image>();
            LayoutElement badgeLe = badge.AddComponent<LayoutElement>();
            badgeLe.preferredHeight = 80f;
            badgeLe.flexibleHeight = 0f;
            VerticalLayoutGroup badgeLayout = badge.AddComponent<VerticalLayoutGroup>();
            badgeLayout.padding = new RectOffset(18, 18, 10, 10);
            badgeLayout.spacing = 4f;
            badgeLayout.childControlWidth = true;
            badgeLayout.childControlHeight = true;
            badgeLayout.childForceExpandWidth = true;
            badgeLayout.childForceExpandHeight = true;

            _stepTitleText = CreateText("StepTitle", badge.transform, font, Steps[0].Title, 25, FontStyle.Bold,
                Color.white, TextAnchor.UpperLeft);
            _stepBadgeText = CreateText("StepSub", badge.transform, font, Steps[0].Badge, 16, FontStyle.Bold,
                new Color(0.92f, 0.97f, 1f, 0.95f), TextAnchor.LowerLeft);

            GameObject contentRow = new GameObject("ContentRow", typeof(RectTransform));
            contentRow.transform.SetParent(card.transform, false);
            LayoutElement contentRowLe = contentRow.AddComponent<LayoutElement>();
            contentRowLe.flexibleHeight = 1f;
            HorizontalLayoutGroup contentRowLayout = contentRow.AddComponent<HorizontalLayoutGroup>();
            contentRowLayout.padding = new RectOffset(12, 24, 0, 0);
            contentRowLayout.spacing = 96f;
            contentRowLayout.childAlignment = TextAnchor.MiddleLeft;
            contentRowLayout.childControlWidth = true;
            contentRowLayout.childControlHeight = true;
            contentRowLayout.childForceExpandWidth = false;
            contentRowLayout.childForceExpandHeight = false;

            GameObject imageSlot = CreatePanel("StepImagePlaceholder", contentRow.transform, new Color(0.05f, 0.09f, 0.16f, 1f));
            _stepImage = imageSlot.GetComponent<Image>();
            _stepImage.preserveAspect = true;
            LayoutElement imageLe = imageSlot.AddComponent<LayoutElement>();
            imageLe.minWidth = 340f;
            imageLe.minHeight = 340f;
            imageLe.preferredWidth = 340f;
            imageLe.preferredHeight = 340f;
            imageLe.flexibleWidth = 0f;
            imageLe.flexibleHeight = 0f;

            _stepImageOutline = imageSlot.AddComponent<Outline>();
            _stepImageOutline.effectColor = Steps[0].AccentColor;
            _stepImageOutline.effectDistance = new Vector2(2f, -2f);

            _stepImagePlaceholderText = CreateText("ImagePlaceholderLabel", imageSlot.transform, font,
                "IMAGE PLACEHOLDER\n(1. BUILD)", 16, FontStyle.Bold,
                new Color(0.55f, 0.72f, 0.88f, 0.85f), TextAnchor.MiddleCenter);
            RectTransform imgTxtRt = _stepImagePlaceholderText.GetComponent<RectTransform>();
            imgTxtRt.anchorMin = Vector2.zero;
            imgTxtRt.anchorMax = Vector2.one;
            imgTxtRt.offsetMin = new Vector2(12f, 12f);
            imgTxtRt.offsetMax = new Vector2(-12f, -12f);

            _stepBodyText = CreateText("StepBody", contentRow.transform, font, Steps[0].Body, 23, FontStyle.Normal,
                new Color(0.90f, 0.95f, 0.99f, 1f), TextAnchor.MiddleLeft);
            _stepBodyText.lineSpacing = 1.25f;
            LayoutElement bodyLe = _stepBodyText.gameObject.AddComponent<LayoutElement>();
            bodyLe.minWidth = 280f;
            bodyLe.preferredWidth = 1200f;
            bodyLe.flexibleWidth = 1f;
            bodyLe.flexibleHeight = 1f;

            _nextStepButton = CreateButton(carouselRow.transform, font, "Btn_NextOnboardingStep", ">",
                new Color(0.14f, 0.24f, 0.38f, 1f), 68f, 30, () => StepBy(1));
            LayoutElement nextLe = _nextStepButton.gameObject.AddComponent<LayoutElement>();
            nextLe.preferredWidth = 68f;
            nextLe.preferredHeight = 88f;
            nextLe.flexibleWidth = 0f;
            nextLe.flexibleHeight = 0f;

            GameObject footerRow = new GameObject("FooterRow", typeof(RectTransform));
            footerRow.transform.SetParent(window.transform, false);
            LayoutElement footerLe = footerRow.AddComponent<LayoutElement>();
            footerLe.preferredHeight = 46f;
            footerLe.flexibleHeight = 0f;
            HorizontalLayoutGroup footerLayout = footerRow.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 20f;
            footerLayout.childAlignment = TextAnchor.MiddleLeft;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;

            _dontShowToggle = CreateCheckbox(footerRow.transform, font, "DontShowAgainToggle", "Don't show automatically on campaign start");
            LayoutElement toggleLe = _dontShowToggle.gameObject.AddComponent<LayoutElement>();
            toggleLe.flexibleWidth = 1f;
            toggleLe.preferredHeight = 36f;

            Button closeBtn = CreateButton(footerRow.transform, font, "Btn_CloseOnboarding", "GOT IT — CONTINUE",
                new Color(0.15f, 0.55f, 0.34f, 1f), 240f, 15, CloseModal);
            LayoutElement closeLe = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeLe.preferredWidth = 240f;
            closeLe.preferredHeight = 40f;
            closeLe.flexibleHeight = 0f;
        }

        private static Toggle CreateCheckbox(Transform parent, Font font, string name, string labelText)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);

            HorizontalLayoutGroup row = root.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            GameObject box = CreatePanel("Background", root.transform, new Color(0.12f, 0.18f, 0.28f, 1f));
            RectTransform boxRt = box.GetComponent<RectTransform>();
            boxRt.sizeDelta = new Vector2(28f, 28f);
            Outline boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = new Color(0.45f, 0.72f, 0.95f, 0.9f);
            boxOutline.effectDistance = new Vector2(1f, -1f);

            GameObject checkmark = CreatePanel("Checkmark", box.transform, new Color(0.28f, 0.85f, 0.55f, 1f));
            RectTransform checkRt = checkmark.GetComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(0.2f, 0.2f);
            checkRt.anchorMax = new Vector2(0.8f, 0.8f);
            checkRt.offsetMin = Vector2.zero;
            checkRt.offsetMax = Vector2.zero;

            Text label = CreateText("Label", root.transform, font, labelText, 20, FontStyle.Normal,
                new Color(0.84f, 0.90f, 0.96f, 1f), TextAnchor.MiddleLeft);
            label.GetComponent<RectTransform>().sizeDelta = new Vector2(540f, 36f);

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = IsAutoShowDisabled;
            toggle.onValueChanged.AddListener(value =>
            {
                PlayerPrefs.SetInt(HideOnboardingPrefsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            });

            return toggle;
        }

        private static Button CreateButton(
            Transform parent,
            Font font,
            string name,
            string labelText,
            Color color,
            float width,
            int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = CreatePanel(name, parent, color);
            btnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 40f);
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnObj.GetComponent<Image>();
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            Text txt = CreateText("Label", btnObj.transform, font, labelText, fontSize, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            RectTransform txtRt = txt.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            return btn;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text txt = obj.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            return txt;
        }
    }
}
