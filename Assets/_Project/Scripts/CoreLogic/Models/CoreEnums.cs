namespace AstraLink.Core
{
    public enum ComponentCategory
    {
        CorePlatform,
        PowerGeneration,
        EnergyStorage,
        CommsAndStorage,
        SciencePayload
    }

    public enum OrbitType
    {
        LowEarth,
        Lunar,
        Mars,
        DeepSpace
    }

    public enum MissionStatus
    {
        NotStarted,
        InProgress,
        Success,
        Failed
    }

    public enum SensorMetric
    {
        SolarWatts,
        BatteryPercent,
        SsdUsedPercent,
        RadiationLevel
    }

    public enum ComparisonOp
    {
        LessThan,
        GreaterThan
    }

    public enum AutopilotAction
    {
        None,
        ScienceOn,
        ScienceOff,
        CommsOn,
        CommsOff,
        SafeModeOn,
        SafeModeOff
    }

    public enum AuditSeverity
    {
        Info,
        Warning,
        Critical
    }
}
