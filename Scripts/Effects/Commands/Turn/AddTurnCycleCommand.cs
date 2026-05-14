using System.Threading.Tasks;
using Domain.State.Host;
using Effects.Core;
using Newtonsoft.Json.Linq;
using Systems;
using UnityEngine;

namespace Effects.Commands.Turn
{
    /// <summary>
    /// 현재 플레이어의 남은 사이클 횟수를 추가함 (예: Stonehenge)
    /// JSON: { "cmd": "AddTurnCycle", "amount": 1 }
    /// </summary>
    public sealed class AddTurnCycleCommand : ICommand
    {
        private readonly TurnSystem _turnSystem;
        private readonly IEffectGameState _gameState;

        public AddTurnCycleCommand(IEffectGameState gameState, TurnSystem turnSystem)
        {
            _gameState = gameState;
            _turnSystem = turnSystem;
        }

        public async Task ExecuteAsync(JObject node, TriggerContext ctx, EffectRunner runner)
        {
            int amount = node["amount"]?.Value<int>() ?? 1;

            // ExecuteAsync는 서버에서만 실행되므로 GameState로의 캐스팅이 안전함
            var fullState = _gameState as GameState;
            if (fullState == null) return;

            // 카드 소유자의 PlayerState에 보너스 사이클 적립
            var pState = fullState.GetPlayerStateById(ctx.Source.OwnerSeat);
            if (pState != null)
            {
                pState.BonusTurnCycles += amount;
                Debug.Log($"[Effect] {ctx.Source?.BaseData.Name} 효과로 {pState.Id}에게 턴 사이클 {amount}회 보너스 예약됨.");

                // 만약 현재가 해당 플레이어의 턴이라면 즉시 RemainingCycles도 증가시킴 (실시간 대응)
                if (fullState.TurnState.ActivePlayer == pState.Id)
                {
                    fullState.TurnState.RemainingCycles += amount;
                }
            }

            await Task.CompletedTask;
        }

        public bool CanExecute(JObject node, TriggerContext ctx) => true;
    }
}