using System;
using System.Collections.Generic;
using AstraLink.Core;

namespace AstraLink.Core
{
    /// <summary>
    /// Mock implementation of IMissionSimulator (Task S1-02) to unblock UI developers (Ivan V, Matviy L)
    /// while pure C# simulation math is being implemented.
    /// </summary>
    public class MockMissionSimulator : IMissionSimulator
    {
        public const int TicksPerOrbit = 20;

        public ResourceSummary CalculateResources(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal)
        {
            if (components == null || components.Count == 0)
            {
                return new ResourceSummary
                {
                    TotalMassKg = 13.0f,
                    TotalCostThousands = 90.0f,
                    MaxSolarWatts = 25.0f,
                    RtgWatts = 0.0f,
                    MaxBatteryWh = 10.0f,
                    MaxSsdGb = 4.0f,
                    DownlinkGbPerTick = 1.0f,
                    OverallReliability = 0.82f,
                    TotalShielding = 10.0f,
                    IsBudgetValid = true,
                    IsMassValid = true
                };
            }

            float totalMass = 0f;
            float totalCost = rocket != null ? rocket.CostThousands : 30.0f;
            float maxSolar = 0f;
            float rtgWatts = 0f;
            float maxBattery = 0f;
            float maxSsd = 0f;
            float downlink = 0f;
            float shielding = 0f;
            float reliability = rocket != null ? rocket.BaseReliability : 0.95f;

            for (int i = 0; i < components.Count; i++)
            {
                SatelliteComponent comp = components[i];
                if (comp == null) continue;

                totalMass += comp.MassKg;
                totalCost += comp.CostThousands;
                maxSolar += comp.SolarWatts;
                rtgWatts += comp.RtgWatts;
                maxBattery += comp.BatteryCapacityWh;
                maxSsd += comp.SsdCapacityGb;
                downlink += comp.DownlinkRateGbPerTick;
                shielding += comp.RadiationShielding;
                reliability *= comp.BaseReliability > 0f ? comp.BaseReliability : 1.0f;
            }

            if (reliability < 0f) reliability = 0f;
            if (reliability > 1f) reliability = 1f;

            float maxAllowedMass = 25.0f;
            if (rocket != null)
            {
                OrbitType orbitType = orbit != null ? orbit.Type : OrbitType.LowEarth;
                switch (orbitType)
                {
                    case OrbitType.LowEarth:
                        maxAllowedMass = rocket.MaxMassLeoKg;
                        break;
                    case OrbitType.Lunar:
                        maxAllowedMass = rocket.MaxMassLunarKg;
                        break;
                    case OrbitType.Mars:
                        maxAllowedMass = rocket.MaxMassMarsKg;
                        break;
                    case OrbitType.DeepSpace:
                        maxAllowedMass = rocket.MaxMassDeepSpaceKg;
                        break;
                }
            }

            float budgetLimit = goal != null && goal.BudgetLimitThousands > 0f ? goal.BudgetLimitThousands : 120.0f;

            return new ResourceSummary
            {
                TotalMassKg = totalMass,
                TotalCostThousands = totalCost,
                MaxSolarWatts = maxSolar,
                RtgWatts = rtgWatts,
                MaxBatteryWh = maxBattery,
                MaxSsdGb = maxSsd,
                DownlinkGbPerTick = downlink,
                OverallReliability = reliability,
                TotalShielding = shielding,
                IsBudgetValid = totalCost <= budgetLimit,
                IsMassValid = totalMass <= maxAllowedMass
            };
        }

        public bool ValidateLaunch(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            out string errorMessage)
        {
            ResourceSummary summary = CalculateResources(components, rocket, orbit, goal);
            if (!summary.IsMassValid)
            {
                errorMessage = "[MockValidator] Перевищено максимальну масу корисного навантаження для обраної ракети та орбіти.";
                return false;
            }

            if (!summary.IsBudgetValid)
            {
                errorMessage = "[MockValidator] Перевищено бюджет місії.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public MissionReport RunDryRun(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules)
        {
            return GenerateMockReport(components, rocket, orbit, goal, rules, TicksPerOrbit);
        }

        public MissionReport SimulateFullMission(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules,
            List<ManualCommandRequest> manualCommands = null,
            List<SpaceEventDefinition> overrideEvents = null)
        {
            return GenerateMockReport(components, rocket, orbit, goal, rules, TicksPerOrbit);
        }

        private MissionReport GenerateMockReport(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules,
            int totalTicks)
        {
            ResourceSummary summary = CalculateResources(components, rocket, orbit, goal);
            float maxBatteryWh = summary.MaxBatteryWh > 0f ? summary.MaxBatteryWh : 10.0f;
            float maxSsdGb = summary.MaxSsdGb > 0f ? summary.MaxSsdGb : 4.0f;
            float eclipseFraction = orbit != null ? orbit.EclipseFraction : 0.35f;
            float commFraction = orbit != null ? orbit.CommWindowFraction : 0.25f;

            var report = new MissionReport
            {
                Status = MissionStatus.Success,
                FinalScore = 145.0f,
                DeliveredScience = 110.0f,
                LostDataGb = 0.0f,
                BudgetSpentThousands = summary.TotalCostThousands,
                MinBatteryPercent = 32.0f,
                FinalHealthPercent = 100.0f,
                FailureTitle = string.Empty,
                FailureCause = string.Empty,
                FailureRecommendation = string.Empty,
                Ticks = new List<SimulationTickSnapshot>(totalTicks)
            };

            float currentBatteryWh = maxBatteryWh;
            float currentSsdGb = 0f;
            float deliveredScience = 0f;

            for (int tick = 0; tick < totalTicks; tick++)
            {
                int orbitNum = (tick / TicksPerOrbit) + 1;
                float u = (tick % TicksPerOrbit) / (float)TicksPerOrbit;
                bool inEclipse = u >= (1.0f - eclipseFraction);
                bool inComm = u < commFraction;

                float solarGen = inEclipse ? 0f : (summary.MaxSolarWatts > 0f ? summary.MaxSolarWatts : 25.0f);
                float powerDraw = inEclipse ? 9.5f : 19.0f;

                if (inEclipse)
                {
                    currentBatteryWh = Math.Max(maxBatteryWh * 0.32f, currentBatteryWh - (maxBatteryWh * 0.08f));
                }
                else
                {
                    currentBatteryWh = Math.Min(maxBatteryWh, currentBatteryWh + (maxBatteryWh * 0.06f));
                }

                if (inComm)
                {
                    currentSsdGb = Math.Max(0f, currentSsdGb - 0.8f);
                    deliveredScience += 22.0f;
                }
                else
                {
                    currentSsdGb = Math.Min(maxSsdGb, currentSsdGb + 0.25f);
                }

                var triggeredRules = new List<string>();
                if (inEclipse && rules != null && rules.Count > 0 && !string.IsNullOrEmpty(rules[0].RuleId))
                {
                    triggeredRules.Add(rules[0].RuleId);
                }

                report.Ticks.Add(new SimulationTickSnapshot
                {
                    TickIndex = tick,
                    OrbitNumber = orbitNum,
                    OrbitProgress = u,
                    IsInEclipse = inEclipse,
                    IsInCommWindow = inComm,
                    SafeModeActive = false,
                    ScienceActive = !inEclipse,
                    CommsActive = inComm,
                    SolarGenWatts = solarGen,
                    PowerDrawWatts = powerDraw,
                    BatteryWh = currentBatteryWh,
                    BatteryPercent = (currentBatteryWh / maxBatteryWh) * 100.0f,
                    SsdUsedGb = currentSsdGb,
                    SsdUsedPercent = (currentSsdGb / maxSsdGb) * 100.0f,
                    RadiationLevel = orbit != null ? orbit.BaseRadiation : 5.0f,
                    HealthPercent = 100.0f,
                    DeliveredScience = deliveredScience,
                    ActiveEventTitle = string.Empty,
                    TriggeredRuleIds = triggeredRules
                });
            }

            return report;
        }
    }
}
