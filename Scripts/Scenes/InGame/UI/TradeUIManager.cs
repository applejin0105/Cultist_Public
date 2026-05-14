using UnityEngine;
using UnityEngine.UI;
using Mirror;
using App.Network;
using Domain.Enums;

namespace Scenes.InGame.UI
{
    public class TradeUIManager : MonoBehaviour
    {
        public static TradeUIManager Instance { get; private set; }

        [Header("Trade Panel")]
        public GameObject tradePanel;
        [SerializeField] private CanvasGroup tradeCanvasGroup;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            Instance = this;

            if (tradeCanvasGroup == null && tradePanel != null)
                tradeCanvasGroup = tradePanel.GetComponent<CanvasGroup>();

            // 풀스크린 패널 루트 Image가 RaycastTarget=true 이면 Trade 버튼 클릭을 가로챔.
            // 자식(ScrollView/카드)의 raycast 는 그대로 살아 있으므로 루트만 비활성화.
            if (tradePanel != null)
            {
                var rootImage = tradePanel.GetComponent<Image>();
                if (rootImage != null) rootImage.raycastTarget = false;
            }

            // CanvasGroup 방식은 GameObject가 항상 켜져 있어야 자식(ScrollView 등)이 정상 초기화됨
            if (tradePanel != null && !tradePanel.activeSelf)
                tradePanel.SetActive(true);

            ApplyState(false);
        }

        public void ToggleTradePanel() => SetTradePanelOpen(!IsOpen);

        public void OpenTradePanel() => SetTradePanelOpen(true);

        public void CloseTradePanel() => SetTradePanelOpen(false);

        private void SetTradePanelOpen(bool open)
        {
            ApplyState(open);
            if (open) RefreshTradeDisplay();
        }

        private void ApplyState(bool open)
        {
            IsOpen = open;
            if (tradeCanvasGroup == null) return;

            tradeCanvasGroup.alpha = open ? 1f : 0f;
            tradeCanvasGroup.interactable = open;
            tradeCanvasGroup.blocksRaycasts = open;
        }

        private void RefreshTradeDisplay()
        {
            var controller = FindFirstObjectByType<NetworkGameController>();
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();

            if (controller == null || localPlayer == null) return;

            bool canBuy = controller.CurrentActivePlayerSeat == localPlayer.seatIndex &&
                          controller.CurrentPhaseMain == (int)Phase.Main.Draw;
        }
    }
}