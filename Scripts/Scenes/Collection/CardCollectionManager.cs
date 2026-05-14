using Data.Models;
using Domain.Entities;
using Domain.Enums;
using UI.Core;
using UnityEngine;

namespace Scenes.Collection
{
    public class CardCollectionManager : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform gridLayoutPanel;

        private async void Start()
        {
            // JSON 기반 정적 데이터 로드 대기
            await CardCatalog.InitializeAsync();

            Debug.Log("[CardCollectionManager] 데이터 로드 완료. 프리팹 생성을 시작합니다.");
            SpawnAllCards();
        }

        private void SpawnAllCards()
        {
            foreach (Card cardData in CardCatalog.Instance.GetAllCards())
            {
                GameObject cardObj = Instantiate(cardPrefab, gridLayoutPanel);
                CardUIManager uiManager = cardObj.GetComponent<CardUIManager>();

                uiManager.SetupCard(cardData);
            }
        }
    }
}