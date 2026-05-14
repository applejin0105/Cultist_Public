using System.Collections.Generic;
using Domain.Structure.Field;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Scenes.Tests
{
    // 기본 골조는 ID Swapping
    // 단순 UI 띄우기가 아니라 내부 데이터 트리 자체를 일시적으로 변형 -> 복구하는 방식
    // 당연하게도 이게 더 효율적이다. 일종의 캐싱이기도 하고.
    // 거기에 이 작업은 서버에서 모든 플레이어에게 뿌리는게 아니라 하나의 플레이어가 자신의 필드에서만 하는 과정이므로
    // 이 방법이 오버헤드도 줄이고 더 효과적이다.
    // 배열 인터리빙(Interleaving): 부모 카드에 자식이 $N$개 있을 때, OpenSlotsFor는 $N+1$개의 가짜 슬롯 ID(음수)를 생성합니다. 이를 기존 자식들 사이사이에 [Slot0, Child0, Slot1, Child1, Slot2] 순서로 재배치합니다.
    // 레이아웃 엔진의 무지(Agnostic Layout): DeterministicTreeLayout 엔진은 노드 ID가 양수인지 음수인지 상관하지 않습니다. 단지 리스트에 들어있는 순서대로 가로 너비를 계산하고 좌표를 할당합니다. 이 덕분에 슬롯이 추가되면 기존 카드들이 공간 확보를 위해 자동으로 밀려나게 됩니다.
    // 승급 및 정밀 타겟팅: 유저가 슬롯을 클릭하면 PlaceRealCardAtSlot이 호출됩니다. 이때 중요한 점은 새로운 자식을 리스트 끝에 추가하는 것이 아니라, 클릭된 슬롯의 인덱스(Index)를 찾아 그 자리에 진짜 카드 ID를 덮어씌우는 것입니다. 이를 통해 유저가 선택한 물리적 위치가 논리적 순서로 완벽하게 변환됩니다.
    // 더블 클릭 방지 및 우선순위: Time.time을 이용한 쿨다운과 거리 기반 타겟팅 로직은 카드와 슬롯이 겹쳐 있을 때 발생할 수 있는 입력 혼선을 차단합니다.
    public class GhostLayoutTesterV5 : MonoBehaviour
    {
        [Header("Settings")]
        public GameObject mockCardPrefab;
        public GameObject linePrefab;

        public float cardWidth = 268.8f;
        public float cardHeight = 420f;
        public float paddingX = 80f;
        public float paddingY = 100f;
        public float lerpSpeed = 15f;
        public int testMaxJunction = 3;

        public Vector2 rootStartPos = new Vector2(0, -400f);

        private DeterministicTreeLayout _layoutEngine;
        private FieldTree _mockTree;

        private Dictionary<int, RectTransform> _cardUIs = new Dictionary<int, RectTransform>();
        private Dictionary<int, UICurvedLine> _lineUIs = new Dictionary<int, UICurvedLine>();
        private Dictionary<int, Vector2> _targetPositions = new Dictionary<int, Vector2>();

        private RectTransform _canvasRect;

        // [상태 관리 핵심]
        private int _selectedParentId = -1;
        private List<int> _activeSlotIds = new List<int>();
        private int _nextInstanceId = 1000;

        // [핵심 추가] 더블 클릭 방지용 타이머
        private float _lastClickTime = 0f;

        private Vector2 TopOffset => new Vector2(0, cardHeight / 2f);
        private Vector2 BottomOffset => new Vector2(0, -cardHeight / 2f);

        void Start()
        {
            _canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            _layoutEngine = new DeterministicTreeLayout(cardWidth, cardHeight, paddingX, paddingY);

            InitializeMockTree();
            RecalculateLayout();
        }

        private void InitializeMockTree()
        {
            _mockTree = new FieldTree();
            int rootId = 100;
            _mockTree.AddNode(new FieldNode(rootId, null));
            CreateCardUI(rootId, Color.white, "Root\n(100)");
        }

        void Update()
        {
            if (Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, mousePos, null,
                out Vector2 localMousePos);

            HandleVisualHover(localMousePos);

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleMouseClick(localMousePos);
            }

            SmoothMoveCards();
            UpdateLines();
        }

        private void HandleVisualHover(Vector2 localMousePos)
        {
            foreach (var slotId in _activeSlotIds)
            {
                if (_targetPositions.TryGetValue(slotId, out Vector2 pos))
                {
                    bool isHover = Vector2.Distance(localMousePos, pos) < 120f;
                    var img = _cardUIs[slotId].GetComponent<Image>();
                    img.color = isHover ? new Color(0f, 1f, 0f, 0.8f) : new Color(0f, 1f, 0f, 0.3f);
                }
            }
        }

        // [핵심 수정] 타겟팅 정밀화 및 디버깅 로그 추가
        private void HandleMouseClick(Vector2 localMousePos)
        {
            // 0.2초 이내의 연속 클릭 무시 (안정성 보장)
            if (Time.time - _lastClickTime < 0.2f)
            {
                Debug.LogWarning("[Debug] 너무 빠른 연속 클릭! (더블 클릭 방지)");
                return;
            }

            _lastClickTime = Time.time;

            int clickedCardId = -1;
            int clickedSlotId = -1;

            float minSlotDist = float.MaxValue;
            float minCardDist = float.MaxValue;

            // 1. 모든 타겟을 검사하여 가장 가까운 카드와 슬롯을 각각 찾음
            foreach (var kvp in _targetPositions)
            {
                float dist = Vector2.Distance(localMousePos, kvp.Value);
                if (dist < 130f)
                {
                    if (kvp.Key <= -1000 && dist < minSlotDist)
                    {
                        clickedSlotId = kvp.Key;
                        minSlotDist = dist;
                    }
                    else if (kvp.Key > 0 && dist < minCardDist)
                    {
                        clickedCardId = kvp.Key;
                        minCardDist = dist;
                    }
                }
            }

            // 2. 로직 분기: 슬롯과 카드가 겹쳐있을 경우 무조건 슬롯을 우선!
            if (clickedSlotId != -1)
            {
                Debug.Log($"<color=green>[Click]</color> 슬롯 {clickedSlotId} 클릭됨. (대상 부모: {_selectedParentId})");
                PlaceRealCardAtSlot(_selectedParentId, clickedSlotId);
            }
            else if (clickedCardId != -1)
            {
                Debug.Log($"<color=cyan>[Click]</color> 카드 {clickedCardId} 클릭됨. 슬롯 전개 시작!");
                OpenSlotsFor(clickedCardId);
            }
            else
            {
                Debug.Log("[Click] 허공 클릭됨.");
            }
        }

        private void PlaceRealCardAtSlot(int parentId, int clickedSlotId)
        {
            int newId = _nextInstanceId++;
            var parentNode = _mockTree.GetNodeByInstanceId(parentId);

            int indexOfSlot = parentNode.ChildrenInstanceIds.IndexOf(clickedSlotId);
            if (indexOfSlot != -1)
            {
                parentNode.ChildrenInstanceIds[indexOfSlot] = newId;
            }

            _mockTree.AddNode(new FieldNode(newId, parentId));

            ClearUnusedSlots(clickedSlotId);

            CreateCardUI(newId, new Color(Random.value, Random.value, Random.value), $"Card\n({newId})");

            _selectedParentId = -1;
            RecalculateLayout();

            // [디버그] 배치 후 부모의 자식 상태 완벽 출력
            PrintTreeState(parentId);
        }

        private void PrintTreeState(int parentId)
        {
            var node = _mockTree.GetNodeByInstanceId(parentId);
            if (node == null) return;
            string childrenStr = string.Join(", ", node.ChildrenInstanceIds);
            Debug.Log($"<color=yellow>[Tree Update]</color> 부모 {parentId}의 현재 자식 목록: [{childrenStr}]");
        }

        private void ClearUnusedSlots(int usedSlotId)
        {
            var parentNode = _mockTree.GetNodeByInstanceId(_selectedParentId);

            foreach (int slotId in _activeSlotIds)
            {
                if (slotId == usedSlotId)
                {
                    if (_cardUIs.ContainsKey(slotId)) DestroyImmediate(_cardUIs[slotId].gameObject);
                    if (_lineUIs.ContainsKey(slotId)) DestroyImmediate(_lineUIs[slotId].gameObject);
                    _cardUIs.Remove(slotId);
                    _lineUIs.Remove(slotId);
                    _mockTree.Nodes.Remove(slotId);
                    continue;
                }

                parentNode.ChildrenInstanceIds.Remove(slotId);
                _mockTree.Nodes.Remove(slotId);

                if (_cardUIs.ContainsKey(slotId))
                {
                    DestroyImmediate(_cardUIs[slotId].gameObject);
                    _cardUIs.Remove(slotId);
                }

                if (_lineUIs.ContainsKey(slotId))
                {
                    DestroyImmediate(_lineUIs[slotId].gameObject);
                    _lineUIs.Remove(slotId);
                }
            }

            _activeSlotIds.Clear();
        }

        private void OpenSlotsFor(int parentId)
        {
            if (_selectedParentId != -1) CloseSlots();

            var parentNode = _mockTree.GetNodeByInstanceId(parentId);
            if (parentNode == null || parentNode.ChildrenInstanceIds.Count >= testMaxJunction)
            {
                Debug.Log($"[Info] 부모 {parentId}는 분기 한계에 도달했거나 존재하지 않습니다.");
                return;
            }

            _selectedParentId = parentId;

            List<int> originalChildren = new List<int>(parentNode.ChildrenInstanceIds);
            parentNode.ChildrenInstanceIds.Clear();

            int slotsToCreate = originalChildren.Count + 1;

            for (int i = 0; i < slotsToCreate; i++)
            {
                int slotId = -1000 - i;
                _activeSlotIds.Add(slotId);
                _mockTree.AddNode(new FieldNode(slotId, parentId));

                parentNode.ChildrenInstanceIds.Add(slotId);
                if (i < originalChildren.Count) parentNode.ChildrenInstanceIds.Add(originalChildren[i]);

                CreateCardUI(slotId, new Color(0f, 1f, 0f, 0.3f), "SLOT " + i);
            }

            RecalculateLayout();
        }

        private void CloseSlots()
        {
            if (_selectedParentId == -1) return;
            var parentNode = _mockTree.GetNodeByInstanceId(_selectedParentId);

            foreach (int slotId in _activeSlotIds)
            {
                parentNode.ChildrenInstanceIds.Remove(slotId);
                _mockTree.Nodes.Remove(slotId);
                if (_cardUIs.ContainsKey(slotId))
                {
                    DestroyImmediate(_cardUIs[slotId].gameObject);
                    _cardUIs.Remove(slotId);
                }

                if (_lineUIs.ContainsKey(slotId))
                {
                    DestroyImmediate(_lineUIs[slotId].gameObject);
                    _lineUIs.Remove(slotId);
                }
            }

            _activeSlotIds.Clear();
            RecalculateLayout();
        }

        private void RecalculateLayout()
        {
            _targetPositions = _layoutEngine.CalculatePositions(_mockTree, 100, rootStartPos);
        }

        private void SmoothMoveCards()
        {
            foreach (var kvp in _cardUIs)
            {
                if (kvp.Value.gameObject.activeSelf && _targetPositions.TryGetValue(kvp.Key, out Vector2 targetPos))
                    kvp.Value.anchoredPosition =
                        Vector2.Lerp(kvp.Value.anchoredPosition, targetPos, Time.deltaTime * lerpSpeed);
            }
        }

        private void UpdateLines()
        {
            foreach (var kvp in _mockTree.Nodes)
            {
                int childId = kvp.Key;
                if (kvp.Value.ParentInstanceId.HasValue)
                {
                    int parentId = kvp.Value.ParentInstanceId.Value;
                    if (_cardUIs.TryGetValue(childId, out RectTransform childRect) &&
                        _cardUIs.TryGetValue(parentId, out RectTransform parentRect))
                    {
                        if (!_lineUIs.ContainsKey(childId)) CreateLineUI(childId);
                        _lineUIs[childId].DrawCurve(parentRect.anchoredPosition + TopOffset,
                            childRect.anchoredPosition + BottomOffset);
                    }
                }
            }
        }

        private void CreateCardUI(int id, Color color, string text)
        {
            GameObject obj = Instantiate(mockCardPrefab, transform);
            obj.transform.SetAsLastSibling();
            var img = obj.GetComponent<Image>();
            if (img != null) img.color = color;
            var txt = obj.GetComponentInChildren<UnityEngine.UI.Text>();
            if (txt != null) txt.text = text;
            var rect = obj.GetComponent<RectTransform>();
            if (id < 0 && _targetPositions.TryGetValue(_selectedParentId, out Vector2 parentPos))
                rect.anchoredPosition = parentPos;
            _cardUIs[id] = rect;
        }

        private void CreateLineUI(int childId)
        {
            if (linePrefab == null) return;
            GameObject obj = Instantiate(linePrefab, transform);
            obj.transform.SetAsFirstSibling();
            _lineUIs[childId] = obj.GetComponent<UICurvedLine>();
        }
    }
}