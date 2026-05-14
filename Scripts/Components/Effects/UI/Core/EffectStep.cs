using System;
using System.Collections.Generic;
using UnityEngine;

namespace Components.Effects.UI.Core
{
    [Serializable]
    public class EffectStep
    {
        [Tooltip("인스펙터에서 구분하기 위한 이름")]
        public string stepName = "New step";

        [Tooltip("단계 실행 전 대기시간")]
        public float delayBeforeStep;

        [Tooltip("이 단계에 등록된 이펙트들이 다 끝날 때까지 대기할지 여부")]
        public bool waitForCompletion = true;

        [Tooltip("동시에 실행될 이펙트들")]
        public List<UIEffect> simultaneousEffects = new();

        public EffectStep()
        {
            stepName = "New Step";
            delayBeforeStep = 0;
            waitForCompletion = false;
            simultaneousEffects = new List<UIEffect>();
        }

        public EffectStep(string stepName, float delayBeforeStep, bool waitForCompletion)
        {
            this.stepName = stepName;
            this.delayBeforeStep = delayBeforeStep;
            this.waitForCompletion = waitForCompletion;
        }

        public void AddSimultaneousEffect(UIEffect effect)
        {
            simultaneousEffects.Add(effect);
        }
    }
}