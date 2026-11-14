using System.Collections.Generic;

namespace AstraLink.Core
{
    public interface IMissionSimulator
    {
        ResourceSummary CalculateResources(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal);

        bool ValidateLaunch(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            out string errorMessage);

        MissionReport RunDryRun(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules);

        MissionReport SimulateFullMission(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules,
            List<ManualCommandRequest> manualCommands = null,
            List<SpaceEventDefinition> overrideEvents = null);
    }
}
