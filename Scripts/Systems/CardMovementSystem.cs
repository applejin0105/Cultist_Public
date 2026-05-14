using Domain.Entities;
using Domain.Enums;
using Domain.State;
using Domain.State.Host;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// 카드 이동 처리
    /// </summary>
    public static class CardMovementSystem
    {
        public static void MoveCard(GameState state, int instanceId, Player toPlayer, Zone toZone, CardStatus toStatus)
        {
            var card = state.GetCard(instanceId);

            if (card == null)
            {
                Debug.LogError($"[MoveSystem] 카드를 찾을 수 없음: ID: {instanceId}");
                return;
            }

            Debug.Log($"[MoveSystem] {card.BaseData.Name}({instanceId}) 상태 변경 시도: {card.CardStatus} -> {toStatus} ({toZone})");

            RemoveFromSourceCollection(state, card);

            card.ChangeOwner(toPlayer);
            card.ChangeZone(toZone);
            card.ChangeCardStatus(toStatus);

            AddToDestinationCollection(state, card, toZone, toPlayer);

            Debug.Log($"[MoveSystem] {card.BaseData.Name}({instanceId}) 이동 완료: {toZone} ({toPlayer})");
        }

        private static void RemoveFromSourceCollection(GameState state, CardInstance card)
        {
            if (card.Zone == Zone.Hand)
            {
                var pState = state.GetPlayerStateById(card.OwnerSeat);
                if (pState != null)
                {
                    pState.Hand.Remove(card);
                }
            }
            else if (card.Zone == Zone.Trade)
            {
                state.GetTradeDeck().RemoveCard(card);
            }
            else if (card.Zone == Zone.Deck)
            {
                // Draw 정상 흐름에서는 deckState.Draw가 이미 Pop했지만
                // 조건부 검색(SetNextDraw "where" 등)에서는 Pop 없이 select하므로 명시 제거 필요.
                // 이미 빠진 카드면 RemoveCard 내부에서 Contains 체크로 NOOP.
                var deckState = state.GetDeckStateById(card.OwnerSeat);
                if (deckState != null)
                {
                    deckState.RemoveCardInstance(card);
                    // 정책: 덱에서 카드가 빠질 때마다 자동 셔플
                    // (탐색·조건부 검색으로 카드 순서가 노출된 후의 보정 + 결정론은 ServerRng 사용)
                    deckState.Shuffle(state.ReturnServerRng());
                }
            }
        }

        private static void AddToDestinationCollection(GameState state, CardInstance card, Zone toZone, Player toPlayer)
        {
            switch (toZone)
            {
                case Zone.Hand:
                    var pState = state.GetPlayerStateById(toPlayer);
                    if (pState != null)
                    {
                        pState.Hand.Add(card);
                    }

                    break;
                case Zone.Trade:
                    state.GetTradeDeck().AddCards(card);
                    break;
                case Zone.Exile:
                    state.GetExileDeck().AddCards(card);
                    break;
                case Zone.Deck:
                    DeckState playerDeck = state.GetDeckStateById(toPlayer);
                    playerDeck.AddCardInstance(card);
                    playerDeck.Shuffle(state.ReturnServerRng());
                    break;
            }
        }

        public static void MoveToHand(GameState state, int instanceId, Player player)
        {
            MoveCard(state, instanceId, player, Zone.Hand, CardStatus.Hand);
        }

        public static void MoveToField(GameState state, int instanceId, Player player)
        {
            MoveCard(state, instanceId, player, Zone.Field, CardStatus.FieldBack);
        }

        public static void MoveToTrade(GameState state, int instanceId, Player player = Player.Game)
        {
            MoveCard(state, instanceId, player, Zone.Trade, CardStatus.Trade);
        }

        public static void MoveToDeck(GameState state, int instanceId, Player player)
        {
            MoveCard(state, instanceId, player, Zone.Deck, CardStatus.Deck);
        }
    }
}