using System.Threading.Tasks;
using Domain.Policies;
using Effects.Core;
using Newtonsoft.Json.Linq;
using Systems;
using UnityEngine;

namespace Effects.Commands.Card
{
    /// <summary>
    /// 즉시 N장 드로우 (Source 카드 OwnerSeat에게).
    /// JSON: {
    ///   "cmd": "Draw",
    ///   "amount": dynamicInt,
    ///   "where": { ...filter... }   // 옵션 (Step 4에서 일반화)
    /// }
    ///
    /// 카드 19(풍요의 봄)에서 사용 예정. Step 5 함께 진행 가능.
    /// </summary>
    public sealed class DrawCommand : ICommand
    {
        private readonly GameActionSystem _actionSystem;
        private readonly TargetResolver _targets;

        public DrawCommand(GameActionSystem actionSystem, TargetResolver targets)
        {
            _actionSystem = actionSystem;
            _targets = targets;
        }

        public async Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            int amount = ValueResolver.ResolveInt(node["amount"], ctx, _targets, 1);
            JObject where = node["where"] as JObject;

            var rule = new DrawRule
            {
                Type = DrawType.Simple,
                SkipSelection = true,
                Amount = amount,
                CardCondition = where
            };

            bool ok = await _actionSystem.Draw(ctx.Source.OwnerSeat, rule);
            if (!ok)
            {
                Debug.LogWarning($"[Draw] {ctx.Source.OwnerSeat} 드로우 실패");
            }
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}
