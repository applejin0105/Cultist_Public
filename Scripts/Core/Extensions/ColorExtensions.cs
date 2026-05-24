using UnityEngine;

namespace Core.Extensions
{
    public static class ColorExtensions
    {
        /// <summary>
        ///     Hex 색상 문자열을 Unity Color로 변환.
        /// </summary>
        /// <param name="hexCode">변환할 Hex 문자열 (예: "#FFFFFF", "FFFFFF", "#FFFFFFFF")</param>
        /// <param name="fallbackColor">변환 실패 시 반환할 기본 색상 (지정하지 않으면 Color.clear 반환)</param>
        /// <returns>변환된 Color 구조체</returns>
        public static Color ToColor(this string hexCode, Color fallbackColor = default)
        {
            if (string.IsNullOrEmpty(hexCode)) return fallbackColor == default ? Color.clear : fallbackColor;

            // ColorUtility는 '#' 기호가 필수이므로 누락된 경우 추가
            if (!hexCode.StartsWith("#")) hexCode = "#" + hexCode;

            // 변환 성공 시 해당 색상 반환
            if (ColorUtility.TryParseHtmlString(hexCode, out var color)) return color;

            // 변환 실패 시 경고 로그 출력 및 대체 색상 반환
            Debug.LogWarning($"[ColorExtensions] 잘못된 Hex 색상 코드입니다: {hexCode}");
            return fallbackColor == default ? Color.clear : fallbackColor;
        }
    }
}