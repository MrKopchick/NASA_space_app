using System;
using System.Collections.Generic;

namespace AstraLink.Core
{
    /// <summary>
    /// S1-04: deterministic orbital ticks and resource consumption.
    /// No autopilot/manual commands or triggered events here; they belong to S2-01.
    /// Uses the existing ResourceCalculator and shared Data Contract models.
    /// </summary>
    public class OrbitSimulator
    {
        public const int TicksPerOrbit = 20;

        private readonly ResourceCalculator _resourceCalculator = new ResourceCalculator();

        public MissionReport RunOrbitTicks(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal)
        {
            var report = new MissionReport { Status = MissionStatus.NotStarted };
            if (components == null || rocket == null || orbit == null || goal == null)
            {
                report.Status = MissionStatus.Failed;
                report.FailureTitle = "Invalid mission configuration";
                report.FailureCause = "One or more required inputs are missing.";
                return report;
            }

            ResourceSummary resources = _resourceCalculator.CalculateResources(components, rocket, orbit, goal);
            report.BudgetSpentThousands = resources.TotalCostThousands;
            if (!resources.IsBudgetValid || !resources.IsMassValid)
            {
                report.Status = MissionStatus.Failed;
                report.FailureTitle = "Invalid launch configuration";
                report.FailureCause = !resources.IsBudgetValid
                    ? "Satellite and rocket cost exceeds the mission budget."
                    : "Satellite mass exceeds rocket payload capacity for this orbit.";
                return report;
            }

            if (resources.MaxBatteryWh <= 0f || resources.MaxSsdGb <= 0f || goal.RequiredOrbits <= 0)
            {
                report.Status = MissionStatus.Failed;
                report.FailureTitle = "Missing required resources";
                report.FailureCause = "Battery, SSD capacity and mission duration must be positive.";
                return report;
            }

            float baseDraw = 0f;
            float scienceDraw = 0f;
            float commsDraw = 0f;
            float dataPerTick = 0f;
            float sciencePerTick = 0f;

            foreach (SatelliteComponent component in components)
            {
                if (component == null) continue;
                baseDraw += component.BasePowerDrawWatts;
                if (component.Category == ComponentCategory.SciencePayload)
                {
                    scienceDraw += component.ActivePowerDrawWatts;
                    dataPerTick += component.DataGenGbPerTick;
                    sciencePerTick += component.SciencePointsPerTick;
                }
                else if (component.Category == ComponentCategory.CommsAndStorage)
                {
                    commsDraw += component.ActivePowerDrawWatts;
                }
            }

            float batteryWh = resources.MaxBatteryWh;
            float ssdUsedGb = 0f;
            float pendingScience = 0f;
            float deliveredScience = 0f;
            float lostDataGb = 0f;
            float health = 100f;
            float minBatteryPercent = 100f;
            float tickHours = orbit.OrbitPeriodHours / TicksPerOrbit;
            bool scienceActive = true;
            bool commsActive = true;
            bool safeModeActive = false;
            int totalTicks = goal.RequiredOrbits * TicksPerOrbit;
            report.Status = MissionStatus.InProgress;

            for (int tick = 0; tick < totalTicks; tick++)
            {
                float position = (tick % TicksPerOrbit) / (float)TicksPerOrbit;
                bool eclipse = position >= 1f - orbit.EclipseFraction;
                bool inCommWindow = position < orbit.CommWindowFraction;
                float generation = (eclipse ? 0f : resources.MaxSolarWatts * orbit.SolarFluxFactor)
                                   + resources.RtgWatts;

                float draw = safeModeActive
                    ? baseDraw * 0.5f
                    : baseDraw + (scienceActive ? scienceDraw : 0f)
                        + (commsActive && inCommWindow ? commsDraw : 0f);
                batteryWh = Clamp(batteryWh + (generation - draw) * tickHours, 0f, resources.MaxBatteryWh);
                float batteryPercent = 100f * batteryWh / resources.MaxBatteryWh;
                minBatteryPercent = Math.Min(minBatteryPercent, batteryPercent);

                if (batteryWh > 0f)
                {
                    if (scienceActive && !safeModeActive)
                    {
                        ssdUsedGb += dataPerTick;
                        pendingScience += sciencePerTick;
                        if (ssdUsedGb > resources.MaxSsdGb)
                        {
                            lostDataGb += ssdUsedGb - resources.MaxSsdGb;
                            pendingScience *= resources.MaxSsdGb / ssdUsedGb;
                            ssdUsedGb = resources.MaxSsdGb;
                        }
                    }

                    if (commsActive && !safeModeActive && inCommWindow && ssdUsedGb > 0.0001f)
                    {
                        float sentGb = Math.Min(ssdUsedGb, resources.DownlinkGbPerTick);
                        float deliveredNow = pendingScience * (sentGb / ssdUsedGb);
                        deliveredScience += deliveredNow;
                        pendingScience -= deliveredNow;
                        ssdUsedGb -= sentGb;
                    }
                }

                float radiation = orbit.BaseRadiation; // Event radiation is added in S2-01.
                if (radiation > 50f && !safeModeActive && batteryWh > 0f)
                {
                    health = Math.Max(0f, health - Math.Max(0f,
                        (radiation - resources.TotalShielding * resources.OverallReliability) * 0.4f));
                }

                report.Ticks.Add(new SimulationTickSnapshot
                {
                    TickIndex = tick,
                    OrbitNumber = tick / TicksPerOrbit + 1,
                    OrbitProgress = position,
                    IsInEclipse = eclipse,
                    IsInCommWindow = inCommWindow,
                    SafeModeActive = safeModeActive,
                    ScienceActive = scienceActive,
                    CommsActive = commsActive,
                    SolarGenWatts = generation,
                    PowerDrawWatts = draw,
                    BatteryWh = batteryWh,
                    BatteryPercent = batteryPercent,
                    SsdUsedGb = ssdUsedGb,
                    SsdUsedPercent = 100f * ssdUsedGb / resources.MaxSsdGb,
                    RadiationLevel = radiation,
                    HealthPercent = health,
                    DeliveredScience = deliveredScience,
                    ActiveEventTitle = string.Empty
                });

                if (batteryWh <= 0f || health <= 0f)
                {
                    report.Status = MissionStatus.Failed;
                    report.FailureTitle = batteryWh <= 0f ? "Battery depleted" : "Electronics failure";
                    report.FailureCause = "Failure on orbit " + (tick / TicksPerOrbit + 1)
                        + ", tick " + tick + ".";
                    break;
                }
            }

            report.DeliveredScience = deliveredScience;
            report.LostDataGb = lostDataGb;
            report.MinBatteryPercent = minBatteryPercent;
            report.FinalHealthPercent = health;

            if (report.Status == MissionStatus.InProgress)
            {
                report.Status = deliveredScience >= goal.TargetSciencePoints
                    ? MissionStatus.Success : MissionStatus.Failed;
                if (report.Status == MissionStatus.Failed)
                {
                    report.FailureTitle = "Insufficient science data";
                    report.FailureCause = "Delivered " + deliveredScience.ToString("F1")
                        + " of " + goal.TargetSciencePoints.ToString("F1") + " required science points.";
                }
            }

            if (report.Status == MissionStatus.Success)
            {
                float bonus = minBatteryPercent >= 15f && health >= 80f &&
                              resources.OverallReliability >= 0.85f ? 50f : 0f;
                report.FinalScore = deliveredScience
                    + (goal.BudgetLimitThousands - resources.TotalCostThousands) * 0.5f + bonus;
            }

            return report;
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
