using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Flight.SpaceMap
{
    /// <summary>
    /// Centralized ScriptableObject + runtime cache providing crisp pixel-art UI frames, icons, portraits,
    /// spacecraft sprites, audio clips, and 60-frame animated GIF sprite sheets for the Solar System flight view.
    /// </summary>
    [CreateAssetMenu(fileName = "SolarSystemArtBank", menuName = "AstraLink/Solar System Art Bank")]
    public class SolarSystemArtBank : ScriptableObject
    {
        public readonly struct SheetSpec
        {
            public readonly string ResourceName;
            public readonly int Columns;
            public readonly int Rows;
            public readonly int CellWidth;
            public readonly int CellHeight;
            public readonly int FrameCount;
            public readonly float Fps;

            public SheetSpec(string resourceName, int columns, int rows, int cellWidth, int cellHeight, int frameCount, float fps)
            {
                ResourceName = resourceName;
                Columns = columns;
                Rows = rows;
                CellWidth = cellWidth;
                CellHeight = cellHeight;
                FrameCount = frameCount;
                Fps = fps;
            }
        }

        [Header("UI Frames & Chrome (Assets/_Project/Art/UI)")]
        public Sprite buttonNormal;
        public Sprite buttonHover;
        public Sprite buttonPressed;
        public Sprite buttonDisabled;
        public Sprite inventoryPanel;
        public Sprite dialogueBox;
        public Sprite itemSlot;
        public Sprite itemSlotSelected;
        public Sprite nameTab;
        public Sprite controlPanel;
        public Sprite radarScreen;
        public Sprite missionBadge;

        [Header("Indicator Icons (Assets/_Project/Art/UI/Indicators)")]
        public Sprite starMapTablet;
        public Sprite starChart;
        public Sprite countdownClock;
        public Sprite holoProjector;
        public Sprite oxygenGauge;

        [Header("Hardware Icons (Assets/_Project/Art/Icons)")]
        public Sprite iconEnergyCell;
        public Sprite iconDataChip;
        public Sprite iconDishAntenna;
        public Sprite iconShieldGenerator;
        public Sprite iconSpaceTelescope;
        public Sprite iconSolarPanel;

        [Header("Portraits & Spacecraft")]
        public Sprite astraPortrait;
        public Sprite controllerPortrait;
        public Sprite cubesatSprite;
        public Sprite satelliteSprite;
        public Sprite deepSpaceProbeSprite;
        public Sprite smallRocketSprite;
        public Sprite reusableRocketSprite;
        public Sprite heavyLiftRocketSprite;
        public Sprite rocketFlameSprite;
        public Sprite launchSmokeSprite;

        [Header("Audio Clips (Assets/_Project/Audio)")]
        public AudioClip flightMusic;
        public AudioClip missionAmbience;
        public AudioClip uiClick;
        public AudioClip uiToggle;
        public AudioClip astraNotification;
        public AudioClip solarStormAlarm;
        public AudioClip missionStart;
        public AudioClip missionSuccess;

        [Header("UI Fonts (Assets/_Project/Fonts)")]
        public Font uiPixelFont;

        private static SolarSystemArtBank _cachedBank;
        private static readonly Dictionary<string, Sprite[]> _sheetFramesCache = new Dictionary<string, Sprite[]>(StringComparer.Ordinal);
        private static readonly Dictionary<int, Sprite> _slicedSpriteCache = new Dictionary<int, Sprite>();

        private static readonly Dictionary<string, SheetSpec> _sheetSpecs = new Dictionary<string, SheetSpec>(StringComparer.Ordinal)
        {
            { "sun_sheet", new SheetSpec("SolarSystem/Sheets/sun_sheet", 10, 6, 150, 150, 60, 7.0f) },
            { "mercury_sheet", new SheetSpec("SolarSystem/Sheets/mercury_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "venus_sheet", new SheetSpec("SolarSystem/Sheets/venus_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "earth_sheet", new SheetSpec("SolarSystem/Sheets/earth_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "moon_sheet", new SheetSpec("SolarSystem/Sheets/moon_sheet", 10, 6, 100, 100, 60, 5.5f) },
            { "mars_sheet", new SheetSpec("SolarSystem/Sheets/mars_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "jupiter_sheet", new SheetSpec("SolarSystem/Sheets/jupiter_sheet", 10, 6, 100, 100, 60, 6.5f) },
            { "io_sheet", new SheetSpec("SolarSystem/Sheets/io_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "saturn_sheet", new SheetSpec("SolarSystem/Sheets/saturn_sheet", 8, 8, 232, 196, 60, 6.0f) },
            { "uranus_sheet", new SheetSpec("SolarSystem/Sheets/uranus_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "neptune_sheet", new SheetSpec("SolarSystem/Sheets/neptune_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "asteroid_0_sheet", new SheetSpec("SolarSystem/Sheets/asteroid_0_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "asteroid_1_sheet", new SheetSpec("SolarSystem/Sheets/asteroid_1_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "asteroid_2_sheet", new SheetSpec("SolarSystem/Sheets/asteroid_2_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "asteroid_3_sheet", new SheetSpec("SolarSystem/Sheets/asteroid_3_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "asteroid_4_sheet", new SheetSpec("SolarSystem/Sheets/asteroid_4_sheet", 10, 6, 100, 100, 60, 6.0f) },
            { "asteroid_5_sheet", new SheetSpec("SolarSystem/Sheets/asteroid_5_sheet", 10, 6, 100, 100, 60, 6.0f) }
        };

        /// <summary>
        /// Loads the shared art/audio catalog from Resources.
        /// </summary>
        public static SolarSystemArtBank Load()
        {
            if (_cachedBank == null)
            {
                _cachedBank = Resources.Load<SolarSystemArtBank>("SolarSystem/SolarSystemArtBank");
            }
            return _cachedBank;
        }

        /// <summary>
        /// Returns the shared Kenney Pixel UI font (falling back to LegacyRuntime if unassigned).
        /// </summary>
        public static Font GetPixelFont()
        {
            SolarSystemArtBank bank = Load();
            if (bank != null && bank.uiPixelFont != null)
            {
                return bank.uiPixelFont;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>
        /// Returns all sliced animated frames for a celestial or asteroid sprite sheet.
        /// </summary>
        public static Sprite[] GetAnimatedFrames(string sheetKey, out float recommendedFps)
        {
            recommendedFps = 6.0f;
            if (string.IsNullOrEmpty(sheetKey))
            {
                return null;
            }

            if (!_sheetSpecs.TryGetValue(sheetKey, out SheetSpec spec))
            {
                return null;
            }

            recommendedFps = spec.Fps;

            if (_sheetFramesCache.TryGetValue(sheetKey, out Sprite[] cached) && cached != null && cached.Length > 0 && cached[0] != null)
            {
                return cached;
            }

            Texture2D tex = Resources.Load<Texture2D>(spec.ResourceName);
            if (tex == null)
            {
                return null;
            }

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Sprite[] frames = new Sprite[spec.FrameCount];
            for (int i = 0; i < spec.FrameCount; i++)
            {
                int col = i % spec.Columns;
                int rowFromTop = i / spec.Columns;
                float x = col * spec.CellWidth;
                float y = tex.height - (rowFromTop + 1) * spec.CellHeight;
                if (x + spec.CellWidth > tex.width || y < 0f || y + spec.CellHeight > tex.height)
                {
                    break;
                }

                Sprite sp = Sprite.Create(
                    tex,
                    new Rect(x, y, spec.CellWidth, spec.CellHeight),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                sp.name = sheetKey + "_" + i;
                frames[i] = sp;
            }

            _sheetFramesCache[sheetKey] = frames;
            return frames;
        }

        /// <summary>
        /// Returns a cached 9-sliced runtime Sprite preserving pixel-art borders without touching shared texture metas.
        /// </summary>
        public static Sprite GetSlicedSprite(Sprite source, Vector4 borderPx)
        {
            if (source == null || source.texture == null)
            {
                return null;
            }

            int key = source.GetInstanceID() * 397 ^ borderPx.GetHashCode();
            if (_slicedSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            source.texture.filterMode = FilterMode.Point;
            Sprite sliced = Sprite.Create(
                source.texture,
                source.rect,
                new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit > 0f ? source.pixelsPerUnit : 100f,
                0,
                SpriteMeshType.FullRect,
                borderPx);
            sliced.name = source.name + "_Sliced";
            _slicedSpriteCache[key] = sliced;
            return sliced;
        }

        /// <summary>
        /// Applies a pixel-art 9-sliced frame onto a UI Image while keeping light tint so artwork stays vibrant.
        /// </summary>
        public static void ApplyPanelFrame(Image target, Sprite source, Vector4 borderPx, Color tint)
        {
            if (target == null) return;
            Sprite sliced = GetSlicedSprite(source, borderPx);
            if (sliced != null)
            {
                target.sprite = sliced;
                target.type = Image.Type.Sliced;
                target.pixelsPerUnitMultiplier = 0.5f;
                target.color = tint;
            }
            else
            {
                target.color = tint;
            }
        }

        /// <summary>
        /// Applies normal/hover/pressed/disabled pixel-art button sprites to a Unity UI Button.
        /// </summary>
        public void ApplyButtonSprites(Button button, Color normalTint)
        {
            if (button == null) return;
            Image img = button.GetComponent<Image>();
            if (img == null) return;

            ApplyPanelFrame(img, buttonNormal, new Vector4(8f, 8f, 8f, 8f), normalTint);

            if (buttonHover != null || buttonPressed != null || buttonDisabled != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState st = button.spriteState;
                if (buttonHover != null) st.highlightedSprite = GetSlicedSprite(buttonHover, new Vector4(8f, 8f, 8f, 8f));
                if (buttonPressed != null) st.pressedSprite = GetSlicedSprite(buttonPressed, new Vector4(8f, 8f, 8f, 8f));
                if (buttonDisabled != null) st.disabledSprite = GetSlicedSprite(buttonDisabled, new Vector4(8f, 8f, 8f, 8f));
                button.spriteState = st;
            }
        }
    }
}
