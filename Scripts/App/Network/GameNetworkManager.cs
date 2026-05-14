using UnityEngine;
using Mirror;

namespace App.Network
{
    public class GameNetworkManager : NetworkManager
    {
        [Header("Game Presets")]
        [SerializeField]
        private NetworkGameController gameControllerPrefab;

        public static new GameNetworkManager singleton => NetworkManager.singleton as GameNetworkManager;

        public override void OnStartServer()
        {
            base.OnStartServer();

            GameObject controllerObj = Instantiate(gameControllerPrefab.gameObject);
            NetworkServer.Spawn(controllerObj);
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // 인스펙터에 할당된 Player Prefab 생성 
            // (이 프리팹에는 GamePlayer와 LobbyPlayerState가 모두 부착되어 있어야 함)
            GameObject playerObj = Instantiate(playerPrefab);

            // 권한 부여 및 네트워크 스폰 우선 실행
            NetworkServer.AddPlayerForConnection(conn, playerObj);
        }

        public void StartHostGame()
        {
            StartHost();
        }

        public void StartClientGame(string address)
        {
            this.networkAddress = address;
            StartClient();
        }
    }
}