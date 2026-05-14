using System.Collections.Generic;
using App.Network;
using Data.Models;
using Domain.Entities;
using TMPro;
using UnityEngine;

namespace Scenes.InGame.Debugging
{
    public class DebugDeckCardUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI orderText;
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private TMP_Dropdown cardSelectDropdown;

        private CardInstance _targetCardInstance;
        private List<Card> _allCardsCache = new List<Card>();

        public void Setup(int orderIndex, CardInstance cardInstance)
        {
            _targetCardInstance = cardInstance;
            orderText.text = $"#{orderIndex}";
            cardNameText.text = cardInstance.BaseData.Name;

            InitializeDropdown();
        }

        private void InitializeDropdown()
        {
            cardSelectDropdown.ClearOptions();
            _allCardsCache.Clear();

            List<string> options = new List<string>();
            int currentIndex = 0;
            int selectedIndex = 0;

            // 카탈로그의 모든 카드를 드롭다운에 추가
            foreach (var card in CardCatalog.Instance.GetAllCards())
            {
                _allCardsCache.Add(card);
                options.Add($"[{card.Id}] {card.Name}");

                if (card.Id == _targetCardInstance.CardId)
                {
                    selectedIndex = currentIndex;
                }

                currentIndex++;
            }

            cardSelectDropdown.AddOptions(options);
            cardSelectDropdown.value = selectedIndex;

            // 리스너 중복 등록 방지
            cardSelectDropdown.onValueChanged.RemoveAllListeners();
            cardSelectDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        private void OnDropdownValueChanged(int index)
        {
            if (_allCardsCache.Count > index)
            {
                int newCardId = _allCardsCache[index].Id;
                // 1. 실제 인스턴스 데이터 강제 변경
                _targetCardInstance.DebugChangeCardId(newCardId);
                cardNameText.text = _allCardsCache[index].Name;

                // 2. 서버 전체 동기화 트리거 (모든 클라이언트의 화면이 새로고침됨)
                if (NetworkGameController.Instance != null)
                {
                    NetworkGameController.Instance.TriggerCardSync();
                    Debug.Log($"<color=orange>[Debug]</color> 덱의 카드가 [{newCardId}]로 강제 교체되었습니다.");
                }
            }
        }
    }
}