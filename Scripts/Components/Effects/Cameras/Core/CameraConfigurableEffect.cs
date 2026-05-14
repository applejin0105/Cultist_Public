using System.Collections;

namespace Components.Effects.Cameras.Core
{
    public abstract class CameraConfigurableEffect<T> : CameraEffect
    {
        public abstract void ClearProperty();
        public abstract void SetProperty(T targetValue, float? customDuration = null);
        public abstract void PlayOverrideEffect();
        public abstract IEnumerator PlayWaitableEffect();
    }
}