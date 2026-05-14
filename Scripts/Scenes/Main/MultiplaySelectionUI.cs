using App.Network;
using Components.Common.Buttons.Core;
using Mirror;
using Steamworks;
using UI.Core;
using UnityEngine;

namespace Scenes.Main
{
    public class MultiplaySelectionUI : MonoBehaviour
    {
        [Header("Steam Buttons")]
        [SerializeField] private CompoundButton hostButton;
        [SerializeField] private CompoundButton joinToHostButton;

        [Header("Local Debug Buttons")]
        [SerializeField] private CompoundButton localHostButton;
        [SerializeField] private CompoundButton localJoinButton;

        private void OnEnable()
        {
            SetButtonsInteractable(true);

            hostButton.onLeftClickEvent.AddListener(OnHostClicked);
            joinToHostButton.onLeftClickEvent.AddListener(OnInviteFriendClicked);

            if (localHostButton != null)
                localHostButton.onLeftClickEvent.AddListener(OnLocalHostClicked);
            if (localJoinButton != null)
                localJoinButton.onLeftClickEvent.AddListener(OnLocalJoinClicked);
        }

        private void OnDisable()
        {
            hostButton.onLeftClickEvent.RemoveAllListeners();
            joinToHostButton.onLeftClickEvent.RemoveAllListeners();

            if (localHostButton != null)
                localHostButton.onLeftClickEvent.RemoveAllListeners();
            if (localJoinButton != null)
                localJoinButton.onLeftClickEvent.RemoveAllListeners();
        }

        private void SetButtonsInteractable(bool state)
        {
            hostButton.IsInteractable = state;
            joinToHostButton.IsInteractable = state;
            if (localHostButton != null) localHostButton.IsInteractable = state;
            if (localJoinButton != null) localJoinButton.IsInteractable = state;
        }

        // 스팀 트랜스포트를 원천 파괴하고 KCP를 강제 주입하는 무결점 메서드
        private void ForceLocalTransport()
        {
            // 1. NetworkManager에 붙어있는 모든 Transport 클래스 탐색
            Transport[] allTransports = NetworkManager.singleton.GetComponents<Transport>();

            // 2. KCP 트랜스포트 확보 (없을 경우 AddComponent로 즉시 생성. Awake가 강제 실행되어 NRE 절대 발생 안함)
            kcp2k.KcpTransport kcp = NetworkManager.singleton.GetComponent<kcp2k.KcpTransport>();
            if (kcp == null)
            {
                kcp = NetworkManager.singleton.gameObject.AddComponent<kcp2k.KcpTransport>();
                kcp.Port = 7777;
            }

            // 3. KCP를 제외한 모든 트랜스포트(FizzyFacepunch, Multiplex 등)를 메모리에서 즉각 파괴
            // (ParrelSync 로컬 클론 창이므로 스팀 관련 객체를 날려버려도 시스템에 무관함)
            foreach (var t in allTransports)
            {
                if (t != kcp) Destroy(t);
            }

            // 4. Mirror 루프에 강제 연결
            Transport.active = kcp;
            NetworkManager.singleton.transport = kcp;
        }

        #region Steam Logic

        private void OnHostClicked()
        {
            SetButtonsInteractable(false);
            var transitionManager = FindFirstObjectByType<RadialTransitionManager>();
            if (transitionManager != null)
                transitionManager.ForceShrinkAndExecute(ExecuteCreateLobby);
            else
                ExecuteCreateLobby();
        }

        private void ExecuteCreateLobby()
        {
            SteamManager.Instance.CreateLobby((success) =>
            {
                if (!success) SetButtonsInteractable(true);
            });
        }

        private void OnInviteFriendClicked()
        {
            if (!SteamClient.IsValid) return;

            if (SteamManager.Instance.CurrentLobby.HasValue)
                SteamFriends.OpenGameInviteOverlay(SteamManager.Instance.CurrentLobby.Value.Id);
            else
                SteamFriends.OpenOverlay("friends");
        }

        #endregion

        #region Local Test Logic (ParrelSync 전용)

        private void OnLocalHostClicked()
        {
            SetButtonsInteractable(false);
            ForceLocalTransport(); // 스팀 컴포넌트 완전 삭제 및 KCP 세팅

            var transitionManager = FindFirstObjectByType<RadialTransitionManager>();
            if (transitionManager != null)
                transitionManager.ForceShrinkAndExecute(() => NetworkManager.singleton.StartHost());
            else
                NetworkManager.singleton.StartHost();
        }

        private void OnLocalJoinClicked()
        {
            SetButtonsInteractable(false);
            ForceLocalTransport();

            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient();

            // 연결 실패 대비 타임아웃 코루틴 시작
            StartCoroutine(CheckConnectionTimeoutRoutine());
        }

        private System.Collections.IEnumerator CheckConnectionTimeoutRoutine()
        {
            float timeout = 5f; // 5초 대기

            while (timeout > 0)
            {
                if (Mirror.NetworkClient.isConnected)
                {
                    yield break; // 접속 성공 시 대기 종료
                }

                timeout -= UnityEngine.Time.deltaTime;
                yield return null;
            }

            // 타임아웃 발생 시 강제 중단 및 UI 복구
            if (!Mirror.NetworkClient.isConnected)
            {
                UnityEngine.Debug.LogWarning("[Client] 로컬 서버 연결 실패 (타임아웃). 서버가 열려있는지 확인하십시오.");
                Mirror.NetworkManager.singleton.StopClient();
                SetButtonsInteractable(true);
            }
        }

        #endregion

        private void CloseWindow()
        {
            Destroy(gameObject);
        }
    }
}