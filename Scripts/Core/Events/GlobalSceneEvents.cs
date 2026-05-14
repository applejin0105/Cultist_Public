using System;

namespace Core.Events
{
    public static class GlobalSceneEvents
    {
        // 출발 씬 이름, 목적지 씬 이름 -> 슬라이드 시작될 때
        public static Action<string, string> OnSceneSlideStarted;

        // 도착 씬 이름 -> 슬라이드 끝날 때
        public static Action<string> OnSceneSlideCompleted;

        public static void ReleaseAll()
        {
            OnSceneSlideStarted = null;
            OnSceneSlideCompleted = null;
        }
    }
}