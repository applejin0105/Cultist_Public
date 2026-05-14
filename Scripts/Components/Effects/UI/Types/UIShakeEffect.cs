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
    public struct ShakeConfig
    {
        public float power;
        public int frequency;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    [RequireComponent(typeof(RectTransform))]
    public class UIShakeEffect : UIConfigurableEffect<ShakeConfig>
    {
        [Header("Config")]
        [SerializeField] private ShakeConfig defaultConfig;

        private bool _isShaking;

        private Vector2 _originPos;

        private ShakeConfig? _overrideConfig;

        private Tween _shakeTween;

        public ShakeConfig DefaultConfig => defaultConfig;

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(ShakeConfig configData, float? customDuration = null)
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

            if (!_isShaking) _originPos = targetRectTransform.anchoredPosition;

            _isShaking = true;

            yield return null;

            if (configToUse.useSound && SoundManager.Instance != null)
            {
                var finalVolume = configToUse.volume <= 0f ? 1.0f : configToUse.volume;
                PlaySound(configToUse.soundType, finalVolume);
            }

            yield return animType switch
            {
                AnimType.Normal => ShakeRoutine(configToUse, durationToUse),
                AnimType.DoTween => ShakeRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (_isShaking && targetRectTransform != null)
            {
                targetRectTransform.anchoredPosition = _originPos;
                _isShaking = false;
            }
        }

        private IEnumerator ShakeRoutine(ShakeConfig configToUse, float durationToUse)
        {
            var elapsed = 0f;

            while (elapsed < durationToUse)
            {
                elapsed += Time.deltaTime;

                var progress = elapsed / durationToUse;
                var damper = 1f - Mathf.Clamp01(progress);

                var x = Mathf.Sin(elapsed * configToUse.frequency * Mathf.PI * 2f) * configToUse.power * damper;

                targetRectTransform.anchoredPosition = _originPos + new Vector2(x, 0);

                yield return null;
            }

            targetRectTransform.anchoredPosition = _originPos;
        }

        private IEnumerator ShakeRoutineDoTween(ShakeConfig configToUse, float durationToUse)
        {
            _shakeTween?.Kill();
            _shakeTween = targetRectTransform.DOShakeAnchorPos(
                durationToUse,
                new Vector2(configToUse.power, 0.0f),
                configToUse.frequency,
                0f,
                true
            );
            yield return _shakeTween.WaitForCompletion();
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_shakeTween != null && _shakeTween.IsActive()) _shakeTween.Kill();

            if (_isShaking && snapToEnd && targetRectTransform != null)
                targetRectTransform.anchoredPosition = _originPos;

            _isShaking = false;
        }
    }
}