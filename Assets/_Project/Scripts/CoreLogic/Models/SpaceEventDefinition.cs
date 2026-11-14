using System;

namespace AstraLink.Core
{
    public enum SpaceEventType
    {

        Custom,
        CoronalMassEjection,
        TransmitterOverheat,
        MicrometeoroidNoise
    }

    /// <summary>
    /// Defines an orbital space weather event or anomaly triggered at a specific orbit and duration.
    /// </summary>

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
