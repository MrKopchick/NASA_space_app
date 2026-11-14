using System;
using System.Collections.Generic;

namespace AstraLink.Core
{
    public class ResourceCalculator
    {
        public ResourceSummary ResourceCalculator(List<SatelliteComponent> components, RocketOption rocketOption, OrbitDefinition orbitDefinition, MissionGoal missionGoal)
        {
            var summary = new ResourceSummary();

            float totalMass = 0f;
            float TotalCostThousands = rocketOption.CostThousands;
            float MaxSolarWatts = 0f;
            float RtgWatts = 0f;
            float MaxBatteryWh = 0f;
            float MaxSsdGb = 0f;
            float DownlinkGbPerTick = 0f;
            float TotalShielding = 0f;
            float OverallReliability = 1f;
            bool IsBudgetValid = false;
            bool IsMassValid = false;

            for (int i = 0; i < components.Count; i++) {
                if (components[i] == null) continue;
                totalMass += components[i].MassKg;
                TotalCostThousands += components[i].CostThousands;
                MaxSolarWatts += components[i].SolarWatts;
                RtgWatts += components[i].RtgWatts;
                MaxBatteryWh += components[i].BatteryCapacityWh;
                MaxSsdGb += components[i].SsdCapacityGb;
                DownlinkGbPerTick += components[i].DownlinkRateGbPerTick;
                TotalShielding += components[i].RadiationShielding;
                OverallReliability *= components[i].BaseReliability;
            }
            if (TotalCostThousands <= MissionGoal.BudgetLimitThousands) IsBudgetValid = true;
            if (totalMass <= MaxOrbitKg(rocketOption, orbitDefinition.OrbitType)) IsMassValid = true;
            summary.TotalMassKg = totalMass;
            summary.TotalCostThousands = TotalCostThousands;
            summary.MaxSolarWatts = maxSolarWatts;
            summary.RtgWatts = rtgWatts;
            summary.MaxBatteryWh = maxBatteryWh;
            summary.MaxSsdGb = maxSsdGb;
            summary.DownlinkGbPerTick = downlinkGbPerTick;
            summary.TotalShielding = totalShielding;
            summary.OverallReliability = OverallReliability;
            summary.IsBudgetValid = IsBudgetValid;
            summary.IsMassValid = IsMassValid;
            
            return summary;
        }

        private float MaxOrbitKg(RocketOption rocket, OrbitType orbitType)
        {
            switch (orbitType)
            {
                case OrbitType.LowEarth: return rocket.MaxMassLeoKg;
                case OrbitType.Lunar: return rocket.MaxMassLunarKg;
                case OrbitType.Mars: return rocket.MaxMassMarsKg;
                case OrbitType.DeepSpace: return rocket.MaxMassDeepSpaceKg;
                default: return 0f;
            }
        }
    }
}