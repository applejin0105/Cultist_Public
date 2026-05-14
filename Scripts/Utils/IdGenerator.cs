using Data.Models;
using Domain.Entities;
using Domain.Enums;
using Domain.State;

namespace Utils
{
    /// <summary>
    /// 고유 ID 생성기
    /// </summary>
    public static class IdGenerator
    {
        private static int _globalInstanceIdCounter = 5000;

        public static CardInstance ReturnInstanceId(Card newCard, Player player, Zone zone, CardStatus status,
            int instanceId)
        {
            var instance = new CardInstance(
                instanceId,
                newCard.Id,
                player,
                zone,
                status
            );

            return instance;
        }

        public static DeckState ReturnInstanceIdDeck(DeckData data, Player player)
        {
            var deckState = new DeckState();
            var catalog = CardCatalog.Instance;

            if (data.rootCardId != 0)
            {
                var def = catalog.Get(data.rootCardId);
                var instanceId = _globalInstanceIdCounter++;
                var instance = new CardInstance(
                    instanceId,
                    def.Id,
                    player,
                    Zone.Deck
                );
                deckState.SetRootCard(instance);
            }

            foreach (var cardId in data.cardIds)
            {
                var def = catalog.Get(cardId);
                var instanceId = _globalInstanceIdCounter++;
                var instance = new CardInstance(
                    instanceId,
                    def.Id,
                    player,
                    Zone.Deck
                );
                deckState.AddCardInstance(instance);
            }

            return deckState;
        }
    }
}