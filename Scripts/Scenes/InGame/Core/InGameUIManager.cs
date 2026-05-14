using System.Collections.Generic;
using Components.Common.Buttons.Core;
using Effects.Interfaces;
using Scenes.InGame.UI;
using TMPro;
using UnityEngine;

namespace Scenes.InGame.Core
{
    public class InGameUIManager : MonoBehaviour
    {
        public static InGameUIManager Instance { get; private set; }

        [Header("Top: Player Status Panels")]
        public PlayerStatusUI leftPanel;
        public PlayerStatusUI centerPanel;
        public PlayerStatusUI rightPanel;
        private readonly PlayerStatusUI[] _mappedPanels = new PlayerStatusUI[4];

        [Header("Left: Player Selection")]
        [SerializeField] private Transform selectButtonContainer;
        [SerializeField] private PlayerSelectButton selectButtonPrefab;
        private List<PlayerSelectButton> _instantiatedSelectButtons = new List<PlayerSelectButton>();

        [Header("Center: Fields")]
        [SerializeField] private Transform fieldContentContainer;
        [SerializeField] private GameObject playerFieldPanelPrefab;
        [SerializeField] private TextMeshProUGUI fieldPlayerNameText;

        [Header("Field View Shared References")]
        [SerializeField] private RectTransform fullScreenLayoutParent;
        [SerializeField] private GameObject raycastBlockPanel;

        private Dictionary<int, GameObject> _playerFieldPanels = new Dictionary<int, GameObject>();
        private Dictionary<int, string> _playerNicknames = new Dictionary<int, string>();

        // 원본(접미사 없는) 이름 — Turn 배너 등에서 "(Me)" 가 따라붙지 않은 표시명을 사용하기 위함
        private Dictionary<int, string> _realNames = new Dictionary<int, string>();

        private int _mySeatIndex;
        private int _currentViewTargetSeat = -1;

        private bool _localCanDraw = false;

        [Header("Right: Actions")]
        public CompoundButton tradeButton;
        public CompoundButton drawButton;
        public CompoundButton turnEndButton;
        public TextMeshProUGUI turnEndButtonText;

        public event System.Action OnLocalStatsUpdated;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (tradeButton != null)
            {
                // Trade 버튼은 패널 가시성 토글 전용. 항상 클릭 가능하며 게임 액션은 제출하지 않음.
                tradeButton.IsInteractable = true;
                tradeButton.onLeftClickEvent.AddListener(() =>
                {
                    if (TradeUIManager.Instance != null)
                        TradeUIManager.Instance.ToggleTradePanel();
                });
            }

            if (drawButton != null)
            {
                drawButton.onLeftClickEvent.AddListener(() =>
                {
                    if (TradeUIManager.Instance != null && TradeUIManager.Instance.IsOpen)
                    {
                        TradeUIManager.Instance.CloseTradePanel();
                    }

                    if (_currentViewTargetSeat != _mySeatIndex) OnPlayerSelectButtonClicked(_mySeatIndex);

                    _localCanDraw = false;
                    if (drawButton != null) drawButton.IsInteractable = false;

                    var localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<App.Network.GamePlayer>();
                    if (localPlayer != null) localPlayer.Cmd_SubmitDrawAction((int)DrawPhaseAction.Draw);
                });
            }

            if (turnEndButton != null)
            {
                turnEndButton.onLeftClickEvent.AddListener(() =>
                {
                    var cardManager = FindFirstObjectByType<ClientCardManager>();
                    if (cardManager != null && cardManager.IsTargetSelectionMode && cardManager.TargetSelectionMin == 0)
                    {
                        Debug.Log("[UI] Done 버튼 클릭 -> 선택 종료");
                        cardManager.FinishTargetSelection();
                        return;
                    }

                    var localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<App.Network.GamePlayer>();
                    if (localPlayer != null)
                    {
                        localPlayer.RequestAdvancePhaseOrEndTurn();
                    }
                });
            }

            if (App.Network.NetworkGameController.Instance != null)
            {
                App.Network.NetworkGameController.Instance.OnClientGameStateChanged += RefreshTurnButtonUI;
                RefreshTurnButtonUI();
            }
        }

        private void OnDestroy()
        {
            if (App.Network.NetworkGameController.Instance != null)
            {
                App.Network.NetworkGameController.Instance.OnClientGameStateChanged -= RefreshTurnButtonUI;
            }
        }

        private void RefreshTurnButtonUI()
        {
            if (turnEndButtonText == null || App.Network.NetworkGameController.Instance == null) return;

            var controller = App.Network.NetworkGameController.Instance;
            var localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<App.Network.GamePlayer>();

            if (localPlayer == null) return;

            bool isMyTurn = (controller.CurrentActivePlayerSeat == localPlayer.seatIndex);

            // 1. Draw 버튼 상태 업데이트 (값이 변할 때만)
            bool targetDrawInteractable = isMyTurn && _localCanDraw;
            if (drawButton != null && drawButton.IsInteractable != targetDrawInteractable)
            {
                drawButton.IsInteractable = targetDrawInteractable;
            }

            // 2. Turn End 버튼 및 텍스트 업데이트
            bool targetEndInteractable = false;
            string targetText = "Waiting";

            if (!isMyTurn)
            {
                targetText = "Opponent Turn";
                targetEndInteractable = false;
            }
            else if (controller.CurrentPhaseMain == (int)Domain.Enums.Phase.Main.Play)
            {
                targetEndInteractable = true;

                // [추가] 타겟 선택 모드 (특히 min:0 인 경우) 'Done' 표시
                var cardManager = FindFirstObjectByType<ClientCardManager>();
                if (cardManager != null && cardManager.IsTargetSelectionMode && cardManager.TargetSelectionMin == 0)
                {
                    targetText = "Done";
                }
                else
                {
                    targetText = (controller.CurrentPhaseSub == 0) ? "Play Phase" : "Turn End";
                }
            }

            if (turnEndButton != null && turnEndButton.IsInteractable != targetEndInteractable)
            {
                turnEndButton.IsInteractable = targetEndInteractable;
            }

            if (turnEndButtonText.text != targetText)
            {
                turnEndButtonText.text = targetText;
            }
        }

        public void SetActionButtonsInteractable(bool drawState, bool turnEndState)
        {
            // Trade 버튼은 항상 활성 (패널 토글 전용)이므로 여기서 변경하지 않음.
            if (drawButton != null) drawButton.IsInteractable = drawState;
            if (turnEndButton != null) turnEndButton.IsInteractable = turnEndState;
        }

        public void InitializeUIMapping(int localSeatIndex, int totalPlayers)
        {
            _mappedPanels[localSeatIndex] = centerPanel;
            centerPanel.gameObject.SetActive(true);
            centerPanel.SetName($"Player {localSeatIndex}");

            if (totalPlayers == 2)
            {
                int otherSeat = (localSeatIndex == 1) ? 2 : 1;
                _mappedPanels[otherSeat] = rightPanel;
                rightPanel.gameObject.SetActive(true);
                rightPanel.SetName($"Player {otherSeat}");
                leftPanel.gameObject.SetActive(false);
            }
            else if (totalPlayers == 3)
            {
                int rightSeat = (localSeatIndex % 3) + 1;
                int leftSeat = ((localSeatIndex + 1) % 3) + 1;
                _mappedPanels[rightSeat] = rightPanel;
                _mappedPanels[leftSeat] = leftPanel;
                rightPanel.gameObject.SetActive(true);
                rightPanel.SetName($"Player {rightSeat}");
                leftPanel.gameObject.SetActive(true);
                leftPanel.SetName($"Player {leftSeat}");
            }
        }

        public void InitializeLocalPlayerUI(int mySeatIndex, List<int> allPlayerSeats)
        {
            _mySeatIndex = mySeatIndex;
            _playerNicknames.Clear();

            foreach (Transform child in fieldContentContainer) Destroy(child.gameObject);
            _playerFieldPanels.Clear();

            List<int> sortedSeats = new List<int>(allPlayerSeats);
            if (sortedSeats.Contains(_mySeatIndex))
            {
                sortedSeats.Remove(_mySeatIndex);
                sortedSeats.Insert(0, _mySeatIndex);
            }

            foreach (int seatId in allPlayerSeats)
            {
                GameObject newPanel = Instantiate(playerFieldPanelPrefab, fieldContentContainer);
                newPanel.SetActive(false);

                var draggable = newPanel.GetComponentInChildren<DraggableFieldPanel>();
                if (draggable != null)
                {
                    draggable.SetFullScreenDependencies(fullScreenLayoutParent, raycastBlockPanel);
                }

                _playerFieldPanels.Add(seatId, newPanel);
                _playerNicknames[seatId] = $"Player {seatId}";
            }

            if (_playerFieldPanels.TryGetValue(_mySeatIndex, out var myField))
            {
                myField.SetActive(true);
                myField.transform.SetAsFirstSibling();
                _currentViewTargetSeat = _mySeatIndex;
                UpdateFrameNameText(_mySeatIndex);
            }

            foreach (Transform child in selectButtonContainer) Destroy(child.gameObject);
            _instantiatedSelectButtons.Clear();

            foreach (int targetSeat in sortedSeats)
            {
                PlayerSelectButton newBtn = Instantiate(selectButtonPrefab, selectButtonContainer);
                newBtn.Setup($"Player {targetSeat}", targetSeat, 0, OnPlayerSelectButtonClicked);
                newBtn.gameObject.SetActive(true);
                _instantiatedSelectButtons.Add(newBtn);
            }
        }

        public Transform GetPlayerFieldTransform(int seatIndex)
        {
            if (_playerFieldPanels.TryGetValue(seatIndex, out var panel))
            {
                var pfp = panel.GetComponentInChildren<PlayerFieldPanel>();
                if (pfp != null && pfp.fieldContent != null) return pfp.fieldContent;
                return panel.transform;
            }

            return null;
        }

        public PlayerStatusUI GetPanelBySeat(int seatIndex)
        {
            if (seatIndex < 1 || seatIndex > 3) return null;
            return _mappedPanels[seatIndex];
        }

        private void OnPlayerSelectButtonClicked(int targetSeatIndex)
        {
            // 1. 이미 보고 있는 상대 필드 버튼을 다시 누른 경우 (토글 기능: 분할 화면 닫기)
            if (_currentViewTargetSeat == targetSeatIndex && targetSeatIndex != _mySeatIndex)
            {
                if (_playerFieldPanels.TryGetValue(targetSeatIndex, out var currentOpponentPanel))
                {
                    currentOpponentPanel.SetActive(false); // 상대 필드 끄기
                }

                _currentViewTargetSeat = _mySeatIndex;
                UpdateFrameNameText(_mySeatIndex);
                return;
            }

            if (_currentViewTargetSeat == targetSeatIndex) return;

            // 2. 다른 상대를 보고 있었다면 그 사람의 필드만 끕니다. (내 필드는 끄지 않음!)
            if (_currentViewTargetSeat != _mySeatIndex && _currentViewTargetSeat != -1)
            {
                if (_playerFieldPanels.TryGetValue(_currentViewTargetSeat, out var previousOpponentPanel))
                {
                    previousOpponentPanel.SetActive(false);
                }
            }

            // 3. 새로 선택한 상대(또는 나)의 필드를 켭니다.
            if (_playerFieldPanels.TryGetValue(targetSeatIndex, out var targetPanel))
            {
                targetPanel.SetActive(true);

                // [핵심 보장] 내 필드는 무조건 켜져 있어야 하고, 무조건 왼쪽에 있어야 함
                if (_playerFieldPanels.TryGetValue(_mySeatIndex, out var myPanel))
                {
                    myPanel.SetActive(true);
                    myPanel.transform.SetAsFirstSibling();
                }

                _currentViewTargetSeat = targetSeatIndex;
                UpdateFrameNameText(_mySeatIndex, targetSeatIndex == _mySeatIndex ? -1 : targetSeatIndex);
            }

            var cardManager = FindFirstObjectByType<ClientCardManager>();
            if (cardManager != null)
            {
                cardManager.TriggerFocus(targetSeatIndex);
            }
        }

        private void UpdateFrameNameText(int mySeat, int targetSeat = -1)
        {
            if (fieldPlayerNameText == null) return;

            string myName = _playerNicknames.ContainsKey(mySeat) ? _playerNicknames[mySeat] : $"Player {mySeat}";

            if (targetSeat == -1)
            {
                fieldPlayerNameText.text = myName;
            }
            else
            {
                string targetName = _playerNicknames.ContainsKey(targetSeat)
                    ? _playerNicknames[targetSeat]
                    : $"Player {targetSeat}";
                fieldPlayerNameText.text = $"{myName} & {targetName}";
            }
        }

        public void UpdateRealPlayerName(int seatIndex, string realName, ulong steamId, bool isMe)
        {
            _realNames[seatIndex] = realName;

            string displayStr = isMe ? $"{realName} (Me)" : realName;
            _playerNicknames[seatIndex] = displayStr;

            if (_mappedPanels[seatIndex] != null)
                _mappedPanels[seatIndex].SetName(displayStr);

            if (_playerFieldPanels.TryGetValue(seatIndex, out var fieldPanel))
            {
                var pfp = fieldPanel.GetComponent<PlayerFieldPanel>();
            }

            int foundButtonCount = 0;
            foreach (var btn in _instantiatedSelectButtons)
            {
                if (btn.TargetSeatIndex == seatIndex)
                {
                    foundButtonCount++;
                    Debug.Log($"[InGameUIManager] 버튼 매칭 성공. 시트: {seatIndex}, 전달 텍스트: {displayStr}");
                    btn.UpdateData(displayStr, steamId);
                }
            }

            if (foundButtonCount == 0)
            {
                Debug.LogWarning($"[InGameUIManager] 시트 {seatIndex}에 해당하는 버튼을 _instantiatedSelectButtons에서 찾을 수 없습니다.");
            }

            UpdateFrameNameText(_mySeatIndex, _currentViewTargetSeat == _mySeatIndex ? -1 : _currentViewTargetSeat);
        }

        public void TriggerLocalStatsUpdated()
        {
            OnLocalStatsUpdated?.Invoke();
        }

        /// <summary>
        /// 시트 인덱스에 해당하는 원본 표시명을 반환한다 ("(Me)" 등 접미사 없음).
        /// 이름 동기화가 아직 안 된 경우 "Player {seat}" 폴백.
        /// </summary>
        public string GetDisplayName(int seat)
        {
            if (_realNames.TryGetValue(seat, out var n) && !string.IsNullOrEmpty(n))
                return n;
            return $"Player {seat}";
        }

        public void ShowDrawTradeChoice(bool canDraw, bool canTrade)
        {
            Debug.Log($"[UI] 행동 선택 활성화 - Draw: {canDraw}, Trade: {canTrade}");

            // Draw 버튼만 게임 상태로 제어. Trade 버튼은 패널 토글 전용으로 항상 활성.
            _localCanDraw = canDraw;
            if (drawButton != null) drawButton.IsInteractable = canDraw;
        }

        // Draw 중복 클릭 방지용 헬퍼 (Trade 버튼은 절대 건드리지 않음)
        private void SetDrawTradeButtonsInteractable(bool canDraw, bool _)
        {
            if (drawButton != null) drawButton.IsInteractable = canDraw;
        }
    }
}