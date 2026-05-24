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

        // [재진입 안전화] 진행 중인 초기화 Task를 캐싱한다.
        // CardCatalog.InitializeAsync()는 부트스트랩 / Collection 씬의 여러 매니저에서
        // 거의 동시에 호출될 수 있다. 이전 구현은 'if (Instance != null) return;' 체크와
        // Instance 대입 사이에 await가 있어, 동시 호출자가 모두 체크를 통과하고
        // DataInitializer.InitJsonFiles()를 중복 실행 → 같은 JSON 파일에 동시 쓰기
        // 충돌(sampleDecks.json 손상 등)을 일으켰다. 진행 중 Task를 공유하여 정확히 1회만 실행.
        private static Task _initTask;

        private readonly Dictionary<int, Card> _cards;

        public CardCatalog(Dictionary<int, Card> cards)
        {
            _cards = cards;
        }

        public IEnumerable<Card> GetAllCards()
        {
            return _cards.Values;
        }

        /// <summary>
        /// CardCatalog 초기화를 보장한다. 멱등(idempotent)이며 재진입에 안전하다.
        ///
        /// [초기화 책임 구조]
        ///  - 소유자(Owner): GameBootstrapper 가 앱 시작 시(부트 씬) 1회 호출하여 실제 초기화를 수행한다.
        ///    초기화를 "트리거"하는 곳은 원칙적으로 GameBootstrapper 한 곳이다.
        ///  - 씬 매니저(Consumer): CardCollectionManager / CollectionSceneUIManager / LobbyUIManager 등은
        ///    이 메서드를 "초기화 완료 대기" 목적으로 호출한다. 부트스트랩이 이미 끝났으면 즉시 반환하고,
        ///    진행 중이면 동일한 진행 Task 를 공유해 await 하므로 DataInitializer.InitJsonFiles() 가
        ///    절대 중복 실행되지 않는다(=JSON 파일 동시 쓰기 충돌 원천 차단).
        ///  - 부트스트랩을 거치지 않은 직접 진입(에디터에서 씬을 단독 실행하는 경우 등)에도 안전하다.
        ///    이 경우 최초 호출 씬 매니저가 초기화를 트리거하고 나머지는 같은 Task 를 대기한다.
        /// </summary>
        public static Task InitializeAsync()
        {
            if (Instance != null) return Task.CompletedTask;

            // 동시 호출자는 모두 동일한 진행 중 Task를 await한다 → InitJsonFiles 중복 실행 방지.
            return _initTask ??= InitializeInternalAsync();
        }

        private static async Task InitializeInternalAsync()
        {
            try
            {
                await DataInitializer.InitJsonFiles();
                var path = PathConstants.CardDbTargetFilePath;

                if (!File.Exists(path)) throw new Exception($"CardDbTargetFilePath {path} does not exist");

                var json = await File.ReadAllTextAsync(path);
                var cards = ParseCards(json);

                Instance = new CardCatalog(cards);

                UnityEngine.Debug.Log("CardCatalog initialized");
            }
            catch
            {
                // 실패 시 캐시를 비워 다음 호출이 재시도할 수 있도록 한다.
                _initTask = null;
                throw;
            }
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