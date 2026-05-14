using App.Network;
using Data.Models;
using Effects.Keywords;
using UnityEngine;

namespace App.Common
{
    /// <summary>
    /// 게임의 진입점.
    /// 앱 실행 시 최초 1회 실행되어 전역 데이터와 네트워크 매니저를 준비
    /// </summary>
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [Header("Prefabs")] [SerializeField] private GameNetworkManager _networkManagerPrefab;

        private async void Start()
        {
            await CardCatalog.InitializeAsync();
            await EffectRegistry.InitializeAsync();

            //  키워드 자동 주입 / 검증 — CardCatalog와 EffectRegistry가 모두 초기화된 직후 1회.
            //  [희생: N] 텍스트가 있으나 OnRevealCost 가 비어 있는 카드에 Sacrifice 비용을 자동 주입하고,
            //  [Echo] 텍스트와 IsEcho 플래그의 일관성을 검증한다.
            KeywordExpander.Expand(EffectRegistry.Instance);

            if (GameNetworkManager.singleton == null && _networkManagerPrefab != null)
            {
                Instantiate(_networkManagerPrefab);
                Debug.Log("[GameBootstrapper] GameNetworkManager 생성됨.");
            }

            Debug.Log("[GameBootstrapper] 모든 시스템 초기화 완료. 게임 시작 대기.");
        }

        // UI 버튼 연결용
        public void StartHost()
        {
            if (GameNetworkManager.singleton != null)
            {
                GameNetworkManager.singleton.StartHostGame();
                // Host 시작 시 NetworkManager가 자동으로 NetworkGameController를 Spawn
            }
            else
            {
                Debug.LogError("[GameBootstrapper] NetworkManager가 존재하지 않습니다.");
            }
        }

        // UI 버튼 연결용
        public void StartClient()
        {
            if (GameNetworkManager.singleton != null)
            {
                // 로컬 테스트용 localhost, 추후 InputField로 입력받게 수정 가능
                GameNetworkManager.singleton.StartClientGame("localhost");
            }
            else
            {
                Debug.LogError("[GameBootstrapper] NetworkManager가 존재하지 않습니다.");
            }
        }
    }
}