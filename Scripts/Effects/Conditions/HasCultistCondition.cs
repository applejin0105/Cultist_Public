using Domain.Enums;
using Effects.Core;
using Newtonsoft.Json.Linq;

namespace Effects.Conditions
{
    /// <summary>
    /// 플레이어의 현재 신도(Cultist) 수치 체크.
    /// JSON: { "type": "HasCultist", "amount": 5, "op": ">=|>|==|<|<=", "target": "Self|Opponent" }
    /// </summary>
    public class HasCultistCondition : ConditionBase
    {
        public override bool Evaluate(JObject node, TriggerContext ctx)
        {
            int required = node["amount"]?.ToObject<int>() ?? 0;
            string op = node["op"]?.ToString() ?? ">=";

            Player targetPlayer = ResolveTarget(node, ctx);
            int current = ctx.GameState.GetPlayerStat(targetPlayer, "cultist");

            return op switch
            {
                ">" => current > required,
                ">=" => current >= required,
                "==" => current == required,
                "<" => current < required,
                "<=" => current <= required,
                _ => current >= required,
            };
        }
    }
}