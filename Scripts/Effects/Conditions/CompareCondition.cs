using Effects.Core;
using Newtonsoft.Json.Linq;

namespace Effects.Conditions
{
    /// <summary>
    /// 두 수치를 비교하는 범용 조건문.
    /// JSON: { "type": "Compare", "lhs": dynamicInt, "op": ">"|">="|"<"|"<="|"==", "rhs": dynamicInt }
    /// </summary>
    public class CompareCondition : ConditionBase
    {
        public override bool Evaluate(JObject node, TriggerContext ctx)
        {
            // 수치 해결 (lhs, rhs)
            int lhs = ValueResolver.ResolveInt(node["lhs"], ctx, null, 0);
            int rhs = ValueResolver.ResolveInt(node["rhs"], ctx, null, 0);
            string op = node["op"]?.ToString() ?? "==";

            return op switch
            {
                ">" => lhs > rhs,
                ">=" => lhs >= rhs,
                "<" => lhs < rhs,
                "<=" => lhs <= rhs,
                "==" => lhs == rhs,
                "!=" => lhs != rhs,
                _ => lhs == rhs
            };
        }
    }
}
