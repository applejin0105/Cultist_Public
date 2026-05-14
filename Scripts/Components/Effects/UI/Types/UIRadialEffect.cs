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
    public struct RadialConfig
    {
        public RectTransform centerRect;
        public bool isExpand;
        public bool useSound;
        public Color effectColor;

        [ShowIf("useSound")]
        public UISoundType soundType;

        [ShowIf("useSound")]
        public float volume;
    }

    public class UIRadialEffect : UIConfigurableEffect<RadialConfig>
    {
        private static readonly int CenterID = Shader.PropertyToID("_EffectCenter");
        private static readonly int RadiusID = Shader.PropertyToID("_EffectRadius");
        private static readonly int TargetColorID = Shader.PropertyToID("_TargetColor"); // 추가됨

        [Header("Target Graphics")]
        [SerializeField] private Image[] backgroundImages;

        [Header("Target Area")]
        [SerializeField] private RectTransform targetArea;

        [Header("Config")]
        [SerializeField] private RadialConfig defaultConfig;
        private RectTransform _canvasRect;

        private float _currentRadius = -100f;
        private Coroutine _effectCoroutine;
        private Material[] _instancedMaterials;

        private RadialConfig? _overrideConfig;
        private Tween _radialTween;
        private float _targetRadius = -100f;
        public RadialConfig DefaultConfig => defaultConfig;

        protected override void Awake()
        {
            base.Awake();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) _canvasRect = canvas.GetComponent<RectTransform>();

            _instancedMaterials = new Material[backgroundImages.Length];
            for (var i = 0; i < backgroundImages.Length; i++)
                if (backgroundImages[i] != null && backgroundImages[i].material != null)
                {
                    _instancedMaterials[i] = new Material(backgroundImages[i].material);
                    backgroundImages[i].material = _instancedMaterials[i];
                    _instancedMaterials[i].SetFloat(RadiusID, -100f);
                }
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(RadialConfig configData, float? customDuration = null)
        {
            _overrideConfig = configData;
            if (customDuration.HasValue) OverrideDuration = customDuration.Value;
        }

        public override void PlayOverrideEffect()
        {
            if (_effectCoroutine != null) StopCoroutine(_effectCoroutine);
            _effectCoroutine = StartCoroutine(ExecuteEffect());
        }

        public override IEnumerator PlayWaitableEffect()
        {
            if (_effectCoroutine != null) StopCoroutine(_effectCoroutine);
            _effectCoroutine = StartCoroutine(ExecuteEffect());
            yield return _effectCoroutine;
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

            for (var i = 0; i < _instancedMaterials.Length; i++)
            {
                if (_instancedMaterials[i] != null)
                {
                    _instancedMaterials[i].SetColor(TargetColorID, configToUse.effectColor);
                }
            }

            if (configToUse.useSound)
                PlaySound(configToUse.soundType, configToUse.volume <= 0f ? 1.0f : configToUse.volume);

            if (configToUse.centerRect != null) UpdateShaderCenter(configToUse.centerRect);

            float startRadius;

            if (configToUse.isExpand)
            {
                startRadius = _currentRadius > 0 ? _currentRadius : 0f;
                _targetRadius = CalculateExactWorldMaxRadius(configToUse);
            }
            else
            {
                startRadius = _currentRadius;
                _targetRadius = -100f;
            }

            yield return animType switch
            {
                AnimType.Normal => RadialRoutine(durationToUse, startRadius, _targetRadius),
                AnimType.DoTween => RadialRoutineDoTween(durationToUse, _targetRadius),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void UpdateShaderCenter(RectTransform targetRect)
        {
            var centerData = new Vector4(targetRect.position.x, targetRect.position.y, targetRect.position.z, 0);

            for (var i = 0; i < _instancedMaterials.Length; i++)
                if (_instancedMaterials[i] != null)
                    _instancedMaterials[i].SetVector(CenterID, centerData);
        }

        private void UpdateShaderRadius(float radius)
        {
            _currentRadius = radius;
            for (var i = 0; i < _instancedMaterials.Length; i++)
                if (_instancedMaterials[i] != null)
                    _instancedMaterials[i].SetFloat(RadiusID, _currentRadius);
        }

        private float CalculateExactWorldMaxRadius(RadialConfig config)
        {
            if (config.centerRect == null) return 2000f;
            var clickWorldPos = config.centerRect.position;

            var referenceRect = targetArea != null ? targetArea : _canvasRect;
            if (referenceRect == null) return 2000f;

            var worldCorners = new Vector3[4];
            referenceRect.GetWorldCorners(worldCorners);

            var maxWorldDist = 0f;
            foreach (var corner in worldCorners)
            {
                var dist = Vector3.Distance(clickWorldPos, corner);
                if (dist > maxWorldDist) maxWorldDist = dist;
            }

            return maxWorldDist + 50f;
        }

        private IEnumerator RadialRoutine(float duration, float startRadius, float targetRadius)
        {
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var ease = 1f - Mathf.Pow(1f - progress, 3f);

                var newRadius = Mathf.Lerp(startRadius, targetRadius, ease);
                UpdateShaderRadius(newRadius);

                yield return null;
            }

            UpdateShaderRadius(targetRadius);
        }

        private IEnumerator RadialRoutineDoTween(float duration, float targetRadius)
        {
            _radialTween?.Kill();

            _radialTween = DOTween.To(() => _currentRadius, UpdateShaderRadius, targetRadius, duration)
                .SetEase(Ease.OutQuad);

            yield return _radialTween.WaitForCompletion();
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_radialTween != null && _radialTween.IsActive()) _radialTween.Kill();

            if (_effectCoroutine != null)
            {
                StopCoroutine(_effectCoroutine);
                _effectCoroutine = null;
            }

            if (snapToEnd) UpdateShaderRadius(_targetRadius);
        }
    }
}