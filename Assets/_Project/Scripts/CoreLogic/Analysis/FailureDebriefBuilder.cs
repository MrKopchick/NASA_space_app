using System;
using System.Collections.Generic;
using System.Globalization;

namespace AstraLink.Core
{
    public enum FailureScenarioType
    {
        None,
        BatteryDepleted,
        RadiationDamage,
        InsufficientScienceWithSsdLoss,
        InsufficientScienceNoLoss
    }

    /// <summary>
    /// Generates post-mission failure debriefings (FailureTitle, FailureCause, and FailureRecommendation) from simulation results.
    /// </summary>
    public class FailureDebriefBuilder
    {
        private static readonly Dictionary<FailureScenarioType, string> DebriefTitles = new Dictionary<FailureScenarioType, string>
        {
            { FailureScenarioType.None, string.Empty },
            { FailureScenarioType.BatteryDepleted, "Critical Battery Depletion (0%)" },
            { FailureScenarioType.RadiationDamage, "Radiation Electronics Burnout (0% Health)" },
            { FailureScenarioType.InsufficientScienceWithSsdLoss, "Insufficient Delivered Science (SSD Overflow)" },
            { FailureScenarioType.InsufficientScienceNoLoss, "Insufficient Delivered Science Quota" }
        };

        private static readonly Dictionary<FailureScenarioType, string> DebriefCauses = new Dictionary<FailureScenarioType, string>
        {
            { FailureScenarioType.None, string.Empty },
            { FailureScenarioType.BatteryDepleted, "Satellite lost power on orbit {0} (tick {1}). Power draw reached {2:F1} W against {3:F1} W generation, draining battery capacity ({4:F1} Wh) in eclipse." },
            { FailureScenarioType.RadiationDamage, "Onboard computer failed on orbit {0} (tick {1}) during \"{2}\". Radiation reached {3:F0} units (shielding: {4:F0} units) while SafeMode was inactive." },
            { FailureScenarioType.InsufficientScienceWithSsdLoss, "Satellite completed {0} orbits but delivered only {1:F1} / {2:F1} science points (deficit: {3:F1} pts). SSD overflow caused {4:F1} Gb of lost data." },
            { FailureScenarioType.InsufficientScienceNoLoss, "Satellite completed {0} orbits with 0 Gb lost on SSD, but delivered only {1:F1} / {2:F1} science points (deficit: {3:F1} pts)." }
        };

        private static readonly Dictionary<FailureScenarioType, string> DebriefRecommendations = new Dictionary<FailureScenarioType, string>
        {
            { FailureScenarioType.None, string.Empty },
            { FailureScenarioType.BatteryDepleted, "Add autopilot rule: IF SolarWatts < 5 THEN ScienceOff ELSE ScienceOn (or use BatteryPercent for RTG), or install a larger battery." },
            { FailureScenarioType.RadiationDamage, "Add high-priority autopilot rule: IF RadiationLevel > 50 THEN SafeModeOn ELSE SafeModeOff to protect electronics without waiting for ground signal delay." },
            { FailureScenarioType.InsufficientScienceWithSsdLoss, "Upgrade to a higher-rate antenna with larger SSD buffer (S-Band or X-Band) or add an autopilot rule triggered by SsdUsedPercent > 80%." },
            { FailureScenarioType.InsufficientScienceNoLoss, "Verify autopilot rules to ensure science payload and transmitter remain active during sunlit and communication windows." }
        };

        public static string GetTitle(FailureScenarioType scenario)
        {
            string title;
            return DebriefTitles.TryGetValue(scenario, out title) ? title : string.Empty;
        }

        public static string GetCauseTemplate(FailureScenarioType scenario)
        {
            string cause;
            return DebriefCauses.TryGetValue(scenario, out cause) ? cause : string.Empty;
        }

        public static string GetRecommendation(FailureScenarioType scenario)
        {
            string recommendation;
            return DebriefRecommendations.TryGetValue(scenario, out recommendation) ? recommendation : string.Empty;
        }

        public void BuildDebrief(MissionReport report, float targetSciencePoints = 100.0f)
        {
            SimulationTickSnapshot lastTick = GetLastTick(report);
            MissionGoal goal = new MissionGoal
            {
                TargetSciencePoints = targetSciencePoints,
                RequiredOrbits = lastTick != null ? Math.Max(1, lastTick.OrbitNumber) : 1
            };
            PopulateFailureDebrief(report, lastTick, null, null, goal);
        }

        public void BuildDebrief(MissionReport report, MissionGoal goal, ResourceSummary resources = null, OrbitDefinition orbit = null)
        {
            SimulationTickSnapshot lastTick = GetLastTick(report);
            PopulateFailureDebrief(report, lastTick, resources, orbit, goal);
        }

        public static void PopulateFailureDebrief(
            MissionReport report,
            SimulationTickSnapshot lastTick,
            ResourceSummary resources,
            OrbitDefinition orbit,
            MissionGoal goal)
        {
            if (report == null)
                return;

            if (report.Status == MissionStatus.Success)
            {
                report.FailureTitle = GetTitle(FailureScenarioType.None);
                report.FailureCause = GetCauseTemplate(FailureScenarioType.None);
                report.FailureRecommendation = GetRecommendation(FailureScenarioType.None);
                return;
            }

            if (lastTick == null)
            {
                lastTick = GetLastTick(report);
            }

            int orbitNum = lastTick != null && lastTick.OrbitNumber > 0 ? lastTick.OrbitNumber : 1;
            int tickNum = lastTick != null ? lastTick.TickIndex : 0;
            float maxBatWh = resources != null ? resources.MaxBatteryWh : 0.0f;
            float rtgWatts = resources != null ? resources.RtgWatts : 0.0f;
            float shielding = resources != null ? resources.TotalShielding : 0.0f;

            bool isBatteryFailure = (lastTick != null && (lastTick.BatteryWh <= 0.0001f || lastTick.BatteryPercent <= 0.0001f))
                || (lastTick == null && report.MinBatteryPercent <= 0.0001f && report.FinalHealthPercent > 0.0001f);

            if (isBatteryFailure)
            {
                float drawWatts = lastTick != null ? lastTick.PowerDrawWatts : 0.0f;
                float genWatts = (lastTick != null ? lastTick.SolarGenWatts : 0.0f) + rtgWatts;

                report.FailureTitle = GetTitle(FailureScenarioType.BatteryDepleted);
                report.FailureCause = string.Format(
                    CultureInfo.InvariantCulture,
                    GetCauseTemplate(FailureScenarioType.BatteryDepleted),
                    orbitNum,
                    tickNum,
                    drawWatts,
                    genWatts,
                    maxBatWh);
                report.FailureRecommendation = GetRecommendation(FailureScenarioType.BatteryDepleted);
                return;
            }

            bool isRadiationFailure = (lastTick != null && lastTick.HealthPercent <= 0.0001f)
                || (report.FinalHealthPercent <= 0.0001f && report.Ticks != null && report.Ticks.Count > 0);

            if (isRadiationFailure)
            {
                string evtName = (lastTick != null && !string.IsNullOrWhiteSpace(lastTick.ActiveEventTitle))
                    ? lastTick.ActiveEventTitle
                    : "Solar Storm (CME)";
                float radLevel = lastTick != null ? lastTick.RadiationLevel : 100.0f;

                report.FailureTitle = GetTitle(FailureScenarioType.RadiationDamage);
                report.FailureCause = string.Format(
                    CultureInfo.InvariantCulture,
                    GetCauseTemplate(FailureScenarioType.RadiationDamage),
                    orbitNum,
                    tickNum,
                    evtName,
                    radLevel,
                    shielding);
                report.FailureRecommendation = GetRecommendation(FailureScenarioType.RadiationDamage);
                return;
            }

            float targetScience = goal != null && goal.TargetSciencePoints > 0.0f ? goal.TargetSciencePoints : 100.0f;
            float deficitScience = Math.Max(0.0f, targetScience - report.DeliveredScience);
            int totalOrbits = goal != null && goal.RequiredOrbits > 0 ? goal.RequiredOrbits : orbitNum;

            if (report.LostDataGb > 0.01f)
            {
                report.FailureTitle = GetTitle(FailureScenarioType.InsufficientScienceWithSsdLoss);
                report.FailureCause = string.Format(
                    CultureInfo.InvariantCulture,
                    GetCauseTemplate(FailureScenarioType.InsufficientScienceWithSsdLoss),
                    totalOrbits,
                    report.DeliveredScience,
                    targetScience,
                    deficitScience,
                    report.LostDataGb);
                report.FailureRecommendation = GetRecommendation(FailureScenarioType.InsufficientScienceWithSsdLoss);
            }
            else
            {
                report.FailureTitle = GetTitle(FailureScenarioType.InsufficientScienceNoLoss);
                report.FailureCause = string.Format(
                    CultureInfo.InvariantCulture,
                    GetCauseTemplate(FailureScenarioType.InsufficientScienceNoLoss),
                    totalOrbits,
                    report.DeliveredScience,
                    targetScience,
                    deficitScience);
                report.FailureRecommendation = GetRecommendation(FailureScenarioType.InsufficientScienceNoLoss);
            }
        }

        private static SimulationTickSnapshot GetLastTick(MissionReport report)
        {
            if (report == null || report.Ticks == null || report.Ticks.Count == 0)
                return null;

            return report.Ticks[report.Ticks.Count - 1];
        }
    }
}
