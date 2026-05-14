using System.Collections.Generic;
using System.Threading.Tasks;
using App.Network;
using Components.Common.Buttons.Core;
using Data.Models;
using Data.Repositories;
using Steamworks;
using TMPro;
using UnityEngine;

namespace Scenes.Lobby
{
    public class LobbyUIManager : MonoBehaviour
    {
        public static LobbyUIManager Instance { get; private set; }

        [Header("UI Slots")]
        public PlayerUISlot hostSlot;
        public PlayerUISlot[] clientSlots = new PlayerUISlot[2];

        [Header("Host Controls & Countdown")]
        public CompoundButton startButton;
        public GameObject countdownPanel;
        public TextMeshProUGUI countdownText;

        private DeckRepository _deckRepository;
        public string LocalSelectedDeckName { get; set; }
        private Dictionary<string, DeckData> _cachedDecks = new Dictionary<string, DeckData>();

        private void Awake()
        {
            Instance = this;
            ClearAllSlots();
        }

        private async void Start()
        {
            await CardCatalog.InitializeAsync();
            _deckRepository = new DeckRepository(CardCatalog.Instance);
            _cachedDecks = await _deckRepository.LoadAllDecksAsync();
        }

        private void OnEnable()
        {
            SteamMatchmaking.OnLobbyMemberDataChanged += HandleMemberDataChanged;
            SteamMatchmaking.OnLobbyDataChanged += HandleLobbyDataChanged;

            if (startButton != null)
            {
                startButton.onLeftClickEvent.RemoveAllListeners();
                startButton.onLeftClickEvent.AddListener(OnClickStartButton);
            }
        }

        private void OnDisable()
        {
            SteamMatchmaking.OnLobbyMemberDataChanged -= HandleMemberDataChanged;
            SteamMatchmaking.OnLobbyDataChanged -= HandleLobbyDataChanged;
        }

        public void OnClickStartButton()
        {
            if (!Mirror.NetworkServer.active) return;

            bool isLocalTest = Mirror.NetworkManager.singleton.transport is kcp2k.KcpTransport;

            if (isLocalTest)
            {
                var players = FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None);
                int clientCount = 0;
                int readyCount = 0;

                foreach (var p in players)
                {
                    if (!p.isLeader)
                    {
                        clientCount++;
                        if (p.isReady) readyCount++;
                    }
                }

                if (clientCount == 0 || readyCount < clientCount)
                {
                    Debug.LogWarning($"[Local] 클라이언트 대기 중이거나 준비되지 않음. (Ready: {readyCount}/{clientCount})");
                    return;
                }

                // 호스트 단독 처리가 아닌, 모든 클라이언트로 RPC 전송
                var localState = Mirror.NetworkClient.localPlayer?.GetComponent<LobbyPlayerState>();
                if (localState != null && localState.isLeader)
                {
                    localState.CmdTriggerLocalGameStart();
                }
            }
            else if (SteamManager.Instance.CurrentLobby.HasValue)
            {
                var lobby = SteamManager.Instance.CurrentLobby.Value;
                int readyCount = 0;
                int clientCount = lobby.MemberCount - 1;

                foreach (var member in lobby.Members)
                {
                    if (member.Id != lobby.Owner.Id && lobby.GetMemberData(member, "IsReady") == "True")
                        readyCount++;
                }

                if (clientCount == 0 || readyCount < clientCount)
                {
                    Debug.LogWarning($"[Steam] 클라이언트 대기 중이거나 준비되지 않음. (Ready: {readyCount}/{clientCount})");
                    return;
                }

                SteamManager.Instance.CurrentLobby.Value.SetData("GameStarting", "True");
            }
        }

        // [추가] RPC 수신 시 각 클라이언트가 개별적으로 실행하는 로직
        public void ExecuteLocalGameStart()
        {
            PrepareMatchSessionData();
            StopAllCoroutines();
            StartCoroutine(CountdownAndTransitionRoutine());
        }

        private void HandleLobbyDataChanged(Steamworks.Data.Lobby lobby)
        {
            if (lobby.GetData("GameStarting") == "True")
            {
                ExecuteLocalGameStart(); // 중복 코드 제거 및 메서드 통합
            }
        }

        private void HandleMemberDataChanged(Steamworks.Data.Lobby lobby, Friend member)
        {
            string selectedDeck = lobby.GetMemberData(member, "SelectedDeck");
            PlayerUISlot targetSlot = GetSlotBySteamId(member.Id);

            if (targetSlot != null && !string.IsNullOrEmpty(selectedDeck))
            {
                targetSlot.UpdateRemoteDeckSelection(selectedDeck);
            }

            string isReadyStr = lobby.GetMemberData(member, "IsReady");
            bool isReady = isReadyStr == "True";

            if (targetSlot != null) targetSlot.UpdateRemoteReadyState(isReady);

            if (SteamManager.Instance.IsHost)
            {
                CheckAllPlayersReady(lobby);
            }
        }

        private void CheckAllPlayersReady(Steamworks.Data.Lobby lobby)
        {
            int readyCount = 0;
            int clientCount = lobby.MemberCount - 1;

            foreach (var member in lobby.Members)
            {
                if (member.Id != lobby.Owner.Id && lobby.GetMemberData(member, "IsReady") == "True")
                    readyCount++;
            }

            if (startButton != null)
            {
                startButton.IsInteractable = (clientCount > 0 && readyCount == clientCount);
            }
        }

        public void CheckLocalReadyState()
        {
            if (!Mirror.NetworkServer.active) return;

            var players = FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None);
            int clientCount = 0;
            int readyCount = 0;

            foreach (var p in players)
            {
                if (!p.isLeader)
                {
                    clientCount++;
                    if (p.isReady) readyCount++;
                }
            }

            if (startButton != null)
            {
                startButton.IsInteractable = (clientCount > 0 && readyCount == clientCount);
            }
        }

        private System.Collections.IEnumerator CountdownAndTransitionRoutine()
        {
            countdownPanel.SetActive(true);

            for (int i = 5; i > 0; i--)
            {
                countdownText.text = i.ToString();
                yield return new WaitForSeconds(1f);
            }

            countdownText.text = "0";

            if (Mirror.NetworkServer.active)
            {
                Mirror.NetworkManager.singleton.ServerChangeScene("05_InGame");
            }
        }

        public void AssignPlayerToSlot(ulong id, bool isHost, bool isLocalPlayer, bool isReady)
        {
            PlayerUISlot targetSlot = GetSlotBySteamId(id);

            if (targetSlot != null)
            {
                bool isCurrentlyHostSlot = (targetSlot == hostSlot);
                if (isCurrentlyHostSlot != isHost)
                {
                    targetSlot.Clear();
                    targetSlot = null;
                }
            }

            if (targetSlot == null)
            {
                targetSlot = isHost ? hostSlot : GetAvailableClientSlot();

                if (targetSlot != null)
                {
                    List<string> deckNames = new List<string>(_cachedDecks.Keys);
                    targetSlot.Setup(id, isLocalPlayer, isHost, deckNames);
                }
            }

            if (targetSlot != null)
            {
                if (targetSlot.deckDropdown != null)
                    targetSlot.deckDropdown.gameObject.SetActive(isLocalPlayer);

                targetSlot.UpdateRemoteReadyState(isReady);

                if (isLocalPlayer && startButton != null)
                {
                    startButton.gameObject.SetActive(isHost);
                }
            }

            if (Mirror.NetworkManager.singleton.transport is kcp2k.KcpTransport)
            {
                CheckLocalReadyState();
            }
        }

        public PlayerUISlot GetSlotBySteamId(ulong id)
        {
            if (id == 0) return null;

            if (hostSlot.SteamId == id) return hostSlot;
            foreach (var slot in clientSlots)
            {
                if (slot.SteamId == id) return slot;
            }

            return null;
        }

        private PlayerUISlot GetAvailableClientSlot()
        {
            foreach (var slot in clientSlots)
            {
                if (slot.IsEmpty) return slot;
            }

            return null;
        }

        public PlayerUISlot FindSlotBySteamId(ulong id) => GetSlotBySteamId(id);

        public void ClearAllSlots()
        {
            hostSlot.Clear();
            foreach (var slot in clientSlots) slot.Clear();
        }

        private void PrepareMatchSessionData()
        {
            DeckData myDeckData = GetDeckDataByName(LocalSelectedDeckName);
            List<ulong> allPlayers = new List<ulong>();

            if (Mirror.NetworkManager.singleton.transport is kcp2k.KcpTransport)
            {
                var players = FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None);
                System.Array.Sort(players, (a, b) => a.netId.CompareTo(b.netId));

                foreach (var p in players)
                {
                    allPlayers.Add(p.steamId);
                }
            }
            else if (SteamManager.Instance.CurrentLobby.HasValue)
            {
                foreach (var member in SteamManager.Instance.CurrentLobby.Value.Members)
                {
                    allPlayers.Add(member.Id);
                }
            }

            MatchSessionManager.Instance.SetupSession(myDeckData, allPlayers);
        }

        public void UpdateReadyUI(ulong id, bool isReady)
        {
            PlayerUISlot targetSlot = FindSlotBySteamId(id);
            if (targetSlot != null) targetSlot.UpdateReadyState(isReady);

            if (Mirror.NetworkManager.singleton.transport is kcp2k.KcpTransport)
            {
                CheckLocalReadyState();
            }
        }

        public void ClearSlotBySteamId(ulong id)
        {
            if (hostSlot.SteamId == id) hostSlot.Clear();
            foreach (var slot in clientSlots)
            {
                if (slot.SteamId == id) slot.Clear();
            }

            if (Mirror.NetworkManager.singleton.transport is kcp2k.KcpTransport)
            {
                CheckLocalReadyState();
            }
        }

        private DeckData GetDeckDataByName(string deckName)
        {
            if (string.IsNullOrEmpty(deckName)) return null;
            _cachedDecks.TryGetValue(deckName, out DeckData data);
            return data;
        }
    }
}