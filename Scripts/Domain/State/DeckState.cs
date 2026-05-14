using System.Collections.Generic;
using Domain.Entities;
using Domain.Structure.Deck;

namespace Domain.State
{
    /// <summary>
    /// 플레이어 개별 덱 상태
    /// </summary>
    public sealed class DeckState
    {
        private CardInstance _rootCardInstance;
        private DeckCollection PlayerDeck { get; set; }
        public int DeckCount() => PlayerDeck.Count;
        public CardInstance GetRootCardInstance() => _rootCardInstance;
        public IEnumerable<CardInstance> GetAllCards()
        {
            if (_rootCardInstance != null) yield return _rootCardInstance;
            foreach (var card in PlayerDeck) yield return card;
        }

        public DeckState()
        {
            PlayerDeck = new DeckCollection();
        }

        public DeckState(CardInstance root, List<CardInstance> deck)
        {
            _rootCardInstance = root;
            PlayerDeck = new DeckCollection(deck);
        }

        public DeckState(DeckState other)
        {
            _rootCardInstance = other._rootCardInstance;
            PlayerDeck = new DeckCollection(other.PlayerDeck);
        }

        public bool Draw(out CardInstance cardInstance)
        {
            return PlayerDeck.Draw(out cardInstance);
        }

        public CardInstance Peek()
        {
            PlayerDeck.Peek(out var cardInstance);
            return cardInstance;
        }

        public void SetRootCard(CardInstance cardInstance)
        {
            _rootCardInstance = cardInstance;
        }

        public void AddCardInstance(CardInstance cardInstance)
        {
            PlayerDeck.AddCards(cardInstance);
        }

        public void RemoveCardInstance(CardInstance cardInstance)
        {
            PlayerDeck.RemoveCard(cardInstance);
        }

        public void Shuffle(System.Random rng)
        {
            PlayerDeck.Shuffle(rng);
        }
    }
}