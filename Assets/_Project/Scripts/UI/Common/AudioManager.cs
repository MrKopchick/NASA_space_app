using System.Collections.Generic;
using AstraLink.UI.Flight;
using AstraLink.UI.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AstraLink.Core
{
    /// <summary>
    /// Singleton audio coordinator that auto-binds all UI buttons and manages SFX, background music, and ambience from Assets/_Project/Audio (Task S3-02).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public const string SfxEnabledPrefsKey = "AstraLink_SfxEnabled";
        public const string MusicEnabledPrefsKey = "AstraLink_MusicEnabled";
        public const string HoverEnabledPrefsKey = "AstraLink_HoverSfxEnabled";
        public const string SfxVolumePrefsKey = "AstraLink_SfxVolume";
        public const string MusicVolumePrefsKey = "AstraLink_MusicVolume";

        private static AudioManager _instance;

        [Header("Audio Preferences")]
        [SerializeField] private bool sfxEnabled = true;
        [SerializeField] private bool musicEnabled = true;
        [SerializeField] private bool hoverEnabled = true;
        [SerializeField] [Range(0f, 1f)] private float sfxVolume = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.40f;
        [SerializeField] [Range(0f, 1f)] private float ambienceVolume = 0.25f;

        [Header("Background Music & Ambience (Assets/_Project/Audio)")]
        [SerializeField] private AudioClip menuMusicClip;
        [SerializeField] private AudioClip flightMusicClip;
        [SerializeField] private AudioClip missionControlAmbienceClip;

        [Header("UI Sound Effects (Assets/_Project/Audio/UI & Alternatives)")]
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip uiClickAlt1Clip;
        [SerializeField] private AudioClip uiClickAlt2Clip;
        [SerializeField] private AudioClip uiToggleClip;
        [SerializeField] private AudioClip uiErrorClip;
        [SerializeField] private AudioClip astraNotificationClip;

        [Header("Workshop & R&D Sound Effects (Assets/_Project/Audio/Workshop)")]
        [SerializeField] private AudioClip moduleInstallClip;
        [SerializeField] private AudioClip moduleRemoveClip;
        [SerializeField] private AudioClip researchUnlockClip;

        [Header("Mission & Flight Sound Effects (Assets/_Project/Audio/Mission)")]
        [SerializeField] private AudioClip missionStartClip;
        [SerializeField] private AudioClip rocketIgnitionClip;
        [SerializeField] private AudioClip dryRunCompleteClip;
        [SerializeField] private AudioClip missionSuccessClip;
        [SerializeField] private AudioClip missionFailedClip;

        [Header("Autopilot, Comms & Space Events (Assets/_Project/Audio)")]
        [SerializeField] private AudioClip commandExecutedClip;
        [SerializeField] private AudioClip safeModeOnClip;
        [SerializeField] private AudioClip safeModeOffClip;
        [SerializeField] private AudioClip commsRestoredClip;
        [SerializeField] private AudioClip dataDownlinkClip;
        [SerializeField] private AudioClip micrometeoroidEventClip;
        [SerializeField] private AudioClip solarStormAlarmClip;
        [SerializeField] private AudioClip systemWarningClip;

        private AudioSource _sfxSource;
        private AudioSource _longSfxSource;
        private AudioSource _musicSource;
        private AudioSource _ambienceSource;
        private Dictionary<SoundEffectId, AudioClip> _sfxMap;
        private Dictionary<MusicTrackId, AudioClip> _musicMap;
        private readonly HashSet<int> _autoBoundControls = new HashSet<int>();
        private readonly HashSet<int> _boundEventPopups = new HashSet<int>();
        private readonly HashSet<int> _boundDryRunUis = new HashSet<int>();
        private readonly Dictionary<SoundEffectId, float> _lastPlayedTimes = new Dictionary<SoundEffectId, float>();
        private GameManager _boundGameManager;
        private DryRunUI _activeDryRunUi;
        private Button _activeDryRunButton;
        private bool _isDryRunAmbienceActive;
        private float _dryRunStartedAt;
        private float _nextScanTime;
        private float _lastErrorSoundTime;
        private float _lastWarningSoundTime;
        private float _lastButtonClickTime;

        /// <summary>
        /// Gets a value indicating whether an active <see cref="AudioManager"/> instance currently exists.
        /// </summary>
        public static bool HasInstance
        {
            get { return _instance != null; }
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="AudioManager"/>, creating it automatically if not present in the scene.
        /// </summary>
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AudioManager>(true);
                    if (_instance == null)
                    {
                        GameObject host = new GameObject("AudioManager");
                        DontDestroyOnLoad(host);
                        _instance = host.AddComponent<AudioManager>();
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// Gets or sets whether sound effects are enabled in user preferences.
        /// </summary>
        public bool IsSfxEnabled
        {
            get { return sfxEnabled && AudioCatalog.IsSfxEnabled; }
            set { SetSfxEnabled(value); }
        }

        /// <summary>
        /// Gets or sets whether background music and ambience are enabled in user preferences.
        /// </summary>
        public bool IsMusicEnabled
        {
            get { return musicEnabled && AudioCatalog.IsMusicEnabled; }
            set { SetMusicEnabled(value); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeOnLoad()
        {
            AudioManager manager = Instance;
            if (manager != null)
            {
                manager.AutoBindSceneControls();
                manager.PlayMusicForScene(SceneManager.GetActiveScene().name);
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            AutoAssignClipsInEditorIfMissing();
            EnsureAudioSources();
            RebuildLookupMaps();
            LoadAudioPreferences();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += HandleUnityLogMessage;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Application.logMessageReceived -= HandleUnityLogMessage;
                UnsubscribeGameManager();
                _instance = null;
            }
        }

        private void Start()
        {
            AutoBindSceneControls();
            BindGameplayEventSources();
            PlayMusicForScene(SceneManager.GetActiveScene().name);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _autoBoundControls.Clear();
            _boundEventPopups.Clear();
            _boundGameManager = null;
            AutoBindSceneControls();
            BindGameplayEventSources();
            PlayMusicForScene(scene.name);
        }

        private void Update()
        {
            if (_isDryRunAmbienceActive)
            {
                float elapsed = Time.unscaledTime - _dryRunStartedAt;
                bool buttonReadyAgain = _activeDryRunButton != null && _activeDryRunButton.interactable && elapsed >= 0.25f;
                bool modalClosed = _activeDryRunUi == null || !_activeDryRunUi.gameObject.activeInHierarchy;
                bool timedOut = elapsed >= DryRunUI.DryRunDurationSeconds + 0.5f;
                if (buttonReadyAgain || modalClosed || timedOut)
                {
                    StopDryRunAmbience(false);
                }
            }

            if (Time.unscaledTime >= _nextScanTime)
            {
                _nextScanTime = Time.unscaledTime + 0.25f;
                AutoBindSceneControls();
                BindGameplayEventSources();
            }
        }

        /// <summary>
        /// Plays a registered sound effect by <see cref="SoundEffectId"/> with an optional volume scale.
        /// </summary>
        public void PlaySfx(SoundEffectId soundId, float volumeScale = 1.0f)
        {
            if (!IsSfxEnabled || soundId == SoundEffectId.None)
            {
                return;
            }

            float now = Time.unscaledTime;
            float lastTime;
            if (_lastPlayedTimes.TryGetValue(soundId, out lastTime) && now - lastTime < 0.16f)
            {
                return;
            }

            _lastPlayedTimes[soundId] = now;
            if (volumeScale >= 0.9f)
            {
                _lastButtonClickTime = now;
            }

            EnsureAudioSources();
            RebuildLookupMaps();

            float finalVolume = Mathf.Clamp01(sfxVolume * volumeScale);
            if (soundId == SoundEffectId.MissionControlAmbience)
            {
                if (volumeScale >= 0.9f)
                {
                    StartDryRunAmbience();
                }
                else if (uiClickClip != null)
                {
                    _sfxSource.PlayOneShot(uiClickClip, finalVolume);
                }

                return;
            }

            AudioClip clip;
            if (_sfxMap.TryGetValue(soundId, out clip) && clip != null)
            {
                if (IsLongEventSound(soundId) && _longSfxSource != null)
                {
                    _longSfxSource.Stop();
                    _longSfxSource.clip = clip;
                    _longSfxSource.volume = finalVolume;
                    _longSfxSource.Play();
                }
                else
                {
                    _sfxSource.PlayOneShot(clip, finalVolume);
                }

                return;
            }

            if (uiClickClip != null)
            {
                _sfxSource.PlayOneShot(uiClickClip, finalVolume);
            }
        }

        private static bool IsLongEventSound(SoundEffectId soundId)
        {
            return soundId == SoundEffectId.RocketIgnition ||
                   soundId == SoundEffectId.DataDownlink ||
                   soundId == SoundEffectId.SolarStormAlarm ||
                   soundId == SoundEffectId.MicrometeoroidEvent ||
                   soundId == SoundEffectId.MissionSuccess ||
                   soundId == SoundEffectId.MissionFailed;
        }

        /// <summary>
        /// Stops any currently playing long event or flight sound effect.
        /// </summary>
        public void StopLongSfx()
        {
            if (_longSfxSource != null && _longSfxSource.isPlaying)
            {
                _longSfxSource.Stop();
            }
        }

        /// <summary>
        /// Plays a custom <see cref="AudioClip"/> as a one-shot sound effect with an optional volume scale.
        /// </summary>
        public void PlayCustomSfx(AudioClip clip, float volumeScale = 1.0f)
        {
            if (!IsSfxEnabled || clip == null)
            {
                return;
            }

            EnsureAudioSources();
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volumeScale));
        }

        /// <summary>
        /// Starts or switches looping background music to the specified <see cref="MusicTrackId"/>.
        /// </summary>
        public void PlayMusic(MusicTrackId trackId)
        {
            EnsureAudioSources();
            RebuildLookupMaps();

            if (!IsMusicEnabled || trackId == MusicTrackId.None)
            {
                StopMusic();
                return;
            }

            AudioClip targetClip;
            if (!_musicMap.TryGetValue(trackId, out targetClip) || targetClip == null)
            {
                return;
            }

            if (_musicSource.clip == targetClip && _musicSource.isPlaying)
            {
                return;
            }

            _musicSource.clip = targetClip;
            _musicSource.volume = musicVolume;
            _musicSource.Play();

            UpdateAmbiencePlayback();
        }

        private void StartDryRunAmbience()
        {
            EnsureAudioSources();
            RebuildLookupMaps();

            _isDryRunAmbienceActive = true;
            _dryRunStartedAt = Time.unscaledTime;

            if (_activeDryRunUi == null)
            {
                _activeDryRunUi = FindObjectOfType<DryRunUI>(true);
            }

            if (_activeDryRunUi != null && _activeDryRunButton == null)
            {
                Button[] buttons = _activeDryRunUi.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null && buttons[i].name == "Btn_RunDryRun")
                    {
                        _activeDryRunButton = buttons[i];
                        break;
                    }
                }
            }

            UpdateAmbiencePlayback();
        }

        private void StopDryRunAmbience(bool playCompletionSfx)
        {
            if (!_isDryRunAmbienceActive)
            {
                return;
            }

            _isDryRunAmbienceActive = false;
            UpdateAmbiencePlayback();

            if (playCompletionSfx)
            {
                PlaySfx(SoundEffectId.DryRunComplete);
            }
        }

        private void UpdateAmbiencePlayback()
        {
            if (_ambienceSource == null)
            {
                return;
            }

            bool isFlightScreen = GameManager.Instance != null && GameManager.Instance.CurrentScreen == GameScreen.Flight;
            bool shouldPlayAmbience = IsMusicEnabled && missionControlAmbienceClip != null && (isFlightScreen || _isDryRunAmbienceActive);

            if (shouldPlayAmbience)
            {
                if (_ambienceSource.clip != missionControlAmbienceClip || !_ambienceSource.isPlaying)
                {
                    _ambienceSource.clip = missionControlAmbienceClip;
                    _ambienceSource.volume = ambienceVolume;
                    _ambienceSource.Play();
                }
            }
            else if (_ambienceSource.isPlaying)
            {
                _ambienceSource.Stop();
            }
        }

        /// <summary>
        /// Selects and plays the background music for the active scene.
        /// </summary>
        public void PlayMusicForScene(string sceneName)
        {
            if (!IsMusicEnabled)
            {
                StopMusic();
                return;
            }

            PlayMusic(MusicTrackId.FlightMusic);
        }

        /// <summary>
        /// Stops background music, mission control ambience, and any active long event sound.
        /// </summary>
        public void StopMusic()
        {
            _isDryRunAmbienceActive = false;

            if (_musicSource != null && _musicSource.isPlaying)
            {
                _musicSource.Stop();
            }

            if (_ambienceSource != null && _ambienceSource.isPlaying)
            {
                _ambienceSource.Stop();
            }

            StopLongSfx();
        }

        /// <summary>
        /// Enables or disables UI and gameplay sound effects from user settings and persists the preference.
        /// </summary>
        public void SetSfxEnabled(bool enabled)
        {
            if (enabled)
            {
                sfxEnabled = true;
                AudioCatalog.SetSfxEnabled(true);
                PlayerPrefs.SetInt(SfxEnabledPrefsKey, 1);
            }
            else
            {
                sfxEnabled = false;
                AudioCatalog.SetSfxEnabled(false);
                PlayerPrefs.SetInt(SfxEnabledPrefsKey, 0);
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Enables or disables background music and ambience from user settings and persists the preference.
        /// </summary>
        public void SetMusicEnabled(bool enabled)
        {
            if (enabled)
            {
                musicEnabled = true;
                AudioCatalog.SetMusicEnabled(true);
                PlayerPrefs.SetInt(MusicEnabledPrefsKey, 1);
                PlayMusicForScene(SceneManager.GetActiveScene().name);
            }
            else
            {
                musicEnabled = false;
                AudioCatalog.SetMusicEnabled(false);
                PlayerPrefs.SetInt(MusicEnabledPrefsKey, 0);
                StopMusic();
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Toggles sound effects on or off and returns the updated state.
        /// </summary>
        public bool ToggleSfxEnabled()
        {
            if (IsSfxEnabled)
            {
                SetSfxEnabled(false);
                return false;
            }
            else
            {
                SetSfxEnabled(true);
                return true;
            }
        }

        /// <summary>
        /// Toggles background music on or off and returns the updated state.
        /// </summary>
        public bool ToggleMusicEnabled()
        {
            if (IsMusicEnabled)
            {
                SetMusicEnabled(false);
                return false;
            }
            else
            {
                SetMusicEnabled(true);
                return true;
            }
        }

        /// <summary>
        /// Updates SFX playback volume (0.0 to 1.0) and persists it in PlayerPrefs.
        /// </summary>
        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            AudioCatalog.SfxVolume = sfxVolume;
            PlayerPrefs.SetFloat(SfxVolumePrefsKey, sfxVolume);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Updates background music playback volume (0.0 to 1.0) and persists it in PlayerPrefs.
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            AudioCatalog.MusicVolume = musicVolume;
            if (_musicSource != null)
            {
                _musicSource.volume = musicVolume;
            }

            PlayerPrefs.SetFloat(MusicVolumePrefsKey, musicVolume);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Enables or disables UI hover tick sounds from user settings and persists the preference.
        /// </summary>
        public void SetHoverEnabled(bool enabled)
        {
            if (enabled)
            {
                hoverEnabled = true;
                AudioCatalog.SetHoverEnabled(true);
                PlayerPrefs.SetInt(HoverEnabledPrefsKey, 1);
            }
            else
            {
                hoverEnabled = false;
                AudioCatalog.SetHoverEnabled(false);
                PlayerPrefs.SetInt(HoverEnabledPrefsKey, 0);
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Scans all active and inactive interactive UI controls in the scene and attaches <see cref="AudioTrigger"/> automatically.
        /// </summary>
        public void AutoBindSceneControls()
        {
            Selectable[] selectables = FindObjectsOfType<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable item = selectables[i];
                if (item == null)
                {
                    continue;
                }

                bool isSupported = item is Button || item is Toggle || item is TMP_Dropdown || item is Dropdown || item is Slider;
                if (!isSupported)
                {
                    continue;
                }

                int id = item.GetInstanceID();
                if (_autoBoundControls.Contains(id))
                {
                    continue;
                }

                _autoBoundControls.Add(id);

                if (item.GetComponent<AudioTrigger>() != null)
                {
                    continue;
                }

                bool isToggleOrDropdown = item is Toggle || item is TMP_Dropdown || item is Dropdown || item is Slider;
                SoundEffectId resolved = AudioCatalog.ResolveSoundForButton(item.gameObject.name, isToggleOrDropdown);
                AudioTrigger.Attach(item.gameObject, resolved, hoverEnabled);
            }
        }

        private void BindGameplayEventSources()
        {
            if (_boundGameManager == null && GameManager.Instance != null)
            {
                _boundGameManager = GameManager.Instance;
                _boundGameManager.OnScreenChanged += HandleGameScreenChanged;
                _boundGameManager.OnActiveMissionChanged += HandleActiveMissionChanged;
            }

            EventPopupUI[] popups = FindObjectsOfType<EventPopupUI>(true);
            for (int i = 0; i < popups.Length; i++)
            {
                EventPopupUI popup = popups[i];
                if (popup == null)
                {
                    continue;
                }

                int id = popup.GetInstanceID();
                if (_boundEventPopups.Contains(id))
                {
                    continue;
                }

                _boundEventPopups.Add(id);
                popup.OnEventStateChanged += HandleSpaceEventStateChanged;
            }

            DryRunUI[] dryRunUis = FindObjectsOfType<DryRunUI>(true);
            for (int i = 0; i < dryRunUis.Length; i++)
            {
                DryRunUI dryRunUi = dryRunUis[i];
                if (dryRunUi == null)
                {
                    continue;
                }

                _activeDryRunUi = dryRunUi;
                int id = dryRunUi.GetInstanceID();
                if (_boundDryRunUis.Contains(id))
                {
                    continue;
                }

                _boundDryRunUis.Add(id);
                dryRunUi.OnDryRunCompleted += HandleDryRunCompleted;
            }
        }

        private void UnsubscribeGameManager()
        {
            if (_boundGameManager != null)
            {
                _boundGameManager.OnScreenChanged -= HandleGameScreenChanged;
                _boundGameManager.OnActiveMissionChanged -= HandleActiveMissionChanged;
                _boundGameManager = null;
            }
        }

        private void HandleGameScreenChanged(GameScreen screen)
        {
            _isDryRunAmbienceActive = false;
            StopLongSfx();
            UpdateAmbiencePlayback();

            if (IsMusicEnabled)
            {
                PlayMusic(MusicTrackId.FlightMusic);
            }
            else
            {
                StopMusic();
            }

            if (screen == GameScreen.Debrief && _boundGameManager != null && _boundGameManager.LastMissionReport != null)
            {
                if (_boundGameManager.LastMissionReport.Status == MissionStatus.Success)
                {
                    PlaySfx(SoundEffectId.MissionSuccess);
                }
                else if (_boundGameManager.LastMissionReport.Status == MissionStatus.Failed)
                {
                    PlaySfx(SoundEffectId.MissionFailed);
                }
            }
        }

        private void HandleActiveMissionChanged(MissionGoal goal)
        {
            if (goal != null)
            {
                PlaySfx(SoundEffectId.MissionStart);
            }
        }

        private void HandleDryRunCompleted(MissionReport report)
        {
            StopDryRunAmbience(true);
        }

        private void HandleSpaceEventStateChanged(string eventTitle, bool isActive)
        {
            if (Time.unscaledTime - _lastButtonClickTime < 0.25f)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentScreen != GameScreen.Flight)
            {
                return;
            }

            SoundEffectId soundId = AudioCatalog.ResolveSoundForSpaceEvent(eventTitle, isActive);
            PlaySfx(soundId);
        }

        private void HandleUnityLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!IsSfxEnabled || string.IsNullOrEmpty(condition))
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (Time.unscaledTime - _lastErrorSoundTime >= 0.35f)
                {
                    _lastErrorSoundTime = Time.unscaledTime;
                    PlaySfx(SoundEffectId.UiError);
                }
            }
            else if (type == LogType.Warning)
            {
                if (condition.Contains("[MissionValidator]") || condition.Contains("[AstraAuditor]") || condition.Contains("[GameManager]"))
                {
                    if (Time.unscaledTime - _lastWarningSoundTime >= 0.50f)
                    {
                        _lastWarningSoundTime = Time.unscaledTime;
                        PlaySfx(SoundEffectId.SystemWarning);
                    }
                }
            }
        }

        private void EnsureAudioSources()
        {
            if (_sfxSource == null)
            {
                _sfxSource = CreateChildAudioSource("AudioSource_SFX", false);
            }

            if (_longSfxSource == null)
            {
                _longSfxSource = CreateChildAudioSource("AudioSource_LongSFX", false);
            }

            if (_musicSource == null)
            {
                _musicSource = CreateChildAudioSource("AudioSource_Music", true);
            }

            if (_ambienceSource == null)
            {
                _ambienceSource = CreateChildAudioSource("AudioSource_Ambience", true);
            }
        }

        private AudioSource CreateChildAudioSource(string sourceName, bool loop)
        {
            Transform existing = transform.Find(sourceName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(sourceName);
            if (existing == null)
            {
                child.transform.SetParent(transform, false);
            }

            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null)
            {
                source = child.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private void RebuildLookupMaps()
        {
            _sfxMap = new Dictionary<SoundEffectId, AudioClip>
            {
                { SoundEffectId.UiClick, uiClickClip },
                { SoundEffectId.UiClickAlt1, uiClickAlt1Clip },
                { SoundEffectId.UiClickAlt2, uiClickAlt2Clip },
                { SoundEffectId.UiToggle, uiToggleClip },
                { SoundEffectId.UiError, uiErrorClip },
                { SoundEffectId.AstraNotification, astraNotificationClip },
                { SoundEffectId.ModuleInstall, moduleInstallClip },
                { SoundEffectId.ModuleRemove, moduleRemoveClip },
                { SoundEffectId.ResearchUnlock, researchUnlockClip },
                { SoundEffectId.MissionStart, missionStartClip },
                { SoundEffectId.RocketIgnition, rocketIgnitionClip },
                { SoundEffectId.DryRunComplete, dryRunCompleteClip },
                { SoundEffectId.MissionSuccess, missionSuccessClip },
                { SoundEffectId.MissionFailed, missionFailedClip },
                { SoundEffectId.CommandExecuted, commandExecutedClip },
                { SoundEffectId.SafeModeOn, safeModeOnClip },
                { SoundEffectId.SafeModeOff, safeModeOffClip },
                { SoundEffectId.CommsRestored, commsRestoredClip },
                { SoundEffectId.DataDownlink, dataDownlinkClip },
                { SoundEffectId.MicrometeoroidEvent, micrometeoroidEventClip },
                { SoundEffectId.SolarStormAlarm, solarStormAlarmClip },
                { SoundEffectId.SystemWarning, systemWarningClip },
                { SoundEffectId.MissionControlAmbience, missionControlAmbienceClip }
            };

            _musicMap = new Dictionary<MusicTrackId, AudioClip>
            {
                { MusicTrackId.MenuMusic, menuMusicClip },
                { MusicTrackId.FlightMusic, flightMusicClip },
                { MusicTrackId.MissionControlAmbience, missionControlAmbienceClip }
            };
        }

        private void LoadAudioPreferences()
        {
            if (PlayerPrefs.HasKey(SfxEnabledPrefsKey))
            {
                bool savedSfx = PlayerPrefs.GetInt(SfxEnabledPrefsKey, 1) == 1;
                if (savedSfx)
                {
                    sfxEnabled = true;
                    AudioCatalog.SetSfxEnabled(true);
                }
                else
                {
                    sfxEnabled = false;
                    AudioCatalog.SetSfxEnabled(false);
                }
            }

            if (PlayerPrefs.HasKey(MusicEnabledPrefsKey))
            {
                bool savedMusic = PlayerPrefs.GetInt(MusicEnabledPrefsKey, 1) == 1;
                if (savedMusic)
                {
                    musicEnabled = true;
                    AudioCatalog.SetMusicEnabled(true);
                }
                else
                {
                    musicEnabled = false;
                    AudioCatalog.SetMusicEnabled(false);
                }
            }

            if (PlayerPrefs.HasKey(HoverEnabledPrefsKey))
            {
                bool savedHover = PlayerPrefs.GetInt(HoverEnabledPrefsKey, 1) == 1;
                if (savedHover)
                {
                    hoverEnabled = true;
                    AudioCatalog.SetHoverEnabled(true);
                }
                else
                {
                    hoverEnabled = false;
                    AudioCatalog.SetHoverEnabled(false);
                }
            }

            if (PlayerPrefs.HasKey(SfxVolumePrefsKey))
            {
                sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefsKey, sfxVolume));
                AudioCatalog.SfxVolume = sfxVolume;
            }

            if (PlayerPrefs.HasKey(MusicVolumePrefsKey))
            {
                musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefsKey, musicVolume));
                AudioCatalog.MusicVolume = musicVolume;
            }
        }

        private void AutoAssignClipsInEditorIfMissing()
        {
#if UNITY_EDITOR
            if (menuMusicClip == null) menuMusicClip = LoadClip(MusicTrackId.MenuMusic);
            if (flightMusicClip == null) flightMusicClip = LoadClip(MusicTrackId.FlightMusic);
            if (missionControlAmbienceClip == null) missionControlAmbienceClip = LoadClip(MusicTrackId.MissionControlAmbience);

            if (uiClickClip == null) uiClickClip = LoadClip(SoundEffectId.UiClick);
            if (uiClickAlt1Clip == null) uiClickAlt1Clip = LoadClip(SoundEffectId.UiClickAlt1);
            if (uiClickAlt2Clip == null) uiClickAlt2Clip = LoadClip(SoundEffectId.UiClickAlt2);
            if (uiToggleClip == null) uiToggleClip = LoadClip(SoundEffectId.UiToggle);
            if (uiErrorClip == null) uiErrorClip = LoadClip(SoundEffectId.UiError);
            if (astraNotificationClip == null) astraNotificationClip = LoadClip(SoundEffectId.AstraNotification);

            if (moduleInstallClip == null) moduleInstallClip = LoadClip(SoundEffectId.ModuleInstall);
            if (moduleRemoveClip == null) moduleRemoveClip = LoadClip(SoundEffectId.ModuleRemove);
            if (researchUnlockClip == null) researchUnlockClip = LoadClip(SoundEffectId.ResearchUnlock);

            if (missionStartClip == null) missionStartClip = LoadClip(SoundEffectId.MissionStart);
            if (rocketIgnitionClip == null) rocketIgnitionClip = LoadClip(SoundEffectId.RocketIgnition);
            if (dryRunCompleteClip == null) dryRunCompleteClip = LoadClip(SoundEffectId.DryRunComplete);
            if (missionSuccessClip == null) missionSuccessClip = LoadClip(SoundEffectId.MissionSuccess);
            if (missionFailedClip == null) missionFailedClip = LoadClip(SoundEffectId.MissionFailed);

            if (commandExecutedClip == null) commandExecutedClip = LoadClip(SoundEffectId.CommandExecuted);
            if (safeModeOnClip == null) safeModeOnClip = LoadClip(SoundEffectId.SafeModeOn);
            if (safeModeOffClip == null) safeModeOffClip = LoadClip(SoundEffectId.SafeModeOff);
            if (commsRestoredClip == null) commsRestoredClip = LoadClip(SoundEffectId.CommsRestored);
            if (dataDownlinkClip == null) dataDownlinkClip = LoadClip(SoundEffectId.DataDownlink);
            if (micrometeoroidEventClip == null) micrometeoroidEventClip = LoadClip(SoundEffectId.MicrometeoroidEvent);
            if (solarStormAlarmClip == null) solarStormAlarmClip = LoadClip(SoundEffectId.SolarStormAlarm);
            if (systemWarningClip == null) systemWarningClip = LoadClip(SoundEffectId.SystemWarning);
#endif
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoAssignClipsInEditorIfMissing();
        }

        private void OnValidate()
        {
            AutoAssignClipsInEditorIfMissing();
        }

        [MenuItem("AstraLink/Audio/Auto-Assign Clips from _Project Audio")]
        private static void MenuAutoAssignAllClips()
        {
            AudioManager manager = Instance;
            manager.AutoAssignClipsInEditorIfMissing();
            EditorUtility.SetDirty(manager);
        }

        private static AudioClip LoadClip(SoundEffectId soundId)
        {
            string path = AudioCatalog.GetSfxAssetPath(soundId);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static AudioClip LoadClip(MusicTrackId trackId)
        {
            string path = AudioCatalog.GetMusicAssetPath(trackId);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
#endif
    }
}
