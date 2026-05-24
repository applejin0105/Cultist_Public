using System.Collections.Generic;
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
    /// 카드 제거 명령(Destroy / Exile / Sacrifice)의 공통 골격.
    /// </summary>
    public abstract class TargetedRemovalCommand : ICommand
    {
        protected readonly IEffectGameState _gameState;
        protected readonly GameActionSystem _actionSystem;
        protected readonly TargetResolver _targets;
        protected readonly IRandomSource _rng;

        protected TargetedRemovalCommand(IEffectGameState gameState, GameActionSystem actionSystem,
            TargetResolver targets, IRandomSource rng)
        {
            _gameState = gameState;
            _actionSystem = actionSystem;
            _targets = targets;
            _rng = rng;
        }

        /// <summary>
        /// 공통 진입점. 자식은 hook으로 동작을 추가하거나 미세 조정한다.
        /// </summary>
        public async Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            // 사전 검증: Sacrifice가 owner 강제 + CanPay 체크
            if (!await PreCheckAsync(node, ctx))
            {
                ctx.Cancelled = true;
                return;
            }

            var from = node["from"] as JObject;
            if (from == null) return;

            var amountTok = node["amount"];
            bool isAll = amountTok?.ToString().Equals("All", System.StringComparison.OrdinalIgnoreCase) == true;
            int processed = 0;

            // amount == "All": 후보 전부 일괄 처리
            if (isAll)
            {
                var allTargets = _targets.Resolve(from, ctx, excludeSource: true);
                foreach (var t in allTargets)
                {
                    if (_gameState.IsGameEnded) break;
                    await ApplyAsync(ctx.Actor, t);
                    processed++;
                }
            }
            else
            {
                // selectionType에 따라 타겟 모델 분기
                (int _, int max) = ValueResolver.ResolveAmountRange(amountTok, ctx, _targets);
                bool singleOwner = node["singleOwner"]?.Value<bool>() ?? false;
                string selectionType = node["selectionType"]?.ToString() ?? "Auto";

                if (selectionType == "Manual")
                {
                    // 단일 풀: 사용자가 후보 전체에서 픽. 첫 픽이 owner 결정 (singleOwner=true일 때).
                    processed = await RunPickLoopAsync(from, ctx, max, singleOwner, selectionType, ctx.Actor);
                }
                else
                {
                    // 명단 루프: 각 플레이어에게 자동(랜덤) 적용.
                    var targetPlayers = _targets.ResolvePlayers(from["owner"], ctx).ToList();
                    foreach (var p in targetPlayers)
                    {
                        if (_gameState.IsGameEnded) break;
                        processed += await RunPickLoopAsync(from, ctx, max, singleOwner, selectionType,
                            ctx.Actor, ownerLock: p);
                    }
                }
            }

            // 후처리 — Sacrifice가 then 분기 또는 Cancelled
            await PostLoopAsync(node, ctx, runner, processed);
        }

        /// <summary>
        /// 실행 전 사전 검증/조작. false 반환 시 ExecuteAsync는 Cancelled로 종료.
        /// 기본 구현은 항상 true (검증 없음).
        /// </summary>
        protected virtual Task<bool> PreCheckAsync(JObject node, TriggerContext ctx)
            => Task.FromResult(true);

        /// <summary>자식이 구현: 실제 제거 액션.</summary>
        protected abstract Task ApplyAsync(Player actor, CardInstance target);

        /// <summary>
        /// 픽 루프 종료 후 후처리. processed는 실제로 처리된 카드 수.
        /// 기본 구현은 no-op.
        /// </summary>
        protected virtual Task PostLoopAsync(JObject node, TriggerContext ctx,
            EffectRunner runner, int processed)
            => Task.CompletedTask;

        /// <summary>
        /// 후보군에서 max장까지 픽-앤-적용 루프. 처리된 카드 수를 반환.
        /// ownerLock가 지정되면 해당 owner의 카드만 후보로 한정.
        /// singleOwner는 첫 픽 이후 동일 owner로 잠그며, ownerLock가 이미 지정된 경우엔 무의미.
        /// </summary>
        protected async Task<int> RunPickLoopAsync(
            JObject from, TriggerContext ctx,
            int max, bool singleOwner, string selectionType, Player picker,
            Player? ownerLock = null)
        {
            Player? lockedOwner = ownerLock;
            int processed = 0;

            for (int i = 0; i < max; i++)
            {
                if (_gameState.IsGameEnded) break;

                var candidates = _targets.Resolve(from, ctx, excludeSource: true);
                if (lockedOwner.HasValue)
                {
                    candidates = candidates.Where(c => c.OwnerSeat == lockedOwner.Value).ToList();
                }

                if (candidates.Count == 0) break;

                List<CardInstance> picked;
                if (selectionType == "Manual")
                {
                    picked = await _targets.ManualPickOneOrDoneAsync(picker, candidates, singleOwner,
                        excludeSource: true);
                }
                else
                {
                    var randomOne = candidates[_rng.Next(candidates.Count)];
                    picked = new List<CardInstance> { randomOne };
                }

                if (picked == null || picked.Count == 0)
                {
                    Debug.Log($"[{GetType().Name}] 사용자가 'Done' 선택 또는 후보 없음. 루프 종료.");
                    break;
                }

                var target = picked[0];
                var current = ctx.GameState.GetCard(target.InstanceId);
                if (current == null || current.Zone != Zone.Field) continue;

                if (singleOwner && !lockedOwner.HasValue)
                {
                    lockedOwner = current.OwnerSeat;
                }

                await ApplyAsync(ctx.Actor, current);
                processed++;
                await Task.Delay(200);
            }

            return processed;
        }
    }
}