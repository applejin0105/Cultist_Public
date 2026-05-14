using System;
using System.Collections;
using Components.Effects.UI.Core;
using Core.Attributes;
using Core.Data.Enums;
using Core.Managers;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Components.Effects.UI.Types
{
    public enum GlitchMode
    {
        SingleFadeIn,
        RandomLoop
    }

    [Serializable]
    public struct GlitchFadeConfig
    {
        public GlitchMode mode;
        public GlitchMaskMode maskMode;
        public Vector2 gridSize;
        public Material targetMaterial;

        [Header("Loop Settings (RandomLoop 모드 전용)")]
        public float loopIntervalMin;
        public float loopIntervalMax;
        public float loopGlitchDuration;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    [RequireComponent(typeof(Graphic))]
    public class UIGlitchFadeEffect : UIConfigurableEffect<GlitchFadeConfig>
    {
        private static readonly int GlitchIntensity = Shader.PropertyToID("_GlitchIntensity");
        private static readonly int GridSize = Shader.PropertyToID("_GridSize");
        private static readonly int Seed = Shader.PropertyToID("_Seed");
        private static readonly int MaskMode = Shader.PropertyToID("_MaskMode");

        [Header("Target Components")]
        [SerializeField] private Graphic targetGraphic;

        [Header("Config")]
        [SerializeField] private GlitchFadeConfig defaultConfig;
        private Coroutine _glitchCoroutine;
        private Tween _glitchTween;
        private Material _instancedMaterial;

        private GlitchFadeConfig? _overrideConfig;

        protected override void Awake()
        {
            base.Awake();

            if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();

            if (targetGraphic != null && defaultConfig.targetMaterial != null)
            {
                _instancedMaterial = new Material(defaultConfig.targetMaterial);
                targetGraphic.material = _instancedMaterial;
                _instancedMaterial.SetFloat(GlitchIntensity, 0f);
            }
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(GlitchFadeConfig targetValue, float? customDuration = null)
        {
            _overrideConfig = targetValue;
            if (customDuration.HasValue) OverrideDuration = customDuration.Value;
        }

        public override void PlayOverrideEffect()
        {
            StopEffectRoutines();
            _glitchCoroutine = StartCoroutine(ExecuteEffect());
        }

        public override IEnumerator PlayWaitableEffect()
        {
            StopEffectRoutines();
            yield return _glitchCoroutine = StartCoroutine(ExecuteEffect());
        }

        private void StopEffectRoutines()
        {
            if (_glitchCoroutine != null)
            {
                StopCoroutine(_glitchCoroutine);
                _glitchCoroutine = null;
            }

            if (_glitchTween != null && _glitchTween.IsActive()) _glitchTween.Kill();
        }

        public void RestoreOriginalState()
        {
            Stop();
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
            if (_instancedMaterial == null) yield break;

            if (targetGraphic != null && targetGraphic.material != _instancedMaterial)
                targetGraphic.material = _instancedMaterial;

            var durationToUse = ActualDuration;
            OverrideDuration = null;

            var configToUse = _overrideConfig ?? defaultConfig;

            _instancedMaterial.SetVector(GridSize, configToUse.gridSize);
            _instancedMaterial.SetFloat(MaskMode, configToUse.maskMode == GlitchMaskMode.VisibleArea ? 1f : 0f);

            if (configToUse.mode == GlitchMode.SingleFadeIn)
            {
                PlayGlitchSound(configToUse);
                yield return StartGlitchSpike(durationToUse);
            }
            else if (configToUse.mode == GlitchMode.RandomLoop)
            {
                while (true)
                {
                    var waitTime = Random.Range(configToUse.loopIntervalMin, configToUse.loopIntervalMax);
                    yield return new WaitForSeconds(waitTime);

                    PlayGlitchSound(configToUse);
                    yield return StartGlitchSpike(configToUse.loopGlitchDuration);
                }
            }
        }

        private void PlayGlitchSound(GlitchFadeConfig config)
        {
            if (config.useSound && SoundManager.Instance != null)
            {
                var finalVolume = config.volume <= 0f ? 1.0f : config.volume;
                PlaySound(config.soundType, finalVolume);
            }
        }

        private IEnumerator StartGlitchSpike(float duration)
        {
            return animType switch
            {
                AnimType.Normal => NormalRoutine(duration),
                AnimType.DoTween => DoTweenRoutine(duration),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerator NormalRoutine(float duration)
        {
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = elapsed / duration;

                var intensity = Mathf.Lerp(1f, 0f, progress);
                _instancedMaterial.SetFloat(GlitchIntensity, intensity);
                _instancedMaterial.SetFloat(Seed, Random.value * 100f);

                yield return null;
            }

            _instancedMaterial.SetFloat(GlitchIntensity, 0f);
        }

        private IEnumerator DoTweenRoutine(float duration)
        {
            _glitchTween?.Kill();

            _instancedMaterial.SetFloat(GlitchIntensity, 1f);

            _glitchTween = _instancedMaterial.DOFloat(0f, GlitchIntensity, duration)
                .SetEase(Ease.OutQuad)
                .OnUpdate(() => { _instancedMaterial.SetFloat(Seed, Random.value * 100f); });

            yield return _glitchTween.WaitForCompletion();
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            StopEffectRoutines();

            if (snapToEnd && _instancedMaterial != null) _instancedMaterial.SetFloat(GlitchIntensity, 0f);
        }
    }
}