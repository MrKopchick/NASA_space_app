using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using AstraLink.Core;

namespace AstraLink.UI.Flight.SpaceMap
{
    /// <summary>
    /// Collapsible (- / +) pixel-art Command HUD for the Solar System flight tab,
    /// paired with a separate bottom-left Planet Info card so both remain spacious,
    /// legible, and harmonious with uniform button sizing.
    /// </summary>
    public class SolarSystemHUDPanel : MonoBehaviour
    {
        private const int ChartWidth = 240;
        private const int ChartHeight = 82;

        private SolarSystemArtBank _artBank;
        private SpaceMapController _spaceMap;

        // Main Command HUD (Top-Right, collapsible - / +)
        private GameObject _expandedRoot;
        private GameObject _minimizedRoot;
        private Button _minimizeButton;
        private Button _expandButton;
        private Text _minimizedBadgeText;

        // Separate Planet Info Panel (Bottom-Left)
        private GameObject _planetInfoRoot;
        private GameObject _planetInfoBody;
        private Button _planetCollapseBtn;
        private Text _planetCollapseLabel;
        private bool _planetCardCollapsed;
        private Image _inspectorPreviewImg;
        private Text _inspectorTitleText;
        private Text _inspectorMetaText;
        private Text _inspectorDescText;
        private string _inspectedBodyId = "earth";
        private Sprite[] _inspectedFrames;
        private float _inspectedFps = 6f;
        private float _inspectedAnimTime;

        // Dev Safe Mode (Endless Test)
        private bool _devSafeMode;
        private Button _devSafeBtn;
        private Text _devSafeLabel;

        // Playback & timeline widgets
        private Button _playPauseBtn;
        private Text _playPauseLabel;
        private Button _speed1xBtn;
        private Button _speed5xBtn;
        private Button _speed10xBtn;
        private Slider _timelineScrubber;
        private bool _suppressScrubberCallback;
        private Text _tickStatusText;
        private Image _statusBannerBg;
        private Text _statusAlertText;

        // 5 Live Telemetry Bars + Values
        private Image _batteryFillImg;
        private Text _batteryValueText;
        private Image _ssdFillImg;
        private Text _ssdValueText;
        private Image _healthFillImg;
        private Text _healthValueText;
        private Image _scienceFillImg;
        private Text _scienceValueText;
        private Image _radiationFillImg;
        private Text _radiationValueText;

        // Live Pixel-Art Telemetry Chart
        private RawImage _telemetryChartRaw;
        private Texture2D _telemetryChartTex;
        private Color32[] _chartPixels;
        private IReadOnlyList<SimulationTickSnapshot> _historyTicks;
        private MissionGoal _historyGoal;
        private int _lastRenderedTickIdx = -1;

        // Phase Badges
        private Image _eclipseBadgeBg;
        private Text _eclipseBadgeText;
        private Image _commBadgeBg;
        private Text _commBadgeText;
        private Image _safeBadgeBg;
        private Text _safeBadgeText;

        // Manual Overrides + Signal Delay Timer
        private Image _sciSlotBg;
        private Text _sciValueText;
        private Image _commsSlotBg;
        private Text _commsValueText;
        private Image _safeSlotBg;
        private Text _safeSlotValueText;
        private Image _signalDelayFillImg;
        private Text _signalDelayText;
        private Text _commandLogText;

        // Autopilot Rules Monitor
        private Transform _rulesListContainer;
        private readonly List<AutopilotRule> _boundRules = new List<AutopilotRule>();
        private readonly List<Image> _ruleRowBgs = new List<Image>();
        private readonly List<Text> _ruleRowTexts = new List<Text>();

        // Audio channel & controls
        private AudioSource _musicSource;
        private AudioSource _ambienceSource;
        private AudioSource _sfxSource;
        private bool _musicEnabled = true;
        private bool _sfxEnabled = true;
        private float _masterVolume = 0.70f;
        private Button _musicToggleBtn;
        private Text _musicToggleLabel;
        private Button _sfxToggleBtn;
        private Text _sfxToggleLabel;
        private Text _volumeLabelText;
        private bool _wasStormOrAlarmActive;
        private bool _isCollapsed;
        private bool _built;

        // Callbacks wired by MissionReportUI
        public event Action OnPlayPauseClicked;
        public event Action OnStepTickClicked;
        public event Action OnRestartFlightClicked;
        public event Action<int> OnScrubTickRequested;
        public event Action<float> OnSpeedSelected;
        public event Action OnToggleInstrumentsClicked;
        public event Action OnToggleCommsClicked;
        public event Action OnToggleSafeModeClicked;
        public event Action<bool> OnDevSafeModeToggled;
        public event Action OnOpenDebriefClicked;

        public bool IsCollapsed => _isCollapsed;
        public bool IsDevSafeMode => _devSafeMode;
        public string InspectedBodyId => _inspectedBodyId;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureAudioSources();
            RefreshAudioPlaybackState();
        }

        private void OnDisable()
        {
            if (_musicSource != null && _musicSource.isPlaying) _musicSource.Pause();
            if (_ambienceSource != null && _ambienceSource.isPlaying) _ambienceSource.Pause();
        }

        private void OnDestroy()
        {
            if (_telemetryChartTex != null)
            {
                Destroy(_telemetryChartTex);
                _telemetryChartTex = null;
            }
        }

        private void Update()
        {
            if (!_built || _planetCardCollapsed || _inspectorPreviewImg == null)
            {
                return;
            }

            if (_inspectedFrames != null && _inspectedFrames.Length > 0)
            {
                _inspectedAnimTime += Time.unscaledDeltaTime * _inspectedFps;
                int idx = Mathf.FloorToInt(_inspectedAnimTime) % _inspectedFrames.Length;
                if (idx < 0) idx += _inspectedFrames.Length;
                Sprite frame = _inspectedFrames[idx];
                if (frame != null && _inspectorPreviewImg.sprite != frame)
                {
                    _inspectorPreviewImg.sprite = frame;
                }
            }
        }

        public void BindSpaceMap(SpaceMapController spaceMap)
        {
            EnsureBuilt();
            if (_spaceMap != null)
            {
                _spaceMap.OnCelestialBodySelected -= HandleMapBodySelected;
            }
            _spaceMap = spaceMap;
            if (_spaceMap != null)
            {
                _spaceMap.OnCelestialBodySelected += HandleMapBodySelected;
                HandleMapBodySelected(_spaceMap.SelectedBody);
            }
        }

        public void SetCollapsed(bool collapsed, bool playSound = false)
        {
            EnsureBuilt();
            _isCollapsed = collapsed;
            if (_expandedRoot != null) _expandedRoot.SetActive(!collapsed);
            if (_minimizedRoot != null) _minimizedRoot.SetActive(collapsed);
            if (playSound) PlaySfx(_artBank != null ? _artBank.uiToggle : null, 0.7f);
        }

        public void SetDevSafeModeVisual(bool active)
        {
            _devSafeMode = active;
            if (_devSafeLabel != null)
            {
                _devSafeLabel.text = active ? "DEV:ON" : "DEV:OFF";
                _devSafeLabel.color = active ? new Color(0.55f, 1f, 0.72f, 1f) : new Color(0.90f, 0.94f, 1f, 1f);
            }
            if (_devSafeBtn != null)
            {
                Image img = _devSafeBtn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = active ? new Color(0.45f, 0.96f, 0.65f, 1f) : new Color(0.80f, 0.85f, 0.95f, 0.92f);
                }
            }
        }

        public void SetTelemetryHistory(IReadOnlyList<SimulationTickSnapshot> ticks, MissionGoal goal)
        {
            EnsureBuilt();
            _historyTicks = ticks;
            _historyGoal = goal;
            _lastRenderedTickIdx = -1;
            RedrawTelemetryChart(0);
        }

        public void SetAutopilotRules(IReadOnlyList<AutopilotRule> rules)
        {
            EnsureBuilt();
            _boundRules.Clear();
            _ruleRowBgs.Clear();
            _ruleRowTexts.Clear();

            if (_rulesListContainer == null) return;
            for (int i = _rulesListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_rulesListContainer.GetChild(i).gameObject);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (rules == null || rules.Count == 0)
            {
                Text empty = CreateText("EmptyRules", _rulesListContainer, font, "No autopilot rules configured.", 10, FontStyle.Italic, new Color(0.66f, 0.76f, 0.90f, 1f), TextAnchor.MiddleLeft);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
                return;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                AutopilotRule rule = rules[i];
                if (rule == null) continue;
                _boundRules.Add(rule);

                GameObject row = CreateUIObject("RuleRow_" + i, _rulesListContainer);
                row.AddComponent<LayoutElement>().preferredHeight = 22f;
                Image bg = row.AddComponent<Image>();
                bg.color = new Color(0.10f, 0.14f, 0.24f, 0.86f);
                _ruleRowBgs.Add(bg);

                string label = string.Format("IF {0} {1} {2:0.#} -> {3}",
                    rule.Metric,
                    FormatOp(rule.Operator),
                    rule.Threshold,
                    rule.ThenAction);

                Text txt = CreateText("RuleText", row.transform, font, label, 10, FontStyle.Bold, new Color(0.84f, 0.90f, 0.98f, 1f), TextAnchor.MiddleLeft);
                RectTransform trt = txt.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(8f, 0f);
                trt.offsetMax = new Vector2(-8f, 0f);
                _ruleRowTexts.Add(txt);
            }
        }

        public void UpdateSignalDelay(bool inTransit, float remainingSec, float totalSec, string latestCommandSummary)
        {
            EnsureBuilt();
            if (_signalDelayFillImg != null)
            {
                float frac = inTransit && totalSec > 0.01f
                    ? 1f - Mathf.Clamp01(remainingSec / totalSec)
                    : 1f;
                _signalDelayFillImg.fillAmount = frac;
                _signalDelayFillImg.color = inTransit
                    ? new Color(1f, 0.78f, 0.26f, 1f)
                    : new Color(0.32f, 0.90f, 0.62f, 1f);
            }

            if (_signalDelayText != null)
            {
                _signalDelayText.text = inTransit
                    ? string.Format("TX IN TRANSIT: {0:0.0}s", Mathf.Max(0f, remainingSec))
                    : "SIGNAL READY (0.0s)";
                _signalDelayText.color = inTransit
                    ? new Color(1f, 0.88f, 0.45f, 1f)
                    : new Color(0.65f, 0.96f, 0.80f, 1f);
            }

            if (_commandLogText != null && !string.IsNullOrEmpty(latestCommandSummary))
            {
                _commandLogText.text = latestCommandSummary;
            }
        }

        public void UpdatePlaybackState(bool isPlaying, float speedMultiplier, int tickIndex, int totalTicks, MissionGoal goal, OrbitDefinition orbit)
        {
            EnsureBuilt();
            if (_playPauseLabel != null)
            {
                _playPauseLabel.text = isPlaying ? "PAUSE" : "PLAY";
            }

            HighlightSpeedButton(_speed1xBtn, Mathf.Approximately(speedMultiplier, 1f));
            HighlightSpeedButton(_speed5xBtn, Mathf.Approximately(speedMultiplier, 5f));
            HighlightSpeedButton(_speed10xBtn, Mathf.Approximately(speedMultiplier, 10f));

            int maxTick = Mathf.Max(0, totalTicks - 1);
            if (_timelineScrubber != null)
            {
                _suppressScrubberCallback = true;
                _timelineScrubber.minValue = 0;
                _timelineScrubber.maxValue = Mathf.Max(1, maxTick);
                _timelineScrubber.value = Mathf.Clamp(tickIndex, 0, Mathf.Max(1, maxTick));
                _suppressScrubberCallback = false;
            }

            int dispTick = Mathf.Clamp(tickIndex + 1, 1, Mathf.Max(1, totalTicks));
            string missionName = goal != null && !string.IsNullOrEmpty(goal.Title) ? goal.Title : "Solar Mission";
            string orbitShort = orbit != null && !string.IsNullOrEmpty(orbit.Name) ? orbit.Name : "Orbit";
            if (_tickStatusText != null)
            {
                _tickStatusText.text = string.Format("T {0:00}/{1:00}  |  {2}", dispTick, Mathf.Max(1, totalTicks), missionName);
            }
            if (_minimizedBadgeText != null)
            {
                _minimizedBadgeText.text = string.Format("{0:00}/{1:00}  {2}", dispTick, Mathf.Max(1, totalTicks), orbitShort);
            }

            if (_lastRenderedTickIdx != tickIndex)
            {
                _lastRenderedTickIdx = tickIndex;
                RedrawTelemetryChart(tickIndex);
            }
        }

        public void UpdateTelemetrySnapshot(SimulationTickSnapshot snap, MissionGoal goal)
        {
            EnsureBuilt();
            if (snap == null) return;

            if (_batteryFillImg != null)
            {
                _batteryFillImg.fillAmount = Mathf.Clamp01(snap.BatteryPercent / 100f);
                _batteryFillImg.color = snap.BatteryPercent < 20f
                    ? new Color(1f, 0.38f, 0.34f, 1f)
                    : new Color(0.96f, 0.78f, 0.24f, 1f);
            }
            if (_batteryValueText != null)
            {
                _batteryValueText.text = string.Format("{0:0}% ({1:0}Wh)", snap.BatteryPercent, snap.BatteryWh);
            }

            if (_ssdFillImg != null)
            {
                _ssdFillImg.fillAmount = Mathf.Clamp01(snap.SsdUsedPercent / 100f);
                _ssdFillImg.color = snap.SsdUsedPercent > 85f
                    ? new Color(1f, 0.56f, 0.26f, 1f)
                    : new Color(0.45f, 0.78f, 1f, 1f);
            }
            if (_ssdValueText != null)
            {
                _ssdValueText.text = string.Format("{0:0}% ({1:0.0}GB)", snap.SsdUsedPercent, snap.SsdUsedGb);
            }

            if (_healthFillImg != null)
            {
                _healthFillImg.fillAmount = Mathf.Clamp01(snap.HealthPercent / 100f);
                _healthFillImg.color = snap.HealthPercent < 55f
                    ? new Color(1f, 0.42f, 0.36f, 1f)
                    : new Color(0.42f, 0.94f, 0.62f, 1f);
            }
            if (_healthValueText != null)
            {
                _healthValueText.text = string.Format("{0:0}%", snap.HealthPercent);
            }

            float targetSci = goal != null ? Mathf.Max(1f, goal.TargetSciencePoints) : 100f;
            if (_scienceFillImg != null)
            {
                _scienceFillImg.fillAmount = Mathf.Clamp01(snap.DeliveredScience / targetSci);
            }
            if (_scienceValueText != null)
            {
                _scienceValueText.text = string.Format("{0:0}/{1:0}", snap.DeliveredScience, targetSci);
            }

            if (_radiationFillImg != null)
            {
                _radiationFillImg.fillAmount = Mathf.Clamp01(snap.RadiationLevel / 100f);
                _radiationFillImg.color = snap.RadiationLevel >= 50f
                    ? new Color(1f, 0.34f, 0.52f, 1f)
                    : new Color(0.76f, 0.54f, 0.98f, 1f);
            }
            if (_radiationValueText != null)
            {
                _radiationValueText.text = string.Format("{0:0} mSv", snap.RadiationLevel);
            }

            UpdatePhasePill(_eclipseBadgeBg, _eclipseBadgeText, snap.IsInEclipse, "ECLIPSE", "SUNLIGHT",
                new Color(0.22f, 0.30f, 0.56f, 0.95f), new Color(0.50f, 0.40f, 0.14f, 0.92f));
            UpdatePhasePill(_commBadgeBg, _commBadgeText, snap.IsInCommWindow, "DSN LINK", "NO COMM",
                new Color(0.14f, 0.48f, 0.30f, 0.95f), new Color(0.24f, 0.20f, 0.28f, 0.90f));
            UpdatePhasePill(_safeBadgeBg, _safeBadgeText, snap.SafeModeActive, "SAFE MODE", "NOMINAL",
                new Color(0.58f, 0.42f, 0.12f, 0.95f), new Color(0.14f, 0.26f, 0.38f, 0.90f));

            if (_commsValueText != null)
            {
                bool linkUp = snap.CommsActive && snap.IsInCommWindow;
                _commsValueText.text = snap.CommsActive ? (linkUp ? "LINK" : "ON") : "OFF";
                _commsValueText.color = !snap.CommsActive
                    ? new Color(0.82f, 0.55f, 0.55f, 1f)
                    : (linkUp ? new Color(0.45f, 0.98f, 0.68f, 1f) : new Color(0.95f, 0.85f, 0.45f, 1f));
            }
            if (_sciValueText != null)
            {
                _sciValueText.text = snap.ScienceActive ? "ON" : "OFF";
                _sciValueText.color = snap.ScienceActive
                    ? new Color(0.52f, 0.92f, 1f, 1f)
                    : new Color(0.82f, 0.55f, 0.55f, 1f);
            }
            if (_safeSlotValueText != null)
            {
                _safeSlotValueText.text = snap.SafeModeActive ? "ON" : "OFF";
                _safeSlotValueText.color = snap.SafeModeActive
                    ? new Color(1f, 0.86f, 0.36f, 1f)
                    : new Color(0.84f, 0.92f, 1f, 1f);
            }

            if (_safeSlotBg != null && _artBank != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(_safeSlotBg, snap.SafeModeActive ? _artBank.itemSlotSelected : _artBank.itemSlot, new Vector4(10f, 10f, 10f, 10f), Color.white);
            }
            if (_commsSlotBg != null && _artBank != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(_commsSlotBg, snap.CommsActive ? _artBank.itemSlotSelected : _artBank.itemSlot, new Vector4(10f, 10f, 10f, 10f), Color.white);
            }
            if (_sciSlotBg != null && _artBank != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(_sciSlotBg, snap.ScienceActive ? _artBank.itemSlotSelected : _artBank.itemSlot, new Vector4(10f, 10f, 10f, 10f), Color.white);
            }

            for (int i = 0; i < _boundRules.Count && i < _ruleRowBgs.Count; i++)
            {
                bool active = snap.TriggeredRuleIds != null && snap.TriggeredRuleIds.Contains(_boundRules[i].RuleId);
                _ruleRowBgs[i].color = active
                    ? new Color(0.14f, 0.46f, 0.24f, 0.96f)
                    : new Color(0.10f, 0.14f, 0.24f, 0.86f);
                _ruleRowTexts[i].color = active
                    ? new Color(0.75f, 1f, 0.82f, 1f)
                    : new Color(0.84f, 0.90f, 0.98f, 1f);
            }

            bool hasAlert = !string.IsNullOrEmpty(snap.ActiveEventTitle) || snap.RadiationLevel >= 50f || snap.SafeModeActive;
            if (_statusAlertText != null && _statusBannerBg != null)
            {
                if (_devSafeMode)
                {
                    _statusAlertText.text = !string.IsNullOrEmpty(snap.ActiveEventTitle)
                        ? "DEV SAFE: " + snap.ActiveEventTitle.ToUpperInvariant()
                        : "DEV SAFE MODE: ENDLESS SOLAR TEST";
                    _statusBannerBg.color = new Color(0.14f, 0.40f, 0.28f, 0.92f);
                }
                else if (!string.IsNullOrEmpty(snap.ActiveEventTitle))
                {
                    _statusAlertText.text = "EVENT: " + snap.ActiveEventTitle.ToUpperInvariant();
                    _statusBannerBg.color = new Color(0.55f, 0.18f, 0.18f, 0.92f);
                }
                else if (snap.IsInEclipse)
                {
                    _statusAlertText.text = "PHASE: ECLIPSE SHADOW (SOLAR 0W)";
                    _statusBannerBg.color = new Color(0.16f, 0.22f, 0.42f, 0.90f);
                }
                else if (snap.IsInCommWindow)
                {
                    _statusAlertText.text = "PHASE: DSN COMMS WINDOW ACTIVE";
                    _statusBannerBg.color = new Color(0.12f, 0.40f, 0.26f, 0.90f);
                }
                else
                {
                    _statusAlertText.text = "PHASE: SUNLIT ORBITAL CRUISE";
                    _statusBannerBg.color = new Color(0.10f, 0.20f, 0.34f, 0.88f);
                }
            }

            if (hasAlert && !_wasStormOrAlarmActive && !_devSafeMode)
            {
                PlaySfx(_artBank != null ? _artBank.solarStormAlarm : null, 0.55f);
            }
            _wasStormOrAlarmActive = hasAlert;
        }

        public void NotifyMissionOutcomeSfx(MissionStatus status)
        {
            if (status == MissionStatus.Success)
            {
                PlaySfx(_artBank != null ? _artBank.missionSuccess : null, 0.75f);
            }
        }

        public void EnsureBuilt()
        {
            if (_built && _expandedRoot != null && _minimizedRoot != null && _planetInfoRoot != null)
            {
                return;
            }

            _artBank = SolarSystemArtBank.Load();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            RectTransform selfRt = GetComponent<RectTransform>();
            if (selfRt == null) selfRt = gameObject.AddComponent<RectTransform>();
            selfRt.anchorMin = Vector2.zero;
            selfRt.anchorMax = Vector2.one;
            selfRt.offsetMin = Vector2.zero;
            selfRt.offsetMax = Vector2.zero;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            BuildSeparatePlanetInfoPanel(font);
            BuildMinimizedChip(font);
            BuildExpandedPanel(font);
            EnsureAudioSources();
            RefreshAudioUI();

            _built = true;
            SetDevSafeModeVisual(_devSafeMode);
            SetCollapsed(false, playSound: false);
        }

        private void BuildSeparatePlanetInfoPanel(Font font)
        {
            _planetInfoRoot = CreateUIObject("PlanetInfoPanel_BottomLeft", transform);
            RectTransform rt = _planetInfoRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(16f, 16f);
            rt.sizeDelta = new Vector2(368f, 146f);

            ContentSizeFitter csf = _planetInfoRoot.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image bg = _planetInfoRoot.AddComponent<Image>();
            if (_artBank != null && _artBank.inventoryPanel != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(bg, _artBank.inventoryPanel, new Vector4(12f, 12f, 12f, 12f), new Color(0.96f, 0.98f, 1f, 0.95f));
            }
            else
            {
                bg.color = new Color(0.08f, 0.12f, 0.24f, 0.94f);
            }

            VerticalLayoutGroup rootVl = _planetInfoRoot.AddComponent<VerticalLayoutGroup>();
            rootVl.padding = new RectOffset(10, 10, 8, 10);
            rootVl.spacing = 6f;
            rootVl.childControlWidth = true;
            rootVl.childControlHeight = true;
            rootVl.childForceExpandWidth = true;
            rootVl.childForceExpandHeight = false;

            // Header bar with uniform buttons
            GameObject header = CreateUIObject("PlanetHeader", _planetInfoRoot.transform);
            header.AddComponent<LayoutElement>().preferredHeight = 32f;
            HorizontalLayoutGroup hhl = header.AddComponent<HorizontalLayoutGroup>();
            hhl.spacing = 6f;
            hhl.childAlignment = TextAnchor.MiddleCenter;
            hhl.childControlWidth = true;
            hhl.childControlHeight = true;
            hhl.childForceExpandWidth = false;

            GameObject titleBar = CreateUIObject("TitleBanner", header.transform);
            titleBar.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Image tBg = titleBar.AddComponent<Image>();
            if (_artBank != null && _artBank.nameTab != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(tBg, _artBank.nameTab, new Vector4(8f, 6f, 8f, 6f), Color.white);
            }
            else
            {
                tBg.color = new Color(0.14f, 0.42f, 0.56f, 1f);
            }

            _inspectorTitleText = CreateText("BodyName", titleBar.transform, font, "EARTH", 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(_inspectorTitleText.GetComponent<RectTransform>());

            CreatePixelButton(header.transform, "Btn_PrevBody", "<", Color.white, 36f, 32f, 12, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.55f);
                if (_spaceMap != null) _spaceMap.SelectNextBody(-1);
            });
            CreatePixelButton(header.transform, "Btn_NextBody", ">", Color.white, 36f, 32f, 12, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.55f);
                if (_spaceMap != null) _spaceMap.SelectNextBody(1);
            });
            _planetCollapseBtn = CreatePixelButton(header.transform, "Btn_CollapsePlanet", "-", new Color(1f, 0.92f, 0.90f, 1f), 36f, 32f, 14, font, () =>
            {
                _planetCardCollapsed = !_planetCardCollapsed;
                if (_planetInfoBody != null) _planetInfoBody.SetActive(!_planetCardCollapsed);
                if (_planetCollapseLabel != null) _planetCollapseLabel.text = _planetCardCollapsed ? "+" : "-";
                PlaySfx(_artBank != null ? _artBank.uiToggle : null, 0.65f);
            });
            _planetCollapseLabel = _planetCollapseBtn.GetComponentInChildren<Text>();

            // Body card
            _planetInfoBody = CreateCardBox("PlanetCardBody", _planetInfoRoot.transform, 92f);
            HorizontalLayoutGroup hl = _planetInfoBody.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(8, 8, 6, 6);
            hl.spacing = 10f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;

            GameObject slotObj = CreateUIObject("PlanetPreviewSlot", _planetInfoBody.transform);
            LayoutElement slotLe = slotObj.AddComponent<LayoutElement>();
            slotLe.preferredWidth = 74f;
            slotLe.preferredHeight = 74f;
            Image slotBg = slotObj.AddComponent<Image>();
            if (_artBank != null && _artBank.itemSlotSelected != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(slotBg, _artBank.itemSlotSelected, new Vector4(10f, 10f, 10f, 10f), Color.white);
            }
            else
            {
                slotBg.color = new Color(0.10f, 0.16f, 0.28f, 1f);
            }

            GameObject previewObj = CreateUIObject("AnimatedPlanetPreview", slotObj.transform);
            RectTransform prt = previewObj.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.12f, 0.12f);
            prt.anchorMax = new Vector2(0.88f, 0.88f);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            _inspectorPreviewImg = previewObj.AddComponent<Image>();
            _inspectorPreviewImg.preserveAspect = true;
            _inspectorPreviewImg.raycastTarget = false;

            GameObject infoCol = CreateUIObject("PlanetInfoCol", _planetInfoBody.transform);
            infoCol.AddComponent<LayoutElement>().flexibleWidth = 1f;
            VerticalLayoutGroup vl = infoCol.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 4f;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            _inspectorMetaText = CreateText("BodyMeta", infoCol.transform, font, "1.00 AU  |  365.25d orbit", 11, FontStyle.Bold, new Color(1f, 0.88f, 0.52f, 1f), TextAnchor.MiddleLeft);
            _inspectorMetaText.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            _inspectorDescText = CreateText("BodyDesc", infoCol.transform, font, "Click any planet or Asteroid Belt on the map to inspect & follow.", 11, FontStyle.Normal, new Color(0.86f, 0.92f, 0.99f, 1f), TextAnchor.UpperLeft);
            _inspectorDescText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _inspectorDescText.verticalOverflow = VerticalWrapMode.Truncate;
            _inspectorDescText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private void BuildMinimizedChip(Font font)
        {
            _minimizedRoot = CreateUIObject("SinglePanel_Minimized", transform);
            RectTransform rt = _minimizedRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, -14f);
            rt.sizeDelta = new Vector2(234f, 46f);

            Image bg = _minimizedRoot.AddComponent<Image>();
            if (_artBank != null && _artBank.dialogueBox != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(bg, _artBank.dialogueBox, new Vector4(10f, 10f, 10f, 10f), new Color(0.95f, 0.97f, 1f, 0.96f));
            }
            else
            {
                bg.color = new Color(0.08f, 0.12f, 0.24f, 0.94f);
            }

            HorizontalLayoutGroup hl = _minimizedRoot.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(8, 8, 6, 6);
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;

            if (_artBank != null && _artBank.starChart != null)
            {
                GameObject iconObj = CreateUIObject("MiniIcon", _minimizedRoot.transform);
                iconObj.AddComponent<LayoutElement>().preferredWidth = 26f;
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = _artBank.starChart;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            _minimizedBadgeText = CreateText("MiniStatus", _minimizedRoot.transform, font, "MAP HUD", 12, FontStyle.Bold, new Color(0.90f, 0.96f, 1f, 1f), TextAnchor.MiddleLeft);
            _minimizedBadgeText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _expandButton = CreatePixelButton(_minimizedRoot.transform, "Btn_ExpandPanel", "+", new Color(0.95f, 1f, 0.98f, 1f), 36f, 32f, 16, font, () =>
            {
                SetCollapsed(false, playSound: true);
            });
        }

        private void BuildExpandedPanel(Font font)
        {
            _expandedRoot = CreateUIObject("SinglePanel_Expanded", transform);
            RectTransform rt = _expandedRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(-418f, 16f);
            rt.offsetMax = new Vector2(-16f, -14f);

            Image frameImg = _expandedRoot.AddComponent<Image>();
            if (_artBank != null && _artBank.inventoryPanel != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(frameImg, _artBank.inventoryPanel, new Vector4(12f, 12f, 12f, 12f), new Color(0.96f, 0.98f, 1f, 0.95f));
            }
            else
            {
                frameImg.color = new Color(0.08f, 0.12f, 0.24f, 0.94f);
            }

            VerticalLayoutGroup outerLayout = _expandedRoot.AddComponent<VerticalLayoutGroup>();
            outerLayout.padding = new RectOffset(12, 12, 10, 10);
            outerLayout.spacing = 8f;
            outerLayout.childControlWidth = true;
            outerLayout.childControlHeight = true;
            outerLayout.childForceExpandWidth = true;
            outerLayout.childForceExpandHeight = false;

            BuildPanelHeader(font, _expandedRoot.transform);

            GameObject scrollObj = CreateUIObject("PanelScroll", _expandedRoot.transform);
            LayoutElement scrollLe = scrollObj.AddComponent<LayoutElement>();
            scrollLe.minHeight = 140f;
            scrollLe.preferredHeight = 720f;
            scrollLe.flexibleHeight = 1f;
            ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 28f;

            GameObject viewport = CreateUIObject("Viewport", scrollObj.transform);
            StretchFull(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            GameObject content = CreateUIObject("Content", viewport.transform);
            RectTransform crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0f, 0f);

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            VerticalLayoutGroup bodyLayout = content.AddComponent<VerticalLayoutGroup>();
            bodyLayout.padding = new RectOffset(0, 2, 2, 4);
            bodyLayout.spacing = 8f;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;

            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.content = crt;

            BuildPlaybackStrip(font, content.transform);
            BuildTelemetryGaugesAndChartCard(font, content.transform);
            BuildManualOverrideAndSignalCard(font, content.transform);
            BuildAutopilotRulesCard(font, content.transform);
            BuildAudioControlsRow(font, content.transform);

            Button debriefBtn = CreatePixelButton(_expandedRoot.transform, "Btn_OpenDebrief", "POST-FLIGHT DEBRIEF >>", new Color(0.88f, 1f, 0.92f, 1f), 0f, 36f, 12, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.65f);
                OnOpenDebriefClicked?.Invoke();
            });
            LayoutElement debriefLe = debriefBtn.GetComponent<LayoutElement>();
            debriefLe.flexibleWidth = 1f;
            debriefLe.minHeight = 36f;
            debriefLe.preferredHeight = 36f;
            debriefLe.flexibleHeight = 0f;
        }

        private void BuildPanelHeader(Font font, Transform parent)
        {
            GameObject header = CreateUIObject("PanelHeader", parent);
            LayoutElement headerLe = header.AddComponent<LayoutElement>();
            headerLe.minHeight = 34f;
            headerLe.preferredHeight = 34f;
            headerLe.flexibleHeight = 0f;

            HorizontalLayoutGroup hl = header.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;

            GameObject tabBanner = CreateUIObject("TitleBanner", header.transform);
            tabBanner.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Image tabBg = tabBanner.AddComponent<Image>();
            if (_artBank != null && _artBank.nameTab != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(tabBg, _artBank.nameTab, new Vector4(8f, 6f, 8f, 6f), Color.white);
            }
            else
            {
                tabBg.color = new Color(0.14f, 0.42f, 0.56f, 1f);
            }

            HorizontalLayoutGroup thl = tabBanner.AddComponent<HorizontalLayoutGroup>();
            thl.padding = new RectOffset(8, 8, 2, 2);
            thl.spacing = 6f;
            thl.childAlignment = TextAnchor.MiddleLeft;
            thl.childControlWidth = true;
            thl.childControlHeight = true;
            thl.childForceExpandWidth = false;

            if (_artBank != null && _artBank.starMapTablet != null)
            {
                GameObject ic = CreateUIObject("MapTabletIcon", tabBanner.transform);
                ic.AddComponent<LayoutElement>().preferredWidth = 20f;
                Image icImg = ic.AddComponent<Image>();
                icImg.sprite = _artBank.starMapTablet;
                icImg.preserveAspect = true;
                icImg.raycastTarget = false;
            }

            Text title = CreateText("HeaderTitle", tabBanner.transform, font, "MISSION CONTROL", 12, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Uniform 32px height header buttons
            _devSafeBtn = CreatePixelButton(header.transform, "Btn_DevSafeMode", "DEV:OFF", Color.white, 72f, 32f, 11, font, () =>
            {
                _devSafeMode = !_devSafeMode;
                SetDevSafeModeVisual(_devSafeMode);
                PlaySfx(_artBank != null ? _artBank.uiToggle : null, 0.7f);
                OnDevSafeModeToggled?.Invoke(_devSafeMode);
            });
            _devSafeLabel = _devSafeBtn.GetComponentInChildren<Text>();

            CreatePixelButton(header.transform, "Btn_FitSystem", "FIT", new Color(0.95f, 0.98f, 1f, 1f), 48f, 32f, 11, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.6f);
                if (_spaceMap != null) _spaceMap.ResetCameraToSystemOverview();
            });

            _minimizeButton = CreatePixelButton(header.transform, "Btn_MinimizePanel", "-", new Color(1f, 0.90f, 0.88f, 1f), 36f, 32f, 15, font, () =>
            {
                SetCollapsed(true, playSound: true);
            });
        }

        private void BuildPlaybackStrip(Font font, Transform parent)
        {
            GameObject playbackBox = CreateCardBox("PlaybackAndStatusCard", parent, 0f);
            VerticalLayoutGroup vl = playbackBox.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(10, 10, 8, 8);
            vl.spacing = 6f;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            // Status line with missionBadge
            GameObject topRow = CreateUIObject("MissionTickRow", playbackBox.transform);
            topRow.AddComponent<LayoutElement>().preferredHeight = 20f;
            HorizontalLayoutGroup trl = topRow.AddComponent<HorizontalLayoutGroup>();
            trl.spacing = 6f;
            trl.childAlignment = TextAnchor.MiddleLeft;
            trl.childControlWidth = true;
            trl.childControlHeight = true;
            trl.childForceExpandWidth = false;

            if (_artBank != null && _artBank.missionBadge != null)
            {
                GameObject bdg = CreateUIObject("BadgeIcon", topRow.transform);
                bdg.AddComponent<LayoutElement>().preferredWidth = 18f;
                Image bi = bdg.AddComponent<Image>();
                bi.sprite = _artBank.missionBadge;
                bi.preserveAspect = true;
                bi.raycastTarget = false;
            }

            _tickStatusText = CreateText("TickStatus", topRow.transform, font, "T 01/20  |  LEO Mission", 11, FontStyle.Bold, new Color(0.92f, 0.97f, 1f, 1f), TextAnchor.MiddleLeft);
            _tickStatusText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Uniform Row 1: 3 Equal Action Buttons (PLAY/PAUSE | STEP +1 | RESTART)
            GameObject actionRow = CreateUIObject("PlaybackActionRow", playbackBox.transform);
            actionRow.AddComponent<LayoutElement>().preferredHeight = 32f;
            HorizontalLayoutGroup arl = actionRow.AddComponent<HorizontalLayoutGroup>();
            arl.spacing = 6f;
            arl.childAlignment = TextAnchor.MiddleCenter;
            arl.childControlWidth = true;
            arl.childControlHeight = true;
            arl.childForceExpandWidth = true;

            _playPauseBtn = CreatePixelButton(actionRow.transform, "Btn_PlayPause", "PAUSE", new Color(0.84f, 1f, 0.90f, 1f), 0f, 32f, 11, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.65f);
                OnPlayPauseClicked?.Invoke();
            });
            _playPauseLabel = _playPauseBtn.GetComponentInChildren<Text>();

            CreatePixelButton(actionRow.transform, "Btn_Step", "STEP +1", Color.white, 0f, 32f, 11, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.55f);
                OnStepTickClicked?.Invoke();
            });

            CreatePixelButton(actionRow.transform, "Btn_Restart", "RESTART", new Color(1f, 0.92f, 0.78f, 1f), 0f, 32f, 11, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.6f);
                OnRestartFlightClicked?.Invoke();
            });

            // Uniform Row 2: 3 Equal Speed Buttons (1x | 5x | 10x)
            GameObject speedRow = CreateUIObject("SpeedControlsRow", playbackBox.transform);
            speedRow.AddComponent<LayoutElement>().preferredHeight = 30f;
            HorizontalLayoutGroup srl = speedRow.AddComponent<HorizontalLayoutGroup>();
            srl.spacing = 6f;
            srl.childAlignment = TextAnchor.MiddleCenter;
            srl.childControlWidth = true;
            srl.childControlHeight = true;
            srl.childForceExpandWidth = true;

            _speed1xBtn = CreatePixelButton(speedRow.transform, "Btn_1x", "1x SPEED", Color.white, 0f, 30f, 11, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.55f);
                OnSpeedSelected?.Invoke(1f);
            });
            _speed5xBtn = CreatePixelButton(speedRow.transform, "Btn_5x", "5x SPEED", Color.white, 0f, 30f, 11, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.55f);
                OnSpeedSelected?.Invoke(5f);
            });
            _speed10xBtn = CreatePixelButton(speedRow.transform, "Btn_10x", "10x SPEED", Color.white, 0f, 30f, 11, font, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.55f);
                OnSpeedSelected?.Invoke(10f);
            });

            // Timeline Scrubber Slider
            _timelineScrubber = CreateTimelineScrubber(playbackBox.transform);
            _timelineScrubber.onValueChanged.AddListener(val =>
            {
                if (_suppressScrubberCallback) return;
                OnScrubTickRequested?.Invoke(Mathf.RoundToInt(val));
            });

            // Alert phase bar
            GameObject alertBanner = CreateUIObject("PhaseBanner", playbackBox.transform);
            alertBanner.AddComponent<LayoutElement>().preferredHeight = 22f;
            _statusBannerBg = alertBanner.AddComponent<Image>();
            _statusBannerBg.color = new Color(0.10f, 0.20f, 0.34f, 0.88f);
            _statusAlertText = CreateText("AlertText", alertBanner.transform, font, "PHASE: SUNLIT ORBITAL CRUISE", 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(_statusAlertText.GetComponent<RectTransform>());
        }

        private void BuildTelemetryGaugesAndChartCard(Font font, Transform parent)
        {
            GameObject card = CreateCardBox("TelemetryAndChartsCard", parent, 0f);
            VerticalLayoutGroup vl = card.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(10, 10, 8, 8);
            vl.spacing = 5f;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            Text hdr = CreateText("TelemetryHdr", card.transform, font, "LIVE TELEMETRY & MISSION CHARTS", 11, FontStyle.Bold, new Color(0.65f, 0.90f, 1f, 1f), TextAnchor.MiddleLeft);
            hdr.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            CreateGaugeBarRow(card.transform, font, "BAT", new Color(0.96f, 0.78f, 0.24f, 1f), out _batteryFillImg, out _batteryValueText);
            CreateGaugeBarRow(card.transform, font, "SSD", new Color(0.45f, 0.78f, 1f, 1f), out _ssdFillImg, out _ssdValueText);
            CreateGaugeBarRow(card.transform, font, "HLT", new Color(0.42f, 0.94f, 0.62f, 1f), out _healthFillImg, out _healthValueText);
            CreateGaugeBarRow(card.transform, font, "SCI", new Color(0.32f, 0.94f, 0.88f, 1f), out _scienceFillImg, out _scienceValueText);
            CreateGaugeBarRow(card.transform, font, "RAD", new Color(0.86f, 0.45f, 0.98f, 1f), out _radiationFillImg, out _radiationValueText);

            GameObject chartFrame = CreateUIObject("TelemetryChartFrame", card.transform);
            chartFrame.AddComponent<LayoutElement>().preferredHeight = 84f;
            Image chartBg = chartFrame.AddComponent<Image>();
            chartBg.color = new Color(0.03f, 0.05f, 0.11f, 0.96f);

            GameObject rawObj = CreateUIObject("ChartRaw", chartFrame.transform);
            RectTransform rrt = rawObj.GetComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(2f, 2f);
            rrt.offsetMax = new Vector2(-2f, -2f);
            _telemetryChartRaw = rawObj.AddComponent<RawImage>();
            _telemetryChartRaw.raycastTarget = false;

            _telemetryChartTex = new Texture2D(ChartWidth, ChartHeight, TextureFormat.RGBA32, false);
            _telemetryChartTex.filterMode = FilterMode.Point;
            _telemetryChartTex.wrapMode = TextureWrapMode.Clamp;
            _chartPixels = new Color32[ChartWidth * ChartHeight];
            _telemetryChartRaw.texture = _telemetryChartTex;
            RedrawTelemetryChart(0);

            Text legend = CreateText("ChartLegend", card.transform, font, "CURVES:  [BAT: YEL]  [SCI: CYN]  [HLT: GRN]  [RAD: MAG]", 9, FontStyle.Bold, new Color(0.76f, 0.86f, 0.96f, 1f), TextAnchor.MiddleCenter);
            legend.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;

            GameObject phaseRow = CreateUIObject("PhaseIndicatorsRow", card.transform);
            phaseRow.AddComponent<LayoutElement>().preferredHeight = 22f;
            HorizontalLayoutGroup phl = phaseRow.AddComponent<HorizontalLayoutGroup>();
            phl.spacing = 6f;
            phl.childControlWidth = true;
            phl.childControlHeight = true;
            phl.childForceExpandWidth = true;

            CreatePhasePill(phaseRow.transform, font, "SUNLIGHT", out _eclipseBadgeBg, out _eclipseBadgeText);
            CreatePhasePill(phaseRow.transform, font, "NO COMM", out _commBadgeBg, out _commBadgeText);
            CreatePhasePill(phaseRow.transform, font, "NOMINAL", out _safeBadgeBg, out _safeBadgeText);
        }

        private void BuildManualOverrideAndSignalCard(Font font, Transform parent)
        {
            GameObject card = CreateCardBox("ManualOverrideCard", parent, 0f);
            VerticalLayoutGroup vl = card.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(10, 10, 8, 8);
            vl.spacing = 6f;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            Text hdr = CreateText("OverrideHdr", card.transform, font, "MANUAL GROUND OVERRIDE & SIGNAL DELAY", 11, FontStyle.Bold, new Color(1f, 0.86f, 0.52f, 1f), TextAnchor.MiddleLeft);
            hdr.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            GameObject row = CreateUIObject("OverrideButtonsRow", card.transform);
            row.AddComponent<LayoutElement>().preferredHeight = 68f;
            HorizontalLayoutGroup hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;

            _sciValueText = CreateTelemetryChip(row.transform, "PayloadChip", _artBank != null ? _artBank.iconSpaceTelescope : null, "INSTRUMENTS", "ON", font, true, out _sciSlotBg, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiToggle : null, 0.65f);
                OnToggleInstrumentsClicked?.Invoke();
            });
            _commsValueText = CreateTelemetryChip(row.transform, "CommsChip", _artBank != null ? _artBank.iconDishAntenna : null, "COMMS", "LINK", font, true, out _commsSlotBg, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiToggle : null, 0.65f);
                OnToggleCommsClicked?.Invoke();
            });
            _safeSlotValueText = CreateTelemetryChip(row.transform, "SafeChip", _artBank != null ? _artBank.iconShieldGenerator : null, "SAFE MODE", "OFF", font, true, out _safeSlotBg, () =>
            {
                PlaySfx(_artBank != null ? _artBank.uiToggle : null, 0.65f);
                OnToggleSafeModeClicked?.Invoke();
            });

            GameObject delayTrack = CreateUIObject("SignalDelayTrack", card.transform);
            delayTrack.AddComponent<LayoutElement>().preferredHeight = 18f;
            Image trackBg = delayTrack.AddComponent<Image>();
            trackBg.color = new Color(0.04f, 0.07f, 0.14f, 0.95f);

            GameObject delayFillObj = CreateUIObject("Fill", delayTrack.transform);
            StretchFull(delayFillObj.GetComponent<RectTransform>());
            _signalDelayFillImg = delayFillObj.AddComponent<Image>();
            _signalDelayFillImg.sprite = CreateSolidWhiteSprite();
            _signalDelayFillImg.type = Image.Type.Filled;
            _signalDelayFillImg.fillMethod = Image.FillMethod.Horizontal;
            _signalDelayFillImg.fillAmount = 1f;
            _signalDelayFillImg.color = new Color(0.32f, 0.90f, 0.62f, 1f);

            _signalDelayText = CreateText("SignalDelayText", delayTrack.transform, font, "SIGNAL READY (0.0s)", 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(_signalDelayText.GetComponent<RectTransform>());

            _commandLogText = CreateText("CmdLogText", card.transform, font, "NO MANUAL COMMANDS", 10, FontStyle.Normal, new Color(0.76f, 0.84f, 0.94f, 1f), TextAnchor.MiddleLeft);
            _commandLogText.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;
        }

        private void BuildAutopilotRulesCard(Font font, Transform parent)
        {
            GameObject card = CreateCardBox("AutopilotRulesCard", parent, 0f);
            VerticalLayoutGroup vl = card.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(10, 10, 8, 8);
            vl.spacing = 4f;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            Text hdr = CreateText("RulesHdr", card.transform, font, "AUTOPILOT RULES (GREEN = TRIGGERED)", 11, FontStyle.Bold, new Color(0.62f, 0.96f, 0.76f, 1f), TextAnchor.MiddleLeft);
            hdr.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            GameObject listObj = CreateUIObject("RulesList", card.transform);
            VerticalLayoutGroup listVl = listObj.AddComponent<VerticalLayoutGroup>();
            listVl.spacing = 3f;
            listVl.childControlWidth = true;
            listVl.childControlHeight = true;
            listVl.childForceExpandWidth = true;
            listVl.childForceExpandHeight = false;
            _rulesListContainer = listObj.transform;
        }

        private void BuildAudioControlsRow(Font font, Transform parent)
        {
            GameObject box = CreateCardBox("AudioControlBar", parent, 48f);
            HorizontalLayoutGroup hl = box.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(8, 8, 6, 6);
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;

            _musicToggleBtn = CreatePixelButton(box.transform, "Btn_MusicToggle", "MUSIC: ON", Color.white, 0f, 32f, 10, font, () =>
            {
                _musicEnabled = !_musicEnabled;
                PlaySfx(_artBank != null ? _artBank.uiToggle : null, 0.65f);
                RefreshAudioPlaybackState();
                RefreshAudioUI();
            });
            _musicToggleLabel = _musicToggleBtn.GetComponentInChildren<Text>();

            _sfxToggleBtn = CreatePixelButton(box.transform, "Btn_SfxToggle", "SFX: ON", Color.white, 0f, 32f, 10, font, () =>
            {
                _sfxEnabled = !_sfxEnabled;
                if (_sfxEnabled) PlaySfx(_artBank != null ? _artBank.uiToggle : null, 0.65f);
                RefreshAudioUI();
            });
            _sfxToggleLabel = _sfxToggleBtn.GetComponentInChildren<Text>();

            CreatePixelButton(box.transform, "Btn_VolDown", "VOL -", Color.white, 0f, 32f, 10, font, () =>
            {
                _masterVolume = Mathf.Clamp01(_masterVolume - 0.15f);
                RefreshAudioPlaybackState();
                RefreshAudioUI();
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.55f);
            });

            _volumeLabelText = CreateText("VolReadout", box.transform, font, "70%", 11, FontStyle.Bold, new Color(0.75f, 0.94f, 1f, 1f), TextAnchor.MiddleCenter);
            LayoutElement vle = _volumeLabelText.gameObject.AddComponent<LayoutElement>();
            vle.preferredWidth = 42f;
            vle.flexibleWidth = 0f;

            CreatePixelButton(box.transform, "Btn_VolUp", "VOL +", Color.white, 0f, 32f, 10, font, () =>
            {
                _masterVolume = Mathf.Clamp01(_masterVolume + 0.15f);
                RefreshAudioPlaybackState();
                RefreshAudioUI();
                PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.55f);
            });
        }

        private GameObject CreateCardBox(string name, Transform parent, float preferredHeight)
        {
            GameObject box = CreateUIObject(name, parent);
            if (preferredHeight > 0f)
            {
                box.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            }
            Image bg = box.AddComponent<Image>();
            if (_artBank != null && _artBank.dialogueBox != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(bg, _artBank.dialogueBox, new Vector4(10f, 10f, 10f, 10f), Color.white);
            }
            else
            {
                bg.color = new Color(0.06f, 0.08f, 0.18f, 0.95f);
            }
            return box;
        }

        private void CreateGaugeBarRow(Transform parent, Font font, string label, Color barColor, out Image fillImg, out Text valueText)
        {
            GameObject row = CreateUIObject("Gauge_" + label, parent);
            row.AddComponent<LayoutElement>().preferredHeight = 18f;
            HorizontalLayoutGroup hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;

            Text lbl = CreateText("Lbl", row.transform, font, label, 10, FontStyle.Bold, new Color(0.78f, 0.88f, 0.98f, 1f), TextAnchor.MiddleLeft);
            lbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 32f;

            GameObject track = CreateUIObject("Track", row.transform);
            LayoutElement tle = track.AddComponent<LayoutElement>();
            tle.flexibleWidth = 1f;
            tle.preferredHeight = 12f;
            Image trackBg = track.AddComponent<Image>();
            trackBg.color = new Color(0.04f, 0.07f, 0.14f, 0.96f);

            GameObject fillObj = CreateUIObject("Fill", track.transform);
            StretchFull(fillObj.GetComponent<RectTransform>());
            fillImg = fillObj.AddComponent<Image>();
            fillImg.sprite = CreateSolidWhiteSprite();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            fillImg.color = barColor;

            valueText = CreateText("Val", row.transform, font, "--", 10, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            valueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;
        }

        private static void CreatePhasePill(Transform parent, Font font, string initialText, out Image bg, out Text txt)
        {
            GameObject obj = CreateUIObject("Pill_" + initialText, parent);
            bg = obj.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.24f, 0.36f, 0.90f);
            txt = CreateText("Txt", obj.transform, font, initialText, 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
        }

        private static void UpdatePhasePill(Image bg, Text txt, bool active, string activeLabel, string idleLabel, Color activeCol, Color idleCol)
        {
            if (bg != null) bg.color = active ? activeCol : idleCol;
            if (txt != null) txt.text = active ? activeLabel : idleLabel;
        }

        private Slider CreateTimelineScrubber(Transform parent)
        {
            GameObject sliderObj = CreateUIObject("TimelineScrubber", parent);
            sliderObj.AddComponent<LayoutElement>().preferredHeight = 16f;

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.wholeNumbers = true;
            slider.minValue = 0;
            slider.maxValue = 19;

            GameObject bgObj = CreateUIObject("Background", sliderObj.transform);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.3f);
            bgRt.anchorMax = new Vector2(1f, 0.7f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgObj.AddComponent<Image>().color = new Color(0.06f, 0.11f, 0.22f, 0.95f);

            GameObject fillArea = CreateUIObject("Fill Area", sliderObj.transform);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.3f);
            faRt.anchorMax = new Vector2(1f, 0.7f);
            faRt.offsetMin = Vector2.zero;
            faRt.offsetMax = Vector2.zero;

            GameObject fillObj = CreateUIObject("Fill", fillArea.transform);
            StretchFull(fillObj.GetComponent<RectTransform>());
            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.32f, 0.86f, 1f, 0.92f);

            GameObject handleArea = CreateUIObject("Handle Slide Area", sliderObj.transform);
            StretchFull(handleArea.GetComponent<RectTransform>());
            GameObject handleObj = CreateUIObject("Handle", handleArea.transform);
            RectTransform hRt = handleObj.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(10f, 12f);
            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = new Color(1f, 0.92f, 0.54f, 1f);

            slider.fillRect = fillObj.GetComponent<RectTransform>();
            slider.handleRect = hRt;
            slider.targetGraphic = handleImg;
            return slider;
        }

        private Text CreateTelemetryChip(
            Transform parent,
            string name,
            Sprite icon,
            string caption,
            string initialVal,
            Font font,
            bool interactive,
            out Image slotBg,
            UnityAction onClick)
        {
            GameObject obj = CreateUIObject(name, parent);
            slotBg = obj.AddComponent<Image>();
            if (_artBank != null && _artBank.itemSlot != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(slotBg, _artBank.itemSlot, new Vector4(10f, 10f, 10f, 10f), Color.white);
            }
            else
            {
                slotBg.color = new Color(0.08f, 0.12f, 0.22f, 0.95f);
            }

            if (interactive && onClick != null)
            {
                Button btn = obj.AddComponent<Button>();
                btn.targetGraphic = slotBg;
                btn.onClick.AddListener(onClick);
            }

            VerticalLayoutGroup vl = obj.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(4, 4, 5, 5);
            vl.spacing = 2f;
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            if (icon != null)
            {
                GameObject icObj = CreateUIObject("Icon", obj.transform);
                icObj.AddComponent<LayoutElement>().preferredHeight = 22f;
                Image icImg = icObj.AddComponent<Image>();
                icImg.sprite = icon;
                icImg.preserveAspect = true;
                icImg.raycastTarget = false;
            }

            Text cap = CreateText("Cap", obj.transform, font, caption, 9, FontStyle.Bold, new Color(0.72f, 0.84f, 0.98f, 1f), TextAnchor.MiddleCenter);
            cap.gameObject.AddComponent<LayoutElement>().preferredHeight = 13f;

            Text val = CreateText("Val", obj.transform, font, initialVal, 11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            val.gameObject.AddComponent<LayoutElement>().preferredHeight = 15f;
            return val;
        }

        private void RedrawTelemetryChart(int currentTickIndex)
        {
            if (_telemetryChartTex == null || _chartPixels == null) return;

            Color32 bg = new Color32(10, 15, 32, 255);
            Color32 grid = new Color32(26, 38, 66, 255);
            for (int y = 0; y < ChartHeight; y++)
            {
                bool isGridY = (y == 20 || y == 41 || y == 62);
                for (int x = 0; x < ChartWidth; x++)
                {
                    bool isGridX = (x % 30 == 0);
                    _chartPixels[y * ChartWidth + x] = (isGridY || isGridX) ? grid : bg;
                }
            }

            if (_historyTicks != null && _historyTicks.Count >= 2)
            {
                int count = _historyTicks.Count;
                float targetSci = _historyGoal != null ? Mathf.Max(1f, _historyGoal.TargetSciencePoints) : 100f;

                Color32 batCol = new Color32(246, 200, 62, 255);
                Color32 sciCol = new Color32(82, 240, 224, 255);
                Color32 hltCol = new Color32(106, 240, 158, 255);
                Color32 radCol = new Color32(226, 114, 250, 255);

                for (int i = 0; i < count - 1; i++)
                {
                    SimulationTickSnapshot a = _historyTicks[i];
                    SimulationTickSnapshot b = _historyTicks[i + 1];
                    int x0 = Mathf.RoundToInt((i / (float)(count - 1)) * (ChartWidth - 1));
                    int x1 = Mathf.RoundToInt(((i + 1) / (float)(count - 1)) * (ChartWidth - 1));

                    DrawChartSegment(x0, NormToY(a.BatteryPercent / 100f), x1, NormToY(b.BatteryPercent / 100f), batCol);
                    DrawChartSegment(x0, NormToY(a.DeliveredScience / targetSci), x1, NormToY(b.DeliveredScience / targetSci), sciCol);
                    DrawChartSegment(x0, NormToY(a.HealthPercent / 100f), x1, NormToY(b.HealthPercent / 100f), hltCol);
                    DrawChartSegment(x0, NormToY(a.RadiationLevel / 100f), x1, NormToY(b.RadiationLevel / 100f), radCol);
                }

                int cursorX = Mathf.Clamp(Mathf.RoundToInt((currentTickIndex / (float)Mathf.Max(1, count - 1)) * (ChartWidth - 1)), 0, ChartWidth - 1);
                Color32 cursorCol = new Color32(255, 255, 255, 210);
                for (int y = 0; y < ChartHeight; y++)
                {
                    _chartPixels[y * ChartWidth + cursorX] = cursorCol;
                }
            }

            _telemetryChartTex.SetPixels32(_chartPixels);
            _telemetryChartTex.Apply(false);
        }

        private static int NormToY(float norm)
        {
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(norm) * (ChartHeight - 6)) + 3, 1, ChartHeight - 2);
        }

        private void DrawChartSegment(int x0, int y0, int x1, int y1, Color32 col)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                if (x0 >= 0 && x0 < ChartWidth && y0 >= 0 && y0 < ChartHeight)
                {
                    _chartPixels[y0 * ChartWidth + x0] = col;
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        private static string FormatOp(ComparisonOp op)
        {
            switch (op)
            {
                case ComparisonOp.LessThan: return "<";
                case ComparisonOp.GreaterThan: return ">";
                default: return "?";
            }
        }

        private void HandleMapBodySelected(SolarSystemBodyCatalog.BodyDefinition body)
        {
            if (body == null) return;

            _inspectedBodyId = body.Id;
            if (_inspectorTitleText != null)
            {
                _inspectorTitleText.text = body.DisplayName.ToUpperInvariant();
                _inspectorTitleText.color = Color.white;
            }
            if (_inspectorMetaText != null)
            {
                _inspectorMetaText.text = string.Format("{0}  |  {1}", body.DistanceLabel, body.PeriodLabel);
            }
            if (_inspectorDescText != null)
            {
                _inspectorDescText.text = body.SummaryText;
            }

            _inspectedFrames = SolarSystemArtBank.GetAnimatedFrames(body.SheetKey, out _inspectedFps);
            if (_inspectorPreviewImg != null && _inspectedFrames != null && _inspectedFrames.Length > 0)
            {
                _inspectorPreviewImg.sprite = _inspectedFrames[0];
            }

            PlaySfx(_artBank != null ? _artBank.uiClick : null, 0.5f);
        }

        private void EnsureAudioSources()
        {
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.playOnAwake = false;
                _musicSource.loop = true;
                _musicSource.spatialBlend = 0f;
            }
            if (_ambienceSource == null)
            {
                _ambienceSource = gameObject.AddComponent<AudioSource>();
                _ambienceSource.playOnAwake = false;
                _ambienceSource.loop = true;
                _ambienceSource.spatialBlend = 0f;
            }
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.spatialBlend = 0f;
            }

            if (_artBank != null)
            {
                if (_musicSource.clip == null && _artBank.flightMusic != null) _musicSource.clip = _artBank.flightMusic;
                if (_ambienceSource.clip == null && _artBank.missionAmbience != null) _ambienceSource.clip = _artBank.missionAmbience;
            }
        }

        private void RefreshAudioPlaybackState()
        {
            if (_musicSource != null)
            {
                _musicSource.volume = _musicEnabled ? _masterVolume * 0.48f : 0f;
                if (_musicEnabled && gameObject.activeInHierarchy && _musicSource.clip != null && !_musicSource.isPlaying)
                {
                    _musicSource.Play();
                }
                else if (!_musicEnabled && _musicSource.isPlaying)
                {
                    _musicSource.Pause();
                }
            }

            if (_ambienceSource != null)
            {
                _ambienceSource.volume = _musicEnabled ? _masterVolume * 0.30f : 0f;
                if (_musicEnabled && gameObject.activeInHierarchy && _ambienceSource.clip != null && !_ambienceSource.isPlaying)
                {
                    _ambienceSource.Play();
                }
                else if (!_musicEnabled && _ambienceSource.isPlaying)
                {
                    _ambienceSource.Pause();
                }
            }
        }

        private void RefreshAudioUI()
        {
            if (_musicToggleLabel != null)
            {
                _musicToggleLabel.text = _musicEnabled ? "MUSIC: ON" : "MUSIC: OFF";
                _musicToggleLabel.color = _musicEnabled ? Color.white : new Color(0.85f, 0.70f, 0.70f, 1f);
            }
            if (_sfxToggleLabel != null)
            {
                _sfxToggleLabel.text = _sfxEnabled ? "SFX: ON" : "SFX: OFF";
                _sfxToggleLabel.color = _sfxEnabled ? Color.white : new Color(0.85f, 0.70f, 0.70f, 1f);
            }
            if (_volumeLabelText != null)
            {
                _volumeLabelText.text = string.Format("{0:0}%", _masterVolume * 100f);
            }
        }

        private void PlaySfx(AudioClip clip, float scale)
        {
            if (!_sfxEnabled || clip == null || _sfxSource == null || !gameObject.activeInHierarchy)
            {
                return;
            }
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(_masterVolume * scale));
        }

        private void HighlightSpeedButton(Button btn, bool active)
        {
            if (btn == null) return;
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = active ? new Color(0.75f, 1.00f, 0.92f, 1f) : new Color(0.82f, 0.86f, 0.94f, 0.85f);
            }
        }

        private static Sprite _solidWhiteSprite;
        private static Sprite CreateSolidWhiteSprite()
        {
            if (_solidWhiteSprite != null) return _solidWhiteSprite;
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _solidWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            return _solidWhiteSprite;
        }

        private Button CreatePixelButton(Transform parent, string name, string label, Color tint, float width, float height, int fontSize, Font font, UnityAction onClick)
        {
            GameObject obj = CreateUIObject(name, parent);
            LayoutElement le = obj.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = width;
            if (height > 0f) le.preferredHeight = height;

            Image img = obj.AddComponent<Image>();
            img.color = tint;
            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;

            if (_artBank != null && _artBank.buttonNormal != null)
            {
                _artBank.ApplyButtonSprites(btn, tint);
            }
            else
            {
                img.color = new Color(0.16f, 0.45f, 0.58f, 1f);
            }

            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            Text txt = CreateText("Label", obj.transform, font, label, fontSize, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            txt.raycastTarget = false;
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, FontStyle style, Color color, TextAnchor alignment)
        {
            GameObject go = CreateUIObject(name, parent);
            Text txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private static void StretchFull(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
