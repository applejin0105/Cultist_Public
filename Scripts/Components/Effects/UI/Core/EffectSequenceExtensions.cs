using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Components.Effects.UI.Core
{
    public static class EffectSequenceExtensions
    {
        /// <summary>
        ///     Sequence에 등록된 Step 중 무작위로 하나만 뽑아서 실행 (완벽 동기화 적용)
        /// </summary>
        private static IEnumerator PlayRandomStepRoutine(this EffectSequence sequence, Action onComplete = null)
        {
            if (sequence.steps == null || sequence.steps.Count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            var randomIndex = Random.Range(0, sequence.steps.Count);
            var selectedStep = sequence.steps[randomIndex];

            if (selectedStep.delayBeforeStep > 0f)
                yield return new WaitForSeconds(selectedStep.delayBeforeStep);

            var runningEffects = new List<Coroutine>();

            foreach (var effect in selectedStep.simultaneousEffects)
                if (effect != null && effect.gameObject.activeInHierarchy)
                {
                    var c = effect.StartCoroutine(effect.PlayWaitable());
                    runningEffects.Add(c);
                }

            if (selectedStep.waitForCompletion && runningEffects.Count > 0)
                foreach (var runningCoroutine in runningEffects)
                    yield return runningCoroutine;

            onComplete?.Invoke();
        }

        /// <summary>
        ///     무작위 이펙트 실행을 지정된 횟수(또는 무한)만큼 반복하며, 중간 대기시간(고정/랜덤)을 설정.
        /// </summary>
        public static IEnumerator PlayRandomLoopRoutine(
            this EffectSequence sequence,
            int repeatCount = 0,
            float minWait = 0f,
            float maxWait = 0f,
            float initialDelay = 0f,
            Action onComplete = null)
        {
            if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);

            var isInfinite = repeatCount <= 0; // 0 이하면 무한루프
            var currentCount = 0;

            while (isInfinite || currentCount < repeatCount)
            {
                // 단일 랜덤 스텝 실행 (내부에서 완벽하게 대기하므로 시간 오차가 없음 우효오옷)
                yield return sequence.PlayRandomStepRoutine();

                currentCount++;

                if ((isInfinite || currentCount < repeatCount) && maxWait > 0f)
                {
                    var waitTime = Mathf.Max(0f, Random.Range(minWait, maxWait));
                    if (waitTime > 0f) yield return new WaitForSeconds(waitTime);
                }
            }

            onComplete?.Invoke();
        }
    }
}