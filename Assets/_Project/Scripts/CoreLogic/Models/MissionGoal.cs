using System;

namespace AstraLink.Core
{
    [Serializable]
    public class MissionGoal
    {
        public string Id;
        public string Title;
        public string Brief;
        public string OrbitId;
        public float BudgetLimitThousands;
        public float TargetSciencePoints;
        public int RequiredOrbits;
        public int RewardThousands;
        public int PassiveIncomeThousands;
        public int MinChipSlots;
        public string RequiredSensorTag;
        public string EventId;
    }
}
