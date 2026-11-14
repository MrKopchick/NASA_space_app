using System;
using System.Collections.Generic;

namespace AstraLink.Core
{
    public class ResourceCalculator(List<SatelliteComponent> components, RocketOption, OrbitDefinition, MissionGoal)
    {
        var Summary = new ResourceSummary();

        float totalMass = 0f;
        float TotalCostThousands = RocketOption.CostThousands;
        float MaxSolarWatts = 0f;
        float RtgWatts = 0f;
        float MaxBatteryWh = 0f;
        float MaxSsdGb = 0f;
        float DownlinkGbPerTick = 0f;
        float TotalShielding = 0f;
        float OverallReliability = 1f;
        bool IsBudgetValid = false;
        bool IsMassValid = false;

        for (int i = 0; i < components.Count; i++){
            if (components[i] == null) continue;
            totalMass += components[i].MassKg;
            TotalCostThousands += components[i].CostThousands;
            MaxSolarWatts += components[i].MaxSolarWatts;
            RtgWatts += components[i].RtgWatts;
            MaxBatteryWh += components[i].MaxBatteryWh;
            MaxSsdGb += components[i].MaxSsdGb;
            DownlinkGbPerTick += components[i].DownlinkGbPerTick;
            TotalShielding += components[i].TotalShielding;
            OverallReliability *= components[i].OverallReliability;
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

        summary.TotalMassKg = totalMass;
        summary.TotalCostThousands = componentsCost;
        summary.MaxSolarWatts = maxSolarWatts;
        summary.RtgWatts = rtgWatts;
        summary.MaxBatteryWh = maxBatteryWh;
        summary.MaxSsdGb = maxSsdGb;
        summary.DownlinkGbPerTick = downlinkGbPerTick;
        summary.TotalShielding = totalShielding;
        summary.OverallReliability = reliabilityProduct;

        if (TotalCostThousands <= MissionGoal.BudgetLimitThousands) IsBudgetValid = true;
        if (totalMass <= MaxOrbitKg(RocketOption, OrbitDefinition.OrbitType)) IsMassValid = true;
        return ResourceSummary;
    }
}