using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Flight.SpaceMap
{
    /// <summary>
    /// Procedural pixel-art deep-space background featuring a dithered cosmic gradient,
    /// multi-depth twinkling pixel-dot stars, and occasional ambient shooting stars.
    /// </summary>
    public class PixelStarfieldLayer : MonoBehaviour
    {
        private struct StarNode
        {
            public RectTransform Rect;
            public Image Img;
            public Vector2 AnchorNormalized;
            public Color BaseColor;
            public float MinAlpha;
            public float MaxAlpha;
            public float BlinkSpeed;
            public float Phase;
            public float ParallaxFactor;
            public float BaseSize;
            public bool PulseScale;
        }

        private const int StarCount = 220;
        private const int NebulaWidth = 320;
        private const int NebulaHeight = 180;

        private static Sprite _nebulaSprite;
        private static Sprite _pixelDotSprite;
        private static Sprite _pixelPlusSprite;
        private static Sprite _pixelDiamondSprite;

        private RectTransform _rootRect;
        private StarNode[] _stars;
        private RectTransform _shootingStarRect;
        private Image _shootingStarImg;
        private float _shootingTimer;
        private Vector2 _shootingStartNorm;
        private Vector2 _shootingDir;
        private float _shootingDuration;
        private bool _built;

        private static readonly Color[] SpectralPalette =
        {
            new Color(0.95f, 0.97f, 1.00f, 1f),
            new Color(0.68f, 0.88f, 1.00f, 1f),
            new Color(1.00f, 0.91f, 0.66f, 1f),
            new Color(0.84f, 0.78f, 1.00f, 1f),
            new Color(0.75f, 0.96f, 0.92f, 1f)
        };

        public void EnsureBuilt()
        {
            if (_built && _stars != null)
            {
                return;
            }

            _rootRect = GetComponent<RectTransform>();
            if (_rootRect == null)
            {
                _rootRect = gameObject.AddComponent<RectTransform>();
            }
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;

            EnsureProceduralSprites();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            // 1. Dithered retro cosmic gradient backdrop
            GameObject bgObj = new GameObject("PixelNebulaBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgObj.transform.SetParent(transform, false);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.sprite = _nebulaSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
            bgImg.raycastTarget = false;
            bgImg.color = Color.white;

            // 2. Twinkling pixel-dot stars
            _stars = new StarNode[StarCount];
            System.Random rng = new System.Random(42019);

            for (int i = 0; i < StarCount; i++)
            {
                bool isSparkle = (i % 7 == 0);
                bool isMid = !isSparkle && (i % 3 != 0);

                GameObject starObj = new GameObject("PixelStar_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                starObj.transform.SetParent(transform, false);
                RectTransform rt = starObj.GetComponent<RectTransform>();
                float nx = (float)rng.NextDouble();
                float ny = (float)rng.NextDouble();
                rt.anchorMin = new Vector2(nx, ny);
                rt.anchorMax = new Vector2(nx, ny);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                float size = isSparkle ? (rng.NextDouble() > 0.45 ? 7f : 5f) : (isMid ? 4f : 2.5f);
                rt.sizeDelta = new Vector2(size, size);

                Image img = starObj.GetComponent<Image>();
                img.raycastTarget = false;
                img.sprite = isSparkle
                    ? (rng.NextDouble() > 0.5 ? _pixelDiamondSprite : _pixelPlusSprite)
                    : _pixelDotSprite;

                Color col = SpectralPalette[rng.Next(SpectralPalette.Length)];
                _stars[i] = new StarNode
                {
                    Rect = rt,
                    Img = img,
                    AnchorNormalized = new Vector2(nx, ny),
                    BaseColor = col,
                    MinAlpha = isSparkle ? 0.18f : (isMid ? 0.14f : 0.08f),
                    MaxAlpha = isSparkle ? 1.00f : (isMid ? 0.85f : 0.55f),
                    BlinkSpeed = 1.2f + (float)rng.NextDouble() * (isSparkle ? 3.4f : 2.6f),
                    Phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                    ParallaxFactor = isSparkle ? 0.045f : (isMid ? 0.024f : 0.010f),
                    BaseSize = size,
                    PulseScale = isSparkle
                };
            }

            // 3. Ambient shooting star streak
            GameObject shootObj = new GameObject("AmbientShootingStar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shootObj.transform.SetParent(transform, false);
            _shootingStarRect = shootObj.GetComponent<RectTransform>();
            _shootingStarRect.anchorMin = new Vector2(0.5f, 0.5f);
            _shootingStarRect.anchorMax = new Vector2(0.5f, 0.5f);
            _shootingStarRect.pivot = new Vector2(0.5f, 0.5f);
            _shootingStarRect.sizeDelta = new Vector2(36f, 2f);
            _shootingStarImg = shootObj.GetComponent<Image>();
            _shootingStarImg.sprite = _pixelDotSprite;
            _shootingStarImg.raycastTarget = false;
            _shootingStarImg.color = new Color(0.82f, 0.94f, 1f, 0f);
            _shootingTimer = 4.2f;
            _shootingDuration = 0.65f;

            _built = true;
        }

        public void Animate(float simDt, float simTimeSec, Vector2 cameraPanPx)
        {
            if (!_built || _stars == null)
            {
                return;
            }

            for (int i = 0; i < _stars.Length; i++)
            {
                ref StarNode star = ref _stars[i];
                if (star.Img == null || star.Rect == null) continue;

                // Subtle stepped pixel-art twinkle feel blended smoothly
                float wave = 0.5f + 0.5f * Mathf.Sin(simTimeSec * star.BlinkSpeed + star.Phase);
                float crispWave = Mathf.Round(wave * 4f) * 0.25f;
                float alpha = Mathf.Lerp(star.MinAlpha, star.MaxAlpha, crispWave);

                Color c = star.BaseColor;
                c.a = alpha;
                star.Img.color = c;

                if (star.ParallaxFactor > 0f)
                {
                    star.Rect.anchoredPosition = cameraPanPx * star.ParallaxFactor;
                }

                if (star.PulseScale)
                {
                    float s = crispWave > 0.72f ? star.BaseSize * 1.35f : star.BaseSize;
                    star.Rect.sizeDelta = new Vector2(s, s);
                }
            }

            AnimateShootingStar(simDt);
        }

        private void AnimateShootingStar(float dt)
        {
            if (_shootingStarRect == null || _shootingStarImg == null || _rootRect == null)
            {
                return;
            }

            _shootingTimer -= dt;
            if (_shootingTimer > 0f)
            {
                _shootingStarImg.color = new Color(0.82f, 0.94f, 1f, 0f);
                return;
            }

            float elapsed = -_shootingTimer;
            if (elapsed == 0f || elapsed < dt * 1.1f)
            {
                _shootingStartNorm = new Vector2(Random.Range(-0.42f, 0.35f), Random.Range(0.10f, 0.44f));
                _shootingDir = new Vector2(Random.Range(0.65f, 1.0f), Random.Range(-0.55f, -0.25f)).normalized;
                float angle = Mathf.Atan2(_shootingDir.y, _shootingDir.x) * Mathf.Rad2Deg;
                _shootingStarRect.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            if (elapsed > _shootingDuration)
            {
                _shootingStarImg.color = new Color(0.82f, 0.94f, 1f, 0f);
                _shootingTimer = Random.Range(4.8f, 9.5f);
                return;
            }

            float p = elapsed / _shootingDuration;
            float alpha = Mathf.Sin(p * Mathf.PI) * 0.85f;
            float spanW = Mathf.Max(800f, _rootRect.rect.width);
            float spanH = Mathf.Max(500f, _rootRect.rect.height);
            Vector2 startPx = new Vector2(_shootingStartNorm.x * spanW, _shootingStartNorm.y * spanH);
            _shootingStarRect.anchoredPosition = startPx + _shootingDir * (p * 420f);
            _shootingStarImg.color = new Color(0.85f, 0.96f, 1f, alpha);
        }

        private static void EnsureProceduralSprites()
        {
            if (_nebulaSprite == null)
            {
                Texture2D tex = new Texture2D(NebulaWidth, NebulaHeight, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                Color deepBg = new Color(0.015f, 0.022f, 0.055f, 1f);
                Color midNebula = new Color(0.036f, 0.055f, 0.125f, 1f);
                Color violetNebula = new Color(0.065f, 0.042f, 0.135f, 1f);
                Color tealDust = new Color(0.035f, 0.095f, 0.155f, 1f);

                int[] bayer4 = {
                     0,  8,  2, 10,
                    12,  4, 14,  6,
                     3, 11,  1,  9,
                    15,  7, 13,  5
                };

                for (int y = 0; y < NebulaHeight; y++)
                {
                    float ny = y / (float)NebulaHeight;
                    for (int x = 0; x < NebulaWidth; x++)
                    {
                        float nx = x / (float)NebulaWidth;

                        // Diagonal faint Milky Way dust ribbon + warm center glow region
                        float bandDist = Mathf.Abs((ny - 0.5f) - (nx - 0.5f) * 0.42f);
                        float ribbon = Mathf.Clamp01(1f - bandDist * 3.1f);
                        float swirl = 0.5f + 0.5f * Mathf.Sin(nx * 9.5f + ny * 6.0f) * Mathf.Cos(nx * 5.0f - ny * 8.0f);
                        float centerGlow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(0.45f, 0.52f)) * 2.2f);

                        float intensity = ribbon * 0.55f + swirl * 0.25f + centerGlow * 0.35f;
                        float threshold = (bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;

                        Color c = deepBg;
                        if (intensity > threshold * 0.75f + 0.28f)
                        {
                            c = Color.Lerp(midNebula, (nx > 0.52f ? tealDust : violetNebula), swirl);
                        }
                        else if (intensity > threshold * 0.65f + 0.12f)
                        {
                            c = Color.Lerp(deepBg, midNebula, 0.55f);
                        }
                        tex.SetPixel(x, y, c);
                    }
                }
                tex.Apply();
                _nebulaSprite = Sprite.Create(tex, new Rect(0, 0, NebulaWidth, NebulaHeight), new Vector2(0.5f, 0.5f), 100f);
                _nebulaSprite.name = "Proc_PixelNebula";
            }

            if (_pixelDotSprite == null)
            {
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color[] px = { Color.white, Color.white, Color.white, Color.white };
                tex.SetPixels(px);
                tex.Apply();
                _pixelDotSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            }

            if (_pixelPlusSprite == null)
            {
                Texture2D tex = new Texture2D(3, 3, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color tr = Color.clear;
                Color w = Color.white;
                Color m = new Color(1f, 1f, 1f, 0.55f);
                Color[] px =
                {
                    tr, m, tr,
                    m,  w, m,
                    tr, m, tr
                };
                tex.SetPixels(px);
                tex.Apply();
                _pixelPlusSprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 100f);
            }

            if (_pixelDiamondSprite == null)
            {
                Texture2D tex = new Texture2D(5, 5, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color tr = Color.clear;
                Color w = Color.white;
                Color g = new Color(1f, 1f, 1f, 0.55f);
                Color f = new Color(1f, 1f, 1f, 0.25f);
                Color[] px =
                {
                    tr, tr, f,  tr, tr,
                    tr, f,  g,  f,  tr,
                    f,  g,  w,  g,  f,
                    tr, f,  g,  f,  tr,
                    tr, tr, f,  tr, tr
                };
                tex.SetPixels(px);
                tex.Apply();
                _pixelDiamondSprite = Sprite.Create(tex, new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
