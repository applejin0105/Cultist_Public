using UnityEngine;
using UnityEngine.UI;

namespace Scenes.InGame.UI
{
    public class UILineConnection : MonoBehaviour
    {
        [SerializeField] private RectTransform lineRect;
        [Tooltip("선의 두께 (Height)")]
        [SerializeField] private float lineThickness = 10f;

        private void Awake()
        {
            if (lineRect == null) lineRect = GetComponent<RectTransform>();
            // 피벗 강제 셋팅 (왼쪽 중앙 기준)
            lineRect.pivot = new Vector2(0f, 0.5f);
        }

        /// <summary>
        /// 두 점 사이를 잇는 선을 그립니다.
        /// </summary>
        /// <param name="startLocalPos">부모 카드의 로컬 좌표</param>
        /// <param name="endLocalPos">자식 카드의 로컬 좌표</param>
        public void DrawLine(Vector2 startLocalPos, Vector2 endLocalPos)
        {
            // 1. 방향 및 거리 계산
            Vector2 direction = endLocalPos - startLocalPos;
            float distance = direction.magnitude;

            // 2. 각도 계산 (Atan2를 사용하여 라디안을 구하고 각도로 변환)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 3. 선의 크기와 회전 적용
            // 너비(Width)를 거리만큼 늘리고, 높이(Height)는 두께로 유지합니다.
            lineRect.sizeDelta = new Vector2(distance, lineThickness);

            // 위치를 시작점(부모)으로 이동
            lineRect.anchoredPosition = startLocalPos;

            // 목적지를 향해 Z축 회전
            lineRect.localEulerAngles = new Vector3(0, 0, angle);
        }
    }
}