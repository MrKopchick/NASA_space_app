using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstraLink.Core
{
    /// <summary>
    /// Attaches click, value-change, and pointer-hover audio feedback to any interactive UI control using <see cref="AudioManager"/> (Task S3-02).
    /// </summary>
    public class AudioTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("Click / Action Sound")]
        [SerializeField] private SoundEffectId soundId = SoundEffectId.UiClick;
        [SerializeField] private AudioClip customClip;

        [Header("Hover Sound")]
        [SerializeField] private bool playHoverSound = true;
        [SerializeField] private SoundEffectId hoverSoundId = SoundEffectId.UiClickAlt2;
        [SerializeField] private AudioClip customHoverClip;

        private Selectable _selectable;
        private Button _button;
        private Toggle _toggle;
        private TMP_Dropdown _tmpDropdown;
        private Dropdown _legacyDropdown;
        private Slider _slider;
        private bool _isBound;
        private float _lastSliderSoundTime;
        private float _lastHoverSoundTime;
        private float _lastClickSoundTime;

        /// <summary>
        /// Gets or sets the <see cref="SoundEffectId"/> played when this UI element is activated.
        /// </summary>
        public SoundEffectId SoundId
        {
            get { return soundId; }
            set { soundId = value; }
        }

        /// <summary>
        /// Gets or sets an optional custom <see cref="AudioClip"/> that overrides <see cref="SoundId"/>.
        /// </summary>
        public AudioClip CustomClip
        {
            get { return customClip; }
            set { customClip = value; }
        }

        /// <summary>
        /// Gets or sets whether a subtle hover sound plays when the pointer enters this control.
        /// </summary>
        public bool PlayHoverSound
        {
            get { return playHoverSound; }
            set { playHoverSound = value; }
        }

        /// <summary>
        /// Gets or sets the <see cref="SoundEffectId"/> played when hovering over this control.
        /// </summary>
        public SoundEffectId HoverSoundId
        {
            get { return hoverSoundId; }
            set { hoverSoundId = value; }
        }

        /// <summary>
        /// Attaches an <see cref="AudioTrigger"/> to the target GameObject and assigns its <see cref="SoundEffectId"/>.
        /// </summary>
        public static AudioTrigger Attach(GameObject target, SoundEffectId effectId = SoundEffectId.UiClick, bool enableHover = true)
        {
            if (target == null)
            {
                return null;
            }

            AudioTrigger trigger = target.GetComponent<AudioTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<AudioTrigger>();
            }

            trigger.soundId = effectId;
            trigger.customClip = null;
            trigger.playHoverSound = enableHover;
            trigger.BindControl();
            return trigger;
        }

        /// <summary>
        /// Attaches an <see cref="AudioTrigger"/> to the target GameObject and assigns a custom <see cref="AudioClip"/>.
        /// </summary>
        public static AudioTrigger Attach(GameObject target, AudioClip clip, bool enableHover = true)
        {
            if (target == null)
            {
                return null;
            }

            AudioTrigger trigger = target.GetComponent<AudioTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<AudioTrigger>();
            }

            trigger.customClip = clip;
            trigger.playHoverSound = enableHover;
            trigger.BindControl();
            return trigger;
        }

        private void Awake()
        {
            BindControl();
        }

        /// <summary>
        /// Plays a subtle hover sound when the pointer enters an interactable control.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!playHoverSound || !AudioCatalog.IsSfxEnabled || !AudioCatalog.IsHoverEnabled)
            {
                return;
            }

            if (_selectable == null)
            {
                _selectable = GetComponent<Selectable>();
            }

            if (_selectable != null && !_selectable.IsInteractable())
            {
                return;
            }

            if (Time.unscaledTime - _lastHoverSoundTime < 0.08f)
            {
                return;
            }

            _lastHoverSoundTime = Time.unscaledTime;

            if (customHoverClip != null)
            {
                AudioManager.Instance.PlayCustomSfx(customHoverClip, AudioCatalog.HoverVolumeScale);
            }
            else if (hoverSoundId != SoundEffectId.None)
            {
                AudioManager.Instance.PlaySfx(hoverSoundId, AudioCatalog.HoverVolumeScale);
            }
        }

        /// <summary>
        /// Plays the click sound on pointer click so Button.onClick.RemoveAllListeners() cannot detach audio.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button == null)
            {
                return;
            }

            if (_selectable == null)
            {
                _selectable = _button;
            }

            if (_selectable != null && !_selectable.IsInteractable())
            {
                return;
            }

            PlayTriggerSound();
        }

        private void BindControl()
        {
            if (_isBound)
            {
                return;
            }

            _selectable = GetComponent<Selectable>();

            _button = GetComponent<Button>();
            if (_button != null)
            {
                _isBound = true;
                return;
            }

            _toggle = GetComponent<Toggle>();
            if (_toggle != null)
            {
                _toggle.onValueChanged.AddListener(OnToggleChanged);
                _isBound = true;
                return;
            }

            _tmpDropdown = GetComponent<TMP_Dropdown>();
            if (_tmpDropdown != null)
            {
                _tmpDropdown.onValueChanged.AddListener(OnDropdownChanged);
                _isBound = true;
                return;
            }

            _legacyDropdown = GetComponent<Dropdown>();
            if (_legacyDropdown != null)
            {
                _legacyDropdown.onValueChanged.AddListener(OnDropdownChanged);
                _isBound = true;
                return;
            }

            _slider = GetComponent<Slider>();
            if (_slider != null)
            {
                _slider.onValueChanged.AddListener(OnSliderChanged);
                _isBound = true;
            }
        }

        private void OnToggleChanged(bool isOn)
        {
            PlayTriggerSound();
        }

        private void OnDropdownChanged(int index)
        {
            PlayTriggerSound();
        }

        private void OnSliderChanged(float value)
        {
            if (Time.unscaledTime - _lastSliderSoundTime < 0.12f)
            {
                return;
            }

            _lastSliderSoundTime = Time.unscaledTime;
            PlayTriggerSound();
        }

        /// <summary>
        /// Plays the configured sound effect or custom clip through <see cref="AudioManager"/>.
        /// </summary>
        public void PlayTriggerSound()
        {
            if (!AudioCatalog.IsSfxEnabled)
            {
                return;
            }

            if (Time.unscaledTime - _lastClickSoundTime < 0.15f)
            {
                return;
            }

            _lastClickSoundTime = Time.unscaledTime;

            if (customClip != null)
            {
                AudioManager.Instance.PlayCustomSfx(customClip);
            }
            else
            {
                AudioManager.Instance.PlaySfx(soundId);
            }
        }
    }
}
