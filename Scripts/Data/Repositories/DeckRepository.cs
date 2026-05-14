using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Newtonsoft.Json;
using Utils;
using UnityEngine;


namespace Data.Repositories
{
    /// <summary>
    /// 덱 불러오기, 수정, 저장
    /// </summary>
    public sealed class DeckRepository
    {
        private readonly CardCatalog _cardCatalog;
        private DeckData _currentDeckData;
        public DeckData CurrentDeckData => _currentDeckData;

        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        private string _originalEditingDeckName;

        public DeckRepository(CardCatalog cardCatalog)
        {
            _cardCatalog = cardCatalog;
            _currentDeckData = new DeckData();
        }

        public async Task<Dictionary<string, DeckData>> LoadAllDecksAsync()
        {
            var deckMap = new Dictionary<string, DeckData>();

            string samplePath = PathConstants.SampleDeckDBTargetFilePath;

            if (File.Exists(samplePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(samplePath);

                    var sampleData = JsonConvert.DeserializeObject<SampleDeck>(json);

                    if (sampleData != null && sampleData.sampleDecks != null)
                    {
                        foreach (var deck in sampleData.sampleDecks)
                        {
                            deck.IsSample = true;
                            deck.cardIds.RemoveAll(id => id == deck.rootCardId);
                            deckMap.TryAdd(deck.deckName, deck);
                        }
                    }

                    Debug.Log($"[DeckRepository] 샘플 덱 로드 완료");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[DeckRepo] 샘플 덱 로드 실패: {ex.Message}");
                }
            }

            PlayerDeck playerDeckData = await LoadPlayerDeckAsync();

            if (playerDeckData != null && playerDeckData.playerDecks != null)
            {
                foreach (var deck in playerDeckData.playerDecks)
                {
                    if (deckMap.ContainsKey(deck.deckName))
                    {
                        deckMap[deck.deckName] = deck;
                    }
                    else
                    {
                        deckMap.Add(deck.deckName, deck);
                    }
                }

                Debug.Log($"[DeckRepository] PlayerDeck 로드 완료");
            }

            return deckMap;
        }

        public async Task<PlayerDeck> LoadPlayerDeckAsync()
        {
            string path = PathConstants.PlayerDeckTargetFilePath;
            if (!File.Exists(path)) return new PlayerDeck();

            await _fileLock.WaitAsync(); // 파일 접근 대기
            try
            {
                string json = await File.ReadAllTextAsync(path);
                var playerDeck = JsonConvert.DeserializeObject<PlayerDeck>(json) ?? new PlayerDeck();

                // SanitizeDecks 검증 호출 로직 (생략)
                return playerDeck;
            }
            finally
            {
                _fileLock.Release(); // 락 해제
            }
        }

        private bool SanitizeDecks(PlayerDeck playerDeck)
        {
            bool changed = false;
            var decksToRemove = new List<DeckData>();

            if (playerDeck.playerDecks == null) playerDeck.playerDecks = new List<DeckData>();

            foreach (var deck in playerDeck.playerDecks)
            {
                var rootCard = _cardCatalog.Get(deck.rootCardId);
                if (rootCard is not { IsRoot: true })
                {
                    decksToRemove.Add(deck);
                    changed = true;
                    Debug.LogWarning($"[DeckRepo] 유효하지 않은 RootCard(ID: {deck.rootCardId}). '{deck.deckName}' 덱 삭제.");
                    continue;
                }

                if (deck.cardIds == null) deck.cardIds = new List<int>();

                int removedCount = deck.cardIds.RemoveAll(id =>
                {
                    var card = _cardCatalog.Get(id);
                    return card == null || card.IsRoot;
                });

                if (removedCount > 0)
                {
                    changed = true;
                    Debug.LogWarning($"[DeckRepo] 덱 '{deck.deckName}'에서 유효하지 않은 카드 '{removedCount}'장 삭제.");
                }
            }

            foreach (var deck in decksToRemove) playerDeck.playerDecks.Remove(deck);

            return changed;
        }

        public bool CreateNewDeck(string deckName, int rootCardId)
        {
            var rootCard = _cardCatalog.Get(rootCardId);
            if (rootCard == null || !rootCard.IsRoot) return false;

            _originalEditingDeckName = null; // 새 덱 생성이므로 원본 이름 없음
            _currentDeckData = new DeckData { deckName = deckName, rootCardId = rootCardId, cardIds = new List<int>() };
            return true;
        }

        public bool AddCardToCurrentDeck(int cardId)
        {
            if (_currentDeckData == null) return false;
            if (_currentDeckData.cardIds.Count == 30) return false;

            var card = _cardCatalog.Get(cardId);
            if (card == null) return false;

            if (!card.IsCollectible) return false;

            if (card.IsRoot)
            {
                _currentDeckData.rootCardId = cardId;
            }
            else
            {
                if (IsContainOver3(cardId)) return false;
                _currentDeckData.cardIds.Add(cardId);
                _currentDeckData.cardIds.Sort((a, b) =>
                {
                    var cardA = _cardCatalog.Get(a);
                    var cardB = _cardCatalog.Get(b);
                    return cardA.Cultist.CompareTo(cardB.Cultist);
                });
            }

            return true;
        }

        private bool IsContainOver3(int key)
        {
            var count = 0;
            foreach (var card in _currentDeckData.cardIds)
            {
                if (key == card)
                    count++;
            }

            return count > 2;
        }

        public bool RemoveCardFromCurrentDeck(int cardId)
        {
            if (_currentDeckData?.cardIds == null) return false;

            if (!_currentDeckData.cardIds.Contains(cardId)) return false;

            _currentDeckData.cardIds.Remove(cardId);

            return true;
        }

        public async Task<bool> SaveCurrentDeckAsync()
        {
            if (_currentDeckData == null || _currentDeckData.cardIds.Count != 30) return false;

            PlayerDeck allData = await LoadPlayerDeckAsync();

            // 기존 덱 수정인 경우, 원본 데이터를 리스트에서 제거
            if (!string.IsNullOrEmpty(_originalEditingDeckName))
            {
                var oldDeck = allData.playerDecks.FirstOrDefault(d => d.deckName == _originalEditingDeckName);
                if (oldDeck != null)
                {
                    allData.playerDecks.Remove(oldDeck);
                }
            }

            string safeName = GenerateUniqueDeckName(allData.playerDecks, _currentDeckData.deckName);
            _currentDeckData.deckName = safeName;

            allData.playerDecks.Add(_currentDeckData);
            await SavePlayerDeckToFileAsync(allData);

            // 연속 저장을 대비해 원본 이름을 현재 이름으로 갱신
            _originalEditingDeckName = safeName;
            return true;
        }

        public async Task<bool> DeleteDeckAsync(string deckName)
        {
            if (_currentDeckData.IsSample) return false;

            PlayerDeck allData = await LoadPlayerDeckAsync();

            var target = allData.playerDecks.FirstOrDefault(d => d.deckName == deckName);

            if (target != null)
            {
                allData.playerDecks.Remove(target);
                await SavePlayerDeckToFileAsync(allData);
                return true;
            }

            return false;
        }

        public async Task<bool> LoadDeckForEditingAsync(string deckName)
        {
            if (deckName.StartsWith("Sample")) return false;

            PlayerDeck allData = await LoadPlayerDeckAsync();
            var target = allData.playerDecks.FirstOrDefault(d => d.deckName == deckName);
            if (target != null)
            {
                _originalEditingDeckName = deckName; // 편집할 원본 이름 저장
                string tempJson = JsonConvert.SerializeObject(target);
                _currentDeckData = JsonConvert.DeserializeObject<DeckData>(tempJson);
                return true;
            }

            return false;
        }

        private async Task SavePlayerDeckToFileAsync(PlayerDeck playerDeck)
        {
            await _fileLock.WaitAsync(); // 파일 접근 대기
            try
            {
                var json = JsonConvert.SerializeObject(playerDeck, Formatting.Indented);
                await File.WriteAllTextAsync(PathConstants.PlayerDeckTargetFilePath, json);
            }
            finally
            {
                _fileLock.Release(); // 락 해제
            }
        }

        // Regex 부분 블로그에 추가 정리
        private string GenerateUniqueDeckName(List<DeckData> existingDecks, string deckName)
        {
            if (existingDecks.All(d => d.deckName != deckName)) return deckName;

            int maxIndex = 0;
            var pattern = $@"^{Regex.Escape(deckName)}_(\d+)$";

            foreach (var deck in existingDecks)
            {
                if (deck.deckName == deckName) continue;
                var match = Regex.Match(deck.deckName, pattern);
                if (match.Success)
                {
                    maxIndex = Math.Max(maxIndex, int.Parse(match.Groups[1].Value));
                }
            }

            return $"{deckName}_{maxIndex + 1}";
        }

        public void ClearCurrentDeck()
        {
            _originalEditingDeckName = null;
            _currentDeckData = null;
        }
    }
}