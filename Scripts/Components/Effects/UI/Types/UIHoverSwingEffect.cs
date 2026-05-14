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
    public struct HoverSwingConfig
    {
        public Vector3 swingRotInit;

        public Vector3 swingRotMin;
        public Vector3 swingRotMax;

        public float returnDuration;

        public bool isLoop;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    [RequireComponent(typeof(RectTransform))]
    public class UIHoverSwingEffect : UIConfigurableEffect<HoverSwingConfig>
    {
        [Header("Config")]
        [SerializeField] private HoverSwingConfig defaultConfig;
        private Sequence _hoverSwingSequence;

        private Coroutine _mainCoroutine;

        private HoverSwingConfig? _overrideConfig;
        private Coroutine _returnCoroutine;

        private Tween _returnTween;

        public HoverSwingConfig DefaultConfig => defaultConfig;

        protected override void Awake()
        {
            base.Awake();
            if (targetRectTransform == null)
                targetRectTransform = GetComponent<RectTransform>();
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(HoverSwingConfig configData, float? customDuration = null)
        {
            _overrideConfig = configData;

            if (customDuration.HasValue)
                OverrideDuration = customDuration.Value;
        }

        public override void PlayOverrideEffect()
        {
            ClearRunningRoutines();
            _mainCoroutine = StartCoroutine(ExecuteEffect());
        }

        public override IEnumerator PlayWaitableEffect()
        {
            ClearRunningRoutines();
            yield return _mainCoroutine = StartCoroutine(ExecuteEffect());
        }

        private void ClearRunningRoutines()
        {
            if (_mainCoroutine != null)
            {
                StopCoroutine(_mainCoroutine);
                _mainCoroutine = null;
            }

            if (_returnCoroutine != null)
            {
                StopCoroutine(_returnCoroutine);
                _returnCoroutine = null;
            }

            _hoverSwingSequence?.Kill();
            _returnTween?.Kill();
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

            if (configToUse.useSound && SoundManager.Instance != null)
            {
                var finalVolume = configToUse.volume <= 0f ? 1.0f : configToUse.volume;
                PlaySound(configToUse.soundType, finalVolume);
            }

            if (targetRectTransform.localEulerAngles != configToUse.swingRotInit)
                targetRectTransform.localEulerAngles = configToUse.swingRotInit;

            yield return animType switch
            {
                AnimType.Normal => HoverSwingRoutine(configToUse, durationToUse),
                AnimType.DoTween => HoverSwingRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerator HoverSwingRoutine(HoverSwingConfig configToUse, float durationToUse)
        {
            var initRot = Quaternion.Euler(configToUse.swingRotInit);
            var minRot = Quaternion.Euler(configToUse.swingRotMin);
            var maxRot = Quaternion.Euler(configToUse.swingRotMax);

            if (configToUse.isLoop)
                while (true)
                {
                    yield return SwingTo(initRot, minRot, durationToUse, true);
                    yield return SwingTo(minRot, initRot, durationToUse, false);
                    yield return SwingTo(initRot, maxRot, durationToUse, true);
                    yield return SwingTo(maxRot, initRot, durationToUse, false);
                }

            yield return SwingTo(initRot, minRot, durationToUse, true);
            yield return SwingTo(minRot, initRot, durationToUse, false);
            yield return SwingTo(initRot, maxRot, durationToUse, true);
            yield return SwingTo(maxRot, initRot, durationToUse, false);
        }

        private IEnumerator SwingTo(Quaternion startRot, Quaternion endRot, float duration, bool isEaseOut)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);

                var ease = isEaseOut
                    ? Mathf.Sin(progress * Mathf.PI / 2f)
                    : 1f - Mathf.Cos(progress * Mathf.PI / 2f);

                targetRectTransform.localRotation = Quaternion.Lerp(startRot, endRot, ease);
                yield return null;
            }

            targetRectTransform.localRotation = endRot;
        }

        private IEnumerator HoverSwingRoutineDoTween(HoverSwingConfig configToUse, float durationToUse)
        {
            _hoverSwingSequence?.Kill();
            _hoverSwingSequence = DOTween.Sequence();

            targetRectTransform.localEulerAngles = configToUse.swingRotInit;

            _hoverSwingSequence.Append(
                targetRectTransform.DOLocalRotate(configToUse.swingRotMin, durationToUse)
                    .SetEase(Ease.OutSine)
            );

            _hoverSwingSequence.Append(
                targetRectTransform.DOLocalRotate(configToUse.swingRotInit, durationToUse)
                    .SetEase(Ease.InSine)
            );

            _hoverSwingSequence.Append(
                targetRectTransform.DOLocalRotate(configToUse.swingRotMax, durationToUse)
                    .SetEase(Ease.OutSine)
            );

            _hoverSwingSequence.Append(
                targetRectTransform.DOLocalRotate(configToUse.swingRotInit, durationToUse)
                    .SetEase(Ease.InSine)
            );

            var loopCount = configToUse.isLoop ? -1 : 1;
            _hoverSwingSequence.SetLoops(loopCount, LoopType.Restart);

            yield return _hoverSwingSequence.WaitForCompletion();
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            ClearRunningRoutines();

            if (snapToEnd && targetRectTransform != null)
            {
                var config = _overrideConfig ?? defaultConfig;
                targetRectTransform.localEulerAngles = config.swingRotInit;
            }
        }

        public void ReturnBasic()
        {
            base.Stop(false);
            ClearRunningRoutines();

            if (targetRectTransform == null) return;

            var config = _overrideConfig ?? defaultConfig;

            if (!gameObject.activeInHierarchy || config.returnDuration <= 0f)
            {
                targetRectTransform.localEulerAngles = config.swingRotInit;
                return;
            }

            if (animType == AnimType.DoTween)
                _returnTween = targetRectTransform.DOLocalRotate(config.swingRotInit, config.returnDuration)
                    .SetEase(Ease.OutQuad);
            else
                _returnCoroutine = StartCoroutine(SmoothReturnRoutine(config.swingRotInit, config.returnDuration));
        }

        private IEnumerator SmoothReturnRoutine(Vector3 targetRot, float duration)
        {
            var elapsed = 0f;
            var startRot = targetRectTransform.localRotation;
            var endRot = Quaternion.Euler(targetRot);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var ease = Mathf.SmoothStep(0f, 1f, progress);

                targetRectTransform.localRotation = Quaternion.Lerp(startRot, endRot, ease);
                yield return null;
            }

            targetRectTransform.localRotation = endRot;
            _returnCoroutine = null;
        }
    }
}