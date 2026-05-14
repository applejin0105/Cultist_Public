using System.Threading.Tasks;
using Effects.Core;
using Effects.Interfaces;
using Newtonsoft.Json.Linq;

namespace Effects.Commands.Flow
{
    /// <summary>
    /// 현재 실행 중인 효과 시퀀스를 중단하는 명령.
    /// JSON: { "cmd": "Cancel" }
    /// </summary>
    public sealed class CancelCommand : ICommand
    {
        public Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            ctx.Cancelled = true;
            UnityEngine.Debug.Log("[CancelCommand] 효과 실행 시퀀스 중단됨.");
            return Task.CompletedTask;
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}
