using System.Threading.Tasks;
using Effects.Core;
using Effects.Interfaces;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Effects.Commands.Flow
{
    /// <summary>
    /// 조건부 분기 명령.
    /// JSON:
    /// {
    ///   "cmd": "If",
    ///   "condition": { "type": "HasSymbol", "symbol": "Strength", "amount": 2 },
    ///   "then": [ { "cmd": "Log", "msg": "Success" } ],
    ///   "else": [ { "cmd": "Log", "msg": "Fail" } ]
    /// }
    /// </summary>
    public class IfCommand : ICommand
    {
        private readonly ConditionRegistry _conditions;

        public IfCommand(ConditionRegistry conditions)
        {
            _conditions = conditions;
        }

        public async Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            var condNode = node["condition"] as JObject;
            if (condNode == null) return;

            string type = condNode["type"]?.ToString();
            var condition = _conditions.Get(type);

            bool result = false;
            if (condition != null)
            {
                result = condition.Evaluate(condNode, ctx);
            }

            string branchKey = result ? "then" : "else";
            var branch = node[branchKey] as JArray;

            if (branch != null)
            {
                await runner.RunNodesAsync(branch, ctx);
            }
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}