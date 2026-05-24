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
    /// 호스트의 효과 코드가 await로 클라이언트 입력을 기다릴 수 있게 RPC와 Task를 연결하는 어댑터.
    /// 게임이 순차 턴제라 한 번에 한 플레이어만 입력을 기다리므로, TCS를 평탄하게 한 묶음만 들고 있다.
    /// </summary>
    public sealed class RemotePlayerInputProvider : IPlayerInputProvider
    {
        private readonly NetworkGameController _controller;

        private TaskCompletionSource<List<int>> _targetSelectTcs;
        private TaskCompletionSource<int> _drawActionTcs;
        private TaskCompletionSource<int> _keepCardTcs;
        private TaskCompletionSource<int> _tradeSelectTcs;
        private int? _pendingTradeCardId;

        public RemotePlayerInputProvider(NetworkGameController controller)
        {
            _controller = controller;
        }

        public Task<List<CardInstance>> SelectTargetsAsync(Player player, List<CardInstance> candidates, int min,
            int max, bool singleOwner = false)
        {
            Debug.Log($"[RemoteInput] {player} 에게 타겟 선택 요청. (후보: {candidates.Count}개, SingleOwner: {singleOwner})");
            _targetSelectTcs = new TaskCompletionSource<List<int>>();

            List<int> candidatesIds = candidates.Select(c => c.InstanceId).ToList();
            var gamePlayer = _controller.GetPlayerComponent(player);

            if (gamePlayer != null) gamePlayer.TargetRpc_RequestSelectTargets(candidatesIds, min, max, singleOwner);
            else return Task.FromResult(candidates.Take(min).ToList());

            return _targetSelectTcs.Task.ContinueWith(task =>
            {
                var selectedIds = task.Result;
                var gState = _controller.ServerGameState;
                return selectedIds.Select(id => gState.Cards[id]).ToList();
            });
        }

        public Task<DrawPhaseAction> SelectDrawPhaseAsync(Player player, bool canDraw, bool canTrade)
        {
            Debug.Log($"[RemoteInput] {player} 에게 DrawPhaseAction 선택 요청 전송.");
            _drawActionTcs = new TaskCompletionSource<int>();
            _pendingTradeCardId = null;

            var gamePlayer = _controller.GetPlayerComponent(player);
            if (gamePlayer != null) gamePlayer.TargetRpc_RequestDrawAction(canDraw, canTrade);

            return _drawActionTcs.Task.ContinueWith(task => (DrawPhaseAction)task.Result);
        }

        public Task<CardInstance> SelectCardToKeepAsync(Player player, List<CardInstance> cardInstances)
        {
            _keepCardTcs = new TaskCompletionSource<int>();

            var ids = cardInstances.Select(c => c.InstanceId).ToArray();
            var cardIds = cardInstances.Select(c => c.CardId).ToArray();

            var gamePlayer = _controller.GetPlayerComponent(player);
            if (gamePlayer != null) gamePlayer.TargetRpc_RequestSelectCardToKeep(ids, cardIds);
            else _keepCardTcs.SetResult(ids.FirstOrDefault());

            return _keepCardTcs.Task.ContinueWith(t =>
            {
                int selectedId = t.Result;
                return _controller.ServerGameState.Cards.GetValueOrDefault(selectedId);
            });
        }

        public Task<CardInstance> SelectCardFromTradeAsync(Player player, List<CardInstance> tradeCards)
        {
            Debug.Log($"[RemoteInput] {player} 에게 교역소 카드 선택 요청 전송.");
            _tradeSelectTcs = new TaskCompletionSource<int>();
            var ids = tradeCards.Select(c => c.InstanceId).ToList();

            if (_pendingTradeCardId.HasValue)
            {
                int pending = _pendingTradeCardId.Value;
                _pendingTradeCardId = null;
                Debug.Log($"[RemoteInput] pending Trade 카드({pending}) 즉시 적용.");
                _tradeSelectTcs.TrySetResult(pending);
            }
            else
            {
                var gamePlayer = _controller.GetPlayerComponent(player);
                if (gamePlayer != null) gamePlayer.TargetRpc_RequestSelectCardFromTrade(ids);
                else _tradeSelectTcs.SetResult(ids.FirstOrDefault());
            }

            return _tradeSelectTcs.Task.ContinueWith(t =>
            {
                int selectedId = t.Result;
                return _controller.ServerGameState.Cards.GetValueOrDefault(selectedId);
            });
        }

        // 클라이언트 응답 수신부 — 현재 대기 중인 TCS 하나를 풀어준다.

        public void ReceiveTargetResponse(List<int> selectedInstanceIds) =>
            _targetSelectTcs?.TrySetResult(selectedInstanceIds);

        public void ReceiveDrawActionResponse(int actionType) =>
            _drawActionTcs?.TrySetResult(actionType);

        public void ReceiveKeepCardResponse(int selectedInstanceId) =>
            _keepCardTcs?.TrySetResult(selectedInstanceId);

        public void ReceiveTradeSelectResponse(int selectedInstanceId)
        {
            if (_tradeSelectTcs != null && !_tradeSelectTcs.Task.IsCompleted)
            {
                _tradeSelectTcs.TrySetResult(selectedInstanceId);
                return;
            }

            if (_drawActionTcs != null && !_drawActionTcs.Task.IsCompleted)
            {
                _pendingTradeCardId = selectedInstanceId;
                _drawActionTcs.TrySetResult((int)DrawPhaseAction.Trade);
                Debug.Log($"[RemoteInput] Trade 카드({selectedInstanceId}) 클릭으로 Trade 액션 결정.");
                return;
            }

            Debug.Log($"[RemoteInput] Trade 카드({selectedInstanceId}) 선택 무시 — 응답 대기 단계 아님.");
        }
    }
}