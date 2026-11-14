using System;
using System.Collections.Generic;

namespace AstraLink.Core
{
    [Serializable]
    public class MissionReport
    {
        public MissionStatus Status;
        public float FinalScore;
        public float DeliveredScience;
        public float LostDataGb;
        public float BudgetSpentThousands;
        public float MinBatteryPercent;
        public float FinalHealthPercent;
        public List<SimulationTickSnapshot> Ticks = new List<SimulationTickSnapshot>();
        public string FailureTitle;
        public string FailureCause;
        public string FailureRecommendation;
    }
}
