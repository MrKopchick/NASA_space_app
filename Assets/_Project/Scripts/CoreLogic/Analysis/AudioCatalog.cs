using System;
using System.Collections.Generic;

namespace AstraLink.Core
{
    /// <summary>
    /// Identifies every UI, Workshop, Autopilot, Comms, Space Event, and Mission sound effect in Assets/_Project/Audio.
    /// </summary>
    public enum SoundEffectId
    {
        None = 0,
        UiClick,
        UiClickAlt1,
        UiClickAlt2,
        UiToggle,
        UiError,
        AstraNotification,
        ModuleInstall,
        ModuleRemove,
        ResearchUnlock,
        MissionStart,
        RocketIgnition,
        DryRunComplete,
        MissionSuccess,
        MissionFailed,
        CommandExecuted,
        SafeModeOn,
        SafeModeOff,
        CommsRestored,
        DataDownlink,
        MicrometeoroidEvent,
        SolarStormAlarm,
        SystemWarning,
        MissionControlAmbience
    }

    /// <summary>
    /// Identifies background music and ambience tracks in Assets/_Project/Audio.
    /// </summary>
    public enum MusicTrackId
    {
        None = 0,
        MenuMusic,
        FlightMusic,
        MissionControlAmbience
    }

    /// <summary>
    /// Pure C# catalog mapping sound effects, music tracks, UI button names, and audio preferences to Assets/_Project/Audio paths (Task S3-02).
    /// </summary>
    public static class AudioCatalog
    {
        public const string AudioManagerResourcePath = "AudioManager";

        private static bool _sfxEnabled = true;
        private static bool _musicEnabled = true;
        private static bool _hoverEnabled = true;
        private static float _sfxVolume = 0.85f;
        private static float _hoverVolumeScale = 0.28f;
        private static float _musicVolume = 0.40f;
        private static float _ambienceVolume = 0.25f;

        private static readonly Dictionary<SoundEffectId, string> SfxAssetPaths = new Dictionary<SoundEffectId, string>
        {
            { SoundEffectId.None, string.Empty },
            { SoundEffectId.UiClick, "Assets/_Project/Audio/UI/ui_click.ogg" },
            { SoundEffectId.UiClickAlt1, "Assets/_Project/Audio/Alternatives/ui_click_alt_01.ogg" },
            { SoundEffectId.UiClickAlt2, "Assets/_Project/Audio/Alternatives/ui_click_alt_02.ogg" },
            { SoundEffectId.UiToggle, "Assets/_Project/Audio/UI/ui_toggle.ogg" },
            { SoundEffectId.UiError, "Assets/_Project/Audio/UI/ui_error.ogg" },
            { SoundEffectId.AstraNotification, "Assets/_Project/Audio/UI/astra_notification.ogg" },
            { SoundEffectId.ModuleInstall, "Assets/_Project/Audio/Workshop/module_install.ogg" },
            { SoundEffectId.ModuleRemove, "Assets/_Project/Audio/Workshop/module_remove.ogg" },
            { SoundEffectId.ResearchUnlock, "Assets/_Project/Audio/Workshop/research_unlock.ogg" },
            { SoundEffectId.MissionStart, "Assets/_Project/Audio/Mission/mission_start.ogg" },
            { SoundEffectId.RocketIgnition, "Assets/_Project/Audio/Mission/rocket_ignition.ogg" },
            { SoundEffectId.DryRunComplete, "Assets/_Project/Audio/Mission/dry_run_complete.ogg" },
            { SoundEffectId.MissionSuccess, "Assets/_Project/Audio/Mission/mission_success.ogg" },
            { SoundEffectId.MissionFailed, "Assets/_Project/Audio/Mission/mission_failed.ogg" },
            { SoundEffectId.CommandExecuted, "Assets/_Project/Audio/Autopilot/command_executed.ogg" },
            { SoundEffectId.SafeModeOn, "Assets/_Project/Audio/Autopilot/safe_mode_on.ogg" },
            { SoundEffectId.SafeModeOff, "Assets/_Project/Audio/Autopilot/safe_mode_off.ogg" },
            { SoundEffectId.CommsRestored, "Assets/_Project/Audio/Comms/comms_restored.ogg" },
            { SoundEffectId.DataDownlink, "Assets/_Project/Audio/Comms/data_downlink.ogg" },
            { SoundEffectId.MicrometeoroidEvent, "Assets/_Project/Audio/Events/micrometeoroid_event.ogg" },
            { SoundEffectId.SolarStormAlarm, "Assets/_Project/Audio/Events/solar_storm_alarm.ogg" },
            { SoundEffectId.SystemWarning, "Assets/_Project/Audio/Events/system_warning.ogg" },
            { SoundEffectId.MissionControlAmbience, "Assets/_Project/Audio/Ambience/mission_control_ambience.ogg" }
        };

        private static readonly Dictionary<MusicTrackId, string> MusicAssetPaths = new Dictionary<MusicTrackId, string>
        {
            { MusicTrackId.None, string.Empty },
            { MusicTrackId.MenuMusic, "Assets/_Project/Audio/Music/menu_music.ogg" },
            { MusicTrackId.FlightMusic, "Assets/_Project/Audio/Music/flight_music.ogg" },
            { MusicTrackId.MissionControlAmbience, "Assets/_Project/Audio/Ambience/mission_control_ambience.ogg" }
        };

        private static readonly Dictionary<string, SoundEffectId> ButtonKeyToSound = new Dictionary<string, SoundEffectId>(StringComparer.OrdinalIgnoreCase)
        {
            { "Btn_StartGame", SoundEffectId.MissionStart },
            { "StartButton", SoundEffectId.MissionStart },
            { "Btn_NewCampaign", SoundEffectId.MissionStart },
            { "Btn_Quit", SoundEffectId.UiClickAlt1 },

            { "Btn_Hub", SoundEffectId.UiClick },
            { "Btn_Workshop", SoundEffectId.UiClick },
            { "Btn_Flight", SoundEffectId.UiClick },
            { "Btn_Debrief", SoundEffectId.UiClick },

            { "Btn_AcceptContract_0", SoundEffectId.MissionStart },
            { "Btn_AcceptContract_1", SoundEffectId.MissionStart },
            { "Btn_AcceptContract_2", SoundEffectId.MissionStart },

            { "Btn_LaunchMission", SoundEffectId.RocketIgnition },
            { "btn_launch", SoundEffectId.RocketIgnition },
            { "Btn_PlayLaunch", SoundEffectId.RocketIgnition },
            { "Btn_RunDryRun", SoundEffectId.MissionControlAmbience },
            { "btn_dry_run", SoundEffectId.MissionControlAmbience },
            { "Btn_RefreshAudit", SoundEffectId.AstraNotification },
            { "Btn_OpenDryRun", SoundEffectId.AstraNotification },

            { "Btn_Prev", SoundEffectId.CommsRestored },
            { "Btn_Next", SoundEffectId.CommsRestored },
            { "Btn_PrevRocket", SoundEffectId.CommsRestored },
            { "Btn_NextRocket", SoundEffectId.CommsRestored },
            { "Btn_ResetTemplate", SoundEffectId.ModuleRemove },
            { "Btn_ClearRules", SoundEffectId.ModuleRemove },

            { "Btn_NodeAction", SoundEffectId.ResearchUnlock },
            { "Btn_UpgradeChip_4", SoundEffectId.ResearchUnlock },
            { "Btn_UpgradeChip_6", SoundEffectId.ResearchUnlock },
            { "rnd_node_unlock", SoundEffectId.ResearchUnlock },
            { "rnd_chip_upgrade", SoundEffectId.ResearchUnlock },

            { "Btn_PresetEclipse", SoundEffectId.CommandExecuted },
            { "Btn_PresetStorm", SoundEffectId.CommandExecuted },
            { "Btn_EnableSafeMode", SoundEffectId.SafeModeOn },
            { "btn_manual_safemode", SoundEffectId.SafeModeOn },
            { "Btn_ToggleInstruments", SoundEffectId.UiToggle },
            { "btn_manual_science", SoundEffectId.UiToggle },
            { "Btn_ToggleComms", SoundEffectId.UiToggle },
            { "btn_manual_comms", SoundEffectId.UiToggle },
            { "Toggle_Enabled", SoundEffectId.UiToggle }
        };

        /// <summary>
        /// Gets or sets a value indicating whether UI and gameplay sound effects are enabled.
        /// </summary>
        public static bool IsSfxEnabled
        {
            get { return _sfxEnabled; }
            set { SetSfxEnabled(value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether background music and ambience are enabled.
        /// </summary>
        public static bool IsMusicEnabled
        {
            get { return _musicEnabled; }
            set { SetMusicEnabled(value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether pointer hover ticks are enabled.
        /// </summary>
        public static bool IsHoverEnabled
        {
            get { return _hoverEnabled; }
            set { SetHoverEnabled(value); }
        }

        /// <summary>
        /// Gets or sets the normalized SFX playback volume from 0.0 to 1.0.
        /// </summary>
        public static float SfxVolume
        {
            get { return _sfxVolume; }
            set { _sfxVolume = Clamp01(value); }
        }

        /// <summary>
        /// Gets or sets the relative volume multiplier applied to UI hover sounds.
        /// </summary>
        public static float HoverVolumeScale
        {
            get { return _hoverVolumeScale; }
            set { _hoverVolumeScale = Clamp01(value); }
        }

        /// <summary>
        /// Gets or sets the normalized background music volume from 0.0 to 1.0.
        /// </summary>
        public static float MusicVolume
        {
            get { return _musicVolume; }
            set { _musicVolume = Clamp01(value); }
        }

        /// <summary>
        /// Gets or sets the normalized mission control ambience volume from 0.0 to 1.0.
        /// </summary>
        public static float AmbienceVolume
        {
            get { return _ambienceVolume; }
            set { _ambienceVolume = Clamp01(value); }
        }

        /// <summary>
        /// Enables or disables sound effects according to user preferences.
        /// </summary>
        public static void SetSfxEnabled(bool enabled)
        {
            if (enabled)
            {
                _sfxEnabled = true;
            }
            else
            {
                _sfxEnabled = false;
            }
        }

        /// <summary>
        /// Enables or disables background music and ambience according to user preferences.
        /// </summary>
        public static void SetMusicEnabled(bool enabled)
        {
            if (enabled)
            {
                _musicEnabled = true;
            }
            else
            {
                _musicEnabled = false;
            }
        }

        /// <summary>
        /// Enables or disables UI hover sounds according to user preferences.
        /// </summary>
        public static void SetHoverEnabled(bool enabled)
        {
            if (enabled)
            {
                _hoverEnabled = true;
            }
            else
            {
                _hoverEnabled = false;
            }
        }

        /// <summary>
        /// Toggles sound effects on or off and returns the updated state.
        /// </summary>
        public static bool ToggleSfxEnabled()
        {
            if (_sfxEnabled)
            {
                _sfxEnabled = false;
            }
            else
            {
                _sfxEnabled = true;
            }

            return _sfxEnabled;
        }

        /// <summary>
        /// Toggles background music on or off and returns the updated state.
        /// </summary>
        public static bool ToggleMusicEnabled()
        {
            if (_musicEnabled)
            {
                _musicEnabled = false;
            }
            else
            {
                _musicEnabled = true;
            }

            return _musicEnabled;
        }

        /// <summary>
        /// Returns the relative Unity project asset path for the specified <see cref="SoundEffectId"/>.
        /// </summary>
        public static string GetSfxAssetPath(SoundEffectId soundId)
        {
            string path;
            return SfxAssetPaths.TryGetValue(soundId, out path) ? path : string.Empty;
        }

        /// <summary>
        /// Returns the relative Unity project asset path for the specified <see cref="MusicTrackId"/>.
        /// </summary>
        public static string GetMusicAssetPath(MusicTrackId trackId)
        {
            string path;
            return MusicAssetPaths.TryGetValue(trackId, out path) ? path : string.Empty;
        }

        /// <summary>
        /// Returns the <c>Resources.Load</c>-compatible path (without prefix or file extension) for the specified <see cref="SoundEffectId"/>.
        /// </summary>
        public static string GetSfxResourcePath(SoundEffectId soundId)
        {
            return ConvertAssetPathToResourcePath(GetSfxAssetPath(soundId));
        }

        /// <summary>
        /// Returns the <c>Resources.Load</c>-compatible path (without prefix or file extension) for the specified <see cref="MusicTrackId"/>.
        /// </summary>
        public static string GetMusicResourcePath(MusicTrackId trackId)
        {
            return ConvertAssetPathToResourcePath(GetMusicAssetPath(trackId));
        }

        /// <summary>
        /// Resolves the appropriate background music track for the given scene name.
        /// </summary>
        public static MusicTrackId ResolveMusicForScene(string sceneName)
        {
            if (!string.IsNullOrWhiteSpace(sceneName) && ContainsIgnoreCase(sceneName, "Menu"))
            {
                return MusicTrackId.MenuMusic;
            }

            return MusicTrackId.FlightMusic;
        }

        private static string ConvertAssetPathToResourcePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            string normalized = assetPath.Trim().Replace('\\', '/');
            const string projectPrefix = "Assets/_Project/";
            const string resourcesPrefix = "Assets/_Project/Resources/";

            if (normalized.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(resourcesPrefix.Length);
            }
            else if (normalized.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(projectPrefix.Length);
            }

            int dotIndex = normalized.LastIndexOf('.');
            if (dotIndex > 0)
            {
                normalized = normalized.Substring(0, dotIndex);
            }

            return normalized;
        }

        /// <summary>
        /// Resolves the appropriate <see cref="SoundEffectId"/> for a space event title.
        /// </summary>
        public static SoundEffectId ResolveSoundForSpaceEvent(string eventTitle, bool isActive)
        {
            if (!isActive)
            {
                return SoundEffectId.CommsRestored;
            }

            if (string.IsNullOrWhiteSpace(eventTitle))
            {
                return SoundEffectId.SystemWarning;
            }

            if (ContainsIgnoreCase(eventTitle, "Solar") || ContainsIgnoreCase(eventTitle, "Storm") || ContainsIgnoreCase(eventTitle, "CME") || ContainsIgnoreCase(eventTitle, "Flare"))
            {
                return SoundEffectId.SolarStormAlarm;
            }

            if (ContainsIgnoreCase(eventTitle, "Micrometeoroid") || ContainsIgnoreCase(eventTitle, "Meteor") || ContainsIgnoreCase(eventTitle, "Dust"))
            {
                return SoundEffectId.MicrometeoroidEvent;
            }

            return SoundEffectId.SystemWarning;
        }

        /// <summary>
        /// Resolves the most appropriate <see cref="SoundEffectId"/> for any Unity UI button or toggle name.
        /// </summary>
        public static SoundEffectId ResolveSoundForButton(string buttonName, bool isToggle = false)
        {
            if (string.IsNullOrWhiteSpace(buttonName))
            {
                return isToggle ? SoundEffectId.UiToggle : SoundEffectId.UiClick;
            }

            string trimmed = buttonName.Trim();
            SoundEffectId exact;
            if (ButtonKeyToSound.TryGetValue(trimmed, out exact))
            {
                return exact;
            }

            if (ContainsIgnoreCase(trimmed, "Launch") || ContainsIgnoreCase(trimmed, "Ignition"))
            {
                return SoundEffectId.RocketIgnition;
            }

            if (ContainsIgnoreCase(trimmed, "Unlock") || ContainsIgnoreCase(trimmed, "Upgrade") || ContainsIgnoreCase(trimmed, "Research"))
            {
                return SoundEffectId.ResearchUnlock;
            }

            if (ContainsIgnoreCase(trimmed, "SafeMode"))
            {
                return SoundEffectId.SafeModeOn;
            }

            if (ContainsIgnoreCase(trimmed, "RunDryRun"))
            {
                return SoundEffectId.MissionControlAmbience;
            }

            if (ContainsIgnoreCase(trimmed, "DryRun"))
            {
                return SoundEffectId.DryRunComplete;
            }

            if (ContainsIgnoreCase(trimmed, "Audit") || ContainsIgnoreCase(trimmed, "Astra"))
            {
                return SoundEffectId.AstraNotification;
            }

            if (ContainsIgnoreCase(trimmed, "AcceptContract") || ContainsIgnoreCase(trimmed, "StartGame"))
            {
                return SoundEffectId.MissionStart;
            }

            if (ContainsIgnoreCase(trimmed, "Reset") || ContainsIgnoreCase(trimmed, "Clear") || ContainsIgnoreCase(trimmed, "Remove"))
            {
                return SoundEffectId.ModuleRemove;
            }

            if (ContainsIgnoreCase(trimmed, "Prev") || ContainsIgnoreCase(trimmed, "Next"))
            {
                return SoundEffectId.CommsRestored;
            }

            return isToggle ? SoundEffectId.UiToggle : SoundEffectId.UiClick;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
