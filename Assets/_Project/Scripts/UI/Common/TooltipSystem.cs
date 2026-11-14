using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AstraLink.UI.Common
{
    /// <summary>
    /// Namespace marker allowing UI scripts to import AstraLink.UI.Common.
    /// </summary>
    public static class CommonTooltipMarker
    {
    }
}

namespace AstraLink.Core
{
    /// <summary>
    /// Singleton overlay tooltip renderer that creates a top-order Canvas (sortingOrder = 999) and follows the cursor within screen bounds (Task S3-08).
    /// </summary>
    public class TooltipSystem : MonoBehaviour
    {
        public const int OverlaySortingOrder = 999;
        public const string TipsEnabledPrefsKey = "AstraLink_TipsEnabled";
        private const string BuiltinFontResourceName = "LegacyRuntime.ttf";

        private static TooltipSystem _instance;

        [Header("Tooltip Layout & Styling")]
        [SerializeField] private float maxTooltipWidth = 420f;
        [SerializeField] private Vector2 cursorOffset = new Vector2(16f, -16f);
        [SerializeField] private float screenEdgePadding = 12f;
        [SerializeField] private int fontSize = 14;
        [SerializeField] private bool tipsEnabled = true;

        private Canvas _canvas;
        private RectTransform _panelRect;
        private Text _tipText;
        private LayoutElement _textLayoutElement;
        private readonly HashSet<int> _autoBoundButtons = new HashSet<int>();
        private float _nextAutoBindTime;

        /// <summary>
        /// Gets a value indicating whether an active <see cref="TooltipSystem"/> instance currently exists.
        /// </summary>
        public static bool HasInstance
        {
            get { return _instance != null; }
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="TooltipSystem"/>, creating the overlay Canvas host automatically if needed.
        /// </summary>
        public static TooltipSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<TooltipSystem>(true);
                    if (_instance == null)
                    {
                        GameObject host = new GameObject("TooltipSystem");
                        DontDestroyOnLoad(host);
                        _instance = host.AddComponent<TooltipSystem>();
                    }
                }

                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeOnLoad()
        {
            TooltipSystem system = Instance;
            if (system != null)
            {
                system.EnsureOverlayUI();
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadTipsSetting();
            EnsureOverlayUI();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// Gets or sets whether UI tooltips are enabled in user settings.
        /// </summary>
        public bool IsTipsEnabled
        {
            get { return tipsEnabled && DescriptionTips.IsEnabled; }
            set { SetTipsEnabled(value); }
        }

        /// <summary>
        /// Enables or disables UI tooltips from the settings menu and persists the choice.
        /// </summary>
        public void SetTipsEnabled(bool enabled)
        {
            if (enabled)
            {
                tipsEnabled = true;
                DescriptionTips.SetEnabled(true);
                PlayerPrefs.SetInt(TipsEnabledPrefsKey, 1);
            }
            else
            {
                tipsEnabled = false;
                DescriptionTips.SetEnabled(false);
                PlayerPrefs.SetInt(TipsEnabledPrefsKey, 0);
                Hide();
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Toggles UI tooltips on or off and returns the updated enabled state.
        /// </summary>
        public bool ToggleTipsEnabled()
        {
            if (IsTipsEnabled)
            {
                SetTipsEnabled(false);
                return false;
            }
            else
            {
                SetTipsEnabled(true);
                return true;
            }
        }

        private void LoadTipsSetting()
        {
            if (PlayerPrefs.HasKey(TipsEnabledPrefsKey))
            {
                int saved = PlayerPrefs.GetInt(TipsEnabledPrefsKey, 1);
                if (saved == 1)
                {
                    tipsEnabled = true;
                    DescriptionTips.SetEnabled(true);
                }
                else
                {
                    tipsEnabled = false;
                    DescriptionTips.SetEnabled(false);
                }
            }
            else
            {
                tipsEnabled = true;
                DescriptionTips.SetEnabled(true);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _autoBoundButtons.Clear();
            Hide();
            AutoBindKnownSceneButtons();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextAutoBindTime)
            {
                _nextAutoBindTime = Time.unscaledTime + 0.75f;
                AutoBindKnownSceneButtons();
            }

            if (_panelRect != null && _panelRect.gameObject.activeSelf)
            {
                UpdatePositionToCursor();
            }
        }

        /// <summary>
        /// Updates the tooltip text, rebuilds layout immediately, and displays the tooltip panel clamped to screen bounds.
        /// </summary>
        public void Show(string text)
        {
            if (!IsTipsEnabled)
            {
                Hide();
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Hide();
                return;
            }

            EnsureOverlayUI();
            if (_panelRect == null || _tipText == null)
            {
                return;
            }

            _tipText.text = text.Trim();

            if (_textLayoutElement != null)
            {
                _textLayoutElement.preferredWidth = -1f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

                if (_tipText.preferredWidth > maxTooltipWidth)
                {
                    _textLayoutElement.preferredWidth = maxTooltipWidth;
                }
            }

            if (!_panelRect.gameObject.activeSelf)
            {
                _panelRect.gameObject.SetActive(true);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            UpdatePositionToCursor();
        }

        /// <summary>
        /// Immediately hides the floating tooltip panel.
        /// </summary>
        public void Hide()
        {
            if (_panelRect != null && _panelRect.gameObject.activeSelf)
            {
                _panelRect.gameObject.SetActive(false);
            }
        }

        private void EnsureOverlayUI()
        {
            if (_canvas != null && _panelRect != null && _tipText != null)
            {
                return;
            }

            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = OverlaySortingOrder;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            group.blocksRaycasts = false;
            group.interactable = false;

            Transform existingBox = transform.Find("TooltipPanel");
            if (existingBox != null)
            {
                Destroy(existingBox.gameObject);
            }

            GameObject panelObj = new GameObject(
                "TooltipPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));
            panelObj.transform.SetParent(transform, false);

            _panelRect = panelObj.GetComponent<RectTransform>();
            _panelRect.anchorMin = Vector2.zero;
            _panelRect.anchorMax = Vector2.zero;
            _panelRect.pivot = new Vector2(0f, 1f);

            Image bg = panelObj.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.09f, 0.16f, 0.96f);
            bg.raycastTarget = false;

            Outline border = panelObj.AddComponent<Outline>();
            border.effectColor = new Color(0.24f, 0.74f, 0.98f, 0.92f);
            border.effectDistance = new Vector2(1.2f, -1.2f);

            HorizontalLayoutGroup layout = panelObj.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panelObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject textObj = new GameObject(
                "TooltipText",
                typeof(RectTransform),
                typeof(Text),
                typeof(LayoutElement));
            textObj.transform.SetParent(panelObj.transform, false);

            _tipText = textObj.GetComponent<Text>();
            _tipText.font = Resources.GetBuiltinResource<Font>(BuiltinFontResourceName);
            _tipText.fontSize = fontSize;
            _tipText.color = new Color(0.95f, 0.98f, 1.0f, 1.0f);
            _tipText.alignment = TextAnchor.MiddleLeft;
            _tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tipText.verticalOverflow = VerticalWrapMode.Overflow;
            _tipText.raycastTarget = false;

            _textLayoutElement = textObj.GetComponent<LayoutElement>();
            _textLayoutElement.preferredWidth = -1f;

            _panelRect.gameObject.SetActive(false);
        }

        private void UpdatePositionToCursor()
        {
            if (_panelRect == null)
            {
                return;
            }

            float scale = _canvas != null && _canvas.scaleFactor > 0.001f ? _canvas.scaleFactor : 1f;
            float width = _panelRect.rect.width * scale;
            float height = _panelRect.rect.height * scale;

            Vector3 mousePos = Input.mousePosition;
            float x = mousePos.x + cursorOffset.x;
            float y = mousePos.y + cursorOffset.y;

            float maxX = Screen.width - width - screenEdgePadding;
            if (x > maxX)
            {
                x = maxX;
            }

            if (x < screenEdgePadding)
            {
                x = screenEdgePadding;
            }

            if (y - height < screenEdgePadding)
            {
                y = mousePos.y + Mathf.Abs(cursorOffset.y) + height;
            }

            float maxY = Screen.height - screenEdgePadding;
            if (y > maxY)
            {
                y = maxY;
            }

            if (y - height < screenEdgePadding)
            {
                y = height + screenEdgePadding;
            }

            _panelRect.position = new Vector3(x, y, 0f);
        }

        private void AutoBindKnownSceneButtons()
        {
            Button[] buttons = FindObjectsOfType<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn == null)
                {
                    continue;
                }

                int id = btn.GetInstanceID();
                if (_autoBoundButtons.Contains(id))
                {
                    continue;
                }

                _autoBoundButtons.Add(id);

                if (btn.GetComponent<TooltipTrigger>() != null)
                {
                    continue;
                }

                string tip = DescriptionTips.Get(btn.gameObject.name);
                if (!string.IsNullOrEmpty(tip))
                {
                    TooltipTrigger.Attach(btn.gameObject, btn.gameObject.name);
                }
            }
        }
    }
}
