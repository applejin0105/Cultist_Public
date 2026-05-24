using System.Collections;
using Core.Data.Enums;
using Core.Events;
using Core.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Managers
{
    public abstract class BaseSceneManager<T> : MonoBehaviour, ISceneLifecycle
        where T : MonoBehaviour, ISceneUIController
    {
        [Header("Scene Setup")]
        [SerializeField] protected string currentScene;
        [SerializeField] protected BGMSoundType sceneBGM = BGMSoundType.Start;

        [Header("Dependencies")]
        [Tooltip("이 씬의 UI를 담당하는 컨트롤러")]
        [SerializeField] protected T uiController;

        protected virtual void Start()
        {
            if (GameSettingManager.GetDeviceMode() || IsDirectSceneLoad()) StartCoroutine(SingleLoadOn());
        }

        protected virtual void OnEnable()
        {
            GlobalSceneEvents.OnSceneSlideStarted += HandleSlideStarted;
            GlobalSceneEvents.OnSceneSlideCompleted += HandleSlideCompleted;
        }

        protected virtual void OnDisable()
        {
            GlobalSceneEvents.OnSceneSlideStarted -= HandleSlideStarted;
            GlobalSceneEvents.OnSceneSlideCompleted -= HandleSlideCompleted;
        }

        private bool IsDirectSceneLoad()
        {
            return SceneManager.sceneCount == 1;
        }

        private void HandleSlideStarted(string fromScene, string toScene)
        {
            if (toScene == currentScene)
            {
                ResetScenePreload();
                EnterScenePreload();
            }
        }

        private void HandleSlideCompleted(string arrivedScene)
        {
            // 애니메이션은 슬라이드와 동시에 이미 진행 중이므로 여기서는 아무 작업도 하지 않음
        }

        #region Single Load

        private IEnumerator SingleLoadOn()
        {
            ResetSceneSingleLoad();
            yield return null;
            EnterSceneSingleLoad();
            Debug.Log($"[{currentScene}] SingleLoadOn: 단일 로드 씬 입장 완료");
        }

        private void SingleLoadOff()
        {
            ExitSceneSingleLoad();
            Debug.Log($"[{currentScene}] SingleLoadOff: 단일 로드 씬 퇴장 완료");
        }

        public virtual void ResetSceneSingleLoad()
        {
            if (uiController != null) uiController.ResetUISingleLoad();
        }

        public virtual void EnterSceneSingleLoad()
        {
            SoundManager.Instance.PlayBgm(sceneBGM);
            if (uiController != null) uiController.PlayEnterUISingleLoad();
        }

        public virtual void ExitSceneSingleLoad()
        {
            if (uiController != null) StartCoroutine(uiController.PlayExitUISingleLoad());
        }

        #endregion

        #region Preload

        public virtual void ResetScenePreload()
        {
            if (uiController != null) uiController.ResetUIPreload();
        }

        public virtual void EnterScenePreload()
        {
            SoundManager.Instance.PlayBgm(sceneBGM);
            if (uiController != null) uiController.PlayEnterUIPreload();
        }

        public virtual void ExitScenePreload()
        {
            if (uiController != null) uiController.PlayExitUIPreload();
        }

        #endregion
    }
}