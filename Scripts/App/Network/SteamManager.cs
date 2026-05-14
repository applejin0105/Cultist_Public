using System;
using Mirror;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace App.Network
{
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; }

        public ulong MySteamID { get; private set; }
        public string MyName { get; private set; }

        public Lobby? CurrentLobby { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSteam(); // 스팀 초기화 진입점
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            // 씬 전환 시 파괴되더라도 스팀 클라이언트를 끄지 않도록 Shutdown 로직 제거
            // 싱글톤 참조 해제만 수행
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void InitializeSteam()
        {
            try
            {
                SteamClient.Init(4696600, true);

                if (SteamClient.IsValid)
                {
                    MySteamID = SteamClient.SteamId;
                    MyName = SteamClient.Name;
                    Debug.Log($"[SteamManager] SteamID: {MySteamID}, Name: {MyName}");
                    RegisterSteamCallbacks();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SteamManager] Initialization failed: {e.Message}");
            }
        }

        public bool IsHost
        {
            get
            {
                if (CurrentLobby.HasValue)
                {
                    return CurrentLobby.Value.Owner.Id == MySteamID;
                }

                return false;
            }
        }

        private void Update() => SteamClient.RunCallbacks();

        private void OnApplicationQuit()
        {
            DisconnectNetwork();

            try
            {
                Steamworks.SteamClient.Shutdown();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[SteamManager] 스팀 셧다운 예외: {e.Message}");
            }
        }

        public void DisconnectNetwork()
        {
            if (Mirror.NetworkManager.singleton != null)
            {
                if (Mirror.NetworkServer.active && Mirror.NetworkClient.isConnected)
                {
                    Mirror.NetworkManager.singleton.StopHost();
                }
                else if (Mirror.NetworkClient.isConnected)
                {
                    Mirror.NetworkManager.singleton.StopClient();
                }
            }
        }

        private void RegisterSteamCallbacks()
        {
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        }

        public async void CreateLobby(Action<bool> onComplete = null)
        {
            Debug.Log("[SteamManager] 스팀 로비 생성 요청 중...");

            var lobby = await SteamMatchmaking.CreateLobbyAsync(3);

            if (!lobby.HasValue)
            {
                Debug.LogError("[SteamManager] 로비 생성 실패! (스팀 서버 지연 또는 접속 문제)");
                onComplete?.Invoke(false);
                return;
            }

            Debug.Log($"[SteamManager] 로비 생성 성공! ID: {lobby.Value.Id}");

            CurrentLobby = lobby.Value;
            CurrentLobby.Value.SetFriendsOnly();
            CurrentLobby.Value.SetJoinable(true);

            Debug.Log("[SteamManager] Mirror 호스트 시작 준비");

            // [핵심 해결 로직] 좀비 상태(Fake Null)의 네트워크 매니저 참조를 방어하고, 유효한 진짜 객체를 찾습니다.
            var netManager = GameNetworkManager.singleton;

            // Unity의 오버로딩된 == 연산자를 통해 기본 참조나 내부 Native 객체가 파괴되었는지 검사
            if (netManager == null || netManager.gameObject == null)
            {
                Debug.LogWarning("[SteamManager] 기존 네트워크 매니저가 유실되어 씬에서 새로 탐색합니다.");
                netManager = FindFirstObjectByType<GameNetworkManager>();
            }

            if (netManager != null)
            {
                netManager.StartHost();
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogError("[SteamManager] 씬에서 활성화된 GameNetworkManager를 찾을 수 없습니다!");
                onComplete?.Invoke(false);
            }
        }

        public void OpenSteamOverlayForFriends()
        {
        }

        private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId) => lobby.Join();

        private void OnLobbyEntered(Lobby lobby)
        {
            CurrentLobby = lobby;
            if (NetworkServer.active) return;

            string hostAddress = lobby.Owner.Id.ToString();
            GameNetworkManager.singleton.networkAddress = hostAddress;
            GameNetworkManager.singleton.StartClient();
        }

        public void LeaveLobby()
        {
            if (CurrentLobby.HasValue)
            {
                CurrentLobby.Value.Leave();
                CurrentLobby = null;
                UnityEngine.Debug.Log("[SteamManager] 로비를 떠났습니다.");
            }
        }
    }
}
