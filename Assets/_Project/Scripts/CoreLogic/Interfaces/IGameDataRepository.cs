using System.Collections.Generic;

namespace AstraLink.Core
{
    public interface IGameDataRepository
    {
        List<SatelliteComponent> GetComponents();
        List<RocketOption> GetRockets();
        List<OrbitDefinition> GetOrbits();
        OrbitDefinition GetOrbit(string orbitId);
        List<MissionGoal> GetMissions();
        List<SpaceEventDefinition> GetEvents();
        SpaceEventDefinition GetEvent(string eventId);
    }
}
