using System.Collections.Generic;

namespace Domain.Structure.Field
{
    /// <summary>
    /// 카드 노드
    /// </summary>
    public sealed class FieldNode
    {
        public readonly int InstanceId;
        public int? ParentInstanceId; // null 이면 root
        public readonly List<int> ChildrenInstanceIds;

        public FieldNode(int instanceId, int? parentInstanceId = null, List<int> childrenIds = null)
        {
            InstanceId = instanceId;
            ParentInstanceId = parentInstanceId;
            ChildrenInstanceIds = childrenIds ?? new List<int>();
        }

        public void SetParent(int? parentInstanceId)
        {
            ParentInstanceId = parentInstanceId;
        }

        public void AddChild(int childInstanceId)
        {
            if (!ChildrenInstanceIds.Contains(childInstanceId))
                ChildrenInstanceIds.Add(childInstanceId);
        }

        public void InsertChild(int index, int childInstanceId)
        {
            if (!ChildrenInstanceIds.Contains(childInstanceId))
            {
                // 인덱스 범위 초과 방어 로직 (안전장치)
                if (index < 0) index = 0;
                if (index > ChildrenInstanceIds.Count) index = ChildrenInstanceIds.Count;

                ChildrenInstanceIds.Insert(index, childInstanceId);
            }
        }

        public void RemoveChild(int childInstanceId)
        {
            ChildrenInstanceIds.Remove(childInstanceId);
        }
    }
}