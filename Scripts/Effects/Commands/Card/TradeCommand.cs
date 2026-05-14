using System.Threading.Tasks;
using Domain.Enums;
using Effects.Core;
using Effects.Interfaces;
using Newtonsoft.Json.Linq;
using Systems;
using UnityEngine;

namespace Effects.Commands.Card
{
    /// <summary>
    /// 특정 플레이어에게 교역소에서 카드를 가져오게 함.
    /// JSON: { "cmd": "Trade", "target": "Self"|"Opponent", "amount": 1 }
    /// </summary>
    public sealed class TradeCommand : ICommand
    {
        private readonly GameActionSystem _actionSystem;
        private readonly TargetResolver _targets;

        public TradeCommand(GameActionSystem actionSystem, TargetResolver targets)
        {
            _actionSystem = actionSystem;
            _targets = targets;
        }

        public async Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            var players = _targets.ResolvePlayers(node["target"], ctx);
            int amount = ValueResolver.ResolveInt(node["amount"], ctx, _targets, 1);
            bool starveIfFailed = node["starveIfFailed"]?.Value<bool>() ?? false;

            foreach (var p in players)
            {
                for (int i = 0; i < amount; i++)
                {
                    Debug.Log($"[TradeCommand] {p}에게 교역 실행 (시도 {i+1}/{amount})");
                    bool success = await _actionSystem.Trade(p);
                    
                    if (!success && starveIfFailed)
                    {
                        Debug.Log($"[TradeCommand] 교역 실패로 인해 {p}에게 기아 발생");
                        _actionSystem.Starve(p, 1, true);
                    }
                }
            }
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}
