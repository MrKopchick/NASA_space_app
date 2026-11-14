using System;
using System.Globalization;
using AstraLink.Core;
using AstraLink.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Flight
{
    /// <summary>
    /// Popup notification banner for orbital anomalies and space weather events during flight playback (Task S2-04).
    /// Monitors <see cref="SimulationTickSnapshot.ActiveEventTitle"/> on each simulation tick and displays
    /// a high-visibility alert when an event (e.g., Solar Flare CME, Transmitter Overheat, Micrometeoroid Noise) is active.
    /// </summary>
    public class EventPopupUI : MonoBehaviour
    {
        [Header("Event Popup UI References (Auto-bound if null)")]
        [SerializeField] private GameObject popupBannerRoot;
        [SerializeField] private Image bannerBackground;
        [SerializeField] private Image alertBadgeBackground;
        [SerializeField] private Image eventIconImage;
        [SerializeField] private Text alertBadgeText;
        [SerializeField] private Text activeEventTitleText;
        [SerializeField] private Text eventImpactDescriptionText;
        [SerializeField] private bool hideWhenNoActiveEvent = false;

        private string _currentEventTitle = string.Empty;

        /// <summary>
        /// Gets the currently displayed <see cref="SimulationTickSnapshot.ActiveEventTitle"/>, or empty string when nominal.
        /// </summary>
        public string CurrentEventTitle => _currentEventTitle;

        /// <summary>
        /// Gets a value indicating whether an orbital anomaly or space weather event is currently active on this tick.
        /// </summary>
        public bool HasActiveEvent => !string.IsNullOrWhiteSpace(_currentEventTitle);

        /// <summary>
        /// Fired when a new orbital event starts or clears during flight playback.
        /// </summary>
        public event Action<string, bool> OnEventStateChanged;

        private void Awake()
        {
            EnsurePopupHierarchy();
        }

        /// <summary>
        /// Updates the popup notification state from the current <see cref="SimulationTickSnapshot"/>.
        /// </summary>
        public void UpdateFromTick(SimulationTickSnapshot snapshot)
        {
            EnsurePopupHierarchy();

            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ActiveEventTitle))
            {
                SetNominalState(snapshot != null ? snapshot.OrbitNumber : 1, snapshot != null ? snapshot.TickIndex : 0);
                return;
            }

            ShowEvent(
                snapshot.ActiveEventTitle,
                snapshot.OrbitNumber,
                snapshot.TickIndex,
                snapshot.RadiationLevel,
                snapshot.SafeModeActive);
        }

        /// <summary>
        /// Displays an active orbital anomaly notification with severity styling and telemetry impact details.
        /// </summary>
        public void ShowEvent(
            string activeEventTitle,
            int orbitNumber = 1,
            int tickIndex = 0,
            float radiationLevel = 0.0f,
            bool safeModeActive = false)
        {
            EnsurePopupHierarchy();

            string cleanTitle = string.IsNullOrWhiteSpace(activeEventTitle) ? string.Empty : activeEventTitle.Trim();
            if (string.IsNullOrEmpty(cleanTitle))
            {
                SetNominalState(orbitNumber, tickIndex);
                return;
            }

            bool wasChanged = !string.Equals(_currentEventTitle, cleanTitle, StringComparison.Ordinal);
            _currentEventTitle = cleanTitle;

            if (popupBannerRoot != null)
            {
                popupBannerRoot.SetActive(true);
            }

            bool isRadiationStorm = cleanTitle.IndexOf("CME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cleanTitle.IndexOf("Solar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cleanTitle.IndexOf("Storm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cleanTitle.IndexOf("Flare", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    radiationLevel > 50.0f;

            if (bannerBackground != null)
            {
                bannerBackground.color = isRadiationStorm
                    ? new Color(1.0f, 0.72f, 0.72f, 0.98f)
                    : new Color(1.0f, 0.88f, 0.64f, 0.98f);
            }

            if (alertBadgeBackground != null)
            {
                alertBadgeBackground.color = isRadiationStorm
                    ? UITheme.ButtonTintDanger
                    : UITheme.ButtonTintGold;
            }

            if (eventIconImage != null)
            {
                Sprite hazardSprite = UITheme.GetHazardSprite(cleanTitle);
                eventIconImage.sprite = hazardSprite != null
                    ? hazardSprite
                    : (UITheme.Assets != null ? (isRadiationStorm ? UITheme.Assets.astraError : UITheme.Assets.astraWarn) : null);
                eventIconImage.color = Color.white;
            }

            if (alertBadgeText != null)
            {
                alertBadgeText.text = isRadiationStorm ? "HAZARD ALERT" : "EVENT ACTIVE";
            }

            if (activeEventTitleText != null)
            {
                activeEventTitleText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "ACTIVE EVENT: {0}  (Orbit #{1}, Tick #{2})",
                    cleanTitle.ToUpperInvariant(),
                    orbitNumber,
                    tickIndex + 1);
                activeEventTitleText.color = new Color(1.0f, 0.92f, 0.68f, 1.0f);
            }

            if (eventImpactDescriptionText != null)
            {
                eventImpactDescriptionText.text = DescribeEventImpact(cleanTitle, radiationLevel, safeModeActive);
                eventImpactDescriptionText.color = new Color(0.96f, 0.88f, 0.86f, 1.0f);
            }

            if (wasChanged)
            {
                OnEventStateChanged?.Invoke(_currentEventTitle, true);
            }
        }

        /// <summary>
        /// Resets the popup banner to its calm nominal state (or hides it if <see cref="hideWhenNoActiveEvent"/> is enabled).
        /// </summary>
        public void SetNominalState(int orbitNumber = 1, int tickIndex = 0)
        {
            EnsurePopupHierarchy();

            bool hadEvent = !string.IsNullOrEmpty(_currentEventTitle);
            _currentEventTitle = string.Empty;

            if (hideWhenNoActiveEvent && popupBannerRoot != null)
            {
                popupBannerRoot.SetActive(false);
            }
            else if (popupBannerRoot != null)
            {
                popupBannerRoot.SetActive(true);
            }

            if (bannerBackground != null)
            {
                bannerBackground.color = Color.white;
            }

            if (alertBadgeBackground != null)
            {
                alertBadgeBackground.color = UITheme.ButtonTintSuccess;
            }

            if (eventIconImage != null && UITheme.Assets != null)
            {
                eventIconImage.sprite = UITheme.Assets.astraNormal;
                eventIconImage.color = Color.white;
            }

            if (alertBadgeText != null)
            {
                alertBadgeText.text = "NOMINAL";
            }

            if (activeEventTitleText != null)
            {
                activeEventTitleText.text = "SPACE WEATHER: NOMINAL (No Active Orbital Anomalies)";
                activeEventTitleText.color = new Color(0.68f, 0.92f, 0.80f, 1.0f);
            }

            if (eventImpactDescriptionText != null)
            {
                eventImpactDescriptionText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Orbit #{0}, Tick #{1:00}: Solar flux, radiation belt, and ground downlink channels operating within normal parameters.",
                    Math.Max(1, orbitNumber),
                    tickIndex + 1);
                eventImpactDescriptionText.color = new Color(0.68f, 0.78f, 0.90f, 1.0f);
            }

            if (hadEvent)
            {
                OnEventStateChanged?.Invoke(string.Empty, false);
            }
        }

        private static string DescribeEventImpact(string eventTitle, float radiationLevel, bool safeModeActive)
        {
            if (eventTitle.IndexOf("CME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                eventTitle.IndexOf("Solar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                eventTitle.IndexOf("Storm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Radiation surge ({0:0.0} rads)! {1}",
                    radiationLevel,
                    safeModeActive
                        ? "Safe Mode is ACTIVE — avionics protected from radiation damage."
                        : "WARNING: Safe Mode is INACTIVE — electronics health degrading rapidly!");
            }

            if (eventTitle.IndexOf("Overheat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                eventTitle.IndexOf("Transmitter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Transmitter thermal throttling active: ground downlink bandwidth reduced to 45% capacity.";
            }

            if (eventTitle.IndexOf("Micrometeoroid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                eventTitle.IndexOf("Noise", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Micrometeoroid dust cloud: solar panel output reduced to 60% and background radiation elevated (+15 rads).";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Anomaly detected in orbital sector (Radiation: {0:0.0} rads). Monitor telemetry and autopilot rules.",
                radiationLevel);
        }

        /// <summary>
        /// Ensures the popup UI hierarchy exists and is bound.
        /// </summary>
        public void EnsurePopupHierarchy()
        {
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
            {
                rootRt = gameObject.AddComponent<RectTransform>();
            }

            Transform existingBanner = rootRt.Find("EventPopupBanner");
            if (existingBanner != null)
            {
                popupBannerRoot = existingBanner.gameObject;
                bannerBackground = existingBanner.GetComponent<Image>();
                Transform badgeTf = existingBanner.Find("AlertBadge");
                if (badgeTf != null)
                {
                    alertBadgeBackground = badgeTf.GetComponent<Image>();
                    alertBadgeText = badgeTf.GetComponentInChildren<Text>();
                }
                Transform iconTf = existingBanner.Find("EventIconFrame/EventIconSprite");
                if (iconTf != null)
                {
                    eventIconImage = iconTf.GetComponent<Image>();
                }
                Transform textCol = existingBanner.Find("EventTextColumn");
                if (textCol != null)
                {
                    Transform titleTf = textCol.Find("ActiveEventTitleText");
                    if (titleTf != null) activeEventTitleText = titleTf.GetComponent<Text>();
                    Transform descTf = textCol.Find("EventImpactDescriptionText");
                    if (descTf != null) eventImpactDescriptionText = descTf.GetComponent<Text>();
                }
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            popupBannerRoot = new GameObject("EventPopupBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            popupBannerRoot.transform.SetParent(rootRt, false);
            RectTransform bannerRt = popupBannerRoot.GetComponent<RectTransform>();
            bannerRt.anchorMin = Vector2.zero;
            bannerRt.anchorMax = Vector2.one;
            bannerRt.offsetMin = Vector2.zero;
            bannerRt.offsetMax = Vector2.zero;

            bannerBackground = popupBannerRoot.GetComponent<Image>();
            UITheme.ApplyPanelFrame(bannerBackground, useDialogueBox: true);

            HorizontalLayoutGroup hLayout = popupBannerRoot.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(12, 12, 8, 8);
            hLayout.spacing = 12f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = true;

            // Left Framed Event / A.S.T.R.A. Icon
            GameObject iconFrame = new GameObject("EventIconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconFrame.transform.SetParent(popupBannerRoot.transform, false);
            UITheme.ApplySlotFrame(iconFrame.GetComponent<Image>(), selected: false);
            LayoutElement iconFrameLe = iconFrame.AddComponent<LayoutElement>();
            iconFrameLe.preferredWidth = 44f;
            iconFrameLe.minWidth = 44f;

            GameObject iconSpriteObj = new GameObject("EventIconSprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconSpriteObj.transform.SetParent(iconFrame.transform, false);
            RectTransform iconRt = iconSpriteObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.10f, 0.10f);
            iconRt.anchorMax = new Vector2(0.90f, 0.90f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            eventIconImage = iconSpriteObj.GetComponent<Image>();
            eventIconImage.preserveAspect = true;
            eventIconImage.sprite = UITheme.Assets != null ? UITheme.Assets.astraNormal : null;
            eventIconImage.color = Color.white;

            // Status Name Tab Badge
            var badgeObj = new GameObject("AlertBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObj.transform.SetParent(popupBannerRoot.transform, false);
            alertBadgeBackground = badgeObj.GetComponent<Image>();
            UITheme.ApplyNameTab(alertBadgeBackground, UITheme.ButtonTintSuccess);
            LayoutElement badgeLe = badgeObj.AddComponent<LayoutElement>();
            badgeLe.preferredWidth = 132f;
            badgeLe.minWidth = 114f;

            var badgeTxtObj = new GameObject("BadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            badgeTxtObj.transform.SetParent(badgeObj.transform, false);
            RectTransform badgeTxtRt = badgeTxtObj.GetComponent<RectTransform>();
            badgeTxtRt.anchorMin = Vector2.zero;
            badgeTxtRt.anchorMax = Vector2.one;
            badgeTxtRt.offsetMin = Vector2.zero;
            badgeTxtRt.offsetMax = Vector2.zero;
            alertBadgeText = badgeTxtObj.GetComponent<Text>();
            alertBadgeText.font = font;
            alertBadgeText.text = "NOMINAL";
            alertBadgeText.fontSize = 13;
            alertBadgeText.fontStyle = FontStyle.Bold;
            alertBadgeText.color = Color.white;
            alertBadgeText.alignment = TextAnchor.MiddleCenter;
            Shadow badgeTxtShadow = badgeTxtObj.AddComponent<Shadow>();
            badgeTxtShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            badgeTxtShadow.effectDistance = new Vector2(1f, -1f);

            // Right Text Column
            var textColObj = new GameObject("EventTextColumn", typeof(RectTransform));
            textColObj.transform.SetParent(popupBannerRoot.transform, false);
            LayoutElement colLe = textColObj.AddComponent<LayoutElement>();
            colLe.flexibleWidth = 1f;

            VerticalLayoutGroup colLayout = textColObj.AddComponent<VerticalLayoutGroup>();
            colLayout.spacing = 2f;
            colLayout.childControlWidth = true;
            colLayout.childControlHeight = true;
            colLayout.childForceExpandWidth = true;
            colLayout.childForceExpandHeight = false;

            var titleObj = new GameObject("ActiveEventTitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleObj.transform.SetParent(textColObj.transform, false);
            activeEventTitleText = titleObj.GetComponent<Text>();
            activeEventTitleText.font = font;
            activeEventTitleText.text = "SPACE WEATHER: NOMINAL (No Active Orbital Anomalies)";
            activeEventTitleText.fontSize = 15;
            activeEventTitleText.fontStyle = FontStyle.Bold;
            activeEventTitleText.color = new Color(0.68f, 0.92f, 0.80f, 1.0f);
            activeEventTitleText.alignment = TextAnchor.MiddleLeft;

            var descObj = new GameObject("EventImpactDescriptionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            descObj.transform.SetParent(textColObj.transform, false);
            eventImpactDescriptionText = descObj.GetComponent<Text>();
            eventImpactDescriptionText.font = font;
            eventImpactDescriptionText.text = "Solar flux, radiation belt, and ground downlink channels operating within normal parameters.";
            eventImpactDescriptionText.fontSize = 13;
            eventImpactDescriptionText.fontStyle = FontStyle.Normal;
            eventImpactDescriptionText.color = new Color(0.68f, 0.78f, 0.90f, 1.0f);
            eventImpactDescriptionText.alignment = TextAnchor.MiddleLeft;
        }
    }
}
