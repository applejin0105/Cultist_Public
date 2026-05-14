using Mirror;
using UnityEngine;
using App.Network;
using Components.Common.Buttons.Core;

namespace Scenes.InGame.UI
{
    public class TurnEndButtonUI : MonoBehaviour
    {
        [SerializeField] private CompoundButton endTurnButton;

        private void OnEnable()
        {
            if (endTurnButton != null)
                endTurnButton.onLeftClickEvent.AddListener(OnClickEndTurn);
        }

        private void OnDisable()
        {
            if (endTurnButton != null)
                endTurnButton.onLeftClickEvent.RemoveListener(OnClickEndTurn);
        }

        private void OnClickEndTurn()
        {
            if (NetworkClient.localPlayer != null)
            {
                var localGamePlayer = NetworkClient.localPlayer.GetComponent<GamePlayer>();
                if (localGamePlayer != null)
                {
                    localGamePlayer.RequestAdvancePhaseOrEndTurn();
                }
            }
        }
    }
}