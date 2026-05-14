using System.Collections.Generic;
using Domain.Structure.Field;
using Scenes.InGame.UI;
using UnityEngine;

namespace Scenes.InGame.Core
{
    public class FieldLayoutManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject linePrefab;

        [Header("Layout Settings")]
        public float cardWidth = 268.8f;
        public float cardHeight = 420f;
        public float paddingX = 80f;
        public float paddingY = 100f;
        public float animationSpeed = 12f;

        private DeterministicTreeLayout _layoutEngine;
        private Dictionary<int, Vector2> _targetPositions = new Dictionary<int, Vector2>();
        private Dictionary<int, RectTransform> _cardUIs = new Dictionary<int, RectTransform>();

        private List<UICurvedLine> _linePool = new List<UICurvedLine>();

        private class LineConnection
        {
            public RectTransform parent;
            public RectTransform child;
            public UICurvedLine lineRenderer;
        }

        private List<LineConnection> _activeLines = new List<LineConnection>();

        private void Awake()
        {
            _layoutEngine = new DeterministicTreeLayout(cardWidth, cardHeight, paddingX, paddingY);
        }

        private void Update()
        {
            foreach (var kvp in _cardUIs)
            {
                int id = kvp.Key;
                RectTransform rect = kvp.Value;
                if (rect != null && _targetPositions.TryGetValue(id, out Vector2 targetPos))
                {
                    rect.anchoredPosition =
                        Vector2.Lerp(rect.anchoredPosition, targetPos, Time.deltaTime * animationSpeed);
                }
            }

            foreach (var conn in _activeLines)
            {
                if (conn.parent != null && conn.child != null)
                {
                    conn.lineRenderer.DrawCurve(conn.parent.anchoredPosition, conn.child.anchoredPosition);
                }
            }
        }

        public void SyncLayout(FieldTree tree, int rootId, Dictionary<int, RectTransform> cardUIs, int focusId)
        {
            _cardUIs = cardUIs;
            _targetPositions = _layoutEngine.CalculatePositions(tree, rootId, Vector2.zero);

            UpdateContentBounds();

            UpdateLines(tree);

            if (_targetPositions.TryGetValue(focusId, out Vector2 focusPos))
            {
                Debug.Log($"[Focus Debug 4] SyncLayout - focusId({focusId})의 좌표 발견: {focusPos}. 카메라 이동 지시!");
                var draggablePanel = GetComponentInParent<DraggableFieldPanel>();
                if (draggablePanel != null) draggablePanel.CenterToPosition(focusPos);
            }
            else
            {
                Debug.LogWarning($"[Focus Debug 4-Error] SyncLayout - focusId({focusId})의 좌표를 계산 엔진에서 찾을 수 없습니다!");
            }

            Debug.Log($"[FieldLayoutManager] 레이아웃 갱신 완료. 포커스 타겟: {focusId}");
        }

        private void UpdateContentBounds()
        {
            if (_targetPositions == null || _targetPositions.Count == 0) return;

            float minX = 0f;
            float maxX = 0f;
            float minY = 0f;
            float maxY = 0f;

            foreach (var pos in _targetPositions.Values)
            {
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }

            float totalWidth = (maxX - minX) + (cardWidth * 2.5f);
            float totalHeight = (maxY - minY) + (cardHeight * 2.5f);

            totalWidth = Mathf.Max(totalWidth, 3000f);
            totalHeight = Mathf.Max(totalHeight, 3000f);

            RectTransform myRect = GetComponent<RectTransform>();
            if (myRect != null)
            {
                myRect.sizeDelta = new Vector2(totalWidth, totalHeight);
            }
        }

        private void UpdateLines(FieldTree tree)
        {
            foreach (var line in _activeLines)
            {
                if (line.lineRenderer != null)
                    line.lineRenderer.gameObject.SetActive(false);
            }

            _activeLines.Clear();

            if (linePrefab == null) return;

            int poolIndex = 0;

            foreach (var nodeKvp in tree.Nodes)
            {
                FieldNode parentNode = nodeKvp.Value;
                foreach (int childId in parentNode.ChildrenInstanceIds)
                {
                    if (_cardUIs.ContainsKey(parentNode.InstanceId) && _cardUIs.ContainsKey(childId))
                    {
                        UICurvedLine curve;

                        // [풀링 로직] 만들어둔 선이 남아있으면 재활용하고, 모자라면 새로 생성
                        if (poolIndex < _linePool.Count)
                        {
                            curve = _linePool[poolIndex];
                            curve.gameObject.SetActive(true);
                        }
                        else
                        {
                            GameObject lineObj = Instantiate(linePrefab, transform);
                            lineObj.transform.SetAsFirstSibling();
                            curve = lineObj.GetComponent<UICurvedLine>();
                            _linePool.Add(curve);
                        }

                        _activeLines.Add(new LineConnection
                        {
                            parent = _cardUIs[parentNode.InstanceId],
                            child = _cardUIs[childId],
                            lineRenderer = curve
                        });

                        poolIndex++;
                    }
                }
            }

            for (int i = poolIndex; i < _linePool.Count; i++)
            {
                if (_linePool[i] != null) _linePool[i].gameObject.SetActive(false);
            }
        }

        public void FocusOnCard(int cardId)
        {
            if (_targetPositions.TryGetValue(cardId, out Vector2 pos))
            {
                Debug.Log($"[Focus Debug 5] FocusOnCard - cardId({cardId})의 좌표 발견: {pos}. 탭 전환 카메라 이동!");
                var panel = GetComponentInParent<DraggableFieldPanel>();
                if (panel != null) panel.CenterToPosition(pos);
            }
            else
            {
                Debug.LogWarning($"[Focus Debug 5-Error] FocusOnCard - cardId({cardId})의 좌표를 찾을 수 없습니다!");
            }
        }
    }
}