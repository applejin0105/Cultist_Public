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
    public struct CameraShakeConfig
    {
        public Vector3 power;
        public int frequency;
        public float randomness;

        [Header("Effect Sound")]
        public bool useSound;
        [ShowIf("useSound")] public UISoundType soundType;
        [ShowIf("useSound")] public float volume;
    }

    public class CameraShakeEffect : CameraConfigurableEffect<CameraShakeConfig>
    {
        [Header("Config")]
        [SerializeField] private CameraShakeConfig defaultConfig;
        public CameraShakeConfig DefaultConfig => defaultConfig;

        private Vector3 _originPos;
        private CameraShakeConfig? _overrideConfig;
        private Tween _shakeTween;
        private bool _isShaking;

        public override void ClearProperty() => _overrideConfig = null;

        public override void SetProperty(CameraShakeConfig configData, float? customDuration = null)
        {
            _overrideConfig = configData;
            if (customDuration.HasValue) OverrideDuration = customDuration.Value;
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

        protected override IEnumerator ExecuteEffect()
        {
            float durationToUse = ActualDuration;
            OverrideDuration = null;
            CameraShakeConfig configToUse = _overrideConfig ?? defaultConfig;

            if (!_isShaking) _originPos = TargetTransform.localPosition;
            _isShaking = true;

            if (configToUse.useSound && SoundManager.Instance != null)
            {
                float finalVol = configToUse.volume <= 0f ? 1.0f : configToUse.volume;
                SoundManager.Instance.PlaySfx(configToUse.soundType, finalVol);
            }

            yield return animType switch
            {
                AnimType.Normal => ShakeRoutine(configToUse, durationToUse),
                AnimType.DoTween => ShakeRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (_isShaking && TargetTransform != null)
            {
                TargetTransform.localPosition = _originPos;
                _isShaking = false;
            }
        }

        private IEnumerator ShakeRoutine(CameraShakeConfig configToUse, float durationToUse)
        {
            float elapsed = 0f;
            while (elapsed < durationToUse)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / durationToUse;
                float damper = 1f - Mathf.Clamp01(progress);

                // 단순 사인파로 구현 (필요시 Perlin Noise 적용 가능)
                float x = Mathf.Sin(elapsed * configToUse.frequency * Mathf.PI * 2f) * configToUse.power.x * damper;
                float y = Mathf.Cos(elapsed * configToUse.frequency * Mathf.PI * 2f) * configToUse.power.y * damper;
                float z = Mathf.Sin(elapsed * configToUse.frequency * Mathf.PI * 3f) * configToUse.power.z * damper;

                TargetTransform.localPosition = _originPos + new Vector3(x, y, z);
                yield return null;
            }

            TargetTransform.localPosition = _originPos;
        }

        private IEnumerator ShakeRoutineDoTween(CameraShakeConfig configToUse, float durationToUse)
        {
            _shakeTween?.Kill();
            _shakeTween = TargetTransform.DOShakePosition(
                duration: durationToUse,
                strength: configToUse.power,
                vibrato: configToUse.frequency,
                randomness: configToUse.randomness,
                snapping: false,
                fadeOut: true
            );
            yield return _shakeTween.WaitForCompletion();
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);
            if (_shakeTween != null && _shakeTween.IsActive()) _shakeTween.Kill();

            if (_isShaking && snapToEnd && TargetTransform != null)
            {
                TargetTransform.localPosition = _originPos;
            }

            _isShaking = false;
        }
    }
}