using System.Collections.Generic;
using Domain.Structure.Field;

namespace Domain.State
{
    /// <summary>
    /// 각 플레이어의 필드 상태
    /// </summary>
    public sealed class FieldState
    {
        private FieldTree PlayerFieldTree { get; set; }
        public int NodeCount => PlayerFieldTree.Count;

        public IEnumerable<FieldNode> AllNodes => PlayerFieldTree.Nodes.Values;
        
        public FieldState()
            => PlayerFieldTree = new FieldTree();

        public FieldState(FieldState other)
            => PlayerFieldTree = new FieldTree(other.PlayerFieldTree);

        public void AddNode(FieldNode node)
            => PlayerFieldTree.AddNode(node);

        public FieldNode GetNodeByInstanceId(int instanceId)
            => PlayerFieldTree.GetNodeByInstanceId(instanceId);

        public bool ContainsNode(int instanceId)
            => PlayerFieldTree.GetNodeByInstanceId(instanceId) != null;

        public List<FieldNode> GetAncestors(int instanceId, bool includeSelf = false)
            => PlayerFieldTree.GetAncestors(instanceId, includeSelf);

        public List<FieldNode> GetDescendants(int instanceId, bool includeSelf = false)
            => PlayerFieldTree.GetDescendants(instanceId, includeSelf);

        public HashSet<int> GetSectInstanceIds(int instanceId)
            => PlayerFieldTree.GetSectInstanceIds(instanceId);
    }
}