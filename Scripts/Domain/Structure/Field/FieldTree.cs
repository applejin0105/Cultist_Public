using System.Collections.Generic;

namespace Domain.Structure.Field
{
    /// <summary>
    /// 카드 노드 트리
    /// </summary>
    public class FieldTree
    {
        // InstanceID, FiledNode
        private readonly Dictionary<int, FieldNode> _nodes;
        public IReadOnlyDictionary<int, FieldNode> Nodes => _nodes;
        
        public int Count => Nodes.Count;

        public FieldTree()
        {
            _nodes = new Dictionary<int, FieldNode>();
        }
        public FieldTree(FieldTree playerFieldTree)
        {
            _nodes = playerFieldTree == null
                ? new Dictionary<int, FieldNode>()
                : new Dictionary<int, FieldNode>(playerFieldTree._nodes);
        }
        public void AddNode(FieldNode fieldNode)
        {
            _nodes.TryAdd(fieldNode.InstanceId, fieldNode);
        }

        public FieldNode GetNodeByInstanceId(int instanceId)
        {
            return Nodes.GetValueOrDefault(instanceId);
        }

        #region 트리 탐색 유틸리티

        /// <summary>
        /// 직속 부모 라인 (자신부터 Root까지 위로 거슬러 올라감)
        /// </summary>
        public List<FieldNode> GetAncestors(int instanceId, bool includeSelf = false)
        {
            List<FieldNode> result = new List<FieldNode>();
            var current = GetNodeByInstanceId(instanceId);
            if (current == null) return result;

            if (includeSelf) result.Add(current);

            while (current.ParentInstanceId.HasValue)
            {
                current = GetNodeByInstanceId(current.ParentInstanceId.Value);
                if (current != null) result.Add(current);
                else break;
            }

            return result;
        }

        /// <summary>
        /// 모든 자손 라인 (자신을 뿌리로 하여 아래로 파생된 모든 가지)
        /// </summary>
        public List<FieldNode> GetDescendants(int instanceId, bool includeSelf = false)
        {
            List<FieldNode> result = new List<FieldNode>();
            var node = GetNodeByInstanceId(instanceId);
            if (node == null) return result;

            if (includeSelf) result.Add(node);
            CollectDescendants(node, result);
            return result;
        }

        /// <summary>
        /// 종파(Sect) 멤버 InstanceId 집합.
        /// 종파 = 해당 카드의 직계 조상(부모 → 부모의 부모 → ... → Root) + 자기 자신 + 모든 자손.
        /// 형제·사촌 등 가로로 연결된 노드는 포함되지 않는다.
        /// </summary>
        public HashSet<int> GetSectInstanceIds(int instanceId)
        {
            var result = new HashSet<int>();
            foreach (var node in GetAncestors(instanceId, includeSelf: true))
                result.Add(node.InstanceId);
            foreach (var node in GetDescendants(instanceId, includeSelf: true))
                result.Add(node.InstanceId);
            return result;
        }

        private void CollectDescendants(FieldNode node, List<FieldNode> result)
        {
            foreach (var childId in node.ChildrenInstanceIds)
            {
                var child = GetNodeByInstanceId(childId);
                if (child != null)
                {
                    result.Add(child);
                    CollectDescendants(child, result); // 재귀 호출
                }
            }
        }

        /// <summary>
        /// 형제 노드 (같은 부모를 공유하는 노드들)
        /// </summary>
        public List<FieldNode> GetSiblings(int instanceId, bool includeSelf = false)
        {
            List<FieldNode> result = new List<FieldNode>();
            var node = GetNodeByInstanceId(instanceId);

            // 최상단 Root이거나 부모가 없는 경우
            if (node == null || !node.ParentInstanceId.HasValue)
            {
                if (includeSelf && node != null) result.Add(node);
                return result;
            }

            var parent = GetNodeByInstanceId(node.ParentInstanceId.Value);
            if (parent == null) return result;

            foreach (var childId in parent.ChildrenInstanceIds)
            {
                if (!includeSelf && childId == instanceId) continue;

                var sibling = GetNodeByInstanceId(childId);
                if (sibling != null) result.Add(sibling);
            }

            return result;
        }

        /// <summary>
        /// 직속 자식 (바로 아래에 연결된 노드들만 반환)
        /// </summary>
        public List<FieldNode> GetChildren(int instanceId)
        {
            List<FieldNode> result = new List<FieldNode>();
            var node = GetNodeByInstanceId(instanceId);
            if (node == null) return result;

            foreach (var childId in node.ChildrenInstanceIds)
            {
                var child = GetNodeByInstanceId(childId);
                if (child != null) result.Add(child);
            }

            return result;
        }

        #endregion
    }
}