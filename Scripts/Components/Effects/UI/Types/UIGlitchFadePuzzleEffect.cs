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
    public enum PuzzleGlitchMode
    {
        SingleError,
        RandomLoop
    }

    public enum GlitchMaskMode
    {
        RectArea,
        VisibleArea
    }

    [Serializable]
    public struct GlitchPuzzleConfig
    {
        public PuzzleGlitchMode mode;
        public GlitchMaskMode maskMode;
        public Vector2 gridSize;

        [Range(0f, 1f)]
        public float filterChance;
        public Material targetMaterial;

        [Header("Loop Settings")]
        public float brokenTimeMin;
        public float brokenTimeMax;
        public float transitionDuration;
        public float normalTimeMin;
        public float normalTimeMax;

        [Header("Impact Settings")]
        [Tooltip("퍼즐이 변할 때 강하게 파르르 떨리는 시간")]
        public float jitterDuration;

        [Header("Effect Sound")]
        public bool useSound;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    [RequireComponent(typeof(Graphic))]
    public class UIGlitchFadePuzzleEffect : UIConfigurableEffect<GlitchPuzzleConfig>
    {
        private static readonly int GlitchIntensity = Shader.PropertyToID("_GlitchIntensity");
        private static readonly int JitterIntensity = Shader.PropertyToID("_JitterIntensity");
        private static readonly int GridSize = Shader.PropertyToID("_GridSize");
        private static readonly int FilterAmount = Shader.PropertyToID("_FilterAmount");
        private static readonly int Seed = Shader.PropertyToID("_Seed");
        private static readonly int MaskMode = Shader.PropertyToID("_MaskMode");

        [Header("Target Components")]
        [SerializeField] private Graphic targetGraphic;

        [Header("Config")]
        [SerializeField] private GlitchPuzzleConfig defaultConfig;
        private Tween _glitchTween;

        private Material _instancedMat;
        private Coroutine _jitterRoutine;
        private Coroutine _mainRoutine;

        private GlitchPuzzleConfig? _overrideConfig;

        protected override void Awake()
        {
            base.Awake();
            if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();

            if (targetGraphic != null && defaultConfig.targetMaterial != null)
            {
                _instancedMat = new Material(defaultConfig.targetMaterial);
                targetGraphic.material = _instancedMat;

                _instancedMat.SetFloat(GlitchIntensity, 0f);
                _instancedMat.SetFloat(JitterIntensity, 0f);
            }
        }

        private void OnDestroy()
        {
            if (_instancedMat != null) Destroy(_instancedMat);
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(GlitchPuzzleConfig targetValue, float? customDuration = null)
        {
            _overrideConfig = targetValue;
            if (customDuration.HasValue) OverrideDuration = customDuration.Value;
        }

        public override void PlayOverrideEffect()
        {
            StopEffectRoutines();
            _mainRoutine = StartCoroutine(ExecuteEffect());
        }

        public override IEnumerator PlayWaitableEffect()
        {
            StopEffectRoutines();
            yield return _mainRoutine = StartCoroutine(ExecuteEffect());
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

        private void StopEffectRoutines()
        {
            if (_mainRoutine != null)
            {
                StopCoroutine(_mainRoutine);
                _mainRoutine = null;
            }

            if (_jitterRoutine != null)
            {
                StopCoroutine(_jitterRoutine);
                _jitterRoutine = null;
            }

            if (_glitchTween != null && _glitchTween.IsActive()) _glitchTween.Kill();
        }

        protected override IEnumerator ExecuteEffect()
        {
            if (_instancedMat == null) yield break;

            if (targetGraphic != null && targetGraphic.material != _instancedMat)
                targetGraphic.material = _instancedMat;

            var durationToUse = ActualDuration;
            OverrideDuration = null;

            var configToUse = _overrideConfig ?? defaultConfig;

            _instancedMat.SetVector(GridSize, configToUse.gridSize);
            _instancedMat.SetFloat(FilterAmount, configToUse.filterChance);
            _instancedMat.SetFloat(MaskMode, configToUse.maskMode == GlitchMaskMode.VisibleArea ? 1f : 0f);

            if (configToUse.mode == PuzzleGlitchMode.SingleError)
            {
                PlayEffectSound(configToUse);
                BreakPuzzle(_instancedMat, configToUse.jitterDuration);

                var holdTime = durationToUse > 0 ? durationToUse : configToUse.brokenTimeMin;
                yield return new WaitForSeconds(holdTime);

                yield return TransitionToNormal(_instancedMat, configToUse.transitionDuration);
            }
            else if (configToUse.mode == PuzzleGlitchMode.RandomLoop)
            {
                while (true)
                {
                    PlayEffectSound(configToUse);
                    BreakPuzzle(_instancedMat, configToUse.jitterDuration);

                    var brokenTime = Random.Range(configToUse.brokenTimeMin, configToUse.brokenTimeMax);
                    if (brokenTime > 0f) yield return new WaitForSeconds(brokenTime);

                    if (configToUse.transitionDuration > 0f)
                        yield return TransitionToNormal(_instancedMat, configToUse.transitionDuration);
                    else
                        _instancedMat.SetFloat(GlitchIntensity, 0f);

                    var normalTime = Random.Range(configToUse.normalTimeMin, configToUse.normalTimeMax);
                    if (normalTime > 0f) yield return new WaitForSeconds(normalTime);
                }
            }
        }

        private void PlayEffectSound(GlitchPuzzleConfig config)
        {
            if (config.useSound && SoundManager.Instance != null)
            {
                var finalVolume = config.volume <= 0f ? 1.0f : config.volume;
                PlaySound(config.soundType, finalVolume);
            }
        }

        private void BreakPuzzle(Material mat, float jitterDuration)
        {
            var brokenAmount = Random.Range(0.8f, 1.0f);
            mat.SetFloat(GlitchIntensity, brokenAmount);
            mat.SetFloat(Seed, Random.value * 1000f);

            if (jitterDuration > 0f)
            {
                if (_jitterRoutine != null) StopCoroutine(_jitterRoutine);

                mat.SetFloat(JitterIntensity, 1f);
                _jitterRoutine = StartCoroutine(JitterSpikeRoutine(mat, jitterDuration));
            }
        }

        private IEnumerator JitterSpikeRoutine(Material mat, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = elapsed / duration;

                mat.SetFloat(JitterIntensity, Mathf.Lerp(1f, 0f, progress));
                yield return null;
            }

            mat.SetFloat(JitterIntensity, 0f);
            _jitterRoutine = null;
        }

        private IEnumerator TransitionToNormal(Material mat, float duration)
        {
            return animType switch
            {
                AnimType.Normal => NormalTransitionRoutine(mat, duration),
                AnimType.DoTween => DoTweenTransitionRoutine(mat, duration),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private IEnumerator NormalTransitionRoutine(Material mat, float duration)
        {
            var elapsed = 0f;
            var startIntensity = mat.GetFloat(GlitchIntensity);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = elapsed / duration;

                mat.SetFloat(GlitchIntensity, Mathf.Lerp(startIntensity, 0f, progress));
                yield return null;
            }

            mat.SetFloat(GlitchIntensity, 0f);
        }

        private IEnumerator DoTweenTransitionRoutine(Material mat, float duration)
        {
            _glitchTween?.Kill();
            _glitchTween = mat.DOFloat(0f, GlitchIntensity, duration).SetEase(Ease.OutQuad);
            yield return _glitchTween.WaitForCompletion();
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            StopEffectRoutines();

            if (snapToEnd && _instancedMat != null)
            {
                _instancedMat.SetFloat(GlitchIntensity, 0f);
                _instancedMat.SetFloat(JitterIntensity, 0f);
            }
        }
    }
}