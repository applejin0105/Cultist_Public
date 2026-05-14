using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Components.Effects.UI.Core
{
    // MonoBehaviour 아님. 직렬화용
    [Serializable]
    public class EffectSequence
    {
        [Tooltip("순차적으로 실행될 단계들을 추가")]
        public List<EffectStep> steps = new();

        public void AddStep(EffectStep step)
        {
            steps.Add(step);
        }

        public IEnumerator PlaySequenceRoutine(Action onComplete = null)
        {
            foreach (var step in steps)
            {
                if (step.delayBeforeStep > 0f) yield return new WaitForSeconds(step.delayBeforeStep);

                var runningEffects = new List<Coroutine>();

                foreach (var effect in step.simultaneousEffects)
                    if (effect != null && effect.gameObject.activeInHierarchy)
                    {
                        var c = effect.StartCoroutine(effect.PlayWaitable());
                        runningEffects.Add(c);
                    }

                if (step.waitForCompletion)
                    foreach (var runningCoroutine in runningEffects)
                        yield return runningCoroutine;
            }

            onComplete?.Invoke();
        }

        public void StopAll()
        {
            foreach (var step in steps)
            foreach (var effect in step.simultaneousEffects)
                if (effect != null)
                    effect.Stop();
        }
    }
}