using System.Collections;
using Components.Common.Buttons.Core;
using Components.Effects.UI.Core;
using Components.Effects.UI.Types;
using UnityEngine;

namespace Scenes.Main
{
    public class MainSceneUIController : MonoBehaviour
    {
        [Header("UI Sequences - Single Load")]
        [Tooltip("싱글로직 씬 진입 시 재생될 연출")]
        [SerializeField] private EffectSequence fadeOnSequence;

        [Tooltip("싱글로직 씬 퇴장 시 재생될 연출")]
        [SerializeField] private EffectSequence fadeOutSequence;

        [Header("UI Sequences - Preload")]
        [Tooltip("씬 진입 시 재생될 공통(프리로드) 연출")]
        [SerializeField] private EffectSequence startSequence;

        [Tooltip("씬 퇴장 시 재생될 공통(프리로드) 연출")]
        [SerializeField] private EffectSequence endSequence;

        [SerializeField] private UIFadeCanvasGroupEffect uiFadeCanvasGroupEffect;
        [SerializeField] private CanvasGroup canvasGroup;

        private FadeCanvasGroupConfig fadeCanvasGroupConfig;

        private void Start()
        {
            ResetUILoad();
            PlayEnterUILoad();
        }

        public void ResetUILoad()
        {
            fadeOnSequence?.StopAll();
            fadeOutSequence?.StopAll();

            canvasGroup.alpha = 0.0f;

            fadeCanvasGroupConfig = uiFadeCanvasGroupEffect.DefaultConfig;
            fadeCanvasGroupConfig.endAlpha = 1.0f;
        }

        public void PlayEnterUILoad()
        {
            ResetUILoad();

            if (fadeOnSequence != null && fadeOnSequence.steps.Count > 0)
            {
                StartCoroutine(EnterUILoadRoutine());
            }
        }

        private IEnumerator EnterUILoadRoutine()
        {
            yield return StartCoroutine(fadeOnSequence.PlaySequenceRoutine());
        }
    }
}