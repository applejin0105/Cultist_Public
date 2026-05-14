using Components.Common.Buttons.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scenes.Tutorial
{
    public class TutorialSelectionUI : MonoBehaviour
    {
        [Header("Tutorial Button")]
        [SerializeField] private CompoundButton tutorialButton;


        private void OnEnable()
        {
            SetButtonsInteractable(true);

            tutorialButton.onLeftClickEvent.AddListener(OnTutorialButtonClicked);
        }

        private void OnDisable()
        {
            tutorialButton.onLeftClickEvent.RemoveListener(OnTutorialButtonClicked);
        }


        private void SetButtonsInteractable(bool state)
        {
            tutorialButton.IsInteractable = state;
        }

        private void OnTutorialButtonClicked()
        {
            SetButtonsInteractable(false);

            SceneManager.LoadScene("02_Tutorial");
        }
    }
}