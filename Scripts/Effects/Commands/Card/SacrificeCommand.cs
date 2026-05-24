using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using Effects.Core;
using Newtonsoft.Json.Linq;
using Systems;
using UnityEngine;

namespace Effects.Commands.Card
{
    /// <summary>
    /// 내 카드를 비용으로 바치는 명령.
    /// Destroy와 동일한 골격(부모 클래스) 위에 세 가지가 추가된다:
    ///  - owner 강제 "Self" (PreCheckAsync)
    ///  - 자살 방지 검증 (PreCheckAsync, CanPay 호출)
    ///  - 비용 충족 시 then 분기, 미달 시 Cancelled (PostLoopAsync)
    ///
    /// 액션 자체는 Destroy와 동일(파괴 후 교역소에 복사본 생성).
    /// JSON: { "cmd": "Sacrifice", "amount": IntExpr, "selectionType": "Manual"|"Auto",
    ///         "from": { ... }, "then": [ ... ], "bind": "k" }
    /// </summary>
    public sealed class SacrificeCommand : TargetedRemovalCommand
    {
        /// <summary>
        /// 자살 방지
        /// 희생 후 신도카드가 최소 1장 남아야 패배 회피.
        /// 즉, 현재 신도 카드 수 ≥ 요구량 + 1.
        ///
        /// Host(PreCheckAsync)와 Client(InGameCardUI.CheckLocalCanReveal) 양쪽에서 이 한 함수를 호출하여
        /// 룰이 두 곳에 흩어지지 않도록 한다.
        /// </summary>
        public static bool CanPay(int cultistCardCount, int requiredAmount)
            => cultistCardCount >= requiredAmount + 1;

        public SacrificeCommand(IEffectGameState gameState, GameActionSystem actionSystem,
            TargetResolver targets, IRandomSource rng)
            : base(gameState, actionSystem, targets, rng)
        {
        }

        protected override Task ApplyAsync(Player actor, CardInstance target)
            => _actionSystem.Destroy(actor, target);

        protected override Task<bool> PreCheckAsync(JObject node, TriggerContext ctx)
        {
            var from = node["from"] as JObject;
            if (from == null) return Task.FromResult(false);

            // owner 강제 — 희생은 항상 내 카드 대상
            from["owner"] = "Self";

            // 자살 방지
            // 희생 발동 카드 본인은 FieldFront(앞면)이므로 신도카드 카운팅에 포함되지 않는다.
            // 따라서 실제 필요량은: 제물 N장 + 생존용 뒷면 1장 = 최소 N+1장.
            // 희생 후 신도 카드가 0이 되면 패배이므로, 1장 이상 남도록 강제한다.
            var myCultistCards = ctx.GameState.GetAllCards().Where(c =>
                c.OwnerSeat == ctx.Actor &&
                c.Zone == Zone.Field &&
                c.CardStatus == CardStatus.FieldBack &&
                c.BaseData != null).ToList();

            int requiredAmount = ValueResolver.ResolveInt(node["amount"], ctx, _targets, 1);

            if (!CanPay(myCultistCards.Count, requiredAmount))
            {
                Debug.LogWarning($"[SacrificeCommand] {ctx.Actor} 자살 방지: " +
                                 $"필드에 뒷면 신도 카드가 부족함. " +
                                 $"(현재:{myCultistCards.Count}장, 요구:{requiredAmount}장 + 1장)");
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        protected override async Task PostLoopAsync(JObject node, TriggerContext ctx,
            EffectRunner runner, int processed)
        {
            int requiredAmount = ValueResolver.ResolveInt(node["amount"], ctx, _targets, 1);

            // 결과 바인딩
            string bindVar = node["bind"]?.ToString();
            if (!string.IsNullOrEmpty(bindVar)) ctx.Vars[bindVar] = processed;

            if (processed >= requiredAmount)
            {
                var thenBranch = node["then"] as JArray;
                if (thenBranch != null) await runner.RunNodesAsync(thenBranch, ctx);
            }
            else
            {
                Debug.LogWarning($"[SacrificeCommand] 요구량({requiredAmount}) 미달로 취소 처리. " +
                                 $"(희생됨: {processed})");
                ctx.Cancelled = true;
            }
        }
    }
}