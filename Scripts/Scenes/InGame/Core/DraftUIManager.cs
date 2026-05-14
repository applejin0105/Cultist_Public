using System.Collections.Generic;
using App.Network;
using Components.Common.Buttons.Core;
using Domain.Enums;
using Mirror;
using TMPro;
using UI.Core;
using UnityEngine;
using Scenes.InGame.UI;

namespace Scenes.InGame.Core
{
    public class DraftUIManager : MonoBehaviour
    {
        public static DraftUIManager Instance { get; private set; }

        [Header("UI Components")]
        public GameObject draftPanel;
        public Transform cardsContainer;

        public InGameCardUI cardPrefab;

        [Header("Hide Feature (Single Button Toggle)")]
        public CompoundButton hideButton;
        public TextMeshProUGUI hideButtonText;

        private readonly List<InGameCardUI> _spawnedCards = new List<InGameCardUI>();
        private bool _isDrafting = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            draftPanel.SetActive(false);
            if (hideButton != null) hideButton.gameObject.SetActive(false);

            if (hideButton != null)
            {
                hideButton.onLeftClickEvent.AddListener(ToggleDraftPanel);
            }
        }

        public void ShowDraft(int[] instanceIds, int[] cardIds)
        {
            ClearCards();
            _isDrafting = true;

            if (InGameUIManager.Instance != null)
            {
                InGameUIManager.Instance.SetActionButtonsInteractable(false, false);
            }

            for (int i = 0; i < instanceIds.Length; i++)
            {
                int instanceId = instanceIds[i];
                int cardId = cardIds[i];

                var actualData = GetSyncData(instanceId, cardId);

                actualData.Status = (int)CardStatus.Draft;

                InGameCardUI newCard = Instantiate(cardPrefab, cardsContainer);

                newCard.Setup(actualData, true);

                _spawnedCards.Add(newCard);
            }

            if (hideButton != null) hideButton.gameObject.SetActive(true);
            SetDraftVisibility(true);
        }

        private NetworkDTOs.CardNetData GetSyncData(int instanceId, int cardId)
        {
            if (NetworkGameController.Instance != null)
            {
                foreach (var netData in NetworkGameController.Instance.SyncCards)
                {
                    if (netData.InstanceId == instanceId) return netData;
                }
            }

            Debug.LogWarning($"[DraftUI] SyncCards에서 ID {instanceId}를 찾지 못해 임시 데이터를 생성합니다.");
            return new NetworkDTOs.CardNetData
            {
                InstanceId = instanceId,
                CardId = cardId,
                OwnerSeat = NetworkClient.localPlayer.GetComponent<GamePlayer>().seatIndex,
                Zone = (int)Zone.Deck,
                Status = (int)CardStatus.Draft // <--- 상태를 Draft로 강제 지정
            };
        }

        private void ToggleDraftPanel()
        {
            if (!_isDrafting) return;
            SetDraftVisibility(!draftPanel.activeSelf);
        }

        private void SetDraftVisibility(bool isVisible)
        {
            draftPanel.SetActive(isVisible);
            if (hideButtonText != null)
            {
                hideButtonText.text = isVisible ? "Hide" : "Show";
            }
        }

        private void ClearCards()
        {
            foreach (var card in _spawnedCards)
            {
                if (card != null) Destroy(card.gameObject);
            }

            _spawnedCards.Clear();
        }

        public void CloseDraft()
        {
            _isDrafting = false;
            draftPanel.SetActive(false);
            if (hideButton != null) hideButton.gameObject.SetActive(false);
            ClearCards();
        }

        public bool HasForceSelectCard()
        {
            foreach (var card in _spawnedCards)
            {
                if (card == null) continue;
                var baseData = Data.Models.CardCatalog.Instance.Get(card.CardId);
                if (baseData != null && baseData.IsForceSelect) return true;
            }

            return false;
        }
    }
}