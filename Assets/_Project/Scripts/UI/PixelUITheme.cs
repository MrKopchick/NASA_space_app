using System;
using System.Collections.Generic;
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
        private static readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public static Font HeaderFont { get; private set; }
        public static Font BodyFont { get; private set; }
        public static Sprite PanelFrameSprite { get; private set; }
        public static Sprite BoxFrameSprite { get; private set; }
        public static Sprite SlotFrameSprite { get; private set; }
        public static Sprite CoinSprite { get; private set; }
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
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/space_credits_coin.png", Vector4.zero);
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/button_normal.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/button_hover.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/button_pressed.png", new Vector4(5, 5, 5, 5));
            ConfigureSpriteInEditor("Assets/_Project/Art/UI/button_disabled.png", new Vector4(5, 5, 5, 5));

            HeaderFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/Kenney Future.ttf");
            BodyFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/Kenney Mini Square Mono.ttf");

            PanelFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/inventory_panel.png");
            BoxFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/dialogue_box.png");
            SlotFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/item_slot.png");
            CoinSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/space_credits_coin.png");

            ButtonNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_normal.png");
            ButtonHoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_hover.png");
            ButtonPressedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_pressed.png");
            ButtonDisabledSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_disabled.png");
#endif
        }

        /// <summary>
        /// Resolves a crisp pixel-art sprite for any satellite module, rocket, or CPU chip ID.
        /// </summary>
        public static Sprite ResolvePartIconSprite(string idOrCategory)
        {
            if (string.IsNullOrEmpty(idOrCategory)) return null;
            EnsureAssetsLoaded();

            if (_iconCache.TryGetValue(idOrCategory, out Sprite cached) && cached != null)
            {
                return cached;
            }

            string path = ResolvePartIconPath(idOrCategory);
            if (string.IsNullOrEmpty(path)) return null;

#if UNITY_EDITOR
            ConfigureSpriteInEditor(path, Vector4.zero);
            Sprite loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (loaded != null)
            {
                if (loaded.texture != null) loaded.texture.filterMode = FilterMode.Point;
                _iconCache[idOrCategory] = loaded;
                return loaded;
            }
#endif
            return null;
        }

        private static string ResolvePartIconPath(string rawId)
        {
            string id = rawId.ToLowerInvariant();

            // Rockets
            if (id.Contains("rocket_light") || id.Contains("rkt_light")) return "Assets/_Project/Art/Spacecraft/small_rocket.png";
            if (id.Contains("rocket_medium")) return "Assets/_Project/Art/Spacecraft/reusable_rocket.png";
            if (id.Contains("rocket_heavy")) return "Assets/_Project/Art/Spacecraft/heavy_lift_rocket.png";
            if (id.Contains("rocket") || id.Contains("rkt")) return "Assets/_Project/Art/Spacecraft/small_rocket.png";

            // Autopilot / CPU / Tools
            if (id.Contains("chip") || id.Contains("cpu")) return "Assets/_Project/Art/Icons/data_chip.png";
            if (id.Contains("reset") || id.Contains("wrench")) return "Assets/_Project/Art/Icons/repair_wrench.png";

            // Core Platform / Bus
            if (id.Contains("cubesat") || id == "bus_1") return "Assets/_Project/Art/Spacecraft/cubesat.png";
            if (id.Contains("smallsat")) return "Assets/_Project/Art/Spacecraft/satellite.png";
            if (id.Contains("deepspace")) return "Assets/_Project/Art/Spacecraft/deep_space_probe.png";
            if (id.StartsWith("bus_") || id.Contains("bus")) return "Assets/_Project/Art/Icons/Modules/hull_plate.png";

            // Power Generation
            if (id.Contains("rtg") || id.Contains("nuclear") || id.Contains("core")) return "Assets/_Project/Art/Icons/power_core.png";
            if (id.StartsWith("pwr_") || id.Contains("solar")) return "Assets/_Project/Art/Icons/solar_panel.png";

            // Energy Storage
            if (id.Contains("solid") || id.Contains("heavy")) return "Assets/_Project/Art/Icons/Modules/plasma_crystal.png";
            if (id.StartsWith("bat_") || id.Contains("battery")) return "Assets/_Project/Art/Icons/energy_cell.png";

            // Comms & Storage
            if (id.StartsWith("com_") || id.StartsWith("comm_") || id.StartsWith("comms_") || id.Contains("comms") || id.Contains("antenna"))
                return "Assets/_Project/Art/Icons/dish_antenna.png";

            // Science Payloads
            if (id.Contains("radar") || id.Contains("sar")) return "Assets/_Project/Art/Icons/Modules/space_beacon.png";
            if (id.Contains("spectrometer")) return "Assets/_Project/Art/Icons/Modules/gold_mirror_telescope.png";
            if (id.Contains("magnetometer")) return "Assets/_Project/Art/Icons/Modules/warp_drive.png";
            if (id.StartsWith("sci_") || id.Contains("camera") || id.Contains("sensor")) return "Assets/_Project/Art/Icons/space_telescope.png";

            return null;
        }

        /// <summary>
        /// Resolves a crisp pixel-art icon for the 6 NASA telemetry bars (0..5).
        /// </summary>
        public static Sprite ResolveTelemetryIconSprite(int gaugeIndex)
        {
            string key = "telemetry_" + gaugeIndex;
            if (_iconCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            string path = gaugeIndex switch
            {
                0 => "Assets/_Project/Art/Spacecraft/small_rocket.png",
                1 => "Assets/_Project/Art/UI/space_credits_coin.png",
                2 => "Assets/_Project/Art/Icons/energy_cell.png",
                3 => "Assets/_Project/Art/Icons/data_chip.png",
                4 => "Assets/_Project/Art/Icons/dish_antenna.png",
                5 => "Assets/_Project/Art/Icons/shield_generator.png",
                _ => "Assets/_Project/Art/Icons/data_chip.png"
            };

#if UNITY_EDITOR
            ConfigureSpriteInEditor(path, Vector4.zero);
            Sprite loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (loaded != null)
            {
                if (loaded.texture != null) loaded.texture.filterMode = FilterMode.Point;
                _iconCache[key] = loaded;
                return loaded;
            }
#endif
            return null;
        }

        /// <summary>
        /// Resolves a crisp planet/badge sprite for a mission or orbit ID.
        /// </summary>
        public static Sprite ResolveMissionIconSprite(string missionOrOrbitId)
        {
            string id = (missionOrOrbitId ?? string.Empty).ToLowerInvariant();
            string key = "mission_" + id;
            if (_iconCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            string path = "Assets/_Project/Art/Planets/earth.png";
            if (id.Contains("lunar") || id.Contains("moon") || id.Contains("m2"))
                path = "Assets/_Project/Art/Planets/moon.png";
            else if (id.Contains("mars") || id.Contains("m3"))
                path = "Assets/_Project/Art/Planets/mars.png";
            else if (id.Contains("venus") || id.Contains("deep") || id.Contains("m4"))
                path = "Assets/_Project/Art/UI/mission_badge.png";

#if UNITY_EDITOR
            ConfigureSpriteInEditor(path, Vector4.zero);
            Sprite loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (loaded != null)
            {
                if (loaded.texture != null) loaded.texture.filterMode = FilterMode.Point;
                _iconCache[key] = loaded;
                return loaded;
            }
#endif
            return null;
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
                    bool isStronglyColored = false;
                    if (img != null)
                    {
                        // Keep clear semantic tint for launch (green) and reset/close (red)
                        Color c = img.color;
                        bool isGreen = c.g > 0.45f && c.r < 0.30f;
                        bool isRed = c.r > 0.45f && c.g < 0.35f;
                        isStronglyColored = isGreen || isRed;

                        LayoutElement btnLe = btn.GetComponent<LayoutElement>();
                        bool isCompact = btnLe != null && ((btnLe.preferredHeight > 0f && btnLe.preferredHeight <= 42f) ||
                                                           (btnLe.preferredWidth > 0f && btnLe.preferredWidth <= 50f));

                        img.sprite = (isStronglyColored && ButtonDisabledSprite != null)
                            ? ButtonDisabledSprite
                            : ButtonNormalSprite;
                        img.type = Image.Type.Sliced;
                        img.pixelsPerUnitMultiplier = isCompact ? 0.65f : 0.42f;
                        if (isGreen)
                            img.color = new Color(0.18f, 0.80f, 0.34f, 1f);
                        else if (isRed)
                            img.color = new Color(0.88f, 0.24f, 0.26f, 1f);
                        else
                            img.color = Color.white;

                        Text btnText = btn.GetComponentInChildren<Text>(true);
                        if (btnText != null)
                        {
                            btnText.color = isStronglyColored
                                ? Color.white
                                : new Color(0.06f, 0.12f, 0.18f, 1f);
                        }
                    }

                    if (isStronglyColored)
                    {
                        btn.transition = Selectable.Transition.ColorTint;
                        ColorBlock cb = btn.colors;
                        cb.normalColor = Color.white;
                        cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
                        cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
                        btn.colors = cb;
                    }
                    else if (ButtonHoverSprite != null || ButtonPressedSprite != null || ButtonDisabledSprite != null)
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

                if (n.IndexOf("IconSprite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("CoinIcon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("PlanetIcon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("TelemetryIcon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if ((n.IndexOf("IconFrame", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.IndexOf("IconSlot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     (n.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("Row", StringComparison.OrdinalIgnoreCase) < 0))
                    && SlotFrameSprite != null)
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
                else if (n.IndexOf("GaugeRow_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("ValidationBanner", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ApplySlicedSprite(img, BoxFrameSprite, img.color == Color.clear ? Color.white : img.color);
                    img.pixelsPerUnitMultiplier = 0.55f;
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
                    string goName = lg.gameObject.name;
                    bool isCompact = goName.StartsWith("Node_", StringComparison.OrdinalIgnoreCase) ||
                                     goName.StartsWith("GaugeRow_", StringComparison.OrdinalIgnoreCase) ||
                                     goName.Equals("ValidationBanner", StringComparison.OrdinalIgnoreCase);
                    int minH = isCompact ? 12 : 18;
                    int minV = isCompact ? 8 : 16;
                    RectOffset p = lg.padding;
                    lg.padding = new RectOffset(
                        Mathf.Max(p.left, minH),
                        Mathf.Max(p.right, minH),
                        Mathf.Max(p.top, minV),
                        Mathf.Max(p.bottom, minV));
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
