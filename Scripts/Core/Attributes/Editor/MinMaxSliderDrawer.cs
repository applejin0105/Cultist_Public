using UnityEditor;
using UnityEngine;

namespace Core.Attributes.Editor
{
    [CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
    public class MinMaxSliderDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var minMax = (MinMaxSliderAttribute)attribute;

            if (property.propertyType == SerializedPropertyType.Vector2)
            {
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

                var floatFieldWidth = 50f;
                var spacing = 5f;

                var minRect = new Rect(position.x, position.y, floatFieldWidth, position.height);
                var sliderRect = new Rect(position.x + floatFieldWidth + spacing, position.y,
                    position.width - floatFieldWidth * 2 - spacing * 2, position.height);
                var maxRect = new Rect(position.x + position.width - floatFieldWidth, position.y, floatFieldWidth,
                    position.height);

                var value = property.vector2Value;
                var min = value.x;
                var max = value.y;

                EditorGUI.BeginChangeCheck();

                min = EditorGUI.FloatField(minRect, min);

                EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, minMax.Min, minMax.Max);

                max = EditorGUI.FloatField(maxRect, max);

                min = Mathf.Clamp(min, minMax.Min, max);
                max = Mathf.Clamp(max, min, minMax.Max);

                if (EditorGUI.EndChangeCheck()) property.vector2Value = new Vector2(min, max);
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "MinMaxSlider는 Vector2 타입에만 사용할 수 있습니다.");
            }
        }
    }
}