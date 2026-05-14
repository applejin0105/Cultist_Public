using System.Collections.Generic;
using Domain.Entities;
using Domain.Enums;
using Domain.State.Host;

namespace Utils
{
    /// <summary>
    /// 필터링/검색 헬퍼
    /// </summary>
    public static class CardQueries
    {
        public static IEnumerable<CardInstance> GetCardsInZone(GameState state, Zone zone)
        {
            foreach (var card in state.Cards.Values)
            {
                if (card.Zone == zone) yield return card;
            }
        }

        public static IEnumerable<CardInstance> GetDeckCards(GameState state, Player player)
        {
            foreach (var card in state.Cards.Values)
            {
                if (card.Zone == Zone.Deck && card.OwnerSeat == player)
                    yield return card;
            }
        }

        public static IEnumerable<CardInstance> GetHandCards(GameState state, Player player)
        {
            foreach (var card in state.Cards.Values)
            {
                if (card.Zone == Zone.Hand && card.OwnerSeat == player)
                {
                    yield return card;
                }
            }
        }

        public static IEnumerable<CardInstance> GetFieldCards(GameState state, Player player)
        {
            foreach (var card in state.Cards.Values)
            {
                if (card.Zone == Zone.Field && card.OwnerSeat == player)
                    yield return card;
            }
        }

        public static IEnumerable<CardInstance> GetTradeCards(GameState state)
        {
            foreach (var card in state.Cards.Values)
            {
                if (card.Zone == Zone.Trade)
                    yield return card;
            }
        }
    }
}