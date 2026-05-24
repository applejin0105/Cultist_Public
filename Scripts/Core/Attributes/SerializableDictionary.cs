using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Attributes
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new();
        [SerializeField] private List<TValue> values = new();

        public void OnBeforeSerialize()
        {
            var hasDuplicate = false;
            var keySet = new HashSet<TKey>();
            for (var i = 0; i < keys.Count; i++)
                if (!keySet.Add(keys[i]))
                {
                    hasDuplicate = true;
                    break;
                }

            if (hasDuplicate) return;

            keys.Clear();
            values.Clear();
            foreach (var pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            for (var i = 0; i < Math.Min(keys.Count, values.Count); i++)
                if (!ContainsKey(keys[i]))
                    Add(keys[i], values[i]);
        }
    }
}