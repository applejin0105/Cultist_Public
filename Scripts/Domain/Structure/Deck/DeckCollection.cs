using System.Collections.Generic;
using Domain.Entities;
using UnityEngine;

namespace Domain.Structure.Deck
{
    public class DeckCollection : IEnumerable<CardInstance>
    {
        private List<CardInstance> _cards;
        public int Count => _cards.Count;

        public DeckCollection()
        {
            _cards = new List<CardInstance>();
        }

        public DeckCollection(List<CardInstance> cards)
        {
            _cards = cards;
        }

        public DeckCollection(DeckCollection playerDeck)
        {
            // 원본 리스트의 요소들을 가지고 새로운 리스트 생성 (Deep Copy)
            _cards = playerDeck == null ? new List<CardInstance>() : new List<CardInstance>(playerDeck._cards);
        }

        public void AddCards(CardInstance newCard) => _cards.Add(newCard);

        public bool Draw(out CardInstance poppedCard)
        {
            if (_cards.Count == 0)
            {
                Debug.Log("[DeckCollection] 덱이 비어있음.");
                poppedCard = null;
                return false;
            }

            poppedCard = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return true;
        }

        public bool Peek(out CardInstance peekedCard)
        {
            if (_cards.Count == 0)
            {
                Debug.Log("[DeckCollection] 덱이 비어있음.");
                peekedCard = null;
                return false;
            }

            peekedCard = _cards[^1];
            return true;
        }

        public bool PushToBottom(CardInstance cardInstance)
        {
            _cards.Insert(0, cardInstance);
            return true;
        }

        public bool Insert(int cardIndex, CardInstance cardInstance)
        {
            _cards.Insert(cardIndex, cardInstance);
            return true;
        }

        public void Shuffle(System.Random rng)
        {
            var n = _cards.Count;
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                (_cards[k], _cards[n]) = (_cards[k], _cards[n]);
            }
        }

        public IEnumerator<CardInstance> GetEnumerator()
        {
            return _cards.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void RemoveCard(CardInstance card)
        {
            if (_cards.Contains(card))
            {
                _cards.Remove(card);
            }
        }
    }
}