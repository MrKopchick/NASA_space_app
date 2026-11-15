using AstraLink.Core;
using AstraLink.UI.Hub;
using AstraLink.UI.Workshop;
using AstraLink.UnityBridge;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AstraLink.UI.Common
{
    /// <summary>
    /// Progressively unlocks interactive buttons and displays an animated hand pointer during the first mission (Task S4-01).
    /// </summary>
    public class FirstMissionGuide : MonoBehaviour
    {
        private enum GuideStep
        {
            SelectMission = 0,
            GoToWorkshop = 1,
            OpenAutopilot = 2,
            AddEclipseRule = 3,
            OpenDryRun = 4,
            CloseDryRun = 5,
            LaunchMission = 6,
            Completed = 7
        }

        private GuideStep _currentStep = GuideStep.SelectMission;
        private bool _missionAcceptedClicked;
        private bool _hubSubscribed;
        private bool _dryRunSubscribed;
        private bool _presetBound;
        private ContractHubUI _boundHubUi;
        private DryRunUI _boundDryRunUi;

        private Button _navWorkshopBtn;
        private Button _navFlightBtn;
        private Button _acceptContract0Btn;
        private Button _openAutopilotBtn;
        private Button _openDryRunBtn;
        private Button _openRnDBtn;
        private Button _launchMissionBtn;
        private Button _autopilotPresetEclipseBtn;
        private Button _autopilotOpenDryRunBtn;
        private Button _runDryRunBtn;
        private Button _dryRunCloseBtn;
        private GameObject _autopilotModal;
        private GameObject _dryRunModal;

        private Button _highlightedButton;
        private Outline _highlightOutline;
        private bool _addedOutline;
        private RectTransform _arrowRoot;
        private float _nextScanTime;
        private bool _loggedState;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeEvents();
            ClearHighlight();
            DestroyArrowPointer();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetCachedReferences();
        }

        private void Update()
        {
            if (!IsFirstMissionActive())
            {
                if (_currentStep != GuideStep.Completed)
                {
                    _currentStep = GuideStep.Completed;
                    RestoreAllManagedButtons();
                    ClearHighlight();
                    DestroyArrowPointer();
                }

                return;
            }

            if (Time.unscaledTime >= _nextScanTime)
            {
                _nextScanTime = Time.unscaledTime + 0.15f;
                CacheSceneControls();
            }

            AdvanceStepIfReady();
            ApplyStepInteractivity();
            UpdateHighlightAndArrow();
        }

        private bool IsFirstMissionActive()
        {
            if (GameManager.Instance == null)
            {
                return false;
            }

            AgencyState state = GameManager.Instance.CurrentAgencyState;
            if (state != null && state.CompletedMissionIds != null && state.CompletedMissionIds.Count > 0)
            {
                if (!_loggedState)
                {
                    _loggedState = true;
                    Debug.Log("[FirstMissionGuide] Inactive: Mission 1 already completed in PlayerPrefs.");
                }

                return false;
            }

            if (!_loggedState)
            {
                _loggedState = true;
                Debug.Log("[FirstMissionGuide] Active for first mission.");
            }

            return true;
        }

        private void AdvanceStepIfReady()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            if (gm.CurrentScreen == GameScreen.Flight || gm.CurrentScreen == GameScreen.Debrief)
            {
                _currentStep = GuideStep.Completed;
                return;
            }

            switch (_currentStep)
            {
                case GuideStep.SelectMission:
                    if (gm.CurrentScreen == GameScreen.Workshop)
                    {
                        _currentStep = GuideStep.OpenAutopilot;
                    }
                    else if (_missionAcceptedClicked)
                    {
                        _currentStep = GuideStep.GoToWorkshop;
                    }
                    break;

                case GuideStep.GoToWorkshop:
                    if (gm.CurrentScreen == GameScreen.Workshop)
                    {
                        _currentStep = GuideStep.OpenAutopilot;
                    }
                    break;

                case GuideStep.OpenAutopilot:
                    if (_autopilotModal != null && _autopilotModal.activeInHierarchy)
                    {
                        _currentStep = GuideStep.AddEclipseRule;
                    }
                    break;

                case GuideStep.AddEclipseRule:
                    if (_dryRunModal != null && _dryRunModal.activeInHierarchy)
                    {
                        _currentStep = GuideStep.OpenDryRun;
                    }
                    else if (_autopilotModal != null && !_autopilotModal.activeInHierarchy)
                    {
                        _currentStep = GuideStep.OpenDryRun;
                    }
                    break;

                case GuideStep.OpenDryRun:
                    break;

                case GuideStep.CloseDryRun:
                    if (_dryRunModal == null || !_dryRunModal.activeInHierarchy)
                    {
                        _currentStep = GuideStep.LaunchMission;
                    }
                    break;

                case GuideStep.LaunchMission:
                    break;
            }
        }

        private void ApplyStepInteractivity()
        {
            bool allowWorkshopNav = _currentStep >= GuideStep.GoToWorkshop;
            bool allowDryRun = _currentStep >= GuideStep.OpenDryRun;
            bool allowLaunch = _currentStep >= GuideStep.LaunchMission;

            SetButtonInteractable(_navWorkshopBtn, allowWorkshopNav);
            SetButtonInteractable(_navFlightBtn, allowLaunch);
            SetButtonInteractable(_openAutopilotBtn, _currentStep >= GuideStep.OpenAutopilot);
            SetButtonInteractable(_openDryRunBtn, allowDryRun);
            SetButtonInteractable(_openRnDBtn, allowLaunch);
            SetButtonInteractable(_launchMissionBtn, allowLaunch);

            Button target = ResolveTargetHighlightButton();
            if (target != _highlightedButton)
            {
                SetHighlightTarget(target);
            }
        }

        private Button ResolveTargetHighlightButton()
        {
            switch (_currentStep)
            {
                case GuideStep.SelectMission:
                    return _acceptContract0Btn;
                case GuideStep.GoToWorkshop:
                    return _navWorkshopBtn;
                case GuideStep.OpenAutopilot:
                    return _openAutopilotBtn;
                case GuideStep.AddEclipseRule:
                    if (_autopilotModal != null && _autopilotModal.activeInHierarchy)
                    {
                        return _autopilotPresetEclipseBtn;
                    }
                    return _openAutopilotBtn;
                case GuideStep.OpenDryRun:
                    if (_dryRunModal != null && _dryRunModal.activeInHierarchy)
                    {
                        return _runDryRunBtn;
                    }
                    if (_autopilotModal != null && _autopilotModal.activeInHierarchy)
                    {
                        return _autopilotOpenDryRunBtn;
                    }
                    return _openDryRunBtn;
                case GuideStep.CloseDryRun:
                    if (_dryRunModal != null && _dryRunModal.activeInHierarchy)
                    {
                        return _dryRunCloseBtn;
                    }
                    return _launchMissionBtn;
                case GuideStep.LaunchMission:
                    return _launchMissionBtn;
                default:
                    return null;
            }
        }

        private void RestoreAllManagedButtons()
        {
            SetButtonInteractable(_navWorkshopBtn, true);
            SetButtonInteractable(_navFlightBtn, true);
            SetButtonInteractable(_openAutopilotBtn, true);
            SetButtonInteractable(_openDryRunBtn, true);
            SetButtonInteractable(_openRnDBtn, true);
            SetButtonInteractable(_launchMissionBtn, true);
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null && button.interactable != interactable)
            {
                button.interactable = interactable;
            }
        }

        private void CacheSceneControls()
        {
            if (_navWorkshopBtn == null) _navWorkshopBtn = FindButtonByName("Btn_Workshop", null);
            if (_navFlightBtn == null) _navFlightBtn = FindButtonByName("Btn_Flight", null);
            if (_acceptContract0Btn == null) _acceptContract0Btn = FindButtonByName("Btn_AcceptContract_0", null);
            if (_openRnDBtn == null) _openRnDBtn = FindButtonByName("Btn_OpenRnD", null);
            if (_launchMissionBtn == null) _launchMissionBtn = FindButtonByName("Btn_LaunchMission", null);
            if (_runDryRunBtn == null) _runDryRunBtn = FindButtonByName("Btn_RunDryRun", null);

            if (_autopilotModal == null)
            {
                _autopilotModal = GameObject.Find("AutopilotModalOverlay");
            }

            if (_dryRunModal == null)
            {
                _dryRunModal = GameObject.Find("DryRunModalOverlay");
            }

            GameObject activeBar = GameObject.Find("ActiveContractBar");
            if (activeBar != null)
            {
                if (_openAutopilotBtn == null || !_openAutopilotBtn.gameObject.activeInHierarchy)
                {
                    _openAutopilotBtn = FindButtonByName("Btn_OpenAutopilot", activeBar.transform);
                }

                if (_openDryRunBtn == null || !_openDryRunBtn.gameObject.activeInHierarchy)
                {
                    _openDryRunBtn = FindButtonByName("Btn_OpenDryRun", activeBar.transform);
                }
            }

            if (_autopilotModal != null)
            {
                if (_autopilotPresetEclipseBtn == null)
                {
                    _autopilotPresetEclipseBtn = FindButtonByName("Btn_PresetEclipse", _autopilotModal.transform);
                }

                if (_autopilotOpenDryRunBtn == null)
                {
                    _autopilotOpenDryRunBtn = FindButtonByName("Btn_OpenDryRun", _autopilotModal.transform);
                }

                if (!_presetBound && _autopilotPresetEclipseBtn != null)
                {
                    _autopilotPresetEclipseBtn.onClick.AddListener(HandleEclipsePresetClicked);
                    _presetBound = true;
                }
            }

            if (_dryRunModal != null && _dryRunCloseBtn == null)
            {
                _dryRunCloseBtn = FindButtonByName("Btn_CloseModal", _dryRunModal.transform);
            }

            if (!_hubSubscribed)
            {
                ContractHubUI hubUi = FindObjectOfType<ContractHubUI>(true);
                if (hubUi != null)
                {
                    _boundHubUi = hubUi;
                    _boundHubUi.OnMissionSelected += HandleHubMissionSelected;
                    _hubSubscribed = true;
                }
            }

            if (!_dryRunSubscribed)
            {
                DryRunUI dryRunUi = FindObjectOfType<DryRunUI>(true);
                if (dryRunUi != null)
                {
                    _boundDryRunUi = dryRunUi;
                    _boundDryRunUi.OnDryRunCompleted += HandleDryRunCompleted;
                    _dryRunSubscribed = true;
                }
            }
        }

        private void HandleHubMissionSelected(MissionGoal mission)
        {
            _missionAcceptedClicked = true;
            if (_currentStep == GuideStep.SelectMission)
            {
                _currentStep = GuideStep.GoToWorkshop;
            }
        }

        private void HandleEclipsePresetClicked()
        {
            if (_currentStep == GuideStep.AddEclipseRule)
            {
                _currentStep = GuideStep.OpenDryRun;
            }
        }

        private void HandleDryRunCompleted(MissionReport report)
        {
            if (_currentStep == GuideStep.OpenDryRun)
            {
                _currentStep = _dryRunModal != null && _dryRunModal.activeInHierarchy
                    ? GuideStep.CloseDryRun
                    : GuideStep.LaunchMission;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_hubSubscribed && _boundHubUi != null)
            {
                _boundHubUi.OnMissionSelected -= HandleHubMissionSelected;
            }

            if (_dryRunSubscribed && _boundDryRunUi != null)
            {
                _boundDryRunUi.OnDryRunCompleted -= HandleDryRunCompleted;
            }

            if (_presetBound && _autopilotPresetEclipseBtn != null)
            {
                _autopilotPresetEclipseBtn.onClick.RemoveListener(HandleEclipsePresetClicked);
            }

            _hubSubscribed = false;
            _dryRunSubscribed = false;
            _presetBound = false;
            _boundHubUi = null;
            _boundDryRunUi = null;
        }

        private void ResetCachedReferences()
        {
            UnsubscribeEvents();
            ClearHighlight();
            DestroyArrowPointer();
            _navWorkshopBtn = null;
            _navFlightBtn = null;
            _acceptContract0Btn = null;
            _openAutopilotBtn = null;
            _openDryRunBtn = null;
            _openRnDBtn = null;
            _launchMissionBtn = null;
            _autopilotPresetEclipseBtn = null;
            _autopilotOpenDryRunBtn = null;
            _runDryRunBtn = null;
            _dryRunCloseBtn = null;
            _autopilotModal = null;
            _dryRunModal = null;
        }

        private static Button FindButtonByName(string objectName, Transform scope)
        {
            Button[] buttons = scope != null
                ? scope.GetComponentsInChildren<Button>(true)
                : FindObjectsOfType<Button>(true);

            Button fallback = null;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == objectName)
                {
                    if (buttons[i].gameObject.activeInHierarchy)
                    {
                        return buttons[i];
                    }

                    if (fallback == null)
                    {
                        fallback = buttons[i];
                    }
                }
            }

            return fallback;
        }

        private void SetHighlightTarget(Button target)
        {
            ClearHighlight();
            _highlightedButton = target;
            if (_highlightedButton == null)
            {
                return;
            }

            Outline existing = _highlightedButton.GetComponent<Outline>();
            if (existing != null)
            {
                _highlightOutline = existing;
                _addedOutline = false;
            }
            else
            {
                _highlightOutline = _highlightedButton.gameObject.AddComponent<Outline>();
                _highlightOutline.effectDistance = new Vector2(3f, -3f);
                _addedOutline = true;
            }
        }

        private void UpdateHighlightAndArrow()
        {
            if (_highlightOutline != null)
            {
                float alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
                _highlightOutline.effectColor = new Color(1f, 0.24f, 0.24f, alpha);
            }

            if (_highlightedButton == null || !_highlightedButton.gameObject.activeInHierarchy)
            {
                if (_arrowRoot != null)
                {
                    _arrowRoot.gameObject.SetActive(false);
                }

                return;
            }

            EnsureArrowPointer(_highlightedButton.transform);
            if (_arrowRoot == null)
            {
                return;
            }

            _arrowRoot.gameObject.SetActive(true);
            _arrowRoot.SetAsLastSibling();
            _arrowRoot.sizeDelta = new Vector2(56f, 56f);

            RectTransform targetRt = _highlightedButton.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            targetRt.GetWorldCorners(corners);
            float screenY = (corners[0].y + corners[1].y) * 0.5f;

            bool placeAbove = screenY < 180f;
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 12f;

            if (placeAbove)
            {
                _arrowRoot.anchorMin = new Vector2(0.5f, 1f);
                _arrowRoot.anchorMax = new Vector2(0.5f, 1f);
                _arrowRoot.pivot = new Vector2(0.5f, 0.5f);
                _arrowRoot.anchoredPosition = new Vector2(0f, 34f + bounce);
                _arrowRoot.localScale = new Vector3(1f, -1f, 1f);
            }
            else
            {
                _arrowRoot.anchorMin = new Vector2(0.5f, 0f);
                _arrowRoot.anchorMax = new Vector2(0.5f, 0f);
                _arrowRoot.pivot = new Vector2(0.5f, 0.5f);
                _arrowRoot.anchoredPosition = new Vector2(0f, -34f - bounce);
                _arrowRoot.localScale = Vector3.one;
            }
        }

        private void EnsureArrowPointer(Transform buttonTransform)
        {
            if (_arrowRoot != null)
            {
                if (_arrowRoot.parent != buttonTransform)
                {
                    _arrowRoot.SetParent(buttonTransform, false);
                }

                return;
            }

            Sprite handSprite = LoadHandPointerSprite();
            GameObject handGo = new GameObject("FirstMissionHandPointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas), typeof(LayoutElement));
            handGo.transform.SetParent(buttonTransform, false);
            handGo.GetComponent<LayoutElement>().ignoreLayout = true;

            Canvas canvas = handGo.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 3000;

            _arrowRoot = handGo.GetComponent<RectTransform>();
            _arrowRoot.sizeDelta = new Vector2(56f, 56f);

            Image handImg = handGo.GetComponent<Image>();
            handImg.preserveAspect = true;
            handImg.raycastTarget = false;

            if (handSprite != null)
            {
                handImg.sprite = handSprite;
                handImg.color = Color.white;
            }
            else
            {
                handImg.enabled = false;
                Color red = new Color(1f, 0.20f, 0.20f, 1f);
                Color border = new Color(0.05f, 0.02f, 0.02f, 0.95f);

                CreateArrowBar(handGo.transform, "Shaft", new Vector2(0f, -12f), new Vector2(12f, 22f), red, border);
                CreateArrowBar(handGo.transform, "HeadTop", new Vector2(0f, 1f), new Vector2(34f, 7f), red, border);
                CreateArrowBar(handGo.transform, "HeadMid", new Vector2(0f, 8f), new Vector2(22f, 7f), red, border);
                CreateArrowBar(handGo.transform, "HeadTip", new Vector2(0f, 15f), new Vector2(10f, 7f), red, border);
            }
        }

        private static Sprite LoadHandPointerSprite()
        {
            Sprite resourceSprite = Resources.Load<Sprite>("UI/hand_pointer");
            if (resourceSprite != null)
            {
                return resourceSprite;
            }

#if UNITY_EDITOR
            Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/hand_pointer.png");
            if (editorSprite != null)
            {
                return editorSprite;
            }
#endif

            string diskPath = System.IO.Path.Combine(Application.dataPath, "_Project/Art/UI/hand_pointer.png");
            if (System.IO.File.Exists(diskPath))
            {
                byte[] pngBytes = System.IO.File.ReadAllBytes(diskPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(pngBytes))
                {
                    tex.filterMode = FilterMode.Point;
                    return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            return null;
        }

        private static void CreateArrowBar(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            Color fillColor,
            Color outlineColor)
        {
            GameObject bar = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            bar.transform.SetParent(parent, false);

            RectTransform rt = bar.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = bar.GetComponent<Image>();
            img.color = fillColor;
            img.raycastTarget = false;

            Outline outline = bar.GetComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void DestroyArrowPointer()
        {
            if (_arrowRoot != null)
            {
                Destroy(_arrowRoot.gameObject);
            }

            _arrowRoot = null;
        }

        private void ClearHighlight()
        {
            if (_highlightOutline != null && _addedOutline)
            {
                Destroy(_highlightOutline);
            }

            _highlightOutline = null;
            _highlightedButton = null;
            _addedOutline = false;
        }
    }
}
