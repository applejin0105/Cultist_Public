using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using Domain.State.Host;
using Effects.Core;
using Effects.Interfaces;
using Newtonsoft.Json.Linq;
using Systems;
using UnityEngine;

namespace Effects.Commands.Card
{
    /// <summary>
    /// 내 카드를 제물로 바치는 명령. 결과 바인딩 지원.
    /// </summary>
    public sealed class SacrificeCommand : ICommand
    {
        private readonly IEffectGameState _gameState;
        private readonly GameActionSystem _actionSystem;
        private readonly TargetResolver _targets;

        public SacrificeCommand(IEffectGameState gameState, GameActionSystem actionSystem,
            TargetResolver targets, IPlayerInputProvider input, IRandomSource rng)
        {
            _gameState = gameState;
            _actionSystem = actionSystem;
            _targets = targets;
        }

        public async Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            var from = node["from"] as JObject;
            if (from == null) return;
            from["owner"] = "Self"; 
            
            // 희생 시 본인 제외 옵션 추가 (보통 자기 자신을 희생하진 않음)
            var finalTargets = await _targets.PickAsync(node, ctx, excludeSource: true);

            // [자살 방지]
            // 희생 발동 카드 본인은 FieldFront(앞면)이므로 face-down 카운팅에 포함되지 않는다.
            // 따라서 실제 필요량은: 제물 N장 + 생존용 뒷면 1장 = 최소 N+1장.
            // 희생 후 face-down 신도 카드가 0이 되면 패배이므로, 1장 이상 남도록 강제한다.
            var myFacedownCultists = ctx.GameState.GetAllCards().Where(c =>
                c.OwnerSeat == ctx.Actor &&
                c.Zone == Zone.Field &&
                c.CardStatus == CardStatus.FieldBack &&
                c.BaseData != null).ToList();

            int requiredAmount = ValueResolver.ResolveInt(node["amount"], ctx, _targets, 1);
            int safetyThreshold = requiredAmount + 1;

            if (myFacedownCultists.Count < safetyThreshold)
            {
                Debug.LogWarning($"[SacrificeCommand] {ctx.Actor} 자살 방지: 필드에 뒷면 신도 카드가 부족함. (현재:{myFacedownCultists.Count}장, 필요:{safetyThreshold}장)");
                ctx.Cancelled = true;
                return;
            }

            int sacrificedCount = 0;
            foreach (var t in finalTargets)
            {
                if (ctx.GameState.IsGameEnded) break;

                var current = ctx.GameState.GetCard(t.InstanceId);
                // 필드의 뒷면 카드만 희생 가능 (필터에서 걸러지겠지만 이중 확인)
                if (current != null && current.Zone == Zone.Field && current.CardStatus == CardStatus.FieldBack)
                {
                    await _actionSystem.Destroy(ctx.Actor, current);
                    sacrificedCount++;
                }
            }

            // 결과 바인딩
            string bindVar = node["bind"]?.ToString();
            if (!string.IsNullOrEmpty(bindVar)) ctx.Vars[bindVar] = sacrificedCount;

            if (sacrificedCount >= requiredAmount)
            {
                var thenBranch = node["then"] as JArray;
                if (thenBranch != null) await runner.RunNodesAsync(thenBranch, ctx);
            }
            else
            {
                // 요구량을 채우지 못했으면 (취소 등) 컨텍스트 취소 처리
                Debug.LogWarning($"[SacrificeCommand] 요구량({requiredAmount}) 미달로 취소 처리. (희생됨: {sacrificedCount})");
                ctx.Cancelled = true;
            }
        }

        public bool CanExecute(JObject node, TriggerContext ctx)
        {
            if (ctx == null || ctx.GameState == null) return false;

            var from = node["from"] as JObject;
            if (from == null) return false;

            // 희생은 본인 필드에서만 가능하도록 강제
            from["owner"] = "Self";

            // 후보군 조회 (본인 제외)
            var candidates = _targets.Resolve(from, ctx, excludeSource: true);
            int requiredAmount = ValueResolver.ResolveInt(node["amount"], ctx, _targets, 1);

            if (candidates.Count < requiredAmount) return false;

            // [클라이언트 자살 방지]
            // ExecuteAsync와 동일한 기준: face-down 신도 카드가 (희생 요구량 + 1)장 이상이어야 함.
            // 희생 후 최소 1장이 남아 패배를 방지한다.
            var myFacedownCultists = ctx.GameState.GetAllCards().Where(c =>
                c.OwnerSeat == ctx.Actor &&
                c.Zone == Zone.Field &&
                c.CardStatus == CardStatus.FieldBack &&
                c.BaseData != null).ToList();

            return myFacedownCultists.Count >= requiredAmount + 1;
        }
    }
}
