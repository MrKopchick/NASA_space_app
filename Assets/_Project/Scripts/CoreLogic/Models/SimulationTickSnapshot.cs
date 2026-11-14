using System;
using System.Collections.Generic;

namespace AstraLink.Core
{
    [Serializable]
    public class SimulationTickSnapshot
    {
        public int TickIndex;
        public int OrbitNumber;
        public float OrbitProgress;
        public bool IsInEclipse;
        public bool IsInCommWindow;
        public bool SafeModeActive;
        public bool ScienceActive;
        public bool CommsActive;
        public float SolarGenWatts;
        public float PowerDrawWatts;
        public float BatteryWh;
        public float BatteryPercent;
        public float SsdUsedGb;
        public float SsdUsedPercent;
        public float RadiationLevel;
        public float HealthPercent;
        public float DeliveredScience;
        public string ActiveEventTitle;
        public List<string> TriggeredRuleIds = new List<string>();
    }
}
