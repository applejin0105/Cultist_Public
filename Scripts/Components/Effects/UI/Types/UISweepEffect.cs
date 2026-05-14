using System;
using System.Collections;
using Components.Effects.UI.Core;
using Core.Data.Enums;
using Core.Managers;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Components.Effects.UI.Types
{
    [Serializable]
    public struct SweepConfig
    {
        public bool isLoop;

        [Range(0, 360)]
        public float angle; // 닦이는 진행 방향 (각도)
        public float sweepDuration; // 지나가는 속도 (시간)
        public float loopInterval; // 루프 모드일 경우 대기 시간

        public float lineWidth; // 선의 두께
        public Color lineColor; // 선의 색상 및 투명도

        public Ease easeType;
    }

    public class UISweepEffect : UIConfigurableEffect<SweepConfig>
    {
        [Header("Config")]
        [SerializeField] private SweepConfig defaultConfig;
        [SerializeField] private Image sweepLineImage; // 빛줄기로 사용할 자식 Image 참조
        private bool _isSweeping;
        private RectTransform _lineRect;

        private SweepConfig? _overrideConfig;
        private Tween _sweepTween;

        public SweepConfig DefaultConfig => defaultConfig;

        protected override void Awake()
        {
            if (sweepLineImage != null) _lineRect = sweepLineImage.rectTransform;
        }

        public override void ClearProperty()
        {
            _overrideConfig = null;
        }

        public override void SetProperty(SweepConfig configData, float? customDuration = null)
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

            if (sweepLineImage == null || targetRectTransform == null) yield break;

            _isSweeping = true;
            sweepLineImage.gameObject.SetActive(true);

            yield return animType switch
            {
                AnimType.Normal => SweepRoutine(configToUse, durationToUse),
                AnimType.DoTween => SweepRoutineDoTween(configToUse, durationToUse),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (_isSweeping)
            {
                sweepLineImage.gameObject.SetActive(false);
                _isSweeping = false;
            }
        }

        private IEnumerator SweepRoutine(SweepConfig configToUse, float durationToUse)
        {
            // DoTween이 아닌 일반 코루틴 구현부 (수동 선형 보간)
            SetupLineProperties(configToUse, out var startPos, out var endPos);

            var elapsed = 0f;
            while (configToUse.isLoop || elapsed < configToUse.sweepDuration)
            {
                var t = elapsed % (configToUse.sweepDuration + configToUse.loopInterval) /
                        configToUse.sweepDuration;

                if (t <= 1f)
                {
                    sweepLineImage.gameObject.SetActive(true);
                    _lineRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                }
                else
                {
                    sweepLineImage.gameObject.SetActive(false);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator SweepRoutineDoTween(SweepConfig configToUse, float durationToUse)
        {
            _sweepTween?.Kill();

            SetupLineProperties(configToUse, out var startPos, out var endPos);
            _lineRect.anchoredPosition = startPos;

            _sweepTween = _lineRect.DOAnchorPos(endPos, configToUse.sweepDuration)
                .SetEase(configToUse.easeType);

            if (configToUse.isLoop) _sweepTween.SetLoops(-1, LoopType.Restart).SetDelay(configToUse.loopInterval);

            yield return _sweepTween.WaitForCompletion();
        }

        private void SetupLineProperties(SweepConfig config, out Vector2 startPos, out Vector2 endPos)
        {
            sweepLineImage.color = config.lineColor;

            // 부모 UI를 완전히 덮을 수 있도록 대각선 길이 계산
            var parentWidth = targetRectTransform.rect.width;
            var parentHeight = targetRectTransform.rect.height;
            var diagonalLength = Mathf.Sqrt(parentWidth * parentWidth + parentHeight * parentHeight);

            // 선의 크기 설정 (너비는 Config값, 길이는 부모 대각선보다 여유있게 설정)
            _lineRect.sizeDelta = new Vector2(config.lineWidth, diagonalLength * 1.5f);

            // 각도 설정
            _lineRect.localRotation = Quaternion.Euler(0, 0, config.angle);

            // 이동 궤적 계산 (각도 방향으로 부모 대각선 길이만큼 이동)
            var angleRad = config.angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            var moveDistance = diagonalLength;
            startPos = -direction * moveDistance;
            endPos = direction * moveDistance;
        }

        public override void Stop(bool snapToEnd = true)
        {
            base.Stop(snapToEnd);

            if (_sweepTween != null && _sweepTween.IsActive()) _sweepTween.Kill();

            if (sweepLineImage != null) sweepLineImage.gameObject.SetActive(false);

            _isSweeping = false;
        }
    }
}