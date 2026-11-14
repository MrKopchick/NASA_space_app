using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AstraLink.Core;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// Popup notification controller for orbital space weather and hardware anomalies (Task S2-04).
    /// Displays <see cref="SimulationTickSnapshot.ActiveEventTitle"/> and event details when an orbital event is active.
    /// </summary>
    public class EventPopupUI : MonoBehaviour
    {
        [Header("Popup UI References (Auto-bound if null)")]
        [SerializeField] private GameObject popupContainer;
        [SerializeField] private Image bannerBackground;
        [SerializeField] private Image alertAccentBar;
        [SerializeField] private Text eventHeaderLabel;
        [SerializeField] private Text eventTitleLabel;
        [SerializeField] private Text eventDescriptionLabel;

        [Header("Visual Settings")]
        [SerializeField] private bool hideWhenNoEvent;
        [SerializeField] private float pulseSpeed = 4.0f;

        private string _currentEventTitle = string.Empty;
        private bool _hasActiveEvent;
        private Color _activeBaseColor = new Color(0.62f, 0.14f, 0.18f, 0.96f);
        private readonly Dictionary<string, SpaceEventDefinition> _knownEvents =
            new Dictionary<string, SpaceEventDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fired whenever a new orbital event starts or clears on a simulation tick.
        /// </summary>
        public event Action<string, bool> OnEventStateChanged;

        /// <summary>
        /// Gets a value indicating whether an orbital anomaly event is currently active on the satellite.
        /// </summary>
        public bool HasActiveEvent => _hasActiveEvent;

        /// <summary>
        /// Gets the title of the currently active orbital event, or an empty string if nominal.
        /// </summary>
        public string CurrentEventTitle => _currentEventTitle;

        private void Awake()
        {
            EnsurePopupHierarchy();
            RegisterDefaultEvents();
            ApplyIdleVisuals();
        }

        private void Update()
        {
            if (_hasActiveEvent && bannerBackground != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
                Color pulsed = Color.Lerp(_activeBaseColor, _activeBaseColor * 1.28f, pulse * 0.45f);
                pulsed.a = 0.96f;
                bannerBackground.color = pulsed;
            }
        }

        /// <summary>
        /// Initializes the event popup with optional catalog event definitions for rich description lookup.
        /// </summary>
        public void Initialize(List<SpaceEventDefinition> eventsCatalog = null)
        {
            EnsurePopupHierarchy();
            RegisterDefaultEvents();

            if (eventsCatalog != null)
            {
                for (int i = 0; i < eventsCatalog.Count; i++)
                {
                    SpaceEventDefinition evt = eventsCatalog[i];
                    if (evt != null && !string.IsNullOrWhiteSpace(evt.Title))
                    {
                        _knownEvents[evt.Title.Trim()] = evt;
                    }
                }
            }

            ClearEvent();
        }

        /// <summary>
        /// Updates the popup state from the current <see cref="SimulationTickSnapshot"/>.
        /// </summary>
        public void UpdateFromSnapshot(SimulationTickSnapshot snapshot)
        {
            if (snapshot == null)
            {
                ClearEvent();
                return;
            }

            UpdateEventBanner(snapshot.ActiveEventTitle, snapshot.OrbitNumber, snapshot.TickIndex, snapshot.RadiationLevel);
        }

        /// <summary>
        /// Displays or clears the active orbital event notification banner.
        /// </summary>
        public void UpdateEventBanner(string activeEventTitle, int orbitNumber = 1, int tickIndex = 0, float radiationLevel = 0f)
        {
            EnsurePopupHierarchy();

            string normalizedTitle = !string.IsNullOrWhiteSpace(activeEventTitle)
                ? activeEventTitle.Trim()
                : string.Empty;

            bool isEventActive = !string.IsNullOrEmpty(normalizedTitle);
            bool stateChanged = (isEventActive != _hasActiveEvent) ||
                                !string.Equals(_currentEventTitle, normalizedTitle, StringComparison.Ordinal);

            _hasActiveEvent = isEventActive;
            _currentEventTitle = normalizedTitle;

            if (!isEventActive)
            {
                ApplyIdleVisuals();
                if (stateChanged)
                {
                    OnEventStateChanged?.Invoke(string.Empty, false);
                }
                return;
            }

            if (popupContainer != null && !popupContainer.activeSelf)
            {
                popupContainer.SetActive(true);
            }

            bool isHighRadiation = radiationLevel > 50.0f ||
                                   normalizedTitle.IndexOf("CME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   normalizedTitle.IndexOf("Coronal", StringComparison.OrdinalIgnoreCase) >= 0;

            _activeBaseColor = isHighRadiation
                ? new Color(0.68f, 0.12f, 0.18f, 0.96f)
                : new Color(0.64f, 0.36f, 0.08f, 0.96f);

            if (bannerBackground != null)
            {
                bannerBackground.color = _activeBaseColor;
            }

            if (alertAccentBar != null)
            {
                alertAccentBar.color = isHighRadiation
                    ? new Color(1.0f, 0.28f, 0.32f, 1.0f)
                    : new Color(1.0f, 0.76f, 0.20f, 1.0f);
            }

            if (eventHeaderLabel != null)
            {
                eventHeaderLabel.text = string.Format(
                    "ALERT  |  ORBIT #{0} (TICK {1:00})  |  SPACE WEATHER / SYSTEM EVENT",
                    Mathf.Max(1, orbitNumber),
                    tickIndex + 1);
                eventHeaderLabel.color = new Color(1.0f, 0.92f, 0.72f, 1.0f);
            }

            if (eventTitleLabel != null)
            {
                eventTitleLabel.text = "ACTIVE EVENT: " + normalizedTitle.ToUpperInvariant();
                eventTitleLabel.color = Color.white;
            }

            if (eventDescriptionLabel != null)
            {
                eventDescriptionLabel.text = ResolveEventDescription(normalizedTitle, radiationLevel);
                eventDescriptionLabel.color = new Color(0.96f, 0.92f, 0.92f, 0.96f);
            }

            if (stateChanged)
            {
                OnEventStateChanged?.Invoke(normalizedTitle, true);
            }
        }

        /// <summary>
        /// Resets the popup banner to the nominal (no active event) state.
        /// </summary>
        public void ClearEvent()
        {
            _hasActiveEvent = false;
            _currentEventTitle = string.Empty;
            ApplyIdleVisuals();
        }

        private void ApplyIdleVisuals()
        {
            if (hideWhenNoEvent && popupContainer != null)
            {
                popupContainer.SetActive(false);
                return;
            }

            if (popupContainer != null && !popupContainer.activeSelf)
            {
                popupContainer.SetActive(true);
            }

            if (bannerBackground != null)
            {
                bannerBackground.color = new Color(0.07f, 0.14f, 0.22f, 0.92f);
            }

            if (alertAccentBar != null)
            {
                alertAccentBar.color = new Color(0.16f, 0.68f, 0.54f, 0.95f);
            }

            if (eventHeaderLabel != null)
            {
                eventHeaderLabel.text = "SPACE ENVIRONMENT MONITOR  |  STATUS: NOMINAL";
                eventHeaderLabel.color = new Color(0.56f, 0.92f, 0.80f, 1.0f);
            }

            if (eventTitleLabel != null)
            {
                eventTitleLabel.text = "NO ACTIVE ORBITAL ANOMALIES";
                eventTitleLabel.color = new Color(0.84f, 0.94f, 1.0f, 1.0f);
            }

            if (eventDescriptionLabel != null)
            {
                eventDescriptionLabel.text = "Solar flux, radiation levels, and transceiver temperatures are within nominal parameters.";
                eventDescriptionLabel.color = new Color(0.68f, 0.80f, 0.92f, 0.92f);
            }
        }

        private string ResolveEventDescription(string eventTitle, float radiationLevel)
        {
            SpaceEventDefinition def;
            if (_knownEvents.TryGetValue(eventTitle, out def) && def != null && !string.IsNullOrWhiteSpace(def.Description))
            {
                return string.Format(
                    "{0}  (Solar x{1:0.00} | Downlink x{2:0.00} | Rad +{3:0} -> {4:0} rad)",
                    def.Description,
                    def.SolarMultiplier,
                    def.DownlinkMultiplier,
                    def.RadiationBoost,
                    radiationLevel);
            }

            if (eventTitle.IndexOf("CME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                eventTitle.IndexOf("Coronal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return string.Format(
                    "Coronal Mass Ejection (+85 rad, current {0:0} rad). Enable Safe Mode immediately to prevent electronics damage!",
                    radiationLevel);
            }

            if (eventTitle.IndexOf("Overheat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Transmitter amplifier overheating: downlink bandwidth reduced to 45% for 8 ticks.";
            }

            if (eventTitle.IndexOf("Micrometeor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Micrometeoroid stream: solar output reduced to 60% and background radiation increased by +15 rad.";
            }

            return string.Format("Active orbital event affecting spacecraft subsystems (Radiation: {0:0.0} rad).", radiationLevel);
        }

        private void RegisterDefaultEvents()
        {
            if (_knownEvents.Count > 0) return;

            _knownEvents["Coronal Mass Ejection (CME)"] = new SpaceEventDefinition
            {
                Id = "evt_cme_storm",
                Title = "Coronal Mass Ejection (CME)",
                Description = "Powerful solar storm raises radiation by +85 rad. Unprotected electronics take damage without Safe Mode.",
                TriggerOrbit = 3,
                DurationTicks = 12,
                RadiationBoost = 85.0f,
                SolarMultiplier = 1.0f,
                DownlinkMultiplier = 1.0f
            };

            _knownEvents["Transmitter Overheating"] = new SpaceEventDefinition
            {
                Id = "evt_tx_overheat",
                Title = "Transmitter Overheating",
                Description = "Transceiver amplifier overheats, reducing downlink rate to 45% for 8 ticks.",
                TriggerOrbit = 2,
                DurationTicks = 8,
                RadiationBoost = 0.0f,
                SolarMultiplier = 1.0f,
                DownlinkMultiplier = 0.45f
            };

            _knownEvents["Micrometeoroid Interference"] = new SpaceEventDefinition
            {
                Id = "evt_micrometeorite",
                Title = "Micrometeoroid Interference",
                Description = "Micrometeoroid flux reduces solar generation to 60% and raises radiation by +15 rad.",
                TriggerOrbit = 3,
                DurationTicks = 10,
                RadiationBoost = 15.0f,
                SolarMultiplier = 0.6f,
                DownlinkMultiplier = 1.0f
            };
        }

        /// <summary>
        /// Builds or binds the UI hierarchy for the event popup notification banner.
        /// </summary>
        public void EnsurePopupHierarchy()
        {
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
            {
                rootRt = gameObject.AddComponent<RectTransform>();
            }

            if (popupContainer == null)
            {
                popupContainer = gameObject;
            }

            if (bannerBackground == null)
            {
                bannerBackground = GetComponent<Image>();
                if (bannerBackground == null)
                {
                    bannerBackground = gameObject.AddComponent<Image>();
                }
                bannerBackground.color = new Color(0.07f, 0.14f, 0.22f, 0.92f);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (alertAccentBar == null)
            {
                Transform existingBar = rootRt.Find("AlertAccentBar");
                if (existingBar != null)
                {
                    alertAccentBar = existingBar.GetComponent<Image>();
                }
                else
                {
                    var barObj = new GameObject("AlertAccentBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    barObj.transform.SetParent(rootRt, false);
                    var brt = barObj.GetComponent<RectTransform>();
                    brt.anchorMin = new Vector2(0.0f, 0.0f);
                    brt.anchorMax = new Vector2(0.015f, 1.0f);
                    brt.offsetMin = Vector2.zero;
                    brt.offsetMax = Vector2.zero;
                    alertAccentBar = barObj.GetComponent<Image>();
                    alertAccentBar.color = new Color(0.16f, 0.68f, 0.54f, 0.95f);
                }
            }

            if (eventHeaderLabel == null)
            {
                eventHeaderLabel = FindOrCreateText(
                    rootRt,
                    "EventHeaderLabel",
                    new Vector2(0.03f, 0.68f),
                    new Vector2(0.98f, 0.96f),
                    "SPACE ENVIRONMENT MONITOR  |  STATUS: NOMINAL",
                    12,
                    FontStyle.Bold,
                    new Color(0.56f, 0.92f, 0.80f, 1.0f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (eventTitleLabel == null)
            {
                eventTitleLabel = FindOrCreateText(
                    rootRt,
                    "EventTitleLabel",
                    new Vector2(0.03f, 0.36f),
                    new Vector2(0.98f, 0.70f),
                    "NO ACTIVE ORBITAL ANOMALIES",
                    15,
                    FontStyle.Bold,
                    new Color(0.84f, 0.94f, 1.0f, 1.0f),
                    TextAnchor.MiddleLeft,
                    font);
            }

            if (eventDescriptionLabel == null)
            {
                eventDescriptionLabel = FindOrCreateText(
                    rootRt,
                    "EventDescriptionLabel",
                    new Vector2(0.03f, 0.04f),
                    new Vector2(0.98f, 0.38f),
                    "Solar flux, radiation levels, and transceiver temperatures are within nominal parameters.",
                    12,
                    FontStyle.Normal,
                    new Color(0.68f, 0.80f, 0.92f, 0.92f),
                    TextAnchor.MiddleLeft,
                    font);
            }
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
