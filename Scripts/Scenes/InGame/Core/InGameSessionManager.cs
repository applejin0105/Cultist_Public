using System.Collections.Generic;
using Data.Models;
using Mirror;
using Scenes.Lobby;
using UnityEngine;

namespace Scenes.InGame.Core
{
    public class InGameSessionManager : NetworkBehaviour
    {
        public static InGameSessionManager Instance { get; private set; }

        // 서버 메모리에 저장되는 플레이어별 덱 데이터 (Key: 네트워크 ID)
        private Dictionary<uint, DeckData> _playerDecks = new Dictionary<uint, DeckData>();

        public int ExpectedPlayers { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ExpectedPlayers = MatchSessionManager.Instance.PlayerSteamIds.Count;

            // (Controller가 스스로 초기화하므로 여기서 간섭하면 카운트가 0으로 날아갑니다)
            _playerDecks.Clear(); // 자기 자신의 이전 데이터만 안전하게 비움
        }

        [Server]
        public void RegisterPlayerDeck(uint playerNetId, int rootCardId, int[] cardIds)
        {
            if (_playerDecks.ContainsKey(playerNetId)) return;

            DeckData newDeck = new DeckData
            {
                deckName = $"Player_{playerNetId}_Deck",
                rootCardId = rootCardId,
                cardIds = new List<int>(cardIds)
            };

            _playerDecks[playerNetId] = newDeck;

            // 덱 상세 정보 로그 출력 - 테스트용
            string cardListStr = string.Join(", ", cardIds);
            Debug.Log(
                $"[Server] 플레이어 {playerNetId} 덱 데이터 수신 완료. Root: {rootCardId}, Cards: [{cardListStr}] ({_playerDecks.Count}/{ExpectedPlayers})");
        }

        // 서버에서 특정 플레이어의 덱 정보가 필요할 때 호출
        [Server]
        public DeckData GetPlayerDeck(uint playerNetId)
        {
            if (_playerDecks.TryGetValue(playerNetId, out DeckData deck))
            {
                return deck;
            }

            return null;
        }
    }
}