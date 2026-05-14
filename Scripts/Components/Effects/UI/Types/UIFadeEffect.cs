using System;
using System.Collections;
using Components.Effects.UI.Core;
using Core.Attributes;
using Core.Data.Enums;
using Core.Extensions;
using Core.Managers;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Components.Effects.UI.Types
{
    [Serializable]
    public struct FadeConfig
    {
        public float endAlpha;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    [RequireComponent(typeof(Graphic))]
    public class UIFadeEffect : UIConfigurableEffect<FadeConfig>
    {
        [Header("Target Components")]
        [SerializeField] private Graphic targetGraphic;

        [Header("Config")]
        [SerializeField] private FadeConfig defaultConfig;

        private float _endAlpha;

        private Tween _fadeTween;

        private FadeConfig? _overrideConfig;

        public FadeConfig DefaultConfig => defaultConfig;

        protected override void Awake()
        {
            base.Awake();
            if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
            _endAlpha = defaultConfig.endAlpha;
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(FadeConfig configData, float? customDuration = null)
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

            _endAlpha = configToUse.endAlpha;

            if (configToUse.useSound && SoundManager.Instance != null)
            {
                var finalVolume = configToUse.volume <= 0f ? 1.0f : configToUse.volume;
                PlaySound(configToUse.soundType, finalVolume);
            }

            yield return animType switch
            {
                AnimType.Normal => FadeRoutine(configToUse, durationToUse),
                AnimType.DoTween => FadeRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerator FadeRoutine(FadeConfig configToUse, float durationToUse)
        {
            var startAlpha = targetGraphic.color.a;
            targetGraphic.raycastTarget = configToUse.endAlpha > 0f;

            var elapsed = 0f;

            while (elapsed < durationToUse)
            {
                elapsed += Time.deltaTime;

                var progress = elapsed / durationToUse;
                var currentAlpha = Mathf.Lerp(startAlpha, configToUse.endAlpha, progress);
                targetGraphic.SetAlpha(currentAlpha);
                yield return null;
            }

            targetGraphic.SetAlpha(configToUse.endAlpha);
            targetGraphic.raycastTarget = configToUse.endAlpha > 0f;
        }

        private IEnumerator FadeRoutineDoTween(FadeConfig configToUse, float durationToUse)
        {
            targetGraphic.raycastTarget = configToUse.endAlpha > 0f;
            _fadeTween?.Kill();
            _fadeTween = targetGraphic.DOFade(configToUse.endAlpha, durationToUse).SetEase(Ease.OutCubic);
            yield return _fadeTween.WaitForCompletion();
            targetGraphic.raycastTarget = configToUse.endAlpha > 0f;
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_fadeTween != null && _fadeTween.IsActive()) _fadeTween.Kill();

            if (snapToEnd && targetGraphic != null)
            {
                targetGraphic.SetAlpha(_endAlpha);
                targetGraphic.raycastTarget = _endAlpha > 0f;
            }
        }
    }
}