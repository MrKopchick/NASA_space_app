using System;
using System.Collections.Generic;
using System.Globalization;

namespace AstraLink.Core
{
    public enum AutopilotLogType
    {
        None,
        CommandQueued,
        CommandExecuted,
        RuleTriggered,
        SafeModeOverride,
        InvalidCommand,
        InvalidOrbit
    }

    /// <summary>
    /// Evaluates IF-THEN-ELSE autopilot rules top-down on each simulation tick, enforces SafeMode priority, and manages the signal-delayed manual command queue.
    /// </summary>
    public class AutopilotEvaluator
    {
        private static readonly Dictionary<AutopilotLogType, string> LogMessages = new Dictionary<AutopilotLogType, string>
        {
            { AutopilotLogType.None, string.Empty },
            { AutopilotLogType.CommandQueued, "Manual command \"{0}\" queued at tick {1} for execution at tick {2}." },
            { AutopilotLogType.CommandExecuted, "Manual command \"{0}\" executed at tick {1}." },
            { AutopilotLogType.RuleTriggered, "Autopilot rule \"{0}\" triggered action \"{1}\"." },
            { AutopilotLogType.SafeModeOverride, "SafeMode active: science and communication subsystems forcibly disabled." },
            { AutopilotLogType.InvalidCommand, "Manual command action cannot be None." },
            { AutopilotLogType.InvalidOrbit, "Target orbit definition is required to calculate signal delay." }
        };

        private readonly List<ManualCommandRequest> _commandQueue = new List<ManualCommandRequest>();

        public static string GetLogMessage(AutopilotLogType logType)
        {
            string message;
            return LogMessages.TryGetValue(logType, out message) ? message : string.Empty;
        }

        public static int CalculateDelayTicks(float signalDelaySeconds)
        {
            if (signalDelaySeconds <= 0.0f)
                return 0;

            return (int)Math.Ceiling(signalDelaySeconds);
        }

        public static int CalculateDelayTicks(OrbitDefinition orbit)
        {
            if (orbit == null)
                return 0;

            return CalculateDelayTicks(orbit.SignalDelaySeconds);
        }

        public static ManualCommandRequest CreateManualCommand(
            int issuedAtTick,
            AutopilotAction action,
            float signalDelaySeconds)
        {
            int delayTicks = CalculateDelayTicks(signalDelaySeconds);
            return new ManualCommandRequest
            {
                IssuedAtTick = issuedAtTick,
                ExecuteAtTick = issuedAtTick + delayTicks,
                Action = action
            };
        }

        public static ManualCommandRequest CreateManualCommand(
            int issuedAtTick,
            AutopilotAction action,
            OrbitDefinition orbit)
        {
            float delaySeconds = orbit != null ? orbit.SignalDelaySeconds : 0.0f;
            return CreateManualCommand(issuedAtTick, action, delaySeconds);
        }

        public ManualCommandRequest EnqueueCommand(
            int issuedAtTick,
            AutopilotAction action,
            OrbitDefinition orbit,
            out string statusMessage)
        {
            if (orbit == null)
            {
                statusMessage = GetLogMessage(AutopilotLogType.InvalidOrbit);
                return null;
            }

            return EnqueueCommand(issuedAtTick, action, orbit.SignalDelaySeconds, out statusMessage);
        }

        public ManualCommandRequest EnqueueCommand(
            int issuedAtTick,
            AutopilotAction action,
            float signalDelaySeconds,
            out string statusMessage)
        {
            if (action == AutopilotAction.None)
            {
                statusMessage = GetLogMessage(AutopilotLogType.InvalidCommand);
                return null;
            }

            ManualCommandRequest request = CreateManualCommand(issuedAtTick, action, signalDelaySeconds);
            _commandQueue.Add(request);
            statusMessage = string.Format(
                CultureInfo.InvariantCulture,
                GetLogMessage(AutopilotLogType.CommandQueued),
                request.Action,
                request.IssuedAtTick,
                request.ExecuteAtTick);
            return request;
        }

        public List<ManualCommandRequest> GetCommandQueue()
        {
            return new List<ManualCommandRequest>(_commandQueue);
        }

        public void ClearCommandQueue()
        {
            _commandQueue.Clear();
        }

        public void ApplyQueuedCommandsForTick(
            int tickIndex,
            ref bool scienceActive,
            ref bool commsActive,
            ref bool safeModeActive)
        {
            ApplyManualCommandsForTick(
                tickIndex,
                _commandQueue,
                ref scienceActive,
                ref commsActive,
                ref safeModeActive);
        }

        public void ApplyManualCommandsForTick(
            int tickIndex,
            List<ManualCommandRequest> manualCommands,
            ref bool scienceActive,
            ref bool commsActive,
            ref bool safeModeActive)
        {
            if (manualCommands == null || manualCommands.Count == 0)
                return;

            for (int i = 0; i < manualCommands.Count; i++)
            {
                ManualCommandRequest cmd = manualCommands[i];
                if (cmd == null)
                    continue;

                if (cmd.ExecuteAtTick == tickIndex)
                {
                    ApplyAction(cmd.Action, ref scienceActive, ref commsActive, ref safeModeActive);
                }
            }

            if (safeModeActive)
            {
                scienceActive = false;
                commsActive = false;
            }
        }

        public void EvaluateRulesForTick(
            List<AutopilotRule> rules,
            float solarGenWatts,
            float batteryPercent,
            float ssdUsedPercent,
            float radiationLevel,
            ref bool scienceActive,
            ref bool commsActive,
            ref bool safeModeActive,
            List<string> triggeredRuleIds)
        {
            if (triggeredRuleIds != null)
            {
                triggeredRuleIds.Clear();
            }

            bool safeModeActivatedThisTick = false;
            bool scienceModifiedThisTick = false;
            bool commsModifiedThisTick = false;

            if (rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    AutopilotRule rule = rules[i];
                    if (rule == null || !rule.Enabled)
                        continue;

                    float currentValue = GetMetricValue(
                        rule.Metric,
                        solarGenWatts,
                        batteryPercent,
                        ssdUsedPercent,
                        radiationLevel);

                    bool conditionMet = CheckCondition(currentValue, rule.Operator, rule.Threshold);

                    if (conditionMet)
                    {
                        if (rule.ThenAction != AutopilotAction.None)
                        {
                            ApplyRuleAction(
                                rule.ThenAction,
                                ref scienceActive,
                                ref commsActive,
                                ref safeModeActive,
                                ref safeModeActivatedThisTick,
                                ref scienceModifiedThisTick,
                                ref commsModifiedThisTick);
                        }

                        if (triggeredRuleIds != null)
                        {
                            string idToRecord = !string.IsNullOrWhiteSpace(rule.RuleId)
                                ? rule.RuleId
                                : "rule_" + (i + 1).ToString(CultureInfo.InvariantCulture);
                            if (!triggeredRuleIds.Contains(idToRecord))
                            {
                                triggeredRuleIds.Add(idToRecord);
                            }
                        }
                    }
                    else
                    {
                        if (rule.ElseAction != AutopilotAction.None)
                        {
                            ApplyRuleAction(
                                rule.ElseAction,
                                ref scienceActive,
                                ref commsActive,
                                ref safeModeActive,
                                ref safeModeActivatedThisTick,
                                ref scienceModifiedThisTick,
                                ref commsModifiedThisTick);
                        }
                    }
                }
            }

            if (safeModeActive)
            {
                scienceActive = false;
                commsActive = false;
            }
        }

        private static void ApplyRuleAction(
            AutopilotAction action,
            ref bool scienceActive,
            ref bool commsActive,
            ref bool safeModeActive,
            ref bool safeModeActivatedThisTick,
            ref bool scienceModifiedThisTick,
            ref bool commsModifiedThisTick)
        {
            switch (action)
            {
                case AutopilotAction.ScienceOn:
                    scienceActive = true;
                    scienceModifiedThisTick = true;
                    break;
                case AutopilotAction.ScienceOff:
                    scienceActive = false;
                    scienceModifiedThisTick = true;
                    break;
                case AutopilotAction.CommsOn:
                    commsActive = true;
                    commsModifiedThisTick = true;
                    break;
                case AutopilotAction.CommsOff:
                    commsActive = false;
                    commsModifiedThisTick = true;
                    break;
                case AutopilotAction.SafeModeOn:
                    safeModeActive = true;
                    safeModeActivatedThisTick = true;
                    scienceActive = false;
                    commsActive = false;
                    break;
                case AutopilotAction.SafeModeOff:
                    if (!safeModeActivatedThisTick && safeModeActive)
                    {
                        safeModeActive = false;
                        if (!scienceModifiedThisTick)
                        {
                            scienceActive = true;
                        }
                        if (!commsModifiedThisTick)
                        {
                            commsActive = true;
                        }
                    }
                    break;
                case AutopilotAction.None:
                default:
                    break;
            }
        }

        public static void ResolveEffectiveSubsystems(
            bool scienceActive,
            bool commsActive,
            bool safeModeActive,
            out bool effectiveScience,
            out bool effectiveComms)
        {
            effectiveScience = scienceActive && !safeModeActive;
            effectiveComms = commsActive && !safeModeActive;
        }

        public static float GetMetricValue(
            SensorMetric metric,
            float solarGenWatts,
            float batteryPercent,
            float ssdUsedPercent,
            float radiationLevel)
        {
            switch (metric)
            {
                case SensorMetric.SolarWatts:
                    return solarGenWatts;
                case SensorMetric.BatteryPercent:
                    return batteryPercent;
                case SensorMetric.SsdUsedPercent:
                    return ssdUsedPercent;
                case SensorMetric.RadiationLevel:
                    return radiationLevel;
                default:
                    return 0.0f;
            }
        }

        public static bool CheckCondition(float metricValue, ComparisonOp op, float threshold)
        {
            switch (op)
            {
                case ComparisonOp.LessThan:
                    return metricValue < threshold;
                case ComparisonOp.GreaterThan:
                    return metricValue > threshold;
                default:
                    return false;
            }
        }

        public static void ApplyAction(
            AutopilotAction action,
            ref bool scienceActive,
            ref bool commsActive,
            ref bool safeModeActive)
        {
            switch (action)
            {
                case AutopilotAction.ScienceOn:
                    scienceActive = true;
                    break;
                case AutopilotAction.ScienceOff:
                    scienceActive = false;
                    break;
                case AutopilotAction.CommsOn:
                    commsActive = true;
                    break;
                case AutopilotAction.CommsOff:
                    commsActive = false;
                    break;
                case AutopilotAction.SafeModeOn:
                    safeModeActive = true;
                    scienceActive = false;
                    commsActive = false;
                    break;
                case AutopilotAction.SafeModeOff:
                    if (safeModeActive)
                    {
                        safeModeActive = false;
                        scienceActive = true;
                        commsActive = true;
                    }
                    break;
                case AutopilotAction.None:
                default:
                    break;
            }
        }
    }
}
