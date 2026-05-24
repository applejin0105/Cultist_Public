using App.Network;
using Data.Models;
using UnityEngine;

namespace App.Common
{
    /// <summary>
    /// 게임의 진입점.
    /// 앱 실행 시 최초 1회 실행되어 전역 데이터와 네트워크 매니저를 준비한다.
    ///
    /// [전역 초기화 소유자(Owner)]
    /// CardCatalog / EffectRegistry 의 실제 초기화를 "트리거"하는 유일한 공식 지점이다.
    /// 씬 매니저들(CardCollectionManager, CollectionSceneUIManager, LobbyUIManager 등)은
    /// 초기화를 직접 트리거하지 않고, 멱등 보장된 *.InitializeAsync() 를 "완료 대기" 용도로만 호출한다.
    /// </summary>
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [Header("Prefabs")] [SerializeField] private GameNetworkManager _networkManagerPrefab;

        // Unity 의 Start 는 Task 를 반환할 수 없어 async void 가 불가피하다.
        // async void 는 예외가 호출자에게 전파되지 않고 유실되므로, 본문 전체를 try/catch 로 감싼다.
        private async void Start()
        {
            try
            {
                // [초기화 소유자] 앱 시작 시 전역 정적 데이터를 여기서 1회 초기화한다.
                //   InitializeAsync() 는 멱등이므로, 이후 씬 매니저가 같은 메서드를 호출해도
                //   재실행되지 않고 이 시점의 결과(또는 진행 중 Task)를 그대로 공유한다.
                await CardCatalog.InitializeAsync();
                await EffectRegistry.InitializeAsync();

                if (GameNetworkManager.singleton == null && _networkManagerPrefab != null)
                {
                    Instantiate(_networkManagerPrefab);
                    Debug.Log("[GameBootstrapper] GameNetworkManager 생성됨.");
                }

                Debug.Log("[GameBootstrapper] 모든 시스템 초기화 완료. 게임 시작 대기.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameBootstrapper] 초기화 실패: {ex}");
            }
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