using System.Threading.Tasks;
using Effects.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Effects.Commands.Flow
{
    /// <summary>
    /// 디버그 출력 명령.
    /// JSON: { "cmd": "Log", "msg": "..." }
    /// 테스트 전용으로 만든 영광스러운 첫 번째 command
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
    }
}
