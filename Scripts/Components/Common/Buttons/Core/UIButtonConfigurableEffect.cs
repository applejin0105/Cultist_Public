using System.Collections;

namespace Components.Common.Buttons.Core
{
    public abstract class UIButtonConfigurableEffect<T> : UIButtonEffect
    {
        /// <summary>
        ///     덮어씌워진 설정(OverrideConfig)을 해제하고 기본 설정으로 되돌립니다.
        /// </summary>
        public abstract void ClearProperty();

        /// <summary>
        ///     이펙트의 목표값과(선택사항) 시간을 코드로 주입합니다.
        /// </summary>
        /// <param name="targetValue"> 버튼의 세부 설정 (Text, Image, RectTransform, Material 등)</param>
        public abstract void SetProperty(T targetValue);

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