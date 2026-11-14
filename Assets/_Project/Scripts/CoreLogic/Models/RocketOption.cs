using System;

namespace AstraLink.Core
{
    [Serializable]
    public class RocketOption
    {
        public string Id;
        public string Name;
        public float CostThousands;
        public float MaxMassLeoKg;
        public float MaxMassLunarKg;
        public float MaxMassMarsKg;
        public float MaxMassDeepSpaceKg;
        public float BaseReliability;
    }
}
