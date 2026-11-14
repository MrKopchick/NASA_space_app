using System;
using System.Collections.Generic;
using AstraLink.Core;
using AstraLink.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Workshop
{
    /// <summary>
    /// Displays the 6 interdependent NASA Resource Gauges in the Hangar (Task S1-07, Ivan V):
    /// 1. Mass (kg vs Rocket limit)
    /// 2. Build Budget ($k vs Contract limit)
    /// 3. Power & Battery (Solar/RTG Gen vs Draw, Battery Wh)
    /// 4. Science Data & SSD (SSD Gb, Science/tick, Data/tick)
    /// 5. Communications (Downlink Gb/tick, Signal Delay s)
    /// 6. Reliability & Shielding (Overall Reliability %, Radiation Shielding)
    /// </summary>
    public class ResourceGaugesUI : MonoBehaviour
    {
        [Serializable]
        private class GaugeRowWidgets
        {
            public Text TitleText;
            public Text ValueText;
            public Image FillImage;
            public Image LedBarImage;
            public Image CardBackground;
        }

        private readonly List<GaugeRowWidgets> _gauges = new List<GaugeRowWidgets>();
        private Text _validationBannerText;
        private Image _validationBannerBg;
        private Image _previewRocketImg;
        private Image _previewBusImg;
        private Image _previewPlanetImg;
        private Text _previewSummaryText;

        private static readonly string[] GaugeTitles = new string[]
        {
            "MASS LIMIT",
            "BUILD BUDGET",
            "POWER & BATTERY",
            "SCIENCE & SSD",
            "COMMS LINK",
            "RELIABILITY"
        };

        public void EnsureGaugesUIHierarchy()
        {
            Transform existingRoot = transform.Find("GaugesContentRoot");
            if (existingRoot != null)
            {
                BindExistingHierarchy(existingRoot);
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject rootObj = CreateUIObject("GaugesContentRoot", transform);
            StretchFull(rootObj.GetComponent<RectTransform>());

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(16, 16, 12, 12);
            rootLayout.spacing = 8f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            BuildLaunchpadDiorama(rootObj.transform, font);

            _gauges.Clear();
            for (int i = 0; i < 6; i++)
            {
                _gauges.Add(CreateGaugeRow(i, GaugeTitles[i], rootObj.transform, font));
            }

            // Validation Status Banner at bottom
            GameObject bannerObj = CreateUIObject("ValidationBanner", rootObj.transform);
            _validationBannerBg = bannerObj.AddComponent<Image>();
            UITheme.ApplySlotFrame(_validationBannerBg, selected: true, new Color(0.75f, 1f, 0.84f, 1f));
            LayoutElement bannerLe = bannerObj.AddComponent<LayoutElement>();
            bannerLe.preferredHeight = 46f;

            VerticalLayoutGroup bannerLayout = bannerObj.AddComponent<VerticalLayoutGroup>();
            bannerLayout.padding = new RectOffset(14, 14, 6, 6);
            bannerLayout.childControlWidth = true;
            bannerLayout.childControlHeight = true;
            bannerLayout.childForceExpandWidth = true;
            bannerLayout.childForceExpandHeight = true;

            _validationBannerText = CreateText("StatusText", bannerObj.transform, font,
                "ALL SYSTEMS NOMINAL — READY TO LAUNCH", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        }

        private void BuildLaunchpadDiorama(Transform parent, Font font)
        {
            UIThemeAssets assets = UITheme.Assets;

            GameObject diorama = CreateUIObject("LaunchpadDiorama", parent);
            Image dBg = diorama.AddComponent<Image>();
            UITheme.ApplySlotFrame(dBg, selected: true);
            LayoutElement dLe = diorama.AddComponent<LayoutElement>();
            dLe.preferredHeight = 195f;
            dLe.flexibleHeight = 1f;

            // Space Sky Backdrop inside frame
            GameObject skyObj = CreateUIObject("SkyBackdrop", diorama.transform);
            RectTransform skyRt = skyObj.GetComponent<RectTransform>();
            skyRt.anchorMin = new Vector2(0.02f, 0.05f);
            skyRt.anchorMax = new Vector2(0.98f, 0.95f);
            skyRt.offsetMin = Vector2.zero;
            skyRt.offsetMax = Vector2.zero;
            Image skyImg = skyObj.AddComponent<Image>();
            skyImg.sprite = assets != null ? assets.bgPlanetApproach : null;
            skyImg.color = skyImg.sprite != null ? new Color(0.78f, 0.84f, 0.95f, 1f) : new Color(0.04f, 0.08f, 0.16f, 1f);

            // Target Planet in upper right
            GameObject planetObj = CreateUIObject("TargetPlanetPreview", skyObj.transform);
            RectTransform planetRt = planetObj.GetComponent<RectTransform>();
            planetRt.anchorMin = new Vector2(0.74f, 0.38f);
            planetRt.anchorMax = new Vector2(0.96f, 0.92f);
            planetRt.offsetMin = Vector2.zero;
            planetRt.offsetMax = Vector2.zero;
            _previewPlanetImg = planetObj.AddComponent<Image>();
            _previewPlanetImg.sprite = assets != null ? assets.planetEarth : null;
            _previewPlanetImg.preserveAspect = true;

            // Launch Pad Base (left)
            if (assets != null && assets.launchPad != null)
            {
                GameObject padObj = CreateUIObject("LaunchPad", skyObj.transform);
                RectTransform padRt = padObj.GetComponent<RectTransform>();
                padRt.anchorMin = new Vector2(0.04f, 0.02f);
                padRt.anchorMax = new Vector2(0.32f, 0.34f);
                padRt.offsetMin = Vector2.zero;
                padRt.offsetMax = Vector2.zero;
                Image padImg = padObj.AddComponent<Image>();
                padImg.sprite = assets.launchPad;
                padImg.preserveAspect = true;
            }

            // Launch Tower (left of rocket)
            if (assets != null && assets.launchTower != null)
            {
                GameObject towerObj = CreateUIObject("LaunchTower", skyObj.transform);
                RectTransform towerRt = towerObj.GetComponent<RectTransform>();
                towerRt.anchorMin = new Vector2(0.04f, 0.24f);
                towerRt.anchorMax = new Vector2(0.18f, 0.92f);
                towerRt.offsetMin = Vector2.zero;
                towerRt.offsetMax = Vector2.zero;
                Image towerImg = towerObj.AddComponent<Image>();
                towerImg.sprite = assets.launchTower;
                towerImg.preserveAspect = true;
            }

            // Selected Rocket standing on pad
            GameObject rocketObj = CreateUIObject("SelectedRocketPreview", skyObj.transform);
            RectTransform rocketRt = rocketObj.GetComponent<RectTransform>();
            rocketRt.anchorMin = new Vector2(0.14f, 0.22f);
            rocketRt.anchorMax = new Vector2(0.32f, 0.90f);
            rocketRt.offsetMin = Vector2.zero;
            rocketRt.offsetMax = Vector2.zero;
            _previewRocketImg = rocketObj.AddComponent<Image>();
            _previewRocketImg.sprite = assets != null ? assets.rocketSmall : null;
            _previewRocketImg.preserveAspect = true;

            // Assembled Spacecraft Bus Preview in center slot
            GameObject busFrame = CreateUIObject("SpacecraftPreviewSlot", skyObj.transform);
            RectTransform busFrameRt = busFrame.GetComponent<RectTransform>();
            busFrameRt.anchorMin = new Vector2(0.37f, 0.22f);
            busFrameRt.anchorMax = new Vector2(0.68f, 0.88f);
            busFrameRt.offsetMin = Vector2.zero;
            busFrameRt.offsetMax = Vector2.zero;
            Image bfImg = busFrame.AddComponent<Image>();
            UITheme.ApplySlotFrame(bfImg, selected: false, new Color(1f, 1f, 1f, 0.85f));

            GameObject busSpriteObj = CreateUIObject("SelectedBusPreview", busFrame.transform);
            RectTransform bRt = busSpriteObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.14f, 0.14f);
            bRt.anchorMax = new Vector2(0.86f, 0.86f);
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;
            _previewBusImg = busSpriteObj.AddComponent<Image>();
            _previewBusImg.sprite = UITheme.GetItemSprite("bus_cubesat_3u");
            _previewBusImg.preserveAspect = true;

            // Caption label at bottom right
            _previewSummaryText = CreateText("DioramaLabel", skyObj.transform, font,
                "HANGAR ASSEMBLY BAY", 14, FontStyle.Bold, UITheme.TextGold, TextAnchor.MiddleCenter);
            RectTransform lblRt = _previewSummaryText.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0.34f, 0.02f);
            lblRt.anchorMax = new Vector2(0.98f, 0.20f);
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;
        }

        private void BindExistingHierarchy(Transform root)
        {
            _gauges.Clear();
            for (int i = 0; i < 6; i++)
            {
                Transform row = root.Find("GaugeRow_" + i);
                if (row == null) continue;

                var w = new GaugeRowWidgets
                {
                    CardBackground = row.GetComponent<Image>(),
                    TitleText = FindChild<Text>(row, "TopRow/Title"),
                    ValueText = FindChild<Text>(row, "TopRow/Value"),
                    FillImage = FindChild<Image>(row, "BarBg/BarFill"),
                    LedBarImage = FindChild<Image>(row, "BarBg/LedBar")
                };
                _gauges.Add(w);
            }

            _previewRocketImg = FindChild<Image>(root, "LaunchpadDiorama/SkyBackdrop/SelectedRocketPreview");
            _previewBusImg = FindChild<Image>(root, "LaunchpadDiorama/SkyBackdrop/SpacecraftPreviewSlot/SelectedBusPreview");
            _previewPlanetImg = FindChild<Image>(root, "LaunchpadDiorama/SkyBackdrop/TargetPlanetPreview");
            _previewSummaryText = FindChild<Text>(root, "LaunchpadDiorama/SkyBackdrop/DioramaLabel");

            Transform banner = root.Find("ValidationBanner");
            if (banner != null)
            {
                _validationBannerBg = banner.GetComponent<Image>();
                _validationBannerText = FindChild<Text>(banner, "StatusText");
            }
        }

        public void UpdateGauges(
            ResourceSummary summary,
            List<SatelliteComponent> selectedComponents,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal)
        {
            EnsureGaugesUIHierarchy();
            if (summary == null || _gauges.Count < 6) return;

            UpdateLaunchpadDiorama(selectedComponents, rocket, orbit, goal);

            float maxMassKg = GetMaxRocketMassForOrbit(rocket, orbit);
            float budgetLimit = goal != null ? goal.BudgetLimitThousands : 120f;
            float solarFlux = orbit != null ? orbit.SolarFluxFactor : 1.0f;
            float signalDelay = orbit != null ? orbit.SignalDelaySeconds : 1.0f;

            float effectiveSolarW = summary.MaxSolarWatts * solarFlux;
            float totalGenW = effectiveSolarW + summary.RtgWatts;

            float activeDrawW = 0f;
            float dataGenGb = 0f;
            float sciencePerTick = 0f;
            string sensorTag = "None";

            if (selectedComponents != null)
            {
                for (int i = 0; i < selectedComponents.Count; i++)
                {
                    var c = selectedComponents[i];
                    if (c == null) continue;
                    activeDrawW += c.BasePowerDrawWatts + c.ActivePowerDrawWatts;
                    dataGenGb += c.DataGenGbPerTick;
                    sciencePerTick += c.SciencePointsPerTick;
                    if (!string.IsNullOrEmpty(c.SensorTag)) sensorTag = c.SensorTag;
                }
            }

            // 1. MASS
            float massRatio = maxMassKg > 0.01f ? summary.TotalMassKg / maxMassKg : 0f;
            SetGauge(0,
                string.Format("{0:0.0} / {1:0.0} kg ({2})", summary.TotalMassKg, maxMassKg, summary.IsMassValid ? "OK" : "OVERLOAD!"),
                Mathf.Clamp01(massRatio),
                summary.IsMassValid ? new Color(0.20f, 0.72f, 0.42f, 1f) : new Color(0.88f, 0.24f, 0.24f, 1f),
                !summary.IsMassValid);

            // 2. BUDGET
            float budgetRatio = budgetLimit > 0.01f ? summary.TotalCostThousands / budgetLimit : 0f;
            SetGauge(1,
                string.Format("${0:0}k / ${1:0}k ({2})", summary.TotalCostThousands, budgetLimit, summary.IsBudgetValid ? "OK" : "OVER BUDGET!"),
                Mathf.Clamp01(budgetRatio),
                summary.IsBudgetValid ? new Color(0.24f, 0.68f, 0.88f, 1f) : new Color(0.88f, 0.24f, 0.24f, 1f),
                !summary.IsBudgetValid);

            // 3. POWER & BATTERY
            float powerRatio = Mathf.Clamp01(totalGenW / Mathf.Max(1f, activeDrawW * 1.5f));
            bool powerLow = totalGenW < activeDrawW;
            SetGauge(2,
                string.Format("Gen: {0:0}W vs Draw: {1:0}W • Bat: {2:0} Wh", totalGenW, activeDrawW, summary.MaxBatteryWh),
                powerRatio,
                powerLow ? new Color(0.92f, 0.62f, 0.18f, 1f) : new Color(0.95f, 0.80f, 0.22f, 1f),
                false);

            // 4. DATA & SSD
            float ssdRatio = Mathf.Clamp01(summary.MaxSsdGb / 50f);
            SetGauge(3,
                string.Format("SSD: {0:0} Gb • +{1:0.0} Sci/tick ({2})", summary.MaxSsdGb, sciencePerTick, sensorTag),
                Mathf.Max(0.12f, ssdRatio),
                new Color(0.58f, 0.42f, 0.92f, 1f),
                false);

            // 5. COMMS & SIGNAL DELAY
            float commsRatio = Mathf.Clamp01(summary.DownlinkGbPerTick / 18f);
            SetGauge(4,
                string.Format("Downlink: {0:0.0} Gb/t • Delay: {1:0.0} s", summary.DownlinkGbPerTick, signalDelay),
                Mathf.Max(0.12f, commsRatio),
                new Color(0.28f, 0.78f, 0.78f, 1f),
                false);

            // 6. RELIABILITY & SHIELDING
            float relPercent = summary.OverallReliability * 100f;
            SetGauge(5,
                string.Format("Reliability: {0:0.0}% • Shielding: {1:0}", relPercent, summary.TotalShielding),
                Mathf.Clamp01(summary.OverallReliability),
                relPercent >= 85f ? new Color(0.25f, 0.80f, 0.48f, 1f) : new Color(0.90f, 0.65f, 0.20f, 1f),
                false);

            // Check sensor requirement
            bool sensorValid = true;
            string reqSensor = (goal != null && !string.IsNullOrEmpty(goal.RequiredSensorTag)) ? goal.RequiredSensorTag : "";
            if (!string.IsNullOrEmpty(reqSensor) && !string.Equals(reqSensor, sensorTag, StringComparison.OrdinalIgnoreCase))
            {
                sensorValid = false;
            }

            if (_validationBannerText != null && _validationBannerBg != null)
            {
                if (!summary.IsMassValid)
                {
                    _validationBannerBg.color = new Color(1.0f, 0.62f, 0.65f, 0.98f);
                    _validationBannerText.text = "OVERWEIGHT: SPACECRAFT TOO HEAVY FOR ROCKET!";
                }
                else if (!summary.IsBudgetValid)
                {
                    _validationBannerBg.color = new Color(1.0f, 0.62f, 0.65f, 0.98f);
                    _validationBannerText.text = "OVER BUDGET: REDUCE COMPONENT COST!";
                }
                else if (!sensorValid)
                {
                    _validationBannerBg.color = new Color(1.0f, 0.84f, 0.56f, 0.98f);
                    _validationBannerText.text = "MISSING SENSOR: EQUIP " + reqSensor.ToUpperInvariant() + " PAYLOAD!";
                }
                else
                {
                    _validationBannerBg.color = new Color(0.68f, 1.0f, 0.80f, 0.98f);
                    _validationBannerText.text = "ALL SYSTEMS NOMINAL — READY TO LAUNCH";
                }
            }
        }

        private void UpdateLaunchpadDiorama(
            List<SatelliteComponent> selectedComponents,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal)
        {
            if (_previewRocketImg != null && rocket != null)
            {
                Sprite rSprite = UITheme.GetItemSprite(rocket.Id);
                if (rSprite != null) _previewRocketImg.sprite = rSprite;
            }

            if (_previewBusImg != null && selectedComponents != null)
            {
                for (int i = 0; i < selectedComponents.Count; i++)
                {
                    SatelliteComponent c = selectedComponents[i];
                    if (c != null && c.Category == ComponentCategory.CorePlatform)
                    {
                        Sprite bSprite = UITheme.GetItemSprite(c.Id);
                        if (bSprite != null) _previewBusImg.sprite = bSprite;
                        break;
                    }
                }
            }

            if (_previewPlanetImg != null)
            {
                string targetKey = orbit != null ? orbit.Id : (goal != null ? goal.OrbitId : "orbit_leo");
                Sprite pSprite = UITheme.GetPlanetSprite(targetKey);
                if (pSprite != null) _previewPlanetImg.sprite = pSprite;
            }

            if (_previewSummaryText != null)
            {
                string rocketLabel = rocket != null ? rocket.Name : "Rocket";
                string orbitLabel = orbit != null ? orbit.Name : "LEO";
                _previewSummaryText.text = rocketLabel.ToUpperInvariant() + "  ▶  " + orbitLabel.ToUpperInvariant();
            }
        }

        public static float GetMaxRocketMassForOrbit(RocketOption rocket, OrbitDefinition orbit)
        {
            if (rocket == null) return 25f;
            if (orbit == null) return rocket.MaxMassLeoKg;

            switch (orbit.Type)
            {
                case OrbitType.LowEarth: return rocket.MaxMassLeoKg;
                case OrbitType.Lunar: return rocket.MaxMassLunarKg;
                case OrbitType.Mars: return rocket.MaxMassMarsKg;
                case OrbitType.DeepSpace: return rocket.MaxMassDeepSpaceKg;
                default: return rocket.MaxMassLeoKg;
            }
        }

        private void SetGauge(int index, string valueStr, float fill01, Color barColor, bool isAlert)
        {
            GaugeRowWidgets g = _gauges[index];
            if (g.ValueText != null) g.ValueText.text = valueStr;
            if (g.CardBackground != null)
            {
                g.CardBackground.color = isAlert
                    ? new Color(1.0f, 0.68f, 0.70f, 0.98f)
                    : Color.white;
            }

            float clamped = Mathf.Clamp01(fill01);
            LedSheetKind sheet = isAlert ? LedSheetKind.Pink : (index % 2 == 0 ? LedSheetKind.Cyan : LedSheetKind.Blue);
            Sprite ledSprite = UITheme.GetLedBarSprite(sheet, clamped);

            if (g.LedBarImage != null && ledSprite != null)
            {
                g.LedBarImage.sprite = ledSprite;
                g.LedBarImage.color = isAlert ? new Color(1f, 0.78f, 0.78f, 1f) : Color.white;
                g.LedBarImage.enabled = true;
                if (g.FillImage != null) g.FillImage.enabled = false;
            }
            else if (g.FillImage != null)
            {
                g.FillImage.enabled = true;
                g.FillImage.color = barColor;
                RectTransform fillRt = g.FillImage.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = new Vector2(clamped, 1f);
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
            }
        }

        private static GaugeRowWidgets CreateGaugeRow(int index, string title, Transform parent, Font font)
        {
            GameObject rowObj = CreateUIObject("GaugeRow_" + index, parent);
            Image cardBg = rowObj.AddComponent<Image>();
            UITheme.ApplyPanelFrame(cardBg, useDialogueBox: true);

            LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 68f;

            VerticalLayoutGroup vLayout = rowObj.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(12, 12, 7, 7);
            vLayout.spacing = 4f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            GameObject topRow = CreateUIObject("TopRow", rowObj.transform);
            LayoutElement topLe = topRow.AddComponent<LayoutElement>();
            topLe.preferredHeight = 24f;

            HorizontalLayoutGroup hLayout = topRow.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 8f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;

            Sprite gaugeIcon = ResolveGaugeIcon(index);
            if (gaugeIcon != null)
            {
                GameObject iconObj = CreateUIObject("GaugeIcon", topRow.transform);
                LayoutElement iconLe = iconObj.AddComponent<LayoutElement>();
                iconLe.preferredWidth = 22f;
                iconLe.preferredHeight = 22f;
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = gaugeIcon;
                iconImg.preserveAspect = true;
            }

            Text titleTxt = CreateText("Title", topRow.transform, font, title, 14, FontStyle.Bold,
                UITheme.TextGold, TextAnchor.MiddleLeft);
            LayoutElement titleLe = titleTxt.gameObject.AddComponent<LayoutElement>();
            titleLe.preferredWidth = 140f;

            Text valueTxt = CreateText("Value", topRow.transform, font, "...", 14, FontStyle.Bold,
                Color.white, TextAnchor.MiddleRight);
            LayoutElement valLe = valueTxt.gameObject.AddComponent<LayoutElement>();
            valLe.flexibleWidth = 1f;

            GameObject barBg = CreateUIObject("BarBg", rowObj.transform);
            Image bgImg = barBg.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.05f, 0.10f, 0.85f);
            LayoutElement barLe = barBg.AddComponent<LayoutElement>();
            barLe.preferredHeight = 22f;

            GameObject barFill = CreateUIObject("BarFill", barBg.transform);
            Image fillImg = barFill.AddComponent<Image>();
            fillImg.color = UITheme.AccentGreen;
            StretchFull(barFill.GetComponent<RectTransform>());
            fillImg.enabled = false;

            GameObject ledObj = CreateUIObject("LedBar", barBg.transform);
            Image ledImg = ledObj.AddComponent<Image>();
            ledImg.preserveAspect = false;
            StretchFull(ledObj.GetComponent<RectTransform>());
            Sprite initLed = UITheme.GetLedBarSprite(LedSheetKind.Cyan, 0.5f);
            if (initLed != null) ledImg.sprite = initLed;

            return new GaugeRowWidgets
            {
                CardBackground = cardBg,
                TitleText = titleTxt,
                ValueText = valueTxt,
                FillImage = fillImg,
                LedBarImage = ledImg
            };
        }

        private static T FindChild<T>(Transform root, string path) where T : Component
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }

        private static Sprite ResolveGaugeIcon(int index)
        {
            UIThemeAssets assets = UITheme.Assets;
            if (assets == null) return null;

            switch (index)
            {
                case 0: return UITheme.GetItemSprite("rocket_medium");
                case 1: return assets.spaceCreditsCoin;
                case 2: return assets.iconSolarPanel != null ? assets.iconSolarPanel : UITheme.GetItemSprite("pwr_solar_mini");
                case 3: return assets.iconDataChip;
                case 4: return UITheme.GetItemSprite("comms_uhf_basic");
                case 5: return assets.iconShield != null ? assets.iconShield : assets.missionBadge;
                default: return null;
            }
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

        private static Text CreateText(string name, Transform parent, Font font, string content, int size, FontStyle style, Color color, TextAnchor align)
        {
            GameObject go = CreateUIObject(name, parent);
            Text txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }
    }
}
