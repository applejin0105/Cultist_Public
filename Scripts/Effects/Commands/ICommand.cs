using System.Threading.Tasks;
using Effects.Core;
using Newtonsoft.Json.Linq;

namespace Effects.Commands
{
    /// <summary>
    /// 모든 카드 효과 명령어의 공통 인터페이스.
    /// node: cardsEffects.json의 한 명령 객체 ({ "cmd": "...", ...args }).
    /// ctx: 트리거 실행 컨텍스트.
    /// runner: 중첩 명령(예: If의 then/else, Sacrifice의 then) 실행을 위해 주입.
    /// </summary>
    public interface ICommand
    {
        Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner);
        bool CanExecute(JObject node, TriggerContext ctx);
    }
}
