using System.Collections.Generic;
using Effects.Conditions;
using UnityEngine;

namespace Effects.Core
{
    /// <summary>
    /// "type" 문자열 → ICondition 매핑.
    /// </summary>
    public sealed class ConditionRegistry
    {
        private readonly Dictionary<string, ICondition> _map = new Dictionary<string, ICondition>();

        public void Register(string id, ICondition condition)
        {
            if (string.IsNullOrEmpty(id) || condition == null) return;
            _map[id] = condition;
        }

        public ICondition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_map.TryGetValue(id, out var cond)) return cond;
            Debug.LogWarning($"[ConditionRegistry] 미등록 조건: {id}");
            return null;
        }
    }
}
