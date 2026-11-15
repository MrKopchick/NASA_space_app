using System.Collections;
using System.Collections.Generic;
using AstraLink.Core;
using AstraLink.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AstraLink.UI.Menu
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Scene Navigation")]
        [SerializeField] private string mainSceneName = "MainScene";

        [Header("Design Slots (Swap Sprites / Fonts in Inspector or Hierarchy)")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image logoImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text footerText;

        [Header("Menu Buttons (Continue, Settings, Saves, Exit)")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Text continueButtonLabel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Text settingsButtonLabel;
        [SerializeField] private Button savesButton;
        [SerializeField] private Text savesButtonLabel;
        [SerializeField] private Button exitButton;
        [SerializeField] private Text exitButtonLabel;

        [Header("Background Slideshow")]
        [SerializeField] private List<Sprite> backgroundSprites = new List<Sprite>();
        [SerializeField] private float backgroundHoldSeconds = 5.0f;
        [SerializeField] private float backgroundFadeSeconds = 1.8f;

        [Header("Glitch Animation")]
        [SerializeField] private bool enableGlitchEffects = true;
        [SerializeField] private float minGlitchIntervalSeconds = 1.2f;
        [SerializeField] private float maxGlitchIntervalSeconds = 3.2f;
        [SerializeField] private float glitchIntensityPx = 6.0f;

        [Header("Intro & Outro Transition")]
        [SerializeField] private AudioSource menuAudioSource;
        [SerializeField] private float introFadeSeconds = 1.6f;
        [SerializeField] private float outroFadeSeconds = 1.0f;
        [SerializeField, Range(0f, 1f)] private float targetMusicVolume = 1.0f;

        [Header("Ambient Space Decor")]
        [SerializeField] private bool enableAmbientDecor = true;

        private Image _crossfadeOverlay;
        private Image _screenFadeOverlay;
        private Coroutine _slideshowCoroutine;
        private Coroutine _glitchCoroutine;
        private Coroutine _transitionCoroutine;
        private bool _isTransitioning;
        private SaveManagerPanelUI _saveManagerPanel;

        private Vector2 _baseTitlePos;
        private Vector2 _baseSubtitlePos;
        private Vector2 _baseLogoPos;
        private Color _baseTitleColor;
        private Color _baseSubtitleColor;
        private Color _baseLogoColor;
        private string _baseSubtitleString;

        private Vector2 _baseContinueBtnPos;
        private Vector2 _baseSettingsBtnPos;
        private Vector2 _baseSavesBtnPos;
        private Vector2 _baseExitBtnPos;
        private Color _baseContinueBtnColor;
        private Color _baseSettingsBtnColor;
        private Color _baseSavesBtnColor;
        private Color _baseExitBtnColor;
        private Color _baseContinueLblColor;
        private Color _baseSettingsLblColor;
        private Color _baseSavesLblColor;
        private Color _baseExitLblColor;
        private string _baseContinueLblString;
        private string _baseSettingsLblString;
        private string _baseSavesLblString;
        private string _baseExitLblString;

        private void Awake()
        {
            SaveManager.MigrateLegacySaveIfNeeded();
            BindSceneReferencesIfMissing();
            EnsureFourButtonsExist();
            InitializeSaveManagerModal();
            if (enableAmbientDecor)
            {
                MenuAmbientDecor.AttachIfNeeded(this, backgroundImage);
            }
            BindButtonCallbacks();
            RefreshMenuLabels();
            CacheBaseVisualState();
            InitializeIntroState();
        }

        private void Start()
        {
            StartVisualEffects();
            StartIntroFadeIn();
        }

        private void OnDisable()
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            _isTransitioning = false;
            StopVisualEffects();
            RestoreBaseVisualState();
        }

        private void InitializeSaveManagerModal()
        {
            Transform canvasTransform = backgroundImage != null && backgroundImage.canvas != null
                ? backgroundImage.canvas.transform
                : GameObject.Find("MenuCanvas")?.transform;

            if (canvasTransform == null) return;

            _saveManagerPanel = GetComponent<SaveManagerPanelUI>();
            if (_saveManagerPanel == null)
            {
                _saveManagerPanel = gameObject.AddComponent<SaveManagerPanelUI>();
            }

            Font refFont = continueButtonLabel != null ? continueButtonLabel.font : (titleText?.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));
            _saveManagerPanel.Initialize(canvasTransform, refFont);
            _saveManagerPanel.OnLoadRequested += _ => StartOrContinueGame();
            _saveManagerPanel.OnSavesChanged += RefreshMenuLabels;
        }

        private void EnsureFourButtonsExist()
        {
            Transform panelTransform = GameObject.Find("MenuCanvas/CenterCard/ButtonsPanel")?.transform;
            if (panelTransform == null) return;

            // 1. Continue Button (шукаємо старий Btn_StartGame або схожий)
            if (continueButton == null)
            {
                Transform t = panelTransform.Find("Btn_Continue") ?? panelTransform.Find("Btn_StartGame");
                if (t != null)
                {
                    t.name = "Btn_Continue";
                    continueButton = t.GetComponent<Button>();
                }
                else
                {
                    continueButton = CreateButtonObject(panelTransform, "Btn_Continue", "CONTINUE");
                }
            }
            continueButtonLabel = continueButton.GetComponentInChildren<Text>();

            // 2. Settings Button
            if (settingsButton == null)
            {
                Transform t = panelTransform.Find("Btn_Settings") ?? panelTransform.Find("Btn_NewCampaign");
                if (t != null)
                {
                    t.name = "Btn_Settings";
                    settingsButton = t.GetComponent<Button>();
                }
                else
                {
                    settingsButton = CreateButtonObject(panelTransform, "Btn_Settings", "SETTINGS");
                }
            }
            settingsButtonLabel = settingsButton.GetComponentInChildren<Text>();

            // 3. Saves Button
            if (savesButton == null)
            {
                Transform t = panelTransform.Find("Btn_Saves");
                if (t != null)
                {
                    savesButton = t.GetComponent<Button>();
                }
                else
                {
                    savesButton = CreateButtonObject(panelTransform, "Btn_Saves", "SAVES");
                }
            }
            savesButtonLabel = savesButton.GetComponentInChildren<Text>();

            // 4. Exit Button
            if (exitButton == null)
            {
                Transform t = panelTransform.Find("Btn_Exit") ?? panelTransform.Find("Btn_Quit");
                if (t != null)
                {
                    t.name = "Btn_Exit";
                    exitButton = t.GetComponent<Button>();
                }
                else
                {
                    exitButton = CreateButtonObject(panelTransform, "Btn_Exit", "EXIT");
                }
            }
            exitButtonLabel = exitButton.GetComponentInChildren<Text>();

            LayoutFourMenuButtons(panelTransform as RectTransform);
        }

        private Button CreateButtonObject(Transform parent, string name, string labelText)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            var img = btnObj.GetComponent<Image>();
            img.color = new Color(0.16f, 0.27f, 0.44f, 1f);

            var txtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var txt = txtObj.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("TimesNewRoman.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = labelText;
            txt.fontSize = 20;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            return btnObj.GetComponent<Button>();
        }

        private void LayoutFourMenuButtons(RectTransform panelRt)
        {
            if (panelRt != null)
            {
                panelRt.anchoredPosition = new Vector2(panelRt.anchoredPosition.x, 195f);
                panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, 330f);
            }

            SetButtonVerticalRect(continueButton, 114f, 60f);
            SetButtonVerticalRect(settingsButton, 38f, 60f);
            SetButtonVerticalRect(savesButton, -38f, 60f);
            SetButtonVerticalRect(exitButton, -114f, 60f);
        }

        private static void SetButtonVerticalRect(Button btn, float yPos, float height)
        {
            if (btn == null) return;
            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, yPos);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        }

        private void BindButtonCallbacks()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(StartOrContinueGame);
                continueButton.onClick.AddListener(StartOrContinueGame);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OpenSettings);
                settingsButton.onClick.AddListener(OpenSettings);
            }

            if (savesButton != null)
            {
                savesButton.onClick.RemoveListener(OpenSaveManager);
                savesButton.onClick.AddListener(OpenSaveManager);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(QuitGame);
                exitButton.onClick.AddListener(QuitGame);
            }
        }

        public void OpenSaveManager()
        {
            if (_isTransitioning || _saveManagerPanel == null) return;
            _saveManagerPanel.Open();
        }

        public void OpenSettings()
        {
            if (_isTransitioning) return;
            Debug.Log("[MainMenuUI] Settings clicked (Placeholder for settings panel).");
            // Тут ти зможеш підключити своє вікно налаштувань, коли воно буде готове
        }

        public void RefreshMenuLabels()
        {
            bool hasSave = PlayerPrefs.HasKey(GameManager.AgencyStatePrefsKey) || SaveManager.GetAllSaves().Count > 0;

            if (continueButtonLabel != null)
            {
                continueButtonLabel.text = hasSave ? "CONTINUE" : "NEW GAME";
                _baseContinueLblString = continueButtonLabel.text;
            }

            if (settingsButtonLabel != null)
            {
                settingsButtonLabel.text = "SETTINGS";
                _baseSettingsLblString = settingsButtonLabel.text;
            }

            if (savesButtonLabel != null)
            {
                int count = SaveManager.GetAllSaves().Count;
                savesButtonLabel.text = count > 0 ? $"SAVES ({count})" : "SAVES";
                _baseSavesLblString = savesButtonLabel.text;
            }

            if (exitButtonLabel != null)
            {
                exitButtonLabel.text = "EXIT";
                _baseExitLblString = exitButtonLabel.text;
            }

            if (exitButton != null)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                exitButton.gameObject.SetActive(false);
#else
                exitButton.gameObject.SetActive(true);
#endif
            }
        }

        public void StartOrContinueGame()
        {
            if (_isTransitioning) return;

            if (string.IsNullOrEmpty(mainSceneName))
            {
                Debug.LogError("[MainMenuUI] Main scene name is not set.");
                return;
            }

            if (string.IsNullOrEmpty(SaveManager.ActiveSlotId))
            {
                SaveManager.CreateNewSave("CAMPAIGN 01");
            }

            StartOutroTransition(() => SceneManager.LoadScene(mainSceneName));
        }

        public void QuitGame()
        {
            if (_isTransitioning) return;

            StartOutroTransition(() =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private void InitializeIntroState()
        {
            EnsureScreenFadeOverlay();
            if (_screenFadeOverlay != null)
            {
                _screenFadeOverlay.color = new Color(0f, 0f, 0f, 1f);
                _screenFadeOverlay.raycastTarget = true;
                _screenFadeOverlay.transform.SetAsLastSibling();
            }

            if (menuAudioSource != null)
            {
                menuAudioSource.volume = 0f;
                if (!menuAudioSource.isPlaying && menuAudioSource.clip != null)
                {
                    menuAudioSource.Play();
                }
            }
        }

        private void StartIntroFadeIn()
        {
            if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = StartCoroutine(PlayIntroFadeInCoroutine());
        }

        private IEnumerator PlayIntroFadeInCoroutine()
        {
            float duration = Mathf.Max(0.05f, introFadeSeconds);
            float elapsed = 0f;
            float goalVolume = Mathf.Clamp01(targetMusicVolume);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                if (_screenFadeOverlay != null)
                    _screenFadeOverlay.color = new Color(0f, 0f, 0f, 1f - t);

                if (menuAudioSource != null)
                    menuAudioSource.volume = Mathf.Lerp(0f, goalVolume, t);

                yield return null;
            }

            if (_screenFadeOverlay != null)
            {
                _screenFadeOverlay.color = new Color(0f, 0f, 0f, 0f);
                _screenFadeOverlay.raycastTarget = false;
            }

            if (menuAudioSource != null)
                menuAudioSource.volume = goalVolume;

            _transitionCoroutine = null;
        }

        private void StartOutroTransition(System.Action onComplete)
        {
            _isTransitioning = true;
            if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = StartCoroutine(PlayOutroAndExecuteCoroutine(onComplete));
        }

        private IEnumerator PlayOutroAndExecuteCoroutine(System.Action onComplete)
        {
            EnsureScreenFadeOverlay();

            float startAlpha = 0f;
            if (_screenFadeOverlay != null)
            {
                _screenFadeOverlay.transform.SetAsLastSibling();
                _screenFadeOverlay.raycastTarget = true;
                startAlpha = _screenFadeOverlay.color.a;
            }

            float startVolume = menuAudioSource != null ? menuAudioSource.volume : 0f;
            float duration = Mathf.Max(0.05f, outroFadeSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                if (_screenFadeOverlay != null)
                    _screenFadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(startAlpha, 1f, t));

                if (menuAudioSource != null)
                    menuAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);

                yield return null;
            }

            if (_screenFadeOverlay != null)
                _screenFadeOverlay.color = new Color(0f, 0f, 0f, 1f);

            if (menuAudioSource != null)
            {
                menuAudioSource.volume = 0f;
                if (menuAudioSource.isPlaying) menuAudioSource.Stop();
            }

            _transitionCoroutine = null;
            onComplete?.Invoke();
        }

        private void EnsureScreenFadeOverlay()
        {
            if (_screenFadeOverlay != null) return;

            Transform canvasTransform = backgroundImage != null && backgroundImage.canvas != null
                ? backgroundImage.canvas.transform
                : GameObject.Find("MenuCanvas")?.transform;

            if (canvasTransform == null) return;

            GameObject overlayObj = new GameObject("ScreenFadeOverlay", typeof(RectTransform), typeof(Image));
            overlayObj.transform.SetParent(canvasTransform, false);
            overlayObj.transform.SetAsLastSibling();

            RectTransform rect = overlayObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _screenFadeOverlay = overlayObj.GetComponent<Image>();
            _screenFadeOverlay.color = new Color(0f, 0f, 0f, 1f);
            _screenFadeOverlay.raycastTarget = true;
        }

        private void CacheBaseVisualState()
        {
            if (titleText != null)
            {
                _baseTitlePos = titleText.rectTransform.anchoredPosition;
                _baseTitleColor = titleText.color;
            }

            if (subtitleText != null)
            {
                _baseSubtitlePos = subtitleText.rectTransform.anchoredPosition;
                _baseSubtitleColor = subtitleText.color;
                _baseSubtitleString = subtitleText.text;
            }

            if (logoImage != null)
            {
                _baseLogoPos = logoImage.rectTransform.anchoredPosition;
                _baseLogoColor = logoImage.color;
            }

            CacheButtonBaseState(continueButton, continueButtonLabel, ref _baseContinueBtnPos, ref _baseContinueBtnColor, ref _baseContinueLblColor, ref _baseContinueLblString);
            CacheButtonBaseState(settingsButton, settingsButtonLabel, ref _baseSettingsBtnPos, ref _baseSettingsBtnColor, ref _baseSettingsLblColor, ref _baseSettingsLblString);
            CacheButtonBaseState(savesButton, savesButtonLabel, ref _baseSavesBtnPos, ref _baseSavesBtnColor, ref _baseSavesLblColor, ref _baseSavesLblString);
            CacheButtonBaseState(exitButton, exitButtonLabel, ref _baseExitBtnPos, ref _baseExitBtnColor, ref _baseExitLblColor, ref _baseExitLblString);
        }

        private static void CacheButtonBaseState(Button btn, Text lbl, ref Vector2 basePos, ref Color baseBtnColor, ref Color baseLblColor, ref string baseLblText)
        {
            if (btn != null)
            {
                RectTransform rt = btn.GetComponent<RectTransform>();
                if (rt != null) basePos = rt.anchoredPosition;
                Image img = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
                if (img != null) baseBtnColor = img.color;
            }

            if (lbl != null)
            {
                baseLblColor = lbl.color;
                baseLblText = lbl.text;
            }
        }

        private void RestoreBaseVisualState()
        {
            if (titleText != null)
            {
                titleText.rectTransform.anchoredPosition = _baseTitlePos;
                titleText.color = _baseTitleColor;
            }

            if (subtitleText != null)
            {
                subtitleText.rectTransform.anchoredPosition = _baseSubtitlePos;
                subtitleText.color = _baseSubtitleColor;
                if (!string.IsNullOrEmpty(_baseSubtitleString)) subtitleText.text = _baseSubtitleString;
            }

            if (logoImage != null)
            {
                logoImage.rectTransform.anchoredPosition = _baseLogoPos;
                logoImage.color = _baseLogoColor;
            }

            RestoreButtonBaseState(continueButton, continueButtonLabel, _baseContinueBtnPos, _baseContinueBtnColor, _baseContinueLblColor, _baseContinueLblString);
            RestoreButtonBaseState(settingsButton, settingsButtonLabel, _baseSettingsBtnPos, _baseSettingsBtnColor, _baseSettingsLblColor, _baseSettingsLblString);
            RestoreButtonBaseState(savesButton, savesButtonLabel, _baseSavesBtnPos, _baseSavesBtnColor, _baseSavesLblColor, _baseSavesLblString);
            RestoreButtonBaseState(exitButton, exitButtonLabel, _baseExitBtnPos, _baseExitBtnColor, _baseExitLblColor, _baseExitLblString);
        }

        private static void RestoreButtonBaseState(Button btn, Text lbl, Vector2 basePos, Color baseBtnColor, Color baseLblColor, string baseLblText)
        {
            if (btn != null)
            {
                RectTransform rt = btn.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = basePos;
                Image img = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
                if (img != null) img.color = baseBtnColor;
            }

            if (lbl != null)
            {
                lbl.color = baseLblColor;
                if (!string.IsNullOrEmpty(baseLblText)) lbl.text = baseLblText;
            }
        }

        private void StartVisualEffects()
        {
            StopVisualEffects();

            if (backgroundImage != null && backgroundSprites != null && backgroundSprites.Count > 1)
            {
                EnsureCrossfadeOverlay();
                _slideshowCoroutine = StartCoroutine(BackgroundSlideshowLoop());
            }

            if (enableGlitchEffects && (titleText != null || subtitleText != null || logoImage != null))
            {
                _glitchCoroutine = StartCoroutine(GlitchAnimationLoop());
            }
        }

        private void StopVisualEffects()
        {
            if (_slideshowCoroutine != null)
            {
                StopCoroutine(_slideshowCoroutine);
                _slideshowCoroutine = null;
            }

            if (_glitchCoroutine != null)
            {
                StopCoroutine(_glitchCoroutine);
                _glitchCoroutine = null;
            }
        }

        private void EnsureCrossfadeOverlay()
        {
            if (_crossfadeOverlay != null || backgroundImage == null) return;

            GameObject overlayObj = new GameObject("BackgroundCrossfadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObj.transform.SetParent(backgroundImage.transform.parent, false);
            overlayObj.transform.SetSiblingIndex(backgroundImage.transform.GetSiblingIndex() + 1);

            RectTransform srcRt = backgroundImage.rectTransform;
            RectTransform dstRt = overlayObj.GetComponent<RectTransform>();
            dstRt.anchorMin = srcRt.anchorMin;
            dstRt.anchorMax = srcRt.anchorMax;
            dstRt.pivot = srcRt.pivot;
            dstRt.anchoredPosition = srcRt.anchoredPosition;
            dstRt.sizeDelta = srcRt.sizeDelta;
            dstRt.localScale = srcRt.localScale;

            _crossfadeOverlay = overlayObj.GetComponent<Image>();
            _crossfadeOverlay.type = backgroundImage.type;
            _crossfadeOverlay.preserveAspect = backgroundImage.preserveAspect;
            _crossfadeOverlay.raycastTarget = false;

            Color c = backgroundImage.color;
            c.a = 0f;
            _crossfadeOverlay.color = c;
        }

        private IEnumerator BackgroundSlideshowLoop()
        {
            List<Sprite> validSprites = new List<Sprite>();
            for (int i = 0; i < backgroundSprites.Count; i++)
            {
                if (backgroundSprites[i] != null) validSprites.Add(backgroundSprites[i]);
            }

            if (validSprites.Count < 2) yield break;

            int currentIndex = 0;
            if (backgroundImage.sprite != null)
            {
                int found = validSprites.IndexOf(backgroundImage.sprite);
                if (found >= 0) currentIndex = found;
            }

            backgroundImage.sprite = validSprites[currentIndex];
            Color baseColor = backgroundImage.color;

            while (true)
            {
                yield return new WaitForSeconds(Mathf.Max(0.5f, backgroundHoldSeconds));

                int nextIndex = (currentIndex + 1) % validSprites.Count;
                Sprite nextSprite = validSprites[nextIndex];

                if (_crossfadeOverlay != null)
                {
                    _crossfadeOverlay.sprite = nextSprite;
                    float duration = Mathf.Max(0.2f, backgroundFadeSeconds);
                    float elapsed = 0f;

                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        float smoothAlpha = Mathf.SmoothStep(0f, 1f, t);

                        Color overlayColor = baseColor;
                        overlayColor.a = baseColor.a * smoothAlpha;
                        _crossfadeOverlay.color = overlayColor;
                        yield return null;
                    }

                    backgroundImage.sprite = nextSprite;
                    Color hiddenColor = baseColor;
                    hiddenColor.a = 0f;
                    _crossfadeOverlay.color = hiddenColor;
                }
                else
                {
                    backgroundImage.sprite = nextSprite;
                }

                currentIndex = nextIndex;
            }
        }

        private IEnumerator GlitchAnimationLoop()
        {
            while (true)
            {
                float minWait = Mathf.Max(0.8f, minGlitchIntervalSeconds);
                float maxWait = Mathf.Max(minWait, maxGlitchIntervalSeconds);
                yield return new WaitForSeconds(Random.Range(minWait, maxWait));

                int burstSteps = Random.Range(3, 6);
                for (int step = 0; step < burstSteps; step++)
                {
                    ApplyGlitchFrame(step);
                    yield return new WaitForSeconds(Random.Range(0.035f, 0.065f));
                }

                RestoreBaseVisualState();
            }
        }

        private void ApplyGlitchFrame(int step)
        {
            float jitter = Mathf.Max(1f, glitchIntensityPx);

            if (titleText != null)
            {
                float dx = Random.Range(-jitter, jitter);
                float dy = Random.Range(-jitter * 0.25f, jitter * 0.25f);
                titleText.rectTransform.anchoredPosition = _baseTitlePos + new Vector2(dx, dy);
                titleText.color = step % 2 == 0
                    ? new Color(0.45f, 0.92f, 1.0f, _baseTitleColor.a)
                    : new Color(1.0f, 0.42f, 0.78f, _baseTitleColor.a * 0.92f);
            }

            if (subtitleText != null)
            {
                float subDx = Random.Range(-jitter * 0.8f, jitter * 0.8f);
                subtitleText.rectTransform.anchoredPosition = _baseSubtitlePos + new Vector2(subDx, 0f);
                subtitleText.color = step % 2 == 0
                    ? new Color(0.85f, 1.0f, 0.95f, _baseSubtitleColor.a)
                    : _baseSubtitleColor;

                if (!string.IsNullOrEmpty(_baseSubtitleString))
                {
                    subtitleText.text = step % 2 == 0 ? ScrambleOneOrTwoChars(_baseSubtitleString) : _baseSubtitleString;
                }
            }

            if (logoImage != null)
            {
                float logoDx = Random.Range(-jitter * 0.6f, jitter * 0.6f);
                logoImage.rectTransform.anchoredPosition = _baseLogoPos + new Vector2(logoDx, 0f);
                Color logoC = _baseLogoColor;
                logoC.a = _baseLogoColor.a * Random.Range(0.75f, 1.0f);
                logoImage.color = logoC;
            }

            ApplyButtonGlitch(continueButton, continueButtonLabel, _baseContinueBtnPos, _baseContinueBtnColor, _baseContinueLblColor, _baseContinueLblString, jitter, step);
            ApplyButtonGlitch(settingsButton, settingsButtonLabel, _baseSettingsBtnPos, _baseSettingsBtnColor, _baseSettingsLblColor, _baseSettingsLblString, jitter, step + 1);
            ApplyButtonGlitch(savesButton, savesButtonLabel, _baseSavesBtnPos, _baseSavesBtnColor, _baseSavesLblColor, _baseSavesLblString, jitter, step);
            ApplyButtonGlitch(exitButton, exitButtonLabel, _baseExitBtnPos, _baseExitBtnColor, _baseExitLblColor, _baseExitLblString, jitter, step + 1);
        }

        private static void ApplyButtonGlitch(Button btn, Text lbl, Vector2 basePos, Color baseBtnColor, Color baseLblColor, string baseLblText, float jitter, int step)
        {
            if (btn == null || !btn.gameObject.activeInHierarchy) return;

            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                float dx = Random.Range(-jitter * 0.7f, jitter * 0.7f);
                float dy = Random.Range(-jitter * 0.2f, jitter * 0.2f);
                rt.anchoredPosition = basePos + new Vector2(dx, dy);
            }

            Image img = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
            if (img != null)
            {
                Color tint = step % 2 == 0
                    ? new Color(0.50f, 0.95f, 1.0f, baseBtnColor.a)
                    : new Color(1.0f, 0.55f, 0.85f, baseBtnColor.a);
                img.color = Color.Lerp(baseBtnColor, tint, 0.35f);
            }

            if (lbl != null)
            {
                lbl.color = step % 2 == 0 ? new Color(0.60f, 0.98f, 1.0f, baseLblColor.a) : baseLblColor;
                if (!string.IsNullOrEmpty(baseLblText))
                {
                    lbl.text = step % 2 == 0 ? ScrambleOneOrTwoChars(baseLblText) : baseLblText;
                }
            }
        }

        private static string ScrambleOneOrTwoChars(string source)
        {
            if (string.IsNullOrEmpty(source) || source.Length < 4) return source;

            const string glitchChars = "01#_X%";
            char[] chars = source.ToCharArray();
            int count = Random.Range(1, 3);

            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, chars.Length);
                if (chars[idx] != ' ')
                {
                    chars[idx] = glitchChars[Random.Range(0, glitchChars.Length)];
                }
            }

            return new string(chars);
        }

        private void BindSceneReferencesIfMissing()
        {
            if (backgroundImage == null) backgroundImage = FindChildComponent<Image>("MenuCanvas/BackgroundImage");
            if (logoImage == null) logoImage = FindChildComponent<Image>("MenuCanvas/CenterCard/LogoSlot");
            if (titleText == null) titleText = FindChildComponent<Text>("MenuCanvas/CenterCard/TitleText");
            if (subtitleText == null) subtitleText = FindChildComponent<Text>("MenuCanvas/CenterCard/SubtitleText");
            if (footerText == null) footerText = FindChildComponent<Text>("MenuCanvas/CenterCard/FooterText");

            if (continueButton == null) continueButton = FindChildComponent<Button>("MenuCanvas/CenterCard/ButtonsPanel/Btn_Continue");
            if (continueButtonLabel == null && continueButton != null) continueButtonLabel = continueButton.GetComponentInChildren<Text>();

            if (settingsButton == null) settingsButton = FindChildComponent<Button>("MenuCanvas/CenterCard/ButtonsPanel/Btn_Settings");
            if (settingsButtonLabel == null && settingsButton != null) settingsButtonLabel = settingsButton.GetComponentInChildren<Text>();

            if (savesButton == null) savesButton = FindChildComponent<Button>("MenuCanvas/CenterCard/ButtonsPanel/Btn_Saves");
            if (savesButtonLabel == null && savesButton != null) savesButtonLabel = savesButton.GetComponentInChildren<Text>();

            if (exitButton == null) exitButton = FindChildComponent<Button>("MenuCanvas/CenterCard/ButtonsPanel/Btn_Exit");
            if (exitButtonLabel == null && exitButton != null) exitButtonLabel = exitButton.GetComponentInChildren<Text>();

            if (menuAudioSource == null) menuAudioSource = FindObjectOfType<AudioSource>();
        }

        private static T FindChildComponent<T>(string hierarchyPath) where T : Component
        {
            GameObject obj = GameObject.Find(hierarchyPath);
            return obj != null ? obj.GetComponent<T>() : null;
        }
    }
}