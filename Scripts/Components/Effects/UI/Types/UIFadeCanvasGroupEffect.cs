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
    public struct FadeCanvasGroupConfig
    {
        public float endAlpha;

        [Header("Raycast Settings")]
        public bool blockRaycasts;

        [Tooltip("최종 알파값이 이 수치 미만이면 강제로 blockRaycasts를 해제(false).")]
        public float disableRaycastAlphaThreshold;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    [RequireComponent(typeof(CanvasGroup))]
    public class UIFadeCanvasGroupEffect : UIConfigurableEffect<FadeCanvasGroupConfig>
    {
        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            _endAlpha = defaultConfig.endAlpha;
            _blockRaycasts = defaultConfig.blockRaycasts;
            _disableRaycastAlphaThreshold = defaultConfig.disableRaycastAlphaThreshold;
        }

        #endregion

        #region Fields & Properties

        [Header("Target Components")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Config")]
        [SerializeField] private FadeCanvasGroupConfig defaultConfig;

        public FadeCanvasGroupConfig DefaultConfig => defaultConfig;

        private bool _blockRaycasts;
        private float _endAlpha;
        private float _disableRaycastAlphaThreshold;
        private FadeCanvasGroupConfig? _overrideConfig;
        private Tween _fadeCanvasTween;

        #endregion

        #region Public API (Config & Control)

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(FadeCanvasGroupConfig configData, float? customDuration = null)
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

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_fadeCanvasTween != null && _fadeCanvasTween.IsActive())
                _fadeCanvasTween.Kill();

            if (snapToEnd && canvasGroup != null)
            {
                canvasGroup.alpha = _endAlpha;

                var configToApply = _overrideConfig ?? defaultConfig;
                ApplyFinalRaycastState(configToApply);
            }
        }

        #endregion

        #region Core Effect Routines

        protected override IEnumerator ExecuteEffect()
        {
            var durationToUse = ActualDuration;
            OverrideDuration = null;
            var configToUse = _overrideConfig ?? defaultConfig;

            _endAlpha = configToUse.endAlpha;
            _blockRaycasts = configToUse.blockRaycasts;
            _disableRaycastAlphaThreshold = configToUse.disableRaycastAlphaThreshold;

            if (configToUse.useSound && SoundManager.Instance != null)
            {
                var finalVolume = configToUse.volume <= 0f ? 1.0f : configToUse.volume;
                PlaySound(configToUse.soundType, finalVolume);
            }

            ApplyInitialRaycastState(configToUse);

            yield return animType switch
            {
                AnimType.Normal => FadeRoutine(configToUse, durationToUse),
                AnimType.DoTween => FadeRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerator FadeRoutine(FadeCanvasGroupConfig configToUse, float durationToUse)
        {
            var startAlpha = canvasGroup.alpha;
            var elapsed = 0f;

            while (elapsed < durationToUse)
            {
                elapsed += Time.deltaTime;

                var progress = elapsed / durationToUse;
                var currentAlpha = Mathf.Lerp(startAlpha, configToUse.endAlpha, progress);
                canvasGroup.alpha = currentAlpha;
                yield return null;
            }

            canvasGroup.alpha = configToUse.endAlpha;
            ApplyFinalRaycastState(configToUse);
        }

        private IEnumerator FadeRoutineDoTween(FadeCanvasGroupConfig configToUse, float durationToUse)
        {
            _fadeCanvasTween?.Kill();
            _fadeCanvasTween = canvasGroup.DOFade(configToUse.endAlpha, durationToUse).SetEase(Ease.OutCubic);

            yield return _fadeCanvasTween.WaitForCompletion();

            ApplyFinalRaycastState(configToUse);
        }

        #endregion

        #region Helper Methods (Raycast & Sound)

        private void ApplyInitialRaycastState(FadeCanvasGroupConfig config)
        {
            if (canvasGroup == null) return;

            if (config.endAlpha < config.disableRaycastAlphaThreshold)
                canvasGroup.blocksRaycasts = false;
            else if (config.blockRaycasts) canvasGroup.blocksRaycasts = true;
        }

        private void ApplyFinalRaycastState(FadeCanvasGroupConfig config)
        {
            if (canvasGroup == null) return;

            if (config.endAlpha < config.disableRaycastAlphaThreshold)
                canvasGroup.blocksRaycasts = false;
            else
                canvasGroup.blocksRaycasts = config.blockRaycasts;
        }

        public override void PlaySound(UISoundType soundType, float volume = 0.6f)
        {
            SoundManager.Instance.PlaySfx(soundType, volume);
        }

        public override void StopSound()
        {
            SoundManager.Instance.StopSfx();
        }

        #endregion
    }
}