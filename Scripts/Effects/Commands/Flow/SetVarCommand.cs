using System.Threading.Tasks;
using Effects.Core;
using Effects.Interfaces;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Effects.Commands.Flow
{
    /// <summary>
    /// 수치를 계산하여 TriggerContext.Vars에 저장하는 명령.
    /// JSON: { "cmd": "SetVar", "name": "n", "value": dynamicInt }
    /// </summary>
    public sealed class SetVarCommand : ICommand
    {
        private readonly TargetResolver _targets;

        public SetVarCommand(TargetResolver targets)
        {
            _targets = targets;
        }

        public Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            string varName = node["name"]?.ToString();
            if (string.IsNullOrEmpty(varName)) return Task.CompletedTask;

            // ValueResolver를 통해 동적 수치(카드 수, 스탯 등)를 해결
            int val = ValueResolver.ResolveInt(node["value"], ctx, _targets, 0);
            
            ctx.Vars[varName] = val;
            Debug.Log($"[SetVar] 변수 저장: {varName} = {val}");

            return Task.CompletedTask;
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}
