using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Data.Models;
using Domain.State;
using Data.Repositories;
using Domain.Enums;
using Utils;

namespace Systems
{
    /// <summary>
    /// Deck과 관련된 로직을 수행.
    /// </summary>
    public class DeckSystem
    {
        private readonly DeckRepository _deckRepository;

        // Deck 이름, DeckData
        private Dictionary<string, DeckData> _deckDataCache;
        private bool _isInitialized = false;

        public DeckSystem(DeckRepository deckRepository)
        {
            _deckRepository = deckRepository;
        }

        public async Task Initialize()
        {
            if (_isInitialized) return;

            _deckDataCache = await _deckRepository.LoadAllDecksAsync();

            _isInitialized = true;
            Debug.Log($"[DeckSystem] 초기화. {_deckDataCache.Count}개의 덱 로딩.");
        }

        public DeckState CreateDeckState(Player player, string deckName)
        {
            if (!_isInitialized)
            {
                Debug.LogError($"[DeckSystem] 덱 생성 불가.");
                return null;
            }

            if (!_deckDataCache.TryGetValue(deckName, out DeckData deckState))
            {
                Debug.LogError($"[DeckSystem] 덱 '{deckName}'을 찾을 수 없음.");
                return null;
            }

            return IdGenerator.ReturnInstanceIdDeck(deckState, player);
        }

        public async Task Reload()
        {
            _isInitialized = false;
            await Initialize();
        }
    }
}