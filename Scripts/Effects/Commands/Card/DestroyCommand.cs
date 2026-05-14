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
    /// 카드 파괴 명령. 결과 바인딩 지원.
    /// JSON: { "cmd": "Destroy", ..., "bind": "k" }
    /// </summary>
    public sealed class DestroyCommand : ICommand
    {
        private readonly IEffectGameState _gameState;
        private readonly GameActionSystem _actionSystem;
        private readonly TargetResolver _targets;

        public DestroyCommand(IEffectGameState gameState, GameActionSystem actionSystem,
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
            (int min, int max) = ValueResolver.ResolveAmountRange(amountTok, ctx, _targets);
            
            bool singleOwner = node["singleOwner"]?.Value<bool>() ?? false;
            string selectionType = node["selectionType"]?.ToString() ?? "Auto";
            
            Player? lockedOwner = null;
            int count = 0;

            for (int i = 0; i < max; i++)
            {
                if (ctx.GameState.IsGameEnded) break;

                // 매 루프마다 후보군 최신화 (파괴된 카드는 제외되어야 함)
                var candidates = _targets.Resolve(from, ctx, excludeSource: true);
                if (lockedOwner.HasValue)
                {
                    candidates = candidates.Where(c => c.OwnerSeat == lockedOwner.Value).ToList();
                }

                if (candidates.Count == 0) break;

                List<CardInstance> picked;
                if (selectionType == "Manual")
                {
                    // "Up to N"을 위해 한 장씩 선택 요청 (min: 0, max: 1)
                    picked = await _targets.ManualPickOneOrDoneAsync(ctx.Actor, candidates, singleOwner, excludeSource: true);
                }
                else
                {
                    // Auto: 랜덤하게 한 장 선택
                    var randomOne = candidates[new System.Random().Next(candidates.Count)];
                    picked = new List<CardInstance> { randomOne };
                }

                if (picked == null || picked.Count == 0)
                {
                    Debug.Log($"[Destroy] 사용자가 'Done' 선택 또는 후보 없음. 루프 종료 (파괴 수: {count})");
                    break;
                }

                var target = picked[0];
                var current = ctx.GameState.GetCard(target.InstanceId);
                if (current != null && current.Zone == Zone.Field)
                {
                    if (singleOwner && !lockedOwner.HasValue)
                    {
                        lockedOwner = current.OwnerSeat;
                        Debug.Log($"[Destroy] 첫 파괴 발생. 타겟 플레이어 {lockedOwner.Value} 로 고정.");
                    }

                    await _actionSystem.Destroy(ctx.Actor, current);
                    count++;
                    
                    // 연출을 위한 짧은 대기 (필터링된 목록을 위해 필수)
                    await Task.Delay(200);
                }
            }

            // 결과 바인딩: 실제로 파괴한 카드 수를 변수에 저장
            string bindVar = node["bind"]?.ToString();
            if (!string.IsNullOrEmpty(bindVar))
            {
                ctx.Vars[bindVar] = count;
                Debug.Log($"[Destroy] 결과 바인딩: {bindVar} = {count}");
            }
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}
