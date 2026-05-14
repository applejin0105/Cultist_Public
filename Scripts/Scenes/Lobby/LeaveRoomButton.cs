using App.Network;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scenes.Lobby
{
    public class LeaveRoomButton : MonoBehaviour
    {
        public void OnClickLeaveButton()
        {
            // UI 즉시 정리
            if (LobbyUIManager.Instance != null) LobbyUIManager.Instance.ClearAllSlots();

            // 스팀 로비 퇴장
            SteamManager.Instance.LeaveLobby();

            // 네트워크 연결 종료
            if (NetworkManager.singleton != null)
            {
                if (NetworkServer.active && NetworkClient.isConnected)
                    NetworkManager.singleton.StopHost();
                else if (NetworkClient.isConnected)
                    NetworkManager.singleton.StopClient();
            }

            // 연결 종료 후 0.1초 뒤 강제 씬 이동 (Mirror가 안 보내줄 경우 대비)
            Invoke(nameof(ForceGoToMain), 0.1f);
        }

        private void ForceGoToMain()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("01_Main");
        }
    }
}