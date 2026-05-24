using UnityEditor;
using UnityEngine;

namespace Core.Attributes.Editor
{
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    public class SerializableDictionaryDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

            var keysProp = property.FindPropertyRelative("keys");
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;

            var totalLines = 1 + 1 + keysProp.arraySize + 1;
            return lineHeight * totalLines + spacing * (totalLines - 1);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var keysProp = property.FindPropertyRelative("keys");
            var valuesProp = property.FindPropertyRelative("values");

            if (keysProp.arraySize != valuesProp.arraySize) valuesProp.arraySize = keysProp.arraySize;

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;

            var foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                var currentY = position.y + lineHeight + spacing;

                var sizeRect = new Rect(position.x, currentY, position.width, lineHeight);
                EditorGUI.BeginChangeCheck();
                var newSize = EditorGUI.IntField(sizeRect, "Size", keysProp.arraySize);
                if (EditorGUI.EndChangeCheck())
                {
                    newSize = Mathf.Max(0, newSize);
                    keysProp.arraySize = newSize;
                    valuesProp.arraySize = newSize;
                }

                currentY += lineHeight + spacing;

                for (var i = 0; i < keysProp.arraySize; i++)
                {
                    var rowRect = EditorGUI.IndentedRect(new Rect(position.x, currentY, position.width, lineHeight));

                    var buttonWidth = 20f;
                    var elementWidth = (rowRect.width - buttonWidth - 5f) / 2f;

                    var keyRect = new Rect(rowRect.x, rowRect.y, elementWidth, lineHeight);
                    var valueRect = new Rect(rowRect.x + elementWidth + 5f, rowRect.y, elementWidth, lineHeight);
                    var btnRect = new Rect(rowRect.x + rowRect.width - buttonWidth, rowRect.y, buttonWidth,
                        lineHeight);

                    EditorGUI.PropertyField(keyRect, keysProp.GetArrayElementAtIndex(i), GUIContent.none);
                    EditorGUI.PropertyField(valueRect, valuesProp.GetArrayElementAtIndex(i), GUIContent.none);

                    if (GUI.Button(btnRect, "-"))
                    {
                        keysProp.DeleteArrayElementAtIndex(i);
                        valuesProp.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    currentY += lineHeight + spacing;
                }

                var addBtnRect = EditorGUI.IndentedRect(new Rect(position.x, currentY, position.width, lineHeight));
                if (GUI.Button(addBtnRect, "+ Add Item"))
                {
                    keysProp.arraySize++;
                    valuesProp.arraySize++;
                }

                EditorGUI.indentLevel--;
            }

            property.serializedObject.ApplyModifiedProperties();

            EditorGUI.EndProperty();
        }
    }
}