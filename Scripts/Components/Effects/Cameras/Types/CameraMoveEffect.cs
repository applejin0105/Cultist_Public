using System;
using System.Collections;
using Components.Effects.Cameras.Core;
using Core.Attributes;
using Core.Data.Enums;
using Core.Managers;
using DG.Tweening;
using UnityEngine;

namespace Components.Effects.Cameras.Types
{
    [Serializable]
    public struct Range
    {
        public Vector3 start;
        public Vector3 end;
    }

    // FOV 조절을 위한 단일 float 범위 구조체 추가
    [Serializable]
    public struct FloatRange
    {
        public float start;
        public float end;
    }

    [Serializable]
    public struct CameraMoveConfig
    {
        [Header("Position")]
        public Range position;
        [Header("Rotation")]
        public Range rotation;
        [Header("Field Of View (Zoom)")]
        public FloatRange fov;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    public class CameraMoveEffect : CameraConfigurableEffect<CameraMoveConfig>
    {
        [Header("Config")]
        [SerializeField] private CameraMoveConfig defaultConfig;

        private Sequence _moveSequence;
        private CameraMoveConfig? _overrideConfig;

        private Vector3 _positionEnd;
        private Vector3 _rotationEnd;
        private float _fovEnd; // FOV 캐싱 변수

        public CameraMoveConfig DefaultConfig => defaultConfig;

        protected override void Awake()
        {
            base.Awake();

            if (targetCamera != null)
            {
                TargetTransform.localPosition = defaultConfig.position.start;
                TargetTransform.localRotation = Quaternion.Euler(defaultConfig.rotation.start);

                // 직교 카메라와 원근 카메라 자동 대응
                if (targetCamera.orthographic)
                    targetCamera.orthographicSize = defaultConfig.fov.start;
                else
                    targetCamera.fieldOfView = defaultConfig.fov.start;

                _positionEnd = defaultConfig.position.end;
                _rotationEnd = defaultConfig.rotation.end;
                _fovEnd = defaultConfig.fov.end;
            }
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(CameraMoveConfig configData, float? customDuration = null)
        {
            _overrideConfig = configData;

            if (customDuration.HasValue)
                OverrideDuration = customDuration.Value;
        }

        public override void PlayOverrideEffect()
        {
            StopCoroutine(nameof(ExecuteEffect));
            StartCoroutine(ExecuteEffect());
        }

        public override IEnumerator PlayWaitableEffect()
        {
            StopCoroutine(nameof(ExecuteEffect));
            yield return StartCoroutine(ExecuteEffect());
        }

        public void PlaySound(UISoundType soundType, float volume = 0.6f)
        {
            SoundManager.Instance.PlaySfx(soundType, volume);
        }

        public void StopSound()
        {
            SoundManager.Instance.StopSfx();
        }

        protected override IEnumerator ExecuteEffect()
        {
            var durationToUse = ActualDuration;
            OverrideDuration = null;
            var configToUse = _overrideConfig ?? defaultConfig;

            TargetTransform.localPosition = configToUse.position.start;
            TargetTransform.localRotation = Quaternion.Euler(configToUse.rotation.start);

            if (targetCamera.orthographic)
                targetCamera.orthographicSize = configToUse.fov.start;
            else
                targetCamera.fieldOfView = configToUse.fov.start;

            _positionEnd = configToUse.position.end;
            _rotationEnd = configToUse.rotation.end;
            _fovEnd = configToUse.fov.end;

            if (configToUse.useSound && SoundManager.Instance != null)
            {
                var finalVolume = configToUse.volume <= 0f ? 0.6f : configToUse.volume;
                PlaySound(configToUse.soundType, finalVolume);
            }

            yield return animType switch
            {
                AnimType.Normal => MoveRoutine(configToUse, durationToUse),
                AnimType.DoTween => MoveWithDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerator MoveRoutine(CameraMoveConfig configToUse, float durationToUse)
        {
            var elapsed = 0f;
            var startRot = Quaternion.Euler(configToUse.rotation.start);
            var endRot = Quaternion.Euler(configToUse.rotation.end);
            var isOrtho = targetCamera.orthographic;

            while (elapsed < durationToUse)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / durationToUse);
                var ease = 1f - Mathf.Pow(1f - progress, 3f);

                TargetTransform.localPosition =
                    Vector3.Lerp(configToUse.position.start, configToUse.position.end, ease);
                TargetTransform.localRotation = Quaternion.Lerp(startRot, endRot, ease);

                var currentFov = Mathf.Lerp(configToUse.fov.start, configToUse.fov.end, ease);
                if (isOrtho)
                    targetCamera.orthographicSize = currentFov;
                else
                    targetCamera.fieldOfView = currentFov;

                yield return null;
            }

            TargetTransform.localPosition = configToUse.position.end;
            TargetTransform.localRotation = Quaternion.Euler(configToUse.rotation.end);

            if (isOrtho)
                targetCamera.orthographicSize = configToUse.fov.end;
            else
                targetCamera.fieldOfView = configToUse.fov.end;
        }

        private IEnumerator MoveWithDoTween(CameraMoveConfig configToUse, float durationToUse)
        {
            _moveSequence?.Kill();
            _moveSequence = DOTween.Sequence();

            TargetTransform.localPosition = configToUse.position.start;
            TargetTransform.localRotation = Quaternion.Euler(configToUse.rotation.start);

            var isOrtho = targetCamera.orthographic;
            if (isOrtho)
                targetCamera.orthographicSize = configToUse.fov.start;
            else
                targetCamera.fieldOfView = configToUse.fov.start;

            _moveSequence.Join(
                TargetTransform.DOLocalMove(configToUse.position.end, durationToUse).SetEase(Ease.OutCubic)
            );

            _moveSequence.Join(
                TargetTransform.DOLocalRotate(configToUse.rotation.end, durationToUse).SetEase(Ease.OutCubic)
            );

            if (isOrtho)
            {
                _moveSequence.Join(
                    targetCamera.DOOrthoSize(configToUse.fov.end, durationToUse).SetEase(Ease.OutCubic)
                );
            }
            else
            {
                _moveSequence.Join(
                    targetCamera.DOFieldOfView(configToUse.fov.end, durationToUse).SetEase(Ease.OutCubic)
                );
            }

            yield return _moveSequence.WaitForCompletion();

            TargetTransform.localPosition = configToUse.position.end;
            TargetTransform.localRotation = Quaternion.Euler(configToUse.rotation.end);

            if (isOrtho)
                targetCamera.orthographicSize = configToUse.fov.end;
            else
                targetCamera.fieldOfView = configToUse.fov.end;
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_moveSequence != null && _moveSequence.IsActive()) _moveSequence.Kill();

            if (snapToEnd && targetCamera != null)
            {
                TargetTransform.localPosition = _positionEnd;
                TargetTransform.localRotation = Quaternion.Euler(_rotationEnd);

                if (targetCamera.orthographic)
                    targetCamera.orthographicSize = _fovEnd;
                else
                    targetCamera.fieldOfView = _fovEnd;
            }
        }
    }
}