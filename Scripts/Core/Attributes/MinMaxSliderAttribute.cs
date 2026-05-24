using UnityEngine;

namespace Core.Attributes
{
    public sealed class MinMaxSliderAttribute : PropertyAttribute
    {
        public MinMaxSliderAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Min { get; private set; }
        public float Max { get; private set; }
    }
}