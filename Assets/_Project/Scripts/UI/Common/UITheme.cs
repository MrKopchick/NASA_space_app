using System.Collections.Generic;
using AstraLink.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Common
{
    public enum LedSheetKind
    {
        Blue = 0,
        Cyan = 1,
        Purple = 2,
        Pink = 3,
        Brown = 4
    }

    /// <summary>
    /// Shared visual styling helpers for applying _Project/Art sprites, framed portraits,
    /// segmented pixel-art LED telemetry bars, and cohesive UI styling across all screens.
    /// </summary>
    public static class UITheme
    {
        public static readonly Color TextPrimary = new Color(0.93f, 0.97f, 1.00f, 1f);
        public static readonly Color TextSecondary = new Color(0.70f, 0.82f, 0.94f, 1f);
        public static readonly Color TextGold = new Color(1.00f, 0.84f, 0.36f, 1f);
        public static readonly Color AccentCyan = new Color(0.32f, 0.88f, 0.98f, 1f);
        public static readonly Color AccentGreen = new Color(0.30f, 0.86f, 0.56f, 1f);
        public static readonly Color AccentAmber = new Color(0.98f, 0.72f, 0.28f, 1f);
        public static readonly Color AccentRed = new Color(0.96f, 0.38f, 0.38f, 1f);
        public static readonly Color PanelTintDark = new Color(0.88f, 0.92f, 1.00f, 0.96f);
        public static readonly Color ButtonTintPrimary = Color.white;
        public static readonly Color ButtonTintSuccess = new Color(0.70f, 1.00f, 0.78f, 1f);
        public static readonly Color ButtonTintMuted = new Color(0.66f, 0.76f, 0.90f, 1f);
        public static readonly Color ButtonTintDanger = new Color(1.00f, 0.68f, 0.68f, 1f);
        public static readonly Color ButtonTintGold = new Color(1.00f, 0.88f, 0.58f, 1f);

        private static UIThemeAssets _cachedAssets;
        private static readonly Dictionary<int, Sprite[]> _ledCache = new Dictionary<int, Sprite[]>();

        public static UIThemeAssets Assets
        {
            get
            {
                if (_cachedAssets == null)
                {
                    _cachedAssets = Resources.Load<UIThemeAssets>("UIThemeAssets");
                }

                return _cachedAssets;
            }
        }

        public static void ApplyPanelFrame(Image image, bool useDialogueBox = false, Color? tint = null)
        {
            if (image == null) return;

            UIThemeAssets assets = Assets;
            Sprite frame = assets != null
                ? (useDialogueBox ? assets.dialogueBox : assets.panelFrame)
                : null;

            if (frame != null)
            {
                image.sprite = frame;
                image.type = Image.Type.Sliced;
                image.color = tint ?? Color.white;
            }
            else
            {
                image.color = tint ?? new Color(0.08f, 0.13f, 0.22f, 0.96f);
            }
        }

        public static void ApplySlotFrame(Image image, bool selected = false, Color? tint = null)
        {
            if (image == null) return;

            UIThemeAssets assets = Assets;
            Sprite frame = assets != null
                ? (selected ? assets.itemSlotSelected : assets.itemSlot)
                : null;

            if (frame != null)
            {
                image.sprite = frame;
                image.type = Image.Type.Sliced;
                image.color = tint ?? Color.white;
            }
            else
            {
                image.color = tint ?? (selected
                    ? new Color(0.14f, 0.28f, 0.44f, 0.96f)
                    : new Color(0.08f, 0.13f, 0.22f, 0.94f));
            }
        }

        public static void ApplyNameTab(Image image, Color? tint = null)
        {
            if (image == null) return;

            UIThemeAssets assets = Assets;
            if (assets != null && assets.nameTab != null)
            {
                image.sprite = assets.nameTab;
                image.type = Image.Type.Sliced;
                image.color = tint ?? Color.white;
            }
            else
            {
                image.color = tint ?? new Color(0.12f, 0.26f, 0.40f, 1f);
            }
        }

        public static void ApplyButtonStyle(Button button, Image image = null, Color? tint = null)
        {
            if (button == null) return;

            if (image == null)
            {
                image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            }

            UIThemeAssets assets = Assets;
            if (image != null && assets != null && assets.buttonNormal != null)
            {
                image.sprite = assets.buttonNormal;
                image.type = Image.Type.Sliced;
                image.color = tint ?? Color.white;

                button.targetGraphic = image;
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState ss = button.spriteState;
                ss.highlightedSprite = assets.buttonHover != null ? assets.buttonHover : assets.buttonNormal;
                ss.pressedSprite = assets.buttonPressed != null ? assets.buttonPressed : assets.buttonNormal;
                ss.selectedSprite = assets.buttonHover != null ? assets.buttonHover : assets.buttonNormal;
                ss.disabledSprite = assets.buttonDisabled != null ? assets.buttonDisabled : assets.buttonNormal;
                button.spriteState = ss;
            }
            else if (image != null && tint.HasValue)
            {
                image.color = tint.Value;
            }
        }

        public static Sprite GetLedBarSprite(LedSheetKind sheetKind, float normalized01)
        {
            UIThemeAssets assets = Assets;
            if (assets == null) return null;

            Sprite sourceSheet;
            switch (sheetKind)
            {
                case LedSheetKind.Cyan: sourceSheet = assets.telemetryCyanSheet; break;
                case LedSheetKind.Purple: sourceSheet = assets.telemetryPurpleSheet; break;
                case LedSheetKind.Pink: sourceSheet = assets.telemetryPinkSheet; break;
                case LedSheetKind.Brown: sourceSheet = assets.telemetryBrownSheet; break;
                default: sourceSheet = assets.telemetryBlueSheet; break;
            }

            if (sourceSheet == null || sourceSheet.texture == null)
            {
                return null;
            }

            int key = (int)sheetKind;
            if (!_ledCache.TryGetValue(key, out Sprite[] steps) || steps == null || steps.Length != 10)
            {
                steps = new Sprite[10];
                Texture2D tex = sourceSheet.texture;
                float stepW = Mathf.Min(64f, tex.width * 0.25f);
                float stepH = Mathf.Min(16f, tex.height * 0.10f);
                for (int i = 0; i < 10; i++)
                {
                    Rect r = new Rect(0f, i * stepH, stepW, stepH);
                    steps[i] = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4f, 4f, 4f, 4f));
                }
                _ledCache[key] = steps;
            }

            int idx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized01) * 9f), 0, 9);
            return steps[idx];
        }

        public static Sprite GetItemSprite(string id)
        {
            UIThemeAssets assets = Assets;
            return assets != null ? assets.FindItemSprite(id) : null;
        }

        public static Sprite GetPlanetSprite(string orbitIdOrTitle)
        {
            UIThemeAssets assets = Assets;
            if (assets == null) return null;

            string key = (orbitIdOrTitle ?? string.Empty).ToLowerInvariant();
            if (key.Contains("mars")) return assets.planetMars;
            if (key.Contains("lunar") || key.Contains("moon")) return assets.planetMoon;
            if (key.Contains("venus")) return assets.planetVenus;
            if (key.Contains("sun") || key.Contains("solar")) return assets.planetSun;
            return assets.planetEarth;
        }

        public static Sprite GetHazardSprite(string eventIdOrTitle)
        {
            UIThemeAssets assets = Assets;
            if (assets == null) return null;

            string key = (eventIdOrTitle ?? string.Empty).ToLowerInvariant();
            if (key.Contains("cme") || key.Contains("flare") || key.Contains("storm") || key.Contains("sun"))
            {
                return assets.celestialSupernova != null ? assets.celestialSupernova : assets.planetSun;
            }
            if (key.Contains("meteor") || key.Contains("asteroid") || key.Contains("noise"))
            {
                return assets.celestialMeteor != null ? assets.celestialMeteor : assets.celestialAsteroidField;
            }
            if (key.Contains("overheat") || key.Contains("tx"))
            {
                return assets.planetSun != null ? assets.planetSun : assets.celestialSupernova;
            }
            if (key.Contains("eclipse") || key.Contains("shadow"))
            {
                return assets.celestialEclipse;
            }

            return assets.iconStarChart;
        }

        public static Sprite GetDispatcherForMission(string missionId)
        {
            UIThemeAssets assets = Assets;
            if (assets == null) return null;

            string key = (missionId ?? string.Empty).ToLowerInvariant();
            if (key.Contains("lunar")) return assets.dispatcherBlue != null ? assets.dispatcherBlue : assets.dispatcher;
            if (key.Contains("mars")) return assets.dispatcherArmored != null ? assets.dispatcherArmored : assets.dispatcher;
            return assets.dispatcher;
        }

        public static Sprite GetAstraPortrait(AuditSeverity severity)
        {
            UIThemeAssets assets = Assets;
            if (assets == null) return null;

            switch (severity)
            {
                case AuditSeverity.Critical:
                    return assets.astraError != null ? assets.astraError : assets.astraNormal;
                case AuditSeverity.Warning:
                    return assets.astraWarn != null ? assets.astraWarn : assets.astraNormal;
                default:
                    return assets.astraNormal;
            }
        }

        public static GameObject CreateFramedPortrait(Transform parent, string name, Sprite portraitSprite, float size)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            holder.transform.SetParent(parent, false);
            var le = holder.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            le.minWidth = size;
            le.minHeight = size;

            var bgImg = holder.GetComponent<Image>();
            ApplySlotFrame(bgImg, selected: false);

            if (portraitSprite != null)
            {
                var pObj = new GameObject("CharacterSprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pObj.transform.SetParent(holder.transform, false);
                var pRt = pObj.GetComponent<RectTransform>();
                pRt.anchorMin = new Vector2(0.10f, 0.10f);
                pRt.anchorMax = new Vector2(0.90f, 0.90f);
                pRt.offsetMin = Vector2.zero;
                pRt.offsetMax = Vector2.zero;

                var pImg = pObj.GetComponent<Image>();
                pImg.sprite = portraitSprite;
                pImg.preserveAspect = true;
            }

            return holder;
        }
    }
}
