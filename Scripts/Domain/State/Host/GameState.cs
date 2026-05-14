#nullable enable
using System.Collections.Generic;
using System.Linq;
using Data.Models;
using Domain.Entities;
using Domain.Enums;
using Domain.History;
using Domain.Structure.Deck;
using Domain.Structure.Field;
using Effects.Core;
using Systems;
using UnityEngine;
using Utils;
using Random = System.Random;

namespace Domain.State.Host
{
    /// <summary>
    /// 게임의 전체 상태
    /// </summary>
    public sealed class GameState : IEffectGameState
    {
        private int _runtimeInstanceIdCounter = 2000;
        private int _starveCardStartInstanceId = 400;

        // IEffectGameState 구현
        public IEnumerable<CardInstance> GetAllCards() => _cards.Values;

        public IEnumerable<Player> GetAlivePlayers() =>
            _players.Where(p => p.LifeStatus == PlayerLifeStatus.Alive).Select(p => p.Id);

        public int GetPlayerStat(Player player, string statKey)
        {
            var pState = GetPlayerStateById(player);
            if (pState == null || string.IsNullOrEmpty(statKey)) return 0;
            return statKey.Trim().ToLowerInvariant() switch
            {
                "influence" => pState.Symbols[0],
                "unity" => pState.Symbols[1],
                "monotheism" => pState.Symbols[2],
                "polytheism" => pState.Symbols[3],
                "strength" => pState.Symbols[4],
                "pantheon" => pState.Symbols[5],
                "cultist" => pState.Cultist,
                _ => 0
            };
        }

        public int GetCurrentRound() => TurnState.RoundNumber;

        public int GetHistoryCount(Player actor, ActionType type, string scope)
        {
            // 현재는 라운드 범위만 지원 (추후 scope에 따라 확장 가능)
            if (!HistoryByRound.TryGetValue(TurnState.RoundNumber, out var history)) return 0;
            return history.Count(r => r.Actor == actor && r.Type == type);
        }

        public HashSet<int> GetSectInstanceIds(CardInstance source)
        {
            var hashSet = new HashSet<int>();
            if (source == null) return hashSet;
            var tree = GetFieldTreeById(source.OwnerSeat);
            if (tree == null) return hashSet;

            var ancestors = tree.GetAncestors(source.InstanceId, true);
            var descendants = tree.GetDescendants(source.InstanceId, true);

            foreach (var node in ancestors) hashSet.Add(node.InstanceId);
            foreach (var node in descendants) hashSet.Add(node.InstanceId);
            return hashSet;
        }

        // 플레이어 상태를 담은 리스트
        private readonly List<PlayerState> _players = new();
        public IReadOnlyList<PlayerState> Players => _players;

        // Instance Id로 조회 가능한 cards
        private readonly Dictionary<int, CardInstance> _cards = new();
        public IReadOnlyDictionary<int, CardInstance> Cards => _cards;

        // 일반 덱들
        private DeckCollection FourHorseManDeck { get; set; } = new DeckCollection();
        public int FourHorseManDeckCount => FourHorseManDeck.Count;
        private DeckCollection TradeDeck { get; } = new DeckCollection();
        public int TradeDeckCount => TradeDeck.Count;

        private DeckCollection ExiledDeck { get; } = new DeckCollection();
        public int ExiledDeckCount => ExiledDeck.Count;

        // Id, Deck
        private Dictionary<int, DeckState> DeckStatesById { get; } = new();

        // Id, FieldTree
        private Dictionary<int, FieldState> FieldStatesById { get; } = new();

        // States
        public TurnState TurnState { get; } = new TurnState();

        // 행동 기록 저장소
        public Dictionary<int, List<GameActionRecord>> HistoryByRound { get; } =
            new Dictionary<int, List<GameActionRecord>>();

        // Server Rng
        private readonly System.Random _serverRng;

        public readonly StatSystem StatSystem;

        // 승패 판단
        public Player WinnerId { get; private set; } = Player.Game;
        public bool IsGameEnded { get; private set; } = false;

        public GameState(System.Random serverRng)
        {
            _serverRng = serverRng;
            StatSystem = new StatSystem(this);
            InitLogic(serverRng);
        }

        public void InitLogic(System.Random serverRng)
        {
            // 1. 생존해 있는(참여 중인) 플레이어 목록 추출
            List<Player> activePlayers = new List<Player>();
            foreach (var pState in _players)
            {
                if (pState.LifeStatus == PlayerLifeStatus.Alive)
                {
                    activePlayers.Add(pState.Id);
                }
            }

            // 2. 턴 순서 셔플 (Root 카드 속성에 따른 정렬 로직이 있다면 여기서 orderBy 수행)
            int n = activePlayers.Count;
            while (n > 1)
            {
                n--;
                int k = serverRng.Next(n + 1);
                (activePlayers[k], activePlayers[n]) = (activePlayers[n], activePlayers[k]);
            }

            // 3. 턴 시스템에 순서 등록
            TurnState.SetPlayers(activePlayers);
            TurnState.RoundNumber = 1;

            InitFourHorseManDeck();
            ShuffleFourHorseManDeck(serverRng);

            Debug.Log($"[GameState] 턴 순서 결정 완료: {string.Join(" -> ", activePlayers)}");
        }

        public void RecordAction(Player actor, ActionType type, Player? targetPlayer = null, int cardId = -1,
            int amount = 1)
        {
            int currentRound = TurnState.RoundNumber;
            if (!HistoryByRound.ContainsKey(currentRound))
            {
                HistoryByRound[currentRound] = new List<GameActionRecord>();
            }

            HistoryByRound[currentRound]
                .Add(new GameActionRecord(currentRound, actor, type, targetPlayer, cardId, amount));
        }

        /* Private */
        private void InitPlayerField(Player player, FieldState fieldState)
        {
            FieldStatesById[(int)player] = new FieldState(fieldState);
        }

        private void SetPlayerDeck(Player player, DeckState playerDeck)
        {
            DeckStatesById[(int)player] = new DeckState(playerDeck);
            RegisterDeckCards(playerDeck);
            SetPlayerRootCard(player, playerDeck);
        }

        private void SetPlayerRootCard(Player player, DeckState playerDeck)
        {
            CardInstance? rootCardInstance = playerDeck.GetRootCardInstance();
            if (rootCardInstance == null)
            {
                Debug.LogError($"[GameState] {player}의 RootCard가 존재하지 않음.");
                return;
            }

            int rootCardInstanceId = rootCardInstance.InstanceId;

            // [변경] 초기 상태를 FieldBack으로 설정하여 '0번째 턴' 연출 준비
            CardMovementSystem.MoveCard(this, rootCardInstanceId, player, Zone.Field, CardStatus.FieldBack);

            // [추가] 필드 트리의 시작점(Root)으로 등록
            FieldTree fieldTree = GetFieldTreeById(player);
            if (!fieldTree.Nodes.ContainsKey(rootCardInstanceId))
            {
                fieldTree.AddNode(new FieldNode(rootCardInstanceId));
                Debug.Log($"[GameState] {player}의 Root 노드 등록 완료 (ID: {rootCardInstanceId})");
            }
        }

        // Instance Id는 1, 2, 3, 4
        private void InitFourHorseManDeck()
        {
            // 사기사 카드 ID: 11, 12, 13, 14
            for (int i = 0; i < 4; i++)
            {
                int index = i + 11;
                int horsemanInstanceId = 3000 + i;

                Card newCard = CardCatalog.Instance.Get(index);
                CardInstance newCardInstance =
                    new CardInstance(horsemanInstanceId, index, Player.Game, Zone.Deck, CardStatus.FourHorseManDeck);
                FourHorseManDeck.AddCards(newCardInstance);

                _cards[newCardInstance.InstanceId] = newCardInstance;
            }
        }

        public void CreateAndAddCardToTrade(int baseCardId)
        {
            // 1. 새 ID 발급
            int newInstanceId = _runtimeInstanceIdCounter++;

            // 2. 새 인스턴스 생성 (소유자: Game, 위치: Trade)
            var newCard = new CardInstance(
                newInstanceId,
                baseCardId,
                Player.Game,
                Zone.Trade,
                CardStatus.Trade
            );

            // 3. 전체 카드 목록에 등록 (Lookup용)
            if (_cards.TryAdd(newInstanceId, newCard))
            {
                // 4. 교역 덱에 추가 (섞어 넣거나 맨 아래 넣음)
                TradeDeck.PushToBottom(newCard);
                // 필요하다면 TradeDeck.Shuffle(_serverRng) 호출 가능
            }
            else
            {
                Debug.LogError($"[GameState] 카드 생성 ID 충돌: {newInstanceId}");
            }
        }

        private void ShuffleAllPlayerDecks(System.Random sharedRng)
        {
            foreach (DeckState deckState in DeckStatesById.Values)
            {
                deckState.Shuffle(sharedRng);
            }
        }

        private void ShuffleFourHorseManDeck(Random serverRng)
        {
            FourHorseManDeck.Shuffle(serverRng);
        }

        public void RegisterCard(CardInstance card)
        {
            if (card == null) return;

            if (!_cards.ContainsKey(card.InstanceId))
            {
                _cards.Add(card.InstanceId, card);
            }
            else
            {
                // 이미 존재한다면 덮어씌우거나 무시
                _cards[card.InstanceId] = card;
            }
        }

        private void RegisterDeckCards(DeckState deckState)
        {
            CardInstance root = deckState.GetRootCardInstance();
            if (root != null)
            {
                _cards.TryAdd(root.InstanceId, root);
            }

            foreach (CardInstance card in deckState.GetAllCards())
            {
                _cards.TryAdd(card.InstanceId, card);
            }
        }


        /* public */

        public CardInstance? GetCard(int instanceId)
        {
            return _cards.GetValueOrDefault(instanceId);
        }

        public Player GetActivePlayer()
        {
            return Player.Game;
        }

        public FieldState GetFieldStateById(Player player)
        {
            if (!FieldStatesById.TryGetValue((int)player, out FieldState state))
            {
                state = new FieldState();
                FieldStatesById[(int)player] = state;
            }

            return state;
        }

        public FieldTree GetFieldTreeById(Player player)
        {
            var state = GetFieldStateById(player);
            return state.GetFieldTree();
        }

        public DeckState GetDeckStateById(Player player)
        {
            if (!DeckStatesById.TryGetValue((int)player, out DeckState state))
            {
                state = new DeckState();
                DeckStatesById[(int)player] = state;
            }

            return state;
        }

        public DeckCollection GetTradeDeck()
        {
            return TradeDeck;
        }

        public DeckCollection GetFourHorseManDeck()
        {
            return FourHorseManDeck;
        }

        public DeckCollection GetExileDeck()
        {
            return ExiledDeck;
        }

        public PlayerState GetPlayerStateById(Player id)
        {
            return _players.Find(p => p.Id == id);
        }

        public bool AddToField(Player player, CardInstance cardInstance, int rootCardInstanceId)
        {
            // Field Tree
            FieldTree fieldTree = GetFieldTreeById(player);

            // 만약 부모(root) 노드가 아직 없다면 생성 (안전장치)
            if (!fieldTree.Nodes.ContainsKey(rootCardInstanceId))
            {
                fieldTree.AddNode(new FieldNode(rootCardInstanceId));
            }

            FieldNode currentNode = new FieldNode(cardInstance.InstanceId, rootCardInstanceId, new List<int>());
            fieldTree.AddNode(currentNode);
            return true;
        }

        public void AddPlayer(Player player, PlayerRole playerRole, PlayerLifeStatus playerLifeStatus,
            DeckState? deckState)
        {
            PlayerState newPlayer = new PlayerState(player, playerRole, playerLifeStatus);
            FieldState fieldState = GetFieldStateById(player);

            if (deckState != null)
            {
                SetPlayerDeck(player, deckState);
                InitPlayerField(player, fieldState);
            }

            _players.Add(newPlayer);
        }

        public void AddStarveCard(Player player, DeckState deckState, Zone zone = Zone.Deck,
            CardStatus status = CardStatus.Deck)
        {
            CardCatalog catalog = CardCatalog.Instance;
            Card starveCard = catalog.Get(4);
            CardInstance newStarveCardInstance =
                IdGenerator.ReturnInstanceId(starveCard, player, zone, status, _starveCardStartInstanceId);

            _cards[newStarveCardInstance.InstanceId] = newStarveCardInstance;

            CardMovementSystem.MoveCard(this, newStarveCardInstance.InstanceId, player, zone, status);
            _starveCardStartInstanceId++;
        }

        public void ShuffleDeck(Player player)
        {
            var deckState = GetDeckStateById(player);
            deckState?.Shuffle(_serverRng);
        }

        public bool DrawFourHorseManCard(out CardInstance poppedCard, Player player, Zone zone, CardStatus status)
        {
            if (FourHorseManDeck.Draw(out poppedCard))
            {
                poppedCard.ChangeOwner(player);
                poppedCard.ChangeZone(zone);
                poppedCard.ChangeCardStatus(status);
                return true;
            }

            poppedCard = new CardInstance();
            return false;
        }

        public void SetGameEnd(Player winner)
        {
            if (IsGameEnded) return;
            IsGameEnded = true;
            WinnerId = winner;
        }

        public System.Random ReturnServerRng()
        {
            return _serverRng;
        }
    }
}