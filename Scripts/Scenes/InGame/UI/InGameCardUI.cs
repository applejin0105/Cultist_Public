using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Domain.Entities;
using Domain.Enums;
using UI.Core;
using App.Network;
using Core.Data.Enums;
using Core.Managers;
using Data.Models;
using Mirror;
using Scenes.InGame.Core;
using UnityEngine.UI;

namespace Scenes.InGame.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class InGameCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CardUIManager cardUIManager;
        private RectTransform _rectTransform;

        public int InstanceId { get; private set; }
        public int CardId { get; private set; }
        public int OwnerSeat { get; private set; }
        public Zone CurrentZone { get; private set; }

        public CardStatus CurrentStatus { get; private set; }

        private CanvasGroup _canvasGroup;
        private Transform _originalParent;
        private int _originalSiblingIndex;

        private bool _isDragging = false;
        public bool IsDragging => _isDragging;

        private bool _isRevealedInDraft = false;
        private bool _isPendingPlay = false;
        private bool _isUnfocused = false;
        private bool _isInitialized = false;

        private Vector2 _originalSizeDelta;
        private Vector2 _originalAnchorMin;
        private Vector2 _originalAnchorMax;
        private Vector2 _originalPivot;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Start()
        {
            if (cardUIManager != null && cardUIManager.ThisCardButton != null)
            {
                cardUIManager.ThisCardButton.onLeftClickEvent.RemoveAllListeners();
                cardUIManager.ThisCardButton.onRightClickEvent.RemoveAllListeners();

                cardUIManager.ThisCardButton.onLeftClickEvent.AddListener(OnRawLeftClick);
                cardUIManager.ThisCardButton.onRightClickEvent.AddListener(OnRawRightClick);
            }

            if (InGameUIManager.Instance != null)
                InGameUIManager.Instance.OnLocalStatsUpdated += UpdateRevealVisual;

            if (NetworkGameController.Instance != null)
                NetworkGameController.Instance.OnClientGameStateChanged += UpdateRevealVisual;
        }

        private void OnDestroy()
        {
            if (InGameUIManager.Instance != null)
                InGameUIManager.Instance.OnLocalStatsUpdated -= UpdateRevealVisual;

            if (NetworkGameController.Instance != null)
                NetworkGameController.Instance.OnClientGameStateChanged -= UpdateRevealVisual;
        }

        public void Setup(NetworkDTOs.CardNetData netData, bool isLocalPlayer)
        {
            var prevStatus = CurrentStatus;

            InstanceId = netData.InstanceId;
            CardId = netData.CardId;
            OwnerSeat = netData.OwnerSeat;
            CurrentZone = (Zone)netData.Zone;
            CurrentStatus = (CardStatus)netData.Status;
            _isRevealedInDraft = false;

            if (prevStatus != CurrentStatus)
            {
                SetPendingPlayState(false);
            }

            if (CurrentStatus == CardStatus.Draft)
            {
                cardUIManager.OnCardBack(true);
            }
            else
            {
                bool showFront = ShouldShowFront(netData, isLocalPlayer);

                // [규격화] 뒷면에서 앞면으로 뒤집히는 순간 애니메이션 재생 (단, 초기 생성 시에는 스킵)
                if (_isInitialized && prevStatus == CardStatus.FieldBack && CurrentStatus == CardStatus.FieldFront)
                {
                    cardUIManager.PlayFlipAnimation(true);
                }
                else
                {
                    cardUIManager.OnCardBack(!showFront);
                }
            }

            _isInitialized = true;

            Card baseData = CardCatalog.Instance.Get(CardId);
            cardUIManager.SetupCard(baseData);

            cardUIManager.OnCardDestroyed(netData.Status == (int)CardStatus.FieldDestroyed);

            UpdateRevealVisual();
        }

        #region 이벤트 랩핑을 위한 condition 검증

        private bool CanLeftClick()
        {
            if (_isDragging) return false;

            if (_isPendingPlay) return true;

            if (CurrentStatus == CardStatus.Draft) return true;
            if (CurrentZone == Zone.Trade) return true;
            if (CurrentZone == Zone.Field) return true;
            return false;
        }

        private bool CanRightClick()
        {
            if (_isDragging) return false;
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();

            if (localPlayer == null) return false;
            if (CurrentZone == Zone.Hand && OwnerSeat != localPlayer.seatIndex) return false;
            return true;
        }

        private bool CanDrag()
        {
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            if (localPlayer == null) return false;
            if (OwnerSeat != localPlayer.seatIndex) return false;
            if (CurrentZone != Zone.Hand || CurrentStatus != CardStatus.Hand) return false;
            if (NetworkGameController.Instance == null) return false;
            if (NetworkGameController.Instance.CurrentActivePlayerSeat != localPlayer.seatIndex) return false;

            // [규격화] 통합된 Play 페이즈 내에서는 언제든 드래그(배치) 가능
            if (NetworkGameController.Instance.CurrentPhaseMain != (int)Phase.Main.Play) return false;

            return true;
        }

        private bool CanDropOnThis(InGameCardUI draggedCard)
        {
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            if (localPlayer == null) return false;

            if (CurrentZone != Zone.Field) return false;
            if (OwnerSeat != localPlayer.seatIndex) return false;

            var parentCardData = CardCatalog.Instance.Get(this.CardId);
            if (parentCardData == null) return false;

            int currentChildCount = 0;
            int extraJunctionSum = 0;

            if (NetworkGameController.Instance != null)
            {
                var childrenCountMap = new System.Collections.Generic.Dictionary<int, int>();

                foreach (var netData in NetworkGameController.Instance.SyncCards)
                {
                    // Zone.Field인 모든 카드 (뒷면, 앞면, 파괴됨 포함)를 트리의 노드로 간주
                    if (netData.Zone == (int)Zone.Field && netData.OwnerSeat == localPlayer.seatIndex)
                    {
                        if (netData.ParentInstanceId != 0)
                        {
                            if (!childrenCountMap.ContainsKey(netData.ParentInstanceId))
                                childrenCountMap[netData.ParentInstanceId] = 0;

                            childrenCountMap[netData.ParentInstanceId]++;
                        }
                    }
                }

                // 현재 노드의 실제 자식 수
                if (childrenCountMap.TryGetValue(this.InstanceId, out int childCount))
                {
                    currentChildCount = childCount;
                }

                // 필드 전체의 추가 분기(Extra Junction) 합계 계산
                foreach (var count in childrenCountMap.Values)
                {
                    extraJunctionSum += Mathf.Max(0, count - 1);
                }
            }

            Debug.Log(
                $"<color=cyan>[Client Drop Check]</color> 대상 부모 ID: {this.InstanceId} | 내 자식 수: {currentChildCount} / {parentCardData.Junction} | 필드 총 분기 사용량: {extraJunctionSum} / {localPlayer.maxJunction}");

            int allowedJunction = (this.CurrentStatus == CardStatus.FieldFront) ? parentCardData.Junction : 1;

            // 1단계: 카드 개별 한계 체크
            if (currentChildCount >= parentCardData.Junction)
            {
                Debug.LogWarning(
                    $"<color=red>[Client Drop Block]</color> 이 카드는 더 이상 자식을 가질 수 없습니다. (Junction: {parentCardData.Junction})");
                return false;
            }

            // 2단계: 플레이어 전체 분기 한계 체크 (이미 자식이 1명 이상일 때 추가하려는 경우만 코스트 발생)
            if (currentChildCount >= 1)
            {
                if (extraJunctionSum >= localPlayer.maxJunction)
                {
                    Debug.LogWarning(
                        $"<color=red>[Client Drop Block]</color> 플레이어의 최대 분기(MaxJunction: {localPlayer.maxJunction})에 도달했습니다.");
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region [2단계: 클릭 이벤트 수신부]

        private void OnRawLeftClick()
        {
            if (!CanLeftClick()) return;
            HandleLeftClick();
        }

        private void OnRawRightClick()
        {
            if (!CanRightClick()) return;
            HandleRightClick();
        }

        #endregion

        #region [3단계: 드래그 앤 드랍 이벤트 수신부]

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanDrag()) return;

            _isDragging = true;
            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();

            _originalSizeDelta = _rectTransform.sizeDelta;
            _originalAnchorMin = _rectTransform.anchorMin;
            _originalAnchorMax = _rectTransform.anchorMax;
            _originalPivot = _rectTransform.pivot;

            Canvas rootCanvas = GetComponentInParent<Canvas>().rootCanvas;

            transform.SetParent(rootCanvas.transform, true);
            transform.SetAsLastSibling();

            _canvasGroup.blocksRaycasts = false;

            if (cardUIManager != null && cardUIManager.ThisCardButton != null)
            {
                cardUIManager.ThisCardButton.enabled = false;
                cardUIManager.ThisCardButton.SetBaseScale(new Vector3(0.6f, 0.6f, 1f));
            }

            var cardManager = Object.FindFirstObjectByType<ClientCardManager>();
            if (cardManager != null) cardManager.NotifyDragStart(this.InstanceId);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    _rectTransform.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector3 globalMousePos))
            {
                _rectTransform.position = globalMousePos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            _isDragging = false;
            _canvasGroup.blocksRaycasts = true;

            // 드래그 종료 시 버튼 다시 활성화
            if (cardUIManager != null && cardUIManager.ThisCardButton != null)
            {
                cardUIManager.ThisCardButton.enabled = true;
                // 버튼의 RaycastTarget 복구 (만약 꺼져있다면)
                if (cardUIManager.ThisCardButton.TryGetComponent<Image>(out var img)) img.raycastTarget = true;
            }

            if (transform.parent != _originalParent)
            {
                ReturnToHand();
            }

            var cardManager = Object.FindFirstObjectByType<ClientCardManager>();
            if (cardManager != null) cardManager.NotifyDragEnd(this.InstanceId);
        }

        public void OnDrop(PointerEventData eventData)
        {
            GameObject droppedObj = eventData.pointerDrag;
            if (droppedObj == null) return;

            InGameCardUI draggedCard = droppedObj.GetComponent<InGameCardUI>();
            if (draggedCard == null) return;

            var cardManager = Object.FindFirstObjectByType<ClientCardManager>();
            if (cardManager != null)
            {
                cardManager.NotifyCardAction(this.InstanceId);
            }

            // 내가 필드에 있는 카드(부모)이고, 드래그된 카드가 유효하다면
            if (!CanDropOnThis(draggedCard)) return;

            Debug.Log(
                $"<color=magenta>[Drop Event]</color> 패의 카드(ID:{draggedCard.InstanceId})를 필드 부모(ID:{this.InstanceId}) 위에 드랍. 슬롯 전개 요청!");

            if (cardManager != null)
            {
                cardManager.ReadyToPlayAtSlots(this.InstanceId, draggedCard.InstanceId);
            }

            // 드래그하던 카드는 일단 패로 돌려보냄 (슬롯을 클릭해야 진짜 배치가 됨)
            draggedCard.ReturnToHand();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlaySfx(UISoundType.HoverOn);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlaySfx(UISoundType.HoverOff);
        }

        #endregion

        #region [4단계: 내부 실행 로직]

        public void ReturnToHand()
        {
            if (_originalParent != null)
            {
                transform.SetParent(_originalParent, false);
                transform.SetSiblingIndex(_originalSiblingIndex);

                _rectTransform.sizeDelta = _originalSizeDelta;
                _rectTransform.anchorMin = _originalAnchorMin;
                _rectTransform.anchorMax = _originalAnchorMax;
                _rectTransform.pivot = _originalPivot;

                _rectTransform.anchoredPosition = Vector2.zero;
                _rectTransform.localPosition = Vector3.zero;
            }

            if (_isPendingPlay)
            {
                if (cardUIManager != null && cardUIManager.ThisCardButton != null)
                    cardUIManager.ThisCardButton.SetBaseScale(new Vector3(0.6f, 0.6f, 1f));
            }
            else if (_isUnfocused)
            {
                if (cardUIManager != null && cardUIManager.ThisCardButton != null)
                    cardUIManager.ThisCardButton.SetBaseScale(new Vector3(0.4f, 0.4f, 1f));
            }
            else
            {
                if (cardUIManager != null && cardUIManager.ThisCardButton != null)
                    cardUIManager.ThisCardButton.SetBaseScale(Vector3.one);
            }

            if (cardUIManager != null && cardUIManager.ThisCardButton != null)
            {
                cardUIManager.ThisCardButton.enabled = true;
            }
        }

        private void HandleLeftClick()
        {
            var cardManager = Object.FindFirstObjectByType<ClientCardManager>();

            if (cardManager != null && cardManager.TrySelectTarget(this.InstanceId))
            {
                return;
            }

            if (_isPendingPlay)
            {
                Debug.Log("[UI] 대기 상태 취소");
                SetPendingPlayState(false);

                if (CurrentZone == Zone.Hand)
                {
                    if (cardManager != null) cardManager.CloseSlots();
                }

                return;
            }

            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            if (localPlayer == null) return;

            if (CurrentStatus == CardStatus.Draft)
            {
                if (!_isRevealedInDraft)
                {
                    _isRevealedInDraft = true;
                    cardUIManager.OnCardBack(false);
                    return;
                }

                if (DraftUIManager.Instance != null && DraftUIManager.Instance.HasForceSelectCard())
                {
                    var baseData = CardCatalog.Instance.Get(CardId);
                    if (baseData != null && !baseData.IsForceSelect)
                    {
                        Debug.LogWarning("[UI] 강제 선택 카드가 존재하여 이 카드를 선택할 수 없습니다.");
                        return;
                    }
                }

                localPlayer.Cmd_SubmitKeepCard(InstanceId);

                if (DraftUIManager.Instance != null)
                    DraftUIManager.Instance.CloseDraft();
                return;
            }

            switch (CurrentZone)
            {
                case Zone.Trade:
                    localPlayer.Cmd_SubmitTradeSelect(InstanceId);
                    if (TradeUIManager.Instance != null && TradeUIManager.Instance.IsOpen)
                    {
                        TradeUIManager.Instance.CloseTradePanel();
                    }

                    break;

                case Zone.Field:
                    var controller = NetworkGameController.Instance;
                    if (controller != null && controller.CurrentActivePlayerSeat == localPlayer.seatIndex &&
                        controller.CurrentPhaseMain == (int)Phase.Main.Play && controller.CurrentPhaseSub == 0)
                    {
                        if (CurrentStatus == CardStatus.FieldBack)
                        {
                            // [변경] 하드코딩된 ID 38 체크 대신 OnRevealCost 존재 여부로 범용화
                            var costNodes = EffectRegistry.Instance.GetTrigger(CardId, "OnRevealCost");
                            bool hasCost = costNodes != null && costNodes.Count > 0;

                            if (hasCost)
                            {
                                Debug.Log($"[UI] {CardId} 공개 시도 (비용 발생) -> Bloom Red 활성화");
                                SetPendingPlayState(true);
                            }

                            if (CheckLocalCanReveal())
                            {
                                if (cardManager != null) cardManager.NotifyCardAction(this.InstanceId);

                                localPlayer.Cmd_RevealCard(InstanceId);
                            }
                            else if (hasCost)
                            {
                                SetPendingPlayState(false);
                            }

                            return;
                        }
                        else if (CurrentStatus == CardStatus.FieldFront)
                        {
                            if (cardManager != null) cardManager.NotifyCardAction(this.InstanceId);

                            localPlayer.Cmd_UseCard(InstanceId);
                            return;
                        }
                    }

                    localPlayer.Cmd_SubmitTargets(new int[] { InstanceId });
                    break;
            }
        }

        private void HandleRightClick()
        {
            var localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<App.Network.GamePlayer>();

            bool isOpponentFaceDown = (CurrentZone == Domain.Enums.Zone.Field &&
                                       OwnerSeat != localPlayer?.seatIndex &&
                                       CurrentStatus == Domain.Enums.CardStatus.FieldBack);

            if (DetailPopupUI.Instance != null)
            {
                DetailPopupUI.Instance.SetUp(CardId, isOpponentFaceDown);
            }
        }

        private bool ShouldShowFront(NetworkDTOs.CardNetData netData, bool isLocalPlayer)
        {
            if (netData.IsReveal) return true;
            if (netData.Status == (int)CardStatus.Draft) return true;
            if (netData.Zone == (int)Zone.Trade) return true;
            if (netData.Zone == (int)Zone.Hand && isLocalPlayer) return true;
            return false;
        }

        #endregion

        public bool CheckLocalCanReveal()
        {
            if (CurrentZone != Zone.Field || CurrentStatus != CardStatus.FieldBack) return false;

            var localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<App.Network.GamePlayer>();
            if (localPlayer == null || OwnerSeat != localPlayer.seatIndex) return false;

            var controller = App.Network.NetworkGameController.Instance;
            if (controller == null) return false;

            if (controller.CurrentPhaseMain != (int)Phase.Main.Play || controller.CurrentPhaseSub != 0) return false;

            var baseData = Data.Models.CardCatalog.Instance.Get(CardId);
            if (baseData == null) return false;

            // [Feat] (IsUniqueReveal) 체크: 본인 필드에 이미 앞면인 동일 ID의 카드가 있다면 Reveal 불가
            // 플레이어별 카운팅 — 다른 플레이어가 가지고 있어도 본인은 공개 가능.
            if (baseData.IsUniqueReveal)
            {
                bool alreadyExists = controller.SyncCards.Any(c =>
                    c.CardId == baseData.Id &&
                    c.Status == (int)CardStatus.FieldFront &&
                    c.OwnerSeat == localPlayer.seatIndex);

                if (alreadyExists) return false;
            }

            if (InGameUIManager.Instance == null) return false;
            var myPanel = InGameUIManager.Instance.GetPanelBySeat(localPlayer.seatIndex);
            if (myPanel == null) return false;

            int[] mySymbols = myPanel.GetCurrentSymbols();
            int myCultist = myPanel.GetCurrentCultist();

            if (myCultist <= baseData.Cultist) return false;

            if (mySymbols == null || baseData.SymbolR == null || baseData.SymbolR.Count < 6) return false;

            for (int i = 0; i < 6; i++)
            {
                if (mySymbols[i] < baseData.SymbolR[i]) return false;
            }

            try
            {
                var sourceInstance = new CardInstance(InstanceId, CardId, (Player)localPlayer.seatIndex, Zone.Field,
                    CurrentStatus);
                if (!Effects.Core.EffectValidator.CanPayOnRevealCost(CardId, (Player)localPlayer.seatIndex,
                        sourceInstance))
                {
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EffectValidator] 공개 비용 검증 중 예외 발생 (안전하게 false 처리): {ex.Message}");
                return false;
            }

            return true;
        }

        public void UpdateRevealVisual()
        {
            var localPlayer = NetworkClient.localPlayer?.GetComponent<GamePlayer>();
            bool isShownFront = CurrentStatus == CardStatus.FieldFront ||
                                CurrentStatus == CardStatus.Draft ||
                                CurrentZone == Zone.Trade ||
                                (CurrentZone == Zone.Hand && localPlayer != null && OwnerSeat == localPlayer.seatIndex);

            // 해당 카드의 요구 심볼을 로컬 플레이어가 충족했는지 '순수 심볼'만 검사
            var baseData = CardCatalog.Instance.Get(CardId);
            bool isSymbolMet = baseData != null && CheckSymbolsMetOnly(baseData);

            // 앞면으로 보이는 카드들 (패, 교역소, 공개된 카드 등)
            if (isShownFront)
            {
                cardUIManager.SetRevealHighlight(false);
                cardUIManager.ColorChanger(isSymbolMet); // 덮어놓고 true가 아니라 보유 심볼에 따라 색상 적용!
                return;
            }

            // FieldBack이 아닌 상태(버려지거나 덱에 있는 예외 상황)
            if (CurrentStatus != CardStatus.FieldBack)
            {
                cardUIManager.SetRevealHighlight(false);
                cardUIManager.ColorChanger(isSymbolMet);
                return;
            }

            // 필드에 뒷면(FieldBack)으로 있는 카드
            // 하이라이트(빛남 효과)는 완전히 뒤집을 수 있는지(페이즈, 턴, Cultist, Symbol 모두 만족) 검사
            bool canReveal = CheckLocalCanReveal();
            cardUIManager.SetRevealHighlight(canReveal);

            // 카드 내부 심볼 색상은 오로지 '심볼 충족 여부'만 검사
            cardUIManager.ColorChanger(isSymbolMet);
        }

        public void SetUnfocusedHandState(bool isUnfocused)
        {
            _isUnfocused = isUnfocused;
            if (_isUnfocused)
            {
                if (cardUIManager != null && cardUIManager.ThisCardButton != null)
                {
                    // 선택받지 못한 카드는 0.4 크기로 대폭 축소
                    cardUIManager.ThisCardButton.SetBaseScale(new Vector3(0.4f, 0.4f, 1f));
                }
            }
            else
            {
                // Unfocused가 풀렸을 때, 내가 주인공(Pending)이 아니라면 원래 1.0으로 복구
                if (!_isPendingPlay && cardUIManager != null && cardUIManager.ThisCardButton != null)
                {
                    cardUIManager.ThisCardButton.SetBaseScale(Vector3.one);
                }
            }
        }

        public bool CheckSymbolsMetOnly(Card baseData)
        {
            var localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<App.Network.GamePlayer>();
            if (localPlayer == null) return false;

            if (InGameUIManager.Instance == null) return false;
            var myPanel = InGameUIManager.Instance.GetPanelBySeat(localPlayer.seatIndex);
            if (myPanel == null) return false;

            int[] mySymbols = myPanel.GetCurrentSymbols();

            if (mySymbols == null || baseData.SymbolR == null || baseData.SymbolR.Count < 6) return false;

            for (int i = 0; i < 6; i++)
            {
                if (mySymbols[i] < baseData.SymbolR[i]) return false;
            }

            return true;
        }

        public void SetPendingPlayState(bool isPending)
        {
            _isPendingPlay = isPending;
            if (_isPendingPlay)
            {
                if (cardUIManager != null && cardUIManager.ThisCardButton != null)
                {
                    cardUIManager.ThisCardButton.SetBaseScale(new Vector3(0.6f, 0.6f, 1f));
                    cardUIManager.SetPendingHighlight(true); // 블룸 ON
                }
            }
            else
            {
                if (cardUIManager != null && cardUIManager.ThisCardButton != null)
                {
                    cardUIManager.ThisCardButton.SetBaseScale(Vector3.one);
                    cardUIManager.SetPendingHighlight(false); // 블룸 OFF
                    UpdateRevealVisual();
                }
            }
        }
    }
}