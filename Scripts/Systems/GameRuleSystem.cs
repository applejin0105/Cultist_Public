using System.Collections.Generic;
using System.Linq;
using Domain.Entities;
using Domain.Enums;
using Domain.State.Host;
using UnityEngine;
using Utils;

namespace Systems
{
    /// <summary>
    /// 게임 규칙 판정 엔진.
    /// 서버(GameState 기반)와 클라이언트(SyncCards 기반) 양쪽에서 동일한 로직으로 승패를 판단하기 위해 분리.
    /// </summary>
    public class GameRuleSystem
    {
        private readonly GameState _gameState;
        private readonly HashSet<int> _fourHorsemenCardIds = new HashSet<int>() { 11, 12, 13, 14 };

        public GameRuleSystem(GameState gameState)
        {
            _gameState = gameState;
        }

        /// <summary>
        /// 스탯 변동에 따른 승패 체크 (신도 0 패배, 만신전 7 승리, 최후의 1인)
        /// </summary>
        public void CheckStatConditions()
        {
            if (_gameState == null || _gameState.IsGameEnded) return;

            foreach (var playerState in _gameState.Players)
            {
                if (playerState.LifeStatus == PlayerLifeStatus.Eliminated) continue;

                // 1. 신도수가 0이 되는 경우 즉시 패배 (1라운드 보호 유지)
                if (playerState.Cultist <= 0)
                {
                    if (_gameState.TurnState.RoundNumber <= 1)
                    {
                        Debug.Log($"[GameRule] {playerState.Id} 1라운드 보호 중 (신도 0이지만 탈락 유예)");
                    }
                    else
                    {
                        Debug.Log($"[GameRule] {playerState.Id} 탈락 확정: [신도 수 0]");
                        EliminatePlayer(playerState.Id);
                    }
                }

                // 3. 만신전을 7개 모았다면 승리
                int pantheonCount = playerState.Symbols[(int)Symbols.Pantheon];
                if (pantheonCount >= 7)
                {
                    Debug.Log($"[GameRule] {playerState.Id} 승리: [만신전 7개 달성]");
                    EndGame(playerState.Id);
                    return;
                }
            }

            CheckLastManStanding();
        }

        /// <summary>
        /// 필드 상황 변동에 따른 체크 (4기사 강림)
        /// </summary>
        public void CheckFieldConditions()
        {
            if (_gameState == null || _gameState.IsGameEnded) return;

            int horsemenOnFieldCount = 0;
            List<string> horsemenDetails = new List<string>();

            // 전역 필드 사기사 체크
            foreach (var card in _gameState.Cards.Values)
            {
                if (card.Zone == Zone.Field &&
                    IsFourHorsemen(card.CardId) &&
                    card.CardStatus != CardStatus.FieldDestroyed)
                {
                    horsemenOnFieldCount++;
                    horsemenDetails.Add($"{card.BaseData.Name}(Owner: {card.OwnerSeat})");
                }
            }

            if (horsemenOnFieldCount > 0)
            {
                Debug.Log($"[GameRule] 현재 전역 필드 사기사 수: {horsemenOnFieldCount}/4 | 상세: {string.Join(", ", horsemenDetails)}");
            }

            if (horsemenOnFieldCount >= 4)
            {
                Debug.Log("[GameRule] <4기사 강림> 모든 사기사가 전역 필드에 존재. 최종 승자 판정 시작.");
                ResolveFourHorsemenVictory();
            }
        }

        private void EliminatePlayer(Player id)
        {
            var pState = _gameState.GetPlayerStateById(id);
            if (pState == null) return;

            Debug.Log($"[Server GameRule] <플레이어 탈락 처리 시작> Player ID: {id}");
            pState.SetPlayerLifeStatus(PlayerLifeStatus.Eliminated);
            pState.SetPlayerRole(PlayerRole.Spectator);

            if (_gameState.TurnState.ActivePlayer == id)
            {
                var turnSystem = App.Network.NetworkGameController.Instance?.GetTurnSystem();
                turnSystem?.ForceEndCurrentTurn();
            }
        }

        private void CheckLastManStanding()
        {
            var alivePlayers = _gameState.Players.Where(p => p.LifeStatus == PlayerLifeStatus.Alive).ToList();

            if (alivePlayers.Count == 1)
            {
                EndGame(alivePlayers[0].Id);
            }
            else if (alivePlayers.Count == 0)
            {
                App.Network.NetworkGameController.Instance?.TriggerGameEnd(-1);
            }
        }

        private void ResolveFourHorsemenVictory()
        {
            var candidates = _gameState.Players.Where(p => p.LifeStatus == PlayerLifeStatus.Alive).ToList();
            if (candidates.Count == 0)
            {
                EndGame(Player.Game);
                return;
            }

            var winner = candidates
                .OrderByDescending(p => p.Symbols[(int)Symbols.Pantheon])
                .ThenByDescending(p => p.Cultist)
                .ThenByDescending(p => p.Symbols.Sum())
                .FirstOrDefault();

            if (winner != null)
            {
                EndGame(winner.Id);
            }
        }

        private void EndGame(Player winnerSeat)
        {
            _gameState.SetGameEnd(winnerSeat);
            App.Network.NetworkGameController.Instance?.TriggerGameEnd((int)winnerSeat);
        }

        public bool IsFourHorsemen(int cardId)
        {
            return _fourHorsemenCardIds.Contains(cardId);
        }
    }
}
