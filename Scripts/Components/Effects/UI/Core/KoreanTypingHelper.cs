using System.Collections.Generic;

namespace Components.Effects.UI.Core
{
    public static class KoreanTypingHelper
    {
        private const int HangulBase = 0xAC00;
        private const int HangulEnd = 0xD7A3;
        private static readonly string[] Cho =
            { "ㄱ", "ㄲ", "ㄴ", "ㄷ", "ㄸ", "ㄹ", "ㅁ", "ㅂ", "ㅃ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅉ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ" };

        public static List<string> GetTypingFrames(string sourceText)
        {
            var frames = new List<string>();
            var currentText = "";

            foreach (var c in sourceText)
                if (c >= HangulBase && c <= HangulEnd)
                {
                    var unicode = c - HangulBase;

                    var choIndex = unicode / (21 * 28);
                    var jungIndex = unicode % (21 * 28) / 28;
                    var jongIndex = unicode % 28;

                    frames.Add(currentText + Cho[choIndex]);

                    var choJung = (char)(HangulBase + choIndex * 21 * 28 + jungIndex * 28);
                    frames.Add(currentText + choJung);

                    if (jongIndex > 0) frames.Add(currentText + c);

                    currentText += c;
                }
                else
                {
                    currentText += c;
                    frames.Add(currentText);
                }

            return frames;
        }
    }
}