using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using AstraLink.Core;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// Manual Command Override &amp; Signal Delay Countdown UI Controller (Task S2-04).
    /// Provides 3 manual command buttons (Toggle Instruments, Toggle Comms, Enable Safe Mode),
    /// visualizes the radio signal propagation delay based on <see cref="OrbitDefinition.SignalDelaySeconds"/>,
    /// appends <see cref="ManualCommandRequest"/> items to <c>activeManualCommands</c>,
    /// and triggers immediate deterministic flight recalculation via <see cref="IMissionSimulator.SimulateFullMission"/>.
    /// </summary>
    public class ManualOverrideUI : MonoBehaviour
    {
        [Serializable]
        private class PendingSignalVisual
        {
            public ManualCommandRequest Request;
            public float TotalDelaySeconds;
            public float RemainingSeconds;
            public bool Executed;
        }

        [Header("Manual Command Buttons (Auto-bound if null)")]
        [SerializeField] private Button toggleScienceButton;
        [SerializeField] private Text toggleScienceButtonLabel;
        [SerializeField] private Button toggleCommsButton;
        [SerializeField] private Text toggleCommsButtonLabel;
        [SerializeField] private Button toggleSafeModeButton;
        [SerializeField] private Text toggleSafeModeButtonLabel;

        [Header("Signal Delay Visual Timer (Auto-bound if null)")]
        [SerializeField] private Text orbitDelayInfoLabel;
        [SerializeField] private Slider signalDelaySlider;
        [SerializeField] private Image signalDelayFillImage;
        [SerializeField] private Text signalDelayTimerLabel;
        [SerializeField] private Text commandQueueLogLabel;

        private OrbitDefinition _currentOrbit;
        private int _currentTickIndex;
        private bool _currentScienceActive = true;
        private bool _currentCommsActive = true;
        private bool _currentSafeModeActive;
        private float _playbackSpeedMultiplier = 1.0f;

        private List<ManualCommandRequest> _activeManualCommands = new List<ManualCommandRequest>();
        private readonly List<PendingSignalVisual> _pendingVisuals = new List<PendingSignalVisual>();

        private static Sprite _solidSprite;

        /// <summary>
        /// Fired when the player issues a manual command; passes the new <see cref="ManualCommandRequest"/>
        /// and the full <c>activeManualCommands</c> list for <see cref="IMissionSimulator.SimulateFullMission"/> recalculation.
        /// </summary>
        public event Action<ManualCommandRequest, List<ManualCommandRequest>> OnManualCommandIssued;

        /// <summary>
        /// Gets the active list of manual commands issued during the current mission playback.
        /// </summary>
        public List<ManualCommandRequest> ActiveManualCommands => _activeManualCommands;

        private void Awake()
        {
            EnsureManualOverrideHierarchy();
            BindButtons();
        }

        private void Update()
        {
            UpdateRealtimeSignalCountdown(Time.deltaTime);
        }

        /// <summary>
        /// Initializes the manual override panel with the target orbit and shared <c>activeManualCommands</c> list.
        /// </summary>
        public void Initialize(
            OrbitDefinition orbit,
            List<ManualCommandRequest> sharedCommandList = null)
        {
            EnsureManualOverrideHierarchy();
            BindButtons();

            _currentOrbit = orbit;
            _activeManualCommands = sharedCommandList ?? new List<ManualCommandRequest>();
            _pendingVisuals.Clear();
            _currentTickIndex = 0;

            RefreshOrbitDelayHeader();
            RefreshButtonLabels();
            RefreshSignalTimerDisplay();
        }

        /// <summary>
        /// Updates the target orbit used to compute <see cref="OrbitDefinition.SignalDelaySeconds"/>.
        /// </summary>
        public void SetOrbit(OrbitDefinition orbit)
        {
            _currentOrbit = orbit;
            RefreshOrbitDelayHeader();
        }

        /// <summary>
        /// Sets the active simulation speed multiplier so the visual signal delay countdown matches playback speed.
        /// </summary>
        public void SetPlaybackSpeed(float speedMultiplier)
        {
            _playbackSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.25f, 20.0f);
        }

        /// <summary>
        /// Clears all queued manual commands and resets the signal delay timer (e.g. when restarting a flight).
        /// </summary>
        public void ResetCommands()
        {
            if (_activeManualCommands != null)
            {
                _activeManualCommands.Clear();
            }
            _pendingVisuals.Clear();
            _currentTickIndex = 0;
            _currentScienceActive = true;
            _currentCommsActive = true;
            _currentSafeModeActive = false;

            RefreshButtonLabels();
            RefreshSignalTimerDisplay();
        }

        /// <summary>
        /// Synchronizes the manual command buttons and command queue status with the current <see cref="SimulationTickSnapshot"/>.
        /// </summary>
        public void UpdateFromSnapshot(SimulationTickSnapshot snapshot, int tickIndex)
        {
            if (snapshot == null) return;
            EnsureManualOverrideHierarchy();

            _currentTickIndex = tickIndex;
            _currentScienceActive = snapshot.ScienceActive;
            _currentCommsActive = snapshot.CommsActive;
            _currentSafeModeActive = snapshot.SafeModeActive;

            for (int i = 0; i < _pendingVisuals.Count; i++)
            {
                PendingSignalVisual vis = _pendingVisuals[i];
                if (vis != null && vis.Request != null && tickIndex >= vis.Request.ExecuteAtTick)
                {
                    vis.RemainingSeconds = 0.0f;
                    vis.Executed = true;
                }
            }

            RefreshButtonLabels();
            RefreshSignalTimerDisplay();
        }

        /// <summary>
        /// Issues a manual command to toggle science payload instruments (<see cref="AutopilotAction.ScienceOn"/> / <see cref="AutopilotAction.ScienceOff"/>).
        /// </summary>
        public void IssueToggleScienceCommand()
        {
            AutopilotAction nextAction = _currentScienceActive
                ? AutopilotAction.ScienceOff
                : AutopilotAction.ScienceOn;
            IssueManualCommand(nextAction);
        }

        /// <summary>
        /// Issues a manual command to toggle communications transceiver (<see cref="AutopilotAction.CommsOn"/> / <see cref="AutopilotAction.CommsOff"/>).
        /// </summary>
        public void IssueToggleCommsCommand()
        {
            AutopilotAction nextAction = _currentCommsActive
                ? AutopilotAction.CommsOff
                : AutopilotAction.CommsOn;
            IssueManualCommand(nextAction);
        }

        /// <summary>
        /// Issues a manual command to enable or toggle Safe Mode (<see cref="AutopilotAction.SafeModeOn"/> / <see cref="AutopilotAction.SafeModeOff"/>).
        /// </summary>
        public void IssueToggleSafeModeCommand()
        {
            AutopilotAction nextAction = _currentSafeModeActive
                ? AutopilotAction.SafeModeOff
                : AutopilotAction.SafeModeOn;
            IssueManualCommand(nextAction);
        }

        /// <summary>
        /// Creates a <see cref="ManualCommandRequest"/> with signal delay based on <see cref="OrbitDefinition.SignalDelaySeconds"/>,
        /// appends it to <see cref="ActiveManualCommands"/>, starts the visual countdown timer, and triggers flight recalculation.
        /// </summary>
        public ManualCommandRequest IssueManualCommand(AutopilotAction action)
        {
            if (action == AutopilotAction.None) return null;

            float delaySeconds = (_currentOrbit != null && _currentOrbit.SignalDelaySeconds >= 0.0f)
                ? _currentOrbit.SignalDelaySeconds
                : 1.0f;

            int delayTicks = CalculateDelayTicks(delaySeconds);

            var request = new ManualCommandRequest
            {
                IssuedAtTick = _currentTickIndex,
                ExecuteAtTick = _currentTickIndex + delayTicks,
                Action = action
            };

            if (_activeManualCommands == null)
            {
                _activeManualCommands = new List<ManualCommandRequest>();
            }
            _activeManualCommands.Add(request);

            _pendingVisuals.Add(new PendingSignalVisual
            {
                Request = request,
                TotalDelaySeconds = Mathf.Max(0.1f, delaySeconds),
                RemainingSeconds = delaySeconds,
                Executed = (delayTicks == 0)
            });

            RefreshSignalTimerDisplay();

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[ManualOverrideUI] Queued manual command '{0}' at Tick {1} -> ExecuteAtTick {2} (SignalDelay: {3:0.0}s).",
                action,
                request.IssuedAtTick,
                request.ExecuteAtTick,
                delaySeconds));

            OnManualCommandIssued?.Invoke(request, _activeManualCommands);
            return request;
        }

        /// <summary>
        /// Calculates the number of simulation ticks corresponding to the radio signal delay in seconds (<c>Ceil(SignalDelaySeconds)</c>).
        /// </summary>
        public static int CalculateDelayTicks(float signalDelaySeconds)
        {
            if (signalDelaySeconds <= 0.0f) return 0;
            return (int)Math.Ceiling(signalDelaySeconds);
        }

        private void UpdateRealtimeSignalCountdown(float deltaTime)
        {
            if (_pendingVisuals.Count == 0) return;

            bool anyActive = false;
            float effectiveDelta = deltaTime * Mathf.Max(0.1f, _playbackSpeedMultiplier);

            for (int i = 0; i < _pendingVisuals.Count; i++)
            {
                PendingSignalVisual vis = _pendingVisuals[i];
                if (vis == null || vis.Executed) continue;

                anyActive = true;
                vis.RemainingSeconds = Mathf.Max(0.0f, vis.RemainingSeconds - effectiveDelta);
                if (vis.RemainingSeconds <= 0.001f && _currentTickIndex >= vis.Request.ExecuteAtTick)
                {
                    vis.RemainingSeconds = 0.0f;
                    vis.Executed = true;
                }
            }

            if (anyActive)
            {
                RefreshSignalTimerDisplay();
            }
        }

        private void RefreshOrbitDelayHeader()
        {
            if (orbitDelayInfoLabel == null) return;

            float delaySec = _currentOrbit != null ? _currentOrbit.SignalDelaySeconds : 1.0f;
            int delayTicks = CalculateDelayTicks(delaySec);
            string orbitName = _currentOrbit != null ? _currentOrbit.Name : "Low Earth Orbit";

            orbitDelayInfoLabel.text = string.Format(
                CultureInfo.InvariantCulture,
                "MANUAL TELECOMMAND OVERRIDE   |   Orbit: {0}   |   Signal Delay: {1:0.#}s ({2} ticks)",
                orbitName,
                delaySec,
                delayTicks);
        }

        private void RefreshButtonLabels()
        {
            if (toggleScienceButtonLabel != null)
            {
                toggleScienceButtonLabel.text = _currentScienceActive
                    ? "1. Toggle Instruments\n[Now: ON -> Turn OFF]"
                    : "1. Toggle Instruments\n[Now: OFF -> Turn ON]";
            }
            if (toggleScienceButton != null)
            {
                Image img = toggleScienceButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = _currentScienceActive
                        ? new Color(0.14f, 0.44f, 0.72f, 1.0f)
                        : new Color(0.22f, 0.28f, 0.38f, 1.0f);
                }
            }

            if (toggleCommsButtonLabel != null)
            {
                toggleCommsButtonLabel.text = _currentCommsActive
                    ? "2. Toggle Comms\n[Now: ON -> Turn OFF]"
                    : "2. Toggle Comms\n[Now: OFF -> Turn ON]";
            }
            if (toggleCommsButton != null)
            {
                Image img = toggleCommsButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = _currentCommsActive
                        ? new Color(0.10f, 0.54f, 0.48f, 1.0f)
                        : new Color(0.22f, 0.28f, 0.38f, 1.0f);
                }
            }

            if (toggleSafeModeButtonLabel != null)
            {
                toggleSafeModeButtonLabel.text = _currentSafeModeActive
                    ? "3. Safe Mode [ACTIVE]\n[Click to Exit Safe Mode]"
                    : "3. Enable Safe Mode\n[Emergency Shielding ON]";
            }
            if (toggleSafeModeButton != null)
            {
                Image img = toggleSafeModeButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = _currentSafeModeActive
                        ? new Color(0.76f, 0.42f, 0.10f, 1.0f)
                        : new Color(0.68f, 0.18f, 0.22f, 1.0f);
                }
            }
        }

        private void RefreshSignalTimerDisplay()
        {
            PendingSignalVisual activePending = null;
            for (int i = _pendingVisuals.Count - 1; i >= 0; i--)
            {
                if (!_pendingVisuals[i].Executed)
                {
                    activePending = _pendingVisuals[i];
                    break;
                }
            }

            if (activePending != null && activePending.Request != null)
            {
                float progress = 1.0f - Mathf.Clamp01(activePending.RemainingSeconds / Mathf.Max(0.1f, activePending.TotalDelaySeconds));
                int remainingTicks = Mathf.Max(0, activePending.Request.ExecuteAtTick - _currentTickIndex);

                SetSignalBar(progress, new Color(0.98f, 0.68f, 0.16f, 1.0f));

                if (signalDelayTimerLabel != null)
                {
                    signalDelayTimerLabel.text = string.Format(
                        CultureInfo.InvariantCulture,
                        "SIGNAL IN TRANSIT: '{0}'  |  Delay Remaining: {1:0.0}s ({2} ticks)  ->  Executes at Tick #{3}",
                        activePending.Request.Action,
                        activePending.RemainingSeconds,
                        remainingTicks,
                        activePending.Request.ExecuteAtTick + 1);
                    signalDelayTimerLabel.color = new Color(1.0f, 0.88f, 0.42f, 1.0f);
                }
            }
            else if (_pendingVisuals.Count > 0)
            {
                PendingSignalVisual lastExecuted = _pendingVisuals[_pendingVisuals.Count - 1];
                SetSignalBar(1.0f, new Color(0.18f, 0.82f, 0.46f, 1.0f));

                if (signalDelayTimerLabel != null && lastExecuted.Request != null)
                {
                    signalDelayTimerLabel.text = string.Format(
                        CultureInfo.InvariantCulture,
                        "COMMAND DELIVERED: '{0}' executed on spacecraft at Tick #{1} (Full flight recalculated).",
                        lastExecuted.Request.Action,
                        lastExecuted.Request.ExecuteAtTick + 1);
                    signalDelayTimerLabel.color = new Color(0.52f, 1.0f, 0.72f, 1.0f);
                }
            }
            else
            {
                SetSignalBar(0.0f, new Color(0.20f, 0.34f, 0.52f, 1.0f));

                if (signalDelayTimerLabel != null)
                {
                    float delaySec = _currentOrbit != null ? _currentOrbit.SignalDelaySeconds : 1.0f;
                    signalDelayTimerLabel.text = string.Format(
                        CultureInfo.InvariantCulture,
                        "Signal Channel Ready (Radio Propagation Delay: {0:0.#}s). Click a button above to transmit command.",
                        delaySec);
                    signalDelayTimerLabel.color = new Color(0.72f, 0.84f, 0.96f, 0.92f);
                }
            }

            if (commandQueueLogLabel != null)
            {
                if (_activeManualCommands == null || _activeManualCommands.Count == 0)
                {
                    commandQueueLogLabel.text = "Manual Command Queue: [0 commands issued]";
                }
                else
                {
                    ManualCommandRequest latest = _activeManualCommands[_activeManualCommands.Count - 1];
                    commandQueueLogLabel.text = string.Format(
                        CultureInfo.InvariantCulture,
                        "Manual Command Queue ({0} total)  |  Latest: {1} (Issued Tick #{2} -> Execute Tick #{3})",
                        _activeManualCommands.Count,
                        latest.Action,
                        latest.IssuedAtTick + 1,
                        latest.ExecuteAtTick + 1);
                }
            }
        }

        private void SetSignalBar(float normalizedProgress, Color color)
        {
            float clamped = Mathf.Clamp01(normalizedProgress);
            if (signalDelaySlider != null)
            {
                signalDelaySlider.normalizedValue = clamped;
            }

            if (signalDelayFillImage != null)
            {
                if (signalDelayFillImage.sprite == null)
                {
                    signalDelayFillImage.sprite = GetSolidSprite();
                }
                signalDelayFillImage.type = Image.Type.Filled;
                signalDelayFillImage.fillMethod = Image.FillMethod.Horizontal;
                signalDelayFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                signalDelayFillImage.fillAmount = clamped;
                signalDelayFillImage.color = color;
            }
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                name = "Procedural_SignalDelayBar",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            _solidSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            return _solidSprite;
        }

        private void BindButtons()
        {
            if (toggleScienceButton != null)
            {
                toggleScienceButton.onClick.RemoveAllListeners();
                toggleScienceButton.onClick.AddListener(IssueToggleScienceCommand);
            }

            if (toggleCommsButton != null)
            {
                toggleCommsButton.onClick.RemoveAllListeners();
                toggleCommsButton.onClick.AddListener(IssueToggleCommsCommand);
            }

            if (toggleSafeModeButton != null)
            {
                toggleSafeModeButton.onClick.RemoveAllListeners();
                toggleSafeModeButton.onClick.AddListener(IssueToggleSafeModeCommand);
            }
        }

        /// <summary>
        /// Builds or binds the UI hierarchy for the 3 manual command buttons and the signal delay visual timer.
        /// </summary>
        public void EnsureManualOverrideHierarchy()
        {
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
            {
                rootRt = gameObject.AddComponent<RectTransform>();
            }

            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.09f, 0.16f, 0.96f);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (orbitDelayInfoLabel == null)
            {
                orbitDelayInfoLabel = FindOrCreateText(
                    rootRt,
                    "OrbitDelayInfoLabel",
                    new Vector2(0.03f, 0.78f),
                    new Vector2(0.97f, 0.96f),
                    "MANUAL TELECOMMAND OVERRIDE   |   Signal Delay: 1.0s (1 tick)",
                    14,
                    FontStyle.Bold,
                    new Color(0.88f, 0.95f, 1.0f, 1.0f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            // 3 Manual Command Buttons Row
            if (toggleScienceButton == null)
            {
                toggleScienceButton = CreateCommandButton(
                    rootRt,
                    "Btn_ToggleInstruments",
                    "1. Toggle Instruments\n[Now: ON -> Turn OFF]",
                    new Vector2(0.03f, 0.38f),
                    new Vector2(0.33f, 0.75f),
                    new Color(0.14f, 0.44f, 0.72f, 1.0f),
                    font,
                    out toggleScienceButtonLabel);
            }

            if (toggleCommsButton == null)
            {
                toggleCommsButton = CreateCommandButton(
                    rootRt,
                    "Btn_ToggleComms",
                    "2. Toggle Comms\n[Now: ON -> Turn OFF]",
                    new Vector2(0.35f, 0.38f),
                    new Vector2(0.65f, 0.75f),
                    new Color(0.10f, 0.54f, 0.48f, 1.0f),
                    font,
                    out toggleCommsButtonLabel);
            }

            if (toggleSafeModeButton == null)
            {
                toggleSafeModeButton = CreateCommandButton(
                    rootRt,
                    "Btn_EnableSafeMode",
                    "3. Enable Safe Mode\n[Emergency Shielding ON]",
                    new Vector2(0.67f, 0.38f),
                    new Vector2(0.97f, 0.75f),
                    new Color(0.68f, 0.18f, 0.22f, 1.0f),
                    font,
                    out toggleSafeModeButtonLabel);
            }

            // Signal Delay Countdown Bar (Slider + Fill Image)
            Transform existingTrack = rootRt.Find("SignalDelayTrack");
            RectTransform trackRt;
            if (existingTrack != null)
            {
                trackRt = existingTrack.GetComponent<RectTransform>();
                signalDelaySlider = existingTrack.GetComponent<Slider>();
            }
            else
            {
                var trackObj = new GameObject("SignalDelayTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
                trackObj.transform.SetParent(rootRt, false);
                trackRt = trackObj.GetComponent<RectTransform>();
                trackRt.anchorMin = new Vector2(0.03f, 0.22f);
                trackRt.anchorMax = new Vector2(0.97f, 0.33f);
                trackRt.offsetMin = Vector2.zero;
                trackRt.offsetMax = Vector2.zero;
                trackObj.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.96f);

                signalDelaySlider = trackObj.GetComponent<Slider>();
                signalDelaySlider.interactable = false;
                signalDelaySlider.transition = Selectable.Transition.None;
                signalDelaySlider.minValue = 0f;
                signalDelaySlider.maxValue = 1f;
                signalDelaySlider.value = 0f;
            }

            if (signalDelayFillImage == null)
            {
                Transform existingFill = trackRt.Find("Fill");
                if (existingFill != null)
                {
                    signalDelayFillImage = existingFill.GetComponent<Image>();
                }
                else
                {
                    var fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    fillObj.transform.SetParent(trackRt, false);
                    var fillRt = fillObj.GetComponent<RectTransform>();
                    fillRt.anchorMin = Vector2.zero;
                    fillRt.anchorMax = Vector2.one;
                    fillRt.offsetMin = Vector2.zero;
                    fillRt.offsetMax = Vector2.zero;

                    signalDelayFillImage = fillObj.GetComponent<Image>();
                    signalDelayFillImage.sprite = GetSolidSprite();
                    signalDelayFillImage.type = Image.Type.Filled;
                    signalDelayFillImage.fillMethod = Image.FillMethod.Horizontal;
                    signalDelayFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                    signalDelayFillImage.fillAmount = 0.0f;
                    signalDelayFillImage.color = new Color(0.98f, 0.68f, 0.16f, 1.0f);
                }
            }

            if (signalDelayTimerLabel == null)
            {
                signalDelayTimerLabel = FindOrCreateText(
                    rootRt,
                    "SignalDelayTimerLabel",
                    new Vector2(0.03f, 0.11f),
                    new Vector2(0.97f, 0.22f),
                    "Signal Channel Ready (Radio Propagation Delay: 1.0s).",
                    12,
                    FontStyle.Bold,
                    new Color(0.72f, 0.84f, 0.96f, 0.92f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (commandQueueLogLabel == null)
            {
                commandQueueLogLabel = FindOrCreateText(
                    rootRt,
                    "CommandQueueLogLabel",
                    new Vector2(0.03f, 0.02f),
                    new Vector2(0.97f, 0.11f),
                    "Manual Command Queue: [0 commands issued]",
                    11,
                    FontStyle.Normal,
                    new Color(0.58f, 0.72f, 0.86f, 0.88f),
                    TextAnchor.MiddleLeft,
                    font);
            }
        }

        private static Button CreateCommandButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color bgColor,
            Font font,
            out Text buttonLabel)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                buttonLabel = existing.GetComponentInChildren<Text>();
                return existing.GetComponent<Button>();
            }

            var btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;

            buttonLabel = FindOrCreateText(
                rt,
                "Label",
                new Vector2(0.04f, 0.06f),
                new Vector2(0.96f, 0.94f),
                label,
                13,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter,
                font);

            return btn;
        }

        private static Text FindOrCreateText(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string text,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment,
            Font font)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Text existingTxt = existing.GetComponent<Text>();
                if (existingTxt != null) return existingTxt;
            }

            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var txt = obj.GetComponent<Text>();
            txt.font = font;
            txt.text = text;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;
            return txt;
        }
    }
}
