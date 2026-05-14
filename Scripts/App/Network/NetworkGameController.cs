using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Data.Enums;
using Core.Managers;
using Data.Models;
using Data.Repositories;
using UnityEngine;
using Mirror;
using Domain.State.Host;
using Domain.Enums;
using Systems;
using Domain.State;
using Effects.Core;
using Scenes.InGame.Core;
using Scenes.Lobby;

namespace App.Network
{
    public class NetworkGameController : NetworkBehaviour
    {
        public static NetworkGameController Instance { get; private set; }

        public GameState ServerGameState { get; private set; }
        private TurnSystem _turnSystem;
        private GameActionSystem _actionSystem;
        private PhaseSystem _phaseSystem;
        private EffectsBootstrap _effectsBootstrap;
        private DeckSystem _deckSystem;
        private GameRuleSystem _gameRuleSystem;
        private RemotePlayerInputProvider _remoteInput;

        private readonly Dictionary<int, GamePlayer> _seatMap = new Dictionary<int, GamePlayer>();
        private int _connectedCount = 0;
        private int _expectedPlayerCount = 0;

        public event System.Action OnClientGameStateChanged;

        [SyncVar(hook = nameof(OnStateChangedHook))]
        public int CurrentRound;
        [SyncVar(hook = nameof(OnStateChangedHook))]
        public int CurrentActivePlayerSeat;
        [SyncVar(hook = nameof(OnStateChangedHook))]
        public int CurrentPhaseMain;
        [SyncVar(hook = nameof(OnStateChangedHook))]
        public int CurrentPhaseSub;

        [SyncVar] public int WinnerSeat;
        [SyncVar(hook = nameof(OnGameEndedHook))]
        public bool IsGameEnded;

        public readonly SyncList<NetworkDTOs.CardNetData> SyncCards = new SyncList<NetworkDTOs.CardNetData>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeServerLogic();
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name == "05_InGame")
            {
                if (isServer)
                {
                    ResetSession();
                    _expectedPlayerCount = MatchSessionManager.Instance != null
                        ? MatchSessionManager.Instance.PlayerSteamIds.Count
                        : 1;
                    StartCoroutine(WaitForPlayersToStartGame());
                }
            }
        }

        private async void InitializeServerLogic()
        {
            var rng = new System.Random();
            ServerGameState = new GameState(rng);
            _phaseSystem = new PhaseSystem();
            _phaseSystem.Initialize();
            _phaseSystem.OnPhaseChanged += OnServerPhaseChanged;
            _gameRuleSystem = new GameRuleSystem(ServerGameState);

            _remoteInput = new RemotePlayerInputProvider(this);
            _actionSystem = new GameActionSystem(ServerGameState, _remoteInput);
            var rngSrc = new SystemRandomSource(rng);

            // TurnSystem을 먼저 생성
            _turnSystem = new TurnSystem(ServerGameState, _phaseSystem, _actionSystem, rng, _remoteInput);
            _turnSystem.SetGameRuleSystem(_gameRuleSystem);

            _effectsBootstrap = new EffectsBootstrap(
                ServerGameState, _actionSystem, _remoteInput, rngSrc, _phaseSystem, _turnSystem, _gameRuleSystem);

            _actionSystem.SetEffectRunner(_effectsBootstrap.Runner);
            _actionSystem.SetTargetResolver(_effectsBootstrap.Targets);
            _actionSystem.SetGameRuleSystem(_gameRuleSystem);
            _actionSystem.StatSystem.SetGameRuleSystem(_gameRuleSystem);

            var deckRepo = new DeckRepository(CardCatalog.Instance);
            _deckSystem = new DeckSystem(deckRepo);
            await _deckSystem.Initialize();
        }

        [Server]
        public void TriggerCardSync()
        {
            SyncFullGameState();
        }

        public void RegisterPlayer(GamePlayer playerObject)
        {
            _connectedCount++;
            int seat = _connectedCount;
            playerObject.seatIndex = seat;
            _seatMap[seat] = playerObject;

            Debug.Log($"[Server] 플레이어 덱 제출 완료: Seat {seat} (현재 {_connectedCount}/{_expectedPlayerCount}명)");
        }

        private IEnumerator WaitForPlayersToStartGame()
        {
            Debug.Log($"[Server] 게임 시작 대기 중... 총 예상 인원: {_expectedPlayerCount}명");

            float timeout = 10f;
            while (_connectedCount < _expectedPlayerCount && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (_connectedCount < _expectedPlayerCount)
            {
                Debug.LogError($"[Server] 시간 초과! 클라이언트 접속 누락. ({_connectedCount}/{_expectedPlayerCount}) 강제 시작합니다.");
            }

            StartGameLogic(_connectedCount);
        }

        public void StartGameLogic(int playerCount)
        {
            Debug.Log($"[Server] {playerCount}명 접속 완료. 게임 로직 초기화 시작.");
            var rng = new System.Random();

            for (int i = 1; i <= playerCount; i++)
            {
                GamePlayer pComp = _seatMap[i];
                DeckData dData = InGameSessionManager.Instance.GetPlayerDeck(pComp.netId);

                if (dData != null)
                {
                    var list = dData.cardIds;
                    int n = list.Count;
                    while (n > 1)
                    {
                        n--;
                        int k = rng.Next(n + 1);
                        (list[k], list[n]) = (list[n], list[k]);
                    }

                    var deckState = Utils.IdGenerator.ReturnInstanceIdDeck(dData, (Player)i);
                    ServerGameState.AddPlayer((Player)i, PlayerRole.Participant, PlayerLifeStatus.Alive, deckState);

                    Debug.Log($"[Server] Seat {i} 덱 '{dData.deckName}' 로드 및 셔플, Root 카드 자동 배치 완료.");
                }
                else
                {
                    Debug.LogError($"[Server] Seat {i}의 덱 데이터를 찾을 수 없습니다!");
                }
            }

            ServerGameState.InitLogic(rng);
            SyncFullGameState();

            RpcInitializeGameUI(playerCount);

            Invoke(nameof(KickOffSetupTurn), 1.0f);
        }

        private void KickOffSetupTurn()
        {
            _ = RunSetupTurnAsync();
        }

        private async System.Threading.Tasks.Task RunSetupTurnAsync()
        {
            try
            {
                Debug.Log("[Setup] 0번째 턴 시작 — turn order 결정");
                _turnSystem.PrepareFirstRound();

                var order = ServerGameState.TurnState.TurnOrder;
                Debug.Log($"[Setup] root OnReveal 순서: {string.Join(", ", order)}");

                foreach (var player in order)
                {
                    var deckState = ServerGameState.GetDeckStateById(player);
                    var root = deckState?.GetRootCardInstance();
                    if (root == null) continue;

                    CardMovementSystem.MoveCard(ServerGameState, root.InstanceId, player, Domain.Enums.Zone.Field,
                        Domain.Enums.CardStatus.FieldBack);
                    SyncFullGameState();

                    await System.Threading.Tasks.Task.Delay(1500);

                    Debug.Log($"[Setup] {player} root 공개 실행 (ID: {root.CardId})");
                    await _actionSystem.Reveal(player, root, RevealReason.Auto);

                    SyncFullGameState();
                    await System.Threading.Tasks.Task.Delay(500);
                }

                Debug.Log("[Setup] 0번째 턴 종료 — 1번째 턴 진입");
                _turnSystem.StartFirstTurn();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Setup] 0번째 턴 처리 중 오류: {e.Message}");
            }
        }

        [Server]
        public void ExecuteTurnEnd()
        {
            if (ServerGameState == null || ServerGameState.IsGameEnded) return;

            var activePlayerId = ServerGameState.TurnState.ActivePlayer;
            if (activePlayerId.HasValue)
            {
                var pState = ServerGameState.GetPlayerStateById(activePlayerId.Value);
                if (pState != null && pState.Hand.Count > 0)
                {
                    Debug.LogWarning($"[Server] {activePlayerId.Value}의 패에 카드가 남아있어 사이클을 마칠 수 없습니다.");
                    return;
                }
            }

            Debug.Log($"[Server] 사이클 마감 처리: Seat {CurrentActivePlayerSeat}");
            _turnSystem.EndCurrentPlayerTurn();
            UpdateTurnSyncVars();
            SyncFullGameState();
        }

        [Server]
        private void UpdateTurnSyncVars()
        {
            if (ServerGameState.TurnState.ActivePlayer.HasValue)
            {
                CurrentActivePlayerSeat = (int)ServerGameState.TurnState.ActivePlayer.Value;
            }

            CurrentRound = ServerGameState.TurnState.RoundNumber;

            if (_phaseSystem != null)
            {
                CurrentPhaseMain = (int)_phaseSystem.CurrentPhaseState.Main;
                CurrentPhaseSub = _phaseSystem.CurrentPhaseState.Sub;
            }
            else
            {
                CurrentPhaseMain = (int)ServerGameState.TurnState.Phase.Main;
                CurrentPhaseSub = ServerGameState.TurnState.Phase.Sub;
            }
        }

        [ClientRpc]
        private void RpcInitializeGameUI(int totalPlayers)
        {
            StartCoroutine(InitUIRoutine(totalPlayers));
        }

        private IEnumerator InitUIRoutine(int totalPlayers)
        {
            yield return new WaitUntil(() =>
                Mirror.NetworkClient.localPlayer != null &&
                Mirror.NetworkClient.localPlayer.GetComponent<GamePlayer>().seatIndex != 0);

            var localPlayer = Mirror.NetworkClient.localPlayer.GetComponent<GamePlayer>();

            List<int> allSeats = new List<int>();
            for (int i = 1; i <= totalPlayers; i++) allSeats.Add(i);

            InGameUIManager.Instance.InitializeLocalPlayerUI(localPlayer.seatIndex, allSeats);
            InGameUIManager.Instance.InitializeUIMapping(localPlayer.seatIndex, totalPlayers);

            float timeout = 5f;
            while (timeout > 0)
            {
                var players = FindObjectsByType<GamePlayer>(FindObjectsSortMode.None);
                if (players.Length == totalPlayers)
                {
                    bool allNamesLoaded = true;
                    foreach (var p in players)
                    {
                        if (string.IsNullOrEmpty(p.steamName)) allNamesLoaded = false;
                    }

                    if (allNamesLoaded)
                    {
                        foreach (var p in players)
                        {
                            bool isMe = (p.seatIndex == localPlayer.seatIndex);
                            InGameUIManager.Instance.UpdateRealPlayerName(p.seatIndex, p.steamName, p.steamId, isMe);
                        }

                        yield break;
                    }
                }

                timeout -= 0.5f;
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void OnServerPhaseChanged(PhaseState newState)
        {
            UpdateTurnSyncVars();
            SyncFullGameState();
        }

        private void SyncFullGameState()
        {
            SyncCards.Clear();
            foreach (var kvp in ServerGameState.Cards)
            {
                var c = kvp.Value;
                int parentInstanceId = 0;
                int siblingIndex = 0;

                if (c.Zone == Domain.Enums.Zone.Field)
                {
                    var fieldTree = ServerGameState.GetFieldTreeById(c.OwnerSeat);
                    if (fieldTree != null)
                    {
                        var node = fieldTree.GetNodeByInstanceId(c.InstanceId);
                        if (node != null && node.ParentInstanceId.HasValue)
                        {
                            parentInstanceId = node.ParentInstanceId.Value;
                            var parentNode = fieldTree.GetNodeByInstanceId(parentInstanceId);
                            if (parentNode != null) siblingIndex = parentNode.ChildrenInstanceIds.IndexOf(c.InstanceId);
                        }
                    }
                }

                SyncCards.Add(new NetworkDTOs.CardNetData
                {
                    InstanceId = c.InstanceId,
                    CardId = c.CardId,
                    OwnerSeat = (int)c.OwnerSeat,
                    Zone = (int)c.Zone,
                    Status = (int)c.CardStatus,
                    IsReveal = (c.CardStatus == CardStatus.FieldFront),
                    ParentInstanceId = parentInstanceId,
                    SiblingIndex = siblingIndex
                });
            }

            foreach (var p in ServerGameState.Players)
            {
                RpcSyncPlayerStats((int)p.Id, p.Symbols, p.Cultist);
                var playerComp = GetPlayerComponent(p.Id);
                if (playerComp != null)
                {
                    var pState = ServerGameState.GetPlayerStateById(p.Id);
                    if (pState != null) playerComp.maxJunction = pState.MaxJunction;
                }
            }
        }

        [ClientRpc]
        private void RpcSyncPlayerStats(int seatIndex, int[] symbols, int cultist)
        {
            if (InGameUIManager.Instance != null)
            {
                var panel = InGameUIManager.Instance.GetPanelBySeat(seatIndex);
                if (panel != null)
                {
                    panel.UpdateAllSymbols(symbols);
                    panel.UpdateCultistCount(cultist);
                }

                var localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<GamePlayer>();
                if (localPlayer != null && localPlayer.seatIndex == seatIndex)
                {
                    InGameUIManager.Instance.TriggerLocalStatsUpdated();
                }
            }
        }

        [Server]
        public async void ExecutePlayCard(int playerSeatIndex, int handCardInstanceId, int targetFieldCardInstanceId,
            int slotIndex)
        {
            if (ServerGameState == null || ServerGameState.IsGameEnded) return;
            if (CurrentActivePlayerSeat != playerSeatIndex) return;

            var player = (Player)playerSeatIndex;
            var handCard = ServerGameState.GetCard(handCardInstanceId);
            var parentCard = ServerGameState.GetCard(targetFieldCardInstanceId);

            if (handCard != null && parentCard != null)
            {
                await _actionSystem.Play(player, handCard, parentCard, slotIndex);
                SyncFullGameState();
            }
        }

        [Server]
        public async void ExecuteRevealCard(int playerSeatIndex, int cardInstanceId)
        {
            if (ServerGameState == null || ServerGameState.IsGameEnded) return;

            // [추가] 탈락한 플레이어는 카드 공개 불가
            var pState = ServerGameState.GetPlayerStateById((Player)playerSeatIndex);
            if (pState == null || pState.LifeStatus != PlayerLifeStatus.Alive)
            {
                Debug.LogWarning($"[Server] 탈락한 플레이어 {playerSeatIndex}가 RevealCard를 시도함.");
                return;
            }

            if (CurrentActivePlayerSeat != playerSeatIndex) return;

            var card = ServerGameState.GetCard(cardInstanceId);
            if (card != null)
            {
                await _actionSystem.Reveal((Player)playerSeatIndex, card, RevealReason.Manual);
                SyncFullGameState();
            }
        }

        [Server]
        public async void ExecuteUseCard(int playerSeatIndex, int cardInstanceId)
        {
            if (ServerGameState == null || ServerGameState.IsGameEnded) return;

            // [추가] 탈락한 플레이어는 카드 사용 불가
            var pState = ServerGameState.GetPlayerStateById((Player)playerSeatIndex);
            if (pState == null || pState.LifeStatus != PlayerLifeStatus.Alive)
            {
                Debug.LogWarning($"[Server] 탈락한 플레이어 {playerSeatIndex}가 UseCard를 시도함.");
                return;
            }

            if (CurrentActivePlayerSeat != playerSeatIndex) return;

            var card = ServerGameState.GetCard(cardInstanceId);
            if (card != null)
            {
                await _actionSystem.Use((Player)playerSeatIndex, card);
                SyncFullGameState();
            }
        }

        [Server]
        public void ExecuteAdvancePhase(int playerSeatIndex)
        {
            if (ServerGameState == null || ServerGameState.IsGameEnded) return;

            // [추가] 탈락한 플레이어는 페이즈 전환 불가
            var pState = ServerGameState.GetPlayerStateById((Player)playerSeatIndex);
            if (pState == null || pState.LifeStatus != PlayerLifeStatus.Alive)
            {
                Debug.LogWarning($"[Server] 탈락한 플레이어 {playerSeatIndex}가 AdvancePhase를 시도함.");
                return;
            }

            if (CurrentActivePlayerSeat != playerSeatIndex) return;

            // [규격화] 클릭 시 TurnSystem에 위임
            _turnSystem.PlayerRequestedAdvancePhase((Player)playerSeatIndex);
        }

        public TurnSystem GetTurnSystem() => _turnSystem;

        public GamePlayer GetPlayerComponent(Player player)
        {
            if (_seatMap.TryGetValue((int)player, out var comp)) return comp;
            return null;
        }

        public void ResetSession()
        {
            _connectedCount = 0;
            _seatMap.Clear();
            _expectedPlayerCount = 0;
            IsGameEnded = false;
            WinnerSeat = 0;

            // [수정] 이전 게임 상태 전체 폐기 — ServerGameState / 시스템들 / EffectsBootstrap 까지
            //   모두 재생성하지 않으면 두 번째 게임에서 이전 필드/카드/히스토리가 그대로 남는다.
            //   EffectRunner 등 하위 시스템은 생성자에서 ServerGameState 참조를 보관하므로
            //   InitializeServerLogic() 을 다시 호출하여 일괄 교체한다.
            if (_phaseSystem != null)
            {
                _phaseSystem.OnPhaseChanged -= OnServerPhaseChanged;
            }
            SyncCards.Clear();

            ServerGameState = null;
            _phaseSystem = null;
            _gameRuleSystem = null;
            _remoteInput = null;
            _actionSystem = null;
            _turnSystem = null;
            _effectsBootstrap = null;
            _deckSystem = null;

            InitializeServerLogic();

            Debug.Log("[Server] NetworkGameController 세션 데이터 초기화 완료 (ServerGameState/EffectsBootstrap 재생성).");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            SyncCards.Callback += OnSyncCardsChanged;
        }

        private void OnSyncCardsChanged(SyncList<NetworkDTOs.CardNetData>.Operation op, int itemIndex,
            NetworkDTOs.CardNetData oldItem, NetworkDTOs.CardNetData newItem)
        {
            if (isServer) return; // 서버는 GameRuleSystem이 별도로 동작함

            // 클라이언트 사이드 즉시 판정 (Visual Prediction)
            CheckClientSideVictoryConditions();
        }

        private void CheckClientSideVictoryConditions()
        {
            if (IsGameEnded) return;

            // 1. 사기사 강림 체크 (클라이언트 로컬 데이터 기반)
            //   사기사 = [계시] 카드(IsRevealImmediately=true). 4장 모두 필드에 (파괴되지 않은 채로) 있으면 강림.
            int horsemenCount = 0;
            foreach (var card in SyncCards)
            {
                // Zone.Field(1), CardStatus != FieldDestroyed(2)
                if (card.Zone == 1 && card.Status != 2 && IsRevelationCard(card.CardId))
                {
                    horsemenCount++;
                }
            }

            if (horsemenCount >= 4)
            {
                Debug.Log($"[Client Rule] 사기사 강림 감지 ({horsemenCount}/4). 승리 UI 예고.");
                // 서버가 IsGameEnded를 true로 바꿀 때까지 기다리지만, 
                // 필요한 경우 여기서 미리 UI 효과를 줄 수 있습니다.
            }
        }

        /// <summary>
        /// 카드가 [계시] 키워드(=사기사) 인지 cardDB 데이터 기반으로 판정.
        /// 기존 cardId 11~14 하드코딩 제거 — IsRevealImmediately 플래그가 SoT.
        /// </summary>
        private bool IsRevelationCard(int cardId)
        {
            if (Data.Models.CardCatalog.Instance == null) return false;
            if (!Data.Models.CardCatalog.Instance.Contains(cardId)) return false;
            var card = Data.Models.CardCatalog.Instance.Get(cardId);
            return card != null && card.IsRevealImmediately;
        }

        private void OnStateChangedHook(int oldVal, int newVal) => OnClientGameStateChanged?.Invoke();

        private void OnGameEndedHook(bool oldVal, bool newVal)
        {
            if (oldVal == newVal || !newVal) return;

            Debug.Log($"[Client Network] <게임 종료 UI 출력 요청> Winner Seat: {WinnerSeat}");

            if (Scenes.InGame.UI.GameTurnUIManager.Instance != null)
                Scenes.InGame.UI.GameTurnUIManager.Instance.ShowGameEnd(WinnerSeat);

            // [추가] 승패 사운드 재생
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            if (localPlayer != null && SoundManager.Instance != null)
            {
                UISoundType sound = (localPlayer.seatIndex == WinnerSeat) ? UISoundType.Win : UISoundType.Lose;
                SoundManager.Instance.PlaySfx(sound);
            }
        }

        [ClientRpc]
        public void RpcNotifyTurnStart(int activeSeat)
        {
            if (Scenes.InGame.UI.GameTurnUIManager.Instance != null)
                Scenes.InGame.UI.GameTurnUIManager.Instance.ShowTurnBanner(activeSeat);
        }

        [Server]
        public void TriggerGameEnd(int winnerSeat)
        {
            if (IsGameEnded) return;

            Debug.Log($"[Server Network] <게임 종료 트리거 전파> Winner Seat: {winnerSeat}");

            WinnerSeat = winnerSeat;
            IsGameEnded = true;
            Debug.Log($"[Server] 게임 종료. 승자 시트: {winnerSeat}. 5 초 후 로비로 복귀.");
            StartCoroutine(ReturnToLobbyAfterDelay(5f));
        }

        private const string LobbySceneName = "03_Lobby";

        [Server]
        private IEnumerator ReturnToLobbyAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (Mirror.NetworkManager.singleton != null)
                Mirror.NetworkManager.singleton.ServerChangeScene(LobbySceneName);
        }

        public void OnClientSubmitTargets(int playerSeatIndex, int[] selectedIds) =>
            _remoteInput.ReceiveTargetResponse((Player)playerSeatIndex, new List<int>(selectedIds));

        public void OnClientSubmitDrawAction(int playerSeatIndex, int actionType) =>
            _remoteInput.ReceiveDrawActionResponse((Player)playerSeatIndex, actionType);

        public void OnClientSubmitKeepCard(int playerSeatIndex, int selectedId) =>
            _remoteInput.ReceiveKeepCardResponse((Player)playerSeatIndex, selectedId);

        public void OnClientSubmitTradeSelect(int playerSeatIndex, int selectedId) =>
            _remoteInput.ReceiveTradeSelectResponse((Player)playerSeatIndex, selectedId);
    }
}