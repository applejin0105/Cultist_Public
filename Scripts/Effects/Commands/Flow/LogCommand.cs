using System.Threading.Tasks;
using Effects.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Effects.Commands.Flow
{
    /// <summary>
    /// 디버그 출력 명령.
    /// JSON: { "cmd": "Log", "msg": "..." }
    ///
    /// 카드 0번(콩이) 처럼 디버그용으로만 사용. 게임 로직에 영향 없음.
    /// </summary>
    public sealed class LogCommand : ICommand
    {
        public Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            string msg = node["msg"]?.ToString() ?? "(no msg)";
            string cardName = ctx.Source?.BaseData?.Name ?? "?";
            Debug.Log($"[CardEffect:Log] {cardName}({ctx.Source?.InstanceId}) → {msg}");
            return Task.CompletedTask;
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}
