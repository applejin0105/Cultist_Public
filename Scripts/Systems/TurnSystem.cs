using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Enums;
using Domain.Policies;
using Domain.State;
using Domain.State.Host;
using Effects.Interfaces;
using UnityEngine;
using Utils;

namespace Systems
{
    public sealed class TurnSystem
    {
        private readonly GameState _gameState;
        private readonly TurnState _turnState;

        private PhaseSystem _phaseSystem;
        private GameActionSystem _gameActionSystem;
        private GameRuleSystem _gameRuleSystem;

        private IPlayerInputProvider _playerInputProvider;

        private readonly System.Random _rng;

        public TurnSystem(GameState gameState, PhaseSystem phaseSystem, GameActionSystem gameActionSystem,
            System.Random rng, IPlayerInputProvider playerInputProvider)
        {
            _gameState = gameState;
            _phaseSystem = phaseSystem;
            _gameActionSystem = gameActionSystem;
            _turnState = gameState.TurnState;
            _rng = rng;
            _playerInputProvider = playerInputProvider;

            _phaseSystem.OnPhaseChanged += newPhase => _turnState.Phase = newPhase;
        }

        public void SetGameRuleSystem(GameRuleSystem gameRuleSystem)
        {
            _gameRuleSystem = gameRuleSystem;
        }

        private void CheckGameRules()
        {
            if (_gameRuleSystem == null) return;
            _gameRuleSystem.CheckFieldConditions();
            _gameRuleSystem.CheckStatConditions();
        }

        public async void StartTurn()
        {
            Debug.Log($"[TurnSystem] --- {_turnState.ActivePlayer} 턴 시작 ---");

            CheckGameRules();

            var activatedPlayerId = _turnState.ActivePlayer;
            if (activatedPlayerId == null) return;

            var pState = _gameState.GetPlayerStateById(activatedPlayerId.Value);

            // [규격화] 턴 시작 시 기본 사이클 횟수 설정 (표준 1회 + 보너스)
            int bonus = pState != null ? pState.BonusTurnCycles : 0;
            _turnState.RemainingCycles = 1 + bonus;

            if (pState != null) pState.BonusTurnCycles = 0; // 보너스 소모

            // 모든 클라이언트에 턴 시작 배너 알림.
            global::App.Network.NetworkGameController.Instance?.RpcNotifyTurnStart((int)activatedPlayerId.Value);

            await StartNewCycle();
        }

        private async Task StartNewCycle()
        {
            var activeId = _turnState.ActivePlayer;
            if (!activeId.HasValue) return;

            var pState = _gameState.GetPlayerStateById(activeId.Value);
            Debug.Log($"[TurnSystem] 새 사이클 시작. (남은 횟수: {_turnState.RemainingCycles})");

            // [규격화] 사이클 시작은 무조건 Draw.StandBy에서 시작
            await ProcessDrawFlow(pState, PhaseState.From(Phase.Draw.StandBy));
        }

        public void ForceEndCurrentTurn()
        {
            Debug.Log($"[TurnSystem] 턴 강제 종료 요청됨. (현재:{_turnState.ActivePlayer})");

            // 현재 사이클을 즉시 종료하고 다음 플레이어로 넘김
            _turnState.RemainingCycles = 0;
            _phaseSystem.ChangePhase(PhaseState.StandBy);

            // [중요] EndCurrentPlayerTurn 내부에서 다음 생존 플레이어를 찾는 로직이 실행됨
            EndCurrentPlayerTurnInternal(true);
        }

        public void EndCurrentPlayerTurn()
        {
            EndCurrentPlayerTurnInternal(false);
        }

        private void EndCurrentPlayerTurnInternal(bool force)
        {
            var activePlayerId = _turnState.ActivePlayer;
            if (!activePlayerId.HasValue) return;

            var pState = _gameState.GetPlayerStateById(activePlayerId.Value);

            // [규격화] 패에 카드가 남아있다면 사이클 종료 불가 (강제 종료가 아닐 때만)
            if (!force && pState.Hand.Count > 0)
            {
                Debug.LogWarning($"[TurnSystem] {activePlayerId.Value}의 패에 카드가 남아있어 사이클을 마칠 수 없습니다.");
                return;
            }

            // [규격화] 사이클 횟수 소모
            if (_turnState.RemainingCycles > 0) _turnState.RemainingCycles--;

            if (!force && _turnState.RemainingCycles > 0)
            {
                Debug.Log($"[TurnSystem] 사이클 완료. 추가 사이클 진입 ({_turnState.RemainingCycles}회 남음)");

                // [중요] 다음 사이클 시작 전에 페이즈를 초기화 (Draw.StandBy로)
                _ = StartNewCycle();
                return;
            }

            // 진짜 턴 종료
            _phaseSystem.ChangePhase(PhaseState.StandBy);

            // 다음 플레이어 탐색
            int oldIndex = _turnState.CurrentPlayerIndex;
            int nextIndex = oldIndex;
            int loopCount = 0;
            int totalPlayers = _turnState.TurnOrder.Count;
            bool foundAlivePlayer = false;

            do
            {
                nextIndex = (nextIndex + 1) % totalPlayers;
                loopCount++;

                if (loopCount > totalPlayers) break;

                var candidateId = _turnState.TurnOrder[nextIndex];
                var candidateState = _gameState.GetPlayerStateById(candidateId);

                if (candidateState.LifeStatus == PlayerLifeStatus.Alive)
                {
                    foundAlivePlayer = true;
                    break;
                }
            } while (true);

            if (foundAlivePlayer)
            {
                // [교정] 실제로 인덱스를 변경하기 전에 비교를 수행해야 함.
                if (nextIndex <= oldIndex)
                {
                    Debug.Log($"[TurnSystem] 라운드 종료 (Index: {oldIndex} -> {nextIndex}). 새 라운드 시작");
                    StartNewRound();
                }
                else
                {
                    Debug.Log($"[TurnSystem] 다음 턴 시작 (Index: {oldIndex} -> {nextIndex})");
                    _turnState.CurrentPlayerIndex = nextIndex;
                    StartTurn();
                }
            }
            else
            {
                Debug.Log($"[TurnSystem] 생존자가 없습니다. 게임 종료 대기.");
            }
        }

        public void SetOrder(List<Player> order)
        {
            _turnState.SetPlayers(order);
        }

        private async Task ProcessDrawFlow(PlayerState activePlayer, PhaseState targetPhase)
        {
            // [규격화] 모든 드로우 관련 로직은 DrawRule.cs에 정의된 규격에 따라 일괄 처리함.
            _phaseSystem.ChangePhase(targetPhase);

            if (targetPhase == PhaseState.From(Phase.Draw.StandBy))
            {
                // 1. 특수 DrawRule 검사 (기존 유지)
                if (activePlayer.NextDrawRule != null && activePlayer.NextDrawRule.SkipSelection)
                {
                    _phaseSystem.ChangePhase(PhaseState.From(Phase.Draw.Draw));
                    await ExecuteDrawPhase(activePlayer);
                }
                else
                {
                    // 덱이 비었더라도 교역소가 있다면 선택 기회를 제공한다.
                    // PerformDrawOrTradeChoice 내부에서 canDraw/canTrade를 계산함.
                    await PerformDrawOrTradeChoice(activePlayer);
                }
            }

            // Action (Draw/Trade) 이후에는 자동으로 Play로 넘어감
            _phaseSystem.AdvancePhase();

            // 만약 드로우했는데 패가 없다면 (매우 드문 경우) 즉시 Play 상태 유지 (효과 사용 등 가능)
            if (activePlayer.Hand.Count == 0 && _phaseSystem.CurrentPhaseState.Main == Phase.Main.Play)
            {
                // 사용자가 End Turn을 누를 때까지 Play 상태 유지
            }
        }

        private async Task PerformDrawOrTradeChoice(PlayerState activePlayer)
        {
            // 라운드 및 조건에 따른 활성화 여부 계산
            bool canTrade = _gameState.TradeDeckCount > 0;
            // 덱이 비었어도 'Draw' 버튼은 활성화됨 (누르면 사기사 카드가 나옴)

            bool canDraw = (_turnState.RoundNumber != 0);

            // 만약 덱이 비어있고 교역소도 비어있다면, 선택지 없이 즉시 Draw(사기사) 실행
            if (!canTrade && IsPlayerDeckEmpty(activePlayer))
            {
                Debug.Log($"[TurnSystem] {activePlayer.Id} 덱/교역소 모두 소진 → 사기사 즉시 강제 호출.");
                _phaseSystem.ChangePhase(PhaseState.From(Phase.Draw.Draw));
                await ExecuteDrawPhase(activePlayer);
                return;
            }

            // 유저 입력을 Await 함
            var action = await _playerInputProvider.SelectDrawPhaseAsync(activePlayer.Id, canDraw, canTrade);

            if (action == global::Effects.Interfaces.DrawPhaseAction.Trade)
            {
                _phaseSystem.ChangePhase(PhaseState.From(Phase.Draw.Trade));
                await _gameActionSystem.Trade(activePlayer.Id);
            }
            else // Draw
            {
                _phaseSystem.ChangePhase(PhaseState.From(Phase.Draw.Draw));
                await ExecuteDrawPhase(activePlayer);
            }
        }

        private bool IsPlayerDeckEmpty(PlayerState player)
        {
            var deck = _gameState.GetDeckStateById(player.Id);
            return deck == null || deck.DeckCount() == 0;
        }

        private async Task ExecuteDrawPhase(PlayerState player)
        {
            var rule = player.ConsumeDrawRule() ?? DrawRule.Standard;
            await _gameActionSystem.Draw(player.Id, rule);
        }

        public void PlayerRequestedAdvancePhase(Player player)
        {
            if (_turnState.ActivePlayer != player) return;

            // [규격화] Play -> End (StandBy) 전이만 허용. (Hand 0 체크는 EndCurrentPlayerTurn에서 수행)
            if (_phaseSystem.CurrentPhaseState == PhaseState.From(Phase.Play.Play))
            {
                Debug.Log($"[TurnSystem] {player}가 Play 단계를 마치고 사이클 마감을 요청합니다.");
                _phaseSystem.AdvancePhase();
                EndCurrentPlayerTurn();
            }
        }

        #region Round & Order Management

        public void PrepareFirstRound()
        {
            List<Player> firstOrder = SetOrderWithSymbolsFirstRound();
            SetOrder(firstOrder);
            Debug.Log($"[TurnSystem] 첫 턴 순서 결정: {string.Join(", ", firstOrder)}");
        }

        public void StartFirstTurn()
        {
            _turnState.RoundNumber = 1;
            Debug.Log($"[TurnSystem] Round 1 시작.");
            StartTurn();
        }

        public void StartNewRound()
        {
            _turnState.RoundNumber++;
            List<Player> nextOrder = SetOrderWithSymbols();
            SetOrder(nextOrder);
            Debug.Log($"Round {_turnState.RoundNumber} 시작. 순서: {string.Join(", ", nextOrder)}");
            StartTurn();
        }

        private List<Player> SetOrderWithSymbolsFirstRound()
        {
            List<(int inf, int str, Player player, bool isAlive)> orderList = new List<(int, int, Player, bool)>();
            foreach (var pState in _gameState.Players)
            {
                bool isAlive = pState.LifeStatus == PlayerLifeStatus.Alive;
                int inf = isAlive ? pState.Symbols[(int)Symbols.Influence] : -1;
                int str = isAlive ? pState.Symbols[(int)Symbols.Strength] : -1;
                orderList.Add((inf, str, pState.Id, isAlive));
            }

            orderList.Sort((a, b) =>
            {
                int aliveCompare = b.isAlive.CompareTo(a.isAlive);
                if (aliveCompare != 0) return aliveCompare;
                int infCompare = b.inf.CompareTo(a.inf);
                if (infCompare != 0) return infCompare;
                return b.str.CompareTo(a.str);
            });

            return orderList.Select(x => x.player).ToList();
        }

        private List<Player> SetOrderWithSymbols()
        {
            List<(int inf, int str, int prevIndex, Player player)> orderList = new List<(int, int, int, Player)>();
            foreach (var pState in _gameState.Players)
            {
                if (pState.LifeStatus != PlayerLifeStatus.Alive) continue;
                int prevIndex = _turnState.GetPlayerIndex(pState.Id);
                int inf = pState.Symbols[(int)Symbols.Influence];
                int str = pState.Symbols[(int)Symbols.Strength];
                orderList.Add((inf, str, prevIndex, pState.Id));
            }

            orderList.Sort((a, b) =>
            {
                int infCompare = b.inf.CompareTo(a.inf);
                if (infCompare != 0) return infCompare;
                int strCompare = b.str.CompareTo(a.str);
                if (strCompare != 0) return strCompare;
                return a.prevIndex.CompareTo(b.prevIndex);
            });

            return orderList.Select(x => x.player).ToList();
        }

        #endregion
    }
}