using System;

namespace AstraLink.Core
{
    [Serializable]
    public class OrbitDefinition
    {
        public string Id;
        public string Name;
        public OrbitType Type;
        public float OrbitPeriodHours;
        public float EclipseFraction;
        public float CommWindowFraction;
        public float SolarFluxFactor;
        public float SignalDelaySeconds;
        public float BaseRadiation;
    }
}
