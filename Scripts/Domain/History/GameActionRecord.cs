using Domain.Enums;

namespace Domain.History
{
    public class GameActionRecord
    {
        public int RoundNumber { get; }
        public Player Actor { get; } // 가해자 (헉)
        public ActionType Type { get; } // 행동 종류
        public Player? TargetPlayer { get; } // 피해자 (없을 수 있음)
        public int CardId { get; } // 대상 카드 ID (InstanceId 아님, Base ID)
        public int Amount { get; }

        public GameActionRecord(int round, Player actor, ActionType type, Player? targetPlayer, int cardId, int amount)
        {
            RoundNumber = round;
            Actor = actor;
            Type = type;
            TargetPlayer = targetPlayer;
            CardId = cardId;
            Amount = amount;
        }
    }
}