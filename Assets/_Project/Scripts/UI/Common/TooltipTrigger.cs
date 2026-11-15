using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AstraLink.Core
{
    /// <summary>
    /// Pointer hover trigger that displays an English tooltip via <see cref="TooltipSystem"/> after a 0.5-second unscaled delay (Task S3-08).
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        public const float DefaultHoverDelaySeconds = 0.5f;

        [Header("Tooltip Configuration")]
        [SerializeField] private string tipKey = string.Empty;
        [SerializeField] private float hoverDelay = DefaultHoverDelaySeconds;

        private Func<string> _dynamicProvider;
        private bool _isPointerInside;
        private bool _isShowing;
        private float _hoverTimer;
        private string _lastShownText = string.Empty;

        /// <summary>
        /// Gets or sets the static dictionary lookup key used by <see cref="DescriptionTips"/>.
        /// </summary>
        public string TipKey
        {
            get { return tipKey; }
            set { tipKey = value ?? string.Empty; }
        }

        /// <summary>
        /// Attaches a <see cref="TooltipTrigger"/> component to the target GameObject and assigns its static lookup key.
        /// </summary>
        public static TooltipTrigger Attach(GameObject target, string key)
        {
            if (target == null)
            {
                return null;
            }

            TooltipTrigger trigger = target.GetComponent<TooltipTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<TooltipTrigger>();
            }

            trigger.tipKey = key ?? string.Empty;
            trigger._dynamicProvider = null;
            return trigger;
        }

        /// <summary>
        /// Attaches a <see cref="TooltipTrigger"/> component to the target GameObject and assigns a dynamic text provider callback.
        /// </summary>
        public static TooltipTrigger AttachDynamic(GameObject target, Func<string> provider)
        {
            if (target == null)
            {
                return null;
            }

            TooltipTrigger trigger = target.GetComponent<TooltipTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<TooltipTrigger>();
            }

            trigger._dynamicProvider = provider;
            return trigger;
        }

        /// <summary>
        /// Starts the 0.5-second unscaled hover timer when the cursor enters the UI element.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsTipsEnabledInSettings())
            {
                _isPointerInside = true;
                _isShowing = false;
                _hoverTimer = 0f;
            }
            else
            {
                CancelAndHide();
            }
        }

        /// <summary>
        /// Cancels the hover timer and hides the tooltip when the cursor leaves the UI element.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            CancelAndHide();
        }

        /// <summary>
        /// Immediately hides the tooltip when the user clicks or presses down on the UI element.
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            CancelAndHide();
        }

        private void OnDisable()
        {
            CancelAndHide();
        }

        private void Update()
        {
            if (!IsTipsEnabledInSettings())
            {
                if (_isPointerInside || _isShowing)
                {
                    CancelAndHide();
                }
                return;
            }

            if (!_isPointerInside) return;
            if (_isShowing)
            {
                // Refresh a dynamic slot/rocket tooltip while the pointer remains over its row.
                if (_dynamicProvider != null)
                {
                    string updated = ResolveTooltipText();
                    if (!string.Equals(updated, _lastShownText, StringComparison.Ordinal))
                    {
                        _lastShownText = updated;
                        if (!string.IsNullOrWhiteSpace(updated)) TooltipSystem.Instance.Show(updated);
                        else if (TooltipSystem.HasInstance) TooltipSystem.Instance.Hide();
                    }
                }
                return;
            }

            float requiredDelay = hoverDelay > 0f ? hoverDelay : DefaultHoverDelaySeconds;
            _hoverTimer += Time.unscaledDeltaTime;

            if (_hoverTimer >= requiredDelay)
            {
                _isShowing = true;
                string resolvedText = ResolveTooltipText();
                _lastShownText = resolvedText;
                if (!string.IsNullOrWhiteSpace(resolvedText))
                {
                    TooltipSystem.Instance.Show(resolvedText);
                }
            }
        }

        private static bool IsTipsEnabledInSettings()
        {
            if (!DescriptionTips.IsEnabled)
            {
                return false;
            }
            else if (TooltipSystem.HasInstance && !TooltipSystem.Instance.IsTipsEnabled)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private string ResolveTooltipText()
        {
            if (_dynamicProvider != null)
            {
                string dynamicText = _dynamicProvider.Invoke();
                if (!string.IsNullOrWhiteSpace(dynamicText))
                {
                    return dynamicText;
                }
            }

            if (!string.IsNullOrWhiteSpace(tipKey))
            {
                string fromKey = DescriptionTips.Get(tipKey, tipKey);
                if (!string.IsNullOrWhiteSpace(fromKey))
                {
                    return fromKey;
                }
            }

            return DescriptionTips.Get(gameObject.name, string.Empty);
        }

        private void CancelAndHide()
        {
            _isPointerInside = false;
            _hoverTimer = 0f;
            _lastShownText = string.Empty;

            if (_isShowing)
            {
                _isShowing = false;
                if (TooltipSystem.HasInstance)
                {
                    TooltipSystem.Instance.Hide();
                }
            }
        }
    }
}
