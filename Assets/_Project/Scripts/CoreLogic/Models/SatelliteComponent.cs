using System;

namespace AstraLink.Core
{
    [Serializable]
    public class SatelliteComponent
    {
        public string Id;
        public string Name;
        public string Description;
        public ComponentCategory Category;
        public float MassKg;
        public float CostThousands;
        public float BasePowerDrawWatts;
        public float ActivePowerDrawWatts;
        public float SolarWatts;
        public float RtgWatts;
        public float BatteryCapacityWh;
        public float SsdCapacityGb;
        public float DownlinkRateGbPerTick;
        public float DataGenGbPerTick;
        public float SciencePointsPerTick;
        public float RadiationShielding;
        public float BaseReliability;
        public int UnlockCostThousands;
        public string SensorTag;
        public bool UnlockedByDefault;
    }
}
