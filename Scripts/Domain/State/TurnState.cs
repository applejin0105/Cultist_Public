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

        public int GetPlayerIndex(Player player)
        {
            return _turnOrder.IndexOf(player);
        }
    }
}