using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using Effects.Core;
using Systems;

namespace Effects.Commands.Card
{
    /// <summary>
    /// 카드 파괴 명령. 부모(TargetedRemovalCommand)의 공통 흐름을 그대로 사용한다.
    /// 액션 한 줄(파괴 후 교역소에 복사본 생성)만 다르다.
    /// JSON: { "cmd": "Destroy", "amount": IntExpr|{min,max}|"All",
    ///         "selectionType": "Manual"|"Auto", "singleOwner": bool, "from": { ... } }
    /// </summary>
    public sealed class DestroyCommand : TargetedRemovalCommand
    {
        public DestroyCommand(IEffectGameState gameState, GameActionSystem actionSystem,
            TargetResolver targets, IRandomSource rng)
            : base(gameState, actionSystem, targets, rng)
        {
        }

        protected override Task ApplyAsync(Player actor, CardInstance target)
            => _actionSystem.Destroy(actor, target);
    }
}
