using System.Threading.Tasks;
using Domain.Policies;
using Domain.State.Host;
using Effects.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Effects.Commands.Phase
{
    /// <summary>
    /// 다음 드로우 규칙을 PlayerState.NextDrawRule에 주입.
    /// JSON: {
    ///   "cmd": "SetNextDraw",
    ///   "amount": dynamicInt,
    ///   "skipSelection": true|false,
    ///   "where": { ...filter... }    // 옵션
    /// }
    ///
    /// 규칙 적용 시점: 다음 Draw Phase에서 PlayerState.ConsumeNextDrawRule()이 호출되어 사용된다.
    /// where 필터는 GameActionSystem.FetchCardAsync → TargetResolver가 해석.
    /// </summary>
    public sealed class SetNextDrawCommand : ICommand
    {
        private readonly IEffectGameState _gameState;
        private readonly TargetResolver _targets;

        public SetNextDrawCommand(IEffectGameState gameState, TargetResolver targets)
        {
            _gameState = gameState;
            _targets = targets;
        }

        public Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            var fullState = _gameState as GameState;
            var pState = fullState?.GetPlayerStateById(ctx.Source.OwnerSeat);
            if (pState == null) return Task.CompletedTask;

            int amount = ValueResolver.ResolveInt(node["amount"], ctx, _targets, 1);
            bool skipSelection = node["skipSelection"]?.ToObject<bool>() ?? false;
            JObject where = node["where"] as JObject;

            var rule = new DrawRule
            {
                Type = skipSelection ? DrawType.Simple : DrawType.Draft,
                SkipSelection = skipSelection,
                Amount = amount,
                CardCondition = where
            };

            pState.SetNextDrawRule(rule);
            Debug.Log($"[SetNextDraw] {ctx.Source.OwnerSeat} 다음 드로우: amount={amount}, " +
                      $"skip={skipSelection}, where={where?.ToString(Newtonsoft.Json.Formatting.None) ?? "(none)"}");
            return Task.CompletedTask;
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}
