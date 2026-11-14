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

    public class OrbitSimulator : IMissionSimulator
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
            if (LogMessages.TryGetValue(logType, out string message))
            {
                return message;
            }
            return string.Empty;
        }

        public static string FormatTickLog(SimulationTickSnapshot tick)
        {
            if (tick == null) return GetLogMessage(OrbitLogType.None);
            return string.Format(CultureInfo.InvariantCulture, GetLogMessage(OrbitLogType.TickTelemetry), tick.OrbitNumber, tick.TickIndex, tick.IsInEclipse, tick.IsInCommWindow, tick.SolarGenWatts, tick.PowerDrawWatts, tick.BatteryWh, tick.SsdUsedGb, tick.HealthPercent);
        }

        private readonly ResourceCalculator _resourceCalculator = new ResourceCalculator();
        private readonly AutopilotEvaluator _autopilotEvaluator = new AutopilotEvaluator();
        private readonly List<SpaceEventDefinition> _defaultEvents;
        private readonly Random _random;

        public OrbitSimulator(List<SpaceEventDefinition> defaultEvents = null, int? randomSeed = null)
        {
            _defaultEvents = defaultEvents;
            _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        }

        public ResourceSummary CalculateResources(List<SatelliteComponent> components, RocketOption rocket, OrbitDefinition orbit, MissionGoal goal)
        {
            return _resourceCalculator.CalculateResources(components, rocket, orbit, goal);
        }

        public bool ValidateLaunch(List<SatelliteComponent> components, RocketOption rocket, OrbitDefinition orbit, MissionGoal goal, out string errorMessage)
        {
            if (components == null || rocket == null || orbit == null || goal == null) { errorMessage = GetLogMessage(OrbitLogType.MissingInputs); return false; }
            ResourceSummary resources = _resourceCalculator.CalculateResources(components, rocket, orbit, goal);
            if (!resources.IsBudgetValid) { errorMessage = GetLogMessage(OrbitLogType.BudgetExceeded); return false; }
            if (!resources.IsMassValid) { errorMessage = GetLogMessage(OrbitLogType.MassExceeded); return false; }
            if (resources.MaxBatteryWh <= 0f || resources.MaxSsdGb <= 0f || goal.RequiredOrbits <= 0) { errorMessage = GetLogMessage(OrbitLogType.InvalidResourceValues); return false; }
            errorMessage = string.Empty;
            return true;
        }

        public MissionReport RunOrbitTicks(List<SatelliteComponent> components, RocketOption rocket, OrbitDefinition orbit, MissionGoal goal)
        {
            return SimulateInternal(components, rocket, orbit, goal, null, null, null, goal != null ? goal.RequiredOrbits : 0, false);
        }

        public MissionReport RunDryRun(List<SatelliteComponent> components, RocketOption rocket, OrbitDefinition orbit, MissionGoal goal, List<AutopilotRule> rules)
        {
            return SimulateInternal(components, rocket, orbit, goal, rules, null, null, 1, true);
        }

        public MissionReport SimulateFullMission(List<SatelliteComponent> components, RocketOption rocket, OrbitDefinition orbit, MissionGoal goal, List<AutopilotRule> rules, List<ManualCommandRequest> manualCommands = null, List<SpaceEventDefinition> overrideEvents = null)
        {
            int orbits = goal != null ? goal.RequiredOrbits : 0;
            List<SpaceEventDefinition> eventsToUse;

            if (overrideEvents != null)
            {
                eventsToUse = overrideEvents;
            }
            else if (goal == null || string.IsNullOrEmpty(goal.EventId))
            {
                eventsToUse = new List<SpaceEventDefinition>();
            }
            else
            {
                eventsToUse = new List<SpaceEventDefinition>();
                if (_defaultEvents != null)
                {
                    for (int i = 0; i < _defaultEvents.Count; i++)
                    {
                        var ev = _defaultEvents[i];
                        if (ev != null && ev.Id == goal.EventId)
                        {
                            eventsToUse.Add(ev);
                            break;
                        }
                    }
                }
            }

            return SimulateInternal(components, rocket, orbit, goal, rules, manualCommands, eventsToUse, orbits, false);
        }

        private MissionReport SimulateInternal(List<SatelliteComponent> components, RocketOption rocket, OrbitDefinition orbit, MissionGoal goal, List<AutopilotRule> rules, List<ManualCommandRequest> manualCommands, List<SpaceEventDefinition> events, int orbitsToSimulate, bool isDryRun)
        {
            var report = new MissionReport { Status = MissionStatus.NotStarted, Ticks = new List<SimulationTickSnapshot>() };
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
                if (!resources.IsBudgetValid) report.FailureCause = GetLogMessage(OrbitLogType.BudgetExceeded);
                else report.FailureCause = GetLogMessage(OrbitLogType.MassExceeded);
                return report;
            }

            if (resources.MaxBatteryWh <= 0f || resources.MaxSsdGb <= 0f || orbitsToSimulate <= 0)
            {
                report.Status = MissionStatus.Failed;
                report.FailureTitle = GetLogMessage(OrbitLogType.MissingRequiredResources);
                report.FailureCause = GetLogMessage(OrbitLogType.InvalidResourceValues);
                return report;
            }

            float baseDraw = 0f;
            float sciencePowerDraw = 0f;
            float commsPowerDraw = 0f;
            float dataPerTick = 0f;
            float sciencePerTick = 0f;

            for (int i = 0; i < components.Count; i++)
            {
                SatelliteComponent component = components[i];
                if (component == null) continue;
                baseDraw += component.BasePowerDrawWatts;
                if (component.Category == ComponentCategory.SciencePayload)
                {
                    sciencePowerDraw += component.ActivePowerDrawWatts;
                    dataPerTick += component.DataGenGbPerTick;
                    sciencePerTick += component.SciencePointsPerTick;
                }
                else if (component.Category == ComponentCategory.CommsAndStorage) { commsPowerDraw += component.ActivePowerDrawWatts; }
            }

            float batteryWh = resources.MaxBatteryWh, ssdUsedGb = 0f, pendingScience = 0f, deliveredScience = 0f, lostDataGb = 0f, health = 100f, minBatteryPercent = 100f;
            float tickHours = orbit.OrbitPeriodHours / TicksPerOrbit;
            bool scienceActive = true, commsActive = true, safeModeActive = false;
            int totalTicks = orbitsToSimulate * TicksPerOrbit;
            report.Status = MissionStatus.InProgress;

            int[] eventStartTicks = null;
            if (!isDryRun && events != null && events.Count > 0)
            {
                eventStartTicks = new int[events.Count];
                for (int e = 0; e < events.Count; e++)
                {
                    SpaceEventDefinition ev = events[e];
                    if (ev == null)
                    {
                        eventStartTicks[e] = -1;
                        continue;
                    }

                    if (ev.TriggerOrbit > 0)
                    {
                        eventStartTicks[e] = (ev.TriggerOrbit - 1) * TicksPerOrbit + _random.Next(0, TicksPerOrbit);
                    }
                    else
                    {
                        int maxStartTick = Math.Max(1, totalTicks - ev.DurationTicks + 1);
                        eventStartTicks[e] = _random.Next(0, maxStartTick);
                    }
                }
            }

            for (int tick = 0; tick < totalTicks; tick++)
            {
                int orbitNumber = tick / TicksPerOrbit + 1;
                float position = (tick % TicksPerOrbit) / (float)TicksPerOrbit;
                bool eclipse = false;
                if (position >= 1f - orbit.EclipseFraction) eclipse = true;
                else eclipse = false;
                bool inCommWindow = position < orbit.CommWindowFraction;

                float eventRadiationBoost = 0f, eventSolarMultiplier = 1f, eventDownlinkMultiplier = 1f;
                string activeEventTitle = string.Empty;

                if (!isDryRun && events != null && eventStartTicks != null)
                {
                    for (int e = 0; e < events.Count; e++)
                    {
                        SpaceEventDefinition ev = events[e];
                        if (ev == null) continue;
                        int startTick = eventStartTicks[e];
                        if (startTick >= 0 && tick >= startTick && tick < startTick + ev.DurationTicks)
                        {
                            ApplyEventModifiers(ev, ref eventRadiationBoost, ref eventSolarMultiplier, ref eventDownlinkMultiplier);
                            if (string.IsNullOrEmpty(activeEventTitle))
                            {
                                if (!string.IsNullOrEmpty(ev.Title)) activeEventTitle = ev.Title;
                                else activeEventTitle = ev.Type.ToString();
                            }
                        }
                    }
                }

                if (!isDryRun && manualCommands != null && manualCommands.Count > 0)
                {
                    _autopilotEvaluator.ApplyManualCommandsForTick(tick, manualCommands, ref scienceActive, ref commsActive, ref safeModeActive);
                }

                float solarGenBeforeRules = eclipse ? 0f : resources.MaxSolarWatts * orbit.SolarFluxFactor * eventSolarMultiplier;
                float batteryPercentBeforeRules = 100f * batteryWh / resources.MaxBatteryWh;
                float ssdPercentBeforeRules = 100f * ssdUsedGb / resources.MaxSsdGb;
                float radiation = orbit.BaseRadiation + eventRadiationBoost;

                var triggeredRuleIds = new List<string>();
                if (rules != null && rules.Count > 0)
                {
                    _autopilotEvaluator.EvaluateRulesForTick(rules, solarGenBeforeRules, batteryPercentBeforeRules, ssdPercentBeforeRules, radiation, ref scienceActive, ref commsActive, ref safeModeActive, triggeredRuleIds);
                }

                AutopilotEvaluator.ResolveEffectiveSubsystems(scienceActive, commsActive, safeModeActive, out bool effectiveScience, out bool effectiveComms);

                float generation = solarGenBeforeRules + resources.RtgWatts;
                float draw = safeModeActive ? baseDraw * 0.5f : baseDraw + (effectiveScience ? sciencePowerDraw : 0f) + (effectiveComms && inCommWindow ? commsPowerDraw : 0f);

                batteryWh = Clamp(batteryWh + (generation - draw) * tickHours, 0f, resources.MaxBatteryWh);
                float batteryPercent = 100f * batteryWh / resources.MaxBatteryWh;
                minBatteryPercent = Math.Min(minBatteryPercent, batteryPercent);

                if (batteryWh > 0f)
                {
                    if (effectiveScience)
                    {
                        ssdUsedGb += dataPerTick; pendingScience += sciencePerTick;
                        if (ssdUsedGb > resources.MaxSsdGb) { lostDataGb += ssdUsedGb - resources.MaxSsdGb; pendingScience *= resources.MaxSsdGb / ssdUsedGb; ssdUsedGb = resources.MaxSsdGb; }
                    }
                    if (effectiveComms && inCommWindow && ssdUsedGb > 0.0001f)
                    {
                        float effectiveDownlink = resources.DownlinkGbPerTick * eventDownlinkMultiplier;
                        float sentGb = Math.Min(ssdUsedGb, effectiveDownlink);
                        float deliveredNow = pendingScience * (sentGb / ssdUsedGb);
                        deliveredScience += deliveredNow; pendingScience -= deliveredNow; ssdUsedGb -= sentGb;
                    }
                }

                if (radiation > 50f && !safeModeActive && batteryWh > 0f)
                {
                    health = Math.Max(0f, health - Math.Max(0f, (radiation - resources.TotalShielding * resources.OverallReliability) * 0.4f));
                }

                report.Ticks.Add(new SimulationTickSnapshot
                {
                    TickIndex = tick,
                    OrbitNumber = orbitNumber,
                    OrbitProgress = position,
                    IsInEclipse = eclipse,
                    IsInCommWindow = inCommWindow,
                    SafeModeActive = safeModeActive,
                    ScienceActive = effectiveScience,
                    CommsActive = effectiveComms,
                    SolarGenWatts = generation,
                    PowerDrawWatts = draw,
                    BatteryWh = batteryWh,
                    BatteryPercent = batteryPercent,
                    SsdUsedGb = ssdUsedGb,
                    SsdUsedPercent = 100f * ssdUsedGb / resources.MaxSsdGb,
                    RadiationLevel = radiation,
                    HealthPercent = health,
                    DeliveredScience = deliveredScience,
                    ActiveEventTitle = activeEventTitle,
                    TriggeredRuleIds = triggeredRuleIds
                });

                if (batteryWh <= 0f || health <= 0f)
                {
                    report.Status = MissionStatus.Failed;
                    report.FailureTitle = batteryWh <= 0f ? GetLogMessage(OrbitLogType.BatteryDepleted) : GetLogMessage(OrbitLogType.ElectronicsFailure);
                    report.FailureCause = string.Format(CultureInfo.InvariantCulture, GetLogMessage(OrbitLogType.FailureLocation), orbitNumber, tick);
                    break;
                }
            }

            report.DeliveredScience = deliveredScience; report.LostDataGb = lostDataGb; report.MinBatteryPercent = minBatteryPercent; report.FinalHealthPercent = health;

            if (report.Status == MissionStatus.InProgress)
            {
                if (isDryRun) { report.Status = MissionStatus.Success; }
                else
                {
                    report.Status = deliveredScience >= goal.TargetSciencePoints ? MissionStatus.Success : MissionStatus.Failed;
                    if (report.Status == MissionStatus.Failed)
                    {
                        report.FailureTitle = GetLogMessage(OrbitLogType.InsufficientScienceData);
                        report.FailureCause = string.Format(CultureInfo.InvariantCulture, GetLogMessage(OrbitLogType.InsufficientScienceDetails), deliveredScience, goal.TargetSciencePoints);
                    }
                }
            }

            if (report.Status == MissionStatus.Success)
            {
                float marginBonus = minBatteryPercent >= 15f && health >= 80f && resources.OverallReliability >= 0.85f ? 50f : 0f;
                report.FinalScore = deliveredScience + (goal.BudgetLimitThousands - resources.TotalCostThousands) * 0.5f + marginBonus;
            }

            return report;
        }

        private static void ApplyEventModifiers(SpaceEventDefinition ev, ref float radiationBoost, ref float solarMultiplier, ref float downlinkMultiplier)
        {
            bool isPresetWithDefaults = ev.Type != SpaceEventType.Custom
                                        && Math.Abs(ev.RadiationBoost) < 0.0001f
                                        && Math.Abs(ev.SolarMultiplier - 1f) < 0.0001f
                                        && Math.Abs(ev.DownlinkMultiplier - 1f) < 0.0001f;

            if (!isPresetWithDefaults)
            {
                radiationBoost += ev.RadiationBoost;
                solarMultiplier *= Math.Max(0f, ev.SolarMultiplier);
                downlinkMultiplier *= Math.Max(0f, ev.DownlinkMultiplier);
                return;
            }

            switch (ev.Type)
            {
                case SpaceEventType.CoronalMassEjection: radiationBoost += 85f; break;
                case SpaceEventType.TransmitterOverheat: downlinkMultiplier *= 0.45f; break;
                case SpaceEventType.MicrometeoroidNoise: solarMultiplier *= 0.6f; radiationBoost += 15f; break;
                default: break;
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}