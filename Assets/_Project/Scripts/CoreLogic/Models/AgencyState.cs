using System;
using System.Collections.Generic;
using System.Globalization;

namespace AstraLink.Core
{
    public enum AgencyProgressionStatus
    {
        None,
        ComponentUnlocked,
        ComponentAlreadyUnlocked,
        InsufficientCapitalForComponent,
        InvalidComponentOrState,
        AutopilotUpgraded,
        AutopilotAlreadyMaxTier,
        InsufficientCapitalForAutopilot
    }

    /// <summary>
    /// Represents the persistent financial and technology progression state of the space agency, including R&amp;D component unlocking and autopilot CPU slot upgrades.
    /// </summary>
    [Serializable]
    public class AgencyState
    {
        public const float DefaultStartingCapitalThousands = 50.0f;
        public const int InitialAutopilotSlots = 2;
        public const int Tier2AutopilotSlots = 4;
        public const int Tier3AutopilotSlots = 6;
        public const float UpgradeTo4SlotsCostThousands = 40.0f;
        public const float UpgradeTo6SlotsCostThousands = 80.0f;

        private static readonly Dictionary<AgencyProgressionStatus, string> StatusMessages = new Dictionary<AgencyProgressionStatus, string>
        {
            { AgencyProgressionStatus.None, string.Empty },
            { AgencyProgressionStatus.ComponentUnlocked, "Unlocked component \"{0}\" for ${1:F0}k. Remaining capital: ${2:F0}k." },
            { AgencyProgressionStatus.ComponentAlreadyUnlocked, "Component \"{0}\" is already unlocked." },
            { AgencyProgressionStatus.InsufficientCapitalForComponent, "Insufficient capital to unlock \"{0}\": requires ${1:F0}k, available ${2:F0}k." },
            { AgencyProgressionStatus.InvalidComponentOrState, "Component or agency state reference is null." },
            { AgencyProgressionStatus.AutopilotUpgraded, "Upgraded autopilot CPU to {0} slots for ${1:F0}k. Remaining capital: ${2:F0}k." },
            { AgencyProgressionStatus.AutopilotAlreadyMaxTier, "Autopilot CPU is already at the maximum capacity of 6 rule slots." },
            { AgencyProgressionStatus.InsufficientCapitalForAutopilot, "Insufficient capital to upgrade autopilot CPU to {0} slots: requires ${1:F0}k, available ${2:F0}k." }
        };

        public float CapitalThousands = DefaultStartingCapitalThousands;
        public int AutopilotSlotsUnlocked = InitialAutopilotSlots;
        public List<string> UnlockedComponentIds = new List<string>();
        public List<string> CompletedMissionIds = new List<string>();
        public int TotalPassiveIncomeThousands;

        public static string GetStatusMessage(AgencyProgressionStatus status)
        {
            string message;
            return StatusMessages.TryGetValue(status, out message) ? message : string.Empty;
        }

        public static bool IsComponentUnlocked(SatelliteComponent comp, AgencyState state)
        {
            if (comp == null)
                return false;

            if (comp.UnlockedByDefault || comp.UnlockCostThousands <= 0)
                return true;

            if (state == null || state.UnlockedComponentIds == null)
                return false;

            return state.UnlockedComponentIds.Contains(comp.Id);
        }

        public static bool TryUnlockComponent(SatelliteComponent comp, AgencyState state)
        {
            string statusMessage;
            return TryUnlockComponent(comp, state, out statusMessage);
        }

        public static bool TryUnlockComponent(AgencyState state, SatelliteComponent comp)
        {
            string statusMessage;
            return TryUnlockComponent(comp, state, out statusMessage);
        }

        public static bool TryUnlockComponent(SatelliteComponent comp, AgencyState state, out string statusMessage)
        {
            if (comp == null || state == null || string.IsNullOrWhiteSpace(comp.Id))
            {
                statusMessage = GetStatusMessage(AgencyProgressionStatus.InvalidComponentOrState);
                return false;
            }

            if (IsComponentUnlocked(comp, state))
            {
                statusMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    GetStatusMessage(AgencyProgressionStatus.ComponentAlreadyUnlocked),
                    comp.Name ?? comp.Id);
                return false;
            }

            float cost = Math.Max(0.0f, comp.UnlockCostThousands);
            if (state.CapitalThousands + 0.0001f < cost)
            {
                statusMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    GetStatusMessage(AgencyProgressionStatus.InsufficientCapitalForComponent),
                    comp.Name ?? comp.Id,
                    cost,
                    state.CapitalThousands);
                return false;
            }

            state.CapitalThousands -= cost;
            if (state.UnlockedComponentIds == null)
            {
                state.UnlockedComponentIds = new List<string>();
            }
            state.UnlockedComponentIds.Add(comp.Id);

            statusMessage = string.Format(
                CultureInfo.InvariantCulture,
                GetStatusMessage(AgencyProgressionStatus.ComponentUnlocked),
                comp.Name ?? comp.Id,
                cost,
                state.CapitalThousands);
            return true;
        }

        public bool TryUnlockComponent(SatelliteComponent comp)
        {
            return TryUnlockComponent(comp, this);
        }

        public static bool TryUpgradeAutopilotSlots(AgencyState state)
        {
            string statusMessage;
            return TryUpgradeAutopilotSlots(state, out statusMessage);
        }

        public static bool TryUpgradeAutopilotSlots(AgencyState state, out string statusMessage)
        {
            if (state == null)
            {
                statusMessage = GetStatusMessage(AgencyProgressionStatus.InvalidComponentOrState);
                return false;
            }

            int targetSlots;
            float upgradeCost;

            if (state.AutopilotSlotsUnlocked < Tier2AutopilotSlots)
            {
                targetSlots = Tier2AutopilotSlots;
                upgradeCost = UpgradeTo4SlotsCostThousands;
            }
            else if (state.AutopilotSlotsUnlocked < Tier3AutopilotSlots)
            {
                targetSlots = Tier3AutopilotSlots;
                upgradeCost = UpgradeTo6SlotsCostThousands;
            }
            else
            {
                statusMessage = GetStatusMessage(AgencyProgressionStatus.AutopilotAlreadyMaxTier);
                return false;
            }

            if (state.CapitalThousands + 0.0001f < upgradeCost)
            {
                statusMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    GetStatusMessage(AgencyProgressionStatus.InsufficientCapitalForAutopilot),
                    targetSlots,
                    upgradeCost,
                    state.CapitalThousands);
                return false;
            }

            state.CapitalThousands -= upgradeCost;
            state.AutopilotSlotsUnlocked = targetSlots;

            statusMessage = string.Format(
                CultureInfo.InvariantCulture,
                GetStatusMessage(AgencyProgressionStatus.AutopilotUpgraded),
                targetSlots,
                upgradeCost,
                state.CapitalThousands);
            return true;
        }

        public bool TryUpgradeAutopilotSlots()
        {
            return TryUpgradeAutopilotSlots(this);
        }
    }
}
