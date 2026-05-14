using System.Threading.Tasks;
using Domain.Enums;
using Domain.State.Host;
using Effects.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Effects.Commands.Resource
{
    /// <summary>
    /// 자원 획득 명령.
    /// JSON: { "cmd": "Get", "res": "...", "amount": dynamicInt }
    ///
    /// res:    "influence" | "unity" | "monotheism" | "polytheism" | "strength" | "pantheon" | "cultist"
    /// amount: ValueResolver가 받는 dynamicInt (정수, { "var": "n" }, { "cardCount": ... } 등)
    ///
    /// 자원은 Source 카드의 OwnerSeat 플레이어에게 가산된다.
    /// </summary>
    public sealed class GetCommand : ICommand
    {
        private readonly IEffectGameState _gameState;
        private readonly TargetResolver _targets;

        public GetCommand(IEffectGameState gameState, TargetResolver targets)
        {
            _gameState = gameState;
            _targets = targets;
        }

        public Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            string res = node["res"]?.ToString();
            int amount = ValueResolver.ResolveInt(node["amount"], ctx, _targets, 0);

            if (string.IsNullOrEmpty(res) || amount == 0)
            {
                Debug.LogWarning($"[Get] 무효 인자: res={res}, amount={amount}");
                return Task.CompletedTask;
            }

            // ExecuteAsync는 서버에서만 실행되므로 GameState로의 캐스팅이 안전함
            var fullState = _gameState as GameState;
            var pState = fullState?.GetPlayerStateById(ctx.Source.OwnerSeat);
            
            if (pState == null)
            {
                Debug.LogWarning($"[Get] PlayerState 없음: {ctx.Source.OwnerSeat}");
                return Task.CompletedTask;
            }

            string key = res.Trim().ToLowerInvariant();

            if (key == "cultist")
            {
                pState.AddPermanentCultist(amount);
                Debug.Log($"[Get] {ctx.Source.OwnerSeat} cultist {amount:+#;-#;0} (now {pState.Cultist})");
                return Task.CompletedTask;
            }

            int idx = ResolveSymbolIndex(key);
            if (idx < 0)
            {
                Debug.LogWarning($"[Get] 알 수 없는 res: {res}");
                return Task.CompletedTask;
            }

            pState.AddPermanentSymbol(idx, amount);
            Debug.Log($"[Get] {ctx.Source.OwnerSeat} {key} {amount:+#;-#;0} (now {pState.Symbols[idx]})");
            return Task.CompletedTask;
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;

        private static int ResolveSymbolIndex(string key) => key switch
        {
            "influence"  => (int)Symbols.Influence,
            "unity"      => (int)Symbols.Unity,
            "monotheism" => (int)Symbols.Monotheism,
            "polytheism" => (int)Symbols.Polytheism,
            "strength"   => (int)Symbols.Strength,
            "pantheon"   => (int)Symbols.Pantheon,
            _ => -1
        };
    }
}
