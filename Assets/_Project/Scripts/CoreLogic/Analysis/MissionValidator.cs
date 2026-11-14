using System;
using System.Collections.Generic;
using System.Globalization;

namespace AstraLink.Core
{
    /// <summary>
    /// S1-05 (Саша Г): Валідатор передстартової готовності місії (MissionValidator).
    /// Перевіряє наявність 5 обов'язкових категорій модулів, ліміт маси ракети для обраної орбіти,
    /// ліміт бюджету контракту та наявність обов'язкового наукового сенсора (RequiredSensorTag).
    /// </summary>
    public class MissionValidator
    {
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
                errorMessage = "Не обрано ракету-носій для запуску.";
                return false;
            }

            if (orbit == null)
            {
                errorMessage = "Не обрано цільову орбіту місії.";
                return false;
            }

            if (goal == null)
            {
                errorMessage = "Не обрано контракт місії.";
                return false;
            }

            if (components == null || components.Count == 0)
            {
                errorMessage = "Конструкція супутника порожня. Встановіть усі 5 обов'язкових модулів.";
                return false;
            }

            // 1. Перевірка 5 обов'язкових слотів супутника
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
                errorMessage = "Не встановлено базову платформу (корпус супутника).";
                return false;
            }
            if (!hasPowerGen)
            {
                errorMessage = "Не встановлено джерело енергії (сонячні панелі або РІТЕГ).";
                return false;
            }
            if (!hasStorage)
            {
                errorMessage = "Не встановлено акумуляторну батарею.";
                return false;
            }
            if (!hasComms)
            {
                errorMessage = "Не встановлено модуль зв'язку та бортовий SSD-накопичувач.";
                return false;
            }
            if (!hasScience)
            {
                errorMessage = "Не встановлено жодного наукового приладу.";
                return false;
            }

            // 2. Перевірка ліміту маси ракети для обраної орбіти
            float maxMassKg = GetMaxRocketMassForOrbit(rocket, orbit.Type);
            if (totalMassKg > maxMassKg + 0.0001f)
            {
                float excessKg = totalMassKg - maxMassKg;
                errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Перевищено ліміт маси ракети «{0}» для орбіти «{1}»: маса супутника {2:F1} кг, максимум ракети {3:F1} кг (перевантаження +{4:F1} кг). Оберіть важчу ракету або легші деталі.",
                    rocket.Name,
                    orbit.Name,
                    totalMassKg,
                    maxMassKg,
                    excessKg);
                return false;
            }

            // 3. Перевірка бюджету контракту (у Пісочниці BudgetLimitThousands може бути <= 0 або дуже великим)
            float totalCostThousands = componentsCostThousands + rocket.CostThousands;
            if (goal.BudgetLimitThousands > 0.0f && totalCostThousands > goal.BudgetLimitThousands + 0.0001f)
            {
                float excessCost = totalCostThousands - goal.BudgetLimitThousands;
                errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Перевищено бюджет контракту «{0}»: вартість збірки з ракетою ${1:F0}k при ліміті ${2:F0}k (перевитрата +${3:F0}k). Здешевіть комплектацію або ракету.",
                    goal.Title,
                    totalCostThousands,
                    goal.BudgetLimitThousands,
                    excessCost);
                return false;
            }

            // 4. Перевірка обов'язкового наукового сенсора контракту
            if (!hasRequiredSensor)
            {
                errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Контракт «{0}» вимагає встановлення приладу типу «{1}» ({2}). Замініть науковий сенсор у конструкторі.",
                    goal.Title,
                    goal.RequiredSensorTag,
                    GetSensorUkrainianName(goal.RequiredSensorTag));
                return false;
            }

            errorMessage = string.Empty;
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

        public static string GetSensorUkrainianName(string sensorTag)
        {
            if (string.IsNullOrWhiteSpace(sensorTag))
                return "будь-який сенсор";

            switch (sensorTag.Trim().ToLowerInvariant())
            {
                case "camera":
                    return "Метео-камера HD";
                case "radar":
                    return "Радар синтезованої апертури SAR";
                case "spectrometer":
                    return "Міжпланетний спектрометр";
                case "magnetometer":
                    return "Магнітометр плазми";
                default:
                    return sensorTag;
            }
        }
    }
}
