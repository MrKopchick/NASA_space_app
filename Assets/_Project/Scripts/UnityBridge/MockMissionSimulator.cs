using System.Collections.Generic;
using AstraLink.Core;

namespace AstraLink.Core
{
    /// <summary>
    /// Заглушка симулятора місії (Задача S1-02) для швидкої верстки UI.
    /// Повертає стабільні захардкоджені дані ресурсів та звіт із 20 тіків.
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
            return new ResourceSummary
            {
                TotalMassKg = 30.0f,
                TotalCostThousands = 90.0f,
                MaxSolarWatts = 45.0f,
                RtgWatts = 0.0f,
                MaxBatteryWh = 45.0f,
                MaxSsdGb = 16.0f,
                DownlinkGbPerTick = 2.0f,
                OverallReliability = 0.92f,
                TotalShielding = 15.0f,
                IsBudgetValid = true,
                IsMassValid = true
            };
        }

        public bool ValidateLaunch(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            out string errorMessage)
        {
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
            return BuildMockMissionReport(rules);
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
            return BuildMockMissionReport(rules);
        }

        private static MissionReport BuildMockMissionReport(List<AutopilotRule> rules)
        {
            const float maxBatteryWh = 45.0f;
            const float maxSsdGb = 16.0f;

            // Плавна крива заряду батареї на 20 кроків:
            // старт 100% на світлі -> плавне падіння до 60% у тіні (кроки 6..13) -> відновлення на світлі (кроки 14..19)
            float[] batteryPercentCurve = new float[TicksPerOrbit]
            {
                100f, 100f, 100f, 100f, 100f, 100f,
                95f,  90f,  85f,  80f,  75f,  70f,  65f,  60f,
                67f,  74f,  81f,  88f,  94f,  100f
            };

            var report = new MissionReport
            {
                Status = MissionStatus.Success,
                FinalScore = 162.5f,
                DeliveredScience = 120.0f,
                LostDataGb = 0.0f,
                BudgetSpentThousands = 90.0f,
                MinBatteryPercent = 60.0f,
                FinalHealthPercent = 100.0f,
                FailureTitle = string.Empty,
                FailureCause = string.Empty,
                FailureRecommendation = string.Empty,
                Ticks = new List<SimulationTickSnapshot>(TicksPerOrbit)
            };

            string firstRuleId = (rules != null && rules.Count > 0 && !string.IsNullOrEmpty(rules[0].RuleId))
                ? rules[0].RuleId
                : "rule_eclipse_power_save";

            for (int tick = 0; tick < TicksPerOrbit; tick++)
            {
                float progress = tick / (float)TicksPerOrbit;
                bool isInEclipse = tick >= 6 && tick <= 13;
                bool isInCommWindow = tick < 5;

                float batteryPercent = batteryPercentCurve[tick];
                float batteryWh = (batteryPercent / 100.0f) * maxBatteryWh;

                // SsdUsedPercent поступово заповнюється з 5% до 95%
                float ssdUsedPercent = 5.0f + (tick * (90.0f / (TicksPerOrbit - 1)));
                float ssdUsedGb = (ssdUsedPercent / 100.0f) * maxSsdGb;

                var triggeredRules = new List<string>();
                if (isInEclipse)
                {
                    triggeredRules.Add(firstRuleId);
                }

                report.Ticks.Add(new SimulationTickSnapshot
                {
                    TickIndex = tick,
                    OrbitNumber = 1,
                    OrbitProgress = progress,
                    IsInEclipse = isInEclipse,
                    IsInCommWindow = isInCommWindow,
                    SafeModeActive = false,
                    ScienceActive = !isInEclipse,
                    CommsActive = isInCommWindow,
                    SolarGenWatts = isInEclipse ? 0.0f : 45.0f,
                    PowerDrawWatts = isInEclipse ? 12.0f : 26.0f,
                    BatteryWh = batteryWh,
                    BatteryPercent = batteryPercent,
                    SsdUsedGb = ssdUsedGb,
                    SsdUsedPercent = ssdUsedPercent,
                    RadiationLevel = 5.0f,
                    HealthPercent = 100.0f,
                    DeliveredScience = (tick + 1) * 6.0f,
                    ActiveEventTitle = string.Empty,
                    TriggeredRuleIds = triggeredRules
                });
            }

            return report;
        }
    }
}
