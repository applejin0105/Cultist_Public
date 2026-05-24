#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Attributes.Editor
{
    [CustomPropertyDrawer(typeof(DynamicFillOriginAttribute))]
    public class DynamicFillOriginDrawer : PropertyDrawer
    {
        private DynamicFillOriginAttribute TargetAttribute => (DynamicFillOriginAttribute)attribute;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var propertyPath = property.propertyPath;
            var dotIndex = propertyPath.LastIndexOf('.');
            var methodPath = dotIndex < 0
                ? TargetAttribute.MethodFieldName
                : propertyPath.Substring(0, dotIndex) + "." + TargetAttribute.MethodFieldName;

            var methodProp = property.serializedObject.FindProperty(methodPath);

            if (methodProp == null)
            {
                Debug.LogWarning($"[DynamicFillOrigin] '{methodPath}' 필드를 찾을 수 없습니다.");
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var currentMethod = (Image.FillMethod)methodProp.intValue;
            var currentOrigin = property.intValue;

            EditorGUI.BeginChangeCheck();
            int newOrigin;

            switch (currentMethod)
            {
                case Image.FillMethod.Horizontal:
                    newOrigin = (int)(OriginHorizontal)EditorGUI.EnumPopup(position, label,
                        (OriginHorizontal)Mathf.Clamp(currentOrigin, 0, 1));
                    break;
                case Image.FillMethod.Vertical:
                    newOrigin = (int)(OriginVertical)EditorGUI.EnumPopup(position, label,
                        (OriginVertical)Mathf.Clamp(currentOrigin, 0, 1));
                    break;
                case Image.FillMethod.Radial90:
                    newOrigin = (int)(OriginRadial90)EditorGUI.EnumPopup(position, label,
                        (OriginRadial90)Mathf.Clamp(currentOrigin, 0, 3));
                    break;
                case Image.FillMethod.Radial180:
                    newOrigin = (int)(OriginRadial180)EditorGUI.EnumPopup(position, label,
                        (OriginRadial180)Mathf.Clamp(currentOrigin, 0, 3));
                    break;
                case Image.FillMethod.Radial360:
                    newOrigin = (int)(OriginRadial360)EditorGUI.EnumPopup(position, label,
                        (OriginRadial360)Mathf.Clamp(currentOrigin, 0, 3));
                    break;
                default:
                    newOrigin = EditorGUI.IntField(position, label, currentOrigin);
                    break;
            }

            if (EditorGUI.EndChangeCheck()) property.intValue = newOrigin;
        }

        private enum OriginHorizontal
        {
            Left = 0,
            Right = 1
        }

        private enum OriginVertical
        {
            Bottom = 0,
            Top = 1
        }

        private enum OriginRadial90
        {
            BottomLeft = 0,
            TopLeft = 1,
            TopRight = 2,
            BottomRight = 3
        }

        private enum OriginRadial180
        {
            Bottom = 0,
            Left = 1,
            Top = 2,
            Right = 3
        }

        private enum OriginRadial360
        {
            Bottom = 0,
            Right = 1,
            Top = 2,
            Left = 3
        }
    }
}
#endif