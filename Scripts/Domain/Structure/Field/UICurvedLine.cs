using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Domain.Structure.Field
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UICurvedLine : MaskableGraphic
    {
        [Header("Curve Settings")]
        public float thickness = 10f;
        public int segments = 20;
        public float curveVerticalForce = 100f;

        private Vector2 _startPoint;
        private Vector2 _endPoint;

        private void Awake()
        {
            RectTransform rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(20000, 20000);
            }
        }

        public void DrawCurve(Vector2 startLocalPos, Vector2 endLocalPos)
        {
            if (Vector2.SqrMagnitude(_startPoint - startLocalPos) < 0.1f &&
                Vector2.SqrMagnitude(_endPoint - endLocalPos) < 0.1f)
            {
                return;
            }

            _startPoint = startLocalPos;
            _endPoint = endLocalPos;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (Vector2.Distance(_startPoint, _endPoint) < 1f) return;

            Vector2 p0 = _startPoint;
            Vector2 p1 = _startPoint + new Vector2(0, curveVerticalForce);
            Vector2 p2 = _endPoint + new Vector2(0, -curveVerticalForce);
            Vector2 p3 = _endPoint;

            List<Vector2> points = new List<Vector2>();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points.Add(CalculateCubicBezierPoint(t, p0, p1, p2, p3));
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                CreateLineSegment(vh, points[i], points[i + 1], thickness);
            }
        }

        private Vector2 CalculateCubicBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0;
            p += 3 * uu * t * p1;
            p += 3 * u * tt * p2;
            p += ttt * p3;

            return p;
        }

        private void CreateLineSegment(VertexHelper vh, Vector2 start, Vector2 end, float width)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width / 2f);

            int startIndex = vh.currentVertCount;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = start - normal;
            vertex.uv0 = new Vector2(0, 0);
            vh.AddVert(vertex);

            vertex.position = start + normal;
            vertex.uv0 = new Vector2(0, 1);
            vh.AddVert(vertex);

            vertex.position = end + normal;
            vertex.uv0 = new Vector2(1, 1);
            vh.AddVert(vertex);

            vertex.position = end - normal;
            vertex.uv0 = new Vector2(1, 0);
            vh.AddVert(vertex);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
    }
}