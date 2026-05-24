using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.Extensions.Editor
{
    public class EcNaming : EditorWindow
    {
        [SerializeField]
        private List<GameObject> insGameObjects = new();
        private string _baseName = "";
        private SerializedProperty _listProp;

        private string _prefix = "";

        private Vector2 _scroll;

        private SerializedObject _so;
        private bool _sortByHierarchy = true;

        private int _startIndex;
        private string _suffix = "";
        private bool _useNumber = true;

        private void OnEnable()
        {
            _so = new SerializedObject(this);
            _listProp = _so.FindProperty("insGameObjects");
        }

        private void OnGUI()
        {
            _so.Update();

            GUILayout.Space(5);

            EditorGUILayout.LabelField("오브젝트 리스트", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_listProp, true);

            GUILayout.Space(5);

            if (GUILayout.Button("현재 선택 오브젝트 추가"))
                foreach (var obj in Selection.gameObjects)
                    if (!insGameObjects.Contains(obj))
                        insGameObjects.Add(obj);

            if (GUILayout.Button("리스트 초기화")) insGameObjects.Clear();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.LabelField("이름 설정", EditorStyles.boldLabel);

            _prefix = EditorGUILayout.TextField("접두사", _prefix);
            _baseName = EditorGUILayout.TextField("기본 이름", _baseName);
            _suffix = EditorGUILayout.TextField("접미사", _suffix);

            _useNumber = EditorGUILayout.Toggle("번호 사용", _useNumber);
            _startIndex = EditorGUILayout.IntField("시작 인덱스", _startIndex);
            _sortByHierarchy = EditorGUILayout.Toggle("계층 기준 정렬", _sortByHierarchy);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            DrawPreview();

            GUILayout.Space(10);

            if (GUILayout.Button("이름 변경 실행")) RenameObjects();

            _so.ApplyModifiedProperties();
        }

        [MenuItem("Tools/ECNaming")]
        private static void Open()
        {
            var win = GetWindow<EcNaming>();
            win.titleContent = new GUIContent("Naming Tool Pro");
            win.Show();
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("이름 미리보기", EditorStyles.boldLabel);

            if (insGameObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("리스트가 비어있습니다.", MessageType.Info);
                return;
            }

            var sorted = GetSortedList();

            var digit = CalculateDigit(sorted.Count);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(150));

            for (var i = 0; i < sorted.Count; i++)
            {
                var previewName = BuildName(i, digit);
                EditorGUILayout.LabelField(sorted[i].name + "  →  " + previewName);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RenameObjects()
        {
            if (insGameObjects.Count == 0)
            {
                Debug.LogWarning("리스트가 비어있습니다.");
                return;
            }

            var sorted = GetSortedList();
            var digit = CalculateDigit(sorted.Count);

            for (var i = 0; i < sorted.Count; i++)
            {
                var obj = sorted[i];
                if (obj == null) continue;

                Undo.RecordObject(obj, "Rename Object");

                obj.name = BuildName(i, digit);
                EditorUtility.SetDirty(obj);
            }

            Debug.Log("이름 변경 완료");
        }

        private List<GameObject> GetSortedList()
        {
            if (!_sortByHierarchy)
                return new List<GameObject>(insGameObjects);

            return insGameObjects
                .Where(x => x != null)
                .OrderBy(x => x.transform.GetSiblingIndex())
                .ToList();
        }

        private int CalculateDigit(int count)
        {
            if (!_useNumber)
                return 0;

            var maxNumber = _startIndex + count - 1;

            return Mathf.Max(1, maxNumber.ToString().Length);
        }

        private string BuildName(int index, int digit)
        {
            var numberPart = "";

            if (_useNumber)
            {
                var value = _startIndex + index;
                numberPart = value.ToString("D" + digit);
            }

            return _prefix + _baseName + numberPart + _suffix;
        }
    }
}