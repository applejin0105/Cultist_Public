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
    /// 카드 추방 명령. 결과 바인딩 지원.
    /// </summary>
    public sealed class ExileCommand : ICommand
    {
        private readonly IEffectGameState _gameState;
        private readonly GameActionSystem _actionSystem;
        private readonly TargetResolver _targets;

        public ExileCommand(IEffectGameState gameState, GameActionSystem actionSystem,
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

            var amountTok = node["amount"];
            bool isAll = amountTok?.ToString().Equals("All", System.StringComparison.OrdinalIgnoreCase) == true;

            string selectionType = node["selectionType"]?.ToString() ?? "Auto";
            string selectionController = node["selectionController"]?.ToString() ?? "Actor"; // "Actor" or "TargetPlayer"
            bool singleOwner = node["singleOwner"]?.Value<bool>() ?? false;
            
            int count = 0;

            if (isAll)
            {
                // 상호작용 없는 대량 추방
                var allTargets = _targets.Resolve(from, ctx, excludeSource: true);
                foreach (var t in allTargets)
                {
                    await _actionSystem.Exile(ctx.Actor, t);
                    count++;
                }
            }
            else
            {
                (int min, int max) = ValueResolver.ResolveAmountRange(amountTok, ctx, _targets);
                var targetPlayers = _targets.ResolvePlayers(from["owner"], ctx).ToList();

                Player? lockedOwner = null;

                // 전체 플레이어 통합 max회 루프 또는 플레이어별 루프 여부 결정
                foreach (var p in targetPlayers)
                {
                    if (_gameState.IsGameEnded) break;

                    for (int i = 0; i < max; i++)
                    {
                        if (_gameState.IsGameEnded) break;

                        var candidates = _targets.Resolve(from, ctx, excludeSource: true)
                            .Where(c => c.OwnerSeat == p).ToList();

                        if (lockedOwner.HasValue && p != lockedOwner.Value) continue;
                        if (candidates.Count == 0) break;

                        List<CardInstance> picked;
                        if (selectionType == "Manual")
                        {
                            // 누가 고를 것인가? (발동자 vs 피해자)
                            Player picker = (selectionController == "TargetPlayer") ? p : ctx.Actor;
                            picked = await _targets.ManualPickOneOrDoneAsync(picker, candidates, singleOwner, excludeSource: true);
                        }
                        else
                        {
                            var randomOne = candidates[new System.Random().Next(candidates.Count)];
                            picked = new List<CardInstance> { randomOne };
                        }

                        if (picked == null || picked.Count == 0) break;

                        var target = picked[0];
                        if (singleOwner && !lockedOwner.HasValue) lockedOwner = target.OwnerSeat;

                        await _actionSystem.Exile(ctx.Actor, target);
                        count++;
                        await Task.Delay(200);
                    }
                }
            }

            // 결과 바인딩
            string bindVar = node["bind"]?.ToString();
            if (!string.IsNullOrEmpty(bindVar)) ctx.Vars[bindVar] = count;
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}
