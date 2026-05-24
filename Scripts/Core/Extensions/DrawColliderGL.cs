using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Extensions
{
    public enum RenderPipelineType
    {
        BuiltIn,
        URP
    }

    [ExecuteAlways]
    public class DrawColliderGL : MonoBehaviour
    {
        private static Material lineMaterial;
        [Header("렌더링 파이프라인 설정")]
        public RenderPipelineType pipelineType = RenderPipelineType.URP;

        [Header("렌더링 설정")]
        public bool showInEditor = true;
        public bool showInPlayMode = true;

        [Header("계층별 색상")]
        public Color firstLevelColor = Color.red;
        public Color secondLevelColor = Color.cyan;
        public Color deeperLevelColor = Color.green;

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        // Built-in 렌더링 콜백
        private void OnRenderObject()
        {
            if (pipelineType == RenderPipelineType.BuiltIn)
                if (Camera.current != null &&
                    (Camera.current == Camera.main || Camera.current.cameraType == CameraType.SceneView))
                    RenderColliders();
        }

        // URP 렌더링 콜백
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (pipelineType == RenderPipelineType.URP)
                // 메인 카메라(게임 뷰) 또는 씬 뷰 카메라일 때만 렌더링 (UI 전용 카메라는 무시)
                if ((cam.cameraType == CameraType.Game && cam == Camera.main) || cam.cameraType == CameraType.SceneView)
                    RenderColliders();
        }

        private void RenderColliders()
        {
            var isPlaying = Application.isPlaying;
            if (isPlaying && !showInPlayMode) return;
            if (!isPlaying && !showInEditor) return;
            if (transform == null) return;

            CreateLineMaterial();
            lineMaterial.SetPass(0);

            foreach (Transform child in transform)
            {
                if (child == null) continue;
                DrawCollidersRecursive(child, 0);
            }
        }

        private void CreateLineMaterial()
        {
            if (!lineMaterial)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                lineMaterial.SetInt("_Cull", (int)CullMode.Off);
                lineMaterial.SetInt("_ZWrite", 0);
            }
        }

        private void DrawCollidersRecursive(Transform obj, int depth)
        {
            if (obj == null) return;

            var colliders = obj.GetComponents<Collider>();
            if (colliders != null && colliders.Length > 0)
            {
                var colColor = GetColorByDepth(depth);

                foreach (var col in colliders)
                {
                    if (col == null) continue;

                    GL.PushMatrix();
                    GL.MultMatrix(col.transform.localToWorldMatrix);
                    GL.Begin(GL.LINES);
                    GL.Color(colColor);

                    if (col is BoxCollider box) DrawBox(box);
                    else if (col is SphereCollider sphere) DrawSphere(sphere);
                    else if (col is CapsuleCollider capsule) DrawCapsule(capsule);
                    else if (col is MeshCollider mesh) DrawMesh(mesh);

                    GL.End();
                    GL.PopMatrix();
                }
            }

            foreach (Transform child in obj)
            {
                if (child == null) continue;
                DrawCollidersRecursive(child, depth + 1);
            }
        }

        private Color GetColorByDepth(int depth)
        {
            var baseHue = 0.0f;
            var saturation = 0.8f;
            var value = 0.9f;
            var hueStep = 0.07f;
            var hue = (baseHue + depth * hueStep) % 1f;

            return Color.HSVToRGB(hue, saturation, value);
        }

        private void DrawBox(BoxCollider box)
        {
            var c = box.center;
            var s = box.size * 0.5f;

            var p0 = c + new Vector3(-s.x, -s.y, -s.z);
            var p1 = c + new Vector3(s.x, -s.y, -s.z);
            var p2 = c + new Vector3(s.x, -s.y, s.z);
            var p3 = c + new Vector3(-s.x, -s.y, s.z);
            var p4 = c + new Vector3(-s.x, s.y, -s.z);
            var p5 = c + new Vector3(s.x, s.y, -s.z);
            var p6 = c + new Vector3(s.x, s.y, s.z);
            var p7 = c + new Vector3(-s.x, s.y, s.z);

            DrawLine(p0, p1);
            DrawLine(p1, p2);
            DrawLine(p2, p3);
            DrawLine(p3, p0);
            DrawLine(p4, p5);
            DrawLine(p5, p6);
            DrawLine(p6, p7);
            DrawLine(p7, p4);
            DrawLine(p0, p4);
            DrawLine(p1, p5);
            DrawLine(p2, p6);
            DrawLine(p3, p7);
        }

        private void DrawSphere(SphereCollider sphere)
        {
            DrawCircle(sphere.center, sphere.radius, 0);
            DrawCircle(sphere.center, sphere.radius, 1);
            DrawCircle(sphere.center, sphere.radius, 2);
        }

        private void DrawCapsule(CapsuleCollider capsule)
        {
            var r = capsule.radius;
            var h = Mathf.Max(capsule.height, r * 2);
            var c = capsule.center;
            var dir = capsule.direction;

            var offset = Vector3.zero;
            if (dir == 0) offset = new Vector3(h / 2 - r, 0, 0);
            else if (dir == 1) offset = new Vector3(0, h / 2 - r, 0);
            else if (dir == 2) offset = new Vector3(0, 0, h / 2 - r);

            DrawCircle(c + offset, r, (dir + 1) % 3);
            DrawCircle(c - offset, r, (dir + 1) % 3);
            DrawCircle(c + offset, r, (dir + 2) % 3);
            DrawCircle(c - offset, r, (dir + 2) % 3);

            var up = dir == 0 ? Vector3.up : dir == 1 ? Vector3.forward : Vector3.up;
            var right = dir == 0 ? Vector3.forward : dir == 1 ? Vector3.right : Vector3.right;

            DrawLine(c + offset + up * r, c - offset + up * r);
            DrawLine(c + offset - up * r, c - offset - up * r);
            DrawLine(c + offset + right * r, c - offset + right * r);
            DrawLine(c + offset - right * r, c - offset - right * r);
        }

        private void DrawMesh(MeshCollider meshCollider)
        {
            if (meshCollider.sharedMesh == null) return;
            var m = meshCollider.sharedMesh;
            var vertices = m.vertices;
            var triangles = m.triangles;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                DrawLine(vertices[triangles[i]], vertices[triangles[i + 1]]);
                DrawLine(vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                DrawLine(vertices[triangles[i + 2]], vertices[triangles[i]]);
            }
        }

        private void DrawCircle(Vector3 center, float radius, int axisPlane)
        {
            var segments = 24;
            var angleStep = 360f / segments;

            for (var i = 0; i < segments; i++)
            {
                var a1 = i * angleStep * Mathf.Deg2Rad;
                var a2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                var p1 = GetCirclePoint(a1, radius, axisPlane) + center;
                var p2 = GetCirclePoint(a2, radius, axisPlane) + center;

                DrawLine(p1, p2);
            }
        }

        private Vector3 GetCirclePoint(float angle, float radius, int axisPlane)
        {
            var x = Mathf.Cos(angle) * radius;
            var y = Mathf.Sin(angle) * radius;

            if (axisPlane == 0) return new Vector3(x, y, 0);
            if (axisPlane == 1) return new Vector3(x, 0, y);
            return new Vector3(0, x, y);
        }

        private void DrawLine(Vector3 start, Vector3 end)
        {
            GL.Vertex(start);
            GL.Vertex(end);
        }
    }
}