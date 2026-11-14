using System;
using System.Collections.Generic;
using System.Globalization;

namespace AstraLink.Core
{
    public enum AstraAuditCheckType
    {
        None,
        IncompleteConfiguration,
        MassLimitExceeded,
        BudgetLimitExceeded,
        MissingRequiredSensor,
        EclipseBatteryDrainRisk,
        SsdOverflowRisk,
        SolarStormSafeModeMissing,
        RtgSolarRuleTrap,
        ConfigurationOptimal
    }

    /// <summary>
    /// Performs seven deterministic pre-launch engineering checks on the satellite configuration and autopilot rules using ResourceCalculator.
    /// </summary>
    public class AstraAuditor : IAstraAuditor
    {
        private static readonly Dictionary<AstraAuditCheckType, string> AuditTitles = new Dictionary<AstraAuditCheckType, string>
        {
            { AstraAuditCheckType.IncompleteConfiguration, "Incomplete Mission Configuration" },
            { AstraAuditCheckType.MassLimitExceeded, "Rocket Mass Limit Exceeded (+{0:F1} kg)" },
            { AstraAuditCheckType.BudgetLimitExceeded, "Mission Budget Exceeded (+${0:F0}k)" },
            { AstraAuditCheckType.MissingRequiredSensor, "Missing Required Mission Sensor" },
            { AstraAuditCheckType.EclipseBatteryDrainRisk, "Eclipse Battery Depletion Risk" },
            { AstraAuditCheckType.SsdOverflowRisk, "Onboard SSD Overflow Risk" },
            { AstraAuditCheckType.SolarStormSafeModeMissing, "Solar Storm (CME) Radiation Risk" },
            { AstraAuditCheckType.RtgSolarRuleTrap, "RTG Logic Trap: Rule Uses SolarWatts" },
            { AstraAuditCheckType.ConfigurationOptimal, "A.S.T.R.A.: Configuration Optimal" }
        };

        private static readonly Dictionary<AstraAuditCheckType, string> AuditAdvices = new Dictionary<AstraAuditCheckType, string>
        {
            { AstraAuditCheckType.IncompleteConfiguration, "Select a mission contract, launch vehicle, target orbit, and all 5 mandatory satellite modules." },
            { AstraAuditCheckType.MassLimitExceeded, "Satellite mass is {0:F1} kg, exceeding rocket \"{1}\" limit of {2:F1} kg for orbit \"{3}\". Select a heavier rocket or lighter components." },
            { AstraAuditCheckType.BudgetLimitExceeded, "Total build cost is ${0:F0}k, exceeding contract budget limit of ${1:F0}k. Use lower-cost components and compensate with autopilot rules." },
            { AstraAuditCheckType.MissingRequiredSensor, "Contract \"{0}\" requires a science payload with sensor tag \"{1}\"." },
            { AstraAuditCheckType.EclipseBatteryDrainRisk, "During {0:F0} min of eclipse, satellite consumes {1:F1} Wh while battery capacity is {2:F1} Wh. Add rule: IF SolarWatts < 5 THEN ScienceOff ELSE ScienceOn, or install a larger battery." },
            { AstraAuditCheckType.SsdOverflowRisk, "Payload generates {0:F1} Gb per orbit while SSD capacity is {1:F1} Gb and downlink capacity is {2:F1} Gb. Add an SsdUsedPercent rule or upgrade antenna." },
            { AstraAuditCheckType.SolarStormSafeModeMissing, "Peak radiation exceeds 50 units and signal delay is {0:F0} s. Add high-priority rule: IF RadiationLevel > 50 THEN SafeModeOn ELSE SafeModeOff." },
            { AstraAuditCheckType.RtgSolarRuleTrap, "Satellite uses an RTG without solar panels (SolarWatts is always 0 W). Change autopilot rule metric from SolarWatts to BatteryPercent." },
            { AstraAuditCheckType.ConfigurationOptimal, "All 6 NASA resource constraints and autopilot safeguards are balanced for launch." }
        };

        private readonly ResourceCalculator _resourceCalculator;
        private readonly IGameDataRepository _repository;

        public AstraAuditor()
            : this(new ResourceCalculator(), null)
        {
        }

        public AstraAuditor(IGameDataRepository repository)
            : this(new ResourceCalculator(), repository)
        {
        }

        public AstraAuditor(ResourceCalculator resourceCalculator, IGameDataRepository repository = null)
        {
            _resourceCalculator = resourceCalculator ?? new ResourceCalculator();
            _repository = repository;
        }

        public static string GetTitle(AstraAuditCheckType checkType)
        {
            string title;
            return AuditTitles.TryGetValue(checkType, out title) ? title : string.Empty;
        }

        public static string GetAdvice(AstraAuditCheckType checkType)
        {
            string advice;
            return AuditAdvices.TryGetValue(checkType, out advice) ? advice : string.Empty;
        }

        public List<AstraAuditMessage> AuditConfiguration(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules)
        {
            List<AstraAuditMessage> messages = new List<AstraAuditMessage>();

            if (components == null || rocket == null || orbit == null || goal == null)
            {
                messages.Add(new AstraAuditMessage
                {
                    Severity = AuditSeverity.Critical,
                    Title = GetTitle(AstraAuditCheckType.IncompleteConfiguration),
                    Advice = GetAdvice(AstraAuditCheckType.IncompleteConfiguration)
                });
                return messages;
            }

            ResourceSummary summary = _resourceCalculator.CalculateResources(components, rocket, orbit, goal);

            float basePowerDraw = 0.0f;
            float activeScienceDraw = 0.0f;
            float dataGenPerTick = 0.0f;
            bool hasRequiredSensor = string.IsNullOrWhiteSpace(goal.RequiredSensorTag);

            for (int i = 0; i < components.Count; i++)
            {
                SatelliteComponent comp = components[i];
                if (comp == null)
                    continue;

                basePowerDraw += comp.BasePowerDrawWatts;

                if (comp.Category == ComponentCategory.SciencePayload)
                {
                    activeScienceDraw += comp.ActivePowerDrawWatts;
                    dataGenPerTick += comp.DataGenGbPerTick;
                }

                if (!hasRequiredSensor &&
                    !string.IsNullOrWhiteSpace(comp.SensorTag) &&
                    string.Equals(comp.SensorTag.Trim(), goal.RequiredSensorTag.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    hasRequiredSensor = true;
                }
            }

            float maxOrbitMassKg = GetMaxRocketMassForOrbit(rocket, orbit.Type);
            if (!summary.IsMassValid || summary.TotalMassKg > maxOrbitMassKg + 0.0001f)
            {
                float excessKg = Math.Max(0.0f, summary.TotalMassKg - maxOrbitMassKg);
                messages.Add(new AstraAuditMessage
                {
                    Severity = AuditSeverity.Critical,
                    Title = string.Format(CultureInfo.InvariantCulture, GetTitle(AstraAuditCheckType.MassLimitExceeded), excessKg),
                    Advice = string.Format(
                        CultureInfo.InvariantCulture,
                        GetAdvice(AstraAuditCheckType.MassLimitExceeded),
                        summary.TotalMassKg,
                        rocket.Name,
                        maxOrbitMassKg,
                        orbit.Name)
                });
            }

            if (goal.BudgetLimitThousands > 0.0f && (!summary.IsBudgetValid || summary.TotalCostThousands > goal.BudgetLimitThousands + 0.0001f))
            {
                float excessCost = Math.Max(0.0f, summary.TotalCostThousands - goal.BudgetLimitThousands);
                messages.Add(new AstraAuditMessage
                {
                    Severity = AuditSeverity.Critical,
                    Title = string.Format(CultureInfo.InvariantCulture, GetTitle(AstraAuditCheckType.BudgetLimitExceeded), excessCost),
                    Advice = string.Format(
                        CultureInfo.InvariantCulture,
                        GetAdvice(AstraAuditCheckType.BudgetLimitExceeded),
                        summary.TotalCostThousands,
                        goal.BudgetLimitThousands)
                });
            }

            if (!hasRequiredSensor)
            {
                messages.Add(new AstraAuditMessage
                {
                    Severity = AuditSeverity.Critical,
                    Title = GetTitle(AstraAuditCheckType.MissingRequiredSensor),
                    Advice = string.Format(
                        CultureInfo.InvariantCulture,
                        GetAdvice(AstraAuditCheckType.MissingRequiredSensor),
                        goal.Title,
                        goal.RequiredSensorTag)
                });
            }

            bool hasEclipsePowerSavingRule = false;
            bool hasSsdProtectionRule = false;
            bool hasRadiationSafeModeRule = false;
            bool hasSolarMetricRule = false;

            if (rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    AutopilotRule r = rules[i];
                    if (r == null || !r.Enabled)
                        continue;

                    if (r.Metric == SensorMetric.SolarWatts)
                    {
                        hasSolarMetricRule = true;
                        if (r.Operator == ComparisonOp.LessThan &&
                            (r.ThenAction == AutopilotAction.ScienceOff || r.ThenAction == AutopilotAction.SafeModeOn))
                        {
                            hasEclipsePowerSavingRule = true;
                        }
                        if (r.Operator == ComparisonOp.GreaterThan &&
                            (r.ElseAction == AutopilotAction.ScienceOff || r.ElseAction == AutopilotAction.SafeModeOn))
                        {
                            hasEclipsePowerSavingRule = true;
                        }
                    }

                    if (r.Metric == SensorMetric.BatteryPercent)
                    {
                        if (r.Operator == ComparisonOp.LessThan &&
                            (r.ThenAction == AutopilotAction.ScienceOff || r.ThenAction == AutopilotAction.SafeModeOn))
                        {
                            hasEclipsePowerSavingRule = true;
                        }
                    }

                    if (r.Metric == SensorMetric.SsdUsedPercent)
                    {
                        if (r.Operator == ComparisonOp.GreaterThan &&
                            (r.ThenAction == AutopilotAction.ScienceOff || r.ThenAction == AutopilotAction.CommsOn))
                        {
                            hasSsdProtectionRule = true;
                        }
                    }

                    if (r.Metric == SensorMetric.RadiationLevel)
                    {
                        if (r.Operator == ComparisonOp.GreaterThan && r.ThenAction == AutopilotAction.SafeModeOn)
                        {
                            hasRadiationSafeModeRule = true;
                        }
                    }
                }
            }

            float eclipseHours = orbit.OrbitPeriodHours * orbit.EclipseFraction;
            float netEclipseDrainWatts = (basePowerDraw + activeScienceDraw) - summary.RtgWatts;
            float eclipseEnergyNeededWh = Math.Max(0.0f, netEclipseDrainWatts * eclipseHours);

            if (eclipseEnergyNeededWh > summary.MaxBatteryWh * 0.85f && !hasEclipsePowerSavingRule)
            {
                messages.Add(new AstraAuditMessage
                {
                    Severity = AuditSeverity.Warning,
                    Title = GetTitle(AstraAuditCheckType.EclipseBatteryDrainRisk),
                    Advice = string.Format(
                        CultureInfo.InvariantCulture,
                        GetAdvice(AstraAuditCheckType.EclipseBatteryDrainRisk),
                        eclipseHours * 60.0f,
                        eclipseEnergyNeededWh,
                        summary.MaxBatteryWh)
                });
            }

            int nonCommTicksPerOrbit = (int)Math.Round(20.0f * (1.0f - orbit.CommWindowFraction));
            int commTicksPerOrbit = 20 - nonCommTicksPerOrbit;
            float activeScienceTicksEstimate = hasEclipsePowerSavingRule
                ? 20.0f * (1.0f - orbit.EclipseFraction)
                : 20.0f;
            float dataGeneratedPerOrbitGb = dataGenPerTick * activeScienceTicksEstimate;
            float dataAccumulatedOutsideCommGb = dataGenPerTick * Math.Min(nonCommTicksPerOrbit, activeScienceTicksEstimate);

            float eventDownlinkFactor = 1.0f;
            if (!string.IsNullOrWhiteSpace(goal.EventId) &&
                string.Equals(goal.EventId, "evt_tx_overheat", StringComparison.OrdinalIgnoreCase))
            {
                eventDownlinkFactor = 0.45f;
            }
            float minOrbitDownlinkCapacityGb = commTicksPerOrbit * summary.DownlinkGbPerTick * eventDownlinkFactor;

            if (dataGenPerTick > 0.0f &&
                (dataAccumulatedOutsideCommGb > summary.MaxSsdGb + 0.01f || dataGeneratedPerOrbitGb > minOrbitDownlinkCapacityGb + 0.01f) &&
                !hasSsdProtectionRule)
            {
                messages.Add(new AstraAuditMessage
                {
                    Severity = AuditSeverity.Warning,
                    Title = GetTitle(AstraAuditCheckType.SsdOverflowRisk),
                    Advice = string.Format(
                        CultureInfo.InvariantCulture,
                        GetAdvice(AstraAuditCheckType.SsdOverflowRisk),
                        dataGeneratedPerOrbitGb,
                        summary.MaxSsdGb,
                        minOrbitDownlinkCapacityGb)
                });
            }

            bool hasStormDanger = orbit.BaseRadiation > 50.0f ||
                (!string.IsNullOrWhiteSpace(goal.EventId) &&
                 (goal.EventId.IndexOf("storm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  goal.EventId.IndexOf("cme", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  goal.EventId.IndexOf("radiation", StringComparison.OrdinalIgnoreCase) >= 0));

            if (_repository != null && !string.IsNullOrWhiteSpace(goal.EventId))
            {
                SpaceEventDefinition evt = _repository.GetEvent(goal.EventId);
                if (evt != null && (orbit.BaseRadiation + evt.RadiationBoost) > 50.0f)
                {
                    hasStormDanger = true;
                }
            }

            if (hasStormDanger && !hasRadiationSafeModeRule)
            {
                messages.Add(new AstraAuditMessage
                {
                    Severity = AuditSeverity.Warning,
                    Title = GetTitle(AstraAuditCheckType.SolarStormSafeModeMissing),
                    Advice = string.Format(
                        CultureInfo.InvariantCulture,
                        GetAdvice(AstraAuditCheckType.SolarStormSafeModeMissing),
                        orbit.SignalDelaySeconds)
                });
            }

            if (summary.RtgWatts > 0.0f && summary.MaxSolarWatts <= 0.001f && hasSolarMetricRule)
            {
                messages.Add(new AstraAuditMessage
                {
                    Severity = AuditSeverity.Warning,
                    Title = GetTitle(AstraAuditCheckType.RtgSolarRuleTrap),
                    Advice = GetAdvice(AstraAuditCheckType.RtgSolarRuleTrap)
                });
            }

            if (messages.Count == 0)
            {
                messages.Add(new AstraAuditMessage
                {
                    Severity = AuditSeverity.Info,
                    Title = GetTitle(AstraAuditCheckType.ConfigurationOptimal),
                    Advice = GetAdvice(AstraAuditCheckType.ConfigurationOptimal)
                });
            }

            return messages;
        }

        private static float GetMaxRocketMassForOrbit(RocketOption rocket, OrbitType orbitType)
        {
            if (rocket == null)
                return 0.0f;

            switch (orbitType)
            {
                case OrbitType.LowEarth:
                    return rocket.MaxMassLeoKg;
                case OrbitType.Lunar:
                    return rocket.MaxMassLunarKg;
                case OrbitType.Mars:
                    return rocket.MaxMassMarsKg;
                case OrbitType.DeepSpace:
                default:
                    return rocket.MaxMassDeepSpaceKg;
            }
        }
    }
}
