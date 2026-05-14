using UnityEngine;
using TMPro;
using App.Network;
using Components.Common.Buttons.Core;
using Mirror;
using UnityEngine.InputSystem;

namespace Scenes.InGame.Debugging
{
    public class DebugManager : MonoBehaviour
    {
        [Header("UI Canvas")]
        [SerializeField] private GameObject debugCanvasGroup;

        [Header("Left Panel: Player Stats")]
        [SerializeField] private TMP_Dropdown playerSelectDropdown;
        [SerializeField] private TMP_InputField cultistInput;
        [SerializeField] private TMP_InputField[] symbolInputs;
        [SerializeField] private CompoundButton applyStatsButton;
        [SerializeField] private CompoundButton gameResetButton;

        [Header("Middle Panel: Deck Edit")]
        [SerializeField] private Transform deckScrollViewContent;
        [SerializeField] private GameObject debugDeckCardPrefab;

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugCanvasGroup != null) debugCanvasGroup.SetActive(false);

            gameResetButton.onLeftClickEvent.AddListener(ResetGame);
            applyStatsButton.onLeftClickEvent.AddListener(ApplyPlayerStats);
            playerSelectDropdown.onValueChanged.AddListener(OnPlayerSelected);
#else
            // 일반 정식 빌드(Release)에서는 이벤트 연결 없이 자신을 즉시 파괴합니다.
            Destroy(gameObject);
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame)
            {
                if (NetworkServer.active)
                {
                    bool isActive = !debugCanvasGroup.activeSelf;
                    debugCanvasGroup.SetActive(isActive);

                    if (isActive) RefreshUI();
                }
                else
                {
                    Debug.LogWarning("<color=yellow>[DebugManager]</color> 클라이언트 환경입니다. 디버그 창은 Host(서버)에서만 열 수 있습니다.");
                }
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private void RefreshUI()
        {
            var gameState = NetworkGameController.Instance?.ServerGameState;
            if (gameState == null) return;

            playerSelectDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();

            foreach (var pState in gameState.Players)
            {
                // [핵심 수정] NetworkGameController를 통해 GamePlayer 컴포넌트를 찾고 닉네임을 가져옵니다.
                var pComp = NetworkGameController.Instance.GetPlayerComponent(pState.Id);
                string playerName = pComp != null ? pComp.steamName : $"Player {pState.Id}";

                // 만약 빈 칸이라면 ID를 같이 출력하여 식별을 돕습니다.
                if (string.IsNullOrEmpty(pComp?.steamName)) playerName = $"ID {pState.Id} (Unknown)";

                options.Add(playerName);
            }

            playerSelectDropdown.AddOptions(options);

            // 데이터가 있다면 첫 번째 플레이어 선택 로드
            if (gameState.Players.Count > 0)
            {
                OnPlayerSelected(playerSelectDropdown.value);
            }
        }

        private void OnPlayerSelected(int index)
        {
            var gameState = NetworkGameController.Instance?.ServerGameState;
            if (gameState == null || gameState.Players.Count <= index) return;

            var targetPlayer = gameState.Players[index];

            cultistInput.text = targetPlayer.Cultist.ToString();
            for (int i = 0; i < 6; i++)
            {
                if (i < symbolInputs.Length)
                    symbolInputs[i].text = targetPlayer.Symbols[i].ToString();
            }

            foreach (Transform child in deckScrollViewContent) Destroy(child.gameObject);

            var deckState = gameState.GetDeckStateById(targetPlayer.Id);
            int order = 1;
            foreach (var cardInstance in deckState.GetAllCards())
            {
                GameObject go = Instantiate(debugDeckCardPrefab, deckScrollViewContent);
                var debugUI = go.GetComponent<DebugDeckCardUI>();
                if (debugUI != null) debugUI.Setup(order, cardInstance);
                order++;
            }
        }

        private void ApplyPlayerStats()
        {
            var gameState = NetworkGameController.Instance?.ServerGameState;
            if (gameState == null) return;

            var targetPlayerId = gameState.Players[playerSelectDropdown.value].Id;
            var pState = gameState.GetPlayerStateById(targetPlayerId);

            if (int.TryParse(cultistInput.text, out int cultist)) pState.SetCultist(cultist);

            int[] newSymbols = new int[6];
            for (int i = 0; i < 6; i++)
            {
                if (i < symbolInputs.Length && int.TryParse(symbolInputs[i].text, out int symVal))
                    newSymbols[i] = symVal;
            }

            pState.SetSymbols(newSymbols);

            NetworkGameController.Instance.TriggerCardSync();
            Debug.Log($"<color=orange>[DebugManager]</color> {targetPlayerId}의 스탯이 강제 변경되었습니다!");
        }

        private void ResetGame()
        {
            if (NetworkManager.singleton != null)
            {
                Debug.Log("<color=red>[DebugManager] 게임 하드 리셋! 로비로 돌아갑니다.</color>");
                NetworkManager.singleton.ServerChangeScene("03_Lobby");
            }
        }
#endif
    }
}