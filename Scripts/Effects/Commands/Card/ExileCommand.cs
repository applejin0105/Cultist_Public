using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using Effects.Core;
using Systems;

namespace Effects.Commands.Card
{
    /// <summary>
    /// 카드 추방 명령. 부모(TargetedRemovalCommand)의 공통 흐름을 그대로 사용한다.
    /// 액션 한 줄(추방, 교역소 복사본 생성 안 함)만 Destroy와 다르다.
    /// JSON: { "cmd": "Exile", "amount": IntExpr|{min,max}|"All",
    ///         "selectionType": "Manual"|"Auto", "singleOwner": bool, "from": { ... } }
    /// </summary>
    public sealed class ExileCommand : TargetedRemovalCommand
    {
        public ExileCommand(IEffectGameState gameState, GameActionSystem actionSystem,
            TargetResolver targets, IRandomSource rng)
            : base(gameState, actionSystem, targets, rng)
        {
        }

        protected override Task ApplyAsync(Player actor, CardInstance target)
            => _actionSystem.Exile(actor, target);
    }
}
