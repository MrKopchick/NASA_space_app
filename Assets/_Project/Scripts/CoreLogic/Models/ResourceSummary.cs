using System;

namespace AstraLink.Core
{
    [Serializable]
    public class ResourceSummary
    {
        public float TotalMassKg;
        public float TotalCostThousands;
        public float MaxSolarWatts;
        public float RtgWatts;
        public float MaxBatteryWh;
        public float MaxSsdGb;
        public float DownlinkGbPerTick;
        public float OverallReliability;
        public float TotalShielding;
        public bool IsBudgetValid;
        public bool IsMassValid;
    }
}
