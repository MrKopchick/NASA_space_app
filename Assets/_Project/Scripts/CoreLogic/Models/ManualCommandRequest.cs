using System;

namespace AstraLink.Core
{
    [Serializable]
    public class ManualCommandRequest
    {
        public int IssuedAtTick;
        public int ExecuteAtTick;
        public AutopilotAction Action;
    }
}
