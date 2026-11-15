using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace AstraLink.UI
{
    /// <summary>
    /// Smoothly cycles URP Global Light 2D, camera sky backdrop, and optional building night lights
    /// across Dawn -> Day -> Dusk -> Night while viewing the Space Center map.
    /// </summary>
    public class SpaceCenterDayNightCycle : MonoBehaviour
    {
        [Header("Light Target (auto-found Global Light 2D if empty)")]
        [SerializeField] private Light2D globalLight;

        [Header("Cycle Timing")]
        [Tooltip("Full 24h cycle length in real seconds.")]
        [SerializeField] private float cycleDurationSeconds = 75f;
        [Range(0f, 1f)]
        [SerializeField] private float startTimeOfDay = 0.02f;
        [SerializeField] private bool pauseWhenMapClosed = true;

        [Header("Lighting Palette")]
        [SerializeField] private Color nightLightColor = new Color(0.32f, 0.44f, 0.78f, 1f);
        [SerializeField] private Color dawnDuskLightColor = new Color(1.00f, 0.72f, 0.54f, 1f);
        [SerializeField] private Color dayLightColor = new Color(1.00f, 0.99f, 0.96f, 1f);
        [SerializeField] private float minNightIntensity = 0.42f;
        [SerializeField] private float maxDayIntensity = 1.00f;

        [Header("Sky & Building Night Lights")]
        [SerializeField] private bool syncCameraBackground = true;
        [SerializeField] private Color nightSkyColor = new Color(0.03f, 0.05f, 0.11f, 1f);
        [SerializeField] private Color daySkyColor = new Color(0.11f, 0.18f, 0.30f, 1f);
        [SerializeField] private bool enableBuildingNightLights = true;
        [SerializeField] private float maxLampIntensity = 1.15f;

        private float _timeOfDay;
        private Camera _cachedCamera;
        private readonly List<Light2D> _nightLamps = new List<Light2D>();

        private void Awake()
        {
            _timeOfDay = Mathf.Repeat(startTimeOfDay, 1f);
            ResolveGlobalLight();
        }

        private void Start()
        {
            ResolveGlobalLight();
            if (enableBuildingNightLights)
            {
                EnsureBuildingNightLamps();
            }
            ApplyLighting(_timeOfDay);
        }

        private void Update()
        {
            if (pauseWhenMapClosed && SpaceCenterMapUI.Instance != null && !SpaceCenterMapUI.Instance.IsMapOpen)
            {
                return;
            }

            if (cycleDurationSeconds > 0.5f)
            {
                _timeOfDay = Mathf.Repeat(_timeOfDay + Time.deltaTime / cycleDurationSeconds, 1f);
            }

            ApplyLighting(_timeOfDay);
        }

        private void ResolveGlobalLight()
        {
            if (globalLight != null) return;

            globalLight = GetComponent<Light2D>();
            if (globalLight != null && globalLight.lightType == Light2D.LightType.Global) return;

            Light2D[] lights = FindObjectsOfType<Light2D>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].lightType == Light2D.LightType.Global)
                {
                    globalLight = lights[i];
                    return;
                }
            }
        }

        private void EnsureBuildingNightLamps()
        {
            if (_nightLamps.Count > 0) return;

            Grid grid = FindObjectOfType<Grid>(true);
            if (grid == null) return;

            CreateLampForTilemap(grid, "Tilemap_MissionControl", new Color(0.55f, 0.88f, 1.0f, 1f), 5.2f);
            CreateLampForTilemap(grid, "Tilemap_Hangar", new Color(1.00f, 0.80f, 0.45f, 1f), 6.2f);
            CreateLampForTilemap(grid, "Tilemap_LaunchPad", new Color(1.00f, 0.68f, 0.36f, 1f), 5.6f);
        }

        private void CreateLampForTilemap(Grid grid, string tilemapName, Color lampColor, float outerRadius)
        {
            Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(true);
            Tilemap target = null;
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null && string.Equals(maps[i].gameObject.name, tilemapName, StringComparison.OrdinalIgnoreCase))
                {
                    target = maps[i];
                    break;
                }
            }

            if (target == null || target.GetUsedTilesCount() == 0) return;

            target.CompressBounds();
            Vector3 worldCenter = target.transform.TransformPoint(target.localBounds.center);
            worldCenter.z = 0f;

            string lampName = "NightLamp_" + tilemapName;
            Transform existing = grid.transform.Find(lampName);
            GameObject lampObj = existing != null ? existing.gameObject : new GameObject(lampName);
            if (existing == null)
            {
                lampObj.transform.SetParent(grid.transform, false);
                lampObj.transform.position = worldCenter;
            }

            Light2D lamp = lampObj.GetComponent<Light2D>() ?? lampObj.AddComponent<Light2D>();
            lamp.lightType = Light2D.LightType.Point;
            lamp.color = lampColor;
            lamp.pointLightInnerRadius = outerRadius * 0.25f;
            lamp.pointLightOuterRadius = outerRadius;
            lamp.falloffIntensity = 0.65f;
            lamp.intensity = 0f;

            _nightLamps.Add(lamp);
        }

        private void ApplyLighting(float t)
        {
            EvaluateTime(t, out Color lightColor, out float dayFactor);

            if (globalLight != null)
            {
                globalLight.color = lightColor;
                globalLight.intensity = Mathf.Lerp(minNightIntensity, maxDayIntensity, dayFactor);
            }

            if (syncCameraBackground)
            {
                if (_cachedCamera == null) _cachedCamera = Camera.main;
                if (_cachedCamera != null)
                {
                    _cachedCamera.backgroundColor = Color.Lerp(nightSkyColor, daySkyColor, dayFactor);
                }
            }

            if (_nightLamps.Count > 0)
            {
                float nightFactor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - dayFactor * 1.35f));
                float lampIntensity = nightFactor * maxLampIntensity;
                for (int i = 0; i < _nightLamps.Count; i++)
                {
                    if (_nightLamps[i] != null)
                    {
                        _nightLamps[i].intensity = lampIntensity;
                    }
                }
            }
        }

        private void EvaluateTime(float t, out Color lightColor, out float dayFactor)
        {
            // 0.00-0.20 Night | 0.20-0.34 Dawn | 0.34-0.66 Day | 0.66-0.80 Dusk | 0.80-1.00 Night
            if (t < 0.20f)
            {
                lightColor = nightLightColor;
                dayFactor = 0f;
            }
            else if (t < 0.27f)
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - 0.20f) / 0.07f);
                lightColor = Color.Lerp(nightLightColor, dawnDuskLightColor, k);
                dayFactor = Mathf.Lerp(0f, 0.45f, k);
            }
            else if (t < 0.34f)
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - 0.27f) / 0.07f);
                lightColor = Color.Lerp(dawnDuskLightColor, dayLightColor, k);
                dayFactor = Mathf.Lerp(0.45f, 1f, k);
            }
            else if (t < 0.66f)
            {
                lightColor = dayLightColor;
                dayFactor = 1f;
            }
            else if (t < 0.73f)
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - 0.66f) / 0.07f);
                lightColor = Color.Lerp(dayLightColor, dawnDuskLightColor, k);
                dayFactor = Mathf.Lerp(1f, 0.45f, k);
            }
            else if (t < 0.80f)
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - 0.73f) / 0.07f);
                lightColor = Color.Lerp(dawnDuskLightColor, nightLightColor, k);
                dayFactor = Mathf.Lerp(0.45f, 0f, k);
            }
            else
            {
                lightColor = nightLightColor;
                dayFactor = 0f;
            }
        }
    }
}
