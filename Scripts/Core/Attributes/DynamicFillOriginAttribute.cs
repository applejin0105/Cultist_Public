using System;
using UnityEngine;

namespace Core.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DynamicFillOriginAttribute : PropertyAttribute
    {
        public readonly string MethodFieldName;

        public DynamicFillOriginAttribute(string methodFieldName)
        {
            MethodFieldName = methodFieldName;
        }
    }
}