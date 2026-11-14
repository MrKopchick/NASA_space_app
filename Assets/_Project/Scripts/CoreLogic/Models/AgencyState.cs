using System;
using System.Collections.Generic;

namespace AstraLink.Core
{
    [Serializable]
    public class AgencyState
    {
        public float CapitalThousands = 50.0f;
        public int AutopilotSlotsUnlocked = 2;
        public List<string> UnlockedComponentIds = new List<string>();
        public List<string> CompletedMissionIds = new List<string>();
        public int TotalPassiveIncomeThousands;
    }
}
