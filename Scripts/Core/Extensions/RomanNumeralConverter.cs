using System;
using System.Text;

namespace Core.Extensions
{
    public static class RomanNumeralConverter
    {
        private static readonly int[] Values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        private static readonly string[] Symbols =
            { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

        public static string ToRoman(int number)
        {
            if (number == 0)
            {
                return "0";
            }

            if (number < 0 || number > 3999)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "변환 가능한 범위는 1에서 3999 사이입니다.");
            }

            StringBuilder result = new StringBuilder();

            for (int i = 0; i < Values.Length; i++)
            {
                while (number >= Values[i])
                {
                    number -= Values[i];
                    result.Append(Symbols[i]);
                }
            }

            return result.ToString();
        }
    }
}