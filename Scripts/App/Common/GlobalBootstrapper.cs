using UnityEngine;

namespace App.Common
{
    public class GlobalBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeGlobalManagers()
        {
            var prefabObject = Resources.Load<GameObject>("prefab/Bootstrapper/SoundManager");

            if (GameObject.Find("SoundManager") == null && prefabObject != null)
            {
                var managerObj = Object.Instantiate(prefabObject);
                managerObj.name = "SoundManager";
                Object.DontDestroyOnLoad(managerObj);
                Debug.Log("<color=cyan>[Bootstrapper] SoundManager has been setup</color>");
            }
            else if (prefabObject == null)
            {
                Debug.LogError("[Bootstrapper] Resources 폴더에 'SoundManager' 프리팹이 존재하지 않습니다.");
            }

            prefabObject = Resources.Load<GameObject>("prefab/Bootstrapper/CustomCursor");

            if (prefabObject != null)
            {
                var managerObj = Object.Instantiate(prefabObject);
                managerObj.name = "CustomCursor";
                Object.DontDestroyOnLoad(managerObj);
                Debug.Log("<color=cyan>[Bootstrapper] CustomCursor has been Setup</color>");
            }
            else
            {
                Debug.LogError("[Bootstrapper] Resources 폴더에 'CustomCursor' 프리팹이 존재하지 않습니다.");
            }

            prefabObject = Resources.Load<GameObject>("prefab/Bootstrapper/FixedAspectRatio");

            if (prefabObject != null)
            {
                var managerObj = Object.Instantiate(prefabObject);
                managerObj.name = "FixedAspectRatio";
                Object.DontDestroyOnLoad(managerObj);
                Debug.Log("<color=cyan>[Bootstrapper] FixedAspectRatio has been setup</color>");
            }
            else
            {
                Debug.LogError("[Bootstrapper] Resources 폴더에 'FixedAspectRatio' 프리팹이 존재하지 않습니다.");
            }
        }
    }
}