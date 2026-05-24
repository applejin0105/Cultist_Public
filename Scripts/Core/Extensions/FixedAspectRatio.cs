using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Extensions
{
    public class FixedAspectRatio : MonoBehaviour
    {
        public static FixedAspectRatio Instance;

        public float targetAspect = 16.0f / 9.0f;

        private Camera _camera;
        private float _lastHeight;
        private float _lastWidth;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            FindMainCameraAndUpdate();
        }

        private void Update()
        {
            if (_camera == null) return;

            if (!Mathf.Approximately(Screen.width, _lastWidth) || !Mathf.Approximately(Screen.height, _lastHeight))
            {
                OnResolutionChanged?.Invoke();
                UpdateCameraRect();
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public event Action OnResolutionChanged;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FindMainCameraAndUpdate();
        }

        private void FindMainCameraAndUpdate()
        {
            _camera = Camera.main;

            if (_camera != null)
                UpdateCameraRect();
            else
                Debug.LogWarning("Main Camera를 찾을 수 없습니다. 씬에 'MainCamera' 태그가 붙은 카메라가 있는지 확인하세요.");
        }

        private void UpdateCameraRect()
        {
            if (_camera == null) return;

            var windowAspect = Screen.width / (float)Screen.height;
            var scaleHeight = windowAspect / targetAspect;

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            if (scaleHeight < 1.0f)
            {
                var rect = _camera.rect;

                rect.width = 1.0f;
                rect.height = scaleHeight;
                rect.x = 0;
                rect.y = (1.0f - scaleHeight) / 2.0f;

                _camera.rect = rect;
            }
            else
            {
                var scaleWidth = 1.0f / scaleHeight;

                var rect = _camera.rect;

                rect.width = scaleWidth;
                rect.height = 1.0f;
                rect.x = (1.0f - scaleWidth) / 2.0f;
                rect.y = 0;

                _camera.rect = rect;
            }
        }
    }
}