using System;
using System.Collections.Generic;
using Components.Common.Buttons.Core;
using Core.Extensions;
using Domain.Entities;
using Domain.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Core
{
    [Serializable]
    public struct SymbolDataR
    {
        public Sprite logoImage;
        public Color activatedColor;
        public Color disActivatedColor;
    }

    [Serializable]
    public struct SymbolDataG
    {
        public Sprite logoImage;
        public Color logoColor;
    }

    [Serializable]
    public class CardUIGroupSymbolR
    {
        [Header("Toggle Objects")]
        public GameObject symbolRLayout;

        [Header("Symbols")]
        public GameObject[] symbolRIcon;
        public TextMeshProUGUI[] symbolRCount;
    }

    [Serializable]
    public class CardUIGroupSymbolG
    {
        [Header("Toggle Objects")]
        public GameObject symbolGLayout;

        [Header("Symbols")]
        public GameObject[] symbolGIcon;
        public TextMeshProUGUI[] symbolGCount;
    }

    public class CardUIManager : MonoBehaviour
    {
        private Card _thisCardData;
        private Dictionary<Symbols, int> _symbolRActivatedDictionary = new Dictionary<Symbols, int>();
        private int _symbolRCount;
        private Dictionary<Symbols, int> _symbolGActivatedDictionary = new Dictionary<Symbols, int>();
        private int _symbolGCount;

        [Header("Core Components")]
        [SerializeField] private CompoundButton thisCardButton;
        public CompoundButton ThisCardButton => thisCardButton;

        [Header("Common UI Components")]
        [SerializeField] private TextMeshProUGUI cardName;
        [SerializeField] private Image cardImage;
        [SerializeField] private GameObject cardFront;
        [SerializeField] private GameObject cardBack;
        [SerializeField] private GameObject cardDestroyed;
        [SerializeField] private GameObject cultistLayout;
        [SerializeField] private TextMeshProUGUI cultistCout;
        [SerializeField] private GameObject pantheonLayout;
        [SerializeField] private TextMeshProUGUI effects;
        [SerializeField] private TextMeshProUGUI cultistCountBack;

        [Header("Symbol Groups")]
        [SerializeField] private CardUIGroupSymbolR[] symbolR = new CardUIGroupSymbolR[3];
        [SerializeField] private CardUIGroupSymbolG[] symbolG = new CardUIGroupSymbolG[3];

        // Inf, Uni, Mono, Poly, Str, Pan
        [Header("Icons")]
        [SerializeField] private SymbolDataR[] symbolRIcons = new SymbolDataR[6]; // 딕셔너리 대신 배열 사용
        [SerializeField] private SymbolDataG[] symbolGIcons = new SymbolDataG[6];

        [Header("Color")]
        [SerializeField] private Color activatedLayoutColor;
        [SerializeField] private Color disActivatedLayoutColor;

        [Header("Reveal Highlight (Bloom)")]
        [SerializeField] private CanvasGroup bloomCanvasGroup;
        [SerializeField, Range(0.1f, 20f)] private float bloomFadeSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float bloomMaxAlpha = 1f;

        [SerializeField] private Color revealBloomColor = Color.white; // 일반 Reveal 색상
        [SerializeField] private Color pendingBloomColor = Color.red; // 대기 상태 색상 (빨간색)
        private Image _bloomImage;

        private float _bloomTargetAlpha = 0f;

        [Header("Events")]
        public static Action<Card, bool, bool> onGlobalCardClickedAction;

        private void Awake()
        {
            if (thisCardButton == null)
                thisCardButton = GetComponent<CompoundButton>();

            if (bloomCanvasGroup != null)
            {
                bloomCanvasGroup.alpha = 0f;
                _bloomImage = bloomCanvasGroup.GetComponent<Image>();
                if (_bloomImage == null) _bloomImage = bloomCanvasGroup.GetComponentInChildren<Image>();
            }

            _bloomTargetAlpha = 0f;
        }

        private bool _wasLerping = false;

        private void Update()
        {
            if (bloomCanvasGroup == null) return;
            if (Mathf.Approximately(bloomCanvasGroup.alpha, _bloomTargetAlpha))
            {
                if (_wasLerping)
                {
                    Debug.Log($"[Bloom Lerp 종료:{gameObject.name}] alpha={bloomCanvasGroup.alpha}");
                    _wasLerping = false;
                }

                return;
            }

            if (!_wasLerping)
            {
                Debug.Log($"[Bloom Lerp 시작:{gameObject.name}] {bloomCanvasGroup.alpha} → {_bloomTargetAlpha}");
                _wasLerping = true;
            }

            bloomCanvasGroup.alpha = Mathf.Lerp(
                bloomCanvasGroup.alpha,
                _bloomTargetAlpha,
                Time.unscaledDeltaTime * bloomFadeSpeed
            );

            if (Mathf.Abs(bloomCanvasGroup.alpha - _bloomTargetAlpha) < 0.001f)
                bloomCanvasGroup.alpha = _bloomTargetAlpha;
        }

        private void OnEnable()
        {
            if (thisCardButton != null)
            {
                thisCardButton.onLeftClickEvent.AddListener(OnCardLeftClicked);
                thisCardButton.onRightClickEvent.AddListener(OnCardRightClicked);
            }
        }

        private void OnDisable()
        {
            if (thisCardButton != null)
            {
                thisCardButton.onLeftClickEvent.RemoveListener(OnCardLeftClicked);
                thisCardButton.onRightClickEvent.RemoveListener(OnCardRightClicked);
            }
        }

        public void SetupCard(Card baseData)
        {
            CleanUp();
            _thisCardData = baseData;

            if (_thisCardData == null)
            {
                Debug.LogError("[CardUIManager] 전달받은 baseData가 null입니다. CardId 오류일 수 있습니다.");
                return;
            }

            try
            {
                _symbolRCount = GetSymbolCount(true);
                _symbolGCount = GetSymbolCount(false);

                SymbolLayoutGenerator();
                SetActiveSymbolDictionary(true);
                SetActiveSymbolDictionary(false);
                SetActiveSymbol();

                if (cardName != null) cardName.text = _thisCardData.Name;
                else Debug.LogError("[CardUIManager] cardName (TextMeshProUGUI)이 할당되지 않았습니다.");

                // 런타임 매니저(싱글톤) Null 체크
                if (Systems.Assets.CardAssetManager.Instance == null)
                {
                    Debug.LogError("[CardUIManager] CardAssetManager.Instance가 null입니다. 씬 전환 시 파괴되었거나 초기화되지 않았습니다.");
                }
                else
                {
                    Sprite illust = Systems.Assets.CardAssetManager.Instance.GetSprite(_thisCardData.Id);
                    if (cardImage != null && illust != null) cardImage.sprite = illust;
                }

                if (cultistLayout != null && cultistCout != null)
                {
                    if (_thisCardData.Cultist > 0)
                    {
                        cultistLayout.SetActive(true);
                        cultistCout.text = $"{_thisCardData.Cultist}";
                    }
                }
                else Debug.LogError("[CardUIManager] cultistLayout 또는 cultistCout이 할당되지 않았습니다.");

                if (pantheonLayout != null)
                {
                    if (_thisCardData.SymbolG != null && _thisCardData.SymbolG.Count > 5 &&
                        _thisCardData.SymbolG[5] != 0)
                    {
                        pantheonLayout.SetActive(true);
                    }
                }
                else Debug.LogError("[CardUIManager] pantheonLayout이 할당되지 않았습니다.");

                if (effects != null) effects.text = baseData.Effect;
                else Debug.LogError("[CardUIManager] effects (TextMeshProUGUI)이 할당되지 않았습니다.");

                if (cultistCountBack != null) cultistCountBack.text = RomanNumeralConverter.ToRoman(baseData.Cultist);
                else Debug.LogError("[CardUIManager] cultistCountBack이 할당되지 않았습니다.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardUIManager] 내부 논리 실행 중 에러 발생: {e.Message}\n{e.StackTrace}");
            }
        }

        private int GetSymbolCount(bool isR)
        {
            var tmpList = isR ? _thisCardData.SymbolR : _thisCardData.SymbolG;

            if (tmpList == null) return 0;

            var count = 0;
            foreach (var symbol in tmpList)
            {
                if (symbol != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private void SymbolLayoutGenerator()
        {
            if (_symbolRCount > 0)
                symbolR[_symbolRCount - 1].symbolRLayout.SetActive(true);

            if (_symbolGCount > 0)
                symbolG[_symbolGCount - 1].symbolGLayout.SetActive(true);
        }

        public void ColorChanger(bool isActivated)
        {
            // 데이터가 없으면 무의미한 호출이므로 조기 반환
            if (_thisCardData == null) return;
            if (_symbolRCount <= 0) return;

            int rLayoutIndex = _symbolRCount - 1;
            var currentLayout = symbolR[rLayoutIndex];
            if (currentLayout == null) return;

            // 1. 레이아웃 배경 Image 색 변경
            if (currentLayout.symbolRLayout != null)
            {
                if (currentLayout.symbolRLayout.TryGetComponent<Image>(out var layoutImage))
                {
                    layoutImage.color = isActivated ? activatedLayoutColor : disActivatedLayoutColor;
                }
            }

            // 2. 각 심볼 아이콘과 카운트 텍스트 색 변경
            //    SetActiveSymbol과 동일한 순회 방식으로 인덱스 일치 보장
            int iconIndex = 0;
            foreach (var kvp in _symbolRActivatedDictionary)
            {
                if (iconIndex >= currentLayout.symbolRIcon.Length) break;

                int symbolTypeIndex = (int)kvp.Key;
                SymbolDataR data = symbolRIcons[symbolTypeIndex];
                Color targetColor = isActivated ? data.activatedColor : data.disActivatedColor;

                // 아이콘 색
                var iconGo = currentLayout.symbolRIcon[iconIndex];
                if (iconGo != null && iconGo.TryGetComponent<Image>(out var iconImage))
                {
                    iconImage.color = targetColor;
                }

                // 카운트 텍스트 색
                var countText = currentLayout.symbolRCount[iconIndex];
                if (countText != null)
                {
                    countText.color = targetColor;
                }

                iconIndex++;
            }
        }

        private void ColorChanger(Image targetImage, Color targetColor)
        {
            targetImage.color = targetColor;
        }

        private void SetActiveSymbolDictionary(bool isR)
        {
            var tmpList = isR ? _thisCardData.SymbolR : _thisCardData.SymbolG;
            if (isR)
            {
                _symbolRActivatedDictionary.Clear();
                for (int i = 0; i < 6; i++)
                {
                    if (tmpList[i] != 0)
                    {
                        _symbolRActivatedDictionary.Add((Symbols)i, tmpList[i]);
                    }
                }
            }
            else
            {
                _symbolGActivatedDictionary.Clear();
                for (int i = 0; i < 6; i++)
                {
                    if (tmpList[i] != 0)
                    {
                        _symbolGActivatedDictionary.Add((Symbols)i, tmpList[i]);
                    }
                }
            }
        }

        public void OnCardDestroyed(bool isDestroyed)
        {
            if (isDestroyed)
            {
                SetAllAlphas(front: 0, back: 0, destroyed: 1);
            }
            else
            {
                // 파괴되지 않은 경우 기본적으로 뒷면인지 앞면인지는 OnCardBack에서 결정하므로
                // 여기서는 파괴 레이어만 끕니다.
                if (cardDestroyed != null) cardDestroyed.GetComponent<CanvasGroup>().alpha = 0;
            }
        }

        public void OnCardBack(bool isBack)
        {
            if (isBack)
            {
                SetAllAlphas(front: 0, back: 1, destroyed: 0);
            }
            else
            {
                SetAllAlphas(front: 1, back: 0, destroyed: 0);
            }
        }

        // [추가] 부드러운 뒤집기 연출 (Root 카드 초기화 등에 사용)
        public void PlayFlipAnimation(bool toFront)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(FlipRoutine(toFront));
            }
            else
            {
                // 오브젝트가 비활성화 상태면 코루틴을 돌릴 수 없으므로 즉시 면만 교체
                OnCardBack(!toFront);
            }
        }

        private System.Collections.IEnumerator FlipRoutine(bool toFront)
        {
            float duration = 0.5f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            // 1. 중간(0)까지 줄어듦 (옆면 상태)
            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.5f);
                transform.localScale = new Vector3(Mathf.Lerp(startScale.x, 0, t), startScale.y, startScale.z);
                yield return null;
            }

            // 2. 면 교체
            OnCardBack(!toFront);

            // 3. 다시 원래 크기로 커짐
            elapsed = 0f;
            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.5f);
                transform.localScale = new Vector3(Mathf.Lerp(0, startScale.x, t), startScale.y, startScale.z);
                yield return null;
            }

            transform.localScale = startScale;
        }

        private void SetAllAlphas(float front, float back, float destroyed)
        {
            if (cardFront != null) cardFront.GetComponent<CanvasGroup>().alpha = front;
            if (cardBack != null) cardBack.GetComponent<CanvasGroup>().alpha = back;
            if (cardDestroyed != null) cardDestroyed.GetComponent<CanvasGroup>().alpha = destroyed;

            cardDestroyed.GetComponent<CanvasGroup>().blocksRaycasts = (destroyed > 0);
        }

        public void ResetVisualState()
        {
            SetAllAlphas(front: 1, back: 0, destroyed: 0);
        }

        private void SetActiveSymbol()
        {
            if (_symbolRCount > 0)
            {
                int rLayoutIndex = _symbolRCount - 1;
                int iconIndex = 0;
                foreach (var symbol in _symbolRActivatedDictionary)
                {
                    int symbolTypeIndex = (int)symbol.Key;
                    int symbolCount = symbol.Value;

                    symbolR[rLayoutIndex].symbolRIcon[iconIndex].GetComponent<Image>().sprite =
                        symbolRIcons[symbolTypeIndex].logoImage;
                    symbolR[rLayoutIndex].symbolRCount[iconIndex].text = symbolCount.ToString();
                    iconIndex++;
                }
            }

            if (_symbolGCount > 0)
            {
                int gLayoutIndex = _symbolGCount - 1;
                int iconIndex = 0;
                foreach (var symbol in _symbolGActivatedDictionary)
                {
                    int symbolTypeIndex = (int)symbol.Key;
                    int symbolCount = symbol.Value;

                    symbolG[gLayoutIndex].symbolGIcon[iconIndex].GetComponent<Image>().sprite =
                        symbolGIcons[symbolTypeIndex].logoImage;
                    symbolG[gLayoutIndex].symbolGCount[iconIndex].text = symbolCount.ToString();
                    iconIndex++;
                }
            }
        }

        private void OnCardLeftClicked()
        {
            onGlobalCardClickedAction?.Invoke(_thisCardData, true, false);
        }

        private void OnCardRightClicked()
        {
            onGlobalCardClickedAction?.Invoke(_thisCardData, false, false);
        }

        public void CleanUp()
        {
            // 데이터 및 카운트 초기화
            _thisCardData = null;
            _symbolRCount = 0;
            _symbolGCount = 0;

            // 딕셔너리 비우기
            _symbolRActivatedDictionary.Clear();
            _symbolGActivatedDictionary.Clear();

            // 공통 UI 요소 초기화
            cardName.text = string.Empty;
            cardImage.sprite = null;
            cultistCout.text = string.Empty;

            // 활성화된 개별 레이아웃 비활성화
            if (cultistLayout != null) cultistLayout.SetActive(false);
            if (pantheonLayout != null) pantheonLayout.SetActive(false);

            // 심볼 R 레이아웃 비활성화
            if (symbolR != null)
            {
                for (int i = 0; i < symbolR.Length; i++)
                {
                    if (symbolR[i] != null && symbolR[i].symbolRLayout != null)
                        symbolR[i].symbolRLayout.SetActive(false);
                }
            }

            // 심볼 G 레이아웃 비활성화
            if (symbolG != null)
            {
                for (int i = 0; i < symbolG.Length; i++)
                {
                    if (symbolG[i] != null && symbolG[i].symbolGLayout != null)
                        symbolG[i].symbolGLayout.SetActive(false);
                }
            }

            _bloomTargetAlpha = 0f;
            if (bloomCanvasGroup != null)
            {
                bloomCanvasGroup.alpha = 0f;
            }
        }

        public void SetRevealHighlight(bool isHighlighted)
        {
            _bloomTargetAlpha = isHighlighted ? bloomMaxAlpha : 0f;
            if (_bloomImage != null && isHighlighted) _bloomImage.color = revealBloomColor;
        }

        public void SetPendingHighlight(bool isPending)
        {
            _bloomTargetAlpha = isPending ? bloomMaxAlpha : 0f;
            if (_bloomImage != null && isPending) _bloomImage.color = pendingBloomColor;
        }
    }
}