using System;
using System.Collections.Generic;
using System.Globalization;

namespace AstraLink.Core
{
    public enum LaunchValidationError
    {
        None,
        MissingRocket,
        MissingOrbit,
        MissingGoal,
        EmptyComponents,
        MissingCorePlatform,
        MissingPowerGeneration,
        MissingEnergyStorage,
        MissingCommsAndStorage,
        MissingSciencePayload,
        MassLimitExceeded,
        BudgetLimitExceeded,
        MissingRequiredSensor
    }

    /// <summary>
    /// Validates satellite readiness before launch, checking mandatory component slots, rocket mass limits, mission budget, and required sensor tags.
    /// </summary>
    public class MissionValidator
    {
        private static readonly Dictionary<LaunchValidationError, string> ErrorMessages = new Dictionary<LaunchValidationError, string>
        {
            { LaunchValidationError.None, string.Empty },
            { LaunchValidationError.MissingRocket, "No launch vehicle selected." },
            { LaunchValidationError.MissingOrbit, "No target orbit selected." },
            { LaunchValidationError.MissingGoal, "No mission contract selected." },
            { LaunchValidationError.EmptyComponents, "Satellite build is empty. Install all 5 required modules." },
            { LaunchValidationError.MissingCorePlatform, "Missing core platform (satellite bus)." },
            { LaunchValidationError.MissingPowerGeneration, "Missing power generation module (solar panels or RTG)." },
            { LaunchValidationError.MissingEnergyStorage, "Missing energy storage module (battery)." },
            { LaunchValidationError.MissingCommsAndStorage, "Missing communication and onboard SSD module." },
            { LaunchValidationError.MissingSciencePayload, "Missing science payload module." },
            { LaunchValidationError.MassLimitExceeded, "Rocket \"{0}\" mass limit exceeded for orbit \"{1}\": {2:F1} kg / {3:F1} kg max." },
            { LaunchValidationError.BudgetLimitExceeded, "Mission \"{0}\" budget limit exceeded: ${1:F0}k / ${2:F0}k max." },
            { LaunchValidationError.MissingRequiredSensor, "Mission \"{0}\" requires sensor tag \"{1}\"." }
        };

        public static string GetErrorMessage(LaunchValidationError error)
        {
            string message;
            return ErrorMessages.TryGetValue(error, out message) ? message : string.Empty;
        }

        public bool ValidateLaunch(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            out string errorMessage)
        {
            return Validate(components, rocket, orbit, goal, out errorMessage);
        }

        public static bool Validate(
            List<SatelliteComponent> components,
            RocketOption rocket,
            OrbitDefinition orbit,
            MissionGoal goal,
            out string errorMessage)
        {
            if (rocket == null)
            {
                errorMessage = GetErrorMessage(LaunchValidationError.MissingRocket);
                return false;
            }

            if (orbit == null)
            {
                errorMessage = GetErrorMessage(LaunchValidationError.MissingOrbit);
                return false;
            }

            if (goal == null)
            {
                errorMessage = GetErrorMessage(LaunchValidationError.MissingGoal);
                return false;
            }

            if (components == null || components.Count == 0)
            {
                errorMessage = GetErrorMessage(LaunchValidationError.EmptyComponents);
                return false;
            }

            bool hasPlatform = false;
            bool hasPowerGen = false;
            bool hasStorage = false;
            bool hasComms = false;
            bool hasScience = false;

            float totalMassKg = 0.0f;
            float componentsCostThousands = 0.0f;
            bool hasRequiredSensor = string.IsNullOrWhiteSpace(goal.RequiredSensorTag);

            for (int i = 0; i < components.Count; i++)
            {
                SatelliteComponent comp = components[i];
                if (comp == null)
                    continue;

                totalMassKg += comp.MassKg;
                componentsCostThousands += comp.CostThousands;

                switch (comp.Category)
                {
                    case ComponentCategory.CorePlatform:
                        hasPlatform = true;
                        break;
                    case ComponentCategory.PowerGeneration:
                        hasPowerGen = true;
                        break;
                    case ComponentCategory.EnergyStorage:
                        hasStorage = true;
                        break;
                    case ComponentCategory.CommsAndStorage:
                        hasComms = true;
                        break;
                    case ComponentCategory.SciencePayload:
                        hasScience = true;
                        break;
                }

                if (!hasRequiredSensor &&
                    !string.IsNullOrWhiteSpace(comp.SensorTag) &&
                    string.Equals(comp.SensorTag.Trim(), goal.RequiredSensorTag.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    hasRequiredSensor = true;
                }
            }

            if (!hasPlatform)
            {
                errorMessage = GetErrorMessage(LaunchValidationError.MissingCorePlatform);
                return false;
            }
            if (!hasPowerGen)
            {
                errorMessage = GetErrorMessage(LaunchValidationError.MissingPowerGeneration);
                return false;
            }
            if (!hasStorage)
            {
                errorMessage = GetErrorMessage(LaunchValidationError.MissingEnergyStorage);
                return false;
            }
            if (!hasComms)
            {
                errorMessage = GetErrorMessage(LaunchValidationError.MissingCommsAndStorage);
                return false;
            }
            if (!hasScience)
            {
                errorMessage = GetErrorMessage(LaunchValidationError.MissingSciencePayload);
                return false;
            }

            float maxMassKg = GetMaxRocketMassForOrbit(rocket, orbit.Type);
            if (totalMassKg > maxMassKg + 0.0001f)
            {
                errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    GetErrorMessage(LaunchValidationError.MassLimitExceeded),
                    rocket.Name,
                    orbit.Name,
                    totalMassKg,
                    maxMassKg);
                return false;
            }

            float totalCostThousands = componentsCostThousands + rocket.CostThousands;
            if (goal.BudgetLimitThousands > 0.0f && totalCostThousands > goal.BudgetLimitThousands + 0.0001f)
            {
                errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    GetErrorMessage(LaunchValidationError.BudgetLimitExceeded),
                    goal.Title,
                    totalCostThousands,
                    goal.BudgetLimitThousands);
                return false;
            }

            if (!hasRequiredSensor)
            {
                errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    GetErrorMessage(LaunchValidationError.MissingRequiredSensor),
                    goal.Title,
                    goal.RequiredSensorTag);
                return false;
            }

            errorMessage = GetErrorMessage(LaunchValidationError.None);
            return true;
        }

        public static float GetMaxRocketMassForOrbit(RocketOption rocket, OrbitType orbitType)
        {
            if (rocket == null)
                return 0.0f;

            switch (orbitType)
            {
                case OrbitType.LowEarth:
                    return rocket.MaxMassLeoKg;
                case OrbitType.Lunar:
                    return rocket.MaxMassLunarKg;
                case OrbitType.Mars:
                    return rocket.MaxMassMarsKg;
                case OrbitType.DeepSpace:
                default:
                    return rocket.MaxMassDeepSpaceKg;
            }
        }
    }
}
