using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using App.Network;
using Mirror;

namespace Scenes.InGame.UI
{
    public class SlotUI : MonoBehaviour, IPointerClickHandler
    {
        public int ParentId { get; private set; }
        public int SlotIndex { get; private set; }
        public int HandCardId { get; private set; }

        public void Setup(int parentId, int index, int handCardId)
        {
            ParentId = parentId;
            SlotIndex = index;
            HandCardId = handCardId;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            if (localPlayer != null)
            {
                Debug.Log($"[Slot] {ParentId}의 {SlotIndex}번 위치에 {HandCardId} 배치 확정!");
                // 1~3단계에서 수정한 서버 Command 호출
                localPlayer.Cmd_PlayCardOnField(HandCardId, ParentId, SlotIndex);

                // 배치 후 즉시 로컬 슬롯 제거
                var manager = Object.FindFirstObjectByType<Core.ClientCardManager>();
                manager?.CloseSlots();
            }
        }
    }
}