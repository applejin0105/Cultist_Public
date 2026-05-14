using Effects.Core;
using Newtonsoft.Json.Linq;

namespace Effects.Conditions
{
    /// <summary>
    /// If/passive 등에서 평가되는 조건의 공통 인터페이스.
    /// node: { "type": "...", ...args } 형태.
    /// </summary>
    public interface ICondition
    {
        bool Evaluate(JObject node, TriggerContext ctx);
    }
}
