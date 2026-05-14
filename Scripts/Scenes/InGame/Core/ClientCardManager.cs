using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using App.Network;
using Domain.Enums;
using Domain.Structure.Field;
using Scenes.InGame.UI;

namespace Scenes.InGame.Core
{
    public class ClientCardManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private InGameCardUI inGameCardPrefab;

        [Header("Layout Parents")]
        [SerializeField] private Transform myHandTransform;
        [SerializeField] private Transform tradeGridTransform;

        [SerializeField] private SlotUI slotPrefab;

        private int _pendingParentId = -1;
        private int _pendingHandCardId = -1;
        private List<int> _activeSlotIds = new List<int>();
        private readonly Dictionary<int, SlotUI> _spawnedSlots = new Dictionary<int, SlotUI>();

        private NetworkGameController _controller;
        private readonly Dictionary<int, InGameCardUI> _spawnedCards = new Dictionary<int, InGameCardUI>();

        private HashSet<int> _myFieldCardHistory = new HashSet<int>();

        private int _myLastActionId = -1;

        private bool _isDirty = false;

        // 타겟 선택 관련
        public bool IsTargetSelectionMode => _targetSelectionCandidateIds.Count > 0;
        public int TargetSelectionMin => _targetSelectionMin;

        private List<int> _targetSelectionCandidateIds = new List<int>();
        private int _targetSelectionMin = 0;
        private int _targetSelectionMax = 0;
        private bool _targetSelectionSingleOwner = false;
        private Player? _lockedTargetOwner = null;
        private List<int> _selectedTargetIds = new List<int>();

        private bool _isSubscribedToTargetSelection = false;

        private void Update()
        {
            if (_controller == null && NetworkClient.active)
            {
                _controller = FindFirstObjectByType<NetworkGameController>();
                if (_controller != null)
                {
                    _controller.SyncCards.OnChange += OnSyncCardsChanged;
                    SyncCardsChanged();
                }
            }

            // [수정] localPlayer가 준비되었을 때 이벤트 구독 (딱 한 번만)
            if (!_isSubscribedToTargetSelection)
            {
                var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
                if (localPlayer != null)
                {
                    localPlayer.OnTargetSelectionRequested += StartTargetSelection;
                    _isSubscribedToTargetSelection = true;
                    Debug.Log("[ClientCardManager] TargetSelection 이벤트 구독 완료");
                }
            }

            if (_controller != null)
            {
                foreach (var kvp in _spawnedCards)
                {
                    if (kvp.Value != null && kvp.Value.transform.parent == null)
                    {
                        var netData = _controller.SyncCards.Find(x => x.InstanceId == kvp.Key);
                        if (netData.InstanceId != 0)
                        {
                            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
                            bool isMine = localPlayer != null && netData.OwnerSeat == localPlayer.seatIndex;
                            AssignParentTransform(kvp.Value, netData, isMine);
                        }
                    }
                }
            }
        }

        private void StartTargetSelection(List<int> candidates, int min, int max, bool singleOwner)
        {
            Debug.Log(
                $"<color=cyan>[TargetSelection]</color> 모드 진입. 후보:{candidates.Count}개, 요구:{min}~{max}개, SingleOwner:{singleOwner}");
            _targetSelectionCandidateIds = candidates;
            _targetSelectionMin = min;
            _targetSelectionMax = max;
            _targetSelectionSingleOwner = singleOwner;
            _lockedTargetOwner = null;
            _selectedTargetIds.Clear();

            // 후보 카드들 하이라이트 등 시각 처리
            foreach (var id in candidates)
            {
                if (_spawnedCards.TryGetValue(id, out var ui))
                {
                    // ui.SetTargetCandidateHighlight(true);
                }
            }
        }

        public bool TrySelectTarget(int instanceId)
        {
            if (_targetSelectionCandidateIds.Count == 0) return false;

            if (!_targetSelectionCandidateIds.Contains(instanceId))
            {
                Debug.LogWarning($"[ClientCardManager] 선택 가능한 타겟이 아닙니다. (ID: {instanceId})");
                return false;
            }

            if (_spawnedCards.TryGetValue(instanceId, out var targetUI))
            {
                Player cardOwner = (Player)targetUI.OwnerSeat;

                // SingleOwner 제약 조건 체크
                if (_targetSelectionSingleOwner && _lockedTargetOwner.HasValue && _lockedTargetOwner.Value != cardOwner)
                {
                    if (!_selectedTargetIds.Contains(instanceId))
                    {
                        Debug.LogWarning(
                            $"[ClientCardManager] 이미 플레이어 {_lockedTargetOwner.Value}를 선택했으므로 다른 플레이어의 카드를 선택할 수 없습니다.");
                        return true; // 소비는 하되 무시
                    }
                }

                if (_selectedTargetIds.Contains(instanceId))
                {
                    _selectedTargetIds.Remove(instanceId);
                    Debug.Log($"[ClientCardManager] 타겟 선택 취소: {instanceId}");

                    // 모든 선택이 취소되면 Lock 해제
                    if (_selectedTargetIds.Count == 0) _lockedTargetOwner = null;
                }
                else
                {
                    if (_selectedTargetIds.Count < _targetSelectionMax)
                    {
                        // 첫 번째 카드 선택 시 Owner Lock
                        if (_selectedTargetIds.Count == 0) _lockedTargetOwner = cardOwner;

                        _selectedTargetIds.Add(instanceId);
                        Debug.Log(
                            $"[ClientCardManager] 타겟 선택: {instanceId} (Owner: {cardOwner}, 현재 {_selectedTargetIds.Count}/{_targetSelectionMax})");
                    }
                }
            }

            // 요구 수량 충족 시 자동 제출
            if (_selectedTargetIds.Count >= _targetSelectionMin && _selectedTargetIds.Count <= _targetSelectionMax)
            {
                if (_targetSelectionMax == 1 || _selectedTargetIds.Count == _targetSelectionMax)
                {
                    SubmitSelectedTargets();
                }
            }

            return true;
        }

        public void FinishTargetSelection()
        {
            if (!IsTargetSelectionMode) return;
            SubmitSelectedTargets();
        }

        private void SubmitSelectedTargets()
        {
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            if (localPlayer != null)
            {
                Debug.Log($"[ClientCardManager] 타겟 {string.Join(", ", _selectedTargetIds)} 제출");
                localPlayer.Cmd_SubmitTargets(_selectedTargetIds.ToArray());
            }

            _targetSelectionCandidateIds.Clear();
            _selectedTargetIds.Clear();
            _lockedTargetOwner = null;
        }

        private void OnSyncCardsChanged(SyncList<NetworkDTOs.CardNetData>.Operation op, int itemIndex,
            NetworkDTOs.CardNetData item)
        {
            _isDirty = true;
        }

        private void LateUpdate()
        {
            if (_isDirty)
            {
                SyncCardsChanged();
                _isDirty = false;
            }
        }

        private void SyncCardsChanged()
        {
            var localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<App.Network.GamePlayer>();
            if (localPlayer == null) return;

            int localSeat = localPlayer.seatIndex;

            HashSet<int> activeIds = new HashSet<int>();
            HashSet<int> activeFieldSeats = new HashSet<int>();

            Debug.Log($"[Debug 1] SyncCardsChanged 시작. 서버가 보낸 총 카드 수: {_controller.SyncCards.Count}");

            foreach (var netData in _controller.SyncCards)
            {
                activeIds.Add(netData.InstanceId);
                bool isMine = (netData.OwnerSeat == localSeat);

                if (netData.Zone == (int)Zone.Deck)
                {
                    if (_spawnedCards.ContainsKey(netData.InstanceId))
                    {
                        Destroy(_spawnedCards[netData.InstanceId].gameObject);
                        _spawnedCards.Remove(netData.InstanceId);
                    }

                    continue;
                }

                if (netData.Zone == (int)Zone.Field)
                {
                    activeFieldSeats.Add(netData.OwnerSeat);

                    Debug.Log($"[Debug 2] 필드 카드 인식! OwnerSeat: {netData.OwnerSeat}, InstanceId: {netData.InstanceId}");

                    if (netData.OwnerSeat == localSeat && !_myFieldCardHistory.Contains(netData.InstanceId))
                    {
                        _myLastActionId = netData.InstanceId;
                        _myFieldCardHistory.Add(netData.InstanceId);
                        Debug.Log($"<color=cyan>[Focus]</color> 신규 카드 배치 감지: {_myLastActionId}");
                    }
                }

                InGameCardUI cardUI;
                if (!_spawnedCards.TryGetValue(netData.InstanceId, out cardUI))
                {
                    cardUI = Instantiate(inGameCardPrefab);
                    cardUI.gameObject.SetActive(true); // [추가] 프리팹 상태와 관계없이 활성화 보장
                    _spawnedCards[netData.InstanceId] = cardUI;
                }

                try
                {
                    cardUI.Setup(netData, isMine);
                    AssignParentTransform(cardUI, netData, isMine);

                    if (netData.InstanceId == _pendingHandCardId)
                    {
                        cardUI.SetPendingPlayState(true);
                    }
                    else if (netData.Zone == (int)Zone.Hand && isMine && _pendingHandCardId != -1)
                    {
                        cardUI.SetUnfocusedHandState(true);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ClientCardManager] 카드 시각화 실패 (ID:{netData.InstanceId}): {e}");
                }
            }

            List<int> toRemove = new List<int>();
            foreach (var spawned in _spawnedCards)
            {
                if (!activeIds.Contains(spawned.Key))
                {
                    if (spawned.Value != null) Destroy(spawned.Value.gameObject);
                    toRemove.Add(spawned.Key);
                }
            }

            foreach (var id in toRemove) _spawnedCards.Remove(id);

            Debug.Log($"[Debug 3] 루프 종료. activeFieldSeats 개수: {activeFieldSeats.Count} (0이면 트리를 안 만듦)");

            foreach (int targetSeat in activeFieldSeats)
            {
                Debug.Log($"[Debug 4] BuildAndSyncFieldTree 호출! 대상 시트: {targetSeat}, 내 시트: {localSeat}");
                BuildAndSyncFieldTree(targetSeat, localSeat);
            }
        }

        private void BuildAndSyncFieldTree(int targetSeat, int localSeat)
        {
            Debug.Log($"[Debug 5] BuildAndSyncFieldTree 진입 완료. 타겟 시트: {targetSeat}");

            FieldTree localFieldTree = new FieldTree();

            Dictionary<int, RectTransform> fieldCardUIs = new Dictionary<int, RectTransform>();
            var rootData = _controller.SyncCards.FirstOrDefault(c =>
                c.OwnerSeat == targetSeat && c.Zone == (int)Zone.Field && c.ParentInstanceId == 0);
            int expectedRootId = rootData.InstanceId;

            foreach (var netData in _controller.SyncCards)
            {
                if (netData.OwnerSeat == targetSeat && netData.Zone == (int)Zone.Field)
                {
                    int? parentId = (netData.ParentInstanceId == 0 ||
                                     (expectedRootId != 0 && netData.InstanceId == expectedRootId))
                        ? (int?)null
                        : netData.ParentInstanceId;

                    FieldNode node = new FieldNode(netData.InstanceId, parentId);
                    localFieldTree.AddNode(node);

                    if (_spawnedCards.TryGetValue(netData.InstanceId, out InGameCardUI ui))
                    {
                        fieldCardUIs.Add(netData.InstanceId, ui.GetComponent<RectTransform>());
                    }
                }
            }


            var childrenMap = new Dictionary<int, List<(int childId, int index)>>();

            foreach (var netData in _controller.SyncCards)
            {
                if (netData.OwnerSeat == targetSeat && netData.Zone == (int)Zone.Field && netData.ParentInstanceId != 0)
                {
                    if (!childrenMap.ContainsKey(netData.ParentInstanceId))
                        childrenMap[netData.ParentInstanceId] = new List<(int, int)>();

                    childrenMap[netData.ParentInstanceId].Add((netData.InstanceId, netData.SiblingIndex));
                }
            }

            foreach (var kvp in childrenMap)
            {
                kvp.Value.Sort((a, b) => a.index.CompareTo(b.index));

                var parentNode = localFieldTree.GetNodeByInstanceId(kvp.Key);

                if (parentNode != null)
                {
                    foreach (var child in kvp.Value)
                    {
                        parentNode.AddChild(child.childId);
                    }
                }
            }

            if (targetSeat == localSeat && _pendingParentId != -1)
            {
                var parentNode = localFieldTree.GetNodeByInstanceId(_pendingParentId);
                var localPlayerComp = NetworkClient.localPlayer?.GetComponent<GamePlayer>();

                Debug.Log(
                    $"<color=green>[Client Junction Debug]</color> _pendingParentId: {_pendingParentId}, 노드 발견: {parentNode != null}");

                if (parentNode != null && localPlayerComp != null)
                {
                    var parentNetData = _controller.SyncCards.FirstOrDefault(c => c.InstanceId == _pendingParentId);

                    if (parentNetData.InstanceId == 0)
                    {
                        Debug.LogError(
                            $"<color=red>[Client Junction Error]</color> SyncCards에서 {_pendingParentId} 정보를 찾을 수 없습니다!");
                    }

                    var parentBaseData = (parentNetData.InstanceId != 0)
                        ? Data.Models.CardCatalog.Instance.Get(parentNetData.CardId)
                        : null;

                    int allowedJunction = 1;
                    if (parentBaseData != null)
                    {
                        allowedJunction = (parentNetData.Status == (int)CardStatus.FieldFront)
                            ? parentBaseData.Junction
                            : 1;
                    }

                    int currentCount = parentNode.ChildrenInstanceIds.Count;
                    Debug.Log(
                        $"<color=green>[Client Slot Inject]</color> 부모 {_pendingParentId}({parentBaseData?.Name}) 슬롯 체크 시작 -> 현재 자식: {currentCount} / 허용: {allowedJunction}");

                    if (currentCount < allowedJunction)
                    {
                        bool isBranching = (currentCount >= 1);
                        int extraUsed = 0;
                        foreach (var n in localFieldTree.Nodes.Values)
                            extraUsed += Mathf.Max(0, n.ChildrenInstanceIds.Count - 1);

                        if (isBranching && extraUsed >= localPlayerComp.maxJunction)
                        {
                            Debug.LogWarning(
                                $"<color=red>[Client Slot Block]</color> 플레이어 MaxJunction({localPlayerComp.maxJunction}) 도달로 추가 분기 슬롯을 생성하지 않습니다.");
                        }
                        else
                        {
                            InjectGhostSlots(localFieldTree, parentNode, fieldCardUIs, allowedJunction, targetSeat);
                        }
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"<color=red>[Client Slot Block]</color> 카드 개별 Junction({allowedJunction}) 한계 도달. (현재 자식: {currentCount})");
                    }
                }
            }


            Debug.Log($"[Debug 6] 트리 조립 완료. 노드 개수: {localFieldTree.Count}");

            if (localFieldTree.Count > 0)
            {
                Transform targetField = InGameUIManager.Instance.GetPlayerFieldTransform(targetSeat);
                Debug.Log($"[Debug 7] 타겟 필드 찾기 결과: {(targetField != null ? targetField.name : "NULL")}");
                if (targetField != null)
                {
                    FieldLayoutManager layoutManager = targetField.GetComponent<FieldLayoutManager>()
                                                       ?? targetField.GetComponentInChildren<FieldLayoutManager>()
                                                       ?? targetField.GetComponentInParent<FieldLayoutManager>();

                    Debug.Log($"[Debug 8] FieldLayoutManager 스크립트 찾기: {(layoutManager != null ? "성공" : "실패")}");

                    if (layoutManager != null)
                    {
                        int focusId = (targetSeat == localSeat && _myLastActionId != -1)
                            ? _myLastActionId
                            : expectedRootId;

                        Debug.Log(
                            $"[Focus Debug 2] BuildAndSyncFieldTree - 타겟시트: {targetSeat}, 로컬시트: {localSeat}, _myLastPlacedId: {_myLastActionId} => 최종 결정된 focusId: {focusId}");
                        layoutManager.SyncLayout(localFieldTree, expectedRootId, fieldCardUIs, focusId);
                    }
                    else
                    {
                        Debug.LogError(
                            $"[ClientCardManager] {targetSeat}번 플레이어의 필드({targetField.name}) 주변에서 FieldLayoutManager 스크립트를 찾을 수 없습니다! 프리팹에 스크립트가 부착되어 있는지 확인하세요.");
                    }
                }
            }

            if (targetSeat == localSeat)
            {
                foreach (var slotId in _activeSlotIds)
                {
                    if (fieldCardUIs.TryGetValue(slotId, out RectTransform slotRect))
                    {
                        // 좌표가 이미 계산되었으므로 UI만 업데이트하면 됨
                    }
                }
            }
        }

        private void InjectGhostSlots(FieldTree tree, FieldNode parentNode, Dictionary<int, RectTransform> fieldCardUIs,
            int allowedJunction, int targetSeat)
        {
            _activeSlotIds.Clear();

            List<int> originalChildren = new List<int>(parentNode.ChildrenInstanceIds);
            parentNode.ChildrenInstanceIds.Clear();

            int slotsToCreate = originalChildren.Count + 1;

            for (int i = 0; i < slotsToCreate; i++)
            {
                int slotId = -1000 - i;
                _activeSlotIds.Add(slotId);

                tree.AddNode(new FieldNode(slotId, parentNode.InstanceId));

                parentNode.ChildrenInstanceIds.Add(slotId);

                if (i < originalChildren.Count)
                {
                    parentNode.ChildrenInstanceIds.Add(originalChildren[i]);
                }

                if (!_spawnedSlots.ContainsKey(slotId))
                {
                    SlotUI slot = Instantiate(slotPrefab,
                        InGameUIManager.Instance.GetPlayerFieldTransform(targetSeat));

                    slot.Setup(parentNode.InstanceId, i, _pendingHandCardId);
                    _spawnedSlots[slotId] = slot;
                }

                if (!fieldCardUIs.ContainsKey(slotId))
                {
                    fieldCardUIs.Add(slotId, _spawnedSlots[slotId].GetComponent<RectTransform>());
                }
            }
        }

        private void AssignParentTransform(InGameCardUI cardUI, App.Network.NetworkDTOs.CardNetData netData,
            bool isMine)
        {
            if (cardUI == null || cardUI.IsDragging) return; // [추가] cardUI 널 체크

            Transform targetParent = null;

            if (netData.Zone == (int)Zone.Hand && isMine)
            {
                if (myHandTransform != null)
                {
                    Transform actualHandContainer = myHandTransform.Find("Hand");
                    targetParent = actualHandContainer != null ? actualHandContainer : myHandTransform;
                }
            }
            else if (netData.Zone == (int)Zone.Trade)
            {
                targetParent = tradeGridTransform;
            }
            else if (netData.Zone == (int)Zone.Field)
            {
                // [핵심 추가] InGameUIManager.Instance가 미처 준비되기 전에 접근하는 NRE 방어
                if (InGameUIManager.Instance != null)
                {
                    targetParent = InGameUIManager.Instance.GetPlayerFieldTransform(netData.OwnerSeat);
                }
            }

            if (targetParent != null && cardUI.transform.parent != targetParent)
            {
                cardUI.transform.SetParent(targetParent, false);

                RectTransform rect = cardUI.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                }

                cardUI.transform.localScale = Vector3.one;
                cardUI.transform.localPosition = Vector3.zero;
            }
        }

        public void TriggerFocus(int targetSeat)
        {
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            if (localPlayer == null) return;
            int localSeat = localPlayer.seatIndex;

            // root ID를 찾는 방식 변경 (SyncCards에서 해당 seat의 parent가 없는 카드 찾기)
            var rootData = _controller.SyncCards.FirstOrDefault(c =>
                c.OwnerSeat == targetSeat && c.Zone == (int)Zone.Field && c.ParentInstanceId == 0);
            if (rootData.InstanceId == 0) return;

            int focusId = (targetSeat == localSeat && _myLastActionId != -1) ? _myLastActionId : rootData.InstanceId;
            Debug.Log($"[Focus Debug 3] TriggerFocus(탭 전환) - 타겟시트: {targetSeat}, 최종 결정된 focusId: {focusId}");

            Transform fieldTransform = InGameUIManager.Instance.GetPlayerFieldTransform(targetSeat);
            if (fieldTransform != null)
            {
                var lm = fieldTransform.GetComponent<FieldLayoutManager>();
                if (lm != null) lm.FocusOnCard(focusId); // 정렬과 별개로 포커스만 따로 실행하는 메서드 호출
            }
        }

        public void ReadyToPlayAtSlots(int parentId, int handCardId)
        {
            CloseSlots();
            _pendingParentId = parentId;
            _pendingHandCardId = handCardId;
            _isDirty = true;

            if (_spawnedCards.TryGetValue(handCardId, out InGameCardUI ui))
            {
                ui.SetPendingPlayState(true);
            }

            // [핵심 추가] 대기 슬롯이 열려있는 동안 나머지 패는 0.4 크기로 찌그러져 있도록 유지
            ShrinkOtherHandCards(handCardId);
        }

        public void NotifyDragStart(int handInstanceCardId)
        {
            ShrinkOtherHandCards(handInstanceCardId);
        }

        public void NotifyDragEnd(int handInstanceCardId)
        {
            if (_pendingHandCardId == -1)
            {
                RestoreAllHandCards();
            }
        }

        public void ShrinkOtherHandCards(int activeInstanceCardId)
        {
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            if (localPlayer == null) return;

            foreach (var kvp in _spawnedCards)
            {
                var card = kvp.Value;
                if (card.CurrentZone == Zone.Hand && card.OwnerSeat == localPlayer.seatIndex)
                {
                    if (card.InstanceId != activeInstanceCardId)
                    {
                        card.SetUnfocusedHandState(true);
                    }
                }
            }
        }

        public void RestoreAllHandCards()
        {
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            if (localPlayer == null) return;

            foreach (var kvp in _spawnedCards)
            {
                var card = kvp.Value;
                if (card.CurrentZone == Zone.Hand && card.OwnerSeat == localPlayer.seatIndex)
                {
                    card.SetUnfocusedHandState(false);
                }
            }
        }

        public void NotifyCardAction(int instanceId)
        {
            _myLastActionId = instanceId;
            _isDirty = true;
            Debug.Log($"<color=cyan>[Focus]</color> 사용자 액션 감지(Use/Reveal): {_myLastActionId}");
        }

        public void CloseSlots()
        {
            if (_pendingHandCardId != -1 && _spawnedCards.TryGetValue(_pendingHandCardId, out InGameCardUI ui))
            {
                ui.SetPendingPlayState(false);
            }

            _pendingParentId = -1;
            _pendingHandCardId = -1;
            foreach (var slot in _spawnedSlots.Values) Destroy(slot.gameObject);
            _spawnedSlots.Clear();
            _activeSlotIds.Clear();
            _isDirty = true;

            RestoreAllHandCards();
        }
    }
}