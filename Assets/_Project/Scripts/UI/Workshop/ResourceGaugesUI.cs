using System;
using System.Collections.Generic;
using AstraLink.Core;
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
            public Image CardBackground;
        }

        private readonly List<GaugeRowWidgets> _gauges = new List<GaugeRowWidgets>();
        private Text _validationBannerText;
        private Image _validationBannerBg;

        private static readonly string[] GaugeTitles = new string[]
        {
            "Mass / Rocket Limit",
            "Assembly Budget",
            "Power Balance",
            "Science Data • SSD",
            "Comms Link",
            "Reliability • Shield"
        };

        public void EnsureGaugesUIHierarchy()
        {
            Transform existingRoot = transform.Find("GaugesContentRoot");
            if (existingRoot != null)
            {
                if (existingRoot.Find("GaugesUI_v3") != null)
                {
                    BindExistingHierarchy(existingRoot);
                    return;
                }

                if (Application.isPlaying) Destroy(existingRoot.gameObject);
                else DestroyImmediate(existingRoot.gameObject);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject rootObj = CreateUIObject("GaugesContentRoot", transform);
            StretchFull(rootObj.GetComponent<RectTransform>());
            CreateUIObject("GaugesUI_v3", rootObj.transform);

            VerticalLayoutGroup rootLayout = rootObj.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(22, 22, 18, 18);
            rootLayout.spacing = 6f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            GameObject headerRow = CreateUIObject("TelemetryHeaderRow", rootObj.transform);
            LayoutElement headerRowLe = headerRow.AddComponent<LayoutElement>();
            headerRowLe.preferredHeight = 28f;
            HorizontalLayoutGroup headerH = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerH.childAlignment = TextAnchor.MiddleLeft;
            headerH.childControlWidth = true;
            headerH.childControlHeight = true;
            headerH.childForceExpandWidth = false;

            Text header = CreateText("HeaderTitle", headerRow.transform, font,
                "TELEMETRY", 18, FontStyle.Bold,
                new Color(0.45f, 0.85f, 1f, 1f), TextAnchor.MiddleLeft);
            LayoutElement headerLe = header.gameObject.AddComponent<LayoutElement>();
            headerLe.flexibleWidth = 1f;

            Text subHeader = CreateText("HeaderSub", headerRow.transform, font,
                "6 NASA resources", 13, FontStyle.Normal,
                new Color(0.55f, 0.68f, 0.84f, 1f), TextAnchor.MiddleRight);
            LayoutElement subLe = subHeader.gameObject.AddComponent<LayoutElement>();
            subLe.preferredWidth = 180f;

            _gauges.Clear();
            for (int i = 0; i < 6; i++)
            {
                _gauges.Add(CreateGaugeRow(i, GaugeTitles[i], rootObj.transform, font));
            }

            // Validation Status Banner at bottom
            GameObject bannerObj = CreateUIObject("ValidationBanner", rootObj.transform);
            _validationBannerBg = bannerObj.AddComponent<Image>();
            _validationBannerBg.color = new Color(0.45f, 1.0f, 0.55f, 1f);
            LayoutElement bannerLe = bannerObj.AddComponent<LayoutElement>();
            bannerLe.preferredHeight = 42f;

            VerticalLayoutGroup bannerLayout = bannerObj.AddComponent<VerticalLayoutGroup>();
            bannerLayout.padding = new RectOffset(16, 16, 8, 8);
            bannerLayout.childControlWidth = true;
            bannerLayout.childControlHeight = true;
            bannerLayout.childForceExpandWidth = true;
            bannerLayout.childForceExpandHeight = true;

            _validationBannerText = CreateText("StatusText", bannerObj.transform, font,
                "All 6 NASA systems nominal — ready for launch", 13, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            _validationBannerText.resizeTextForBestFit = true;
            _validationBannerText.resizeTextMinSize = 9;
            _validationBannerText.resizeTextMaxSize = 13;

            PixelUITheme.ApplyToHierarchy(rootObj.transform);
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
                    TitleText = FindChild<Text>(row, "BarColumn/TopRow/Title"),
                    ValueText = FindChild<Text>(row, "BarColumn/TopRow/Value"),
                    FillImage = FindChild<Image>(row, "BarColumn/BarBg/BarFill")
                };
                _gauges.Add(w);
            }

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
                string.Format("{0:0}W / {1:0}W • {2:0} Wh", totalGenW, activeDrawW, summary.MaxBatteryWh),
                powerRatio,
                powerLow ? new Color(0.92f, 0.62f, 0.18f, 1f) : new Color(0.95f, 0.80f, 0.22f, 1f),
                false);

            // 4. DATA & SSD
            float ssdRatio = Mathf.Clamp01(summary.MaxSsdGb / 50f);
            SetGauge(3,
                string.Format("{0:0} Gb • +{1:0.0}/t ({2})", summary.MaxSsdGb, sciencePerTick, sensorTag),
                Mathf.Max(0.12f, ssdRatio),
                new Color(0.58f, 0.42f, 0.92f, 1f),
                false);

            // 5. COMMS & SIGNAL DELAY
            float commsRatio = Mathf.Clamp01(summary.DownlinkGbPerTick / 18f);
            SetGauge(4,
                string.Format("{0:0.0} Gb/t • {1:0.0} s delay", summary.DownlinkGbPerTick, signalDelay),
                Mathf.Max(0.12f, commsRatio),
                new Color(0.28f, 0.78f, 0.78f, 1f),
                false);

            // 6. RELIABILITY & SHIELDING
            float relPercent = summary.OverallReliability * 100f;
            SetGauge(5,
                string.Format("{0:0.0}% • Shield {1:0}", relPercent, summary.TotalShielding),
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
                    _validationBannerBg.color = new Color(1.0f, 0.42f, 0.42f, 1f);
                    _validationBannerText.color = new Color(1f, 0.72f, 0.72f, 1f);
                    _validationBannerText.text = "CRITICAL: MASS EXCEEDS ROCKET CAPACITY";
                }
                else if (!summary.IsBudgetValid)
                {
                    _validationBannerBg.color = new Color(1.0f, 0.42f, 0.42f, 1f);
                    _validationBannerText.color = new Color(1f, 0.72f, 0.72f, 1f);
                    _validationBannerText.text = "CRITICAL: COST EXCEEDS MISSION BUDGET";
                }
                else if (!sensorValid)
                {
                    _validationBannerBg.color = new Color(1.0f, 0.78f, 0.40f, 1f);
                    _validationBannerText.color = new Color(1f, 0.88f, 0.55f, 1f);
                    _validationBannerText.text = "WARNING: REQUIRES SENSOR: " + reqSensor.ToUpperInvariant();
                }
                else
                {
                    _validationBannerBg.color = new Color(0.45f, 1.0f, 0.55f, 1f);
                    _validationBannerText.color = new Color(0.65f, 1f, 0.78f, 1f);
                    _validationBannerText.text = "ALL 6 NASA SYSTEMS NOMINAL — READY";
                }
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
                    ? new Color(1.0f, 0.52f, 0.52f, 1f)
                    : Color.white;
            }
            if (g.FillImage != null)
            {
                g.FillImage.color = barColor;
                RectTransform fillRt = g.FillImage.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
            }
        }

        private static GaugeRowWidgets CreateGaugeRow(int index, string title, Transform parent, Font font)
        {
            GameObject rowObj = CreateUIObject("GaugeRow_" + index, parent);
            Image cardBg = rowObj.AddComponent<Image>();
            cardBg.color = new Color(0.05f, 0.08f, 0.14f, 0.86f);

            LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 68f;
            rowLe.flexibleHeight = 1f;

            HorizontalLayoutGroup rowH = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowH.padding = new RectOffset(10, 10, 8, 8);
            rowH.spacing = 12f;
            rowH.childAlignment = TextAnchor.MiddleLeft;
            rowH.childControlWidth = true;
            rowH.childControlHeight = true;
            rowH.childForceExpandWidth = false;

            GameObject iconObj = CreateUIObject("TelemetryIcon_" + index, rowObj.transform);
            LayoutElement iconLe = iconObj.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 28f;
            iconLe.preferredHeight = 28f;
            iconLe.minWidth = 28f;
            iconLe.minHeight = 28f;
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            Sprite telSprite = PixelUITheme.ResolveTelemetryIconSprite(index);
            iconImg.sprite = telSprite;
            iconImg.color = telSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);

            GameObject barCol = CreateUIObject("BarColumn", rowObj.transform);
            LayoutElement colLe = barCol.AddComponent<LayoutElement>();
            colLe.flexibleWidth = 1f;

            VerticalLayoutGroup vLayout = barCol.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 6f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            GameObject topRow = CreateUIObject("TopRow", barCol.transform);
            LayoutElement topLe = topRow.AddComponent<LayoutElement>();
            topLe.preferredHeight = 22f;

            HorizontalLayoutGroup hLayout = topRow.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 8f;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;

            Text titleTxt = CreateText("Title", topRow.transform, font, title, 14, FontStyle.Bold,
                new Color(0.86f, 0.92f, 1f, 1f), TextAnchor.MiddleLeft);
            LayoutElement titleLe = titleTxt.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;

            Text valueTxt = CreateText("Value", topRow.transform, font, "...", 13, FontStyle.Bold,
                new Color(0.75f, 0.94f, 1f, 1f), TextAnchor.MiddleRight);
            LayoutElement valLe = valueTxt.gameObject.AddComponent<LayoutElement>();
            valLe.preferredWidth = 270f;

            GameObject barBg = CreateUIObject("BarBg", barCol.transform);
            Image bgImg = barBg.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.04f, 0.08f, 1f);
            LayoutElement barLe = barBg.AddComponent<LayoutElement>();
            barLe.preferredHeight = 14f;

            GameObject barFill = CreateUIObject("BarFill", barBg.transform);
            Image fillImg = barFill.AddComponent<Image>();
            fillImg.color = new Color(0.20f, 0.72f, 0.42f, 1f);
            RectTransform fillRt = barFill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.5f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            return new GaugeRowWidgets
            {
                CardBackground = cardBg,
                TitleText = titleTxt,
                ValueText = valueTxt,
                FillImage = fillImg
            };
        }

        private static T FindChild<T>(Transform root, string path) where T : Component
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<T>() : null;
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
