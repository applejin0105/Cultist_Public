using UnityEngine;

namespace Core.Extensions
{
    public class IntersectionCalculator : MonoBehaviour
    {
        /// <summary>
        ///     두 물체가 만나는 시간과 지점을 계산합니다.
        /// </summary>
        /// <returns>만나는 시점이 존재하면 true, 만나지 않으면 false를 반환합니다.</returns>
        public static bool CalculateIntersection(
            Vector3 p1, Vector3 v1, Vector3 a1,
            Vector3 p2, Vector3 v2, Vector3 a2,
            out float meetingTime, out Vector3 meetingPoint)
        {
            meetingTime = -1f;
            meetingPoint = Vector3.zero;

            // 물체 1에서 물체 2를 향하는 상대 위치 벡터 및 거리
            var relativePos = p2 - p1;
            var distance = relativePos.magnitude;

            // 거리가 0이면 이미 만난 상태
            if (distance < Mathf.Epsilon)
            {
                meetingTime = 0f;
                meetingPoint = p1;
                return true;
            }

            var direction = relativePos.normalized;

            // 투영을 통한 스칼라 변환
            var relativeVel = v1 - v2;
            var relativeAcc = a1 - a2;

            var A = 0.5f * Vector3.Dot(relativeAcc, direction);
            var B = Vector3.Dot(relativeVel, direction);
            var C = -distance;

            // 이차방정식 풀이
            if (Mathf.Abs(A) < Mathf.Epsilon)
            {
                // 가속도 차이가 없는 경우 (A = 0)
                if (Mathf.Abs(B) < Mathf.Epsilon) return false;

                var t = -C / B;
                if (t < 0) return false;
                meetingTime = t;
            }
            else
            {
                // 근의 공식 적용
                var discriminant = B * B - 4f * A * C;
                if (discriminant < 0) return false; // 허근 (만나지 않음)

                var sqrtDiscriminant = Mathf.Sqrt(discriminant);
                var t1 = (-B + sqrtDiscriminant) / (2f * A);
                var t2 = (-B - sqrtDiscriminant) / (2f * A);

                // 0보다 큰 가장 작은 시간 선택
                if (t1 >= 0 && t2 >= 0) meetingTime = Mathf.Min(t1, t2);
                else if (t1 >= 0) meetingTime = t1;
                else if (t2 >= 0) meetingTime = t2;
                else return false; // 두 근 모두 음수 (과거에 교차함)
            }

            // 산출된 시간 t를 물체 1의 이동 방정식에 대입하여 최종 좌표 도출
            meetingPoint = p1 + v1 * meetingTime + 0.5f * a1 * meetingTime * meetingTime;
            return true;
        }
    }
}