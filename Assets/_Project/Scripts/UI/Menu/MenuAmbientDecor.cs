using System.Collections.Generic;
using AstraLink.UI.Flight.SpaceMap;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Menu
{
    /// <summary>
    /// Additive, non-destructive ambient space layer for the Main Menu.
    /// Renders twinkling pixel stars, mouse-parallax depth layers, drifting nebula glow,
    /// animated celestial bodies, tumbling asteroids, a periodic rocket flyby with exhaust puffs,
    /// radio-ping ripples, and corner telemetry badges using SolarSystemArtBank assets
    /// without modifying any existing MenuCanvas elements.
    /// </summary>
    public class MenuAmbientDecor : MonoBehaviour
    {
        private struct StarNode
        {
            public RectTransform Rect;
            public Image Img;
            public Vector2 BaseOffset;
            public float BaseAlpha;
            public float Phase;
            public float Speed;
            public float DriftX;
            public float Depth;
        }

        private struct AsteroidNode
        {
            public RectTransform Rect;
            public Image Img;
            public Sprite[] Frames;
            public float Fps;
            public float AnimTime;
            public Vector2 Pos;
            public Vector2 Velocity;
            public float RotSpeed;
            public float Depth;
        }

        private struct SmokePuff
        {
            public RectTransform Rect;
            public Image Img;
            public float Age;
            public float Lifetime;
            public Vector2 Drift;
        }

        private readonly List<StarNode> _stars = new List<StarNode>();
        private readonly List<AsteroidNode> _asteroids = new List<AsteroidNode>();
        private readonly List<SmokePuff> _smokePuffs = new List<SmokePuff>();

        private SolarSystemArtBank _artBank;
        private RectTransform _layerRect;
        private Vector2 _parallaxOffset;

        // Soft drifting nebula glows
        private RectTransform _nebulaLeftRect;
        private RectTransform _nebulaRightRect;

        // Animated celestial bodies
        private RectTransform _earthRect;
        private Vector2 _earthBasePos;
        private Image _earthImg;
        private Sprite[] _earthFrames;
        private float _earthFps = 6f;
        private float _earthAnimTime;
        private RectTransform _earthOrbitSatRect;
        private float _satOrbitAngle;

        // Satellite radio-ping ring
        private RectTransform _pingRingRect;
        private Image _pingRingImg;
        private float _pingProgress = 0f;

        private RectTransform _saturnRect;
        private Image _saturnImg;
        private Sprite[] _saturnFrames;
        private float _saturnFps = 6f;
        private float _saturnAnimTime;

        private RectTransform _probeRect;
        private float _probePhase;

        // Periodic distant rocket flyby
        private RectTransform _flybyRocketRect;
        private RectTransform _flybyFlameRect;
        private Image _flybyRocketImg;
        private Image _flybyFlameImg;
        private float _flybyCooldown = 4.5f;
        private float _flybyProgress = -1f;
        private float _smokeSpawnTimer;
        private Vector2 _flybyStart;
        private Vector2 _flybyEnd;

        // Shooting star
        private RectTransform _shootingStarRect;
        private Image _shootingStarImg;
        private float _shootingTimer = 2.0f;
        private float _shootingActiveProgress = -1f;
        private Vector2 _shootStart;
        private Vector2 _shootEnd;

        // Corner telemetry badges
        private Image _beaconDotImg;
        private Text _relayStatusText;
        private Text _astraStatusText;
        private float _statusCycleTimer;
        private int _statusIdx;

        private static Sprite _softDiscSprite;

        private static readonly string[] RelayStatuses =
        {
            "ORBITAL RELAY: ONLINE",
            "SOLAR FLUX: 1361 W/M2",
            "DEEP SPACE NET: LOCKED",
            "TELEMETRY LINK: 99.8%"
        };

        private static readonly string[] AstraStatuses =
        {
            "ASTRA: ALL GYROS ALIGNED",
            "ASTRA: SOLAR ARRAYS NOMINAL",
            "ASTRA: STAR TRACKER LOCKED",
            "ASTRA: READY FOR LAUNCH"
        };

        public static void AttachIfNeeded(MainMenuUI menu, Image backgroundImage)
        {
            if (menu == null)
            {
                return;
            }

            Canvas canvas = backgroundImage != null ? backgroundImage.canvas : menu.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
            if (canvas == null)
            {
                return;
            }

            if (canvas.GetComponentInChildren<MenuAmbientDecor>(true) != null)
            {
                return;
            }

            GameObject layerObj = new GameObject("MenuAmbientLayer", typeof(RectTransform), typeof(MenuAmbientDecor));
            layerObj.transform.SetParent(canvas.transform, false);

            // Place directly above BackgroundImage (and any CrossfadeOverlay) but strictly BEHIND CenterCard & FooterText.
            int targetIndex = 1;
            if (backgroundImage != null && backgroundImage.transform.parent == canvas.transform)
            {
                targetIndex = backgroundImage.transform.GetSiblingIndex() + 1;
                Transform crossfade = canvas.transform.Find("BackgroundCrossfadeOverlay");
                if (crossfade != null && crossfade.GetSiblingIndex() >= targetIndex)
                {
                    targetIndex = crossfade.GetSiblingIndex() + 1;
                }
            }
            layerObj.transform.SetSiblingIndex(Mathf.Clamp(targetIndex, 0, canvas.transform.childCount - 1));

            RectTransform rt = layerObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            MenuAmbientDecor decor = layerObj.GetComponent<MenuAmbientDecor>();
            decor.Build(rt);
        }

        private void Build(RectTransform root)
        {
            _layerRect = root;
            _artBank = SolarSystemArtBank.Load();
            EnsureSoftDiscSprite();

            BuildNebulaGlows(root);
            BuildStarfield(root);
            BuildScanlines(root);
            BuildShootingStar(root);
            BuildDriftingAsteroids(root);
            BuildAnimatedPlanetsAndCraft(root);
            BuildRocketFlyby(root);
            BuildCornerTelemetryBadges(root);
        }

        private void BuildNebulaGlows(Transform parent)
        {
            _nebulaLeftRect = CreateGlowBlob("NebulaGlow_Left", parent, new Vector2(0.18f, 0.58f), new Vector2(540f, 540f), new Color(0.14f, 0.52f, 0.92f, 0.14f));
            _nebulaRightRect = CreateGlowBlob("NebulaGlow_Right", parent, new Vector2(0.84f, 0.36f), new Vector2(620f, 620f), new Color(0.56f, 0.24f, 0.86f, 0.13f));
        }

        private static RectTransform CreateGlowBlob(string name, Transform parent, Vector2 anchor, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            img.sprite = _softDiscSprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private void BuildStarfield(Transform parent)
        {
            Color[] palette =
            {
                new Color(0.86f, 0.94f, 1.00f, 1f),
                new Color(0.62f, 0.88f, 1.00f, 1f),
                new Color(1.00f, 0.92f, 0.72f, 1f),
                new Color(0.80f, 0.78f, 1.00f, 1f)
            };

            for (int i = 0; i < 64; i++)
            {
                GameObject starObj = new GameObject("Star_" + i, typeof(RectTransform), typeof(Image));
                starObj.transform.SetParent(parent, false);

                RectTransform rt = starObj.GetComponent<RectTransform>();
                float ax = Random.Range(0.02f, 0.98f);
                float ay = Random.Range(0.04f, 0.96f);
                rt.anchorMin = new Vector2(ax, ay);
                rt.anchorMax = new Vector2(ax, ay);
                rt.pivot = new Vector2(0.5f, 0.5f);
                float size = (i % 9 == 0) ? 4f : (i % 3 == 0 ? 3f : 2f);
                rt.sizeDelta = new Vector2(size, size);

                Image img = starObj.GetComponent<Image>();
                Color c = palette[i % palette.Length];
                float baseA = Random.Range(0.25f, 0.90f);
                c.a = baseA;
                img.color = c;
                img.raycastTarget = false;

                _stars.Add(new StarNode
                {
                    Rect = rt,
                    Img = img,
                    BaseOffset = Vector2.zero,
                    BaseAlpha = baseA,
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    Speed = Random.Range(1.2f, 3.6f),
                    DriftX = Random.Range(2f, 7f),
                    Depth = Random.Range(0.25f, 0.85f)
                });
            }
        }

        private void BuildScanlines(Transform parent)
        {
            Texture2D tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.SetPixels32(new[]
            {
                new Color32(0, 0, 0, 0),
                new Color32(0, 0, 0, 0),
                new Color32(120, 210, 255, 16),
                new Color32(0, 0, 0, 0)
            });
            tex.Apply();

            GameObject obj = new GameObject("SubtleScanlines", typeof(RectTransform), typeof(RawImage));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            RawImage raw = obj.GetComponent<RawImage>();
            raw.texture = tex;
            raw.uvRect = new Rect(0f, 0f, 1f, 240f);
            raw.color = new Color(1f, 1f, 1f, 0.55f);
            raw.raycastTarget = false;
        }

        private void BuildShootingStar(Transform parent)
        {
            GameObject obj = new GameObject("ShootingStar", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            _shootingStarRect = obj.GetComponent<RectTransform>();
            _shootingStarRect.anchorMin = new Vector2(0.5f, 0.5f);
            _shootingStarRect.anchorMax = new Vector2(0.5f, 0.5f);
            _shootingStarRect.sizeDelta = new Vector2(62f, 2f);

            _shootingStarImg = obj.GetComponent<Image>();
            _shootingStarImg.color = new Color(0.75f, 0.96f, 1f, 0f);
            _shootingStarImg.raycastTarget = false;
        }

        private void BuildDriftingAsteroids(Transform parent)
        {
            string[] keys = { "asteroid_0_sheet", "asteroid_2_sheet", "asteroid_4_sheet" };
            Vector2[] startPos =
            {
                new Vector2(-760f, 340f),
                new Vector2(680f, -310f),
                new Vector2(-520f, -360f)
            };
            Vector2[] velocities =
            {
                new Vector2(18f, -6f),
                new Vector2(-22f, 8f),
                new Vector2(14f, 5f)
            };

            for (int i = 0; i < keys.Length; i++)
            {
                Sprite[] frames = SolarSystemArtBank.GetAnimatedFrames(keys[i], out float fps);
                if (frames == null || frames.Length == 0) continue;

                GameObject obj = new GameObject("DriftingAsteroid_" + i, typeof(RectTransform), typeof(Image));
                obj.transform.SetParent(parent, false);
                RectTransform rt = obj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                float size = 28f + i * 8f;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = startPos[i];

                Image img = obj.GetComponent<Image>();
                img.sprite = frames[0];
                img.preserveAspect = true;
                img.color = new Color(0.86f, 0.90f, 0.98f, 0.72f);
                img.raycastTarget = false;

                _asteroids.Add(new AsteroidNode
                {
                    Rect = rt,
                    Img = img,
                    Frames = frames,
                    Fps = fps,
                    AnimTime = Random.Range(0f, 4f),
                    Pos = startPos[i],
                    Velocity = velocities[i],
                    RotSpeed = (i % 2 == 0 ? 1f : -1f) * Random.Range(14f, 28f),
                    Depth = 0.55f + i * 0.15f
                });
            }
        }

        private void BuildAnimatedPlanetsAndCraft(Transform parent)
        {
            // Left floating animated Earth + orbiting satellite + radio ping ring
            _earthFrames = SolarSystemArtBank.GetAnimatedFrames("earth_sheet", out _earthFps);
            if (_earthFrames != null && _earthFrames.Length > 0)
            {
                GameObject earthObj = new GameObject("AmbientEarth", typeof(RectTransform), typeof(Image));
                earthObj.transform.SetParent(parent, false);
                _earthRect = earthObj.GetComponent<RectTransform>();
                _earthRect.anchorMin = new Vector2(0.14f, 0.54f);
                _earthRect.anchorMax = new Vector2(0.14f, 0.54f);
                _earthRect.sizeDelta = new Vector2(146f, 146f);
                _earthBasePos = Vector2.zero;

                _earthImg = earthObj.GetComponent<Image>();
                _earthImg.sprite = _earthFrames[0];
                _earthImg.preserveAspect = true;
                _earthImg.color = new Color(1f, 1f, 1f, 0.90f);
                _earthImg.raycastTarget = false;

                if (_artBank != null && (_artBank.satelliteSprite != null || _artBank.cubesatSprite != null))
                {
                    GameObject satObj = new GameObject("OrbitingSatellite", typeof(RectTransform), typeof(Image));
                    satObj.transform.SetParent(earthObj.transform, false);
                    _earthOrbitSatRect = satObj.GetComponent<RectTransform>();
                    _earthOrbitSatRect.anchorMin = new Vector2(0.5f, 0.5f);
                    _earthOrbitSatRect.anchorMax = new Vector2(0.5f, 0.5f);
                    _earthOrbitSatRect.sizeDelta = new Vector2(30f, 30f);

                    Image satImg = satObj.GetComponent<Image>();
                    satImg.sprite = _artBank.satelliteSprite != null ? _artBank.satelliteSprite : _artBank.cubesatSprite;
                    satImg.preserveAspect = true;
                    satImg.color = new Color(0.95f, 0.98f, 1f, 0.94f);
                    satImg.raycastTarget = false;

                    // Radio ping ring anchored to satellite
                    GameObject pingObj = new GameObject("SatPingRing", typeof(RectTransform), typeof(Image));
                    pingObj.transform.SetParent(satObj.transform, false);
                    _pingRingRect = pingObj.GetComponent<RectTransform>();
                    _pingRingRect.anchorMin = new Vector2(0.5f, 0.5f);
                    _pingRingRect.anchorMax = new Vector2(0.5f, 0.5f);
                    _pingRingRect.sizeDelta = new Vector2(18f, 18f);

                    _pingRingImg = pingObj.GetComponent<Image>();
                    _pingRingImg.sprite = _softDiscSprite;
                    _pingRingImg.color = new Color(0.35f, 0.96f, 1f, 0f);
                    _pingRingImg.raycastTarget = false;
                }
            }

            // Right floating animated Saturn
            _saturnFrames = SolarSystemArtBank.GetAnimatedFrames("saturn_sheet", out _saturnFps);
            if (_saturnFrames != null && _saturnFrames.Length > 0)
            {
                GameObject saturnObj = new GameObject("AmbientSaturn", typeof(RectTransform), typeof(Image));
                saturnObj.transform.SetParent(parent, false);
                _saturnRect = saturnObj.GetComponent<RectTransform>();
                _saturnRect.anchorMin = new Vector2(0.86f, 0.42f);
                _saturnRect.anchorMax = new Vector2(0.86f, 0.42f);
                _saturnRect.sizeDelta = new Vector2(188f, 158f);

                _saturnImg = saturnObj.GetComponent<Image>();
                _saturnImg.sprite = _saturnFrames[0];
                _saturnImg.preserveAspect = true;
                _saturnImg.color = new Color(1f, 1f, 1f, 0.86f);
                _saturnImg.raycastTarget = false;
            }

            // Drifting Deep Space Probe on the right side
            if (_artBank != null && (_artBank.deepSpaceProbeSprite != null || _artBank.reusableRocketSprite != null))
            {
                GameObject probeObj = new GameObject("DriftingProbe", typeof(RectTransform), typeof(Image));
                probeObj.transform.SetParent(parent, false);
                _probeRect = probeObj.GetComponent<RectTransform>();
                _probeRect.anchorMin = new Vector2(0.83f, 0.72f);
                _probeRect.anchorMax = new Vector2(0.83f, 0.72f);
                _probeRect.sizeDelta = new Vector2(44f, 44f);
                _probeRect.localRotation = Quaternion.Euler(0f, 0f, -18f);

                Image pImg = probeObj.GetComponent<Image>();
                pImg.sprite = _artBank.deepSpaceProbeSprite != null ? _artBank.deepSpaceProbeSprite : _artBank.reusableRocketSprite;
                pImg.preserveAspect = true;
                pImg.color = new Color(0.92f, 0.97f, 1f, 0.88f);
                pImg.raycastTarget = false;
            }
        }

        private void BuildRocketFlyby(Transform parent)
        {
            if (_artBank == null) return;
            Sprite rocketSp = _artBank.smallRocketSprite ?? _artBank.reusableRocketSprite;
            if (rocketSp == null) return;

            GameObject rocketObj = new GameObject("FlybyRocket", typeof(RectTransform), typeof(Image));
            rocketObj.transform.SetParent(parent, false);
            _flybyRocketRect = rocketObj.GetComponent<RectTransform>();
            _flybyRocketRect.anchorMin = new Vector2(0.5f, 0.5f);
            _flybyRocketRect.anchorMax = new Vector2(0.5f, 0.5f);
            _flybyRocketRect.sizeDelta = new Vector2(36f, 36f);

            _flybyRocketImg = rocketObj.GetComponent<Image>();
            _flybyRocketImg.sprite = rocketSp;
            _flybyRocketImg.preserveAspect = true;
            _flybyRocketImg.color = new Color(1f, 1f, 1f, 0f);
            _flybyRocketImg.raycastTarget = false;

            if (_artBank.rocketFlameSprite != null)
            {
                GameObject flameObj = new GameObject("FlybyFlame", typeof(RectTransform), typeof(Image));
                flameObj.transform.SetParent(rocketObj.transform, false);
                _flybyFlameRect = flameObj.GetComponent<RectTransform>();
                _flybyFlameRect.anchorMin = new Vector2(0.5f, 0f);
                _flybyFlameRect.anchorMax = new Vector2(0.5f, 0f);
                _flybyFlameRect.anchoredPosition = new Vector2(0f, -8f);
                _flybyFlameRect.sizeDelta = new Vector2(14f, 18f);

                _flybyFlameImg = flameObj.GetComponent<Image>();
                _flybyFlameImg.sprite = _artBank.rocketFlameSprite;
                _flybyFlameImg.preserveAspect = true;
                _flybyFlameImg.color = new Color(1f, 0.85f, 0.4f, 0f);
                _flybyFlameImg.raycastTarget = false;
            }
        }

        private void BuildCornerTelemetryBadges(Transform parent)
        {
            Font font = AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.GetPixelFont();
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Top-Left Relay Status Badge
            GameObject leftBadge = CreateBadgeFrame("RelayStatusBadge", parent, new Vector2(0f, 1f), new Vector2(22f, -20f), new Vector2(310f, 52f));
            HorizontalLayoutGroup lhl = leftBadge.AddComponent<HorizontalLayoutGroup>();
            lhl.padding = new RectOffset(10, 12, 8, 8);
            lhl.spacing = 8f;
            lhl.childAlignment = TextAnchor.MiddleLeft;
            lhl.childControlWidth = true;
            lhl.childControlHeight = true;
            lhl.childForceExpandWidth = false;

            if (_artBank != null && _artBank.missionBadge != null)
            {
                GameObject iconObj = new GameObject("BadgeIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconObj.transform.SetParent(leftBadge.transform, false);
                iconObj.GetComponent<LayoutElement>().preferredWidth = 28f;
                Image iImg = iconObj.GetComponent<Image>();
                iImg.sprite = _artBank.missionBadge;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
            }

            GameObject dotObj = new GameObject("BeaconDot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            dotObj.transform.SetParent(leftBadge.transform, false);
            LayoutElement dotLe = dotObj.GetComponent<LayoutElement>();
            dotLe.preferredWidth = 10f;
            dotLe.preferredHeight = 10f;
            _beaconDotImg = dotObj.GetComponent<Image>();
            _beaconDotImg.color = new Color(0.35f, 1f, 0.68f, 1f);
            _beaconDotImg.raycastTarget = false;

            _relayStatusText = CreateBadgeText("RelayText", leftBadge.transform, font, RelayStatuses[0], new Color(0.74f, 0.96f, 1f, 1f));

            // Top-Right ASTRA Live Status Badge
            GameObject rightBadge = CreateBadgeFrame("AstraStatusBadge", parent, new Vector2(1f, 1f), new Vector2(-22f, -20f), new Vector2(326f, 52f));
            HorizontalLayoutGroup rhl = rightBadge.AddComponent<HorizontalLayoutGroup>();
            rhl.padding = new RectOffset(10, 12, 8, 8);
            rhl.spacing = 8f;
            rhl.childAlignment = TextAnchor.MiddleLeft;
            rhl.childControlWidth = true;
            rhl.childControlHeight = true;
            rhl.childForceExpandWidth = false;

            if (_artBank != null && _artBank.astraPortrait != null)
            {
                GameObject portObj = new GameObject("AstraIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                portObj.transform.SetParent(rightBadge.transform, false);
                portObj.GetComponent<LayoutElement>().preferredWidth = 32f;
                Image pImg = portObj.GetComponent<Image>();
                pImg.sprite = _artBank.astraPortrait;
                pImg.preserveAspect = true;
                pImg.raycastTarget = false;
            }

            _astraStatusText = CreateBadgeText("AstraText", rightBadge.transform, font, AstraStatuses[0], new Color(0.86f, 0.92f, 1f, 1f));
        }

        private GameObject CreateBadgeFrame(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            img.raycastTarget = false;
            if (_artBank != null && _artBank.inventoryPanel != null)
            {
                SolarSystemArtBank.ApplyPanelFrame(img, _artBank.inventoryPanel, new Vector4(10f, 10f, 10f, 10f), new Color(0.92f, 0.96f, 1f, 0.88f));
            }
            else
            {
                img.color = new Color(0.05f, 0.09f, 0.18f, 0.84f);
            }
            return obj;
        }

        private static Text CreateBadgeText(string name, Transform parent, Font font, string text, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<LayoutElement>().flexibleWidth = 1f;

            Text txt = obj.GetComponent<Text>();
            txt.font = font;
            txt.text = text;
            txt.fontSize = 13;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = color;
            txt.raycastTarget = false;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 10;
            txt.resizeTextMaxSize = 14;
            return txt;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;

            // Smooth mouse parallax
            float nx = Screen.width > 0 ? Mathf.Clamp((Input.mousePosition.x / Screen.width) * 2f - 1f, -1f, 1f) : 0f;
            float ny = Screen.height > 0 ? Mathf.Clamp((Input.mousePosition.y / Screen.height) * 2f - 1f, -1f, 1f) : 0f;
            Vector2 targetParallax = new Vector2(-nx * 24f, -ny * 16f);
            _parallaxOffset = Vector2.Lerp(_parallaxOffset, targetParallax, 1f - Mathf.Exp(-dt * 4.5f));

            // Drifting nebula glows
            if (_nebulaLeftRect != null)
            {
                _nebulaLeftRect.anchoredPosition = _parallaxOffset * 0.35f + new Vector2(Mathf.Sin(t * 0.25f) * 18f, Mathf.Cos(t * 0.20f) * 14f);
            }
            if (_nebulaRightRect != null)
            {
                _nebulaRightRect.anchoredPosition = _parallaxOffset * 0.40f + new Vector2(Mathf.Cos(t * 0.22f) * 20f, Mathf.Sin(t * 0.18f) * 14f);
            }

            // Twinkling stars + parallax
            for (int i = 0; i < _stars.Count; i++)
            {
                StarNode s = _stars[i];
                if (s.Img == null) continue;
                float pulse = 0.52f + 0.48f * Mathf.Sin(t * s.Speed + s.Phase);
                Color c = s.Img.color;
                c.a = s.BaseAlpha * pulse;
                s.Img.color = c;

                if (s.Rect != null)
                {
                    float bob = Mathf.Sin(t * 0.4f + s.Phase) * s.DriftX;
                    s.Rect.anchoredPosition = _parallaxOffset * s.Depth + new Vector2(bob, 0f);
                }
            }

            // Tumbling asteroids
            UpdateAsteroids(dt);

            // Animated Earth & orbiting satellite
            if (_earthRect != null)
            {
                _earthRect.anchoredPosition = _earthBasePos + _parallaxOffset * 0.95f + new Vector2(0f, Mathf.Sin(t * 0.7f) * 6f);
            }
            if (_earthImg != null && _earthFrames != null && _earthFrames.Length > 0)
            {
                _earthAnimTime += dt * _earthFps;
                int idx = Mathf.FloorToInt(_earthAnimTime) % _earthFrames.Length;
                _earthImg.sprite = _earthFrames[idx];
            }
            if (_earthOrbitSatRect != null)
            {
                _satOrbitAngle += dt * 0.72f;
                float rx = 96f;
                float ry = 46f;
                _earthOrbitSatRect.anchoredPosition = new Vector2(Mathf.Cos(_satOrbitAngle) * rx, Mathf.Sin(_satOrbitAngle) * ry);
                _earthOrbitSatRect.localRotation = Quaternion.Euler(0f, 0f, (_satOrbitAngle * Mathf.Rad2Deg) + 45f);
            }
            if (_pingRingRect != null && _pingRingImg != null)
            {
                _pingProgress = (_pingProgress + dt * 0.55f) % 1f;
                float scale = 1f + _pingProgress * 3.2f;
                _pingRingRect.localScale = new Vector3(scale, scale, 1f);
                _pingRingImg.color = new Color(0.35f, 0.96f, 1f, (1f - _pingProgress) * 0.42f);
            }

            // Animated Saturn
            if (_saturnRect != null)
            {
                _saturnRect.anchoredPosition = _parallaxOffset * 0.65f + new Vector2(0f, Mathf.Cos(t * 0.55f) * 7f);
            }
            if (_saturnImg != null && _saturnFrames != null && _saturnFrames.Length > 0)
            {
                _saturnAnimTime += dt * _saturnFps;
                int idx = Mathf.FloorToInt(_saturnAnimTime) % _saturnFrames.Length;
                _saturnImg.sprite = _saturnFrames[idx];
            }

            // Drifting Probe
            if (_probeRect != null)
            {
                _probePhase += dt * 0.65f;
                _probeRect.anchoredPosition = _parallaxOffset * 1.1f + new Vector2(Mathf.Sin(_probePhase * 0.7f) * 14f, Mathf.Sin(_probePhase) * 10f);
            }

            // Periodic rocket flyby + smoke puffs
            UpdateRocketFlyby(dt, t);
            UpdateSmokePuffs(dt);

            // Shooting star
            UpdateShootingStar(dt);

            // Beacon pulse & status cycling
            if (_beaconDotImg != null)
            {
                float b = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 2.4f));
                _beaconDotImg.color = new Color(0.35f, 1f, 0.68f, b);
            }

            _statusCycleTimer += dt;
            if (_statusCycleTimer >= 4.2f)
            {
                _statusCycleTimer = 0f;
                _statusIdx++;
                if (_relayStatusText != null) _relayStatusText.text = RelayStatuses[_statusIdx % RelayStatuses.Length];
                if (_astraStatusText != null) _astraStatusText.text = AstraStatuses[_statusIdx % AstraStatuses.Length];
            }
        }

        private void UpdateAsteroids(float dt)
        {
            for (int i = 0; i < _asteroids.Count; i++)
            {
                AsteroidNode a = _asteroids[i];
                a.AnimTime += dt * a.Fps;
                a.Pos += a.Velocity * dt;
                if (a.Pos.x > 1060f) a.Pos.x = -1060f;
                if (a.Pos.x < -1060f) a.Pos.x = 1060f;
                if (a.Pos.y > 600f) a.Pos.y = -600f;
                if (a.Pos.y < -600f) a.Pos.y = 600f;

                if (a.Img != null && a.Frames != null && a.Frames.Length > 0)
                {
                    a.Img.sprite = a.Frames[Mathf.FloorToInt(a.AnimTime) % a.Frames.Length];
                }
                if (a.Rect != null)
                {
                    a.Rect.anchoredPosition = a.Pos + _parallaxOffset * a.Depth;
                    a.Rect.Rotate(0f, 0f, a.RotSpeed * dt);
                }
                _asteroids[i] = a;
            }
        }

        private void UpdateRocketFlyby(float dt, float time)
        {
            if (_flybyRocketRect == null || _flybyRocketImg == null) return;

            if (_flybyProgress < 0f)
            {
                _flybyCooldown -= dt;
                if (_flybyCooldown <= 0f)
                {
                    _flybyProgress = 0f;
                    _flybyCooldown = Random.Range(8.5f, 13.5f);
                    bool leftToRight = Random.value > 0.5f;
                    float y1 = Random.Range(-320f, 120f);
                    float y2 = Random.Range(160f, 440f);
                    _flybyStart = new Vector2(leftToRight ? -980f : 980f, y1);
                    _flybyEnd = new Vector2(leftToRight ? 980f : -980f, y2);
                    Vector2 dir = (_flybyEnd - _flybyStart).normalized;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    _flybyRocketRect.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
                return;
            }

            _flybyProgress += dt * 0.19f;
            if (_flybyProgress >= 1f)
            {
                _flybyProgress = -1f;
                _flybyRocketImg.color = new Color(1f, 1f, 1f, 0f);
                if (_flybyFlameImg != null) _flybyFlameImg.color = new Color(1f, 0.85f, 0.4f, 0f);
                return;
            }

            Vector2 pos = Vector2.Lerp(_flybyStart, _flybyEnd, _flybyProgress) + _parallaxOffset * 1.15f;
            _flybyRocketRect.anchoredPosition = pos;
            float fade = Mathf.Sin(_flybyProgress * Mathf.PI);
            _flybyRocketImg.color = new Color(1f, 1f, 1f, Mathf.Clamp01(fade * 1.3f) * 0.88f);

            if (_flybyFlameImg != null && _flybyFlameRect != null)
            {
                float flicker = 0.82f + 0.28f * Mathf.Sin(time * 42f);
                _flybyFlameRect.localScale = new Vector3(flicker, flicker, 1f);
                _flybyFlameImg.color = new Color(1f, 0.85f, 0.4f, fade * 0.9f);
            }

            _smokeSpawnTimer += dt;
            if (_smokeSpawnTimer >= 0.08f && _layerRect != null)
            {
                _smokeSpawnTimer = 0f;
                SpawnSmokePuff(pos);
            }
        }

        private void SpawnSmokePuff(Vector2 pos)
        {
            GameObject puffObj = new GameObject("FlybySmoke", typeof(RectTransform), typeof(Image));
            puffObj.transform.SetParent(_layerRect, false);
            if (_flybyRocketRect != null)
            {
                puffObj.transform.SetSiblingIndex(Mathf.Max(0, _flybyRocketRect.GetSiblingIndex()));
            }

            RectTransform rt = puffObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            float s = Random.Range(8f, 13f);
            rt.sizeDelta = new Vector2(s, s);

            Image img = puffObj.GetComponent<Image>();
            img.sprite = _artBank != null && _artBank.launchSmokeSprite != null ? _artBank.launchSmokeSprite : _softDiscSprite;
            img.color = new Color(0.85f, 0.92f, 1f, 0.35f);
            img.raycastTarget = false;

            _smokePuffs.Add(new SmokePuff
            {
                Rect = rt,
                Img = img,
                Age = 0f,
                Lifetime = Random.Range(0.75f, 1.15f),
                Drift = new Vector2(Random.Range(-10f, 10f), Random.Range(-10f, 10f))
            });
        }

        private void UpdateSmokePuffs(float dt)
        {
            for (int i = _smokePuffs.Count - 1; i >= 0; i--)
            {
                SmokePuff p = _smokePuffs[i];
                p.Age += dt;
                if (p.Age >= p.Lifetime || p.Rect == null || p.Img == null)
                {
                    if (p.Rect != null) Destroy(p.Rect.gameObject);
                    _smokePuffs.RemoveAt(i);
                    continue;
                }

                float t = p.Age / p.Lifetime;
                p.Rect.anchoredPosition += p.Drift * dt;
                float scale = 1f + t * 1.4f;
                p.Rect.localScale = new Vector3(scale, scale, 1f);
                p.Img.color = new Color(0.85f, 0.92f, 1f, (1f - t) * 0.32f);
                _smokePuffs[i] = p;
            }
        }

        private void UpdateShootingStar(float dt)
        {
            if (_shootingStarRect == null || _shootingStarImg == null) return;

            if (_shootingActiveProgress < 0f)
            {
                _shootingTimer -= dt;
                if (_shootingTimer <= 0f)
                {
                    _shootingActiveProgress = 0f;
                    _shootingTimer = Random.Range(3.0f, 5.8f);
                    float w = _layerRect != null && _layerRect.rect.width > 200f ? _layerRect.rect.width * 0.45f : 720f;
                    float h = _layerRect != null && _layerRect.rect.height > 200f ? _layerRect.rect.height * 0.42f : 420f;
                    _shootStart = new Vector2(Random.Range(-w, w * 0.2f), Random.Range(h * 0.2f, h));
                    _shootEnd = _shootStart + new Vector2(Random.Range(360f, 560f), -Random.Range(180f, 300f));
                    Vector2 dir = (_shootEnd - _shootStart).normalized;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    _shootingStarRect.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
                return;
            }

            _shootingActiveProgress += dt * 1.45f;
            if (_shootingActiveProgress >= 1f)
            {
                _shootingActiveProgress = -1f;
                _shootingStarImg.color = new Color(0.75f, 0.96f, 1f, 0f);
                return;
            }

            _shootingStarRect.anchoredPosition = Vector2.Lerp(_shootStart, _shootEnd, _shootingActiveProgress);
            float alpha = Mathf.Sin(_shootingActiveProgress * Mathf.PI) * 0.85f;
            _shootingStarImg.color = new Color(0.75f, 0.96f, 1f, alpha);
        }

        private static void EnsureSoftDiscSprite()
        {
            if (_softDiscSprite != null) return;
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _softDiscSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
