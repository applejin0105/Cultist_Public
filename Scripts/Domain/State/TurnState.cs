using System.Collections.Generic;
using Domain.Enums;

namespace Domain.State
{
    /// <summary>
    /// 게임의 턴 상태
    /// </summary>
    public sealed class TurnState
    {
        public int RoundNumber { get; set; } = 1;
        public int CurrentPlayerIndex { get; set; } = 0;

        public PhaseState Phase { get; set; } = PhaseState.StandBy;

        /// <summary>
        /// 한 턴에 수행 가능한 사이클(Draw -> Play) 남은 횟수. 
        /// 표준적으로는 1회 수행하며, Stonehenge 등 효과로 증가할 수 있음.
        /// </summary>
        public int RemainingCycles { get; set; } = 1;

        /* Player1 = 1
         * Player2 = 2
         * Player3 = 3
         */
        private List<Player> _turnOrder = new List<Player>();
        public IList<Player> TurnOrder => _turnOrder;

        // 게임 첫 시작 시 결정된 턴 순서. 라운드가 바뀌어도 변하지 않는다.
        // 4기사 강림 등의 최종 타이브레이커에서 사용.
        private List<Player> _initialTurnOrder = new List<Player>();
        public IList<Player> InitialTurnOrder => _initialTurnOrder;

        public Player? ActivePlayer
        {
            get
            {
                if (_turnOrder == null || _turnOrder.Count == 0) return null;
                if (CurrentPlayerIndex >= _turnOrder.Count) return _turnOrder[0];
                return _turnOrder[CurrentPlayerIndex];
            }
        }

        public void SetPlayers(List<Player> players)
        {
            _turnOrder = players ?? new List<Player>();
            CurrentPlayerIndex = 0;
        }

        /// <summary>
        /// 게임 첫 라운드 시작 시 호출. 현재 턴 순서와 초기 턴 순서를 동시에 설정한다.
        /// 초기 턴 순서는 이후 라운드가 갱신돼도 보존된다.
        /// </summary>
        public void SetInitialPlayers(List<Player> players)
        {
            var copy = players ?? new List<Player>();
            _turnOrder = new List<Player>(copy);
            _initialTurnOrder = new List<Player>(copy);
            CurrentPlayerIndex = 0;
        }

        public int GetPlayerIndex(Player player)
        {
            return _turnOrder.IndexOf(player);
        }

        /// <summary>
        /// 게임 첫 시작 시 턴 순서에서의 인덱스. 인덱스가 클수록 후턴.
        /// </summary>
        public int GetInitialPlayerIndex(Player player)
        {
            return _initialTurnOrder.IndexOf(player);
        }
    }
}