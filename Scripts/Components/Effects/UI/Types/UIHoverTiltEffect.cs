using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Components.Effects.UI.Types
{
    [Serializable]
    public struct HoverTiltConfig
    {
        [Header("X Axis Rotation")]
        public float maxAngleX;
        public float minAngleX;

        [Header("Y Axis Rotation")]
        public float maxAngleY;
        public float minAngleY;

        [Header("Settings")]
        public float dampingSpeed;
    }

    [RequireComponent(typeof(RectTransform))]
    public class UIHoverTiltEffect : MonoBehaviour,
        IPointerEnterHandler,
        IPointerMoveHandler,
        IPointerExitHandler
    {
        [Header("Target & Config")]
        [SerializeField] private RectTransform targetVisualRect;
        [SerializeField] private HoverTiltConfig defaultConfig;

        [Header("Safety Settings")]
        [SerializeField] private float initDelay = 0.2f;

        private RectTransform _hitAreaRect;
        private bool _isReady;
        private Quaternion _targetRotation = Quaternion.identity;
        private Coroutine _tiltCoroutine;

        private void Awake()
        {
            _hitAreaRect = GetComponent<RectTransform>();

            if (targetVisualRect == null) targetVisualRect = _hitAreaRect;
        }

        private void OnEnable()
        {
            ResetTilt();
            StartCoroutine(EnableAfterDelay());
        }

        private void OnDisable()
        {
            ResetTilt();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isReady) return;

            CalculateTargetRotation(eventData);
            StartTiltRoutine();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isReady) return;

            _targetRotation = Quaternion.identity;
            StartTiltRoutine();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!_isReady) return;

            CalculateTargetRotation(eventData);

            if (_tiltCoroutine == null) StartTiltRoutine();
        }

        private IEnumerator EnableAfterDelay()
        {
            // 씬 로드 후 UI Layout Group이 크기를 확정할 수 있도록 2프레임 대기
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(initDelay);
            _isReady = true;
        }

        private void CalculateTargetRotation(PointerEventData eventData)
        {
            if (_hitAreaRect.rect.width <= 0.001f || _hitAreaRect.rect.height <= 0.001f) return;

            // 호버링 상태의 카메라 정보를 가져오기 위해 enterEventCamera 사용
            var cam = eventData.enterEventCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _hitAreaRect,
                    eventData.position,
                    cam,
                    out var localCursor))
            {
                var xRatio = Mathf.InverseLerp(_hitAreaRect.rect.xMin, _hitAreaRect.rect.xMax, localCursor.x);
                var yRatio = Mathf.InverseLerp(_hitAreaRect.rect.yMin, _hitAreaRect.rect.yMax, localCursor.y);

                var targetRotY = Mathf.Lerp(defaultConfig.minAngleY, defaultConfig.maxAngleY, xRatio);
                var targetRotX = Mathf.Lerp(defaultConfig.maxAngleX, defaultConfig.minAngleX, yRatio);

                _targetRotation = Quaternion.Euler(targetRotX, targetRotY, 0f);
            }
        }

        private void StartTiltRoutine()
        {
            if (_tiltCoroutine != null) StopCoroutine(_tiltCoroutine);

            if (gameObject.activeInHierarchy) _tiltCoroutine = StartCoroutine(TiltRoutine());
        }

        private IEnumerator TiltRoutine()
        {
            while (Quaternion.Angle(targetVisualRect.localRotation, _targetRotation) > 0.01f)
            {
                targetVisualRect.localRotation = Quaternion.Slerp(
                    targetVisualRect.localRotation,
                    _targetRotation,
                    Time.unscaledDeltaTime * defaultConfig.dampingSpeed
                );
                yield return null;
            }

            targetVisualRect.localRotation = _targetRotation;
            _tiltCoroutine = null;
        }

        public void ResetTilt()
        {
            _targetRotation = Quaternion.identity;
            _isReady = false;

            if (targetVisualRect != null) targetVisualRect.localRotation = Quaternion.identity;

            if (_tiltCoroutine != null)
            {
                StopCoroutine(_tiltCoroutine);
                _tiltCoroutine = null;
            }
        }
    }
}