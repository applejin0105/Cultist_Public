namespace Core.Interfaces
{
    public interface ISceneLifecycle
    {
        // ==========================================
        // Single Load (단일 로드 방식)
        // ==========================================

        /// <summary>
        ///     단일 로드 씬의 상태를 초기화합니다.
        ///     동작: 애니메이션 리셋, 데이터 초기화 등
        /// </summary>
        void ResetSceneSingleLoad();

        /// <summary>
        ///     단일 로드 씬이 화면에 나타나기 시작하거나 준비가 완료되었을 때 호출됩니다.
        ///     동작: BGM 재생 혹은 교체, 단일 로드용 등장 연출(Fade In 등) 시작
        /// </summary>
        void EnterSceneSingleLoad();

        /// <summary>
        ///     단일 로드 씬에서 다른 씬으로 떠날 때 호출됩니다.
        ///     동작: 단일 로드용 퇴장 연출(Fade Out 등) 시작, 진행 중인 연출 강제 종료, 리소스 정리
        /// </summary>
        void ExitSceneSingleLoad();


        // ==========================================
        // Preload (프리로드 방식 - 슬라이드 전환 등)
        // ==========================================

        /// <summary>
        ///     프리로드 씬의 상태를 초기화합니다.
        ///     동작: 슬라이드 전 애니메이션 멈춤, UI 알파값 초기화 등
        /// </summary>
        void ResetScenePreload();

        /// <summary>
        ///     프리로드 씬이 화면(중앙)에 완전히 도착했을 때 호출됩니다.
        ///     동작: BGM 재생 혹은 교체, 프리로드 전용 등장 연출(Start Sequence 등) 시작
        /// </summary>
        void EnterScenePreload();

        /// <summary>
        ///     프리로드 씬에서 다른 씬으로 떠나기 시작할 때 호출됩니다.
        ///     동작: 프리로드 전용 퇴장 연출(End Sequence 등) 시작, 진행 중인 연출 강제 종료
        /// </summary>
        void ExitScenePreload();
    }
}