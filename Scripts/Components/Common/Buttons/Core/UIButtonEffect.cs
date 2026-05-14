using System.Collections;
using Core.Attributes;
using Core.Data.Enums;
using Core.Managers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Components.Common.Buttons.Core
{
    public abstract class UIButtonEffect :
        MonoBehaviour,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public enum ButtonState
        {
            Normal,
            Hover,
            LeftPressed,
            RightPressed,
            Disabled
        }

        [Header("Method")]
        [SerializeField] protected AnimType animType;

        [Header("Target RectTransform")]
        [Tooltip("None 설정 시, 자기 자신의 Target Rect로 설정")]
        [SerializeField] protected RectTransform targetRectTransform = null!;

        [Header("Sounds")]
        [SerializeField] protected bool useHoverOnSound;
        [ShowIf("useHoverOnSound == true")]
        [SerializeField] protected UISoundType hoverOnSound;
        [ShowIf("useHoverOnSound == true")]
        [SerializeField] protected float hoverOnSoundVolume;

        [SerializeField] protected bool useHoverOffSound;
        [ShowIf("useHoverOffSound == true")]
        [SerializeField] protected UISoundType hoverOffSound;
        [ShowIf("useHoverOffSound == true")]
        [SerializeField] protected float hoverOffSoundVolume;

        [SerializeField] protected bool useLeftPressSound;
        [ShowIf("useLeftPressSound == true")]
        [SerializeField] protected UISoundType leftPressSound;
        [ShowIf("useLeftPressSound == true")]
        [SerializeField] protected float leftPressSoundVolume;

        [SerializeField] protected bool useRightPressSound;
        [ShowIf("useRightPressSound == true")]
        [SerializeField] protected UISoundType rightPressSound;
        [ShowIf("useRightPressSound == true")]
        [SerializeField] protected float rightPressSoundVolume;

        [Header("State")]
        [SerializeField] protected ButtonState currentState = ButtonState.Normal;

        [Header("Interactable")]
        [SerializeField] protected bool isInteractable = true;

        [Header("Events")]
        public UnityEvent onLeftClickEvent;
        public UnityEvent onRightClickEvent;

        public UnityEvent onLeftPointerDownEvent;
        public UnityEvent onRightPointerDownEvent;

        public UnityEvent onLeftPointerUpEvent;
        public UnityEvent onRightPointerUpEvent;

        public UnityEvent onPointerEnterEvent;
        public UnityEvent onPointerExitEvent;

        private float _enableTime;

        protected Coroutine EffectCoroutine;

        protected bool IsHovering;
        protected bool IsLeftPressed;
        protected bool IsRightPressed;

        public bool IsInteractable
        {
            get => isInteractable;
            set
            {
                if (isInteractable == value) return;
                isInteractable = value;
                UpdateButtonState();
            }
        }

        protected virtual void Awake()
        {
            if (targetRectTransform == null)
            {
                targetRectTransform = GetComponent<RectTransform>();
                if (targetRectTransform == null)
                    Debug.LogError($"[{gameObject.name}] {GetType().Name}에 타겟 RectTransform이 존재하지 않음.", gameObject);
            }
        }

        protected virtual void OnEnable()
        {
            _enableTime = Time.unscaledTime;
            UpdateButtonState();
        }

        protected virtual void OnDisable()
        {
            IsHovering = false;
            IsLeftPressed = false;
            IsRightPressed = false;
            currentState = ButtonState.Normal;
            Stop();
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                onLeftClickEvent?.Invoke();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                onRightClickEvent?.Invoke();
            }
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (!isInteractable) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                IsLeftPressed = true;

                if (useLeftPressSound && SoundManager.Instance != null && CanPlaySound())
                    SoundManager.Instance.PlaySfx(leftPressSound, leftPressSoundVolume);

                onLeftPointerDownEvent?.Invoke();
                UpdateButtonState();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                IsRightPressed = true;

                if (useRightPressSound && SoundManager.Instance != null && CanPlaySound())
                    SoundManager.Instance.PlaySfx(rightPressSound, rightPressSoundVolume);

                onRightPointerDownEvent?.Invoke();

                UpdateButtonState();
            }
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractable) return;
            IsHovering = true;

            if (useHoverOnSound && SoundManager.Instance != null && !IsLeftPressed && !IsRightPressed && CanPlaySound())
                SoundManager.Instance.PlaySfx(hoverOnSound, hoverOnSoundVolume);

            onPointerEnterEvent?.Invoke();
            UpdateButtonState();
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            if (!isInteractable) return;
            IsHovering = false;

            if (useHoverOffSound && SoundManager.Instance != null && !IsLeftPressed && !IsRightPressed &&
                CanPlaySound())
                SoundManager.Instance.PlaySfx(hoverOffSound, hoverOffSoundVolume);

            onPointerExitEvent?.Invoke();
            UpdateButtonState();
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (!isInteractable) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                IsLeftPressed = false;
                onLeftPointerUpEvent?.Invoke();
                UpdateButtonState();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                IsRightPressed = false;
                onRightPointerUpEvent?.Invoke();
                UpdateButtonState();
            }
        }

        protected bool CanPlaySound()
        {
            return Time.unscaledTime - _enableTime > 0.15f;
        }

        public virtual void Play()
        {
            if (!gameObject.activeInHierarchy) return;

            if (EffectCoroutine != null)
            {
                Stop(false);
                EffectCoroutine = null;
            }

            EffectCoroutine = StartCoroutine(ExecuteEffect());
        }

        public virtual void Stop(bool snapToEnd = true)
        {
            if (EffectCoroutine != null)
            {
                StopCoroutine(EffectCoroutine);
                EffectCoroutine = null;
            }
        }

        protected abstract IEnumerator ExecuteEffect();

        public ButtonState GetButtonState()
        {
            return currentState;
        }

        protected void UpdateButtonState()
        {
            ButtonState newState;

            if (!IsInteractable)
            {
                newState = ButtonState.Disabled;
                IsHovering = false;
                IsLeftPressed = false;
                IsRightPressed = false;
            }
            else if (IsLeftPressed)
            {
                newState = IsHovering ? ButtonState.LeftPressed : ButtonState.Normal;
            }
            else if (IsRightPressed)
            {
                newState = IsHovering ? ButtonState.RightPressed : ButtonState.Normal;
            }
            else
            {
                newState = IsHovering ? ButtonState.Hover : ButtonState.Normal;
            }

            if (currentState != newState)
            {
                currentState = newState;
                Play();
            }
        }

        // 외부에서 이벤트를 통해 상태를 제어할 수 있는 래퍼 메서드
        public void SetInteractable(bool value)
        {
            IsInteractable = value;
        }

        protected enum AnimType
        {
            Normal,
            DoTween
        }
    }
}