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
    public enum UIBounceEffectType
    {
        Overshoot = 0,
        Jelly = 1
    }

    [Serializable]
    public struct BounceConfig
    {
        public Vector3 startScale;
        public Vector3 endScale;
        public UIBounceEffectType bounceEffectType;
        [ShowIf("bounceEffectType == Jelly")]
        public float frequency;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    public class UIBounceEffect : UIConfigurableEffect<BounceConfig>
    {
        [Header("Config")]
        [SerializeField] private BounceConfig defaultConfig;

        private readonly Func<float, float, float> _jellyCalc = (f, progress) =>
            1 - Mathf.Cos(progress * Mathf.PI * f) * (1 - progress);

        private readonly Func<float, float, float> _overshootCalc = (_, progress) =>
        {
            var pMinus1 = progress - 1f;
            return 1f + 2.70158f * (pMinus1 * pMinus1 * pMinus1) + 1.70158f * (pMinus1 * pMinus1);
        };

        private Tween _bounceTween;

        private Vector3 _endScale;

        private BounceConfig? _overrideConfig;

        public BounceConfig DefaultConfig => defaultConfig;

        protected override void Awake()
        {
            base.Awake();
            _endScale = defaultConfig.endScale;
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(BounceConfig configData, float? customDuration = null)
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

            _endScale = configToUse.endScale;

            if (configToUse.useSound && SoundManager.Instance != null)
            {
                var finalVolume = configToUse.volume <= 0f ? 1.0f : configToUse.volume;
                PlaySound(configToUse.soundType, finalVolume);
            }

            yield return animType switch
            {
                AnimType.Normal => BounceRoutine(configToUse, durationToUse),
                AnimType.DoTween => BounceRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerator BounceRoutine(BounceConfig configToUse, float durationToUse)
        {
            var currentCalc = configToUse.bounceEffectType switch
            {
                UIBounceEffectType.Overshoot => _overshootCalc,
                UIBounceEffectType.Jelly => _jellyCalc,
                _ => throw new ArgumentOutOfRangeException()
            };

            var originalScale = configToUse.startScale;

            targetRectTransform.localScale = originalScale;

            var elapsed = 0f;

            while (elapsed < durationToUse)
            {
                elapsed += Time.deltaTime;

                var progress = elapsed / durationToUse;

                var scale = currentCalc(configToUse.frequency, progress);

                targetRectTransform.localScale = Vector3.LerpUnclamped(originalScale, configToUse.endScale, scale);

                yield return null;
            }

            targetRectTransform.localScale = configToUse.endScale;
        }

        private IEnumerator BounceRoutineDoTween(BounceConfig configToUse, float durationToUse)
        {
            _bounceTween?.Kill();
            targetRectTransform.localScale = configToUse.startScale;

            if (configToUse.bounceEffectType == UIBounceEffectType.Overshoot)
            {
                _bounceTween = targetRectTransform.DOScale(configToUse.endScale, durationToUse)
                    .SetEase(Ease.OutBack);
                yield return _bounceTween.WaitForCompletion();
            }
            else if (configToUse.bounceEffectType == UIBounceEffectType.Jelly)
            {
                var period = configToUse.frequency > 0 ? 1f / configToUse.frequency : 0.3f;
                var amplitude = 1f;

                _bounceTween = targetRectTransform.DOScale(configToUse.endScale, durationToUse)
                    .SetEase(Ease.OutElastic, amplitude, period);
                yield return _bounceTween.WaitForCompletion();
            }
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_bounceTween != null && _bounceTween.IsActive()) _bounceTween.Kill();

            if (snapToEnd && targetRectTransform != null) targetRectTransform.localScale = _endScale;
        }
    }
}