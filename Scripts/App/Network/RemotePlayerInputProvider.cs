using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using Effects.Interfaces;
using UnityEngine;

namespace App.Network
{
    /// <summary>
    /// 플레이어별로 독립적인 입력 대기 상태를 관리하여 혼선을 방지합니다.
    /// </summary>
    public class RemotePlayerInputProvider : IPlayerInputProvider
    {
        private readonly NetworkGameController _controller;

        // 플레이어별로 TaskCompletionSource를 관리하는 내부 클래스
        private class PlayerInputState
        {
            public TaskCompletionSource<List<int>> TargetSelectTcs;
            public TaskCompletionSource<int> DrawActionTcs;
            public TaskCompletionSource<int> KeepCardTcs;
            public TaskCompletionSource<int> TradeSelectTcs;
            public int? PendingTradeCardId;
        }

        private readonly Dictionary<Player, PlayerInputState> _playerStates = new Dictionary<Player, PlayerInputState>();

        public RemotePlayerInputProvider(NetworkGameController controller)
        {
            _controller = controller;
        }

        private PlayerInputState GetOrCreateState(Player player)
        {
            if (!_playerStates.TryGetValue(player, out var state))
            {
                state = new PlayerInputState();
                _playerStates[player] = state;
            }
            return state;
        }

        public Task<List<CardInstance>> SelectTargetsAsync(Player player, List<CardInstance> candidates, int min, int max, bool singleOwner = false)
        {
            Debug.Log($"[RemoteInput] {player} 에게 타겟 선택 요청. (후보: {candidates.Count}개, SingleOwner: {singleOwner})");
            var state = GetOrCreateState(player);
            state.TargetSelectTcs = new TaskCompletionSource<List<int>>();

            List<int> candidatesIds = candidates.Select(c => c.InstanceId).ToList();
            var gamePlayer = _controller.GetPlayerComponent(player);

            if (gamePlayer != null) gamePlayer.TargetRpc_RequestSelectTargets(candidatesIds, min, max, singleOwner);
            else return Task.FromResult(candidates.Take(min).ToList());

            return state.TargetSelectTcs.Task.ContinueWith(task =>
            {
                var selectedIds = task.Result;
                var gState = _controller.ServerGameState;
                return selectedIds.Select(id => gState.Cards[id]).ToList();
            });
        }

        public Task<DrawPhaseAction> SelectDrawPhaseAsync(Player player, bool canDraw, bool canTrade)
        {
            Debug.Log($"[RemoteInput] {player} 에게 DrawPhaseAction 선택 요청 전송.");
            var state = GetOrCreateState(player);
            state.DrawActionTcs = new TaskCompletionSource<int>();
            state.PendingTradeCardId = null;

            var gamePlayer = _controller.GetPlayerComponent(player);
            if (gamePlayer != null) gamePlayer.TargetRpc_RequestDrawAction(canDraw, canTrade);
            
            return state.DrawActionTcs.Task.ContinueWith(task => (DrawPhaseAction)task.Result);
        }

        public Task<CardInstance> SelectCardToKeepAsync(Player player, List<CardInstance> cardInstances)
        {
            var state = GetOrCreateState(player);
            state.KeepCardTcs = new TaskCompletionSource<int>();
            
            var ids = cardInstances.Select(c => c.InstanceId).ToArray();
            var cardIds = cardInstances.Select(c => c.CardId).ToArray();

            var gamePlayer = _controller.GetPlayerComponent(player);
            if (gamePlayer != null) gamePlayer.TargetRpc_RequestSelectCardToKeep(ids, cardIds);
            else state.KeepCardTcs.SetResult(ids.FirstOrDefault());

            return state.KeepCardTcs.Task.ContinueWith(t =>
            {
                int selectedId = t.Result;
                return _controller.ServerGameState.Cards.GetValueOrDefault(selectedId);
            });
        }

        public Task<CardInstance> SelectCardFromTradeAsync(Player player, List<CardInstance> tradeCards)
        {
            Debug.Log($"[RemoteInput] {player} 에게 교역소 카드 선택 요청 전송.");
            var state = GetOrCreateState(player);
            state.TradeSelectTcs = new TaskCompletionSource<int>();
            var ids = tradeCards.Select(c => c.InstanceId).ToList();

            if (state.PendingTradeCardId.HasValue)
            {
                int pending = state.PendingTradeCardId.Value;
                state.PendingTradeCardId = null;
                Debug.Log($"[RemoteInput] {player}의 pending Trade 카드({pending}) 즉시 적용.");
                state.TradeSelectTcs.TrySetResult(pending);
            }
            else
            {
                var gamePlayer = _controller.GetPlayerComponent(player);
                if (gamePlayer != null) gamePlayer.TargetRpc_RequestSelectCardFromTrade(ids);
                else state.TradeSelectTcs.SetResult(ids.FirstOrDefault());
            }

            return state.TradeSelectTcs.Task.ContinueWith(t =>
            {
                int selectedId = t.Result;
                return _controller.ServerGameState.Cards.GetValueOrDefault(selectedId);
            });
        }

        // --- 클라이언트 응답 수신부 (Player 파라미터 추가) ---

        public void ReceiveTargetResponse(Player player, List<int> selectedInstanceIds) =>
            GetOrCreateState(player).TargetSelectTcs?.TrySetResult(selectedInstanceIds);

        public void ReceiveDrawActionResponse(Player player, int actionType) =>
            GetOrCreateState(player).DrawActionTcs?.TrySetResult(actionType);

        public void ReceiveKeepCardResponse(Player player, int selectedInstanceId) =>
            GetOrCreateState(player).KeepCardTcs?.TrySetResult(selectedInstanceId);

        public void ReceiveTradeSelectResponse(Player player, int selectedInstanceId)
        {
            var state = GetOrCreateState(player);

            if (state.TradeSelectTcs != null && !state.TradeSelectTcs.Task.IsCompleted)
            {
                state.TradeSelectTcs.TrySetResult(selectedInstanceId);
                return;
            }

            if (state.DrawActionTcs != null && !state.DrawActionTcs.Task.IsCompleted)
            {
                state.PendingTradeCardId = selectedInstanceId;
                state.DrawActionTcs.TrySetResult((int)DrawPhaseAction.Trade);
                Debug.Log($"[RemoteInput] {player}가 Trade 카드({selectedInstanceId}) 클릭으로 Trade 액션 결정.");
                return;
            }

            Debug.Log($"[RemoteInput] {player}의 Trade 카드({selectedInstanceId}) 선택 무시 — 응답 대기 단계 아님.");
        }
    }
}
