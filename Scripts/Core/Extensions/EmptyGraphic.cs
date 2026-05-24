using UnityEngine.UI;

namespace Core.Extensions
{
    public class EmptyGraphic : Graphic
    {
        // 머티리얼 및 버텍스 갱신을 무시하여 렌더링 연산 제거
        public override void SetMaterialDirty()
        {
        }

        public override void SetVerticesDirty()
        {
        }

        // 메쉬를 비워 화면에 아무것도 그리지 않음
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}