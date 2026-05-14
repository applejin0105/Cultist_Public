using Data.Models;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// 게임 중에 생성된 카드
    /// </summary>
    public class CardInstance
    {
        public int InstanceId { get; }
        public int CardId { get; private set; }
        public Player OwnerSeat { get; private set; }

        public Zone Zone { get; private set; }
        public CardStatus CardStatus { get; private set; }

        public Card BaseData => CardCatalog.Instance.Get(CardId);

        public int EffectiveJunction { get; private set; }

        public CardInstance()
        {
            // Error Card
            InstanceId = -1;
            CardId = 0;
            OwnerSeat = Player.Game;
            Zone = Zone.Deck;
            CardStatus = CardStatus.Deck;
        }

        public CardInstance(int instanceId, int cardId, Player ownerSeat, Zone zone,
            CardStatus cardStatus = CardStatus.Deck)
        {
            InstanceId = instanceId;
            CardId = cardId;
            OwnerSeat = ownerSeat;
            Zone = zone;
            CardStatus = cardStatus;
        }

        public void ChangeOwner(Player player)
        {
            OwnerSeat = player;
        }

        public void ChangeZone(Zone newZone)
        {
            Zone = newZone;
        }

        public void ChangeCardStatus(CardStatus newCardStatus)
        {
            CardStatus = newCardStatus;
        }

        public void SetEffectiveJunction(int value)
        {
            EffectiveJunction = value;
        }

        public void DebugChangeCardId(int newCardId)
        {
            CardId = newCardId;
        }
    }
}