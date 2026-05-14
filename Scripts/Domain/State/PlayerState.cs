using System.Collections.Generic;
using Domain.Entities;
using Domain.Enums;
using Domain.Policies;

namespace Domain.State
{
    /// <summary>
    /// 각 플레이어의 상태
    /// </summary>
    public sealed class PlayerState
    {
        public Player Id { get; } // 0, 1, 2

        public PlayerRole Role { get; private set; }
        public PlayerLifeStatus? LifeStatus { get; private set; }

        // Awake 할 때, 초기 Root 카드 세팅 시 이 값도 갱신되어야함.
        // 초기에 MaxPlayerSect를 인게임 매니저에서 세팅해서 값 1회 갱신 후 진행할 것
        public int MaxPlayerSect;
        public int PlayerSect = 0;

        public int Cultist { get; private set; }
        public int[] Symbols { get; private set; }

        // [추가] 영구 보너스 (필드 상황과 무관하게 Get 명령 등으로 얻은 보너스)
        public int PermanentCultist { get; private set; }
        public int[] PermanentSymbols { get; private set; }

        public List<CardInstance> Hand { get; }

        public DrawRule NextDrawRule { get; set; } = null;

        public int MaxJunction { get; set; } = 2;

        public int BonusTurnCycles { get; set; } = 0;

        public void SetNextDrawRule(DrawRule drawRule)
        {
            NextDrawRule = drawRule;
        }

        public DrawRule ConsumeDrawRule()
        {
            var rule = NextDrawRule ?? DrawRule.Standard;
            NextDrawRule = null;
            return rule;
        }

        public PlayerState(Player id, PlayerRole role, PlayerLifeStatus? lifeStatus)
        {
            Id = id;
            Symbols = new int[6];
            PermanentSymbols = new int[6];
            Cultist = 0;
            PermanentCultist = 0;
            Role = role;
            LifeStatus = lifeStatus;
            Hand = new List<CardInstance>();
        }

        public void SetSymbols(int[] symbols)
        {
            Symbols = (int[])symbols.Clone();
        }

        public void AddSymbol(int symbolIndex, int value)
        {
            Symbols[symbolIndex] += value;
        }

        public void AddPermanentSymbol(int symbolIndex, int value)
        {
            PermanentSymbols[symbolIndex] += value;
            Symbols[symbolIndex] += value;
        }

        public void SetCultist(int cultist)
        {
            Cultist = cultist;
        }

        public void AddCultist(int cultist)
        {
            Cultist += cultist;
        }

        public void AddPermanentCultist(int cultist)
        {
            PermanentCultist += cultist;
            Cultist += cultist;
        }

        public void SetPlayerRole(PlayerRole playerRole)
        {
            Role = playerRole;
        }

        public void SetPlayerLifeStatus(PlayerLifeStatus playerLifeStatus)
        {
            LifeStatus = playerLifeStatus;
        }

        public List<CardInstance> GetHandState()
        {
            return Hand;
        }
    }
}