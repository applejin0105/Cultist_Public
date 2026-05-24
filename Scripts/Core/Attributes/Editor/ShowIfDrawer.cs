using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Core.Attributes.Editor
{
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfDrawer : PropertyDrawer
    {
        private ShowIfAttribute ShowIf => (ShowIfAttribute)attribute;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (IsConditionMet(property))
                return EditorGUI.GetPropertyHeight(property, label, true);

            return -EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (IsConditionMet(property)) EditorGUI.PropertyField(position, property, label, true);
        }

        private bool IsConditionMet(SerializedProperty property)
        {
            var expr = ShowIf.ConditionExpression;
            if (string.IsNullOrEmpty(expr)) return true;

            // 1. 띄어쓰기 제거 (파싱을 쉽게 하기 위함)
            expr = expr.Replace(" ", "");

            // 2. || (OR) 기준으로 먼저 그룹을 나눔
            var orGroups = expr.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var orGroup in orGroups)
            {
                var andResult = true;

                // 3. && (AND) 기준으로 세부 조건 분리
                var andConditions = orGroup.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var condition in andConditions)
                    // 하나라도 false면 이 AND 그룹은 실패
                    if (!EvaluateSingleCondition(property, condition))
                    {
                        andResult = false;
                        break;
                    }

                // OR 그룹 중 하나라도 전부 통과(true)했다면 패스
                if (andResult) return true;
            }

            return false;
        }

        // 단일 조건식 평가 (예: "pulseType==DoTween", "isLoop", "!isFade")
        private bool EvaluateSingleCondition(SerializedProperty property, string condition)
        {
            var isNotEquals = condition.Contains("!=");
            var isEquals = condition.Contains("==");

            // A. 비교 연산자가 있는 경우 (==, !=)
            if (isEquals || isNotEquals)
            {
                var separator = isNotEquals ? "!=" : "==";
                var parts = condition.Split(new[] { separator }, StringSplitOptions.None);
                var varName = parts[0];
                var targetValueStr = parts[1];

                var targetProp = FindPropertyRelative(property, varName);
                if (targetProp == null) return true; // 못 찾으면 에러 방지용 true

                var isMatch = CompareStringValue(targetProp, targetValueStr);
                return isNotEquals ? !isMatch : isMatch;
            }
            // B. 비교 연산자가 없는 경우 (bool 변수 단독 사용)
            else
            {
                var isNot = condition.StartsWith("!"); // 앞에 !가 붙었는지 확인
                var varName = isNot ? condition.Substring(1) : condition;

                var targetProp = FindPropertyRelative(property, varName);
                if (targetProp == null) return true;

                var val = targetProp.boolValue;
                return isNot ? !val : val;
            }
        }

        // 경로를 찾아 SerializedProperty를 반환하는 헬퍼 함수
        private SerializedProperty FindPropertyRelative(SerializedProperty property, string fieldName)
        {
            var propertyPath = property.propertyPath;
            var dotIndex = propertyPath.LastIndexOf('.');
            var conditionPath = dotIndex < 0 ? fieldName : propertyPath.Substring(0, dotIndex) + "." + fieldName;

            var foundProp = property.serializedObject.FindProperty(conditionPath);
            if (foundProp == null)
                Debug.LogWarning($"[ShowIf] '{conditionPath}' 변수를 찾을 수 없습니다!");

            return foundProp;
        }

        private bool CompareStringValue(SerializedProperty prop, string stringValue)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Boolean => prop.boolValue.ToString().ToLower() == stringValue.ToLower(),
                // Enum은 현재 선택된 이름(string)과 직접 비교
                SerializedPropertyType.Enum => prop.enumNames[prop.enumValueIndex] == stringValue,
                SerializedPropertyType.Integer => prop.intValue.ToString() == stringValue,
                SerializedPropertyType.Float => prop.floatValue.ToString(CultureInfo.InvariantCulture) == stringValue,
                SerializedPropertyType.String => prop.stringValue == stringValue,
                _ => false
            };
        }
    }
}