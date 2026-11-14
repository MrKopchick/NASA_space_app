using System;

namespace AstraLink.Core
{
    public enum SpaceEventType
    {
        Custom = 0,
        CoronalMassEjection = 1,
        TransmitterOverheat = 2,
        MicrometeoroidNoise = 3
    }

    [Serializable]
    public class SpaceEventDefinition
    {
        public string Id;
        public string Title;
        public string Description;
        public SpaceEventType Type = SpaceEventType.Custom;
        public int TriggerOrbit;
        public int DurationTicks;
        public float RadiationBoost;
        public float SolarMultiplier = 1.0f;
        public float DownlinkMultiplier = 1.0f;

        public SpaceEventType EventType
        {
            get => Type;
            set => Type = value;
        }
    }
}
