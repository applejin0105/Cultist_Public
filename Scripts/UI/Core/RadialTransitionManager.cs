using System.Collections;
using Components.Common.Buttons.Core;
using Components.Effects.UI.Types;
using Core.Extensions;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace UI.Core
{
    public class RadialTransitionManager : MonoBehaviour
    {
        [Header("Effect Component")]
        [SerializeField] private UIRadialEffect radialEffect;

        [Header("Managers")]
        [SerializeField] private CategoryUIManager categoryUIManager;

        [Header("UI Elements (버튼)")]
        [SerializeField] private CompoundButton[] actionButtons;

        [Header("Colors")]
        [SerializeField] private Color defaultBackgroundColor = Color.black;
        [SerializeField] private Color[] categoryColors;
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color hiddenTextColor = Color.gray;

        private int _activeIndex = -1;
        private Coroutine _transitionCoroutine;

        private GameObject _currentActiveUI;

        public void OnElementClicked(int index, RectTransform clickedRect)
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            if (_activeIndex == index)
                _transitionCoroutine = StartCoroutine(ShrinkSequence());
            else
                _transitionCoroutine = StartCoroutine(SwitchSequence(index, clickedRect));
        }

        private IEnumerator SwitchSequence(int newIndex, RectTransform newRect)
        {
            if (_activeIndex != -1)
            {
                if (_activeIndex == 4 && _currentActiveUI != null)
                {
                    var creditManager = _currentActiveUI.GetComponent<Scenes.Main.CreditManager>();
                    if (creditManager != null) creditManager.CloseCreditUIEffect();
                }

                var shrinkConfig = new RadialConfig
                {
                    centerRect = null, isExpand = false, useSound = false, effectColor = categoryColors[_activeIndex]
                };
                radialEffect.SetProperty(shrinkConfig, radialEffect.Duration * 0.5f);
                yield return radialEffect.PlayWaitableEffect();
                categoryUIManager.ChangeContent(-1);
            }

            _activeIndex = newIndex;
            UpdateButtonConfigs(newIndex, radialEffect.Duration);

            _currentActiveUI = categoryUIManager.ChangeContent(newIndex);

            Color targetColor = (newIndex >= 0 && newIndex < categoryColors.Length)
                ? categoryColors[newIndex]
                : Color.white;
            var expandConfig = new RadialConfig
                { centerRect = newRect, isExpand = true, useSound = false, effectColor = targetColor };
            radialEffect.SetProperty(expandConfig, radialEffect.Duration);

            yield return radialEffect.PlayWaitableEffect();

            if (newIndex == 4 && _currentActiveUI != null)
            {
                var creditManager = _currentActiveUI.GetComponent<Scenes.Main.CreditManager>();
                if (creditManager != null) creditManager.OpenCreditUIEffect();
            }

            _transitionCoroutine = null;
        }

        private IEnumerator ShrinkSequence()
        {
            UpdateButtonConfigs(-1, radialEffect.Duration);

            if (_activeIndex == 4 && _currentActiveUI != null)
            {
                var creditManager = _currentActiveUI.GetComponent<Scenes.Main.CreditManager>();
                if (creditManager != null) creditManager.CloseCreditUIEffect();
            }

            Color shrinkColor = _activeIndex != -1 ? categoryColors[_activeIndex] : defaultBackgroundColor;
            var shrinkConfig = new RadialConfig
                { centerRect = null, isExpand = false, useSound = false, effectColor = shrinkColor };
            radialEffect.SetProperty(shrinkConfig, radialEffect.Duration * 0.5f);

            yield return radialEffect.PlayWaitableEffect();

            _activeIndex = -1;
            categoryUIManager.ChangeContent(-1);
            _currentActiveUI = null;
            _transitionCoroutine = null;
        }

        private void UpdateButtonConfigs(int selectedIndex, float animTime)
        {
            if (actionButtons == null) return;

            for (var i = 0; i < actionButtons.Length; i++)
            {
                var btn = actionButtons[i];
                if (btn == null) continue;

                if (selectedIndex == -1)
                {
                    var tempConfig = btn.DefaultConfig.Clone();
                    tempConfig.animDuration = animTime;
                    btn.SetProperty(tempConfig);
                    btn.PlayOverrideEffect();
                    DOVirtual.DelayedCall(animTime, btn.ClearProperty);
                }
                else
                {
                    var targetColor = i == selectedIndex ? selectedTextColor : hiddenTextColor;
                    var newConfig = btn.DefaultConfig.Clone();
                    newConfig.animDuration = animTime;

                    for (var j = 0; j < newConfig.graphicSettings.Count; j++)
                    {
                        var gConfig = newConfig.graphicSettings[j];
                        if (gConfig.target is TextMeshProUGUI)
                        {
                            gConfig.baseState.useColor = true;
                            gConfig.baseState.color = targetColor;
                            gConfig.hoverState.useColor = true;
                            gConfig.hoverState.color = targetColor;
                            gConfig.leftPressedState.useColor = true;
                            gConfig.leftPressedState.color = targetColor;
                            gConfig.rightPressedState.useColor = true;
                            gConfig.rightPressedState.color = targetColor;
                            gConfig.disabledState.useColor = true;
                            gConfig.disabledState.color = targetColor;

                            if (gConfig.useAlways)
                            {
                                gConfig.alwaysState.useColor = true;
                                gConfig.alwaysState.color = targetColor;
                            }

                            newConfig.graphicSettings[j] = gConfig;
                        }
                    }

                    btn.SetProperty(newConfig);
                    btn.PlayOverrideEffect();
                }
            }
        }

        public void ForceShrinkAndExecute(System.Action onComplete)
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            _transitionCoroutine = StartCoroutine(ShrinkAndExecuteSequence(onComplete));
        }

        private IEnumerator ShrinkAndExecuteSequence(System.Action onComplete)
        {
            // 기존 축소 로직이 완전히 끝날 때까지 대기
            yield return StartCoroutine(ShrinkSequence());

            // 애니메이션 종료 후 전달받은 로직(스팀 로비 생성 등) 실행
            onComplete?.Invoke();
        }
    }
}