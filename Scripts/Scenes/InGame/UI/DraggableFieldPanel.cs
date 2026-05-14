using Components.Common.Buttons.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

namespace Scenes.InGame.UI
{
    public class DraggableFieldPanel : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        [Header("Target Setup")]
        [SerializeField] private RectTransform contentRect;
        [SerializeField] private RectTransform viewportRect;

        [Header("UI Controls")]
        [SerializeField] private CompoundButton scaleBtn;
        [SerializeField] private TextMeshProUGUI scaleText;
        [SerializeField] private CompoundButton fullScreenBtn;
        [SerializeField] private TextMeshProUGUI fullScreenText;

        [Header("Movement Settings")]
        [SerializeField] private float panSensitivity = 1.0f;

        [Header("Zoom (Smooth) Settings")]
        [SerializeField] private float zoomSensitivity = 0.1f;
        [SerializeField] private float minScale = 0.1f;
        [SerializeField] private float maxScale = 1.0f;
        [SerializeField] private float smoothSpeed = 10f;

        private Vector2 _lastMousePosition;
        private float _currentScale = 0.5f;
        private float _targetScale = 0.5f;
        private bool _isFullScreen = false;

        // 레이아웃 복구를 위한 원본 데이터 저장
        private Transform _orgParent;
        private Vector2 _orgAnchorMin;
        private Vector2 _orgAnchorMax;
        private Vector2 _orgOffsetMin;
        private Vector2 _orgOffsetMax;
        private int _orgSiblingIndex;

        private RectTransform _fullScreenLayoutParent;
        private GameObject _raycastBlockPanel;

        // [핵심 추가] 포커싱 상태 및 타겟 관리
        private Vector2 _focusTargetLocalPos;
        private bool _isFocusing = false;

        private void Awake()
        {
            if (viewportRect == null) viewportRect = GetComponent<RectTransform>();
            _orgParent = transform.parent;
            _orgSiblingIndex = transform.GetSiblingIndex();

            if (scaleBtn != null) scaleBtn.onLeftClickEvent.AddListener(ResetView);
            if (fullScreenBtn != null) fullScreenBtn.onLeftClickEvent.AddListener(ToggleFullScreen);

            SaveOriginalLayout();
            ResetView();
            UpdateScaleUI();
        }

        public void SetFullScreenDependencies(RectTransform fullScreenParent, GameObject raycastBlocker)
        {
            this._fullScreenLayoutParent = fullScreenParent;
            this._raycastBlockPanel = raycastBlocker;
        }

        private void Update()
        {
            // 1. Zoom Lerp 연산 (목표치에 도달하지 않았을 때만)
            if (Mathf.Abs(_currentScale - _targetScale) > 0.001f)
            {
                _currentScale = Mathf.Lerp(_currentScale, _targetScale, Time.unscaledDeltaTime * smoothSpeed);
                contentRect.localScale = Vector3.one * _currentScale;
                UpdateScaleUI();
            }

            // [핵심 추가] 2. Panning (카메라 이동) Lerp 연산
            if (_isFocusing)
            {
                // 현재 줌 비율이 반영된 타겟의 앵커 포지션
                Vector2 targetAnchoredPos = -_focusTargetLocalPos * _currentScale;

                // 목표 지점과의 거리 계산
                if (Vector2.Distance(contentRect.anchoredPosition, targetAnchoredPos) > 0.5f)
                {
                    contentRect.anchoredPosition = Vector2.Lerp(contentRect.anchoredPosition, targetAnchoredPos,
                        Time.unscaledDeltaTime * smoothSpeed);
                }
                else
                {
                    // 목표 도착 시: 1픽셀 이하의 미세 떨림 방지 및 Update 연산 완전 중단
                    contentRect.anchoredPosition = targetAnchoredPos;
                    _isFocusing = false;
                }
            }
        }

        private void SaveOriginalLayout()
        {
            _orgAnchorMin = viewportRect.anchorMin;
            _orgAnchorMax = viewportRect.anchorMax;
            _orgOffsetMin = viewportRect.offsetMin;
            _orgOffsetMax = viewportRect.offsetMax;
        }

        public void Initialize(int playerId)
        {
            _targetScale = 0.5f;
            _currentScale = 0.5f;
            _isFocusing = false;
            ResetView();
            gameObject.SetActive(true);
        }

        #region 인터페이스 구현 (Panning & Zoom)

        public void OnPointerDown(PointerEventData eventData)
        {
            // [방어 로직] 유저가 마우스를 클릭해 직접 화면을 드래그하려 하면 자동 이동을 즉시 끔
            _isFocusing = false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewportRect.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out _lastMousePosition
            );
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewportRect.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 currentMousePosition))
            {
                Vector2 delta = currentMousePosition - _lastMousePosition;
                contentRect.anchoredPosition += delta;
                _lastMousePosition = currentMousePosition;
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            // [방어 로직] 줌을 조작할 때도 자동 이동 끔
            _isFocusing = false;

            float scroll = eventData.scrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // [핵심 수정] 120, -120 등 폭주하는 스크롤 값을 1, -1로 정규화합니다.
                float normalizedScroll = Mathf.Sign(scroll);
                _targetScale = Mathf.Clamp(_targetScale + (normalizedScroll * zoomSensitivity), minScale, maxScale);
            }
        }
        #endregion

        #region 뷰 제어 로직

        private void UpdateScaleUI()
        {
            if (scaleText != null)
                scaleText.text = $"{Mathf.RoundToInt(_currentScale * 100) * 2}%";
        }

        public void ResetView()
        {
            Debug.Log($"[Focus Debug 7] DraggableFieldPanel - ResetView 호출됨! 카메라가 0,0으로 강제 초기화됩니다.");
            _targetScale = 0.5f;
            _currentScale = 0.5f;
            contentRect.localScale = Vector3.one * 0.5f;
            contentRect.anchoredPosition = Vector2.zero;
            UpdateScaleUI();
        }

        public void CenterToPosition(Vector2 targetLocalPos)
        {
            Debug.Log($"[Focus Debug 6] DraggableFieldPanel - CenterToPosition 수신. 타겟 좌표: {targetLocalPos}");
            _focusTargetLocalPos = targetLocalPos;
            _isFocusing = true;
        }

        public void ToggleFullScreen()
        {
            _isFullScreen = !_isFullScreen;

            if (_isFullScreen)
            {
                if (_fullScreenLayoutParent != null)
                {
                    transform.SetParent(_fullScreenLayoutParent, true);
                    viewportRect.anchorMin = Vector2.zero;
                    viewportRect.anchorMax = Vector2.one;
                    viewportRect.offsetMin = Vector2.zero;
                    viewportRect.offsetMax = Vector2.zero;
                }

                if (fullScreenText != null) fullScreenText.text = "-";
                if (_raycastBlockPanel != null) _raycastBlockPanel.SetActive(true);

                ResetView();
            }
            else
            {
                transform.SetParent(_orgParent, true);
                transform.SetSiblingIndex(_orgSiblingIndex);

                viewportRect.anchorMin = _orgAnchorMin;
                viewportRect.anchorMax = _orgAnchorMax;
                viewportRect.offsetMin = _orgOffsetMin;
                viewportRect.offsetMax = _orgOffsetMax;

                if (fullScreenText != null) fullScreenText.text = "+";
                if (_raycastBlockPanel != null) _raycastBlockPanel.SetActive(false);

                ResetView();
            }
        }

        #endregion
    }
}