using System;
using System.Collections;
using Components.Effects.UI.Core;
using Core.Attributes;
using Core.Data.Enums;
using Core.Managers;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Components.Effects.UI.Types
{
    [Serializable]
    public struct PulseConfig
    {
        public bool isLoop;

        public float scaleMin;
        public float scaleMax;

        public float pulseDuration;

        [Header("First Effect Sound (Scale Up)")]
        public bool useFirstSound;

        [ShowIf("useFirstSound")]
        public UISoundType firstSoundType;

        [ShowIf("useFirstSound")]
        public float firstVolume;

        [Header("Second Effect Sound (Scale Down)")]
        public bool useSecondSound;

        [ShowIf("useSecondSound")]
        public UISoundType secondSoundType;

        [ShowIf("useSecondSound")]
        public float secondVolume;
    }

    public class UIPulseEffect : UIConfigurableEffect<PulseConfig>
    {
        [Header("Config")]
        [SerializeField] private PulseConfig defaultConfig;
        private bool _isPulsing;

        private Vector3 _originalScale;
        private PulseConfig? _overrideConfig;
        private Tween _pulseTween;

        public PulseConfig DefaultConfig => defaultConfig;

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(PulseConfig configData, float? customDuration = null)
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

            if (!_isPulsing)
            {
                _originalScale = targetRectTransform.localScale;
                _isPulsing = true;
            }

            yield return animType switch
            {
                AnimType.Normal => PulseRoutine(configToUse, durationToUse),
                AnimType.DoTween => PulseRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (_isPulsing && targetRectTransform != null)
            {
                targetRectTransform.localScale = _originalScale;
                _isPulsing = false;
            }
        }

        private IEnumerator PulseRoutine(PulseConfig configToUse, float durationToUse)
        {
            var fullCycleTime = configToUse.pulseDuration * 2f;
            var calculatedCycles = Mathf.RoundToInt(durationToUse / fullCycleTime);
            if (calculatedCycles <= 0) calculatedCycles = 1;
            var exactTotalTime = calculatedCycles * fullCycleTime;

            var elapsed = 0f;
            var targetScaleMultiplier = Random.Range(configToUse.scaleMin, configToUse.scaleMax);

            var lastPlayedHalfCycle = -1;

            while (configToUse.isLoop || elapsed < exactTotalTime)
            {
                // halfCycleTime(pulseDuration) 기준으로 현재 주기를 계산
                var currentHalfCycle = Mathf.FloorToInt(elapsed / configToUse.pulseDuration);

                if (currentHalfCycle > lastPlayedHalfCycle)
                {
                    var isScalingUp = currentHalfCycle % 2 == 0;

                    if (isScalingUp && configToUse.useFirstSound && SoundManager.Instance != null)
                    {
                        var finalVolume = configToUse.firstVolume <= 0f ? 1.0f : configToUse.firstVolume;
                        PlaySound(configToUse.firstSoundType, finalVolume);
                    }
                    else if (!isScalingUp && configToUse.useSecondSound && SoundManager.Instance != null)
                    {
                        var finalVolume = configToUse.secondVolume <= 0f ? 1.0f : configToUse.secondVolume;
                        PlaySound(configToUse.secondSoundType, finalVolume);
                    }

                    lastPlayedHalfCycle = currentHalfCycle;
                }

                var t = Mathf.PingPong(elapsed, configToUse.pulseDuration) / configToUse.pulseDuration;
                targetRectTransform.localScale = _originalScale * Mathf.Lerp(1f, targetScaleMultiplier, t);

                elapsed += Time.deltaTime;
                yield return null;
            }

            targetRectTransform.localScale = _originalScale;
        }

        private IEnumerator PulseRoutineDoTween(PulseConfig configToUse, float durationToUse)
        {
            _pulseTween?.Kill();
            var targetScale = Random.Range(configToUse.scaleMin, configToUse.scaleMax);

            var fullCycleTime = configToUse.pulseDuration * 2f;
            var calculatedCycles = Mathf.RoundToInt(durationToUse / fullCycleTime);
            if (calculatedCycles <= 0) calculatedCycles = 1;

            var finalLoops = configToUse.isLoop ? -1 : calculatedCycles * 2;

            // 시작 시 첫 번째 사운드 재생
            if (configToUse.useFirstSound && SoundManager.Instance != null)
            {
                var finalVolume = configToUse.firstVolume <= 0f ? 1.0f : configToUse.firstVolume;
                PlaySound(configToUse.firstSoundType, finalVolume);
            }

            var stepCount = 0;

            _pulseTween = targetRectTransform.DOScale(_originalScale * targetScale, configToUse.pulseDuration)
                .SetLoops(finalLoops, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .OnStepComplete(() =>
                {
                    stepCount++;
                    var isLoopingOrNotFinished = configToUse.isLoop || stepCount < finalLoops;

                    if (isLoopingOrNotFinished)
                    {
                        if (stepCount % 2 == 1) // 스케일업 완료 시점 (Peak)
                        {
                            if (configToUse.useSecondSound && SoundManager.Instance != null)
                            {
                                var finalVolume = configToUse.secondVolume <= 0f ? 1.0f : configToUse.secondVolume;
                                PlaySound(configToUse.secondSoundType, finalVolume);
                            }
                        }
                        else // 스케일다운 완료 시점 (원래 크기로 복귀)
                        {
                            if (configToUse.useFirstSound && SoundManager.Instance != null)
                            {
                                var finalVolume = configToUse.firstVolume <= 0f ? 1.0f : configToUse.firstVolume;
                                PlaySound(configToUse.firstSoundType, finalVolume);
                            }
                        }
                    }
                });

            yield return _pulseTween.WaitForCompletion();
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_pulseTween != null && _pulseTween.IsActive()) _pulseTween.Kill();

            if (_isPulsing && snapToEnd && targetRectTransform != null) targetRectTransform.localScale = _originalScale;

            _isPulsing = false;
        }
    }
}