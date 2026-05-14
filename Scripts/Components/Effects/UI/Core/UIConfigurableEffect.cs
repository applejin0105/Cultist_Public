using System.Collections;

namespace Components.Effects.UI.Core
{
    public abstract class UIConfigurableEffect<T> : UIEffect
    {
        /// <summary>
        ///     덮어씌워진 설정(OverrideConfig)을 해제하고 기본 설정으로 되돌립니다.
        /// </summary>
        public abstract void ClearProperty();

        /// <summary>
        ///     이펙트의 목표값과(선택사항) 시간을 코드로 주입합니다.
        /// </summary>
        /// <param name="targetValue">목표로 하는 값 (알파값, 좌표 등)</param>
        /// <param name="customDuration">덮어씌울 시간 (안 넣으면 인스펙터 기본값 사용)</param>
        public abstract void SetProperty(T targetValue, float? customDuration = null);

        /// <summary>
        ///     Override 값으로 효과를 즉시 실행합니다.
        /// </summary>
        public abstract void PlayOverrideEffect();

        /// <summary>
        ///     Override 값으로 효과를 즉시 실행합니다. 이때, 완료를 기다립니다.
        /// </summary>
        public abstract IEnumerator PlayWaitableEffect();
    }
}