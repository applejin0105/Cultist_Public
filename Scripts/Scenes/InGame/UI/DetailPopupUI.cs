using Data.Models;
using Domain.Entities;
using Scenes.InGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UI.Core;

namespace Scenes.InGame.UI
{
    public class DetailPopupUI : MonoBehaviour, IPointerClickHandler
    {
        public static DetailPopupUI Instance { get; private set; }

        [Header("UI Components")]
        [Tooltip("배경과 카드를 포함하는 전체 팝업 오브젝트")]
        [SerializeField] private GameObject popupContainer;
        [Tooltip("화면 중앙에 표시될 거대한 카드 UI")]
        [SerializeField] private CardUIManager cardDetail;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (popupContainer != null) popupContainer.SetActive(false);
        }

        public void ClearData()
        {
            cardDetail.CleanUp();
        }

        /// <summary>
        /// 카드를 상세 보기로 띄웁니다.
        /// </summary>
        /// <param name="cardId">보여줄 카드의 기본 ID</param>
        /// <param name="isHidden">상대방의 엎어진 카드인지 여부 (비밀 보장)</param>
        public void SetUp(int cardId, bool isHidden)
        {
            ClearData();
            if (popupContainer == null || cardDetail == null) return;

            popupContainer.SetActive(true);

            Card baseData = CardCatalog.Instance.Get(cardId);
            if (baseData != null)
            {
                cardDetail.SetupCard(baseData);
            }

            if (isHidden)
            {
                cardDetail.OnCardBack(true);
            }
            else
            {
                cardDetail.OnCardBack(false);
            }

            // [핵심 추가] 순수 심볼 조건만 체크하여 색상(Activated/DisActivated) 결정
            bool isSymbolMet = false;
            if (baseData != null)
            {
                var localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<App.Network.GamePlayer>();
                if (localPlayer != null && InGameUIManager.Instance != null)
                {
                    var myPanel = InGameUIManager.Instance.GetPanelBySeat(localPlayer.seatIndex);
                    if (myPanel != null)
                    {
                        int[] mySymbols = myPanel.GetCurrentSymbols();
                        isSymbolMet = true;
                        for (int i = 0; i < 6; i++)
                        {
                            // 플레이어의 보유 심볼이 카드의 요구 심볼보다 적으면 DisActivated
                            if (mySymbols[i] < baseData.SymbolR[i])
                            {
                                isSymbolMet = false;
                                break;
                            }
                        }
                    }
                }
            }

            // 색상 갱신 적용
            cardDetail.ColorChanger(isSymbolMet);
        }

        public void Close()
        {
            if (popupContainer != null) popupContainer.SetActive(false);
            ClearData();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Close();
        }
    }
}