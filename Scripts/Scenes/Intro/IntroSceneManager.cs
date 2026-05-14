using System.Collections;
using System.Collections.Generic;
using Components.Common.Buttons.Core;
using Components.Effects.UI.Types;
using Core.Data.Enums;
using Core.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scenes.Intro
{
    public class IntroSceneManager : MonoBehaviour
    {
        [SerializeField] private List<BGMSoundType> introBGMType;
        [SerializeField] private CardSoundType introCardSoundType;
        private float _introBGMDuration;

        [SerializeField] private UIFadeCanvasGroupEffect introLogoCanvasGroupEffect;
        [SerializeField] private UIFadeCanvasGroupEffect introClickPanelCanvasGroupEffect;

        [SerializeField] private UIFadeCanvasGroupEffect textFadeCanvasGroupEffect;
        [SerializeField] private UIMoveEffect textMoveEffect;

        [SerializeField] private CanvasGroup introLogoCanvasGroup;
        [SerializeField] private CanvasGroup introClickPanelCanvasGroup;

        [SerializeField] private CompoundButton introCompoundButton;

        private FadeCanvasGroupConfig _introLogoCanvasGroupConfig;
        private FadeCanvasGroupConfig _introClickPanelCanvasGroupConfig;

        private void Start()
        {
            if (introLogoCanvasGroup == null || introClickPanelCanvasGroup == null ||
                introLogoCanvasGroupEffect == null || introClickPanelCanvasGroupEffect == null) return;

            ResetToFadeIn();

            StartCoroutine(PlayIntroRoutine());
        }

        private void ResetToFadeIn()
        {
            introLogoCanvasGroup.alpha = 0;
            introLogoCanvasGroup.blocksRaycasts = false;
            introLogoCanvasGroup.interactable = false;
            _introLogoCanvasGroupConfig = introLogoCanvasGroupEffect.DefaultConfig;
            _introLogoCanvasGroupConfig.endAlpha = 1.0f;

            introLogoCanvasGroupEffect.SetProperty(_introLogoCanvasGroupConfig);

            introClickPanelCanvasGroup.alpha = 0;
            introClickPanelCanvasGroup.blocksRaycasts = false;
            introClickPanelCanvasGroup.interactable = false;
            _introClickPanelCanvasGroupConfig = introClickPanelCanvasGroupEffect.DefaultConfig;
            _introClickPanelCanvasGroupConfig.endAlpha = 1.0f;

            introClickPanelCanvasGroupEffect.SetProperty(_introClickPanelCanvasGroupConfig);
        }

        private void ResetToFadeOut()
        {
            introLogoCanvasGroup.alpha = 0;

            introClickPanelCanvasGroup.alpha = 1;
            introClickPanelCanvasGroup.blocksRaycasts = false;
            introClickPanelCanvasGroup.interactable = false;
            _introClickPanelCanvasGroupConfig = introClickPanelCanvasGroupEffect.DefaultConfig;
            _introClickPanelCanvasGroupConfig.endAlpha = 0.0f;

            introClickPanelCanvasGroupEffect.SetProperty(_introClickPanelCanvasGroupConfig);
        }

        private void ButtonReady()
        {
            introClickPanelCanvasGroup.blocksRaycasts = true;
            introClickPanelCanvasGroup.interactable = true;

            Debug.Log($"[IntroSceneManager] Button Ready");
        }


        void OnEnable()
        {
            introCompoundButton.onLeftClickEvent.AddListener(OnLoadMainScene);
        }

        void OnDisable()
        {
            introCompoundButton.onLeftClickEvent.RemoveListener(OnLoadMainScene);
        }

        // 아무래도 추후에 각 Effect들 사운드 로직을 손봐야할듯 함.
        // 로직 수행 중 특별한 사운드 출력시키고 싶은데
        // Enum 형식이 달라서 하려면 뜯어 고쳐야하는데
        // 그렇다고 매번 뭐 할때마다 enum 대로 추가하긴 어렵고
        // 뭔가 enum을 클래스화 해서 Parent enum 같은건 못만들까
        // Sound enum 전체를 드롭박스 형태로 만들어서 추가만 하면 자유롭게 쓸 수 있게...
        // 그걸 할 수 있게 되면 리팩토링 해보자!!

        private IEnumerator PlayIntroRoutine()
        {
            int random = Random.Range(0, 2);
            SoundManager.Instance.PlayBgm(introBGMType[random]);
            yield return new WaitForSeconds(0.5f);
            introLogoCanvasGroupEffect.PlayOverrideEffect();
            SoundManager.Instance.PlayCardSound(introCardSoundType);
            yield return new WaitForSeconds(5.0f);
            ButtonReady();
            introClickPanelCanvasGroupEffect.PlayOverrideEffect();
            textFadeCanvasGroupEffect.PlayOverrideEffect();
            textMoveEffect.PlayOverrideEffect();
        }

        private void OnLoadMainScene()
        {
            ResetToFadeOut();
            SoundManager.Instance.StopBgm();
            SoundManager.Instance.PlaySfx(UISoundType.Bell);
            introClickPanelCanvasGroupEffect.PlayOverrideEffect();
            StartCoroutine(LoadNextScene());
        }

        private IEnumerator LoadNextScene()
        {
            yield return new WaitForSeconds(3.0f);
            SceneManager.LoadScene("01_Main");
        }
    }
}