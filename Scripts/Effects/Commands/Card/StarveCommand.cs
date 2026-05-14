using System.Threading.Tasks;
using Effects.Core;
using Newtonsoft.Json.Linq;
using Systems;
using UnityEngine;

namespace Effects.Commands.Card
{
    /// <summary>
    /// 특정 플레이어의 덱에 기아(Hunger) 카드를 추가.
    /// JSON: { "cmd": "Starve", "target": "Self"|"Opponent", "amount": 1 }
    /// </summary>
    public sealed class StarveCommand : ICommand
    {
        private readonly GameActionSystem _actionSystem;
        private readonly TargetResolver _targets;

        public StarveCommand(GameActionSystem actionSystem, TargetResolver targets)
        {
            _actionSystem = actionSystem;
            _targets = targets;
        }

        public Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            var players = _targets.ResolvePlayers(node["target"], ctx);
            int amount = ValueResolver.ResolveInt(node["amount"], ctx, _targets, 1);
            bool shuffle = node["shuffle"]?.Value<bool>() ?? true;

            foreach (var p in players)
            {
                Debug.Log($"[StarveCommand] {p}의 덱에 기아 카드 {amount}장 추가 실행");
                _actionSystem.Starve(p, amount, shuffle);
            }

            return Task.CompletedTask;
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}