using AstraLink.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Common
{
    /// <summary>
    /// Shared visual styling helpers for applying _Project/Art sprites and cohesive sci-fi palette
    /// across all screens (TopNavBar, Hub, Workshop, Flight, Debrief).
    /// </summary>
    public static class UITheme
    {
        public static readonly Color TextPrimary = new Color(0.93f, 0.97f, 1.00f, 1f);
        public static readonly Color TextSecondary = new Color(0.68f, 0.80f, 0.92f, 1f);
        public static readonly Color TextGold = new Color(1.00f, 0.84f, 0.36f, 1f);
        public static readonly Color AccentCyan = new Color(0.32f, 0.88f, 0.98f, 1f);
        public static readonly Color AccentGreen = new Color(0.30f, 0.86f, 0.56f, 1f);
        public static readonly Color AccentAmber = new Color(0.98f, 0.72f, 0.28f, 1f);
        public static readonly Color AccentRed = new Color(0.96f, 0.38f, 0.38f, 1f);
        public static readonly Color PanelTintDark = new Color(0.88f, 0.92f, 1.00f, 0.96f);
        public static readonly Color ButtonTintPrimary = Color.white;
        public static readonly Color ButtonTintSuccess = new Color(0.70f, 1.00f, 0.78f, 1f);
        public static readonly Color ButtonTintMuted = new Color(0.62f, 0.72f, 0.86f, 1f);
        public static readonly Color ButtonTintDanger = new Color(1.00f, 0.68f, 0.68f, 1f);

        private static UIThemeAssets _cachedAssets;

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
    }
}
