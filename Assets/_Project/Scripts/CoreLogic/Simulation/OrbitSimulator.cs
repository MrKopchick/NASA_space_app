using System;
using System.Collections.Generic;
using System.Globalization;

namespace AstraLink.Core
{
    public enum OrbitLogType
    {
        None,
        InvalidMissionConfiguration,
        MissingInputs,
        InvalidLaunchConfiguration,
        BudgetExceeded,
        MassExceeded,
        MissingRequiredResources,
        InvalidResourceValues,
        BatteryDepleted,
        ElectronicsFailure,
        FailureLocation,
        InsufficientScienceData,
        InsufficientScienceDetails,
        TickTelemetry
    }

    /// <summary>
    /// S1-04: deterministic orbital ticks and resource consumption.
    /// No autopilot/manual commands or triggered events here; they belong to S2-01.
    /// Uses the existing ResourceCalculator and shared Data Contract models.
    /// </summary>
    public class OrbitSimulator
    {
        public const int TicksPerOrbit = 20;

        private static readonly Dictionary<OrbitLogType, string> LogMessages = new Dictionary<OrbitLogType, string>
        {
            { OrbitLogType.None, string.Empty },
            { OrbitLogType.InvalidMissionConfiguration, "Invalid mission configuration" },
            { OrbitLogType.MissingInputs, "One or more required inputs are missing." },
            { OrbitLogType.InvalidLaunchConfiguration, "Invalid launch configuration" },
            { OrbitLogType.BudgetExceeded, "Satellite and rocket cost exceeds the mission budget." },
            { OrbitLogType.MassExceeded, "Satellite mass exceeds rocket payload capacity for this orbit." },
            { OrbitLogType.MissingRequiredResources, "Missing required resources" },
            { OrbitLogType.InvalidResourceValues, "Battery, SSD capacity and mission duration must be positive." },
            { OrbitLogType.BatteryDepleted, "Battery depleted" },
            { OrbitLogType.ElectronicsFailure, "Electronics failure" },
            { OrbitLogType.FailureLocation, "Failure on orbit {0}, tick {1}." },
            { OrbitLogType.InsufficientScienceData, "Insufficient science data" },
            { OrbitLogType.InsufficientScienceDetails, "Delivered {0:F1} of {1:F1} required science points." },
            { OrbitLogType.TickTelemetry, "Orbit {0}, tick {1}: {2}, comms={3}, solar={4:F1} W, draw={5:F1} W, battery={6:F2} Wh, SSD={7:F2} Gb, health={8:F1}%." }
        };

        public static string GetLogMessage(OrbitLogType logType)
        {
            string message;
            return LogMessages.TryGetValue(logType, out message) ? message : string.Empty;
        }

        /// <summary>Formats one tick for a plain C# console test; no UnityEngine dependency.</summary>
        public static string FormatTickLog(SimulationTickSnapshot tick)
        {
            if (tick == null)
                return GetLogMessage(OrbitLogType.None);

            return string.Format(
                CultureInfo.InvariantCulture,
                GetLogMessage(OrbitLogType.TickTelemetry),
                tick.OrbitNumber,
                tick.TickIndex,
                tick.IsInEclipse ? "eclipse" : "sunlight",
                tick.IsInCommWindow,
                tick.SolarGenWatts,
                tick.PowerDrawWatts,
                tick.BatteryWh,
                tick.SsdUsedGb,
                tick.HealthPercent);
        }

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
                report.FailureTitle = GetLogMessage(OrbitLogType.InvalidMissionConfiguration);
                report.FailureCause = GetLogMessage(OrbitLogType.MissingInputs);
                return report;
            }

            ResourceSummary resources = _resourceCalculator.CalculateResources(components, rocket, orbit, goal);
            report.BudgetSpentThousands = resources.TotalCostThousands;
            if (!resources.IsBudgetValid || !resources.IsMassValid)
            {
                report.Status = MissionStatus.Failed;
                report.FailureTitle = GetLogMessage(OrbitLogType.InvalidLaunchConfiguration);
                report.FailureCause = !resources.IsBudgetValid
                    ? GetLogMessage(OrbitLogType.BudgetExceeded)
                    : GetLogMessage(OrbitLogType.MassExceeded);
                return report;
            }

            if (resources.MaxBatteryWh <= 0f || resources.MaxSsdGb <= 0f || goal.RequiredOrbits <= 0)
            {
                report.Status = MissionStatus.Failed;
                report.FailureTitle = GetLogMessage(OrbitLogType.MissingRequiredResources);
                report.FailureCause = GetLogMessage(OrbitLogType.InvalidResourceValues);
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
                    report.FailureTitle = batteryWh <= 0f
                        ? GetLogMessage(OrbitLogType.BatteryDepleted)
                        : GetLogMessage(OrbitLogType.ElectronicsFailure);
                    report.FailureCause = string.Format(
                        CultureInfo.InvariantCulture,
                        GetLogMessage(OrbitLogType.FailureLocation),
                        tick / TicksPerOrbit + 1,
                        tick);
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
                    report.FailureTitle = GetLogMessage(OrbitLogType.InsufficientScienceData);
                    report.FailureCause = string.Format(
                        CultureInfo.InvariantCulture,
                        GetLogMessage(OrbitLogType.InsufficientScienceDetails),
                        deliveredScience,
                        goal.TargetSciencePoints);
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
