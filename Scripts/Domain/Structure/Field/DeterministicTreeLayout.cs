using System.Collections.Generic;
using UnityEngine;

namespace Domain.Structure.Field
{
    public class DeterministicTreeLayout
    {
        private readonly float _cardWidth;
        private readonly float _cardHeight;
        private readonly float _paddingX;
        private readonly float _paddingY;

        private Dictionary<int, float> _subtreeWidths;
        private Dictionary<int, Vector2> _targetPositions;
        private FieldTree _tree;

        public DeterministicTreeLayout(float cardWidth, float cardHeight, float paddingX, float paddingY)
        {
            _cardWidth = cardWidth;
            _cardHeight = cardHeight;
            _paddingX = paddingX;
            _paddingY = paddingY;
        }

        /// <summary>
        /// 트리를 분석하여 모든 노드의 정확한 타겟 좌표를 계산합니다.
        /// </summary>
        public Dictionary<int, Vector2> CalculatePositions(FieldTree tree, int rootInstanceId, Vector2 rootStartPos)
        {
            _tree = tree;
            _subtreeWidths = new Dictionary<int, float>();
            _targetPositions = new Dictionary<int, Vector2>();

            // 1단계: Bottom-Up 방식으로 각 노드가 차지하는 '가로 너비(Width)'를 계산합니다.
            CalculateSubtreeWidth(rootInstanceId);

            // 2단계: Top-Down 방식으로 실제 좌표를 할당합니다.
            AssignPositions(rootInstanceId, rootStartPos);

            return _targetPositions;
        }

        private float CalculateSubtreeWidth(int nodeId)
        {
            var node = _tree.GetNodeByInstanceId(nodeId);
            if (node == null) return 0;

            var children = node.ChildrenInstanceIds;

            // 자식이 없으면 카드 너비만큼만 공간을 차지함
            if (children.Count == 0)
            {
                _subtreeWidths[nodeId] = _cardWidth;
                return _cardWidth;
            }

            // 자식이 1명이면, 그 자식의 너비를 그대로 물려받음 (수직 직진성)
            if (children.Count == 1)
            {
                float width = CalculateSubtreeWidth(children[0]);
                _subtreeWidths[nodeId] = width;
                return width;
            }

            // 자식이 2명 이상이면, 자식들의 너비 총합 + 자식들 사이의 여백(PaddingX)을 합산함
            float totalWidth = 0;
            foreach (var childId in children)
            {
                totalWidth += CalculateSubtreeWidth(childId);
            }

            totalWidth += (children.Count - 1) * _paddingX;

            _subtreeWidths[nodeId] = totalWidth;
            return totalWidth;
        }

        private void AssignPositions(int nodeId, Vector2 pos)
        {
            _targetPositions[nodeId] = pos;

            var node = _tree.GetNodeByInstanceId(nodeId);
            if (node == null) return;

            var children = node.ChildrenInstanceIds;

            // 자식이 1명이면 무조건 부모의 X좌표를 따라감 (위로 일직선)
            if (children.Count == 1)
            {
                Vector2 childPos = new Vector2(pos.x, pos.y + _cardHeight + _paddingY);
                AssignPositions(children[0], childPos);
            }
            // 자식이 2명 이상이면 공간을 분할하여 대칭 배치
            else if (children.Count > 1)
            {
                float totalWidth = _subtreeWidths[nodeId];
                // 배치를 시작할 가장 왼쪽 X 좌표
                float currentX = pos.x - (totalWidth / 2f);

                foreach (var childId in children)
                {
                    float childWidth = _subtreeWidths[childId];
                    // 자식의 중심 X 좌표는 시작점 + 자식 너비의 절반
                    float childX = currentX + (childWidth / 2f);

                    Vector2 childPos = new Vector2(childX, pos.y + _cardHeight + _paddingY);
                    AssignPositions(childId, childPos);

                    // 다음 자식을 위해 X 좌표 이동
                    currentX += childWidth + _paddingX;
                }
            }
        }
    }
}