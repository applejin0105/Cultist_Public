namespace Domain.Enums
{
    /// <summary>
    /// 카드 Reveal(공개) 호출 사유.
    /// 사유에 따라 비용 검증/페이즈 검증을 우회한다.
    /// </summary>
    public enum RevealReason
    {
        /// <summary>플레이어가 Play.Use 단계에서 직접 클릭 — 비용/페이즈 검증 모두 수행.</summary>
        Manual,

        /// <summary>IsRevealImmediately(=[계시]) 카드의 자동 공개 — 비용/페이즈 검증 우회.</summary>
        Auto,

        /// <summary>(반향) 효과로 인한 강제 공개 — 비용/페이즈 검증 우회. ctx.Reason=Echo로 OnReveal 발화.</summary>
        Echo,

        /// <summary>다른 카드 효과의 명시적 Reveal 명령 — 비용/페이즈 검증 우회.</summary>
        Forced
    }
}
