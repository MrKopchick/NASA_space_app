using System.Collections.Generic;
using UnityEngine;

namespace AstraLink.UI.Flight.SpaceMap
{
    /// <summary>
    /// Astronomical catalog for all celestial bodies and the Main Asteroid Belt rendered on the Solar System flight map.
    /// Ordered strictly in heliocentric astronomical sequence outward from the Sun.
    /// </summary>
    public static class SolarSystemBodyCatalog
    {
        public sealed class BodyDefinition
        {
            public string Id;
            public string ParentId;
            public string DisplayName;
            public string Classification;
            public string DistanceLabel;
            public string PeriodLabel;
            public string SummaryText;
            public string SheetKey;
            public float OrbitRadiusPx;
            public float SatelliteOrbitRadiusPx;
            public float Eccentricity;
            public float PerihelionAngleDeg;
            public float InitialPhaseDeg;
            public float OrbitalSpeedDegPerSec;
            public float VisualWidthPx;
            public float VisualHeightPx;
            public Color OrbitColor;
            public Color AccentColor;
            public bool IsAsteroidBelt;
        }

        public static List<BodyDefinition> BuildStandardSolarSystem()
        {
            return new List<BodyDefinition>
            {
                new BodyDefinition
                {
                    Id = "sun",
                    ParentId = null,
                    DisplayName = "Sun",
                    Classification = "G2V Main-Sequence Star",
                    DistanceLabel = "0.00 AU (Center)",
                    PeriodLabel = "25.4d equatorial rotation",
                    SummaryText = "Contains 99.86% of the Solar System mass. Drives solar wind & flare events.",
                    SheetKey = "sun_sheet",
                    OrbitRadiusPx = 0f,
                    Eccentricity = 0f,
                    PerihelionAngleDeg = 0f,
                    InitialPhaseDeg = 0f,
                    OrbitalSpeedDegPerSec = 0f,
                    VisualWidthPx = 320f,
                    VisualHeightPx = 320f,
                    OrbitColor = new Color(1.0f, 0.72f, 0.25f, 0.25f),
                    AccentColor = new Color(1.0f, 0.78f, 0.28f, 1f)
                },
                new BodyDefinition
                {
                    Id = "mercury",
                    ParentId = null,
                    DisplayName = "Mercury",
                    Classification = "Terrestrial Planet",
                    DistanceLabel = "0.39 AU",
                    PeriodLabel = "88 Earth days",
                    SummaryText = "Innermost cratered world with high orbital eccentricity (0.205).",
                    SheetKey = "mercury_sheet",
                    OrbitRadiusPx = 215f,
                    Eccentricity = 0.18f,
                    PerihelionAngleDeg = 29f,
                    InitialPhaseDeg = 35f,
                    OrbitalSpeedDegPerSec = 18.0f,
                    VisualWidthPx = 14f,
                    VisualHeightPx = 14f,
                    OrbitColor = new Color(0.70f, 0.75f, 0.85f, 0.34f),
                    AccentColor = new Color(0.78f, 0.82f, 0.90f, 1f)
                },
                new BodyDefinition
                {
                    Id = "venus",
                    ParentId = null,
                    DisplayName = "Venus",
                    Classification = "Terrestrial Planet",
                    DistanceLabel = "0.72 AU",
                    PeriodLabel = "224.7 Earth days",
                    SummaryText = "Super-greenhouse atmosphere (190% Earth solar flux, high radiation risk).",
                    SheetKey = "venus_sheet",
                    OrbitRadiusPx = 275f,
                    Eccentricity = 0.01f,
                    PerihelionAngleDeg = 55f,
                    InitialPhaseDeg = 142f,
                    OrbitalSpeedDegPerSec = 10.5f,
                    VisualWidthPx = 26f,
                    VisualHeightPx = 26f,
                    OrbitColor = new Color(0.95f, 0.82f, 0.44f, 0.34f),
                    AccentColor = new Color(0.98f, 0.85f, 0.45f, 1f)
                },
                new BodyDefinition
                {
                    Id = "earth",
                    ParentId = null,
                    DisplayName = "Earth",
                    Classification = "Terrestrial Planet",
                    DistanceLabel = "1.00 AU",
                    PeriodLabel = "365.25 days (1.0 yr)",
                    SummaryText = "Home world & Deep Space Network anchor. Base 1.0s LEO signal link.",
                    SheetKey = "earth_sheet",
                    OrbitRadiusPx = 345f,
                    Eccentricity = 0.02f,
                    PerihelionAngleDeg = 102f,
                    InitialPhaseDeg = 225f,
                    OrbitalSpeedDegPerSec = 7.2f,
                    VisualWidthPx = 28f,
                    VisualHeightPx = 28f,
                    OrbitColor = new Color(0.34f, 0.76f, 1.0f, 0.42f),
                    AccentColor = new Color(0.42f, 0.84f, 1.0f, 1f)
                },
                new BodyDefinition
                {
                    Id = "moon",
                    ParentId = "earth",
                    DisplayName = "Moon",
                    Classification = "Natural Satellite",
                    DistanceLabel = "384,400 km from Earth",
                    PeriodLabel = "27.3 Earth days",
                    SummaryText = "Tidally locked lunar survey orbit. Extended eclipses & 3.0s signal delay.",
                    SheetKey = "moon_sheet",
                    OrbitRadiusPx = 0f,
                    SatelliteOrbitRadiusPx = 38f,
                    Eccentricity = 0.05f,
                    PerihelionAngleDeg = 15f,
                    InitialPhaseDeg = 48f,
                    OrbitalSpeedDegPerSec = 38.0f,
                    VisualWidthPx = 10f,
                    VisualHeightPx = 10f,
                    OrbitColor = new Color(0.75f, 0.84f, 0.96f, 0.36f),
                    AccentColor = new Color(0.82f, 0.88f, 0.98f, 1f)
                },
                new BodyDefinition
                {
                    Id = "mars",
                    ParentId = null,
                    DisplayName = "Mars",
                    Classification = "Terrestrial Planet",
                    DistanceLabel = "1.52 AU",
                    PeriodLabel = "687 Earth days",
                    SummaryText = "Red Frontier. Solar intensity drops to 43% with 10s comms delay.",
                    SheetKey = "mars_sheet",
                    OrbitRadiusPx = 430f,
                    Eccentricity = 0.09f,
                    PerihelionAngleDeg = 336f,
                    InitialPhaseDeg = 312f,
                    OrbitalSpeedDegPerSec = 4.6f,
                    VisualWidthPx = 18f,
                    VisualHeightPx = 18f,
                    OrbitColor = new Color(0.95f, 0.45f, 0.30f, 0.38f),
                    AccentColor = new Color(1.0f, 0.50f, 0.34f, 1f)
                },
                new BodyDefinition
                {
                    Id = "belt",
                    ParentId = null,
                    DisplayName = "Main Asteroid Belt",
                    Classification = "Circumstellar Disc (2.2 - 3.2 AU)",
                    DistanceLabel = "2.20 - 3.20 AU",
                    PeriodLabel = "3.3 - 5.7 years",
                    SummaryText = "Rocky remnants between Mars & Jupiter featuring 6 tumbling asteroid classes.",
                    SheetKey = "asteroid_1_sheet",
                    OrbitRadiusPx = 560f,
                    Eccentricity = 0.04f,
                    PerihelionAngleDeg = 20f,
                    InitialPhaseDeg = 12f,
                    OrbitalSpeedDegPerSec = 2.4f,
                    VisualWidthPx = 24f,
                    VisualHeightPx = 24f,
                    OrbitColor = new Color(0.62f, 0.70f, 0.82f, 0.22f),
                    AccentColor = new Color(0.72f, 0.80f, 0.92f, 1f),
                    IsAsteroidBelt = true
                },
                new BodyDefinition
                {
                    Id = "jupiter",
                    ParentId = null,
                    DisplayName = "Jupiter",
                    Classification = "Gas Giant",
                    DistanceLabel = "5.20 AU",
                    PeriodLabel = "11.86 years",
                    SummaryText = "Largest Solar System planet; intense magnetosphere and volcanic moon Io.",
                    SheetKey = "jupiter_sheet",
                    OrbitRadiusPx = 735f,
                    Eccentricity = 0.05f,
                    PerihelionAngleDeg = 14f,
                    InitialPhaseDeg = 64f,
                    OrbitalSpeedDegPerSec = 1.65f,
                    VisualWidthPx = 92f,
                    VisualHeightPx = 92f,
                    OrbitColor = new Color(0.96f, 0.56f, 0.34f, 0.32f),
                    AccentColor = new Color(1.0f, 0.62f, 0.38f, 1f)
                },
                new BodyDefinition
                {
                    Id = "io",
                    ParentId = "jupiter",
                    DisplayName = "Io (Galilean Moon)",
                    Classification = "Volcanic Satellite",
                    DistanceLabel = "421,700 km from Jupiter",
                    PeriodLabel = "1.77 Earth days",
                    SummaryText = "Most volcanically active body in the Solar System due to Jovian tidal heating.",
                    SheetKey = "io_sheet",
                    OrbitRadiusPx = 0f,
                    SatelliteOrbitRadiusPx = 74f,
                    Eccentricity = 0.01f,
                    PerihelionAngleDeg = 0f,
                    InitialPhaseDeg = 125f,
                    OrbitalSpeedDegPerSec = 42.0f,
                    VisualWidthPx = 10f,
                    VisualHeightPx = 10f,
                    OrbitColor = new Color(1.0f, 0.62f, 0.25f, 0.32f),
                    AccentColor = new Color(1.0f, 0.66f, 0.28f, 1f)
                },
                new BodyDefinition
                {
                    Id = "saturn",
                    ParentId = null,
                    DisplayName = "Saturn",
                    Classification = "Ringed Gas Giant",
                    DistanceLabel = "9.58 AU",
                    PeriodLabel = "29.45 years",
                    SummaryText = "Adorned with icy rings spanning 282,000 km across deep outer space.",
                    SheetKey = "saturn_sheet",
                    OrbitRadiusPx = 915f,
                    Eccentricity = 0.06f,
                    PerihelionAngleDeg = 92f,
                    InitialPhaseDeg = 182f,
                    OrbitalSpeedDegPerSec = 1.05f,
                    VisualWidthPx = 128f,
                    VisualHeightPx = 108f,
                    OrbitColor = new Color(0.94f, 0.84f, 0.58f, 0.34f),
                    AccentColor = new Color(0.96f, 0.86f, 0.60f, 1f)
                },
                new BodyDefinition
                {
                    Id = "uranus",
                    ParentId = null,
                    DisplayName = "Uranus",
                    Classification = "Ice Giant",
                    DistanceLabel = "19.20 AU",
                    PeriodLabel = "84.0 years",
                    SummaryText = "Methane-tinted ice giant rotating on a 98-degree axial tilt.",
                    SheetKey = "uranus_sheet",
                    OrbitRadiusPx = 1085f,
                    Eccentricity = 0.05f,
                    PerihelionAngleDeg = 170f,
                    InitialPhaseDeg = 278f,
                    OrbitalSpeedDegPerSec = 0.68f,
                    VisualWidthPx = 46f,
                    VisualHeightPx = 46f,
                    OrbitColor = new Color(0.56f, 0.90f, 0.98f, 0.32f),
                    AccentColor = new Color(0.62f, 0.94f, 1.0f, 1f)
                },
                new BodyDefinition
                {
                    Id = "neptune",
                    ParentId = null,
                    DisplayName = "Neptune",
                    Classification = "Ice Giant",
                    DistanceLabel = "30.05 AU",
                    PeriodLabel = "164.8 years",
                    SummaryText = "Outermost major planet with supersonic 2,100 km/h deep-blue storms.",
                    SheetKey = "neptune_sheet",
                    OrbitRadiusPx = 1235f,
                    Eccentricity = 0.01f,
                    PerihelionAngleDeg = 44f,
                    InitialPhaseDeg = 18f,
                    OrbitalSpeedDegPerSec = 0.45f,
                    VisualWidthPx = 44f,
                    VisualHeightPx = 44f,
                    OrbitColor = new Color(0.32f, 0.52f, 0.98f, 0.34f),
                    AccentColor = new Color(0.42f, 0.64f, 1.0f, 1f)
                }
            };
        }

        public static Vector2 EvaluateEllipsePosition(float aPx, float e, float tiltDeg, float angleDeg)
        {
            if (aPx <= 0.01f)
            {
                return Vector2.zero;
            }

            float rad = angleDeg * Mathf.Deg2Rad;
            float bPx = aPx * Mathf.Sqrt(Mathf.Max(0.25f, 1f - e * e));
            float focusOffsetX = aPx * e;

            float localX = aPx * Mathf.Cos(rad) - focusOffsetX;
            float localY = bPx * Mathf.Sin(rad);

            float tiltRad = tiltDeg * Mathf.Deg2Rad;
            float cosT = Mathf.Cos(tiltRad);
            float sinT = Mathf.Sin(tiltRad);

            return new Vector2(
                localX * cosT - localY * sinT,
                localX * sinT + localY * cosT);
        }

        public static string MapOrbitToBodyId(AstraLink.Core.OrbitDefinition orbit, AstraLink.Core.MissionGoal goal)
        {
            string orbitId = orbit != null ? (orbit.Id ?? string.Empty).ToLowerInvariant() : string.Empty;
            string goalTitle = goal != null ? (goal.Title ?? string.Empty).ToLowerInvariant() : string.Empty;

            if (orbit != null)
            {
                if (orbit.Type == AstraLink.Core.OrbitType.Mars || orbitId.Contains("mars")) return "mars";
                if (orbit.Type == AstraLink.Core.OrbitType.Lunar || orbitId.Contains("lunar") || orbitId.Contains("moon")) return "moon";
                if (orbit.Type == AstraLink.Core.OrbitType.DeepSpace || orbitId.Contains("venus")) return "venus";
            }

            if (goalTitle.Contains("mars")) return "mars";
            if (goalTitle.Contains("lunar") || goalTitle.Contains("moon")) return "moon";
            if (goalTitle.Contains("venus")) return "venus";
            return "earth";
        }
    }
}
