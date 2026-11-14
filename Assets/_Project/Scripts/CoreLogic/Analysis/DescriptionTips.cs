using System;
using System.Collections.Generic;
using System.Globalization;

namespace AstraLink.Core
{
    /// <summary>
    /// Enumeration of dynamic tooltip format templates used for spacecraft components and launch vehicles.
    /// </summary>
    public enum DescriptionTipTemplate
    {
        None = 0,
        ComponentSummary,
        RocketSummary,
        DefaultComponentName,
        DefaultComponentDescription,
        DefaultRocketName
    }

    /// <summary>
    /// Provides single-sentence English descriptions for UI buttons, resource gauges, hangar slots, and dynamic hardware tooltips (Task S3-08).
    /// </summary>
    public static class DescriptionTips
    {
        private static bool _isEnabled = true;

        private static readonly Dictionary<DescriptionTipTemplate, string> Templates = new Dictionary<DescriptionTipTemplate, string>
        {
            { DescriptionTipTemplate.None, string.Empty },
            { DescriptionTipTemplate.ComponentSummary, "{0}: {1} (Mass: {2:F1} kg | Cost: ${3:F0}k | Reliability: {4:F0}%)" },
            { DescriptionTipTemplate.RocketSummary, "{0} — Max orbit payload: {1:F1} kg | Cost: ${2:F0}k | Reliability: {3:F0}%" },
            { DescriptionTipTemplate.DefaultComponentName, "Spacecraft Module" },
            { DescriptionTipTemplate.DefaultComponentDescription, "Standard orbital hardware subsystem." },
            { DescriptionTipTemplate.DefaultRocketName, "Launch Vehicle" }
        };

        private static readonly Dictionary<string, string> ComponentEnglishNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "bus_cubesat_3u", "CubeSat 3U Platform" },
            { "bus_smallsat", "Orion-S SmallSat Platform" },
            { "bus_deepspace", "Voyager-M DeepSpace Chassis" },
            { "pwr_solar_mini", "Basic 3U Solar Panels" },
            { "pwr_solar_deployable", "S-Class Deployable Solar Array" },
            { "pwr_solar_wings", "Ares Quad Solar Wings" },
            { "pwr_rtg", "Radioisotope Generator (RTG)" },
            { "bat_li_small", "Li-Ion 10 Wh Battery Pack" },
            { "bat_li_medium", "Li-Ion 45 Wh Orbital Battery" },
            { "bat_solid_heavy", "Solid-State 110 Wh Heavy Battery" },
            { "comms_uhf_basic", "UHF Antenna + 4 Gb SSD" },
            { "comms_sband", "S-Band Antenna + 16 Gb SSD" },
            { "comms_xband", "X-Band Dish + 30 Gb SSD" },
            { "comms_laser", "Optical Laser Terminal + 50 Gb SSD" },
            { "sci_camera_basic", "HD Weather Camera" },
            { "sci_radar_sar", "Synthetic Aperture Radar (SAR)" },
            { "sci_spectrometer", "Interplanetary Spectrometer" },
            { "sci_magnetometer", "Plasma Magnetometer" }
        };

        private static readonly Dictionary<string, string> ComponentEnglishDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "bus_cubesat_3u", "Lightweight modular frame designed for Low Earth Orbit cube missions." },
            { "bus_smallsat", "Reinforced spacecraft bus with baseline radiation shielding for Lunar operations." },
            { "bus_deepspace", "Heavy radiation-hardened chassis engineered for Mars and deep-space survival." },
            { "pwr_solar_mini", "Compact body-mounted photovoltaic cells suited for Low Earth Orbit." },
            { "pwr_solar_deployable", "Expanded solar panel array capable of powering high-draw Lunar radar payloads." },
            { "pwr_solar_wings", "High-area solar wings that compensate for reduced 43% solar flux near Mars." },
            { "pwr_rtg", "Continuous 65 W nuclear power source operating independently of sunlight or eclipse." },
            { "bat_li_small", "Lightweight starter battery that requires eclipse power-saving rules to avoid depletion." },
            { "bat_li_medium", "Balanced energy reserve built to sustain avionics through extended orbital eclipses." },
            { "bat_solid_heavy", "High-capacity solid-state energy vault for power-intensive payloads and long shadows." },
            { "comms_uhf_basic", "Entry-level low-bandwidth radio transceiver with a 4 Gb onboard SSD buffer." },
            { "comms_sband", "Medium-rate telemetry transceiver with a 16 Gb SSD buffer for orbital mapping." },
            { "comms_xband", "High-gain directional dish with a 30 Gb SSD buffer for deep-space downlink windows." },
            { "comms_laser", "Ultra-fast optical downlink terminal with a 50 Gb SSD buffer and high power draw." },
            { "sci_camera_basic", "Baseline optical weather sensor for monitoring planetary cloud cover and atmosphere." },
            { "sci_radar_sar", "High-power synthetic aperture radar that generates high-volume surface mapping data." },
            { "sci_spectrometer", "Precision atmospheric and mineral analyzer required for interplanetary Mars missions." },
            { "sci_magnetometer", "Reliable low-power magnetosphere sensor with steady science output." }
        };

        private static readonly Dictionary<string, string> RocketEnglishNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "rocket_light", "Arrow-1 Light Booster" },
            { "rocket_medium", "Falcon-M Medium Launcher" },
            { "rocket_heavy", "Titan-Heavy Interplanetary Vehicle" }
        };

        private static readonly Dictionary<string, string> Tips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "nav_hub", "Open the Contract Hub to select a NASA mission and review its target orbit and rewards." },
            { "nav_workshop", "Open the Spacecraft Hangar to assemble modules, configure autopilot rules, and run pre-launch checks." },
            { "nav_flight", "Open Live Flight Telemetry to monitor orbital progress, battery reserves, and space weather events." },
            { "nav_debrief", "Open the Post-Mission Debrief to analyze telemetry graphs, failure causes, and budget rewards." },

            { "hub_contract_card", "Accept this mission contract to load its target orbit, required payload sensor, and budget cap." },
            { "hub_signal_delay", "Radio propagation latency between Mission Control and the spacecraft that delays manual ground commands." },
            { "hub_passive_income", "Recurring agency funding generated by operational satellites from previously completed missions." },
            { "rnd_capital", "Available agency research budget in thousands of dollars for unlocking new hardware and chip upgrades." },
            { "rnd_chip_upgrade", "Upgrade the onboard flight computer chip to unlock additional simultaneous IF-THEN-ELSE autopilot rule slots." },
            { "rnd_node_unlock", "Spend agency capital to permanently unlock this spacecraft component for hangar assembly." },

            { "res_mass", "Total spacecraft mass in kilograms compared against the selected rocket's maximum payload limit for this orbit." },
            { "res_power", "Net electrical power balance in Watts and battery reserve capacity across sunlight and eclipse phases." },
            { "res_budget", "Total spacecraft build and rocket launch cost compared against the mission contract's maximum budget cap." },
            { "res_data", "Onboard solid-state drive (SSD) buffer capacity in Gigabits and science data generation rate per tick." },
            { "res_comms", "Downlink transmission throughput in Gigabits per tick used to flush the SSD buffer and deliver science points." },
            { "res_reliability", "Combined radiation shielding and hardware reliability rating protecting the spacecraft during solar storms." },

            { "slot_bus", "Core spacecraft chassis defining structural mass, baseline avionics power draw, and radiation shielding." },
            { "slot_power", "Electrical power generator using photovoltaic solar arrays or continuous radioisotope (RTG) nuclear power." },
            { "slot_battery", "Rechargeable energy storage pack that keeps avionics and instruments alive during orbital eclipse." },
            { "slot_comms", "Telemetry transceiver and onboard SSD storage module governing buffer capacity and downlink speed." },
            { "slot_science", "Primary scientific payload instrument that must match the contract's required sensor tag to collect data." },
            { "slot_rocket", "Launch vehicle booster determining launch cost and maximum allowable spacecraft mass for the target orbit." },
            { "btn_autopilot", "Open the IF-THEN-ELSE Autopilot Editor to program autonomous power, memory, and radiation protection rules." },
            { "btn_dry_run", "Open the A.S.T.R.A. pre-launch auditor and run a 20-tick orbital dry run simulation before launch." },
            { "btn_rnd", "Open the Research & Development Tech Tree to unlock advanced spacecraft modules and flight computer chips." },
            { "btn_launch", "Validate constraints and launch the rocket into target orbit." },

            { "ap_solar", "Monitors live solar panel power generation in Watts to detect when the spacecraft enters or exits eclipse." },
            { "ap_battery", "Monitors remaining battery charge percentage to prevent fatal power depletion in planet shadow." },
            { "ap_ssd", "Monitors onboard SSD memory usage percentage to prevent buffer overflow and data corruption." },
            { "ap_radiation", "Monitors ambient radiation level in rads to detect dangerous Coronal Mass Ejection solar storms." },
            { "ap_safemode", "Emergency protection mode that overrides lower-priority rules and disables payloads to survive hazards." },
            { "ap_priority", "Autopilot rules evaluate top-to-bottom each tick, with Safe Mode holding absolute priority over other actions." },
            { "dryrun_test", "Execute a fast 20-tick orbital dry run simulation to verify battery, SSD, and radiation survival." },

            { "btn_manual_science", "Send a manual ground command to toggle the science payload ON or OFF after the orbital signal delay." },
            { "btn_manual_comms", "Send a manual ground command to toggle the downlink transmitter ON or OFF after the orbital signal delay." },
            { "btn_manual_safemode", "Send an emergency ground command to toggle spacecraft Safe Mode after the orbital signal delay." },
            { "btn_speed", "Change orbital simulation playback speed to fast-forward through nominal mission ticks." },
            { "hud_eclipse", "Displays whether the spacecraft is currently illuminated by the Sun or inside planet eclipse shadow." },
            { "hud_comm", "Displays ground station communication link status and current radio signal latency in seconds." },
            { "hud_event", "Displays active space weather hazards such as Coronal Mass Ejections and solar radiation spikes." },
            { "map_camera", "Control the orbital map view and track the spacecraft's real-time trajectory around the target body." },
            { "debrief_bonus", "Frugal engineering bonus (+50% payout) awarded when the mission succeeds using 80% or less of the budget cap." },
            { "debrief_retry", "Return to the Spacecraft Hangar with your current design to adjust modules or autopilot rules and relaunch." },

            { "Btn_Hub", "Open the Contract Hub to select a NASA mission and review its target orbit and rewards." },
            { "Btn_Workshop", "Open the Spacecraft Hangar to assemble modules, configure autopilot rules, and run pre-launch checks." },
            { "Btn_Flight", "Open Live Flight Telemetry to monitor orbital progress, battery reserves, and space weather events." },
            { "Btn_Debrief", "Open the Post-Mission Debrief to analyze telemetry graphs, failure causes, and budget rewards." },
            { "Btn_OpenAutopilot", "Open the IF-THEN-ELSE Autopilot Editor to program autonomous power, memory, and radiation protection rules." },
            { "Btn_OpenDryRun", "Open the A.S.T.R.A. pre-launch auditor and run a 20-tick orbital dry run simulation before launch." },
            { "Btn_OpenRnD", "Open the Research & Development Tech Tree to unlock advanced spacecraft modules and flight computer chips." },
            { "Btn_LaunchMission", "Validate constraints and launch the rocket into target orbit." },
            { "Btn_RunDryRun", "Execute a fast 20-tick orbital dry run simulation to verify battery, SSD, and radiation survival." },
            { "Btn_ToggleInstruments", "Send a manual ground command to toggle the science payload ON or OFF after the orbital signal delay." },
            { "Btn_ToggleComms", "Send a manual ground command to toggle the downlink transmitter ON or OFF after the orbital signal delay." },
            { "Btn_EnableSafeMode", "Send an emergency ground command to toggle spacecraft Safe Mode after the orbital signal delay." },
            { "Btn_ReturnToWorkshop", "Return to the Spacecraft Hangar with your current design to adjust modules or autopilot rules and relaunch." },
            { "Btn_ReturnToHub", "Open the Contract Hub to select a NASA mission and review its target orbit and rewards." }
        };

        /// <summary>
        /// Gets or sets a value indicating whether tooltip descriptions are globally enabled in user settings.
        /// </summary>
        public static bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                if (value)
                {
                    _isEnabled = true;
                }
                else
                {
                    _isEnabled = false;
                }
            }
        }

        /// <summary>
        /// Enables or disables tooltip descriptions according to the user settings toggle.
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                _isEnabled = true;
            }
            else
            {
                _isEnabled = false;
            }
        }

        /// <summary>
        /// Toggles tooltip descriptions between enabled and disabled states and returns the new state.
        /// </summary>
        public static bool ToggleEnabled()
        {
            if (_isEnabled)
            {
                _isEnabled = false;
            }
            else
            {
                _isEnabled = true;
            }

            return _isEnabled;
        }

        /// <summary>
        /// Retrieves the single-sentence English tooltip description for the specified key, or returns the fallback string if not found.
        /// </summary>
        public static string Get(string key, string fallback = "")
        {
            if (!_isEnabled)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                return fallback ?? string.Empty;
            }

            string tip;
            if (Tips.TryGetValue(key.Trim(), out tip))
            {
                return tip;
            }

            return fallback ?? string.Empty;
        }

        /// <summary>
        /// Formats a single-sentence English tooltip description for a spacecraft component including name, description, mass, cost, and reliability.
        /// </summary>
        public static string GetComponentTip(SatelliteComponent comp)
        {
            if (!_isEnabled || comp == null)
            {
                return string.Empty;
            }

            string name;
            if (string.IsNullOrWhiteSpace(comp.Id) || !ComponentEnglishNames.TryGetValue(comp.Id.Trim(), out name))
            {
                name = !string.IsNullOrWhiteSpace(comp.Name)
                    ? comp.Name.Trim()
                    : (!string.IsNullOrWhiteSpace(comp.Id) ? comp.Id.Trim() : Templates[DescriptionTipTemplate.DefaultComponentName]);
            }

            string description;
            if (string.IsNullOrWhiteSpace(comp.Id) || !ComponentEnglishDescriptions.TryGetValue(comp.Id.Trim(), out description))
            {
                description = !string.IsNullOrWhiteSpace(comp.Description)
                    ? comp.Description.Trim()
                    : Templates[DescriptionTipTemplate.DefaultComponentDescription];
            }

            float reliabilityPercent = comp.BaseReliability <= 1.0f
                ? comp.BaseReliability * 100.0f
                : comp.BaseReliability;

            return string.Format(
                CultureInfo.InvariantCulture,
                Templates[DescriptionTipTemplate.ComponentSummary],
                name,
                description,
                comp.MassKg,
                comp.CostThousands,
                reliabilityPercent);
        }

        /// <summary>
        /// Formats a single-sentence English tooltip description for a launch vehicle including name, maximum orbit mass capacity, cost, and reliability.
        /// </summary>
        public static string GetRocketTip(RocketOption rocket, float maxOrbitMassKg)
        {
            if (!_isEnabled || rocket == null)
            {
                return string.Empty;
            }

            string name;
            if (string.IsNullOrWhiteSpace(rocket.Id) || !RocketEnglishNames.TryGetValue(rocket.Id.Trim(), out name))
            {
                name = !string.IsNullOrWhiteSpace(rocket.Name)
                    ? rocket.Name.Trim()
                    : (!string.IsNullOrWhiteSpace(rocket.Id) ? rocket.Id.Trim() : Templates[DescriptionTipTemplate.DefaultRocketName]);
            }

            float reliabilityPercent = rocket.BaseReliability <= 1.0f
                ? rocket.BaseReliability * 100.0f
                : rocket.BaseReliability;

            return string.Format(
                CultureInfo.InvariantCulture,
                Templates[DescriptionTipTemplate.RocketSummary],
                name,
                maxOrbitMassKg,
                rocket.CostThousands,
                reliabilityPercent);
        }
    }
}
