using System;
using System.Collections;
using Components.Effects.UI.Core;
using Core.Attributes;
using Core.Data.Enums;
using Core.Managers;
using DG.Tweening;
using UnityEngine;

namespace Components.Effects.UI.Types
{
    [Serializable]
    public struct Range
    {
        public Vector3 start;
        public Vector3 end;
    }

    [Serializable]
    public struct MoveConfig
    {
        [Header("Position")]
        public Range position;
        [Header("Rotation")]
        public Range rotation;
        [Header("Scale")]
        public Range scale;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    public class UIMoveEffect : UIConfigurableEffect<MoveConfig>
    {
        [Header("Config")]
        [SerializeField] private MoveConfig defaultConfig;

        private Sequence _moveSequence;

        private MoveConfig? _overrideConfig;

        private Vector3 _positionEnd;
        private Vector3 _rotationEnd;
        private Vector3 _scaleEnd;

        // 인스펙터 값에서 안전하게 값 복사를 위한 장치.
        // 이게 없으면 할때마다 모든 scale, rotation 값을 config로 명시해줘야함.
        // 이게 있으면, 인스펙터에서 안전하게 값 복사 가능
        public MoveConfig DefaultConfig => defaultConfig;

        protected override void Awake()
        {
            base.Awake();

            if (targetRectTransform != null)
            {
                targetRectTransform.anchoredPosition = defaultConfig.position.start;
                targetRectTransform.localRotation = Quaternion.Euler(defaultConfig.rotation.start);
                targetRectTransform.localScale = defaultConfig.scale.start;

                _positionEnd = defaultConfig.position.end;
                _rotationEnd = defaultConfig.rotation.end;
                _scaleEnd = defaultConfig.scale.end;
            }
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(MoveConfig configData, float? customDuration = null)
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

        public override void PlaySound(UISoundType soundType, float volume = 0.6f)
        {
            SoundManager.Instance.PlaySfx(soundType, volume);
        }

        public override void StopSound()
        {
            SoundManager.Instance.StopSfx();
        }

        protected override IEnumerator ExecuteEffect()
        {
            var durationToUse = ActualDuration;

            OverrideDuration = null;

            var configToUse = _overrideConfig ?? defaultConfig;

            targetRectTransform.anchoredPosition = configToUse.position.start;
            targetRectTransform.localRotation = Quaternion.Euler(configToUse.rotation.start);
            targetRectTransform.localScale = configToUse.scale.start;

            _positionEnd = configToUse.position.end;
            _rotationEnd = configToUse.rotation.end;
            _scaleEnd = configToUse.scale.end;

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

        // Linear 보간: 시간에 대해 속도가 일정한 보간 방식. 즉, 시간에 대해 선형 증가하는 easing 방식
        // Lerp 보간: 선형 보간 함수

        // 단일 오브젝트의 경우 수동 방식이 약간 더 가볍고, DOTween이 약간 오버헤드가 있지만
        // 수십~수백 개 동시 실행의 경우 DOTween은 중앙 Tween Manager가 일괄 관리하므로 성능상 이점이 있음
        // 따라서 개별 GC 관리가 필요함.

        private IEnumerator MoveRoutine(MoveConfig configToUse, float durationToUse)
        {
            targetRectTransform.anchoredPosition = configToUse.position.start;
            targetRectTransform.localRotation = Quaternion.Euler(configToUse.rotation.start);
            targetRectTransform.localScale = configToUse.scale.start;

            var elapsed = 0f;

            var startRot = Quaternion.Euler(configToUse.rotation.start);
            var endRot = Quaternion.Euler(configToUse.rotation.end);

            while (elapsed < durationToUse)
            {
                elapsed += Time.deltaTime;

                var progress = Mathf.Clamp01(elapsed / durationToUse);

                var ease = 1f - Mathf.Pow(1f - progress, 3f);

                targetRectTransform.anchoredPosition = Vector3.Lerp(configToUse.position.start,
                    configToUse.position.end,
                    ease);

                // 짐벌락 방지 (주석은 기존코드, 짐벌랑 발생 가능)
                /*
                 * targetRectTransform.localRotation = Quaternion.Euler(Vector3.Lerp(configToUse.rotation.start,
                 *  configToUse.rotation.end,
                 *  ease));
                 */
                targetRectTransform.localRotation = Quaternion.Lerp(startRot, endRot, ease);
                targetRectTransform.localScale =
                    Vector3.Lerp(configToUse.scale.start, configToUse.scale.end, ease);

                yield return null;
            }

            targetRectTransform.anchoredPosition = configToUse.position.end;
            targetRectTransform.localRotation = Quaternion.Euler(configToUse.rotation.end);
            targetRectTransform.localScale = configToUse.scale.end;
        }

        private IEnumerator MoveWithDoTween(MoveConfig configToUse, float durationToUse)
        {
            _moveSequence?.Kill();

            _moveSequence = DOTween.Sequence();

            targetRectTransform.anchoredPosition = configToUse.position.start;
            targetRectTransform.localRotation = Quaternion.Euler(configToUse.rotation.start);
            targetRectTransform.localScale = configToUse.scale.start;

            _moveSequence.Join(
                targetRectTransform.DOAnchorPos3D(configToUse.position.end, durationToUse).SetEase(Ease.OutCubic)
            );

            _moveSequence.Join(
                targetRectTransform.DOLocalRotate(configToUse.rotation.end, durationToUse)
                    .SetEase(Ease.OutCubic)
            );

            _moveSequence.Join(
                targetRectTransform.DOScale(configToUse.scale.end, durationToUse)
                    .SetEase(Ease.OutCubic)
            );

            yield return _moveSequence.WaitForCompletion();

            targetRectTransform.anchoredPosition = configToUse.position.end;
            targetRectTransform.localRotation = Quaternion.Euler(configToUse.rotation.end);
            targetRectTransform.localScale = configToUse.scale.end;
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_moveSequence != null && _moveSequence.IsActive()) _moveSequence.Kill();

            if (snapToEnd && targetRectTransform != null)
            {
                targetRectTransform.anchoredPosition = _positionEnd;
                targetRectTransform.localRotation = Quaternion.Euler(_rotationEnd);
                targetRectTransform.localScale = _scaleEnd;
            }
        }
    }
}