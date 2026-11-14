using System;
using System.Collections.Generic;

namespace AstraLink.Core
{
    public class ResourceCalculator
    {
        public ResourceSummary CalculateResources(
            List<SatelliteComponent> components,
            RocketOption rocketOption,
            OrbitDefinition orbitDefinition,
            MissionGoal missionGoal)
        {
            var summary = new ResourceSummary();
            if (components == null || rocketOption == null || orbitDefinition == null || missionGoal == null)
                return summary;

            float totalMass = 0f;
            float totalCostThousands = rocketOption.CostThousands;
            float maxSolarWatts = 0f;
            float rtgWatts = 0f;
            float maxBatteryWh = 0f;
            float maxSsdGb = 0f;
            float downlinkGbPerTick = 0f;
            float totalShielding = 0f;
            float overallReliability = rocketOption.BaseReliability;

            for (int i = 0; i < components.Count; i++)
            {
                var c = components[i];
                if (c == null) continue;

                totalMass += c.MassKg;
                totalCostThousands += c.CostThousands;
                maxSolarWatts += c.SolarWatts;
                rtgWatts += c.RtgWatts;
                maxBatteryWh += c.BatteryCapacityWh;
                maxSsdGb += c.SsdCapacityGb;
                downlinkGbPerTick += c.DownlinkRateGbPerTick;
                totalShielding += c.RadiationShielding;
                overallReliability *= c.BaseReliability;
            }

            summary.TotalMassKg = totalMass;
            summary.TotalCostThousands = totalCostThousands;
            summary.MaxSolarWatts = maxSolarWatts;
            summary.RtgWatts = rtgWatts;
            summary.MaxBatteryWh = maxBatteryWh;
            summary.MaxSsdGb = maxSsdGb;
            summary.DownlinkGbPerTick = downlinkGbPerTick;
            summary.TotalShielding = totalShielding;
            summary.OverallReliability = Math.Clamp(overallReliability, 0f, 1f);
            summary.IsBudgetValid = totalCostThousands <= missionGoal.BudgetLimitThousands;
            summary.IsMassValid = totalMass <= MaxOrbitKg(rocketOption, orbitDefinition.Type);

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
