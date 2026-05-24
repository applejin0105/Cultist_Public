using System;
using UnityEngine;

namespace Core.Attributes
{
    // [AttributeUsage]는 이 이름표를 '클래스'나 '메서드'가 아닌 오직 '변수(Field)'에만 붙일 수 있도록 제한하는 C#의 안전장치
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ShowIfAttribute : PropertyAttribute
    {
        public readonly string ConditionExpression;

        public ShowIfAttribute(string conditionExpression)
        {
            ConditionExpression = conditionExpression;
        }
    }
}