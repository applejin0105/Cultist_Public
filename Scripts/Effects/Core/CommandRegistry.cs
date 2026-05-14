using System.Collections.Generic;
using Effects.Commands;
using UnityEngine;

namespace Effects.Core
{
    /// <summary>
    /// "cmd" 문자열 → ICommand 핸들러 매핑.
    /// EffectsBootstrap에서 명시적으로 Register 호출 (IL2CPP/AOT 친화).
    /// </summary>
    public sealed class CommandRegistry
    {
        private readonly Dictionary<string, ICommand> _map = new Dictionary<string, ICommand>();

        public void Register(string id, ICommand command)
        {
            if (string.IsNullOrEmpty(id) || command == null) return;
            _map[id] = command;
        }

        public ICommand Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_map.TryGetValue(id, out var cmd)) return cmd;
            Debug.LogWarning($"[CommandRegistry] 미등록 명령어: {id}");
            return null;
        }
    }
}
