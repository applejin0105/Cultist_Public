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
    public struct FlickerConfig
    {
        public bool isLoop;

        public Color baseColor;

        public Color dimColor;

        public float flickerDuration;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    [RequireComponent(typeof(Graphic))]
    public class UIFlickerEffect : UIConfigurableEffect<FlickerConfig>
    {
        [Header("Target Components")]
        [SerializeField] private Graphic targetGraphic;

        [Header("Config")]
        [SerializeField] private FlickerConfig defaultConfig;

        private Color _baseColor;

        private Tween _flickerTween;

        private FlickerConfig? _overrideConfig;

        public FlickerConfig DefaultConfig => defaultConfig;

        protected override void Awake()
        {
            base.Awake();
            if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
            _baseColor = defaultConfig.baseColor;
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(FlickerConfig configData, float? customDuration = null)
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

            _baseColor = configToUse.baseColor;

            if (configToUse.useSound && SoundManager.Instance != null)
            {
                var finalVolume = configToUse.volume <= 0f ? 1.0f : configToUse.volume;
                PlaySound(configToUse.soundType, finalVolume);
            }

            yield return animType switch
            {
                AnimType.Normal => FlickerRoutine(configToUse, durationToUse),
                AnimType.DoTween => FlickerRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerator FlickerRoutine(FlickerConfig configToUse, float durationToUse)
        {
            var fullCycleTime = configToUse.flickerDuration * 2f;

            var calculatedCycles = Mathf.RoundToInt(durationToUse / fullCycleTime);
            if (calculatedCycles <= 0) calculatedCycles = 1;

            var exactTotalTime = calculatedCycles * fullCycleTime;

            var elapsed = 0f;

            targetGraphic.color = configToUse.baseColor;

            while (configToUse.isLoop || elapsed < exactTotalTime)
            {
                elapsed += Time.deltaTime;

                var t = Mathf.PingPong(elapsed, configToUse.flickerDuration) / configToUse.flickerDuration;
                targetGraphic.color = Color.Lerp(configToUse.baseColor, configToUse.dimColor, t);

                yield return null;
            }

            targetGraphic.color = _baseColor;
        }

        private IEnumerator FlickerRoutineDoTween(FlickerConfig configToUse, float durationToUse)
        {
            _flickerTween?.Kill();

            targetGraphic.color = configToUse.baseColor;

            var fullCycleTime = configToUse.flickerDuration * 2f;
            var calculatedCycles = Mathf.RoundToInt(durationToUse / fullCycleTime);
            if (calculatedCycles <= 0) calculatedCycles = 1;

            var finalLoops = configToUse.isLoop ? -1 : calculatedCycles * 2;

            _flickerTween = targetGraphic.DOColor(configToUse.dimColor, configToUse.flickerDuration)
                .SetLoops(finalLoops, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);

            yield return _flickerTween.WaitForCompletion();
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_flickerTween != null && _flickerTween.IsActive()) _flickerTween.Kill();

            if (snapToEnd && targetGraphic != null) targetGraphic.color = _baseColor;
        }
    }
}