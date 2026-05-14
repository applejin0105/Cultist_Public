using System.Threading.Tasks;
using Data.Models;
using Domain.Entities;
using Domain.Enums;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Effects.Core
{
    /// <summary>
    /// 트리거 실행 진입점. EffectProcessor를 대체.
    /// GameActionSystem 등 외부 시스템은 RunTriggerAsync 만 호출한다.
    /// </summary>
    public sealed class EffectRunner
    {
        private readonly Domain.State.Host.GameState _gameState;
        private readonly CommandRegistry _commands;
        private Systems.GameRuleSystem _gameRuleSystem;

        public EffectRunner(Domain.State.Host.GameState gameState, CommandRegistry commands)
        {
            _gameState = gameState;
            _commands = commands;
        }

        public void SetGameRuleSystem(Systems.GameRuleSystem gameRuleSystem)
        {
            _gameRuleSystem = gameRuleSystem;
        }

        private void CheckGameRules()
        {
            if (_gameRuleSystem == null) return;
            _gameRuleSystem.CheckFieldConditions();
            _gameRuleSystem.CheckStatConditions();
        }

        /// <summary>
        /// trigger 이름으로 cardsEffects.json에 정의된 명령 시퀀스를 실행한다.
        /// actor: 트리거를 일으킨 주체 (Echo source 판정 등에 사용).
        /// cause: 트리거 원인 카드 (옵션, 예: 이 카드를 파괴하려 한 카드).
        /// initVars: TriggerContext.Vars 초기값 (예: {"isEcho":1}). cardsEffects.json의
        ///   `{ "var": "isEcho" }` 토큰 해석에 사용된다.
        /// </summary>
        public async Task<TriggerContext> RunTriggerAsync(string trigger, CardInstance source, Player actor,
            CardInstance cause = null, System.Collections.Generic.IDictionary<string, int> initVars = null)
        {
            if (source == null || _gameState.IsGameEnded) return null;

            var nodes = EffectRegistry.Instance?.GetTrigger(source.CardId, trigger);
            var ctx = new TriggerContext(_gameState, source, actor, cause);

            if (initVars != null)
            {
                foreach (var kv in initVars) ctx.Vars[kv.Key] = kv.Value;
            }

            if (nodes == null || nodes.Count == 0) return ctx;

            Debug.Log($"[EffectRunner] <카드 효과 발생 시작> {source.BaseData?.Name}({source.InstanceId}) : {trigger} 발동 (actor={actor})");

            await RunNodesAsync(nodes, ctx);

            Debug.Log($"[EffectRunner] <카드 효과 발생 직후> {source.BaseData?.Name}({source.InstanceId}) : {trigger} 처리 완료");

            // [추가] 효과 종료 후 반드시 전역 승패/탈락 조건 검사
            CheckGameRules();

            return ctx;
        }

        /// <summary>
        /// 중첩 명령(예: If의 then/else, Sacrifice의 then) 실행에 사용.
        /// ctx.Cancelled가 true면 즉시 중단.
        /// </summary>
        public async Task RunNodesAsync(JArray nodes, TriggerContext ctx)
        {
            if (nodes == null || _gameState.IsGameEnded) return;

            foreach (var token in nodes)
            {
                if (ctx.Cancelled || _gameState.IsGameEnded) break;
                if (!(token is JObject node)) continue;

                var cmdName = node["cmd"]?.ToString();
                if (string.IsNullOrEmpty(cmdName)) continue;

                var handler = _commands.Get(cmdName);
                if (handler == null) continue;

                await handler.ExecuteAsync(node, ctx, this);
            }
        }
    }
}
