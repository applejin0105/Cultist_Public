using Components.Common.Buttons.Core;
using UI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scenes.Main
{
    public class CollectionSelectionUI : MonoBehaviour
    {
        [SerializeField] private CompoundButton confirmButton;

        private void OnEnable()
        {
            confirmButton.IsInteractable = true;
            confirmButton.onLeftClickEvent.AddListener(OnConfirmClicked);
        }

        private void OnDisable()
        {
            confirmButton.onLeftClickEvent.RemoveAllListeners();
        }

        private void OnConfirmClicked()
        {
            confirmButton.IsInteractable = false;

            // 씬에 존재하는 RadialTransitionManager 탐색
            var transitionManager = FindFirstObjectByType<RadialTransitionManager>();

            if (transitionManager != null)
            {
                // 트랜지션 애니메이션 실행 후 로비 생성 진행
                transitionManager.ForceShrinkAndExecute(() => { SceneManager.LoadScene("04_Collection"); });
            }
            else
            {
                // 매니저가 없을 경우 즉시 실행 (Fallback)
                SceneManager.LoadScene("04_Collection");
            }
        }

        private void CloseWindow()
        {
            Destroy(gameObject);
        }
    }
}