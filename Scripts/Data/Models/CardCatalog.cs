using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Data.Initialization;
using Domain.Entities;
using Newtonsoft.Json.Linq;
using Utils;

namespace Data.Models
{
    public sealed class CardCatalog
    {
        /// <summary>
        /// 카드 DB를 바탕으로, 카드의 모든 정보를 관리하는 싱글톤
        /// </summary>>
        public static CardCatalog Instance { get; private set; }

        private readonly Dictionary<int, Card> _cards;

        public CardCatalog(Dictionary<int, Card> cards)
        {
            _cards = cards;
        }

        public IEnumerable<Card> GetAllCards()
        {
            return _cards.Values;
        }

        public static async Task InitializeAsync()
        {
            if (Instance != null) return;

            await DataInitializer.InitJsonFiles();
            var path = PathConstants.CardDbTargetFilePath;

            if (!File.Exists(path)) throw new Exception($"CardDbTargetFilePath {path} does not exist");

            var json = await File.ReadAllTextAsync(path);
            var cards = ParseCards(json);

            Instance = new CardCatalog(cards);

            UnityEngine.Debug.Log("CardCatalog initialized");
        }

        public Card Get(int cardId)
        {
            if (!_cards.TryGetValue(cardId, out Card card))
                throw new KeyNotFoundException($"CardId {cardId} does not exist");
            return card;
        }

        public bool Contains(int cardId)
            => _cards.ContainsKey(cardId);

        public int Count => _cards.Count;

        private static Dictionary<int, Card> ParseCards(string json)
        {
            var rootObject = JObject.Parse(json);

            var list = rootObject["cards"]?.ToObject<List<Card>>();

            if (list == null)
                UnityEngine.Debug.LogError($"[CardCatalog] 카드 {json}이 카드 리스트에 포함되지 않음.");


            var dict = new Dictionary<int, Card>(list.Count);
            foreach (var card in list)
            {
                if (card == null)
                    continue;
                dict.TryAdd(card.Id, card);
            }

            return dict;
        }
    }
}