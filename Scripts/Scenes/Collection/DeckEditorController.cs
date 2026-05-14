using System;
using System.Collections.Generic;
using Components.Common.Buttons.Core;
using TMPro;
using UnityEngine;
using Domain.Entities;
using Data.Models;
using Data.Repositories;
using Scenes.Collection.UI;
using UI.Core;

namespace Scenes.Collection
{
    public class DeckEditorController : MonoBehaviour
    {
        private DeckRepository _deckRepository;
        private bool _isEditingMode = false;
        private bool isReadOnly = false;

        [Header("Main Containers")]
        [SerializeField] private GameObject deckListPanel; // 기존 덱 목록 패널
        [SerializeField] private GameObject deckEditPanel; // 전체 덱 편집 패널 (On/Off 대상)

        [Header("Deck Edit Hierarchy")]
        [SerializeField] private TMP_InputField deckNameInput;
        [SerializeField] private DeckRootCardUI deckRootCard; // 루트 카드 전용 UI 스크립트
        [SerializeField] private Transform deckEditContent; // 카드 프리팹들이 부착될 실제 Parent
        [SerializeField] private TextMeshProUGUI deckCardCount;

        [Header("Prefabs")]
        [SerializeField] private GameObject deckCardPrefab;

        [Header("Buttons")]
        [SerializeField] private CompoundButton saveButton;
        [SerializeField] private CompoundButton cancelButton;
        [SerializeField] private CompoundButton createNewDeckButton;

        private Dictionary<int, DeckCardUI> _spawnedCardUIs = new Dictionary<int, DeckCardUI>();

        private bool _isSaving = false;

        public Action OnDeckSavedSuccessfully;
        public Action OnExitEditModeAction;

        public void Initialize(DeckRepository deckRepository)
        {
            _deckRepository = deckRepository;

            saveButton.onLeftClickEvent.AddListener(OnSaveClicked);
            cancelButton.onLeftClickEvent.AddListener(OnCancelClicked);
            createNewDeckButton.onLeftClickEvent.AddListener(CreateNewDeck);
        }

        private void OnEnable()
        {
            CardUIManager.onGlobalCardClickedAction += HandleCardClicked;
            DeckSlotUI.OnDeckSelected += OnEnterEditMode;
        }

        private void OnDisable()
        {
            CardUIManager.onGlobalCardClickedAction -= HandleCardClicked;
            DeckSlotUI.OnDeckSelected -= OnEnterEditMode;
        }

        private void SetupDeckCardCount(DeckData manualData = null)
        {
            if (deckCardCount == null) return;

            int totalCards = 0;

            // 수동으로 데이터가 들어온 경우(샘플 덱 등) 해당 데이터를 우선 사용
            if (manualData != null && manualData.cardIds != null)
            {
                totalCards = manualData.cardIds.Count;
            }
            // 그렇지 않으면 리포지토리의 현재 편집 데이터를 사용
            else if (_deckRepository.CurrentDeckData != null && _deckRepository.CurrentDeckData.cardIds != null)
            {
                totalCards = _deckRepository.CurrentDeckData.cardIds.Count;
            }

            deckCardCount.text = $"{totalCards}/30";
        }

        // 덱 선택 시 호출 (조회 또는 수정 모드 진입)
        private async void OnEnterEditMode(DeckData deckData)
        {
            if (deckData == null) return;
            _isEditingMode = true;

            deckListPanel.SetActive(false);
            deckEditPanel.SetActive(true);

            SetupReadOnlyMode(deckData.IsSample);

            // 플레이어 덱일 때만 리포지토리에 로드
            if (!deckData.IsSample)
            {
                await _deckRepository.LoadDeckForEditingAsync(deckData.deckName);
            }

            RefreshEditUI(deckData);

            // 진입 시점에 넘겨받은 deckData로 카운트 강제 설정
            SetupDeckCardCount(deckData);
        }

        // 덱 생성 버튼 클릭 시 호출
        private void CreateNewDeck()
        {
            _isEditingMode = true;
            SetupReadOnlyMode(false);

            deckNameInput.text = "New Deck";
            deckListPanel.SetActive(false);
            deckEditPanel.SetActive(true);

            _deckRepository.ClearCurrentDeck(); // 이전 작업 데이터 비우기
            ClearEditUI();
            SetupDeckCardCount();
        }

        private void SetupReadOnlyMode(bool readOnly)
        {
            isReadOnly = readOnly;
            deckNameInput.interactable = !readOnly;
            saveButton.gameObject.SetActive(!readOnly); // 샘플 덱은 저장 버튼 숨김
        }

        private void RefreshEditUI(DeckData deckData)
        {
            ClearEditUI();
            deckNameInput.text = deckData.deckName;

            // 1. 루트 카드 UI 강제 갱신
            var rootCard = CardCatalog.Instance.Get(deckData.rootCardId);
            if (rootCard != null)
            {
                deckRootCard.Setup(rootCard); // 이 호출이 정확히 일어나야 Root가 바뀜
            }

            // 2. 카드 리스트 생성 시 루트 카드 제외 확인
            Dictionary<int, int> counts = new Dictionary<int, int>();
            foreach (var id in deckData.cardIds)
            {
                // 데이터에 혹시 루트 카드가 포함되어 있더라도 UI에서는 제외
                if (id == deckData.rootCardId) continue;

                if (counts.ContainsKey(id)) counts[id]++;
                else counts[id] = 1;
            }

            foreach (var kvp in counts)
            {
                SpawnOrUpdateCardUI(CardCatalog.Instance.Get(kvp.Key), kvp.Value);
            }

            SortDeckUI();
            SetupDeckCardCount(deckData); // 30/30으로 표시됨
        }

        private void HandleCardClicked(Card card, bool isLeftClick, bool isDeckCard = false)
        {
            if (isReadOnly || !isLeftClick || !_isEditingMode) return;

            // 데이터가 존재하는 경우 (수정 작업)
            if (_deckRepository.CurrentDeckData != null)
            {
                // 루트 카드는 일반 카드 추가 조건(30장 등)을 무시하고 즉시 덮어쓰기
                if (card.IsRoot)
                {
                    _deckRepository.CurrentDeckData.rootCardId = card.Id; // 데이터 강제 교체
                    deckRootCard.Setup(card); // UI 교체
                    SetupDeckCardCount();
                }
                // 일반 카드일 경우 기존 검사 로직 수행
                else if (_deckRepository.AddCardToCurrentDeck(card.Id))
                {
                    int currentCount = GetCardCountInDeck(card.Id);
                    SpawnOrUpdateCardUI(card, currentCount);
                    SortDeckUI();
                    SetupDeckCardCount();
                }
            }
            // 데이터가 빈 상태인 경우 (새 덱의 첫 루트 카드 클릭)
            else if (card.IsRoot)
            {
                if (_deckRepository.CreateNewDeck(deckNameInput.text, card.Id))
                {
                    deckRootCard.Setup(card);
                    SetupDeckCardCount();
                }
            }
        }

        private void SpawnOrUpdateCardUI(Card card, int count)
        {
            if (_spawnedCardUIs.TryGetValue(card.Id, out DeckCardUI existingUI))
            {
                existingUI.UpdateCount(count);
            }
            else
            {
                // deckEditContent 하위에 프리팹 생성
                GameObject obj = Instantiate(deckCardPrefab, deckEditContent);
                DeckCardUI newUI = obj.GetComponent<DeckCardUI>();

                newUI.Setup(card, count);
                newUI.OnRemoveClicked += HandleRemoveCard;

                _spawnedCardUIs.Add(card.Id, newUI);
            }
        }

        private void HandleRemoveCard(int cardId)
        {
            if (isReadOnly || !_isEditingMode) return;

            if (_deckRepository.RemoveCardFromCurrentDeck(cardId))
            {
                int remainingCount = GetCardCountInDeck(cardId);

                if (remainingCount > 0)
                {
                    _spawnedCardUIs[cardId].UpdateCount(remainingCount);
                }
                else
                {
                    Destroy(_spawnedCardUIs[cardId].gameObject);
                    _spawnedCardUIs.Remove(cardId);
                }

                SetupDeckCardCount(); // 카드 삭제 갱신
            }
        }

        private void SortDeckUI()
        {
            List<int> sortedIds = new List<int>(_spawnedCardUIs.Keys);
            sortedIds.Sort((a, b) =>
            {
                var cardA = CardCatalog.Instance.Get(a);
                var cardB = CardCatalog.Instance.Get(b);
                return cardA.Cultist.CompareTo(cardB.Cultist);
            });

            for (int i = 0; i < sortedIds.Count; i++)
            {
                _spawnedCardUIs[sortedIds[i]].transform.SetSiblingIndex(i);
            }
        }

        private int GetCardCountInDeck(int cardId)
        {
            int count = 0;
            if (_deckRepository.CurrentDeckData?.cardIds == null) return 0;
            foreach (var id in _deckRepository.CurrentDeckData.cardIds)
            {
                if (id == cardId) count++;
            }

            return count;
        }

        private void ClearEditUI()
        {
            foreach (Transform child in deckEditContent)
            {
                Destroy(child.gameObject);
            }

            _spawnedCardUIs.Clear();

            if (deckRootCard != null)
            {
                deckRootCard.CleanUp();
            }
        }

        private async void OnSaveClicked()
        {
            // 이미 저장 중이면 리턴
            if (isReadOnly || !_isEditingMode || _isSaving) return;

            _isSaving = true; // 락 온
            saveButton.IsInteractable = false; // 버튼 시각적 비활성화 (선택)

            try
            {
                if (_deckRepository.CurrentDeckData != null)
                    _deckRepository.CurrentDeckData.deckName = deckNameInput.text;

                if (await _deckRepository.SaveCurrentDeckAsync())
                {
                    OnDeckSavedSuccessfully?.Invoke();
                    ExitEditMode();
                }
            }
            finally
            {
                // 로직이 끝나면 반드시 락 해제
                _isSaving = false;
                if (saveButton != null) saveButton.IsInteractable = true;
            }
        }

        private void OnCancelClicked()
        {
            ExitEditMode();
        }

        private void ExitEditMode()
        {
            _isEditingMode = false;

            _deckRepository.ClearCurrentDeck();
            ClearEditUI();
            SetupDeckCardCount();

            deckEditPanel.SetActive(false);
            deckListPanel.SetActive(true);

            // 2. 편집 모드를 나갈 때 이벤트 방출
            OnExitEditModeAction?.Invoke();
        }
    }
}