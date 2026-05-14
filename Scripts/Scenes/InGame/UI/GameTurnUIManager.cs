using System.Collections;
using TMPro;
using UnityEngine;
using Scenes.InGame.Core;

namespace Scenes.InGame.UI
{
    /// <summary>
    /// GameTurn_Layout 하위의 Turn / Win / Loose / Raycast_Panel CanvasGroup 을 관리한다.
    /// 트리거는 서버 측 RPC 또는 SyncVar Hook 으로 들어온다 (NetworkGameController).
    /// </summary>
    public class GameTurnUIManager : MonoBehaviour
    {
        public static GameTurnUIManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private CanvasGroup raycastBlockCanvasGroup;
        [SerializeField] private CanvasGroup turnCanvasGroup;
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private CanvasGroup winCanvasGroup;
        [SerializeField] private CanvasGroup looseCanvasGroup;

        [Header("Turn Banner Tuning")]
        [SerializeField] private float fadeInDuration = 0.25f;
        [SerializeField] private float holdDuration = 1.20f;
        [SerializeField] private float fadeOutDuration = 0.35f;

        private Coroutine _turnBannerRoutine;
        private bool _gameEndShown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 시작 상태: 모두 알파 0 / 비차단
            SetGroup(raycastBlockCanvasGroup, 0f, false);
            SetGroup(turnCanvasGroup,        0f, false);
            SetGroup(winCanvasGroup,         0f, false);
            SetGroup(looseCanvasGroup,       0f, false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Public API (NetworkGameController 가 호출) ----------

        public void ShowTurnBanner(int activeSeat)
        {
            if (_gameEndShown) return; // 게임 종료된 시점엔 턴 배너 무시

            string name = InGameUIManager.Instance != null
                ? InGameUIManager.Instance.GetDisplayName(activeSeat)
                : $"Player {activeSeat}";

            if (turnText != null) turnText.text = $"{name} Turn";

            if (_turnBannerRoutine != null) StopCoroutine(_turnBannerRoutine);
            _turnBannerRoutine = StartCoroutine(TurnBannerRoutine());
        }

        public void ShowGameEnd(int winnerSeat)
        {
            if (_gameEndShown) return;
            _gameEndShown = true;

            // 진행 중인 턴 배너가 있다면 즉시 정리
            if (_turnBannerRoutine != null)
            {
                StopCoroutine(_turnBannerRoutine);
                _turnBannerRoutine = null;
            }
            SetGroup(turnCanvasGroup, 0f, false);

            // 모달 차단 ON
            SetGroup(raycastBlockCanvasGroup, 1f, true);

            int localSeat = ResolveLocalSeat();
            bool isWinner = localSeat > 0 && localSeat == winnerSeat;

            if (isWinner) SetGroup(winCanvasGroup,   1f, true);
            else          SetGroup(looseCanvasGroup, 1f, true);
        }

        // ---------- Internal ----------

        private IEnumerator TurnBannerRoutine()
        {
            yield return FadeGroup(turnCanvasGroup, 0f, 1f, fadeInDuration);
            // 배너 표시 동안에는 입력은 가로채지 않음 (정보 표시 전용).
            yield return new WaitForSeconds(holdDuration);
            yield return FadeGroup(turnCanvasGroup, 1f, 0f, fadeOutDuration);
            _turnBannerRoutine = null;
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;
            if (duration <= 0f)
            {
                group.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            group.alpha = to;
        }

        private static void SetGroup(CanvasGroup g, float alpha, bool blocking)
        {
            if (g == null) return;
            g.alpha = alpha;
            g.interactable = blocking;
            g.blocksRaycasts = blocking;
        }

        private static int ResolveLocalSeat()
        {
            var localPlayer = Mirror.NetworkClient.localPlayer != null
                ? Mirror.NetworkClient.localPlayer.GetComponent<App.Network.GamePlayer>()
                : null;
            return localPlayer != null ? localPlayer.seatIndex : -1;
        }
    }
}
