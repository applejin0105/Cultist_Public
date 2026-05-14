using System;
using System.Collections;
using Components.Effects.UI.Core;
using Core.Attributes;
using Core.Data.Enums;
using Core.Managers;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Components.Effects.UI.Types
{
    [Serializable]
    public struct FillConfig
    {
        public Image.FillMethod fillMethod;
        [DynamicFillOrigin("fillMethod")]
        public int fillOrigin;
        public float startFillAmount;
        public float endFillAmount;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    [RequireComponent(typeof(Image))]
    public class UIFillEffect : UIConfigurableEffect<FillConfig>
    {
        [Header("Target Components")]
        [SerializeField] private Image targetImage;

        [Header("Config")]
        [SerializeField] private FillConfig defaultConfig;

        private float _endFillAmount;

        private Tween _fillTween;

        private FillConfig? _overrideConfig;

        public FillConfig DefaultConfig => defaultConfig;

        protected override void Awake()
        {
            base.Awake();
            if (targetImage == null) targetImage = GetComponent<Image>();

            targetImage.type = Image.Type.Filled;
            targetImage.fillMethod = defaultConfig.fillMethod;
            targetImage.fillOrigin = defaultConfig.fillOrigin;
            _endFillAmount = defaultConfig.endFillAmount;
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(FillConfig configData, float? customDuration = null)
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

            _endFillAmount = configToUse.endFillAmount;

            targetImage.type = Image.Type.Filled;
            targetImage.fillMethod = configToUse.fillMethod;
            targetImage.fillOrigin = configToUse.fillOrigin;

            targetImage.fillAmount = configToUse.startFillAmount;

            if (configToUse.useSound && SoundManager.Instance != null)
            {
                var finalVolume = configToUse.volume <= 0f ? 1.0f : configToUse.volume;
                PlaySound(configToUse.soundType, finalVolume);
            }

            yield return animType switch
            {
                AnimType.Normal => FillRoutine(configToUse, durationToUse),
                AnimType.DoTween => FillRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerator FillRoutine(FillConfig configToUse, float durationToUse)
        {
            var elapsed = 0f;
            while (elapsed < durationToUse)
            {
                elapsed += Time.deltaTime;
                var progress = elapsed / durationToUse;
                targetImage.fillAmount =
                    Mathf.Lerp(configToUse.startFillAmount, configToUse.endFillAmount, progress);
                yield return null;
            }

            targetImage.fillAmount = configToUse.endFillAmount;
        }

        private IEnumerator FillRoutineDoTween(FillConfig configToUse, float durationToUse)
        {
            _fillTween?.Kill();

            _fillTween = targetImage.DOFillAmount(configToUse.endFillAmount, durationToUse).SetEase(Ease.Linear);
            yield return _fillTween.WaitForCompletion();
            targetImage.fillAmount = configToUse.endFillAmount;
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_fillTween != null && _fillTween.IsActive()) _fillTween.Kill();

            if (snapToEnd && targetImage != null) targetImage.fillAmount = _endFillAmount;
        }
    }
}