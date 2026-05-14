using System.Linq;
using Domain.Enums;
using Effects.Core;
using Newtonsoft.Json.Linq;

namespace Effects.Conditions
{
    /// <summary>
    /// 모든 조건문의 기반 클래스. 공통 타겟팅 및 유틸리티 제공.
    /// </summary>
    public abstract class ConditionBase : ICondition
    {
        public abstract bool Evaluate(JObject node, TriggerContext ctx);

        protected Player ResolveTarget(JObject node, TriggerContext ctx)
        {
            string targetStr = node["target"]?.ToString() ?? "Self";

            return targetStr switch
            {
                "Self" => ctx.Actor,
                "Opponent" => GetFirstOpponent(ctx.GameState, ctx.Actor),
                _ => ctx.Actor
            };
        }

        private Player GetFirstOpponent(IEffectGameState gameState, Player actor)
        {
            // 생존 플레이어 중 actor가 아닌 첫 번째 플레이어를 찾습니다.
            return gameState.GetAlivePlayers().FirstOrDefault(p => p != actor && p != Player.Game);
        }
    }
}