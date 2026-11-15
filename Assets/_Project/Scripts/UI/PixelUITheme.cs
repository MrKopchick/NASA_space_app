using System;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI
{
    /// <summary>
    /// Shared indie pixel-art UI theme applier using project assets in Assets/_Project/Art/UI and Assets/_Project/Fonts.
    /// Keeps Workshop, Autopilot (If/Else), DryRun, and R&amp;D visually cohesive with the Hub.
    /// </summary>
    public static class PixelUITheme
    {
        private static bool _loaded;
        public static Font HeaderFont { get; private set; }
        public static Font BodyFont { get; private set; }
        public static Sprite PanelFrameSprite { get; private set; }
        public static Sprite BoxFrameSprite { get; private set; }
        public static Sprite SlotFrameSprite { get; private set; }
        public static Sprite ButtonNormalSprite { get; private set; }
        public static Sprite ButtonHoverSprite { get; private set; }
        public static Sprite ButtonPressedSprite { get; private set; }
        public static Sprite ButtonDisabledSprite { get; private set; }

        public static void EnsureAssetsLoaded()
        {
            if (_loaded && HeaderFont != null && PanelFrameSprite != null) return;
            _loaded = true;

#if UNITY_EDITOR
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/inventory_panel.png", new Vector4(6, 6, 6, 6));
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/dialogue_box.png", new Vector4(6, 6, 6, 6));
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/item_slot.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/button_normal.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/button_hover.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/button_pressed.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/button_disabled.png", new Vector4(5, 5, 5, 5));

            HeaderFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/Kenney Future.ttf");
            BodyFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/Kenney Mini Square Mono.ttf");

            PanelFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/inventory_panel.png");
            BoxFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/dialogue_box.png");
            SlotFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/item_slot.png");

            ButtonNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_normal.png");
            ButtonHoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_hover.png");
            ButtonPressedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_pressed.png");
            ButtonDisabledSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_disabled.png");
#endif
        }

        /// <summary>
        /// Upgrades an existing UI hierarchy (Workshop, Autopilot, DryRun, R&amp;D) to the project's pixel-art sprites &amp; Kenney fonts.
        /// Safe to call multiple times (idempotent).
        /// </summary>
        public static void ApplyToHierarchy(Transform root)
        {
            if (root == null) return;
            EnsureAssetsLoaded();

            // 1. Apply Kenney fonts to all Text elements
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text t = texts[i];
                if (t == null) continue;

                bool isHeaderOrButton = t.fontStyle == FontStyle.Bold || t.GetComponentInParent<Button>() != null;
                Font targetFont = isHeaderOrButton
                    ? (HeaderFont ?? BodyFont)
                    : (BodyFont ?? HeaderFont);

                if (targetFont != null)
                {
                    t.font = targetFont;
                }
            }

            // 2. Upgrade Buttons to 9-sliced pixel-art sprites
            if (ButtonNormalSprite != null)
            {
                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Button btn = buttons[i];
                    if (btn == null) continue;

                    Image img = btn.GetComponent<Image>();
                    if (img != null)
                    {
                        // Keep subtle tint for semantic buttons (green launch, red clear/close, orange dry-run)
                        Color c = img.color;
                        bool isStronglyColored = (c.r > 0.45f && c.g < 0.35f) || (c.g > 0.45f && c.r < 0.30f);

                        img.sprite = ButtonNormalSprite;
                        img.type = Image.Type.Sliced;
                        img.pixelsPerUnitMultiplier = 0.35f;
                        img.color = isStronglyColored
                            ? Color.Lerp(Color.white, new Color(Mathf.Min(1f, c.r * 1.45f), Mathf.Min(1f, c.g * 1.45f), Mathf.Min(1f, c.b * 1.45f), 1f), 0.55f)
                            : Color.white;

                        Text btnText = btn.GetComponentInChildren<Text>(true);
                        if (btnText != null)
                        {
                            btnText.color = isStronglyColored
                                ? Color.white
                                : new Color(0.06f, 0.12f, 0.18f, 1f);
                        }
                    }

                    if (ButtonHoverSprite != null || ButtonPressedSprite != null || ButtonDisabledSprite != null)
                    {
                        btn.transition = Selectable.Transition.SpriteSwap;
                        SpriteState st = btn.spriteState;
                        if (ButtonHoverSprite != null) st.highlightedSprite = ButtonHoverSprite;
                        if (ButtonPressedSprite != null) st.pressedSprite = ButtonPressedSprite;
                        if (ButtonDisabledSprite != null) st.disabledSprite = ButtonDisabledSprite;
                        btn.spriteState = st;
                    }
                }
            }

            // 3. Upgrade container panels & boxes to 9-sliced dialogue_box / inventory_panel / item_slot
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img == null || img.sprite != null) continue;
                if (img.GetComponent<Button>() != null) continue;

                string n = img.gameObject.name;
                // Skip thin fill bars / progress bars / dividers / full-screen dim overlays
                if (n.IndexOf("Fill", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Bar", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("Top", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Header", StringComparison.OrdinalIgnoreCase) < 0 ||
                    n.IndexOf("Overlay", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Divider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Spacer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Curve", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Dot", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (n.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0 && SlotFrameSprite != null && n.IndexOf("Row", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    ApplySlicedSprite(img, SlotFrameSprite, Color.white);
                }
                else if (n.IndexOf("ModalWindow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Column", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Node_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("RuleCard", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ApplySlicedSprite(img, PanelFrameSprite, Color.white);
                }
                else if (n.IndexOf("Top", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Header", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Row", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Box", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Advisory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Summary", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ApplySlicedSprite(img, BoxFrameSprite, Color.white);
                }
            }

            // 4. Add safe inner padding on LayoutGroups whose panel just got a 9-slice border
            LayoutGroup[] groups = root.GetComponentsInChildren<LayoutGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                LayoutGroup lg = groups[i];
                if (lg == null) continue;
                Image bg = lg.GetComponent<Image>();
                if (bg != null && (bg.sprite == PanelFrameSprite || bg.sprite == BoxFrameSprite))
                {
                    RectOffset p = lg.padding;
                    lg.padding = new RectOffset(
                        Mathf.Max(p.left, 18),
                        Mathf.Max(p.right, 18),
                        Mathf.Max(p.top, 16),
                        Mathf.Max(p.bottom, 16));
                }
            }
        }

        private static void ApplySlicedSprite(Image img, Sprite sprite, Color tint)
        {
            if (img == null || sprite == null) return;
            if (sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.35f;
            img.color = tint;
        }

#if UNITY_EDITOR
        private static void ConfigureSpriteInEditor(string assetPath, Vector4 border)
        {
            var importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }
            if (importer.textureCompression != UnityEditor.TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (border != Vector4.zero && importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }
#endif
    }
}
