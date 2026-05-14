using System;
using System.Collections.Generic;
using Core.Extensions;
using Domain.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scenes.InGame.UI
{
    public class PlayerStatusUI : MonoBehaviour
    {
        private int _playerNetId;
        private int _pantheonActivatedCount = 0; // ← 추가: 활성화된 Pantheon 수 캐싱

        [SerializeField] private TextMeshProUGUI playerName;

        [SerializeField] private TextMeshProUGUI infCount;
        [SerializeField] private TextMeshProUGUI uniCount;
        [SerializeField] private TextMeshProUGUI monoCount;
        [SerializeField] private TextMeshProUGUI polyCount;
        [SerializeField] private TextMeshProUGUI strCount;
        [SerializeField] private TextMeshProUGUI culCount;

        [SerializeField] private List<Image> pantheon;

        private readonly Color _pantheonActivatedColor = "#ffcc00".ToColor();
        private readonly Color _pantheonDeactivatedColor = "#737373".ToColor();

        public void Init(int playerNetId, string initialName)
        {
            _playerNetId = playerNetId;
            SetName(initialName);

            _pantheonActivatedCount = 0; // ← 초기화

            foreach (var pan in pantheon)
            {
                pan.color = _pantheonDeactivatedColor;
            }

            UpdateSymbol(Symbols.Influence, 0);
            UpdateSymbol(Symbols.Unity, 0);
            UpdateSymbol(Symbols.Monotheism, 0);
            UpdateSymbol(Symbols.Polytheism, 0);
            UpdateSymbol(Symbols.Strength, 0);
        }

        public void UpdateSymbol(Symbols symbol, int changedValue)
        {
            if (symbol == Symbols.Pantheon)
            {
                // 범위 안전 처리 (음수나 슬롯 초과 입력 방어)
                int clamped = Mathf.Clamp(changedValue, 0, pantheon.Count);
                _pantheonActivatedCount = clamped;

                for (int i = 0; i < pantheon.Count; i++)
                {
                    pantheon[i].color = (i < clamped) ? _pantheonActivatedColor : _pantheonDeactivatedColor;
                }

                return;
            }

            string valueStr = changedValue.ToString();

            switch (symbol)
            {
                case Symbols.Influence: infCount.text = valueStr; break;
                case Symbols.Unity: uniCount.text = valueStr; break;
                case Symbols.Monotheism: monoCount.text = valueStr; break;
                case Symbols.Polytheism: polyCount.text = valueStr; break;
                case Symbols.Strength: strCount.text = valueStr; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null);
            }
        }

        public int[] GetCurrentSymbols()
        {
            int[] tmpSymbolsArray =
            {
                SafeParse(infCount.text),
                SafeParse(uniCount.text),
                SafeParse(monoCount.text),
                SafeParse(polyCount.text),
                SafeParse(strCount.text),
                _pantheonActivatedCount // ← 캐싱된 정확한 값 사용
            };
            return tmpSymbolsArray;
        }

        public int GetCurrentCultist()
        {
            return SafeParse(culCount.text);
        }

        // 빈 문자열/잘못된 값 방어용 헬퍼
        private int SafeParse(string s)
        {
            return int.TryParse(s, out int result) ? result : 0;
        }


        public void SetName(string newPlayerName)
        {
            if (playerName == null) return;

            playerName.text = newPlayerName;
        }

        public void UpdateAllSymbols(int[] symbolsArray)
        {
            if (symbolsArray == null || symbolsArray.Length < 6) return;

            UpdateSymbol(Symbols.Influence, symbolsArray[(int)Symbols.Influence]);
            UpdateSymbol(Symbols.Unity, symbolsArray[(int)Symbols.Unity]);
            UpdateSymbol(Symbols.Monotheism, symbolsArray[(int)Symbols.Monotheism]);
            UpdateSymbol(Symbols.Polytheism, symbolsArray[(int)Symbols.Polytheism]);
            UpdateSymbol(Symbols.Strength, symbolsArray[(int)Symbols.Strength]);
            UpdateSymbol(Symbols.Pantheon, symbolsArray[(int)Symbols.Pantheon]);
        }

        public void UpdateCultistCount(int changedValue)
        {
            culCount.text = changedValue.ToString();
        }
    }
}