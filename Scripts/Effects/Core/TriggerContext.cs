using System.Collections.Generic;
using Domain.Entities;
using Domain.Enums;

namespace Effects.Core
{
    /// <summary>
    /// 트리거 1회 실행 동안 유지되는 컨텍스트.
    /// Source: 트리거를 발동시킨 카드 (자기 자신).
    /// Actor: 트리거 발동 원인이 된 플레이어 (Echo 같은 source 판정에 사용).
    /// Cause: 트리거의 직접 원인이 된 다른 카드(있을 때만, 예: Destroy를 일으킨 카드).
    /// Vars: 명령어 간 변수 바인딩 (예: HasCards에서 bind="n" → Destroy amount={var:"n"}).
    /// Cancelled: Cancel 명령어가 실행되면 이후 노드 실행 중단 + 부분 변경 롤백 트리거.
    /// </summary>
    public class TriggerContext
    {
        public IEffectGameState GameState { get; }
        public CardInstance Source { get; }
        public Player Actor { get; }
        public CardInstance Cause { get; }
        public Dictionary<string, int> Vars { get; } = new Dictionary<string, int>();
        public bool Cancelled { get; set; }

        public TriggerContext(IEffectGameState gameState, CardInstance source, Player actor, CardInstance cause = null)
        {
            GameState = gameState;
            Source = source;
            Actor = actor;
            Cause = cause;
        }
    }
}
