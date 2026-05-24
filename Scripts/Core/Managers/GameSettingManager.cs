using System.Collections.Generic;
using Core.Data.Enums;
using UnityEngine;
using UnityEngine.SceneManagement;

// HertzType이 정의된 곳

namespace Core.Managers
{
    public static class GameSettingManager
    {
        private static bool _isLowEndDeviceMode;
        private static float _bgmVolume;
        private static float _sfxVolume;
        private static HertzType _hertzType = HertzType.Hz60;
        public static HertzType CurrentHertzType => _hertzType;

        // 사용 가능한 해상도 목록을 캐싱할 리스트
        public static List<Resolution> AvailableResolutions { get; } = new();

        /// <summary>
        ///     게임 시작 시(또는 설정 창 열 때) 모니터 지원 해상도를 불러와 초기화합니다.
        /// </summary>
        public static void InitializeResolutions()
        {
            AvailableResolutions.Clear();
            var allResolutions = Screen.resolutions;
            var resolutionCheck = new HashSet<string>();

            foreach (var res in allResolutions)
            {
                // 16:9 비율 필터링을 매니저 레벨에서 수행
                float aspectRatio = (float)res.width / res.height;
                if (Mathf.Abs(aspectRatio - (16f / 9f)) < 0.01f)
                {
                    var key = $"{res.width}x{res.height}";
                    if (!resolutionCheck.Contains(key))
                    {
                        AvailableResolutions.Add(res);
                        resolutionCheck.Add(key);
                    }
                }
            }

            AvailableResolutions.Reverse();
        }

        /// <summary>
        ///     특정 해상도와 창 모드를 적용
        /// </summary>
        /// <param name="resolutionIndex">AvailableResolutions 리스트의 인덱스</param>
        /// <param name="fullScreenMode">전체화면, 창모드, 테두리없는 창모드 등</param>
        public static void SetResolution(int resolutionIndex)
        {
            if (resolutionIndex < 0 || resolutionIndex >= AvailableResolutions.Count) return;
            var targetRes = AvailableResolutions[resolutionIndex];
            Screen.SetResolution(targetRes.width, targetRes.height, Screen.fullScreenMode);
            Debug.Log($"[Setting] 해상도 변경: {targetRes.width}x{targetRes.height}");
        }

        public static void SetScreenType(ScreenType screenType)
        {
            FullScreenMode mode = (screenType == ScreenType.FullScreen)
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;

            Screen.fullScreenMode = mode;

            Screen.SetResolution(Screen.width, Screen.height, mode);

            Debug.Log($"[Setting] 화면 모드 변경: {screenType} (빌드 환경에서만 작동)");
        }

        /// <summary>
        ///     주사율 (목표 프레임 레이트 및 VSync) 설정
        /// </summary>
        public static void SetHertz(HertzType hertzType)
        {
            _hertzType = hertzType;

            // VSync(수직동기화) 설정. 
            // 0이면 VSync 끄기 (직접 프레임 제어), 1이면 모니터 주사율에 맞춤
            QualitySettings.vSyncCount = 0;

            switch (hertzType)
            {
                case HertzType.Hz30:
                    Application.targetFrameRate = 30;
                    break;
                case HertzType.Hz60:
                    Application.targetFrameRate = 60;
                    break;
                case HertzType.Hz120:
                    Application.targetFrameRate = 120;
                    break;
                case HertzType.Hz144:
                    Application.targetFrameRate = 144;
                    break;
                case HertzType.Unlimited:
                    Application.targetFrameRate = -1;
                    break;
                case HertzType.VSync:
                    QualitySettings.vSyncCount = 1;
                    Application.targetFrameRate = -1;
                    break;
            }

            Debug.Log(
                $"[Setting] 프레임 설정: {hertzType} (Target: {Application.targetFrameRate}, VSync: {QualitySettings.vSyncCount})");
        }

        public static void SetDeviceMode(bool isLowEndDeviceMode)
        {
            _isLowEndDeviceMode = isLowEndDeviceMode;
        }

        public static bool GetDeviceMode()
        {
            var isDirectSceneLoad = SceneManager.sceneCount == 1;
            return _isLowEndDeviceMode || isDirectSceneLoad;
        }
    }
}