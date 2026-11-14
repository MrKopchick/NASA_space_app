using System.Collections.Generic;

namespace AstraLink.Core
{
    public interface IAstraAuditor
    {
        List<AstraAuditMessage> AuditConfiguration(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            List<AutopilotRule> rules);
    }
}
