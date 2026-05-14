using Domain.Enums;
using Effects.Core;
using Newtonsoft.Json.Linq;

namespace Effects.Conditions
{
    /// <summary>
    /// 플레이어가 특정 심볼을 일정 수치 이상 보유했는지 체크.
    /// JSON: { "type": "HasSymbol", "symbol": "Strength", "amount": 3, "target": "Self|Opponent" }
    /// </summary>
    public class HasSymbolCondition : ConditionBase
    {
        public override bool Evaluate(JObject node, TriggerContext ctx)
        {
            string symbolStr = node["symbol"]?.ToString() ?? "";
            if (!System.Enum.TryParse<Symbols>(symbolStr, out var symbol)) return false;

            int required = node["amount"]?.ToObject<int>() ?? 0;

            Player targetPlayer = ResolveTarget(node, ctx);
            return ctx.GameState.GetPlayerStat(targetPlayer, symbolStr) >= required;
        }
    }
}