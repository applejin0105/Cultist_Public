using System.Collections.Generic;

namespace Domain.Structure.Field
{
    /// <summary>
    /// 카드 노드 트리
    /// </summary>
    public sealed class FieldTree
    {
        // InstanceID, FiledNode
        public Dictionary<int, FieldNode> Nodes { get; }
        public int Count => Nodes.Count;

        public FieldTree()
        {
            Nodes = new Dictionary<int, FieldNode>();
        }

        public FieldTree(FieldTree playerFieldTree)
        {
            Nodes = playerFieldTree == null ? new Dictionary<int, FieldNode>() : playerFieldTree.Nodes;
        }

        public void AddNode(FieldNode fieldNode)
        {
            Nodes.TryAdd(fieldNode.InstanceId, fieldNode);
        }

        public FieldNode GetNodeByInstanceId(int instanceId)
        {
            return Nodes.GetValueOrDefault(instanceId);
        }

        #region 강력한 트리 탐색 유틸리티

        /// <summary>
        /// 1. 직속 부모 라인 (자신부터 Root까지 위로 거슬러 올라감)
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
        /// 2. 모든 자손 라인 (자신을 뿌리로 하여 아래로 파생된 모든 가지)
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
        /// 3. 형제 노드 (같은 부모를 공유하는 노드들)
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
        /// 4. 직속 자식 (바로 아래에 연결된 노드들만 반환)
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