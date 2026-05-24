using System.Collections;

namespace Core.Interfaces
{
    public interface ISceneUIController
    {
        void ResetUISingleLoad();
        void PlayEnterUISingleLoad();
        IEnumerator PlayExitUISingleLoad();
        void ResetUIPreload();
        void PlayEnterUIPreload();
        void PlayExitUIPreload();
    }
}