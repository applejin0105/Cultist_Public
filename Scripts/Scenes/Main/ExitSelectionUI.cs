using App.Network;
using Components.Common.Buttons.Core;
using UI.Core;
using UnityEngine;

namespace Scenes.Main
{
    public class ExitSelectionUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private CompoundButton exitButton;

        private void OnEnable()
        {
            exitButton.IsInteractable = true;
            exitButton.onLeftClickEvent.AddListener(Exit);
        }

        private void OnDisable()
        {
            exitButton.onLeftClickEvent.RemoveAllListeners();
        }

        private void Exit()
        {
            Application.Quit();
        }
    }
}