using System.Collections.Generic;
using Data.Models;
using UnityEngine;

namespace Scenes.Lobby
{
    public class MatchSessionManager : MonoBehaviour
    {
        public static MatchSessionManager Instance { get; private set; }

        // 내가 사용할 덱의 풀 데이터
        public DeckData MyDeckData { get; private set; }

        // 이번 게임에 참여하는 유저들의 SteamID (순서 보장)
        public List<ulong> PlayerSteamIds { get; private set; } = new List<ulong>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // 씬 전환 시 파괴 방지
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetupSession(DeckData myDeck, List<ulong> players)
        {
            MyDeckData = myDeck;
            PlayerSteamIds = players;
        }
    }
}