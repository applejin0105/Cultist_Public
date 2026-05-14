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

        public FieldState()
        {
            PlayerFieldTree = new FieldTree();
        }

        public FieldState(FieldState other)
        {
            PlayerFieldTree = new FieldTree(other.PlayerFieldTree);
        }

        public void AddNode(FieldNode node)
        {
            PlayerFieldTree.AddNode(node);
        }

        public FieldNode GetNodeByInstanceId(int instanceId)
        {
            return PlayerFieldTree.GetNodeByInstanceId(instanceId);
        }

        public FieldTree GetFieldTree()
        {
            return PlayerFieldTree;
        }
    }
}