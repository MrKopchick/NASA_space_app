using System;

namespace AstraLink.Core
{
    [Serializable]
    public class AutopilotRule
    {
        public string RuleId;
        public bool Enabled = true;
        public SensorMetric Metric;
        public ComparisonOp Operator;
        public float Threshold;
        public AutopilotAction ThenAction;
        public AutopilotAction ElseAction;
    }
}
