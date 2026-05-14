using Components.Common.Buttons.Core;
using UnityEngine;

namespace Scenes.InGame.UI
{
    // Draw/Trade 버튼 제어는 InGameUIManager 가 단독 담당하도록 정리됨.
    // 이 컴포넌트는 더 이상 버튼 클릭/활성 상태에 관여하지 않으며,
    // 씬에서 참조 중인 필드 호환을 위해 클래스 형태만 유지한다.
    public class DrawPhaseUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public GameObject handPanel;
        public CompoundButton tradeButton;
        public CompoundButton drawButton;
    }
}